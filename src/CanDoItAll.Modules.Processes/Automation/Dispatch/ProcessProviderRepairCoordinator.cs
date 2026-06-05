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

        var failedProviderId = currentAgent.ProviderProfileId.Value;
        var providers = await executionClient.ListProvidersAsync(cancellationToken);
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
}
