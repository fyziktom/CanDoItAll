using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.AgentFramework;

using WorkspaceProviderHealthResult = CanDoItAll.Modules.Workspace.ProviderHealthResult;

internal sealed class AgentFrameworkProviderRuntimeGateway(
    ICanDoItAllAgentWorkspaceFactory workspaceFactory,
    IProviderRuntimeProfileSource providerSource,
    IActivityStream activityStream) : IProviderRuntimeGateway
{
    public async Task<WorkspaceProviderHealthResult> CheckHealthAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
            var provider = await providerSource.GetProviderAsync(providerProfileId, cancellationToken);
            if (provider is null)
            {
                return new WorkspaceProviderHealthResult(false, "Provider profile not found.");
            }

            var result = await workspaceService.TestProviderAsync(providerProfileId, cancellationToken);
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

            return new WorkspaceProviderHealthResult(result.Success, result.Summary);
        }
        catch (Exception exception)
        {
            return new WorkspaceProviderHealthResult(false, exception.Message);
        }
    }

    public async Task<Result<ProviderExecutionResponse>> SendAsync(
        ProviderExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
            var provider = await providerSource.GetProviderAsync(request.ProviderProfileId, cancellationToken);
            if (provider is { IsEnabled: false })
            {
                provider = null;
            }
            if (provider is null)
            {
                return Result<ProviderExecutionResponse>.Failure(
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

            return Result<ProviderExecutionResponse>.Success(
                new ProviderExecutionResponse(
                    provider.Name,
                    response.Model,
                    response.ResponseText,
                    request.OutputFormat,
                    request.ContainsSensitiveContent,
                    request.ContainsSensitiveContent
                        ? "Sensitive content was included in the outbound payload."
                        : null));
        }
        catch (Exception exception)
        {
            return Result<ProviderExecutionResponse>.Failure(Error.Failure(exception.Message));
        }
    }
}
