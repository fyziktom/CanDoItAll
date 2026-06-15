using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Core;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessProviderRepairCoordinator(
    IProcessAutomationExecutionClient executionClient,
    ProcessProviderHealthProbeCoordinator healthProbeCoordinator,
    ProcessAssignedAgentProviderRepairCoordinator assignedAgentRepairCoordinator,
    ILogger logger)
{
    public async Task<ProcessRunAutomationDispatchService.ProviderRepairOutcome?> TryRepairAsync(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string failureSummary,
        CancellationToken cancellationToken)
    {
        var agents = await executionClient.ListAgentsAsync(includeTemplates: false, cancellationToken);
        var agentsById = agents.ToDictionary(item => item.Id);
        if (!agentsById.TryGetValue(candidate.TechnicalAgentId, out var currentAgent) ||
            !currentAgent.ProviderProfileId.HasValue)
        {
            return null;
        }

        var providers = await executionClient.ListProvidersAsync(cancellationToken);
        var failedProviderId = ResolveFailedProviderId(currentAgent, providers, detail.Run);
        var failedProviderName = providers.FirstOrDefault(item => item.Id == failedProviderId)?.Name;
        var fallbackResolution = await healthProbeCoordinator.ResolveHealthyFallbackProviderAsync(
            providers,
            failedProviderId,
            cancellationToken);
        if (fallbackResolution is null)
        {
            logger.LogWarning(
                "Process run {RunId}, step {StepRunId} detected a recoverable provider failure, but no healthy fallback provider was available for technical agent {TechnicalAgentId}. Failure summary: {FailureSummary}",
                candidate.Run.Id,
                candidate.StepRun.Id,
                candidate.TechnicalAgentId,
                failureSummary);
            return null;
        }

        var affectedAgentCount = await assignedAgentRepairCoordinator.RepairAssignedAgentsAsync(
            candidate,
            agentsById,
            failedProviderId,
            fallbackResolution,
            cancellationToken);
        if (affectedAgentCount == 0)
        {
            return null;
        }

        return new ProcessRunAutomationDispatchService.ProviderRepairOutcome(
            failedProviderName ?? detail.Run.ProviderName,
            fallbackResolution.Provider.Name,
            fallbackResolution.Model,
            affectedAgentCount,
            failureSummary);
    }

    internal static Guid ResolveFailedProviderId(
        AgentDefinition currentAgent,
        IReadOnlyList<ProviderProfile> providers,
        ProcessAutomationExecutionRunRecord failedRun)
    {
        var failedRunProvider = ResolveProviderFromExecutionRun(providers, failedRun);
        return failedRunProvider?.Id
               ?? currentAgent.ProviderProfileId
               ?? throw new InvalidOperationException($"Agent '{currentAgent.Id:D}' does not have an assigned provider.");
    }

    private static ProviderProfile? ResolveProviderFromExecutionRun(
        IReadOnlyList<ProviderProfile> providers,
        ProcessAutomationExecutionRunRecord failedRun)
    {
        if (string.IsNullOrWhiteSpace(failedRun.ProviderName))
        {
            return null;
        }

        var matchingProviders = providers
            .Where(provider => string.Equals(provider.Name, failedRun.ProviderName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matchingProviders.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(failedRun.Model))
        {
            var model = failedRun.Model.Trim();
            var matchingModelProvider = matchingProviders.FirstOrDefault(provider =>
                string.Equals(provider.DefaultModel, model, StringComparison.OrdinalIgnoreCase) ||
                provider.SuggestedModels.Contains(model, StringComparer.OrdinalIgnoreCase));
            if (matchingModelProvider is not null)
            {
                return matchingModelProvider;
            }
        }

        return matchingProviders.Count == 1
            ? matchingProviders[0]
            : null;
    }
}
