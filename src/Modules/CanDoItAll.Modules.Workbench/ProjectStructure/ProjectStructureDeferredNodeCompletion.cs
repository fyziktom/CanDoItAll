using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

public enum ProjectStructureDeferredNodeCompletionKind
{
    GeneratedImageAsset = 1
}

public enum ProjectStructureDeferredNodeCompletionState
{
    Queued = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
    RequiresObservation = 5
}

public sealed class ProjectStructureDeferredCompletionMetadata
{
    [ProjectStructurePreviewField("Deferred state", 900)]
    public ProjectStructureDeferredNodeCompletionState State { get; set; } = ProjectStructureDeferredNodeCompletionState.Queued;

    [ProjectStructurePreviewField("Deferred kind", 910)]
    public ProjectStructureDeferredNodeCompletionKind Kind { get; set; } = ProjectStructureDeferredNodeCompletionKind.GeneratedImageAsset;

    public Guid OperationId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid? ProviderProfileId { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string PromptHash { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;
    public bool ProviderCompleted { get; set; }
    public string? ContentSha256 { get; set; }
}

public sealed record ProjectStructureGeneratedImageCompletionRequest(
    Guid ProviderProfileId,
    string Model,
    string Prompt,
    string Size,
    string Quality,
    AgentGeneratedImageFormat Format,
    string FileName) {
    public ProviderConfigurationFingerprint? ProviderFingerprint { get; init; }
}

public sealed record ProjectStructureDeferredNodeCompletionRequest(
    Guid OperationId,
    Guid ProjectId,
    string NodeId,
    ProjectStructureDeferredNodeCompletionKind Kind,
    ProjectStructureGeneratedImageCompletionRequest? GeneratedImage = null)
{
    public ProjectStructureImageOrigin? Origin { get; init; }

    public static ProjectStructureDeferredNodeCompletionRequest ForGeneratedImage(
        Guid projectId,
        string nodeId,
        ProjectStructureGeneratedImageCompletionRequest generatedImage)
        => new(
            Guid.NewGuid(),
            projectId,
            nodeId,
            ProjectStructureDeferredNodeCompletionKind.GeneratedImageAsset,
            generatedImage);
}

public sealed record ProjectStructureDeferredNodeCompletionResult(
    Guid OperationId,
    Guid ProjectId,
    string NodeId,
    ProjectStructureDeferredNodeCompletionKind Kind,
    bool IsSuccess,
    string Message,
    ProjectStructureNode? UpdatedNode) {
    public bool ProviderInvoked { get; init; }
    public bool ProviderCompleted { get; init; }
    public bool PersistenceAttempted { get; init; }
    public string? ContentSha256 { get; init; }
    public ProjectStructureContentMediaReceipt? StoredMedia { get; init; }
}

public sealed record ProjectStructureDeferredNodeCompletionHandle(
    Guid OperationId,
    Task<ProjectStructureDeferredNodeCompletionResult> Completion);

public interface IProjectStructureDeferredNodeCompletionQueue
{
    ValueTask<ProjectStructureDeferredNodeCompletionHandle> EnqueueAsync(
        ProjectStructureDeferredNodeCompletionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ProjectStructureDeferredNodeCompletionQueue : IProjectStructureDeferredNodeCompletionQueue
{
    private readonly Channel<ProjectStructureDeferredNodeCompletionQueueItem> channel =
        Channel.CreateBounded<ProjectStructureDeferredNodeCompletionQueueItem>(
            new BoundedChannelOptions(64)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });

    public async ValueTask<ProjectStructureDeferredNodeCompletionHandle> EnqueueAsync(
        ProjectStructureDeferredNodeCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ProjectStructureImageOrigin.Validate(request);

        var item = new ProjectStructureDeferredNodeCompletionQueueItem(request);
        await channel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
        return new ProjectStructureDeferredNodeCompletionHandle(
            request.OperationId,
            item.Completion);
    }

    internal IAsyncEnumerable<ProjectStructureDeferredNodeCompletionQueueItem> ReadAllAsync(
        CancellationToken cancellationToken)
        => channel.Reader.ReadAllAsync(cancellationToken);
}

internal sealed class ProjectStructureDeferredNodeCompletionQueueItem
{
    private readonly TaskCompletionSource<ProjectStructureDeferredNodeCompletionResult> completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ProjectStructureDeferredNodeCompletionQueueItem(ProjectStructureDeferredNodeCompletionRequest request)
    {
        Request = request;
    }

    public ProjectStructureDeferredNodeCompletionRequest Request { get; }

    public Task<ProjectStructureDeferredNodeCompletionResult> Completion => completion.Task;

    public void SetResult(ProjectStructureDeferredNodeCompletionResult result)
        => completion.TrySetResult(result);

    public void SetCanceled(CancellationToken cancellationToken)
        => completion.TrySetCanceled(cancellationToken);

    public void SetException(Exception exception)
        => completion.TrySetException(exception);
}

public sealed class ProjectStructureDeferredNodeCompletionWorker(
    ProjectStructureDeferredNodeCompletionQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ProjectStructureDeferredNodeCompletionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var item in queue.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                await ProcessItemAsync(item, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task ProcessItemAsync(
        ProjectStructureDeferredNodeCompletionQueueItem item,
        CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<ProjectStructureDeferredNodeCompletionProcessor>();
            var result = await processor.ProcessAsync(item.Request, stoppingToken).ConfigureAwait(false);
            item.SetResult(result);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            item.SetCanceled(stoppingToken);
        }
        catch (Exception exception)
        {
            item.SetException(exception);
            logger.LogError(
                "Project structure deferred completion worker failed. ProjectId={ProjectId} NodeId={NodeId} OperationId={OperationId} Kind={Kind} FailureType={FailureType}",
                item.Request.ProjectId,
                item.Request.NodeId,
                item.Request.OperationId,
                item.Request.Kind, exception.GetType().Name);
        }
    }
}

public static class ProjectStructureDeferredCompletionMetadataFactory
{
    public static string BuildGeneratedImageMetadataJson(Guid operationId, ProjectStructureDeferredNodeCompletionState state,
        ProjectStructureGeneratedImageCompletionRequest request, ProviderProfile? provider, string errorMessage = "")
        => BuildGeneratedImageMetadataJson(operationId, state, request, provider, errorMessage, null, false, null);

    public static string BuildGeneratedImageMetadataJson(
        Guid operationId,
        ProjectStructureDeferredNodeCompletionState state,
        ProjectStructureGeneratedImageCompletionRequest request,
        ProviderProfile? provider,
        string errorMessage,
        string? originalMetadataJson,
        bool providerCompleted,
        string? contentSha256)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = DateTimeOffset.UtcNow;
        var metadata = ProjectObjectMetadataSerializer.Parse(originalMetadataJson);
        metadata.File ??= new ProjectFileMetadata { FileSubtype = ProjectFileSubtype.Image, SourceHint = "Generated image" };
        var createdAt = metadata.DeferredCompletion?.CreatedAtUtc ?? now;
        metadata.DeferredCompletion = new ProjectStructureDeferredCompletionMetadata {
                OperationId = operationId,
                Kind = ProjectStructureDeferredNodeCompletionKind.GeneratedImageAsset,
                State = state,
                CreatedAtUtc = createdAt,
                UpdatedAtUtc = now,
                ProviderProfileId = request.ProviderProfileId,
                ProviderName = provider?.Name ?? string.Empty,
                Model = request.Model,
                PromptHash = ComputePromptHash(request.Prompt),
                ErrorMessage = TrimErrorMessage(errorMessage),
                ProviderCompleted = providerCompleted,
                ContentSha256 = contentSha256
        };
        return ProjectObjectMetadataSerializer.SerializePreservingUnknownProperties(originalMetadataJson, metadata);
    }

    private static string ComputePromptHash(string prompt)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(prompt.Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string TrimErrorMessage(string errorMessage)
    {
        var normalized = errorMessage.Trim();
        return normalized.Length <= 512 ? normalized : normalized[..512];
    }
}
