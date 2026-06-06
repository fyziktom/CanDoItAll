using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal readonly record struct ProcessDispatchClaimLeasePolicy(
    TimeSpan LeaseDuration,
    TimeSpan HeartbeatInterval);

internal readonly record struct ProcessDispatchClaimRequest(
    Guid ProcessRunId,
    Guid StepRunId,
    string NormalizedTrigger);

internal interface IProcessDispatchClaimStore
{
    Task<ProcessRunAutomationDispatchService.ProcessStepDispatchClaim?> TryClaimAsync(
        ProcessDispatchClaimRequest request,
        string claimToken,
        string dispatcherInstanceId,
        DateTimeOffset now,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken);

    Task<bool> RenewAsync(
        ProcessRunAutomationDispatchService.ProcessStepDispatchClaim dispatchClaim,
        DateTimeOffset now,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken);

    Task<bool> IsHeldAsync(
        ProcessRunAutomationDispatchService.ProcessStepDispatchClaim dispatchClaim,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task ReleaseAsync(
        ProcessRunAutomationDispatchService.ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken);
}

internal sealed class ProcessDispatchClaimStore(IDbContextFactory<AppDbContext> dbContextFactory) : IProcessDispatchClaimStore
{
    public async Task<ProcessRunAutomationDispatchService.ProcessStepDispatchClaim?> TryClaimAsync(
        ProcessDispatchClaimRequest request,
        string claimToken,
        string dispatcherInstanceId,
        DateTimeOffset now,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var updatedRows = await dbContext.Set<ProcessStepRun>()
            .Where(item => item.Id == request.StepRunId)
            .Where(item => item.ProcessRunId == request.ProcessRunId)
            .Where(item =>
                item.Status == ProcessStepRunStatus.Ready ||
                item.Status == ProcessStepRunStatus.WaitingApproval ||
                item.Status == ProcessStepRunStatus.InProgress)
            .Where(item =>
                item.AutomationDispatchLeaseExpiresAtUtc == null ||
                item.AutomationDispatchLeaseExpiresAtUtc <= now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.AutomationDispatchClaimToken, claimToken)
                    .SetProperty(item => item.AutomationDispatchClaimedBy, dispatcherInstanceId)
                    .SetProperty(item => item.AutomationDispatchClaimedAtUtc, now)
                    .SetProperty(item => item.AutomationDispatchLeaseExpiresAtUtc, leaseExpiresAtUtc)
                    .SetProperty(item => item.AutomationDispatchAttemptCount, item => item.AutomationDispatchAttemptCount + 1),
                cancellationToken);

        return updatedRows == 0
            ? null
            : new ProcessRunAutomationDispatchService.ProcessStepDispatchClaim(request.StepRunId, claimToken);
    }

    public async Task<bool> RenewAsync(
        ProcessRunAutomationDispatchService.ProcessStepDispatchClaim dispatchClaim,
        DateTimeOffset now,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var updatedRows = await dbContext.Set<ProcessStepRun>()
            .Where(item => item.Id == dispatchClaim.StepRunId)
            .Where(item => item.AutomationDispatchClaimToken == dispatchClaim.ClaimToken)
            .Where(item => item.AutomationDispatchLeaseExpiresAtUtc > now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.AutomationDispatchLeaseExpiresAtUtc, leaseExpiresAtUtc),
                cancellationToken);

        return updatedRows > 0;
    }

    public async Task<bool> IsHeldAsync(
        ProcessRunAutomationDispatchService.ProcessStepDispatchClaim dispatchClaim,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item => item.Id == dispatchClaim.StepRunId)
            .Where(item => item.AutomationDispatchClaimToken == dispatchClaim.ClaimToken)
            .Where(item => item.AutomationDispatchLeaseExpiresAtUtc > now)
            .AnyAsync(cancellationToken);
    }

    public async Task ReleaseAsync(
        ProcessRunAutomationDispatchService.ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Set<ProcessStepRun>()
            .Where(item => item.Id == dispatchClaim.StepRunId)
            .Where(item => item.AutomationDispatchClaimToken == dispatchClaim.ClaimToken)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.AutomationDispatchClaimToken, string.Empty)
                    .SetProperty(item => item.AutomationDispatchClaimedBy, string.Empty)
                    .SetProperty(item => item.AutomationDispatchClaimedAtUtc, (DateTimeOffset?)null)
                    .SetProperty(item => item.AutomationDispatchLeaseExpiresAtUtc, (DateTimeOffset?)null),
                cancellationToken);
    }
}

