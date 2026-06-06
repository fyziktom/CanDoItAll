using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessDispatchStepTransitionSnapshot(
    Guid Id,
    ProcessStepRunStatus Status,
    Guid ConcurrencyToken);

internal sealed class ProcessDispatchStepTransitionService(
    IServiceScopeFactory serviceScopeFactory,
    IDbContextFactory<AppDbContext> dbContextFactory,
    Func<ProcessRunAutomationDispatchService.ProcessStepDispatchClaim, CancellationToken, Task> ensureStepDispatchClaimHeldAsync)
{
    public async Task<Result> TransitionStepWithClaimAsync(
        ProcessStepTransitionRequest request,
        ProcessRunAutomationDispatchService.ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await ensureStepDispatchClaimHeldAsync(dispatchClaim, cancellationToken);
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        return await processesService.TransitionStepAsync(request, cancellationToken);
    }

    public async Task<ProcessDispatchStepTransitionSnapshot?> LoadStepRunTransitionSnapshotAsync(
        Guid stepRunId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item => item.Id == stepRunId)
            .Select(item => new ProcessDispatchStepTransitionSnapshot(item.Id, item.Status, item.ConcurrencyToken))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
