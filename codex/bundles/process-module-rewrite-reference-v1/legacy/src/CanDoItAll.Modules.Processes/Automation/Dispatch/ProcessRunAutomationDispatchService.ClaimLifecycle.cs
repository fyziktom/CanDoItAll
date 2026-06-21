namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private ProcessDispatchClaimCoordinator CreateDispatchClaimCoordinator()
    {
        return new ProcessDispatchClaimCoordinator(
            new ProcessDispatchClaimStore(dbContextFactory),
            ResolveDispatchClaimLeasePolicy(),
            clock,
            logger);
    }

    private ProcessDispatchClaimLeasePolicy ResolveDispatchClaimLeasePolicy()
    {
        var leaseDuration = ResolveStepDispatchClaimLeaseDuration();
        var heartbeatInterval = ResolveStepDispatchHeartbeatInterval(leaseDuration);
        return new ProcessDispatchClaimLeasePolicy(leaseDuration, heartbeatInterval);
    }

    private TimeSpan ResolveStepDispatchClaimLeaseDuration()
    {
        var leaseDuration = processRuntimeOptions.Value.StepDispatchClaimLeaseDuration;
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Processes:Runtime:StepDispatchClaimLeaseDuration must be positive.");
        }

        return leaseDuration;
    }

    private TimeSpan ResolveStepDispatchHeartbeatInterval(TimeSpan leaseDuration)
    {
        var heartbeatInterval = processRuntimeOptions.Value.StepDispatchHeartbeatInterval;
        if (heartbeatInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Processes:Runtime:StepDispatchHeartbeatInterval must be positive.");
        }

        if (heartbeatInterval >= leaseDuration)
        {
            throw new InvalidOperationException("Processes:Runtime:StepDispatchHeartbeatInterval must be shorter than StepDispatchClaimLeaseDuration.");
        }

        return heartbeatInterval;
    }

    private async Task<ProcessStepDispatchClaim?> TryClaimStepDispatchAsync(
        ProcessDispatchClaimCoordinator claimCoordinator,
        Guid processRunId,
        Guid stepRunId,
        string trigger,
        Guid? triggerStepRunId,
        CancellationToken cancellationToken)
    {
        return await claimCoordinator.TryClaimAsync(
            new ProcessDispatchClaimRequest(
                processRunId,
                stepRunId,
                NormalizeTrigger(trigger, triggerStepRunId)),
            AutomationDispatcherInstanceId,
            cancellationToken);
    }

    private Func<CancellationToken, Task> CreateDispatchRenewLeaseCallback(
        ProcessDispatchClaimCoordinator claimCoordinator,
        ProcessStepDispatchClaim dispatchClaim,
        Func<CancellationToken, Task>? renewOuterLeaseAsync)
    {
        return claimCoordinator.CreateRenewLeaseCallback(dispatchClaim, renewOuterLeaseAsync);
    }

    private async Task EnsureStepDispatchClaimHeldAsync(
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await CreateDispatchClaimCoordinator().EnsureHeldAsync(dispatchClaim, cancellationToken);
    }

    private async Task<bool> IsStepDispatchClaimHeldAsync(
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        return await CreateDispatchClaimCoordinator().IsHeldAsync(dispatchClaim, cancellationToken);
    }
}
