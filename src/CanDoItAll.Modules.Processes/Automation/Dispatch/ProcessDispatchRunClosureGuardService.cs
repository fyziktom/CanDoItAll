using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessDispatchRunClosureGuardService(
    IDbContextFactory<AppDbContext> dbContextFactory)
{
    public async Task<bool> IsRunClosedToAutomationAsync(
        Guid processRunId,
        Guid stepRunId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var state = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .Where(run => run.Id == processRunId)
            .Join(
                dbContext.Set<ProcessStepRun>().AsNoTracking().Where(stepRun => stepRun.Id == stepRunId),
                run => run.Id,
                stepRun => stepRun.ProcessRunId,
                (run, stepRun) => new
                {
                    RunStatus = (ProcessRunStatus?)run.Status,
                    StepStatus = (ProcessStepRunStatus?)stepRun.Status
                })
            .SingleOrDefaultAsync(cancellationToken);

        return state is null || ProcessDispatchRouteEligibility.IsRunClosedToAutomation(state.RunStatus, state.StepStatus);
    }
}
