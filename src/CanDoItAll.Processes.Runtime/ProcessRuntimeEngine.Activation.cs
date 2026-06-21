using CanDoItAll.Processes.Core;

namespace CanDoItAll.Processes.Runtime;

public sealed partial class ProcessRuntimeEngine
{
    private static ProcessRuntimeMutation Activate(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context)
    {
        ValidateArguments(state, context);

        if (state.Status == ProcessRuntimeStatus.Active)
        {
            return Duplicate(state);
        }

        if (ProcessRuntimeTerminalStates.IsRunTerminal(state.Status))
        {
            return ProcessRuntimeMutation.Rejected(
                state,
                "Runtime.TerminalRunImmutable",
                $"Run '{state.RunId}' is terminal and cannot be activated.");
        }

        if (state.Status != ProcessRuntimeStatus.Created)
        {
            return ProcessRuntimeMutation.Rejected(
                state,
                "Runtime.InvalidRunTransition",
                $"Run transition from '{state.Status}' to '{ProcessRuntimeStatus.Active}' is not allowed.");
        }

        var next = state with
        {
            Status = ProcessRuntimeStatus.Active,
            UpdatedAtUtc = context.OccurredAtUtc
        };

        return Applied(
            next,
            context,
            ProcessRuntimeEventTypes.ProcessRunActivated,
            state.PlanHash);
    }

    private ProcessRuntimeMutation ScheduleReady(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context)
    {
        ValidateArguments(state, context);

        if (state.Status != ProcessRuntimeStatus.Active)
        {
            return ProcessRuntimeMutation.Rejected(
                state,
                "Runtime.RunNotActive",
                "Ready scheduling requires an active run.");
        }

        var readyStepIds = scheduler.CalculateReadySteps(state);
        if (readyStepIds.Count == 0)
        {
            return Duplicate(state);
        }

        var steps = new List<ProcessRuntimeStepState>(state.Steps.Count);
        foreach (var step in state.Steps)
        {
            steps.Add(Contains(readyStepIds, step.StepInstanceId)
                ? step with { Status = ProcessRuntimeStepStatus.Ready }
                : step);
        }

        var next = state with
        {
            Steps = steps,
            UpdatedAtUtc = context.OccurredAtUtc
        };

        var events = new List<ProcessRuntimeEventEnvelope>(readyStepIds.Count);
        foreach (var stepId in readyStepIds)
        {
            events.Add(CreateEvent(
                next,
                context,
                ProcessRuntimeEventTypes.StepReady,
                stepId.ToString()));
        }

        return Applied(next, events);
    }
}
