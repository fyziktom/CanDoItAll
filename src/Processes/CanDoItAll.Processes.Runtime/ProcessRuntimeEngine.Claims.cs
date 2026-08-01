using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;

namespace CanDoItAll.Processes.Runtime;

public sealed partial class ProcessRuntimeEngine
{
    private static ProcessRuntimeMutation CreateClaim(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        CreateDispatchClaimCommand command)
    {
        ValidateArguments(state, context);

        if (command.LeaseExpiresAtUtc <= context.OccurredAtUtc)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.InvalidLease", "Dispatch lease expiration must be in the future.");
        }

        if (state.Status != ProcessRuntimeStatus.Active)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.RunNotActive", "Dispatch claims require an active run.");
        }

        var step = FindStep(state, command.WorkItem.StepInstanceId);
        if (step is null)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.StepMissing", "Dispatch work item step does not exist in runtime state.");
        }

        if (step.Status != ProcessRuntimeStepStatus.Ready)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.StepNotReady", "Dispatch claims require a ready step.");
        }

        if (step.ActiveClaimToken is not null)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.StepAlreadyClaimed", "Ready step already has an active claim token.");
        }

        var claim = new DispatchClaimState(
            command.ClaimToken,
            step.StepInstanceId,
            command.OwnerId,
            DispatchClaimStatus.Claimed,
            command.WorkItem.AttemptNumber,
            context.OccurredAtUtc,
            command.LeaseExpiresAtUtc,
            null,
            null);

        var next = state with
        {
            Steps = ReplaceStep(
                state,
                step with
                {
                    Status = ProcessRuntimeStepStatus.Claimed,
                    AttemptNumber = command.WorkItem.AttemptNumber,
                    ActiveClaimToken = command.ClaimToken
                }),
            Claims = Append(state.Claims, claim),
            UpdatedAtUtc = context.OccurredAtUtc
        };

        return Applied(
            next,
            [
                CreateEvent(next, context, ProcessRuntimeEventTypes.StepClaimed, step.StepInstanceId.ToString()),
                CreateEvent(next, context, ProcessRuntimeEventTypes.DispatchClaimCreated, command.ClaimToken.ToString())
            ]);
    }

    private static ProcessRuntimeMutation MarkClaimRunning(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        ProcessStepInstanceId stepId,
        DispatchClaimToken claimToken)
    {
        ValidateArguments(state, context);

        var step = FindStep(state, stepId);
        var claim = FindClaim(state, claimToken);
        if (step is null || claim is null || step.ActiveClaimToken != claimToken)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.ClaimMissing", "Running transition requires the current active claim.");
        }

        if (claim.ExpiresAtUtc <= context.OccurredAtUtc)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.ClaimExpired", "Expired dispatch claim cannot enter running state.");
        }

        if (step.Status != ProcessRuntimeStepStatus.Claimed)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.StepNotClaimed", "Only claimed steps can enter running state.");
        }

        var next = state with
        {
            Steps = ReplaceStep(state, step with { Status = ProcessRuntimeStepStatus.Running }),
            UpdatedAtUtc = context.OccurredAtUtc
        };

        return Applied(next, context, ProcessRuntimeEventTypes.StepRunning, stepId.ToString());
    }

    private static ProcessRuntimeMutation RenewClaim(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        RenewDispatchClaimCommand command)
    {
        ValidateArguments(state, context);

        var claim = FindClaim(state, command.ClaimToken);
        if (claim is null || claim.StepInstanceId != command.StepInstanceId || claim.OwnerId != command.OwnerId)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.ClaimOwnerMismatch", "Claim renewal requires the current owner and token.");
        }

        if (claim.Status is DispatchClaimStatus.Released or DispatchClaimStatus.Completed or DispatchClaimStatus.Cancelled or DispatchClaimStatus.Expired)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.ClaimClosed", "Closed claims cannot be renewed.");
        }

        if (command.LeaseExpiresAtUtc <= claim.ExpiresAtUtc)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.NonMonotonicLease", "Renewed lease expiration must increase.");
        }

        var renewed = claim with
        {
            Status = DispatchClaimStatus.LeaseRenewed,
            ExpiresAtUtc = command.LeaseExpiresAtUtc,
            RenewedAtUtc = context.OccurredAtUtc
        };
        var next = state with
        {
            Claims = ReplaceClaim(state, renewed),
            UpdatedAtUtc = context.OccurredAtUtc
        };

        return Applied(next, context, ProcessRuntimeEventTypes.DispatchLeaseRenewed, command.ClaimToken.ToString());
    }

    private static ProcessRuntimeMutation ReleaseClaim(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        ReleaseDispatchClaimCommand command)
    {
        ValidateArguments(state, context);

        if (ProcessRuntimeTerminalStates.IsRunTerminal(state.Status))
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.TerminalRunImmutable", "Terminal runs cannot release dispatch claims.");
        }

        var step = FindStep(state, command.StepInstanceId);
        var claim = FindClaim(state, command.ClaimToken);
        if (step is null ||
            claim is null ||
            claim.StepInstanceId != command.StepInstanceId ||
            step.ActiveClaimToken != command.ClaimToken)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.ClaimMissing", "Claim release requires the current active claim.");
        }

        if (claim.OwnerId != command.OwnerId)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.ClaimOwnerMismatch", "Claim release requires the current owner and token.");
        }

        if (claim.Status is not (DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed))
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.ClaimClosed", "Closed claims cannot be released.");
        }

        if (step.Status is not (ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running))
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.StepNotClaimed", "Only claimed or running steps can release a dispatch claim.");
        }

        var next = state with
        {
            Steps = ReplaceStep(
                state,
                step with
                {
                    Status = ProcessRuntimeStepStatus.Ready,
                    ActiveClaimToken = null
                }),
            Claims = ReplaceClaim(state, claim with { Status = DispatchClaimStatus.Released }),
            UpdatedAtUtc = context.OccurredAtUtc
        };

        return Applied(next, context, ProcessRuntimeEventTypes.DispatchClaimReleased, command.ClaimToken.ToString());
    }

    private static ProcessRuntimeMutation DeferClaim(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        DeferDispatchClaimCommand command)
    {
        ValidateArguments(state, context);

        if (ProcessRuntimeTerminalStates.IsRunTerminal(state.Status))
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.TerminalRunImmutable", "Terminal runs cannot defer dispatch claims.");
        }

        var step = FindStep(state, command.StepInstanceId);
        var claim = FindClaim(state, command.ClaimToken);
        if (step is null ||
            claim is null ||
            claim.StepInstanceId != command.StepInstanceId ||
            step.ActiveClaimToken != command.ClaimToken)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.ClaimMissing", "Claim deferral requires the current active claim.");
        }

        if (claim.OwnerId != command.OwnerId)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.ClaimOwnerMismatch", "Claim deferral requires the current owner and token.");
        }

        if (claim.Status is not (DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed))
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.ClaimClosed", "Closed claims cannot be deferred.");
        }

        if (step.Status is not (ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running))
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.StepNotClaimed", "Only claimed or running steps can defer a dispatch claim.");
        }

        var next = state with
        {
            Steps = ReplaceStep(
                state,
                step with
                {
                    Status = ProcessRuntimeStepStatus.Waiting,
                    ActiveClaimToken = null
                }),
            Claims = ReplaceClaim(state, claim with { Status = DispatchClaimStatus.Released }),
            UpdatedAtUtc = context.OccurredAtUtc
        };

        var deferredPayload = command.DeferredRunId?.ToString() ?? command.ClaimToken.ToString();
        return Applied(
            next,
            [
                CreateEvent(next, context, ProcessRuntimeEventTypes.StepWaiting, deferredPayload),
                CreateEvent(next, context, ProcessRuntimeEventTypes.DispatchClaimReleased, command.ClaimToken.ToString())
            ]);
    }

    private static ProcessRuntimeMutation ExpireClaims(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        ExpireDispatchClaimsCommand command)
    {
        ValidateArguments(state, context);

        if (command.NowUtc.Offset != TimeSpan.Zero)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.TimestampNotUtc", "Claim expiration time must be UTC.");
        }

        var claims = new List<DispatchClaimState>(state.Claims.Count);
        var steps = new List<ProcessRuntimeStepState>(state.Steps);
        var events = new List<ProcessRuntimeEventEnvelope>();
        var changed = false;
        var blockedIndeterminateStep = false;

        foreach (var claim in state.Claims)
        {
            if (claim.ExpiresAtUtc <= command.NowUtc &&
                claim.Status is DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed)
            {
                var expired = claim with { Status = DispatchClaimStatus.Expired };
                claims.Add(expired);
                var step = steps.FirstOrDefault(candidate =>
                    candidate.StepInstanceId == claim.StepInstanceId);
                var wasPreExecutionClaim = step?.Status == ProcessRuntimeStepStatus.Claimed;
                ReplaceStepInPlace(
                    steps,
                    claim.StepInstanceId,
                    currentStep => currentStep with
                    {
                        Status = wasPreExecutionClaim
                            ? ProcessRuntimeStepStatus.Ready
                            : ProcessRuntimeStepStatus.Blocked,
                        ActiveClaimToken = null
                    });
                events.Add(CreateEvent(state, context, ProcessRuntimeEventTypes.DispatchClaimExpired, claim.ClaimToken.ToString()));
                if (!wasPreExecutionClaim)
                {
                    events.Add(CreateEvent(
                        state,
                        context,
                        ProcessRuntimeEventTypes.StepBlocked,
                        ProcessRuntimeDiagnosticCodes.RunningClaimExpiredReplayUnsafe));
                    blockedIndeterminateStep = true;
                }

                changed = true;
                continue;
            }

            claims.Add(claim);
        }

        if (!changed)
        {
            return Duplicate(state);
        }

        var next = state with
        {
            Status = blockedIndeterminateStep
                ? ProcessRuntimeStatus.Blocked
                : state.Status,
            Steps = steps,
            Claims = claims,
            UpdatedAtUtc = context.OccurredAtUtc
        };

        if (blockedIndeterminateStep)
        {
            events.Add(CreateEvent(
                next,
                context,
                ProcessRuntimeEventTypes.ProcessRunBlocked,
                ProcessRuntimeDiagnosticCodes.RunningClaimExpiredReplayUnsafe));
        }

        return Applied(next, events);
    }

    private static ProcessRuntimeMutation ReclaimClaim(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        ReclaimDispatchClaimCommand command)
    {
        ValidateArguments(state, context);

        var step = FindStep(state, command.StepInstanceId);
        if (step is null)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.StepMissing", "Reclaimed step does not exist in runtime state.");
        }

        if (step.Status != ProcessRuntimeStepStatus.Ready || step.ActiveClaimToken is not null)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.StepNotReclaimable", "Only ready steps without an active claim can be reclaimed.");
        }

        var claim = new DispatchClaimState(
            command.NewClaimToken,
            step.StepInstanceId,
            command.OwnerId,
            DispatchClaimStatus.Reclaimed,
            step.AttemptNumber + 1,
            context.OccurredAtUtc,
            command.LeaseExpiresAtUtc,
            null,
            null);
        var next = state with
        {
            Steps = ReplaceStep(
                state,
                step with
                {
                    Status = ProcessRuntimeStepStatus.Claimed,
                    AttemptNumber = claim.AttemptNumber,
                    ActiveClaimToken = claim.ClaimToken
                }),
            Claims = Append(state.Claims, claim),
            UpdatedAtUtc = context.OccurredAtUtc
        };

        return Applied(next, context, ProcessRuntimeEventTypes.DispatchClaimReclaimed, command.NewClaimToken.ToString());
    }

}
