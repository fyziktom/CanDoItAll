using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public sealed partial class ProcessRuntimeEngine
{
    private static bool HasOpenClaims(ProcessRuntimeStateSnapshot state)
    {
        foreach (var claim in state.Claims)
        {
            if (claim.Status is DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed)
            {
                return true;
            }
        }

        return false;
    }

    private static ProcessRuntimeStepState? FindStep(ProcessRuntimeStateSnapshot state, ProcessStepInstanceId stepId)
    {
        foreach (var step in state.Steps)
        {
            if (step.StepInstanceId == stepId)
            {
                return step;
            }
        }

        return null;
    }

    private static DispatchClaimState? FindClaim(ProcessRuntimeStateSnapshot state, DispatchClaimToken claimToken)
    {
        foreach (var claim in state.Claims)
        {
            if (claim.ClaimToken == claimToken)
            {
                return claim;
            }
        }

        return null;
    }

    private static StrategyResultReceipt? FindReceipt(
        ProcessRuntimeStateSnapshot state,
        ProcessStepInstanceId stepId,
        StrategyId strategyId,
        StrategyResultIdempotencyKey idempotencyKey)
    {
        foreach (var receipt in state.AppliedResults)
        {
            if (receipt.StepInstanceId == stepId &&
                receipt.StrategyId == strategyId &&
                receipt.IdempotencyKey == idempotencyKey)
            {
                return receipt;
            }
        }

        return null;
    }

    private static IReadOnlyList<ProcessRuntimeStepState> ReplaceStep(
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStepState replacement)
    {
        var steps = new List<ProcessRuntimeStepState>(state.Steps.Count);
        foreach (var step in state.Steps)
        {
            steps.Add(step.StepInstanceId == replacement.StepInstanceId ? replacement : step);
        }

        return steps;
    }

    private static IReadOnlyList<DispatchClaimState> ReplaceClaim(
        ProcessRuntimeStateSnapshot state,
        DispatchClaimState replacement)
    {
        var claims = new List<DispatchClaimState>(state.Claims.Count);
        foreach (var claim in state.Claims)
        {
            claims.Add(claim.ClaimToken == replacement.ClaimToken ? replacement : claim);
        }

        return claims;
    }

    private static void ReplaceStepInPlace(
        IList<ProcessRuntimeStepState> steps,
        ProcessStepInstanceId stepId,
        Func<ProcessRuntimeStepState, ProcessRuntimeStepState> update)
    {
        for (var index = 0; index < steps.Count; index++)
        {
            if (steps[index].StepInstanceId == stepId)
            {
                steps[index] = update(steps[index]);
                return;
            }
        }
    }

    private static IReadOnlyList<T> Append<T>(IReadOnlyList<T> source, T item)
    {
        var target = new List<T>(source.Count + 1);
        foreach (var value in source)
        {
            target.Add(value);
        }

        target.Add(item);
        return target;
    }

    private static bool Contains(IReadOnlyList<ProcessStepInstanceId> stepIds, ProcessStepInstanceId stepId)
    {
        foreach (var candidate in stepIds)
        {
            if (candidate == stepId)
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateArguments(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);
        context.Validate();
    }
}
