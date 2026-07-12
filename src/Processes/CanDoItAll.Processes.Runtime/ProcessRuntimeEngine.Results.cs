using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public sealed partial class ProcessRuntimeEngine
{
    private static ProcessRuntimeMutation SubmitStrategyResult(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        SubmitStrategyResultCommand command)
    {
        ValidateArguments(state, context);

        var existing = FindReceipt(state, command.StepInstanceId, command.Result.StrategyId, command.IdempotencyKey);
        if (existing is not null)
        {
            return Duplicate(state);
        }

        var step = FindStep(state, command.StepInstanceId);
        var claim = FindClaim(state, command.ClaimToken);
        if (step is null || claim is null || step.ActiveClaimToken != command.ClaimToken)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.LostLease", "Strategy result was rejected because the active claim token no longer matches.");
        }

        if (claim.OwnerId != command.OwnerId)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.ClaimOwnerMismatch", "Strategy result owner does not match the active claim.");
        }

        if (claim.ExpiresAtUtc <= context.OccurredAtUtc)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.ClaimExpired", "Strategy result was rejected because the dispatch claim expired.");
        }

        if (step.Status != ProcessRuntimeStepStatus.Running)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.StepNotRunning", "Strategy result requires a running step.");
        }

        var nextStepStatus = ToStepStatus(command.Result);
        var receipt = new StrategyResultReceipt(
            step.StepInstanceId,
            command.Result.StrategyId,
            command.IdempotencyKey,
            command.Result.Outcome,
            nextStepStatus,
            command.Result.ResultHash);
        var nextClaims = ReplaceClaim(
            state,
            claim with
            {
                Status = command.Result.Outcome == StrategyOutcome.Canceled
                    ? DispatchClaimStatus.Cancelled
                    : DispatchClaimStatus.Completed,
                ResultIdempotencyKey = command.IdempotencyKey
            });
        var nextSteps = ReplaceStep(
            state,
            step with
            {
                Status = nextStepStatus,
                ActiveClaimToken = null,
                CompletedResultKey = nextStepStatus == ProcessRuntimeStepStatus.Completed
                    ? command.IdempotencyKey
                    : null
            });
        var next = state with
        {
            Steps = nextSteps,
            Claims = nextClaims,
            AppliedResults = Append(state.AppliedResults, receipt),
            AvailableArtifactSlots = AddProducedSlots(state.AvailableArtifactSlots, command.Result),
            UpdatedAtUtc = context.OccurredAtUtc
        };
        next = CompleteRunIfTerminal(next, context.OccurredAtUtc);

        var events = BuildResultEvents(next, context, command, nextStepStatus);
        var resultEventId = events[1].EventId;

        return Applied(
            next,
            events,
            BuildArtifactLedgerEvents(resultEventId, command));
    }

    private static ProcessRuntimeMutation RequestCancellation(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context)
    {
        ValidateArguments(state, context);

        if (ProcessRuntimeTerminalStates.IsRunTerminal(state.Status))
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.TerminalRunImmutable", "Terminal runs cannot be cancelled again.");
        }

        var steps = new List<ProcessRuntimeStepState>(state.Steps.Count);
        foreach (var step in state.Steps)
        {
            steps.Add(ProcessRuntimeTerminalStates.IsStepTerminal(step.Status)
                ? step
                : step with
                {
                    Status = ProcessRuntimeStepStatus.Cancelled,
                    ActiveClaimToken = null
                });
        }

        var claims = new List<DispatchClaimState>(state.Claims.Count);
        foreach (var claim in state.Claims)
        {
            claims.Add(claim.Status is DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed
                ? claim with { Status = DispatchClaimStatus.Cancelled }
                : claim);
        }

        var next = state with
        {
            Status = ProcessRuntimeStatus.Cancelled,
            Steps = steps,
            Claims = claims,
            UpdatedAtUtc = context.OccurredAtUtc
        };
        return Applied(next, context, ProcessRuntimeEventTypes.ProcessRunCancelled, state.RunId.ToString());
    }
}
