using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.Security.Abstractions;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class SharedProviderRelayApplicationService(
    ISharedProviderRelayRequestPolicy requestPolicy,
    ISharedProviderRoutingResolver routingResolver,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IProviderManifestCatalog providerManifestCatalog,
    SharedProviderPublicationEligibilityPolicy eligibilityPolicy,
    ISharedProviderRelaySupportCatalog relaySupportCatalog,
    ISecretRuntimeResolver secretRuntimeResolver,
    SharedProviderInvocationAuditService invocationAuditService,
    ISharedProviderRelayDispatcher dispatcher,
    IClock clock,
    ILogger<SharedProviderRelayApplicationService> logger) :
    ISharedProviderRelayApplicationService
{
    private static readonly TimeSpan InvocationRetention = TimeSpan.FromDays(30);

    private static readonly SharedProviderRelaySupportDescriptor RoutingLookupSupport = new(
        new HashSet<SharedProviderRelayOperation>
        {
            SharedProviderRelayOperation.ChatCompletions,
            SharedProviderRelayOperation.Responses,
            SharedProviderRelayOperation.ImageGenerations
        },
        SharedProviderStreamingMode.ServerSentEvents,
        supportsFunctionTools: true,
        supportsParallelFunctionTools: true,
        supportsStructuredOutput: true,
        supportsVisionInput: true,
        supportsBase64Images: true,
        SharedProviderRelaySupportDescriptor.MaximumAllowedRequestBytes,
        SharedProviderRelaySupportDescriptor.MaximumAllowedOutputTokens,
        SharedProviderRelaySupportDescriptor.MaximumAllowedImageCount);

    public async ValueTask<SharedProviderRelayDispatchResult> InvokeAsync(
        SharedProviderRelayApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var lookupResult = requestPolicy.Normalize(
            request.Operation,
            request.PayloadUtf8,
            RoutingLookupSupport);
        if (lookupResult is SharedProviderRelayRequestPolicyResult.Rejected lookupRejection)
        {
            return new SharedProviderRelayDispatchResult.Failed(lookupRejection.Failure);
        }

        var lookupRequest = ((SharedProviderRelayRequestPolicyResult.Accepted)lookupResult).Request;
        var route = await routingResolver.ResolveAsync(
            lookupRequest.RoutingModelId,
            cancellationToken);
        if (route is null)
        {
            return Failed(SharedProviderRelayFailures.ModelNotFound);
        }

        var persisted = await ResolvePersistedTargetAsync(route, cancellationToken);
        if (persisted is null)
        {
            return Failed(SharedProviderRelayFailures.ModelNotFound);
        }

        if (!SupportsOperation(persisted, request.Operation))
        {
            return Failed(SharedProviderRelayFailures.OperationMismatch);
        }

        var exactResult = requestPolicy.Normalize(
            request.Operation,
            request.PayloadUtf8,
            persisted.Support);
        if (exactResult is SharedProviderRelayRequestPolicyResult.Rejected exactRejection)
        {
            return new SharedProviderRelayDispatchResult.Failed(exactRejection.Failure);
        }

        var normalizedRequest = ((SharedProviderRelayRequestPolicyResult.Accepted)exactResult).Request;
        if (normalizedRequest.RoutingModelId != lookupRequest.RoutingModelId ||
            normalizedRequest.RequiredCapabilities.Any(capability =>
                !persisted.Model.Capabilities.Contains(capability)))
        {
            return Failed(SharedProviderRelayFailures.CapabilityNotSupported);
        }

        var targetResult = await CreateTargetAsync(
            persisted,
            normalizedRequest.RoutingModelId,
            cancellationToken);
        if (targetResult.Failure is not null)
        {
            return new SharedProviderRelayDispatchResult.Failed(targetResult.Failure);
        }

        var target = targetResult.Target!;
        var finalizer = new SharedProviderInvocationAuditFinalizer(
            request.Context.RequestId,
            normalizedRequest.Operation,
            invocationAuditService,
            clock,
            logger);
        try
        {
            await invocationAuditService.BeginAsync(
                new SharedProviderInvocationStartRequest(
                    request.Context.RequestId,
                    target.PublicationId,
                    target.ProviderProfileId,
                    request.Context.AuthenticatedSubject,
                    request.Context.AccessContextReference,
                    request.Context.TraceId,
                    request.Context.CorrelationId,
                    normalizedRequest.Operation,
                    target.PublicModelId,
                    target.UpstreamModelId,
                    clock.GetUtcNow().Add(InvocationRetention)),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            logger.LogWarning(
                "Shared-provider invocation audit could not start for request {RequestId} and publication {PublicationId}.",
                request.Context.RequestId,
                target.PublicationId);
            return Failed(SharedProviderRelayFailures.AuditUnavailable);
        }

        SharedProviderRelayDispatchResult dispatchResult;
        try
        {
            dispatchResult = await dispatcher.DispatchAsync(
                new SharedProviderRelayDispatchRequest(target, normalizedRequest),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await finalizer.CancelledAsync(SharedProviderRelayUsage.Unavailable);
            throw;
        }
        catch (OperationCanceledException)
        {
            await finalizer.FailedAsync(
                SharedProviderRelayFailures.UpstreamTimeout,
                SharedProviderRelayUsage.Unavailable);
            return Failed(SharedProviderRelayFailures.UpstreamTimeout);
        }
        catch (TimeoutException)
        {
            await finalizer.FailedAsync(
                SharedProviderRelayFailures.UpstreamTimeout,
                SharedProviderRelayUsage.Unavailable);
            return Failed(SharedProviderRelayFailures.UpstreamTimeout);
        }
        catch
        {
            logger.LogWarning(
                "Shared-provider upstream dispatch failed for request {RequestId} and publication {PublicationId}.",
                request.Context.RequestId,
                target.PublicationId);
            await finalizer.FailedAsync(
                SharedProviderRelayFailures.UpstreamFailure,
                SharedProviderRelayUsage.Unavailable);
            return Failed(SharedProviderRelayFailures.UpstreamFailure);
        }

        return dispatchResult switch
        {
            SharedProviderRelayDispatchResult.Buffered buffered when !normalizedRequest.Stream =>
                await CompleteBufferedAsync(buffered, finalizer),
            SharedProviderRelayDispatchResult.Streaming streaming when normalizedRequest.Stream =>
                new SharedProviderRelayDispatchResult.Streaming(
                    new SharedProviderAuditedRelayStream(streaming.Stream, finalizer)),
            SharedProviderRelayDispatchResult.Failed failure =>
                await CompleteFailureAsync(failure, finalizer),
            SharedProviderRelayDispatchResult.Streaming streaming =>
                await RejectUnexpectedStreamingAsync(streaming, finalizer),
            _ => await RejectUnexpectedBufferedAsync(finalizer)
        };
    }

    private async Task<PersistedRelayTarget?> ResolvePersistedTargetAsync(
        SharedProviderRoutingTarget route,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await (
            from publication in dbContext.Set<ProviderSharePublication>().AsNoTracking()
            join profile in dbContext.Set<ProviderProfile>().AsNoTracking()
                on publication.ProviderProfileId equals profile.Id
            join secret in dbContext.Set<SecretRecord>().AsNoTracking()
                on profile.ApiKeySecretId equals (Guid?)secret.Id into matchedSecrets
            from secret in matchedSecrets.DefaultIfEmpty()
            where publication.IsPublished &&
                publication.PublicId == route.PublicationId &&
                publication.ProviderProfileId == route.ProviderProfileId
            select new PersistedRelayRow(publication, profile, secret != null))
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var manifest = providerManifestCatalog.ResolveManifest(
            row.Profile.ConnectorPluginKey,
            row.Profile.ProviderKind);
        var eligibility = eligibilityPolicy.Evaluate(
            row.Profile,
            manifest,
            row.RequiredSecretExists);
        if (!eligibility.IsEligible ||
            eligibility.Purpose != route.Purpose ||
            eligibility.Purpose is not { } purpose ||
            !relaySupportCatalog.TryGet(
                row.Profile.ConnectorPluginKey,
                purpose,
                out var relayDescriptor) ||
            relayDescriptor.Classification != SharedProviderRelayAdapterClassification.Production)
        {
            return null;
        }

        var model = eligibility.Models.SingleOrDefault(candidate =>
            string.Equals(
                candidate.UpstreamModelId,
                route.UpstreamModelId,
                StringComparison.Ordinal));
        if (model is null || !route.Capabilities.All(model.Capabilities.Contains))
        {
            return null;
        }

        var expectedModelId = SharedProviderRoutingModelIdCodec.Create(
            row.Publication.PublicId,
            model.UpstreamModelId);

        return new PersistedRelayTarget(
            row.Publication,
            row.Profile,
            model,
            purpose,
            relayDescriptor.Support,
            expectedModelId);
    }

    private async Task<TargetCreationResult> CreateTargetAsync(
        PersistedRelayTarget persisted,
        SharedProviderRoutingModelId requestedModelId,
        CancellationToken cancellationToken)
    {
        if (requestedModelId != persisted.PublicModelId ||
            !Uri.TryCreate(persisted.Profile.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            return new(null, SharedProviderRelayFailures.ModelNotFound);
        }

        SharedProviderRelayCredential? credential = null;
        if (persisted.Profile.ApiKeySecretId is { } secretId)
        {
            string? value;
            try
            {
                value = await secretRuntimeResolver.ResolveValueAsync(
                    new SecretRuntimeRequest(
                        secretId,
                        SecretRuntimePurposes.AgentProviderApiKey,
                        [secretId],
                        ConsumerType: SecretRuntimeConsumerTypes.ProviderProfile,
                        ConsumerId: SecretRuntimeConsumerIds.ProviderProfile(persisted.Profile.Id)),
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                logger.LogWarning(
                    "Shared-provider credential could not be resolved for provider profile {ProviderProfileId}.",
                    persisted.Profile.Id);
                return new(null, SharedProviderRelayFailures.TargetUnavailable);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return new(null, SharedProviderRelayFailures.TargetUnavailable);
            }

            try
            {
                credential = new SharedProviderRelayCredential(value);
            }
            catch (ArgumentException)
            {
                return new(null, SharedProviderRelayFailures.TargetUnavailable);
            }
        }

        try
        {
            return new(
                new SharedProviderRelayTarget(
                    persisted.Publication.PublicId,
                    persisted.Profile.Id,
                    persisted.Profile.ConnectorPluginKey,
                    persisted.Purpose,
                    baseUri,
                    persisted.Model.UpstreamModelId,
                    persisted.PublicModelId,
                    TimeSpan.FromSeconds(persisted.Profile.TimeoutSeconds),
                    persisted.Profile.ExtraSettingsJson,
                    credential,
                    persisted.Support),
                null);
        }
        catch (ArgumentException)
        {
            return new(null, SharedProviderRelayFailures.TargetUnavailable);
        }
    }

    private static async ValueTask<SharedProviderRelayDispatchResult> CompleteBufferedAsync(
        SharedProviderRelayDispatchResult.Buffered buffered,
        SharedProviderInvocationAuditFinalizer finalizer)
    {
        await finalizer.SucceededAsync(buffered.Usage);
        return buffered;
    }

    private static async ValueTask<SharedProviderRelayDispatchResult> CompleteFailureAsync(
        SharedProviderRelayDispatchResult.Failed failure,
        SharedProviderInvocationAuditFinalizer finalizer)
    {
        if (failure.Failure.Category == SharedProviderFailureCategory.Cancelled)
        {
            await finalizer.CancelledAsync(SharedProviderRelayUsage.Unavailable);
        }
        else
        {
            await finalizer.FailedAsync(failure.Failure, SharedProviderRelayUsage.Unavailable);
        }

        return failure;
    }

    private static async ValueTask<SharedProviderRelayDispatchResult> RejectUnexpectedStreamingAsync(
        SharedProviderRelayDispatchResult.Streaming streaming,
        SharedProviderInvocationAuditFinalizer finalizer)
    {
        try
        {
            await streaming.Stream.DisposeAsync();
        }
        finally
        {
            await finalizer.FailedAsync(
                SharedProviderRelayFailures.UpstreamFailure,
                SharedProviderRelayUsage.Unavailable);
        }

        return Failed(SharedProviderRelayFailures.UpstreamFailure);
    }

    private static async ValueTask<SharedProviderRelayDispatchResult> RejectUnexpectedBufferedAsync(
        SharedProviderInvocationAuditFinalizer finalizer)
    {
        await finalizer.FailedAsync(
            SharedProviderRelayFailures.UpstreamFailure,
            SharedProviderRelayUsage.Unavailable);
        return Failed(SharedProviderRelayFailures.UpstreamFailure);
    }

    private static SharedProviderRelayDispatchResult.Failed Failed(SharedProviderFailure failure)
        => new(failure);

    private static bool SupportsOperation(
        PersistedRelayTarget persisted,
        SharedProviderRelayOperation operation)
    {
        var operationCapability = operation switch
        {
            SharedProviderRelayOperation.ChatCompletions =>
                SharedProviderCapability.ChatCompletions,
            SharedProviderRelayOperation.Responses =>
                SharedProviderCapability.Responses,
            SharedProviderRelayOperation.ImageGenerations =>
                SharedProviderCapability.ImageGenerations,
            _ => (SharedProviderCapability?)null
        };
        return operationCapability.HasValue &&
            persisted.Support.Operations.Contains(operation) &&
            persisted.Model.Capabilities.Contains(operationCapability.Value);
    }

    private sealed record PersistedRelayRow(
        ProviderSharePublication Publication,
        ProviderProfile Profile,
        bool RequiredSecretExists);

    private sealed record PersistedRelayTarget(
        ProviderSharePublication Publication,
        ProviderProfile Profile,
        SharedProviderEligibleModel Model,
        SharedProviderPurpose Purpose,
        SharedProviderRelaySupportDescriptor Support,
        SharedProviderRoutingModelId PublicModelId);

    private sealed record TargetCreationResult(
        SharedProviderRelayTarget? Target,
        SharedProviderFailure? Failure);
}

internal static class SharedProviderRelayFailures
{
    public static SharedProviderFailure ModelNotFound { get; } = Create(
        SharedProviderFailureCategory.NotFound,
        "shared_provider_model_not_found",
        "The requested shared-provider model was not found.",
        "model");

    public static SharedProviderFailure CapabilityNotSupported { get; } = Create(
        SharedProviderFailureCategory.Validation,
        "shared_provider_capability_not_supported",
        "The resolved provider does not support the requested capability.");

    public static SharedProviderFailure OperationMismatch { get; } = Create(
        SharedProviderFailureCategory.Conflict,
        "shared_provider_operation_mismatch",
        "The published model does not support this operation.");

    public static SharedProviderFailure TargetUnavailable { get; } = Create(
        SharedProviderFailureCategory.Unavailable,
        "shared_provider_target_unavailable",
        "The resolved shared-provider target is unavailable.");

    public static SharedProviderFailure AuditUnavailable { get; } = Create(
        SharedProviderFailureCategory.Unavailable,
        "shared_provider_audit_unavailable",
        "The shared-provider invocation could not be recorded.");

    public static SharedProviderFailure UpstreamFailure { get; } = Create(
        SharedProviderFailureCategory.UpstreamFailure,
        "shared_provider_upstream_failure",
        "The upstream provider request failed.");

    public static SharedProviderFailure UpstreamTimeout { get; } = Create(
        SharedProviderFailureCategory.Timeout,
        "shared_provider_upstream_timeout",
        "The upstream provider request timed out.");

    private static SharedProviderFailure Create(
        SharedProviderFailureCategory category,
        string code,
        string message,
        string? parameter = null)
        => new(category, new SharedProviderFailureCode(code), message, parameter);
}
