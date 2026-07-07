using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;

namespace CanDoItAll.Processes.Runtime;

public sealed class ProcessRuntimeScheduler
{
    public IReadOnlyList<ProcessStepInstanceId> CalculateReadySteps(ProcessRuntimeStateSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Status != ProcessRuntimeStatus.Active)
        {
            return [];
        }

        var readySteps = new List<ProcessStepInstanceId>();
        foreach (var step in state.Steps)
        {
            if (step.Status != ProcessRuntimeStepStatus.Pending)
            {
                continue;
            }

            if (ProcessRuntimeArtifactContracts.DependenciesSatisfied(state, step) &&
                ProcessRuntimeArtifactContracts.RequiredArtifactsAvailable(state, step))
            {
                readySteps.Add(step.StepInstanceId);
            }
        }

        return readySteps;
    }

    public IReadOnlyList<DispatchWorkItem> CalculateReadyWork(
        ProcessRuntimeStateSnapshot state,
        ProcessInstancePlan plan,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(plan);

        if (nowUtc.Offset != TimeSpan.Zero || state.Status != ProcessRuntimeStatus.Active)
        {
            return [];
        }

        var readyWork = new List<DispatchWorkItem>();
        foreach (var step in state.Steps)
        {
            if (step.Status != ProcessRuntimeStepStatus.Ready ||
                HasActiveClaim(state, step, nowUtc))
            {
                continue;
            }

            var planStep = FindPlanStep(plan, step.StepInstanceId);
            if (planStep?.ExecutionStrategyBinding is null)
            {
                continue;
            }

            readyWork.Add(new DispatchWorkItem(
                state.RunId,
                step.StepInstanceId,
                step.StepDefinitionId,
                planStep.ExecutionStrategyBinding,
                step.AttemptNumber + 1,
                ProcessRuntimeArtifactContracts.BuildStepContract(state, step)));
        }

        return readyWork;
    }

    private static bool HasActiveClaim(
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStepState step,
        DateTimeOffset nowUtc)
    {
        if (step.ActiveClaimToken is null)
        {
            return false;
        }

        foreach (var claim in state.Claims)
        {
            if (claim.ClaimToken == step.ActiveClaimToken &&
                claim.ExpiresAtUtc > nowUtc &&
                claim.Status is DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed)
            {
                return true;
            }
        }

        return false;
    }

    private static StepInstancePlan? FindPlanStep(ProcessInstancePlan plan, ProcessStepInstanceId stepId)
    {
        foreach (var step in plan.Steps)
        {
            if (step.StepInstanceId == stepId)
            {
                return step;
            }
        }

        return null;
    }
}
