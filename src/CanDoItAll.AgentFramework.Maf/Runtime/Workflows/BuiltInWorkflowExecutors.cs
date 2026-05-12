using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExcelDataReader;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tools.Documents;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig;
using DocumentCellWrite = CanDoItAll.Tools.Documents.SpreadsheetCellWrite;
using DocumentRangeWrite = CanDoItAll.Tools.Documents.SpreadsheetRangeWrite;
using DocumentWriteRequest = CanDoItAll.Tools.Documents.SpreadsheetWriteRequest;

namespace CanDoItAll.AgentFramework.Maf;

public static class BuiltInWorkflowExecutorDescriptors
{
    private static readonly WorkflowValueShape JsonShape = new(
        WorkflowValueShapeKind.Json,
        "{}",
        "JSON payload");

    public static WorkflowExecutorDescriptor StorageFile { get; } = Create(
        WorkflowExecutorIds.StorageFile,
        "Workspace files",
        "Lists, reads, writes, appends, searches, stats, and diffs files through the workspace storage boundary.",
        WorkflowExecutorCategoryKind.Storage,
        "folder_open",
        "builtin.storage-file",
        new WorkflowStorageFileExecutorSettings());

    public static WorkflowExecutorDescriptor SourceIngestion { get; } = Create(
        WorkflowExecutorIds.SourceIngestion,
        "Source ingestion",
        "Loads explicit project-structure workflow file and folder sources into bounded text for downstream LLM nodes.",
        WorkflowExecutorCategoryKind.Data,
        "drive_folder_upload",
        "builtin.source-ingest",
        new WorkflowSourceIngestionExecutorSettings(),
        defaultPolicy: WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 90, CaptureOutputArtifact = true });

    public static WorkflowExecutorDescriptor HttpFetch { get; } = Create(
        WorkflowExecutorIds.HttpFetch,
        "HTTP fetch",
        "Fetches bounded HTTP/HTTPS content with explicit method, headers, body, and size settings.",
        WorkflowExecutorCategoryKind.Http,
        "public",
        "builtin.http-fetch",
        new WorkflowHttpExecutorSettings(),
        defaultPolicy: WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 20 });

    public static WorkflowExecutorDescriptor Spreadsheet { get; } = Create(
        WorkflowExecutorIds.Spreadsheet,
        "Spreadsheet",
        "Inspects, reads, writes, and Markdown-renders XLSX workbooks through the document wrapper.",
        WorkflowExecutorCategoryKind.Spreadsheet,
        "table_chart",
        "builtin.spreadsheet",
        new WorkflowSpreadsheetExecutorSettings(),
        defaultPolicy: WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 60 });

    public static WorkflowExecutorDescriptor ProjectStructure { get; } = Create(
        WorkflowExecutorIds.ProjectStructure,
        "Project structure",
        "Reads project structures and creates typed asset nodes through the project-structure service.",
        WorkflowExecutorCategoryKind.ProjectStructure,
        "account_tree",
        "builtin.project-structure",
        new WorkflowProjectStructureExecutorSettings(),
        defaultPolicy: WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 45 });

    public static WorkflowExecutorDescriptor ImageGeneration { get; } = Create(
        WorkflowExecutorIds.ImageGeneration,
        "Image generation",
        "Prepares image generation through configured image providers and managed workspace output.",
        WorkflowExecutorCategoryKind.Image,
        "image",
        "builtin.image-generation",
        new WorkflowImageGenerationExecutorSettings(),
        defaultPolicy: WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 120, CaptureOutputArtifact = true });

    public static IReadOnlyList<WorkflowExecutorDescriptor> Planned { get; } =
    [
        CreatePlanned(WorkflowExecutorIds.JsonTransform, "JSON transform", "Transforms JSON using a typed projection expression.", WorkflowExecutorCategoryKind.Data, "data_object", "planned.json-transform"),
        CreatePlanned(WorkflowExecutorIds.MarkdownRender, "Markdown render", "Builds Markdown from structured workflow values.", WorkflowExecutorCategoryKind.Markdown, "article", "planned.markdown-render"),
        CreatePlanned(WorkflowExecutorIds.Delay, "Delay", "Waits or schedules a workflow continuation.", WorkflowExecutorCategoryKind.Utility, "timer", "planned.delay"),
        CreatePlanned(WorkflowExecutorIds.ApprovalRequest, "Approval request", "Creates a human approval/request node during workflow execution.", WorkflowExecutorCategoryKind.Human, "approval", "planned.approval-request"),
        CreatePlanned(WorkflowExecutorIds.CommandProcess, "Command process", "Runs a bounded local process through the existing workspace command service.", WorkflowExecutorCategoryKind.Command, "terminal", "planned.command-process")
    ];

    private static WorkflowExecutorDescriptor Create<TSettings>(
        WorkflowExecutorId id,
        string name,
        string description,
        WorkflowExecutorCategoryKind category,
        string iconName,
        string setupRendererKey,
        TSettings defaultSettings,
        WorkflowExecutorExecutionPolicy? defaultPolicy = null)
    {
        return new WorkflowExecutorDescriptor(
            id,
            name,
            description,
            category,
            iconName,
            setupRendererKey,
            WorkflowValueShape.Text,
            JsonShape,
            "{\"type\":\"object\"}",
            WorkflowExecutorJson.Serialize(defaultSettings),
            defaultPolicy ?? WorkflowExecutorExecutionPolicy.Default,
            IsImplemented: true);
    }

    private static WorkflowExecutorDescriptor CreatePlanned(
        WorkflowExecutorId id,
        string name,
        string description,
        WorkflowExecutorCategoryKind category,
        string iconName,
        string setupRendererKey)
    {
        return new WorkflowExecutorDescriptor(
            id,
            name,
            description,
            category,
            iconName,
            setupRendererKey,
            WorkflowValueShape.Text,
            JsonShape,
            "{\"type\":\"object\"}",
            "{}",
            WorkflowExecutorExecutionPolicy.Default,
            IsImplemented: false);
    }
}

