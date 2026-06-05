using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessDispatchCandidateHeaderSelector
{
    public static async Task<IReadOnlyList<ProcessRunAutomationDispatchService.DispatchCandidateHeader>> SelectAsync(
        AppDbContext dbContext,
        Guid processRunId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var runStatus = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .Where(item => item.Id == processRunId)
            .Select(item => (ProcessRunStatus?)item.Status)
            .SingleOrDefaultAsync(cancellationToken);
        if (!ProcessDispatchRouteEligibility.IsRunEligibleForDispatchCandidate(runStatus))
        {
            return [];
        }

        var dispatchableSteps = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == processRunId &&
                (item.Status == ProcessStepRunStatus.Ready ||
                 item.Status == ProcessStepRunStatus.WaitingApproval ||
                 item.Status == ProcessStepRunStatus.InProgress))
            .Where(item =>
                item.AutomationDispatchLeaseExpiresAtUtc == null ||
                item.AutomationDispatchLeaseExpiresAtUtc <= now)
            .OrderBy(item => item.Sequence)
            .Select(item => new ProcessRunAutomationDispatchService.DispatchCandidateHeader(item.Id, item.Status))
            .ToListAsync(cancellationToken);

        return dispatchableSteps
            .Where(item => ProcessDispatchRouteEligibility.IsStepStatusDispatchableForRun(runStatus.Value, item.Status))
            .ToList();
    }
}
