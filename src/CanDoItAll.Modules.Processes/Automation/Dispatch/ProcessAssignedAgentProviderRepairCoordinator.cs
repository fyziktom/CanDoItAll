using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessAssignedAgentProviderRepairCoordinator(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IAiTechnicalAgentBridge technicalAgentBridge,
    IProcessAutomationExecutionClient executionClient,
    ILogger logger)
{
    public async Task<int> RepairAssignedAgentsAsync(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        IReadOnlyDictionary<Guid, AgentDefinition> agentsById,
        Guid failedProviderId,
        ProcessRunAutomationDispatchService.ProviderFallbackResolution fallbackResolution,
        CancellationToken cancellationToken)
    {
        var assignedPartyIds = await LoadAssignedPartyIdsAsync(
            candidate.Run.Id,
            candidate.StepRun.CurrentExecutorPartyId,
            cancellationToken);
        var assignedSummaries = assignedPartyIds.Count == 0
            ? new Dictionary<Guid, AiTechnicalAgentDirectorySummary>()
            : await technicalAgentBridge.GetDirectorySummariesAsync(assignedPartyIds, cancellationToken);
        var technicalAgentIdsToRepair = assignedSummaries.Values
            .Where(summary => summary.TechnicalAgentId.HasValue)
            .Select(summary => summary.TechnicalAgentId!.Value)
            .Distinct()
            .Where(agentId =>
                agentsById.TryGetValue(agentId, out var assignedAgent) &&
                assignedAgent.ProviderProfileId == failedProviderId)
            .ToHashSet();
        technicalAgentIdsToRepair.Add(candidate.TechnicalAgentId);

        var affectedAgentCount = 0;
        foreach (var technicalAgentId in technicalAgentIdsToRepair)
        {
            try
            {
                var editor = await executionClient.GetAgentEditorAsync(technicalAgentId, cancellationToken);
                var resolvedEditorModel = ProcessProviderFallbackSelectionRules.NormalizeFallbackEditorModel(fallbackResolution);
                if (editor.ProviderProfileId == fallbackResolution.Provider.Id &&
                    string.Equals(editor.Model, resolvedEditorModel, StringComparison.Ordinal))
                {
                    affectedAgentCount++;
                    continue;
                }

                editor.ProviderProfileId = fallbackResolution.Provider.Id;
                editor.Model = resolvedEditorModel;
                editor.ConfigurationJson = ManagedSeedProviderFallbacks.EnableProviderRepairFallbackOverride(editor.ConfigurationJson);
                await executionClient.SaveAgentAsync(editor, cancellationToken);
                affectedAgentCount++;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Failed to switch technical agent {TechnicalAgentId} to fallback provider '{ProviderName}' while recovering process run {RunId}, step {StepRunId}.",
                    technicalAgentId,
                    fallbackResolution.Provider.Name,
                    candidate.Run.Id,
                    candidate.StepRun.Id);

                if (technicalAgentId == candidate.TechnicalAgentId)
                {
                    return 0;
                }
            }
        }

        return affectedAgentCount;
    }

    private async Task<IReadOnlyList<Guid>> LoadAssignedPartyIdsAsync(
        Guid processRunId,
        Guid? currentExecutorPartyId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var partyIds = await dbContext.Set<ProcessRunAssignment>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == processRunId && item.PartyId.HasValue && !item.IsCapabilityGap)
            .Select(item => item.PartyId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (currentExecutorPartyId.HasValue && !partyIds.Contains(currentExecutorPartyId.Value))
        {
            partyIds.Add(currentExecutorPartyId.Value);
        }

        return partyIds;
    }
}
