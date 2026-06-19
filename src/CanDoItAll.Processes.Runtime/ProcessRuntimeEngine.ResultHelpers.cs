using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public sealed partial class ProcessRuntimeEngine
{
    private static ProcessRuntimeStepStatus ToStepStatus(StrategyResultEnvelope result)
    {
        if (IsAutomaticallyRetryableManagerResult(result))
        {
            return ProcessRuntimeStepStatus.Ready;
        }

        return result.Outcome switch
        {
            StrategyOutcome.Succeeded => ProcessRuntimeStepStatus.Completed,
            StrategyOutcome.Failed => ProcessRuntimeStepStatus.Failed,
            StrategyOutcome.Waiting or StrategyOutcome.NeedsManager => ProcessRuntimeStepStatus.Blocked,
            StrategyOutcome.Canceled => ProcessRuntimeStepStatus.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "Unknown strategy outcome.")
        };
    }

    private static bool IsAutomaticallyRetryableManagerResult(StrategyResultEnvelope result)
    {
        return result.Outcome == StrategyOutcome.NeedsManager &&
               result.Diagnostics.Count > 0 &&
               result.Diagnostics.All(IsAutomaticallyRetryableDiagnostic) &&
               result.ManagerSignals.Any(signal => signal.Code.Value.StartsWith("process.adapter.", StringComparison.Ordinal));
    }

    private static bool IsAutomaticallyRetryableDiagnostic(StrategyDiagnosticRef diagnostic)
    {
        return diagnostic.RetrySafety == ProcessDiagnosticRetrySafety.SafeToRetry &&
               diagnostic.Idempotency == ProcessDiagnosticIdempotencyClassification.Idempotent &&
               diagnostic.Code.Value.StartsWith("process.adapter.", StringComparison.Ordinal);
    }

    private static IReadOnlyList<ProcessRuntimeEventEnvelope> BuildResultEvents(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        SubmitStrategyResultCommand command,
        ProcessRuntimeStepStatus stepStatus)
    {
        var events = new List<ProcessRuntimeEventEnvelope>
        {
            CreateEvent(state, context, ProcessRuntimeEventTypes.DispatchClaimCompleted, command.ClaimToken.ToString()),
            CreateEvent(state, context, ToStepEventType(stepStatus), command.Result.ResultHash)
        };

        if (state.Status == ProcessRuntimeStatus.Completed)
        {
            events.Add(CreateEvent(state, context, ProcessRuntimeEventTypes.ProcessRunCompleted, state.PlanHash));
        }
        else if (state.Status == ProcessRuntimeStatus.Failed)
        {
            events.Add(CreateEvent(state, context, ProcessRuntimeEventTypes.ProcessRunFailed, command.Result.ResultHash));
        }
        else if (state.Status == ProcessRuntimeStatus.Cancelled)
        {
            events.Add(CreateEvent(state, context, ProcessRuntimeEventTypes.ProcessRunCancelled, command.Result.ResultHash));
        }

        return events;
    }

    private static ProcessEventType ToStepEventType(ProcessRuntimeStepStatus status)
    {
        return status switch
        {
            ProcessRuntimeStepStatus.Completed => ProcessRuntimeEventTypes.StepCompleted,
            ProcessRuntimeStepStatus.Failed => ProcessRuntimeEventTypes.StepFailed,
            ProcessRuntimeStepStatus.Ready => ProcessRuntimeEventTypes.StepReady,
            ProcessRuntimeStepStatus.Blocked => ProcessRuntimeEventTypes.StepBlocked,
            ProcessRuntimeStepStatus.Cancelled => ProcessRuntimeEventTypes.StepCancelled,
            _ => ProcessRuntimeEventTypes.StepBlocked
        };
    }

    private static IReadOnlyList<ProcessArtifactLedgerEvent> BuildArtifactLedgerEvents(
        RuntimeEventId eventId,
        SubmitStrategyResultCommand command)
    {
        if (command.Result.ProducedArtifacts.Count == 0)
        {
            return [];
        }

        var ledgerEvents = new List<ProcessArtifactLedgerEvent>(command.Result.ProducedArtifacts.Count);
        foreach (var artifact in command.Result.ProducedArtifacts)
        {
            ledgerEvents.Add(new ProcessArtifactLedgerEvent(
                ArtifactLedgerEventId.New(),
                eventId,
                artifact.SlotId,
                artifact.ArtifactId,
                artifact.ContentHash));
        }

        return ledgerEvents;
    }

    private static ProcessRuntimeStateSnapshot CompleteRunIfTerminal(
        ProcessRuntimeStateSnapshot state,
        DateTimeOffset occurredAtUtc)
    {
        var hasOpenExecutableSteps = false;
        var hasFailedStep = false;
        var hasCancelledStep = false;
        foreach (var step in state.Steps)
        {
            if (!step.IsExecutable)
            {
                continue;
            }

            if (step.Status == ProcessRuntimeStepStatus.Failed)
            {
                hasFailedStep = true;
                break;
            }

            if (step.Status == ProcessRuntimeStepStatus.Cancelled)
            {
                hasCancelledStep = true;
            }

            if (!ProcessRuntimeTerminalStates.IsStepTerminal(step.Status))
            {
                hasOpenExecutableSteps = true;
            }
        }

        if (hasFailedStep)
        {
            return state with
            {
                Status = ProcessRuntimeStatus.Failed,
                UpdatedAtUtc = occurredAtUtc
            };
        }

        if (hasCancelledStep && !hasOpenExecutableSteps)
        {
            return state with
            {
                Status = ProcessRuntimeStatus.Cancelled,
                UpdatedAtUtc = occurredAtUtc
            };
        }

        if (!hasOpenExecutableSteps)
        {
            return state with
            {
                Status = ProcessRuntimeStatus.Completed,
                UpdatedAtUtc = occurredAtUtc
            };
        }

        return state;
    }

    private static IReadOnlySet<ArtifactSlotId> AddProducedSlots(
        IReadOnlySet<ArtifactSlotId> availableSlots,
        StrategyResultEnvelope result)
    {
        if (result.ProducedArtifacts.Count == 0)
        {
            return availableSlots;
        }

        var next = new HashSet<ArtifactSlotId>(availableSlots);
        foreach (var producedArtifact in result.ProducedArtifacts)
        {
            next.Add(producedArtifact.SlotId);
        }

        return next;
    }
}
