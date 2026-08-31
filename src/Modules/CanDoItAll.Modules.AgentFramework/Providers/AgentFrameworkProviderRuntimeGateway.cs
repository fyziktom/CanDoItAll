using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

using AgentFrameworkProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;

internal sealed class AgentFrameworkProviderRuntimeGateway(
    IProviderRuntimeAdministrationService providerAdministration,
    IProviderRuntimeProfileSource providerSource,
    IProviderRuntimeDescriptorStore descriptorStore,
    IProviderRuntimePool runtimePool,
    IActivityStream activityStream,
    ILogger<AgentFrameworkProviderRuntimeGateway> logger) :
    IProviderHealthCheckService,
    IProviderPromptExecutionService,
    IProviderInferenceRelayRuntime
{
    private const string InferenceRelayCredentialIdentity = "shared-provider-relay";

    public async Task<ProviderHealthCheckResult> CheckHealthAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default)
    {
        AgentFrameworkProviderProfile? provider = null;
        try
        {
            provider = await providerSource.GetProviderAsync(
                providerProfileId,
                cancellationToken);
            if (provider is null)
            {
                return new ProviderHealthCheckResult(false, "Provider profile not found.");
            }

            var result = ProviderFailureDisclosurePolicy.SanitizeHealthResult(
                provider,
                await providerAdministration.TestProviderAsync(
                    providerProfileId,
                    cancellationToken));
            await activityStream.RecordAsync(
                new ActivityWriteRequest(
                    "providers",
                    "health-check",
                    $"Checked provider health for {provider.Name}",
                    result.Summary,
                    ArtifactKind: "provider-profile",
                    ArtifactId: provider.Id,
                    Route: "/agents?tab=providers"),
                cancellationToken);

            return new ProviderHealthCheckResult(result.Success, result.Summary);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (provider is null)
            {
                logger.LogWarning(
                    "Provider runtime profile lookup failed during {Operation}. FailureType={FailureType}.",
                    ProviderFailureOperation.HealthCheck,
                    exception.GetType().FullName);
            }

            return new ProviderHealthCheckResult(
                false,
                provider is null
                    ? ProviderFailureDisclosurePolicy
                        .SanitizedProfileLookupFailureMessage
                    : ProviderFailureDisclosurePolicy.SelectMessage(
                        provider,
                        ProviderFailureOperation.HealthCheck,
                        exception.Message));
        }
    }

    public async Task<Result<ProviderPromptExecutionResponse>> ExecuteAsync(
        ProviderPromptExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        AgentFrameworkProviderProfile? provider = null;
        try
        {
            provider = await providerSource.GetProviderAsync(
                request.ProviderProfileId,
                cancellationToken);
            if (provider is { IsEnabled: false })
            {
                provider = null;
            }
            if (provider is null)
            {
                return Result<ProviderPromptExecutionResponse>.Failure(
                    Error.Validation("Provider profile not found or disabled."));
            }

            var response = await providerAdministration.RunProviderTestChatAsync(
                request.ProviderProfileId,
                new ProviderTestChatRequest(
                    request.ModelOverride ?? provider.DefaultModel,
                    "You are executing a CanDoItAll provider request. Reply directly with the final content only.",
                    [],
                    request.Prompt),
                cancellationToken);

            await activityStream.RecordAsync(
                new ActivityWriteRequest(
                    "providers",
                    "send",
                    $"Sent prompt through {provider.Name}",
                    $"Model: {response.Model}.",
                    ArtifactKind: "provider-profile",
                    ArtifactId: provider.Id,
                    Route: "/agents?tab=providers"),
                cancellationToken);

            return Result<ProviderPromptExecutionResponse>.Success(
                new ProviderPromptExecutionResponse(
                    provider.Name,
                    response.Model,
                    response.ResponseText,
                    request.OutputFormat,
                    request.ContainsSensitiveContent,
                    request.ContainsSensitiveContent
                        ? "Sensitive content was included in the outbound payload."
                        : null));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (provider is null)
            {
                logger.LogWarning(
                    "Provider runtime profile lookup failed during {Operation}. FailureType={FailureType}.",
                    ProviderFailureOperation.RuntimeRequest,
                    exception.GetType().FullName);
            }

            var message = provider is null
                ? ProviderFailureDisclosurePolicy
                    .SanitizedProfileLookupFailureMessage
                : ProviderFailureDisclosurePolicy.SelectMessage(
                    provider,
                    ProviderFailureOperation.RuntimeRequest,
                    exception.Message);
            return Result<ProviderPromptExecutionResponse>.Failure(
                Error.Failure(message));
        }
    }

    public async Task<ProviderInferenceRelayTransportResponse> SendAsync(
        ProviderInferenceRelayRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        descriptorStore.Upsert(
            request.Provider,
            request.Provider.ConnectorPluginKey,
            request.Credential is null
                ? string.Empty
                : InferenceRelayCredentialIdentity);
        var handle = await runtimePool.GetRequiredAsync(
            request.Provider.Id,
            cancellationToken);
        var query = request.Operation == ProviderInferenceRelayOperation.ImageGenerations
            ? new ProviderDispatchQuery(
                request.Provider,
                AgentProviderCapabilityKind.ImageGeneration,
                AgentProviderOperationKind.GenerateImage,
                request.Model)
            : new ProviderDispatchQuery(
                request.Provider,
                AgentProviderCapabilityKind.ChatCompletion,
                AgentProviderOperationKind.CompleteChat,
                request.Model);
        return await handle.DispatchAsync(
            new ProviderRuntimeDispatchRequest<ProviderInferenceRelayRequest>(
                query,
                request),
            async (context, token) =>
            {
                EnsureProviderKindMatches(
                    context.Descriptor,
                    context.Query.Provider);
                var driver = handle.ProviderFactory.Resolve<
                    IProviderInferenceRelayDriver>(
                    context.Query.Provider.Kind);
                return await driver.RelayAsync(
                    context.Payload,
                    token);
            },
            cancellationToken);
    }

    private static void EnsureProviderKindMatches(
        ProviderRuntimeDescriptor descriptor,
        AgentFrameworkProviderProfile provider)
    {
        if (descriptor.ProviderKind != provider.Kind)
        {
            throw new InvalidOperationException(
                "Provider runtime descriptor kind does not match the inference-relay provider kind.");
        }
    }
}
