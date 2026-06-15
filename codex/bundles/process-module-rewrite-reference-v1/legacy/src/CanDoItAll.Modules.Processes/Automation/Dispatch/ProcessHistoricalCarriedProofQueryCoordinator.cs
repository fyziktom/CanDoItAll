using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessHistoricalCarriedProofQueryCoordinator(
    IProcessAutomationExecutionClient executionClient)
{
    public async Task<IReadOnlyList<ProcessAutomationExecutionRunDetail>> LoadHistoricalDetailsAsync(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        Func<ProcessAutomationExecutionRunRecord, bool> isHistoricalCarryForwardRun,
        CancellationToken cancellationToken)
    {
        var executionRuns = await executionClient.ListExecutionRunsAsync(
            ProcessExecutionRunQueryBuilder.ForCandidate(candidate),
            cancellationToken);
        var historicalDetails = new List<ProcessAutomationExecutionRunDetail>();
        foreach (var executionRun in executionRuns
                     .Where(isHistoricalCarryForwardRun)
                     .OrderByDescending(executionRun => executionRun.CompletedAtUtc ?? executionRun.UpdatedAtUtc)
                     .ThenByDescending(executionRun => executionRun.UpdatedAtUtc)
                     .ThenByDescending(executionRun => executionRun.CreatedAtUtc))
        {
            historicalDetails.Add(await executionClient.GetExecutionRunDetailAsync(executionRun.Id, cancellationToken));
        }

        return historicalDetails;
    }
}
