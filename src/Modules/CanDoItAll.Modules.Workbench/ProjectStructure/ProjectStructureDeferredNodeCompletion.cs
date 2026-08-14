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
    Failed = 4
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
}

public sealed record ProjectStructureGeneratedImageCompletionRequest(
    Guid ProviderProfileId,
    string Model,
    string Prompt,
    string Size,
    string Quality,
    AgentGeneratedImageFormat Format,
    string FileName);

public sealed record ProjectStructureDeferredNodeCompletionRequest(
    Guid OperationId,
    Guid ProjectId,
    string NodeId,
    ProjectStructureDeferredNodeCompletionKind Kind,
    ProjectStructureGeneratedImageCompletionRequest? GeneratedImage = null)
{
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
    ProjectStructureNode? UpdatedNode);

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
                exception,
                "Project structure deferred completion worker failed. ProjectId={ProjectId} NodeId={NodeId} OperationId={OperationId} Kind={Kind}",
                item.Request.ProjectId,
                item.Request.NodeId,
                item.Request.OperationId,
                item.Request.Kind);
        }
    }
}

public sealed class ProjectStructureDeferredNodeCompletionProcessor(
    IProviderRuntimeProfileSource providerSource,
    IAgentImageGenerationService imageGenerationService,
    ProjectWorkbenchService projectWorkbenchService,
    ILogger<ProjectStructureDeferredNodeCompletionProcessor> logger)
{
    public async Task<ProjectStructureDeferredNodeCompletionResult> ProcessAsync(
        ProjectStructureDeferredNodeCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Kind switch
        {
            ProjectStructureDeferredNodeCompletionKind.GeneratedImageAsset => await ProcessGeneratedImageAsync(request, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported project structure deferred completion kind '{request.Kind}'.")
        };
    }

    private async Task<ProjectStructureDeferredNodeCompletionResult> ProcessGeneratedImageAsync(
        ProjectStructureDeferredNodeCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var imageRequest = request.GeneratedImage
            ?? throw new InvalidOperationException("Generated image deferred completion requires an image request payload.");
        ProviderProfile? provider = null;

        try
        {
            provider = await providerSource.GetProviderAsync(imageRequest.ProviderProfileId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Image-generation provider '{imageRequest.ProviderProfileId:D}' was not found.");

            await projectWorkbenchService.UpdateObjectMetadataAsync(
                request.ProjectId,
                request.NodeId,
                ProjectStructureDeferredCompletionMetadataFactory.BuildGeneratedImageMetadataJson(
                    request.OperationId,
                    ProjectStructureDeferredNodeCompletionState.Running,
                    imageRequest,
                    provider),
                notes: imageRequest.Prompt,
                status: "Image generation running",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var generated = await imageGenerationService.GenerateAsync(
                new AgentImageGenerationRequest(
                    provider,
                    imageRequest.Model,
                    imageRequest.Prompt,
                    imageRequest.Size,
                    imageRequest.Quality,
                    imageRequest.Format,
                    []),
                cancellationToken).ConfigureAwait(false);
            var image = generated.Images.FirstOrDefault()
                ?? throw new InvalidOperationException("Image generation completed without image data.");
            if (image.Bytes.Length == 0)
            {
                throw new InvalidOperationException("Image generation completed with empty image data.");
            }

            var contentType = string.IsNullOrWhiteSpace(image.ContentType)
                ? ResolveGeneratedImageContentType(imageRequest.Format)
                : image.ContentType.Trim();
            var updatedNode = await projectWorkbenchService.ReplaceObjectMediaAsync(
                request.ProjectId,
                request.NodeId,
                new ProjectObjectMediaPayload(
                    imageRequest.FileName,
                    contentType,
                    Convert.ToBase64String(image.Bytes)),
                ProjectStructureDeferredCompletionMetadataFactory.BuildGeneratedImageMetadataJson(
                    request.OperationId,
                    ProjectStructureDeferredNodeCompletionState.Completed,
                    imageRequest,
                    provider),
                notes: imageRequest.Prompt,
                status: "Generated image ready",
                cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Generated image node '{request.NodeId}' was not found.");

            return new ProjectStructureDeferredNodeCompletionResult(
                request.OperationId,
                request.ProjectId,
                request.NodeId,
                request.Kind,
                true,
                $"{imageRequest.FileName} was generated through {provider.Name}.",
                updatedNode);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Generated image deferred completion failed. ProjectId={ProjectId} NodeId={NodeId} OperationId={OperationId} ProviderProfileId={ProviderProfileId} Model={Model}",
                request.ProjectId,
                request.NodeId,
                request.OperationId,
                imageRequest.ProviderProfileId,
                imageRequest.Model);

            var updatedNode = await projectWorkbenchService.UpdateObjectMetadataAsync(
                request.ProjectId,
                request.NodeId,
                ProjectStructureDeferredCompletionMetadataFactory.BuildGeneratedImageMetadataJson(
                    request.OperationId,
                    ProjectStructureDeferredNodeCompletionState.Failed,
                    imageRequest,
                    provider,
                    exception.GetBaseException().Message),
                notes: imageRequest.Prompt,
                status: "Image generation failed",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return new ProjectStructureDeferredNodeCompletionResult(
                request.OperationId,
                request.ProjectId,
                request.NodeId,
                request.Kind,
                false,
                $"Image generation failed. {exception.GetBaseException().Message}",
                updatedNode);
        }
    }

    private static string ResolveGeneratedImageContentType(AgentGeneratedImageFormat format)
    {
        return format switch
        {
            AgentGeneratedImageFormat.Jpeg => "image/jpeg",
            AgentGeneratedImageFormat.Webp => "image/webp",
            _ => "image/png"
        };
    }
}

public static class ProjectStructureDeferredCompletionMetadataFactory
{
    public static string BuildGeneratedImageMetadataJson(
        Guid operationId,
        ProjectStructureDeferredNodeCompletionState state,
        ProjectStructureGeneratedImageCompletionRequest request,
        ProviderProfile? provider,
        string errorMessage = "")
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = DateTimeOffset.UtcNow;
        return ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            File = new ProjectFileMetadata
            {
                FileSubtype = ProjectFileSubtype.Image,
                SourceHint = "Generated image"
            },
            DeferredCompletion = new ProjectStructureDeferredCompletionMetadata
            {
                OperationId = operationId,
                Kind = ProjectStructureDeferredNodeCompletionKind.GeneratedImageAsset,
                State = state,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                ProviderProfileId = request.ProviderProfileId,
                ProviderName = provider?.Name ?? string.Empty,
                Model = request.Model,
                PromptHash = ComputePromptHash(request.Prompt),
                ErrorMessage = TrimErrorMessage(errorMessage)
            }
        });
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
