using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Xml.Linq;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using ExcelDataReader;
using UglyToad.PdfPig;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;

public sealed partial class SourceIngestionWorkflowExecutor(IWorkspacePathResolutionService paths) : IWorkflowExecutor
{
    private static readonly char[] PathTrimCharacters = [' ', '\t', '\r', '\n', '`', '\'', '"'];

    static SourceIngestionWorkflowExecutor()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.SourceIngestion;

    public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = WorkflowExecutorJson.Deserialize<WorkflowSourceIngestionExecutorSettings>(context.SettingsJson);
        using var document = JsonDocument.Parse(input.PayloadJson);
        var root = document.RootElement;
        var allowedExtensions = NormalizeExtensions(settings.AllowedExtensions);
        var sourceKeys = NormalizeKeys(settings.SourceKeys);
        var maxFiles = Math.Clamp(settings.MaxFiles, 1, 40);
        var maxCharactersPerFile = Math.Clamp(settings.MaxCharactersPerFile, 1000, 80000);
        var remainingCharacters = Math.Clamp(settings.MaxTotalCharacters, 1000, 240000);
        var candidates = CollectCandidates(root, settings, sourceKeys)
            .GroupBy(candidate => $"{candidate.Kind}:{candidate.Value}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        var loaded = new List<WorkflowSourceIngestionDocument>();
        var errors = new List<WorkflowSourceIngestionError>();
        var visitedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var truncated = false;

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (loaded.Count >= maxFiles || remainingCharacters <= 0)
            {
                truncated = true;
                break;
            }

            try
            {
                foreach (var file in ResolveCandidateFiles(candidate, settings, allowedExtensions, maxFiles - loaded.Count))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!visitedFiles.Add(file.FullPath))
                    {
                        continue;
                    }

                    var loadedDocument = ReadSourceDocument(candidate, file, maxCharactersPerFile, remainingCharacters);
                    loaded.Add(loadedDocument);
                    remainingCharacters -= loadedDocument.Text.Length;
                    truncated = truncated || loadedDocument.IsTruncated;
                    if (loaded.Count >= maxFiles || remainingCharacters <= 0)
                    {
                        truncated = true;
                        break;
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                errors.Add(new WorkflowSourceIngestionError(
                    candidate.Key,
                    candidate.Label,
                    candidate.Kind,
                    candidate.Value,
                    candidate.Origin,
                    exception.Message));
            }
        }

        var result = new
        {
            project = TryClone(root, "project"),
            runContext = TryClone(root, "runContext"),
            parentNode = TryClone(root, "parentNode"),
            selectedNodes = TryClone(root, "selectedNodes"),
            parentSubtree = TryClone(root, "parentSubtree"),
            manualInput = TryClone(root, "manualInput"),
            sourceSummary = BuildSourceSummary(loaded, errors, truncated),
            documents = loaded,
            sourceDocuments = loaded,
            sourceErrors = errors,
            loadedSourceCount = loaded.Count,
            failedSourceCount = errors.Count,
            isTruncated = truncated
        };

        return ValueTask.FromResult(WorkflowExecutorJson.Result(context, result));
    }

}