internal sealed class ProcessDispatchClaimCoordinator(
    IProcessDispatchClaimStore claimStore,
    ProcessDispatchClaimLeasePolicy leasePolicy,
    IClock clock,
    ILogger<ProcessRunAutomationDispatchService> logger)
{
    public async Task<ProcessRunAutomationDispatchService.ProcessStepDispatchClaim?> TryClaimAsync(
        ProcessDispatchClaimRequest request,
        string dispatcherInstanceId,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var claimToken = Guid.NewGuid().ToString("N");
        var leaseExpiresAtUtc = now.Add(leasePolicy.LeaseDuration);
        var dispatchClaim = await claimStore.TryClaimAsync(
            request,
            claimToken,
            dispatcherInstanceId,
            now,
            leaseExpiresAtUtc,
            cancellationToken);
        if (dispatchClaim is null)
        {
            logger.LogInformation(
                "Process automation dispatch for run {RunId}, step {StepRunId} was skipped because another worker holds the durable dispatch claim or the step is no longer dispatchable.",
                request.ProcessRunId,
                request.StepRunId);
            return null;
        }

        logger.LogInformation(
            "Claimed process automation dispatch for run {RunId}, step {StepRunId}. Trigger={Trigger}. LeaseExpiresAtUtc={LeaseExpiresAtUtc}.",
            request.ProcessRunId,
            request.StepRunId,
            request.NormalizedTrigger,
            leaseExpiresAtUtc);

        return dispatchClaim;
    }

    public Func<CancellationToken, Task> CreateRenewLeaseCallback(
        ProcessRunAutomationDispatchService.ProcessStepDispatchClaim dispatchClaim,
        Func<CancellationToken, Task>? renewOuterLeaseAsync)
    {
        return async token =>
        {
            if (renewOuterLeaseAsync is not null)
            {
                await renewOuterLeaseAsync(token);
            }

            if (!await RenewAsync(dispatchClaim, token))
            {
                throw new ProcessDispatchClaimLostException(dispatchClaim.StepRunId);
            }
        };
    }

    public ProcessDispatchLeaseHeartbeat StartHeartbeat(
        ProcessRunAutomationDispatchService.ProcessStepDispatchClaim dispatchClaim,
        Func<CancellationToken, Task> renewLeaseAsync,
        CancellationToken cancellationToken)
    {
        return ProcessDispatchLeaseHeartbeat.Start(
            dispatchClaim.StepRunId,
            leasePolicy.HeartbeatInterval,
            renewLeaseAsync,
            cancellationToken);
    }

    public async Task<bool> RenewAsync(
        ProcessRunAutomationDispatchService.ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var leaseExpiresAtUtc = now.Add(leasePolicy.LeaseDuration);
        var renewed = await claimStore.RenewAsync(dispatchClaim, now, leaseExpiresAtUtc, cancellationToken);
        if (!renewed)
        {
            logger.LogWarning(
                "Could not renew process automation dispatch claim for step {StepRunId}; another worker may have claimed or completed it.",
                dispatchClaim.StepRunId);
        }

        return renewed;
    }

    public async Task EnsureHeldAsync(
        ProcessRunAutomationDispatchService.ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        if (await IsHeldAsync(dispatchClaim, cancellationToken))
        {
            return;
        }

        throw new ProcessDispatchClaimLostException(dispatchClaim.StepRunId);
    }

    public async Task<bool> IsHeldAsync(
        ProcessRunAutomationDispatchService.ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        return await claimStore.IsHeldAsync(dispatchClaim, clock.GetUtcNow(), cancellationToken);
    }

    public async Task ReleaseAsync(
        ProcessRunAutomationDispatchService.ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        try
        {
            await claimStore.ReleaseAsync(dispatchClaim, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