public sealed class WorkspaceFileWorkflowExecutor(IWorkspaceFileService files) : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.StorageFile;

    public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = WorkflowExecutorJson.Deserialize<WorkflowStorageFileExecutorSettings>(context.SettingsJson);
        object result = settings.Operation switch
        {
            WorkflowStorageFileOperation.List => EnsureSucceeded(files.ListFiles(EmptyToNull(settings.Path), settings.SearchPattern, settings.MaxResults)),
            WorkflowStorageFileOperation.Stat => EnsureSucceeded(files.StatPath(Require(settings.Path, nameof(settings.Path)))),
            WorkflowStorageFileOperation.ReadText => EnsureSucceeded(files.ReadTextFile(Require(settings.Path, nameof(settings.Path)), settings.MaxCharacters)),
            WorkflowStorageFileOperation.WriteText => EnsureSucceeded(files.WriteTextFile(Require(settings.Path, nameof(settings.Path)), WorkflowInputPayloadText.Resolve(settings.Content, settings.ContentFromInput, input), settings.Overwrite)),
            WorkflowStorageFileOperation.AppendText => EnsureSucceeded(files.AppendTextFile(Require(settings.Path, nameof(settings.Path)), WorkflowInputPayloadText.Resolve(settings.Content, settings.ContentFromInput, input))),
            WorkflowStorageFileOperation.SearchText => EnsureSucceeded(files.SearchText(Require(settings.Query, nameof(settings.Query)), EmptyToNull(settings.Path), settings.MaxResults)),
            WorkflowStorageFileOperation.DiffText => EnsureSucceeded(files.DiffTextFiles(Require(settings.Path, nameof(settings.Path)), Require(settings.DestinationPath, nameof(settings.DestinationPath)), settings.MaxLines)),
            _ => throw new InvalidOperationException($"Workspace file operation '{settings.Operation}' is not supported.")
        };

        return ValueTask.FromResult(WorkflowExecutorJson.Result(context, result));
    }

    private static T EnsureSucceeded<T>(T result)
    {
        var succeededProperty = typeof(T).GetProperty("Succeeded");
        var messageProperty = typeof(T).GetProperty("Message");
        if (succeededProperty?.GetValue(result) is false)
        {
            var message = messageProperty?.GetValue(result)?.ToString() ?? "Workspace operation failed.";
            throw new InvalidOperationException(message);
        }

        return result;
    }

    private static string Require(string value, string name)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Workspace file executor setting '{name}' is required.")
            : value.Trim();

    private static string? EmptyToNull(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class SourceIngestionWorkflowExecutor(IWorkspacePathResolutionService paths) : IWorkflowExecutor
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

    private IEnumerable<WorkflowSourceIngestionFile> ResolveCandidateFiles(
        WorkflowSourceCandidate candidate,
        WorkflowSourceIngestionExecutorSettings settings,
        IReadOnlySet<string> allowedExtensions,
        int take)
    {
        if (take <= 0)
        {
            yield break;
        }

        var kind = candidate.Kind;
        var resolvedAsDirectory = string.Equals(kind, "folderPath", StringComparison.OrdinalIgnoreCase) ||
                                  (!string.Equals(kind, "filePath", StringComparison.OrdinalIgnoreCase) && Directory.Exists(ResolvePathForProbe(candidate.Value, settings)));

        if (resolvedAsDirectory)
        {
            var directory = ResolveDirectory(candidate.Value, settings);
            var count = 0;
            foreach (var file in Directory.EnumerateFiles(
                         directory.FullPath,
                         "*",
                         new EnumerationOptions
                         {
                             RecurseSubdirectories = settings.RecursiveFolders,
                             IgnoreInaccessible = true,
                             AttributesToSkip = 0
                         })
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (!IsAllowedExtension(file, allowedExtensions))
                {
                    continue;
                }

                yield return new WorkflowSourceIngestionFile(
                    file,
                    ToDisplayPath(file, directory),
                    Path.GetFileName(file));
                count++;
                if (count >= take)
                {
                    yield break;
                }
            }

            yield break;
        }

        var resolvedFile = ResolveFile(candidate.Value, settings);
        if (!IsAllowedExtension(resolvedFile.FullPath, allowedExtensions))
        {
            throw new InvalidOperationException($"Source file '{resolvedFile.RelativePath}' has extension '{Path.GetExtension(resolvedFile.FullPath)}', which is not allowed by this workflow source-ingestion node.");
        }

        yield return new WorkflowSourceIngestionFile(
            resolvedFile.FullPath,
            resolvedFile.RelativePath,
            Path.GetFileName(resolvedFile.FullPath));
    }

    private WorkflowSourceIngestionDocument ReadSourceDocument(
        WorkflowSourceCandidate candidate,
        WorkflowSourceIngestionFile file,
        int maxCharactersPerFile,
        int remainingCharacters)
    {
        var maxCharacters = Math.Max(0, Math.Min(maxCharactersPerFile, remainingCharacters));
        var extension = Path.GetExtension(file.FullPath).ToLowerInvariant();
        var result = extension switch
        {
            ".pdf" => ReadPdf(file.FullPath, maxCharacters),
            ".xls" or ".xlsx" => ReadWorkbook(file.FullPath, maxCharacters),
            _ => ReadText(file.FullPath, maxCharacters)
        };

        return new WorkflowSourceIngestionDocument(
            candidate.Key,
            candidate.Label,
            candidate.Kind,
            candidate.Origin,
            file.DisplayPath,
            file.FileName,
            extension,
            result.Text,
            result.TotalCharacters,
            result.IsTruncated,
            result.ExtractionStatus);
    }

    private static WorkflowSourceReadResult ReadText(string fullPath, int maxCharacters)
    {
        using var reader = new StreamReader(fullPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return ReadBounded(reader, maxCharacters, "text");
    }

    private static WorkflowSourceReadResult ReadPdf(string fullPath, int maxCharacters)
    {
        var builder = new StringBuilder(Math.Min(maxCharacters, 8192));
        var totalCharacters = 0;
        var isTruncated = false;

        using var pdf = PdfDocument.Open(fullPath);
        foreach (var page in pdf.GetPages())
        {
            var pageText = page.Text ?? string.Empty;
            totalCharacters += pageText.Length;
            AppendBounded(builder, $"# Page {page.Number}{Environment.NewLine}{pageText}{Environment.NewLine}", maxCharacters, ref isTruncated);
            if (isTruncated)
            {
                break;
            }
        }

        var text = builder.ToString().Trim();
        return new WorkflowSourceReadResult(
            text,
            totalCharacters,
            isTruncated,
            string.IsNullOrWhiteSpace(text) ? "pdf-no-extractable-text" : "pdf-text");
    }

    private static WorkflowSourceReadResult ReadWorkbook(string fullPath, int maxCharacters)
    {
        using var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var builder = new StringBuilder(Math.Min(maxCharacters, 8192));
        var totalCharacters = 0;
        var isTruncated = false;

        do
        {
            var worksheetName = string.IsNullOrWhiteSpace(reader.Name) ? "Sheet" : reader.Name;
            AppendBounded(builder, $"## Worksheet: {worksheetName}{Environment.NewLine}", maxCharacters, ref isTruncated);
            var rowIndex = 0;
            while (reader.Read())
            {
                rowIndex++;
                if (rowIndex > 80)
                {
                    isTruncated = true;
                    break;
                }

                var cells = new List<string>();
                var fieldCount = Math.Min(reader.FieldCount, 20);
                for (var index = 0; index < fieldCount; index++)
                {
                    var value = reader.GetValue(index)?.ToString()?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(value) || cells.Count > 0)
                    {
                        cells.Add(value);
                    }
                }

                if (cells.Count == 0)
                {
                    continue;
                }

                var line = string.Join(" | ", cells);
                totalCharacters += line.Length;
                AppendBounded(builder, line + Environment.NewLine, maxCharacters, ref isTruncated);
                if (isTruncated)
                {
                    break;
                }
            }
        }
        while (!isTruncated && reader.NextResult());

        return new WorkflowSourceReadResult(builder.ToString().Trim(), totalCharacters, isTruncated, "workbook-text");
    }

    private static WorkflowSourceReadResult ReadBounded(TextReader reader, int maxCharacters, string extractionStatus)
    {
        var buffer = new char[Math.Min(Math.Max(maxCharacters, 1), 8192)];
        var builder = new StringBuilder(Math.Min(maxCharacters, 8192));
        var totalCharacters = 0;
        var isTruncated = false;

        while (true)
        {
            var read = reader.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            totalCharacters += read;
            AppendBounded(builder, new string(buffer, 0, read), maxCharacters, ref isTruncated);
            if (isTruncated)
            {
                break;
            }
        }

        return new WorkflowSourceReadResult(builder.ToString(), totalCharacters, isTruncated, extractionStatus);
    }

    private static void AppendBounded(StringBuilder builder, string value, int maxCharacters, ref bool isTruncated)
    {
        if (maxCharacters <= 0 || builder.Length >= maxCharacters)
        {
            isTruncated = true;
            return;
        }

        var remaining = maxCharacters - builder.Length;
        if (value.Length <= remaining)
        {
            builder.Append(value);
            return;
        }

        builder.Append(value.AsSpan(0, remaining));
        isTruncated = true;
    }

    private WorkspaceResolvedPath ResolveFile(string value, WorkflowSourceIngestionExecutorSettings settings)
    {
        var path = NormalizeInputPath(value);
        try
        {
            return paths.ResolveFilePath(path, allowMissing: false);
        }
        catch (InvalidOperationException) when (settings.AllowAbsoluteInputPaths && Path.IsPathRooted(path))
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new InvalidOperationException($"Source file '{fullPath}' was not found.");
            }

            return new WorkspaceResolvedPath(fullPath, NormalizeAbsoluteDisplayPath(fullPath), IsWorkspacePath: false);
        }
    }

    private WorkspaceResolvedPath ResolveDirectory(string value, WorkflowSourceIngestionExecutorSettings settings)
    {
        var path = NormalizeInputPath(value);
        try
        {
            return paths.ResolveDirectoryPath(path, allowMissing: false);
        }
        catch (InvalidOperationException) when (settings.AllowAbsoluteInputPaths && Path.IsPathRooted(path))
        {
            var fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath))
            {
                throw new InvalidOperationException($"Source directory '{fullPath}' was not found.");
            }

            return new WorkspaceResolvedPath(fullPath, NormalizeAbsoluteDisplayPath(fullPath), IsWorkspacePath: false);
        }
    }

    private string ResolvePathForProbe(string value, WorkflowSourceIngestionExecutorSettings settings)
    {
        var path = NormalizeInputPath(value);
        if (Path.IsPathRooted(path))
        {
            if (!settings.AllowAbsoluteInputPaths)
            {
                return path;
            }

            return Path.GetFullPath(path);
        }

        try
        {
            return paths.ResolveDirectoryPath(path, allowMissing: false).FullPath;
        }
        catch (InvalidOperationException)
        {
            return path;
        }
    }

    private static IReadOnlyList<WorkflowSourceCandidate> CollectCandidates(
        JsonElement root,
        WorkflowSourceIngestionExecutorSettings settings,
        IReadOnlySet<string> sourceKeys)
    {
        var candidates = new List<WorkflowSourceCandidate>();
        if (settings.IncludeAdditionalSources &&
            root.TryGetProperty("sources", out var sources) &&
            sources.ValueKind == JsonValueKind.Array)
        {
            foreach (var source in sources.EnumerateArray())
            {
                if (TryReadBoolean(source, "isEnabled", out var isEnabled) && !isEnabled)
                {
                    continue;
                }

                var kind = ReadString(source, "kind");
                if (!IsPathSourceKind(kind))
                {
                    continue;
                }

                var key = ReadString(source, "key");
                if (!ShouldIncludeKey(key, sourceKeys))
                {
                    continue;
                }

                var value = ReadString(source, "value");
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                candidates.Add(new WorkflowSourceCandidate(
                    key,
                    ReadString(source, "label"),
                    kind,
                    value,
                    "additional-source"));
            }
        }

        if (settings.IncludeParentNodePath && root.TryGetProperty("parentNode", out var parentNode))
        {
            AddNodeCandidate(candidates, parentNode, "parent-node", sourceKeys);
        }

        if (settings.IncludeSelectedNodePaths && root.TryGetProperty("selectedNodes", out var selectedNodes))
        {
            AddNodeCandidates(candidates, selectedNodes, "selected-node", sourceKeys);
        }

        if (settings.IncludeParentSubtreePaths && root.TryGetProperty("parentSubtree", out var parentSubtree))
        {
            AddNodeCandidates(candidates, parentSubtree, "parent-subtree", sourceKeys);
        }

        return candidates;
    }

    private static void AddNodeCandidates(
        List<WorkflowSourceCandidate> candidates,
        JsonElement nodes,
        string origin,
        IReadOnlySet<string> sourceKeys)
    {
        if (nodes.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var node in nodes.EnumerateArray())
        {
            AddNodeCandidate(candidates, node, origin, sourceKeys);
        }
    }

    private static void AddNodeCandidate(
        List<WorkflowSourceCandidate> candidates,
        JsonElement node,
        string origin,
        IReadOnlySet<string> sourceKeys)
    {
        var nodeId = ReadString(node, "id");
        if (!ShouldIncludeKey(nodeId, sourceKeys))
        {
            return;
        }

        var mediaPath = ReadString(node, "mediaRelativePath");
        var notes = ReadString(node, "notes");
        var candidatePath = !string.IsNullOrWhiteSpace(mediaPath)
            ? mediaPath
            : ExtractPathLine(notes);
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return;
        }

        var kind = LooksLikeFolderPath(candidatePath) ? "folderPath" : "filePath";
        candidates.Add(new WorkflowSourceCandidate(
            string.IsNullOrWhiteSpace(nodeId) ? origin : nodeId,
            ReadString(node, "title"),
            kind,
            candidatePath,
            origin));
    }

    private static JsonElement? TryClone(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.Clone();
    }

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

    private static bool ShouldIncludeKey(string key, IReadOnlySet<string> sourceKeys)
        => sourceKeys.Count == 0 || sourceKeys.Contains(key);

    private static bool IsPathSourceKind(string kind)
        => string.Equals(kind, "filePath", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(kind, "folderPath", StringComparison.OrdinalIgnoreCase);

    private static bool IsAllowedExtension(string fullPath, IReadOnlySet<string> allowedExtensions)
        => allowedExtensions.Count == 0 || allowedExtensions.Contains(Path.GetExtension(fullPath));

    private static string NormalizeInputPath(string value)
        => value.Trim(PathTrimCharacters).Replace('/', Path.DirectorySeparatorChar);

    private static string NormalizeAbsoluteDisplayPath(string value)
        => Path.GetFullPath(value).Replace('\\', '/');

    private static string ToDisplayPath(string fullPath, WorkspaceResolvedPath directory)
    {
        if (directory.IsWorkspacePath)
        {
            return NormalizeAbsoluteDisplayPath(fullPath).StartsWith(NormalizeAbsoluteDisplayPath(directory.FullPath), StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(directory.RelativePath, Path.GetRelativePath(directory.FullPath, fullPath)).Replace('\\', '/')
                : NormalizeAbsoluteDisplayPath(fullPath);
        }

        return NormalizeAbsoluteDisplayPath(fullPath);
    }

    private static string ExtractPathLine(string value)
    {
        foreach (var line in value.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = ExtractEmbeddedPath(line.Trim(PathTrimCharacters));
            if (Path.IsPathRooted(candidate) ||
                candidate.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith("external-target\\", StringComparison.OrdinalIgnoreCase) ||
                LooksLikeRelativeFilePath(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static string ExtractEmbeddedPath(string value)
    {
        var index = FindWindowsPathStart(value);
        return index > 0
            ? value[index..].Trim(PathTrimCharacters)
            : value;
    }

    private static int FindWindowsPathStart(string value)
    {
        for (var index = 0; index < value.Length - 2; index++)
        {
            if (char.IsAsciiLetter(value[index]) &&
                value[index + 1] == ':' &&
                value[index + 2] is '\\' or '/')
            {
                return index;
            }
        }

        return value.IndexOf(@"\\", StringComparison.Ordinal);
    }

    private static bool LooksLikeFolderPath(string value)
    {
        var normalized = value.Trim(PathTrimCharacters);
        if (Directory.Exists(normalized))
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(Path.GetExtension(normalized));
    }

    private static bool LooksLikeRelativeFilePath(string value)
        => value.Contains('/') || value.Contains('\\') || !string.IsNullOrWhiteSpace(Path.GetExtension(value));

    private static string ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return string.Empty;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : property.GetRawText();
    }

    private static bool TryReadBoolean(JsonElement element, string propertyName, out bool value)
    {
        value = false;
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    private sealed record WorkflowSourceCandidate(
        string Key,
        string Label,
        string Kind,
        string Value,
        string Origin);

    private sealed record WorkflowSourceIngestionFile(
        string FullPath,
        string DisplayPath,
        string FileName);

    private sealed record WorkflowSourceReadResult(
        string Text,
        int TotalCharacters,
        bool IsTruncated,
        string ExtractionStatus);

    private sealed record WorkflowSourceIngestionDocument(
        string Key,
        string Label,
        string Kind,
        string Origin,
        string Path,
        string FileName,
        string Extension,
        string Text,
        int TotalCharacters,
        bool IsTruncated,
        string ExtractionStatus);

    private sealed record WorkflowSourceIngestionError(
        string Key,
        string Label,
        string Kind,
        string Value,
        string Origin,
        string Message);
}

public sealed class HttpFetchWorkflowExecutor : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.HttpFetch;

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        var settings = WorkflowExecutorJson.Deserialize<WorkflowHttpExecutorSettings>(context.SettingsJson);
        var resolvedUrl = ResolveUrl(settings, input);
        if (!Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("HTTP executor requires an absolute http or https URL.");
        }

        var maxBytes = Math.Clamp(settings.MaxResponseBytes, 1024, 5 * 1024 * 1024);
        using var request = new HttpRequestMessage(ToHttpMethod(settings.Method), uri);
        foreach (var header in settings.Headers)
        {
            if (string.IsNullOrWhiteSpace(header.Key))
            {
                continue;
            }

            request.Headers.TryAddWithoutValidation(header.Key.Trim(), header.Value);
        }

        if (!string.IsNullOrEmpty(settings.Body) && settings.Method is not WorkflowHttpMethodKind.Get)
        {
            request.Content = new StringContent(settings.Body, Encoding.UTF8, "application/json");
        }

        using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await ReadBoundedBodyAsync(response, maxBytes, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"HTTP executor received {(int)response.StatusCode} {response.ReasonPhrase} from '{uri}'.");
        }

        var result = new
        {
            url = uri.ToString(),
            statusCode = (int)response.StatusCode,
            reasonPhrase = response.ReasonPhrase ?? string.Empty,
            contentType = response.Content.Headers.ContentType?.ToString() ?? string.Empty,
            body.Text,
            body.IsTruncated,
            inputPayload = settings.IncludeInputPayload ? input.PayloadJson : string.Empty,
            headers = response.Headers
                .Concat(response.Content.Headers)
                .ToDictionary(header => header.Key, header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase)
        };

        return WorkflowExecutorJson.Result(context, result);
    }

    private static HttpMethod ToHttpMethod(WorkflowHttpMethodKind method)
        => method switch
        {
            WorkflowHttpMethodKind.Get => HttpMethod.Get,
            WorkflowHttpMethodKind.Post => HttpMethod.Post,
            WorkflowHttpMethodKind.Put => HttpMethod.Put,
            WorkflowHttpMethodKind.Patch => HttpMethod.Patch,
            WorkflowHttpMethodKind.Delete => HttpMethod.Delete,
            _ => throw new InvalidOperationException($"HTTP method '{method}' is not supported.")
        };

    private static string ResolveUrl(
        WorkflowHttpExecutorSettings settings,
        WorkflowNodeInput input)
    {
        if (!string.IsNullOrWhiteSpace(settings.Url))
        {
            return settings.Url.Trim();
        }

        var url = ResolveInputJsonString(input, settings.UrlJsonPath, nameof(settings.UrlJsonPath));
        return string.IsNullOrWhiteSpace(url)
            ? throw new InvalidOperationException("HTTP executor setting 'Url' or 'UrlJsonPath' is required.")
            : url.Trim();
    }

    private static string? ResolveInputJsonString(
        WorkflowNodeInput input,
        string jsonPath,
        string settingName)
    {
        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            return null;
        }

        if (!WorkflowRoutingValidation.TryParseJsonPath(jsonPath.Trim(), out var path, out var pathError))
        {
            throw new InvalidOperationException($"HTTP executor setting '{settingName}' has invalid JSON path: {pathError}.");
        }

        if (string.IsNullOrWhiteSpace(input.PayloadJson))
        {
            throw new InvalidOperationException($"HTTP executor setting '{settingName}' requires a workflow JSON payload.");
        }

        using var document = JsonDocument.Parse(input.PayloadJson);
        if (!TryResolve(document.RootElement, path, out var value))
        {
            throw new InvalidOperationException($"HTTP executor setting '{settingName}' path '{jsonPath}' was not found in the workflow payload.");
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.GetRawText();
    }

    private static bool TryResolve(
        JsonElement root,
        IReadOnlyList<BuiltInJsonPathSegment> path,
        out JsonElement value)
    {
        value = root;
        foreach (var segment in path)
        {
            if (segment.PropertyName is not null)
            {
                if (value.ValueKind != JsonValueKind.Object ||
                    !value.TryGetProperty(segment.PropertyName, out value))
                {
                    return false;
                }

                continue;
            }

            if (segment.Index is not { } targetIndex || value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var currentIndex = 0;
            var matched = false;
            foreach (var item in value.EnumerateArray())
            {
                if (currentIndex == targetIndex)
                {
                    value = item;
                    matched = true;
                    break;
                }

                currentIndex++;
            }

            if (!matched)
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<(string Text, bool IsTruncated)> ReadBoundedBodyAsync(
        HttpResponseMessage response,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var memory = new MemoryStream(capacity: Math.Min(maxBytes, 8192));
        var buffer = new byte[8192];
        var truncated = false;

        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            var remaining = maxBytes - (int)memory.Length;
            if (read > remaining)
            {
                memory.Write(buffer, 0, Math.Max(remaining, 0));
                truncated = true;
                break;
            }

            memory.Write(buffer, 0, read);
        }

        return (Encoding.UTF8.GetString(memory.ToArray()), truncated);
    }
}

public sealed class SpreadsheetWorkflowExecutor(
    ISpreadsheetDocumentService documents,
    IWorkspacePathResolutionService paths) : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.Spreadsheet;

    public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = WorkflowExecutorJson.Deserialize<WorkflowSpreadsheetExecutorSettings>(context.SettingsJson);
        object result = settings.Operation switch
        {
            WorkflowSpreadsheetOperation.WorkbookSummary => Inspect(settings),
            WorkflowSpreadsheetOperation.ReadCell => ReadCell(settings),
            WorkflowSpreadsheetOperation.ReadRange => ReadRange(settings),
            WorkflowSpreadsheetOperation.RangeToMarkdown => ReadRange(settings),
            WorkflowSpreadsheetOperation.WriteCell => WriteCell(settings),
            WorkflowSpreadsheetOperation.WriteRange => WriteRange(settings),
            WorkflowSpreadsheetOperation.ApplyBatch => WriteBatch(settings),
            _ => throw new InvalidOperationException($"Spreadsheet operation '{settings.Operation}' is not supported.")
        };

        return ValueTask.FromResult(WorkflowExecutorJson.Result(context, result));
    }

    private object Inspect(WorkflowSpreadsheetExecutorSettings settings)
    {
        var workbook = paths.ResolveFilePath(Require(settings.WorkbookPath, nameof(settings.WorkbookPath)), allowMissing: false);
        return documents.InspectWorkbook(workbook.FullPath);
    }

    private object ReadCell(WorkflowSpreadsheetExecutorSettings settings)
    {
        var workbook = paths.ResolveFilePath(Require(settings.WorkbookPath, nameof(settings.WorkbookPath)), allowMissing: false);
        return documents.ReadCell(
            workbook.FullPath,
            Require(settings.WorksheetName, nameof(settings.WorksheetName)),
            Require(settings.CellAddress, nameof(settings.CellAddress)));
    }

    private object ReadRange(WorkflowSpreadsheetExecutorSettings settings)
    {
        var workbook = paths.ResolveFilePath(Require(settings.WorkbookPath, nameof(settings.WorkbookPath)), allowMissing: false);
        return documents.ReadRange(
            workbook.FullPath,
            Require(settings.WorksheetName, nameof(settings.WorksheetName)),
            Require(settings.RangeAddress, nameof(settings.RangeAddress)),
            settings.MaxRows,
            settings.MaxColumns);
    }

    private object WriteCell(WorkflowSpreadsheetExecutorSettings settings)
    {
        var write = settings with
        {
            CellWrites = [new WorkflowSpreadsheetCellWrite(Require(settings.CellAddress, nameof(settings.CellAddress)), settings.Value)]
        };
        return WriteBatch(write);
    }

    private object WriteRange(WorkflowSpreadsheetExecutorSettings settings)
    {
        if (settings.RangeWrites.Count == 0)
        {
            throw new InvalidOperationException("Spreadsheet write-range operation requires at least one range write.");
        }

        return WriteBatch(settings);
    }

    private object WriteBatch(WorkflowSpreadsheetExecutorSettings settings)
    {
        var workbook = paths.ResolveFilePath(Require(settings.WorkbookPath, nameof(settings.WorkbookPath)), settings.CreateWorkbookIfMissing);
        var output = string.IsNullOrWhiteSpace(settings.OutputWorkbookPath)
            ? workbook
            : paths.ResolveFilePath(settings.OutputWorkbookPath, allowMissing: true);
        var result = documents.Write(new DocumentWriteRequest(
            workbook.FullPath,
            output.FullPath,
            Require(settings.WorksheetName, nameof(settings.WorksheetName)),
            settings.CellWrites.Select(item => new DocumentCellWrite(item.CellAddress, item.Value)).ToArray(),
            settings.RangeWrites.Select(item => new DocumentRangeWrite(item.RangeAddress, item.Values)).ToArray(),
            settings.CreateWorkbookIfMissing,
            settings.Overwrite));

        return new
        {
            result.WorkbookPath,
            result.WorksheetName,
            result.CellWriteCount,
            result.RangeWriteCount,
            relativePath = output.RelativePath
        };
    }

    private static string Require(string value, string name)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Spreadsheet executor setting '{name}' is required.")
            : value.Trim();
}

public sealed class ProjectStructureWorkflowExecutor(IServiceScopeFactory scopeFactory) : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.ProjectStructure;

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        var settings = WorkflowExecutorJson.Deserialize<WorkflowProjectStructureExecutorSettings>(context.SettingsJson);
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetService<ProjectStructureAgentService>()
            ?? throw new InvalidOperationException("Project-structure executor requires ProjectStructureAgentService, but it is not registered in this host.");

        object result = settings.Operation switch
        {
            WorkflowProjectStructureOperation.ListProjects => await service.ListProjectsAsync(cancellationToken),
            WorkflowProjectStructureOperation.ReadTree => await service.GetStructureAsync(
                RequireProjectId(settings, input),
                new ProjectStructureReadRequest(
                    IncludeLinks: true,
                    IncludeLayout: true,
                    IncludeMetadata: true,
                    IncludeNotes: true,
                    IncludeAssets: true,
                    Take: 250),
                cancellationToken),
            WorkflowProjectStructureOperation.ReadNode => await service.GetStructureAsync(
                RequireProjectId(settings, input),
                new ProjectStructureReadRequest(
                    NodeIds: [RequireNodeId(settings, input)],
                    IncludeLinks: true,
                    IncludeLayout: true,
                    IncludeMetadata: true,
                    IncludeNotes: true,
                    IncludeAssets: true),
                cancellationToken),
            WorkflowProjectStructureOperation.CreateAsset => await service.CreateAssetAsync(
                RequireProjectId(settings, input),
                BuildAssetRequest(settings, input),
                BuildAgentContext(input),
                cancellationToken),
            _ => throw new InvalidOperationException($"Project-structure operation '{settings.Operation}' is not supported.")
        };

        return WorkflowExecutorJson.Result(context, result);
    }

    private static ProjectStructureAssetCreateInput BuildAssetRequest(
        WorkflowProjectStructureExecutorSettings settings,
        WorkflowNodeInput input)
    {
        var objectType = settings.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            ? ProjectObjectType.ImageAsset
            : ProjectObjectType.File;
        var title = Require(settings.Title, nameof(settings.Title));
        var sourcePath = string.IsNullOrWhiteSpace(settings.SourceWorkspacePath) ? null : settings.SourceWorkspacePath.Trim();
        ProjectObjectMediaPayload? media = null;
        var content = WorkflowInputPayloadText.Resolve(settings.Content, settings.ContentFromInput, input);

        if (sourcePath is null)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            media = new ProjectObjectMediaPayload(
                $"{SanitizeFileName(title)}.{NormalizeAssetKind(settings.AssetKind)}",
                settings.ContentType,
                Convert.ToBase64String(bytes));
        }

        return new ProjectStructureAssetCreateInput(
            objectType,
            title,
            Subtitle: string.Empty,
            Notes: content,
            media,
            ParentNodeKey: ResolveOptionalNodeId(settings, input) ?? ResolveWorkflowParentNodeId(input),
            ObjectSubtype: NormalizeAssetKind(settings.AssetKind),
            MetadataJson: "{}",
            SourceWorkspacePath: sourcePath,
            SourceFileName: $"{SanitizeFileName(title)}.{NormalizeAssetKind(settings.AssetKind)}",
            SourceContentType: settings.ContentType);
    }

    private static Guid RequireProjectId(
        WorkflowProjectStructureExecutorSettings settings,
        WorkflowNodeInput input)
    {
        if (settings.ProjectId is { } projectId && projectId != Guid.Empty)
        {
            return projectId;
        }

        var rawProjectId = ResolveInputJsonString(input, settings.ProjectIdJsonPath, nameof(settings.ProjectIdJsonPath));
        if (Guid.TryParse(rawProjectId, out var parsed) && parsed != Guid.Empty)
        {
            return parsed;
        }

        if (TryResolveInputJsonString(input, "$.project.id", out rawProjectId) &&
            Guid.TryParse(rawProjectId, out parsed) &&
            parsed != Guid.Empty)
        {
            return parsed;
        }

        throw new InvalidOperationException("Project-structure executor setting 'ProjectId' or 'ProjectIdJsonPath' is required unless the workflow input includes '$.project.id'.");
    }

    private static string RequireNodeId(
        WorkflowProjectStructureExecutorSettings settings,
        WorkflowNodeInput input)
        => Require(ResolveOptionalNodeId(settings, input) ?? string.Empty, nameof(settings.NodeId));

    private static string? ResolveOptionalNodeId(
        WorkflowProjectStructureExecutorSettings settings,
        WorkflowNodeInput input)
    {
        if (!string.IsNullOrWhiteSpace(settings.NodeId))
        {
            return settings.NodeId.Trim();
        }

        return ResolveInputJsonString(input, settings.NodeIdJsonPath, nameof(settings.NodeIdJsonPath));
    }

    private static string? ResolveWorkflowParentNodeId(WorkflowNodeInput input)
        => TryResolveInputJsonString(input, "$.runContext.workflowNodeId", out var workflowNodeId) &&
           !string.IsNullOrWhiteSpace(workflowNodeId)
            ? workflowNodeId.Trim()
            : null;

    private static string? ResolveInputJsonString(
        WorkflowNodeInput input,
        string jsonPath,
        string settingName)
    {
        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            return null;
        }

        if (!WorkflowRoutingValidation.TryParseJsonPath(jsonPath.Trim(), out var path, out var pathError))
        {
            throw new InvalidOperationException($"Project-structure executor setting '{settingName}' has invalid JSON path: {pathError}.");
        }

        if (string.IsNullOrWhiteSpace(input.PayloadJson))
        {
            throw new InvalidOperationException($"Project-structure executor setting '{settingName}' requires a workflow JSON payload.");
        }

        using var document = JsonDocument.Parse(input.PayloadJson);
        if (!TryResolve(document.RootElement, path, out var value))
        {
            throw new InvalidOperationException($"Project-structure executor setting '{settingName}' path '{jsonPath}' was not found in the workflow payload.");
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.GetRawText();
    }

    private static bool TryResolveInputJsonString(
        WorkflowNodeInput input,
        string jsonPath,
        out string? resolvedValue)
    {
        resolvedValue = null;
        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            return false;
        }

        if (!WorkflowRoutingValidation.TryParseJsonPath(jsonPath.Trim(), out var path, out var pathError))
        {
            throw new InvalidOperationException($"Project-structure executor has invalid JSON path '{jsonPath}': {pathError}.");
        }

        if (string.IsNullOrWhiteSpace(input.PayloadJson))
        {
            return false;
        }

        using var document = JsonDocument.Parse(input.PayloadJson);
        if (!TryResolve(document.RootElement, path, out var value))
        {
            return false;
        }

        resolvedValue = value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.GetRawText();
        return true;
    }

    private static bool TryResolve(
        JsonElement root,
        IReadOnlyList<BuiltInJsonPathSegment> path,
        out JsonElement value)
    {
        value = root;
        foreach (var segment in path)
        {
            if (segment.PropertyName is not null)
            {
                if (value.ValueKind != JsonValueKind.Object ||
                    !value.TryGetProperty(segment.PropertyName, out value))
                {
                    return false;
                }

                continue;
            }

            if (segment.Index is not { } targetIndex || value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var currentIndex = 0;
            var matched = false;
            foreach (var item in value.EnumerateArray())
            {
                if (currentIndex == targetIndex)
                {
                    value = item;
                    matched = true;
                    break;
                }

                currentIndex++;
            }

            if (!matched)
            {
                return false;
            }
        }

        return true;
    }

    private static ProjectStructureAgentContext BuildAgentContext(WorkflowNodeInput input)
    {
        var fallback = new ProjectStructureAgentContext(
            "workflow-executor",
            "Workflow executor",
            Environment.MachineName,
            string.Empty,
            string.Empty,
            Guid.NewGuid().ToString("N"));

        return string.IsNullOrWhiteSpace(ReadRunContextString(input, "agentId"))
            ? fallback
            : new ProjectStructureAgentContext(
                ReadRunContextString(input, "agentId"),
                ReadRunContextString(input, "agentName", fallback.AgentName),
                ReadRunContextString(input, "machineName", fallback.MachineName),
                ReadRunContextString(input, "repositoryRoot", fallback.RepositoryRoot),
                ReadRunContextString(input, "branchName", fallback.BranchName),
                ReadRunContextString(input, "sessionId", fallback.SessionId));
    }

    private static string ReadRunContextString(
        WorkflowNodeInput input,
        string propertyName,
        string fallback = "")
        => TryResolveInputJsonString(input, $"$.runContext.{propertyName}", out var value) &&
           !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;

    private static string NormalizeAssetKind(string value)
        => string.IsNullOrWhiteSpace(value) ? "md" : value.Trim().TrimStart('.').ToLowerInvariant();

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var sanitized = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray()).Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "asset" : sanitized;
    }

    private static string Require(string value, string name)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Project-structure executor setting '{name}' is required.")
            : value.Trim();
}

