using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Core.Routing;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessDispatchCandidateHeaderSelector
{
    public static async Task<IReadOnlyList<ProcessRunAutomationDispatchService.DispatchCandidateHeader>> SelectAsync(
        AppDbContext dbContext,
        Guid processRunId,
        Guid? targetStepRunId,
        string trigger,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var runStatus = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .Where(item => item.Id == processRunId)
            .Select(item => (ProcessRunStatus?)item.Status)
            .SingleOrDefaultAsync(cancellationToken);
        var allowsTargetedSubprocessDispatch =
            runStatus.HasValue &&
            targetStepRunId.HasValue &&
            ProcessDispatchTargetedTriggerRules.IsSubprocessStatusNotificationDispatchable(runStatus.Value, trigger);
        if (!ProcessDispatchRouteEligibility.IsRunEligibleForDispatchCandidate(runStatus) &&
            !allowsTargetedSubprocessDispatch)
        {
            return [];
        }

        var targetedStepRunId = targetStepRunId.GetValueOrDefault();
        var dispatchableSteps = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == processRunId &&
                (item.Status == ProcessStepRunStatus.Ready ||
                 item.Status == ProcessStepRunStatus.WaitingApproval ||
                 item.Status == ProcessStepRunStatus.InProgress ||
                  (allowsTargetedSubprocessDispatch &&
                   item.Id == targetedStepRunId &&
                   (item.Status == ProcessStepRunStatus.Blocked ||
                    item.Status == ProcessStepRunStatus.Failed) &&
                   item.StepKind == ProcessStepKind.Subprocess)))
            .Where(item =>
                item.AutomationDispatchLeaseExpiresAtUtc == null ||
                item.AutomationDispatchLeaseExpiresAtUtc <= now)
            .OrderBy(item => item.Sequence)
            .Select(item => new ProcessRunAutomationDispatchService.DispatchCandidateHeader(
                item.Id,
                item.Status,
                item.StepKind))
            .ToListAsync(cancellationToken);

        return dispatchableSteps
            .Where(item =>
                ProcessDispatchRouteEligibility.IsStepStatusDispatchableForRun(runStatus.Value, item.Status) ||
                ProcessDispatchTargetedTriggerRules.IsSubprocessStatusNotificationDispatchable(
                    runStatus.Value,
                    item.Status,
                    item.StepKind,
                    trigger))
            .ToList();
    }
}
