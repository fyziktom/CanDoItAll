using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessRuntimeBranchSignalApplicationService(
    IProcessProjectionClock clock,
    IProcessRuntimeStateStore stateStore,
    IProcessRuntimeUnitOfWork unitOfWork,
    IProcessRuntimeStepAssignmentStore assignmentStore,
    ProcessRuntimeProjectionCatchupService projectionCatchupService)
{
    private const int MaximumConcurrencyRetries = 3;
    private const string ActorId = "process-runtime-branch-router";
    private static readonly TimeSpan ConcurrencyRetryDelay = TimeSpan.FromMilliseconds(100);

    public async Task ApplyForResultAsync(
        ProcessRuntimeStateSnapshot initialState,
        ProcessInstancePlan plan,
        StrategyResultEnvelope result,
        string requestedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(result);

        if (!HasBranchOutcomeSignal(result))
        {
            return;
        }

        for (var attempt = 1; attempt <= MaximumConcurrencyRetries; attempt++)
        {
            try
            {
                var state = attempt == 1
                    ? initialState
                    : await LoadRequiredStateAsync(initialState.RunId, cancellationToken).ConfigureAwait(false);

                await ApplyForResultCoreAsync(
                    state,
                    plan,
                    result,
                    requestedBy,
                    attempt == 1 ? "branch" : $"branch-retry-{attempt}",
                    cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (ProcessRuntimeOptimisticConcurrencyException) when (attempt < MaximumConcurrencyRetries)
            {
                await Task.Delay(ConcurrencyRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        var latestState = await LoadRequiredStateAsync(initialState.RunId, cancellationToken).ConfigureAwait(false);
        await ApplyForResultCoreAsync(
            latestState,
            plan,
            result,
            requestedBy,
            "branch-final-retry",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProcessRuntimeStateSnapshot> PropagateSkippedBranchGatesAsync(
        ProcessRuntimeStateSnapshot initialState,
        string requestedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initialState);

        if (ProcessRuntimeTerminalStates.IsRunTerminal(initialState.Status))
        {
            return initialState;
        }

        for (var attempt = 1; attempt <= MaximumConcurrencyRetries; attempt++)
        {
            try
            {
                var state = attempt == 1
                    ? initialState
                    : await LoadRequiredStateAsync(initialState.RunId, cancellationToken).ConfigureAwait(false);

                return await PropagateSkippedBranchGatesCoreAsync(
                    state,
                    requestedBy,
                    attempt == 1 ? "branch-skip-propagation" : $"branch-skip-propagation-retry-{attempt}",
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ProcessRuntimeOptimisticConcurrencyException) when (attempt < MaximumConcurrencyRetries)
            {
                await Task.Delay(ConcurrencyRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        var latestState = await LoadRequiredStateAsync(initialState.RunId, cancellationToken).ConfigureAwait(false);
        return await PropagateSkippedBranchGatesCoreAsync(
            latestState,
            requestedBy,
            "branch-skip-propagation-final-retry",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplyForResultCoreAsync(
        ProcessRuntimeStateSnapshot state,
        ProcessInstancePlan plan,
        StrategyResultEnvelope result,
        string requestedBy,
        string phase,
        CancellationToken cancellationToken)
    {
        if (ProcessRuntimeTerminalStates.IsRunTerminal(state.Status))
        {
            return;
        }

        var selectedOutcome = result.ManagerSignals
            .Select(signal => ProcessBranchSignalCodes.TryReadOutcome(signal, out var outcomeKey) ? outcomeKey : string.Empty)
            .FirstOrDefault(outcomeKey => !string.IsNullOrWhiteSpace(outcomeKey));
        if (string.IsNullOrWhiteSpace(selectedOutcome))
        {
            return;
        }

        var completedStep = state.Steps.FirstOrDefault(step =>
            step.CompletedResultKey is not null &&
            state.AppliedResults.Any(receipt =>
                receipt.StepInstanceId == step.StepInstanceId &&
                receipt.IdempotencyKey == step.CompletedResultKey &&
                receipt.ResultHash == result.ResultHash));
        if (completedStep is null)
        {
            return;
        }

        var completedPlanStep = plan.Steps.FirstOrDefault(step => step.StepInstanceId == completedStep.StepInstanceId);
        if (completedPlanStep is null)
        {
            return;
        }

        var assignments = await assignmentStore.LoadByRunAsync(state.RunId, cancellationToken).ConfigureAwait(false);
        var changed = false;
        var nextSteps = new List<ProcessRuntimeStepState>(state.Steps.Count);
        var events = new List<ProcessRuntimeEventEnvelope>();
        var context = CreateContext(requestedBy, phase);

        foreach (var step in state.Steps)
        {
            var assignment = assignments.FirstOrDefault(candidate => candidate.StepInstanceId == step.StepInstanceId);
            if (assignment?.BranchGate is null ||
                !string.Equals(assignment.BranchGate.SourceStepKey, completedPlanStep.StepKey, StringComparison.OrdinalIgnoreCase) ||
                ProcessRuntimeTerminalStates.IsStepTerminal(step.Status))
            {
                nextSteps.Add(step);
                continue;
            }

            if (string.Equals(assignment.BranchGate.RequiredOutcomeKey, selectedOutcome, StringComparison.OrdinalIgnoreCase))
            {
                if (step.Status != ProcessRuntimeStepStatus.Blocked)
                {
                    nextSteps.Add(step);
                    continue;
                }

                nextSteps.Add(step with
                {
                    Status = ProcessRuntimeStepStatus.Pending
                });
                changed = true;
                continue;
            }

            nextSteps.Add(step with
            {
                Status = ProcessRuntimeStepStatus.Skipped,
                ActiveClaimToken = null
            });
            changed = true;
            events.Add(CreateEvent(
                state,
                context,
                ProcessRuntimeEventTypes.StepSkipped,
                ComputeHash($"{completedPlanStep.StepKey}:{selectedOutcome}:{assignment.StepKey}")));
        }

        if (PropagateSkippedBranchGates(state, assignments, nextSteps, events, context))
        {
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        await CommitBranchStepMutationAsync(state, nextSteps, events, context, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProcessRuntimeStateSnapshot> PropagateSkippedBranchGatesCoreAsync(
        ProcessRuntimeStateSnapshot state,
        string requestedBy,
        string phase,
        CancellationToken cancellationToken)
    {
        if (ProcessRuntimeTerminalStates.IsRunTerminal(state.Status))
        {
            return state;
        }

        var assignments = await assignmentStore.LoadByRunAsync(state.RunId, cancellationToken).ConfigureAwait(false);
        var nextSteps = state.Steps.ToList();
        var events = new List<ProcessRuntimeEventEnvelope>();
        var context = CreateContext(requestedBy, phase);

        if (!PropagateSkippedBranchGates(state, assignments, nextSteps, events, context))
        {
            return state;
        }

        return await CommitBranchStepMutationAsync(state, nextSteps, events, context, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProcessRuntimeStateSnapshot> CommitBranchStepMutationAsync(
        ProcessRuntimeStateSnapshot state,
        IReadOnlyList<ProcessRuntimeStepState> nextSteps,
        IList<ProcessRuntimeEventEnvelope> events,
        RuntimeCommandContext context,
        CancellationToken cancellationToken)
    {
        var next = CreateBranchStepMutationState(state, nextSteps, events, context);
        var mutation = new ProcessRuntimeMutation(
            ProcessRuntimeTransitionOutcome.Applied,
            next,
            events.ToArray(),
            events.Select(runtimeEvent => new ProcessOutboxMessage(
                RuntimeOutboxMessageId.New(),
                runtimeEvent.EventId,
                ProcessOutboxSubscriberKind.RuntimeProjection,
                runtimeEvent.PayloadHash)).ToArray(),
            [],
            []);

        var commit = await unitOfWork.CommitAsync(
            new ProcessRuntimeCommitRequest(context.CommandId, state, mutation),
            cancellationToken).ConfigureAwait(false);
        await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);

        return commit.State;
    }

    private static bool PropagateSkippedBranchGates(
        ProcessRuntimeStateSnapshot state,
        IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
        IList<ProcessRuntimeStepState> steps,
        IList<ProcessRuntimeEventEnvelope> events,
        RuntimeCommandContext context)
    {
        var stepKeyById = assignments.ToDictionary(
            assignment => assignment.StepInstanceId,
            assignment => assignment.StepKey);
        var skippedSourceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skippedStepIds = new HashSet<ProcessStepInstanceId>();
        foreach (var step in steps)
        {
            if (step.Status == ProcessRuntimeStepStatus.Skipped &&
                stepKeyById.TryGetValue(step.StepInstanceId, out var stepKey))
            {
                skippedSourceKeys.Add(stepKey);
                skippedStepIds.Add(step.StepInstanceId);
            }
        }

        var changed = false;
        var changedInPass = true;
        while (changedInPass)
        {
            changedInPass = false;
            for (var index = 0; index < steps.Count; index++)
            {
                var step = steps[index];
                if (ProcessRuntimeTerminalStates.IsStepTerminal(step.Status) ||
                    step.Status is ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running)
                {
                    continue;
                }

                var assignment = assignments.FirstOrDefault(candidate => candidate.StepInstanceId == step.StepInstanceId);
                var skipEvidence = string.Empty;
                if (assignment?.BranchGate is not null &&
                    skippedSourceKeys.Contains(assignment.BranchGate.SourceStepKey))
                {
                    skipEvidence = $"{assignment.BranchGate.SourceStepKey}:source-skipped:{assignment.StepKey}";
                }
                else
                {
                    var skippedDependency = step.DependencyStepIds.FirstOrDefault(skippedStepIds.Contains);
                    if (skippedDependency != default)
                    {
                        skipEvidence = $"{skippedDependency}:dependency-skipped:{assignment?.StepKey ?? step.StepInstanceId.ToString()}";
                    }
                }

                if (string.IsNullOrWhiteSpace(skipEvidence))
                {
                    continue;
                }

                steps[index] = step with
                {
                    Status = ProcessRuntimeStepStatus.Skipped,
                    ActiveClaimToken = null
                };
                skippedStepIds.Add(step.StepInstanceId);
                if (assignment is not null)
                {
                    skippedSourceKeys.Add(assignment.StepKey);
                }

                changed = true;
                changedInPass = true;
                events.Add(CreateEvent(
                    state,
                    context,
                    ProcessRuntimeEventTypes.StepSkipped,
                    ComputeHash(skipEvidence)));
            }
        }

        return changed;
    }

    private static ProcessRuntimeStateSnapshot CreateBranchStepMutationState(
        ProcessRuntimeStateSnapshot state,
        IReadOnlyList<ProcessRuntimeStepState> nextSteps,
        IList<ProcessRuntimeEventEnvelope> events,
        RuntimeCommandContext context)
    {
        var nextStatus = ResolveRunStatus(state.Status, nextSteps);
        var next = state with
        {
            Steps = nextSteps,
            Status = nextStatus,
            UpdatedAtUtc = context.OccurredAtUtc
        };

        if (nextStatus != state.Status && ProcessRuntimeTerminalStates.IsRunTerminal(nextStatus))
        {
            events.Add(CreateEvent(
                next,
                context,
                ToRunTerminalEvent(nextStatus),
                next.PlanHash));
        }

        return next;
    }

    private static ProcessRuntimeStatus ResolveRunStatus(
        ProcessRuntimeStatus current,
        IReadOnlyList<ProcessRuntimeStepState> steps)
    {
        if (ProcessRuntimeTerminalStates.IsRunTerminal(current))
        {
            return current;
        }

        if (steps.Any(step => step.IsExecutable && step.Status == ProcessRuntimeStepStatus.Failed))
        {
            return ProcessRuntimeStatus.Failed;
        }

        if (steps.Where(step => step.IsExecutable).All(step => ProcessRuntimeTerminalStates.IsStepTerminal(step.Status)))
        {
            return ProcessRuntimeStatus.Completed;
        }

        return ProcessRuntimeStatus.Active;
    }

    private async Task<ProcessRuntimeStateSnapshot> LoadRequiredStateAsync(
        ProcessRunId runId,
        CancellationToken cancellationToken)
        => await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false)
           ?? throw new InvalidOperationException($"Process run '{runId}' was not found.");

    private RuntimeCommandContext CreateContext(
        string requestedBy,
        string phase)
    {
        var actorId = string.IsNullOrWhiteSpace(requestedBy)
            ? ActorId
            : requestedBy.Trim();
        return new RuntimeCommandContext(
            RuntimeCommandId.New(),
            new ProcessEventActor(ProcessEventActorKind.System, new ProcessActorId(ActorId)),
            new ProcessCorrelationId($"{phase}-{actorId}-{Guid.NewGuid():N}"),
            NormalizeUtc(clock.GetUtcNow()));
    }

    private static ProcessRuntimeEventEnvelope CreateEvent(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        ProcessEventType eventType,
        string payloadHash)
    {
        var runtimeEvent = new ProcessRuntimeEventEnvelope(
            RuntimeEventId.New(),
            state.RootRunId,
            state.RunId,
            context.CorrelationId,
            CausationId: null,
            context.Actor,
            ProcessContractVersions.RuntimeEventEnvelopeV1,
            ProcessEventSensitivity.Normal,
            context.OccurredAtUtc,
            eventType,
            payloadHash);
        var validation = ProcessRuntimeEventRules.Validate(runtimeEvent);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Failures[0].Message);
        }

        return runtimeEvent;
    }

    private static bool HasBranchOutcomeSignal(StrategyResultEnvelope result)
        => result.ManagerSignals.Any(signal => ProcessBranchSignalCodes.TryReadOutcome(signal, out _));

    private static ProcessEventType ToRunTerminalEvent(ProcessRuntimeStatus status)
    {
        return status switch
        {
            ProcessRuntimeStatus.Completed => ProcessRuntimeEventTypes.ProcessRunCompleted,
            ProcessRuntimeStatus.Failed => ProcessRuntimeEventTypes.ProcessRunFailed,
            ProcessRuntimeStatus.Cancelled => ProcessRuntimeEventTypes.ProcessRunCancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Run status is not terminal.")
        };
    }

    private static DateTimeOffset NormalizeUtc(DateTimeOffset value)
        => value.Offset == TimeSpan.Zero ? value : value.ToUniversalTime();

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