public sealed class ImageGenerationWorkflowExecutor : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.ImageGeneration;

    public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        var settings = WorkflowExecutorJson.Deserialize<WorkflowImageGenerationExecutorSettings>(context.SettingsJson);
        if (string.IsNullOrWhiteSpace(settings.Prompt))
        {
            throw new InvalidOperationException("Image-generation executor setting 'Prompt' is required.");
        }

        throw new InvalidOperationException("Workflow image generation requires a provider bridge extracted from the existing MafAgentRuntime image-generation tool. The descriptor and setup contract are registered, but no workflow-safe provider bridge is registered in this host.");
    }
}

internal static class WorkflowInputPayloadText
{
    public static string Resolve(string configuredValue, bool fromInput, WorkflowNodeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return fromInput
            ? Extract(input.PayloadJson)
            : configuredValue;
    }

    private static string Extract(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.String => document.RootElement.GetString() ?? string.Empty,
                JsonValueKind.Object => TryReadCommonTextProperty(document.RootElement, out var value)
                    ? value
                    : payload,
                _ => payload
            };
        }
        catch (JsonException)
        {
            return payload;
        }
    }

    private static bool TryReadCommonTextProperty(JsonElement element, out string value)
    {
        foreach (var propertyName in new[] { "content", "text", "markdown", "message", "responseText" })
        {
            if (element.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String)
            {
                value = property.GetString() ?? string.Empty;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }
}

public sealed class PlannedWorkflowExecutor(WorkflowExecutorDescriptor descriptor) : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor { get; } = descriptor;

    public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"Workflow executor '{Descriptor.Id}' is planned but not implemented in this bundle.");
    }
}

internal static class WorkflowExecutorJson
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, Options)
           ?? throw new InvalidOperationException($"Workflow executor settings could not be deserialized as {typeof(T).Name}.");

    public static WorkflowNodeExecutionResult Result(
        WorkflowExecutorExecutionContext context,
        object payload)
        => new(
            context.Node.Id,
            Serialize(payload),
            context.Descriptor.ResultShape);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
