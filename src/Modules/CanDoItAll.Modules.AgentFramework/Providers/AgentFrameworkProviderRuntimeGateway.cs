using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

using AgentFrameworkProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;

internal sealed class AgentFrameworkProviderRuntimeGateway(
    ICanDoItAllAgentWorkspaceFactory workspaceFactory,
    IProviderRuntimeProfileSource providerSource,
    IActivityStream activityStream,
    ILogger<AgentFrameworkProviderRuntimeGateway> logger) :
    IProviderHealthCheckService,
    IProviderPromptExecutionService
{
    public async Task<ProviderHealthCheckResult> CheckHealthAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default)
    {
        AgentFrameworkProviderProfile? provider = null;
        try
        {
            var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
            provider = await providerSource.GetProviderAsync(
                providerProfileId,
                cancellationToken);
            if (provider is null)
            {
                return new ProviderHealthCheckResult(false, "Provider profile not found.");
            }

            var result = ProviderFailureDisclosurePolicy.SanitizeHealthResult(
                provider,
                await workspaceService.TestProviderAsync(
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
            var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
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

            var response = await workspaceService.RunProviderTestChatAsync(
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
}
