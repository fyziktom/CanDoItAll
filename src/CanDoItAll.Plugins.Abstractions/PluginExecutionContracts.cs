using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Plugins.Abstractions;

public interface ICanDoItAllPlugin
{
    PluginDescriptor Descriptor { get; }
}

public interface IBundledPlugin : ICanDoItAllPlugin
{
    void ConfigurePluginServices(IPluginServiceRegistry services);
}

public interface IPluginServiceRegistry
{
    void AddWorkflowExecutor<TExecutor>()
        where TExecutor : class, IPluginWorkflowExecutor;

    void AddWorkflowExecutor(IPluginWorkflowExecutor executor);
}

public interface IPluginWorkflowExecutor
{
    PluginWorkflowExecutorDescriptor Descriptor { get; }

    ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        PluginWorkflowExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default);
}

public sealed record PluginWorkflowExecutionContext(
    PluginDescriptor Plugin,
    PluginConnectionSnapshot? Connection,
    WorkflowDefinition Workflow,
    WorkflowNode Node,
    string NodeSettingsJson,
    IPluginCapabilityContext Capabilities);

public sealed record PluginExecutionEvent(
    PluginId PluginId,
    PluginConnectionId? ConnectionId,
    WorkflowId WorkflowId,
    WorkflowVersionId VersionId,
    WorkflowRunId? RunId,
    WorkflowNodeId NodeId,
    WorkflowExecutorId ExecutorId,
    string EventName,
    string RedactedMessage);

public interface IPluginCapabilityContext
{
    IPluginSecretCapability Secrets { get; }

    IPluginWorkspaceFileCapability WorkspaceFiles { get; }

    IPluginStorageCapability Storage { get; }

    IPluginProjectStructureCapability ProjectStructure { get; }

    IPluginHttpCapability Http { get; }

    IPluginOAuth2Capability? OAuth2 { get; }

    IPluginExecutionEvents Events { get; }

    IPluginHostToolCapability HostTools { get; }
}

public interface IPluginHostToolCapability
{
    ValueTask<PluginHostToolExecutionResult> ExecuteAsync(
        PluginHostToolExecutionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PluginHostToolExecutionRequest(
    PluginHostToolRecipeId RecipeId,
    IReadOnlyDictionary<string, string> Arguments,
    int TimeoutSeconds = 30,
    int MaxOutputCharacters = 12000);

public sealed record PluginHostToolExecutionResult(
    PluginHostToolRecipeId RecipeId,
    bool Succeeded,
    int ExitCode,
    string Message,
    string Stdout,
    string Stderr,
    bool StdoutTruncated,
    bool StderrTruncated,
    string BoundaryMode,
    bool BoundaryEnforced,
    IReadOnlyList<string> EnvironmentVariableNames);

public interface IPluginSecretCapability
{
    ValueTask<string> ResolveSecretAsync(
        PluginSecretReference reference,
        PluginSecretResolutionPurpose purpose,
        CancellationToken cancellationToken = default);
}

public interface IPluginWorkspaceFileCapability
{
    ValueTask<PluginWorkspaceFileListResult> ListFilesAsync(
        PluginWorkspaceFileListRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<PluginWorkspaceTextFileReadResult> ReadTextFileAsync(
        PluginWorkspaceTextFileReadRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<PluginWorkspaceFileMutationResult> WriteTextFileAsync(
        PluginWorkspaceTextFileWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PluginWorkspaceFileListRequest(
    string? RelativePath = null,
    string SearchPattern = "*",
    int MaxResults = 100);

public sealed record PluginWorkspaceFileListResult(
    IReadOnlyList<PluginWorkspaceFileSummary> Files,
    IReadOnlyList<string> Warnings);

public sealed record PluginWorkspaceFileSummary(
    string Path,
    bool IsDirectory,
    long? Length,
    DateTimeOffset? ModifiedAtUtc);

public sealed record PluginWorkspaceTextFileReadRequest(
    string Path,
    int MaxCharacters = 12000);

public sealed record PluginWorkspaceTextFileReadResult(
    string Path,
    string Content,
    bool WasTruncated);

public sealed record PluginWorkspaceTextFileWriteRequest(
    string Path,
    string Content,
    bool Overwrite = true);

public sealed record PluginWorkspaceFileMutationResult(
    string Path,
    bool Succeeded,
    string Message);

public interface IPluginStorageCapability
{
    ValueTask<PluginStorageAccessDescriptor> DescribeAsync(
        PluginStorageObjectReference reference,
        CancellationToken cancellationToken = default);

    ValueTask<PluginStoragePlacementResult> PlaceAsync(
        PluginStoragePlacementRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PluginStorageObjectReference(
    Guid StorageId,
    string LocatorKind,
    string Locator);

public sealed record PluginStoragePlacementRequest(
    string FileName,
    string ContentType,
    byte[] Content,
    string UsagePurpose,
    string? RelativePathHint = null,
    Guid? PreferredStorageId = null);

public sealed record PluginStoragePlacementResult(
    PluginStorageObjectReference Reference,
    PluginStorageAccessDescriptor Access,
    string Route,
    string RelativePath,
    IReadOnlyList<string> Warnings);

public sealed record PluginStorageAccessDescriptor(
    string PreviewUrl,
    string DownloadUrl,
    string? DirectUrl,
    bool SupportsInlinePreview,
    bool SupportsDownload,
    string DisplayFileName,
    string ContentType,
    long? ContentLength,
    string ReasonWhenUnavailable);

public interface IPluginProjectStructureCapability
{
    ValueTask<IReadOnlyList<PluginProjectSummary>> ListProjectsAsync(CancellationToken cancellationToken = default);

    ValueTask<PluginProjectStructureReadResult> ReadStructureAsync(
        PluginProjectStructureReadRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PluginProjectSummary(
    Guid Id,
    string Name,
    string Status,
    DateTimeOffset UpdatedAtUtc);

public sealed record PluginProjectStructureReadRequest(
    Guid ProjectId,
    string? SubtreeRootId = null,
    int? Take = null,
    bool IncludeMetadata = false);

public sealed record PluginProjectStructureReadResult(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<PluginProjectNodeSummary> Nodes,
    IReadOnlyList<string> Warnings);

public sealed record PluginProjectNodeSummary(
    string Id,
    string? ParentId,
    string ObjectType,
    string Title,
    string Status,
    string Route,
    string? MetadataJson);

public interface IPluginHttpCapability
{
    ValueTask<PluginHttpResponse> SendAsync(
        PluginHttpRequest request,
        CancellationToken cancellationToken = default);
}

public enum PluginHttpMethod
{
    Get,
    Post,
    Put,
    Patch,
    Delete
}

public sealed record PluginHttpRequest(
    PluginHttpMethod Method,
    Uri Uri,
    IReadOnlyDictionary<string, string> Headers,
    string? Body,
    string? ContentType,
    PluginConnectionId? ConnectionId = null);

public sealed record PluginHttpResponse(
    int StatusCode,
    IReadOnlyDictionary<string, string> Headers,
    string Body,
    string ContentType);

public interface IPluginOAuth2Capability
{
    ValueTask<PluginOAuth2TokenSnapshot> GetAccessTokenAsync(
        PluginConnectionId connectionId,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken = default);
}

public sealed record PluginOAuth2TokenSnapshot(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<string> Scopes);

public interface IPluginExecutionEvents
{
    ValueTask RecordAsync(
        PluginExecutionEvent pluginEvent,
        CancellationToken cancellationToken = default);
}
