using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;

public sealed class SourceIngestionWorkflowExecutor : IWorkflowExecutor
{
    private readonly WorkflowSourceCandidateCollector candidateCollector;
    private readonly WorkflowSourceFileResolver fileResolver;
    private readonly WorkflowSourceDocumentReader documentReader;
    private readonly WorkflowSourceFileContentIdentityResolver contentIdentityResolver;

    public SourceIngestionWorkflowExecutor(
        IWorkspacePathResolutionService paths,
        IWorkspaceDocumentMarkdownConverter documentMarkdownConverter,
        IExternalTargetPathRegistry externalTargetPathRegistry,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory)
        : this(
            new WorkflowSourceCandidateCollector(),
            new WorkflowSourceFileResolver(
                paths,
                externalTargetPathRegistry,
                physicalPathPolicyFactory),
            new WorkflowSourceDocumentReader(documentMarkdownConverter),
            new WorkflowSourceFileContentIdentityResolver())
    {
    }

    internal SourceIngestionWorkflowExecutor(
        WorkflowSourceCandidateCollector candidateCollector,
        WorkflowSourceFileResolver fileResolver,
        WorkflowSourceDocumentReader documentReader,
        WorkflowSourceFileContentIdentityResolver contentIdentityResolver)
    {
        this.candidateCollector = candidateCollector ?? throw new ArgumentNullException(nameof(candidateCollector));
        this.fileResolver = fileResolver ?? throw new ArgumentNullException(nameof(fileResolver));
        this.documentReader = documentReader ?? throw new ArgumentNullException(nameof(documentReader));
        this.contentIdentityResolver = contentIdentityResolver ?? throw new ArgumentNullException(nameof(contentIdentityResolver));
    }

    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.SourceIngestion;

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
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
        var candidates = candidateCollector.Collect(root, settings, sourceKeys)
            .GroupBy(candidate => $"{candidate.Kind}:{candidate.Value}", StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        var loaded = new List<WorkflowSourceIngestionDocument>();
        var errors = new List<WorkflowSourceIngestionError>();
        var visitedFiles = new HashSet<string>(StringComparer.Ordinal);
        var visitedContent = new HashSet<WorkflowSourceFileContentKey>();
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
                foreach (var file in fileResolver.ResolveCandidateFiles(
                             candidate,
                             settings,
                             allowedExtensions,
                             maxFiles - loaded.Count))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!visitedFiles.Add(file.FullPath))
                    {
                        continue;
                    }

                    var contentIdentity = await contentIdentityResolver
                        .ResolveAsync(file, cancellationToken)
                        .ConfigureAwait(false);
                    if (visitedContent.Contains(contentIdentity.Key))
                    {
                        continue;
                    }

                    var effectiveCharacterBudget = Math.Min(maxCharactersPerFile, remainingCharacters);
                    var readResult = await documentReader
                        .ReadAsync(file, effectiveCharacterBudget, cancellationToken)
                        .ConfigureAwait(false);
                    await contentIdentityResolver
                        .EnsureUnchangedAsync(file, contentIdentity, cancellationToken)
                        .ConfigureAwait(false);
                    var loadedDocument = CreateDocument(candidate, file, readResult);
                    loaded.Add(loadedDocument);
                    visitedContent.Add(contentIdentity.Key);
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
                    fileResolver.ToSafeDisplayPath(candidate.Value),
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

        return WorkflowExecutorJson.Result(context, result);
    }

    private static WorkflowSourceIngestionDocument CreateDocument(
        WorkflowSourceCandidate candidate,
        WorkflowSourceIngestionFile file,
        WorkflowSourceReadResult result)
        => new(
            candidate.Key,
            candidate.Label,
            candidate.Kind,
            candidate.Origin,
            file.DisplayPath,
            file.FileName,
            Path.GetExtension(file.FullPath).ToLowerInvariant(),
            result.Text,
            result.TotalCharacters,
            result.IsTruncated,
            result.ExtractionStatus);

    private static JsonElement? TryClone(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value)
            ? value.Clone()
            : null;

    private static string BuildSourceSummary(
        IReadOnlyList<WorkflowSourceIngestionDocument> loaded,
        IReadOnlyList<WorkflowSourceIngestionError> errors,
        bool truncated)
    {
        var sourceText = loaded.Count == 1 ? "source" : "sources";
        var summary = $"Loaded {loaded.Count} {sourceText}";
        if (errors.Count > 0)
        {
            summary += $" with {errors.Count} error(s)";
        }

        if (truncated)
        {
            summary += "; content was truncated to workflow limits";
        }

        return summary + ".";
    }

    private static IReadOnlySet<string> NormalizeKeys(IReadOnlyList<string> sourceKeys)
        => sourceKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlySet<string> NormalizeExtensions(IReadOnlyList<string> extensions)
        => extensions
            .Where(extension => !string.IsNullOrWhiteSpace(extension))
            .Select(extension => extension.Trim().StartsWith(".", StringComparison.Ordinal)
                ? extension.Trim().ToLowerInvariant()
                : "." + extension.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

}
