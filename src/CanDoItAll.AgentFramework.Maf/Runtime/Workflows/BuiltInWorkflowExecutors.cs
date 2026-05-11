using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tools.Documents;
using Microsoft.Extensions.DependencyInjection;
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
            WorkflowStorageFileOperation.WriteText => EnsureSucceeded(files.WriteTextFile(Require(settings.Path, nameof(settings.Path)), settings.Content, settings.Overwrite)),
            WorkflowStorageFileOperation.AppendText => EnsureSucceeded(files.AppendTextFile(Require(settings.Path, nameof(settings.Path)), settings.Content)),
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

public sealed class HttpFetchWorkflowExecutor : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.HttpFetch;

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        var settings = WorkflowExecutorJson.Deserialize<WorkflowHttpExecutorSettings>(context.SettingsJson);
        if (!Uri.TryCreate(settings.Url, UriKind.Absolute, out var uri) ||
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
                RequireProjectId(settings),
                new ProjectStructureReadRequest(
                    IncludeLinks: true,
                    IncludeLayout: true,
                    IncludeMetadata: true,
                    IncludeNotes: true,
                    IncludeAssets: true,
                    Take: 250),
                cancellationToken),
            WorkflowProjectStructureOperation.ReadNode => await service.GetStructureAsync(
                RequireProjectId(settings),
                new ProjectStructureReadRequest(
                    NodeIds: [Require(settings.NodeId, nameof(settings.NodeId))],
                    IncludeLinks: true,
                    IncludeLayout: true,
                    IncludeMetadata: true,
                    IncludeNotes: true,
                    IncludeAssets: true),
                cancellationToken),
            WorkflowProjectStructureOperation.CreateAsset => await service.CreateAssetAsync(
                RequireProjectId(settings),
                BuildAssetRequest(settings),
                BuildAgentContext(),
                cancellationToken),
            _ => throw new InvalidOperationException($"Project-structure operation '{settings.Operation}' is not supported.")
        };

        return WorkflowExecutorJson.Result(context, result);
    }

    private static ProjectStructureAssetCreateInput BuildAssetRequest(WorkflowProjectStructureExecutorSettings settings)
    {
        var objectType = settings.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            ? ProjectObjectType.ImageAsset
            : ProjectObjectType.File;
        var title = Require(settings.Title, nameof(settings.Title));
        var sourcePath = string.IsNullOrWhiteSpace(settings.SourceWorkspacePath) ? null : settings.SourceWorkspacePath.Trim();
        ProjectObjectMediaPayload? media = null;

        if (sourcePath is null)
        {
            var bytes = Encoding.UTF8.GetBytes(settings.Content);
            media = new ProjectObjectMediaPayload(
                $"{SanitizeFileName(title)}.{NormalizeAssetKind(settings.AssetKind)}",
                settings.ContentType,
                Convert.ToBase64String(bytes));
        }

        return new ProjectStructureAssetCreateInput(
            objectType,
            title,
            Subtitle: string.Empty,
            Notes: settings.Content,
            media,
            ParentNodeKey: string.IsNullOrWhiteSpace(settings.NodeId) ? null : settings.NodeId.Trim(),
            ObjectSubtype: NormalizeAssetKind(settings.AssetKind),
            MetadataJson: "{}",
            SourceWorkspacePath: sourcePath,
            SourceFileName: $"{SanitizeFileName(title)}.{NormalizeAssetKind(settings.AssetKind)}",
            SourceContentType: settings.ContentType);
    }

    private static Guid RequireProjectId(WorkflowProjectStructureExecutorSettings settings)
        => settings.ProjectId is { } projectId && projectId != Guid.Empty
            ? projectId
            : throw new InvalidOperationException("Project-structure executor setting 'ProjectId' is required.");

    private static ProjectStructureAgentContext BuildAgentContext()
        => new(
            "workflow-executor",
            "Workflow executor",
            Environment.MachineName,
            string.Empty,
            string.Empty,
            Guid.NewGuid().ToString("N"));

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
