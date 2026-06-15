using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public sealed class ProcessStrategyDispatcher
{
    public async Task<StrategyResultEnvelope> InvokeAsync(
        DispatchWorkItem workItem,
        ProcessInstancePlan plan,
        IProcessStrategyFactory strategyFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(strategyFactory);

        var planStep = FindPlanStep(plan, workItem.StepInstanceId);
        if (planStep?.ExecutionStrategyBinding is null)
        {
            throw new InvalidOperationException("Dispatch work item must reference a planned executable step with a strategy binding.");
        }

        if (planStep.ExecutionStrategyBinding.StrategyId != workItem.StrategyBinding.StrategyId ||
            strategyFactory.Descriptor.StrategyId != workItem.StrategyBinding.StrategyId)
        {
            throw new InvalidOperationException("Dispatcher strategy binding does not match the immutable plan.");
        }

        var strategy = await strategyFactory.CreateAsync(workItem.StrategyBinding, cancellationToken).ConfigureAwait(false);
        var context = new ProcessStrategyExecutionContext(
            workItem.RunId,
            workItem.StepInstanceId,
            workItem.StrategyBinding,
            workItem.StrategyBinding.Inputs);

        return await strategy.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
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
