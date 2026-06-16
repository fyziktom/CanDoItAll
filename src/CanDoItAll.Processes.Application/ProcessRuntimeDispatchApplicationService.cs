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

public sealed record ProcessRuntimeDispatchResult(
    ProcessRunId RunId,
    ProcessLaunchStage Stage,
    ProcessRuntimeStatus Status,
    IReadOnlyList<string> Diagnostics);

public sealed class ProcessRuntimeDispatchApplicationService(
    IProcessProjectionClock clock,
    IProcessRuntimeStateStore stateStore,
    IProcessRuntimeUnitOfWork unitOfWork,
    IProcessInstancePlanStore planStore,
    IProcessRuntimeStepAssignmentStore assignmentStore,
    IProcessRuntimeStrategyFactoryResolver strategyFactoryResolver,
    ProcessRuntimeProjectionCatchupService projectionCatchupService)
{
    private const int MaximumDispatchIterations = 200;
    private static readonly TimeSpan DispatchLease = TimeSpan.FromMinutes(30);

    public async Task<ProcessRuntimeDispatchResult> ExecuteReadyAsync(
        ProcessRunId runId,
        string requestedBy,
        CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<string>();
        var scheduler = new ProcessRuntimeScheduler();
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var dispatcher = new ProcessStrategyDispatcher();

        for (var iteration = 0; iteration < MaximumDispatchIterations; iteration++)
        {
            var state = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Process run '{runId}' was not found.");
            if (ProcessRuntimeTerminalStates.IsRunTerminal(state.Status))
            {
                return new ProcessRuntimeDispatchResult(runId, ToStage(state.Status), state.Status, diagnostics);
            }

            var plan = await planStore.LoadAsync(state.PlanId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Process run '{runId}' references missing plan '{state.PlanId}'.");
            if (state.Status == ProcessRuntimeStatus.Active)
            {
                var scheduleCommit = await engine.ScheduleReadyAsync(
                    state,
                    CreateContext(requestedBy, iteration, "schedule"),
                    cancellationToken).ConfigureAwait(false);
                state = scheduleCommit.State;
            }

            var readyWork = scheduler.CalculateReadyWork(state, plan, NormalizeUtc(clock.GetUtcNow()));
            if (readyWork.Count == 0)
            {
                return new ProcessRuntimeDispatchResult(runId, ProcessLaunchStage.Running, state.Status, diagnostics);
            }

            foreach (var workItem in readyWork)
            {
                var claimToken = DispatchClaimToken.New();
                var ownerId = new DispatcherOwnerId("process-runtime-dispatcher");
                var claimCommit = await engine.CreateClaimAsync(
                    state,
                    CreateContext(requestedBy, iteration, "claim"),
                    new CreateDispatchClaimCommand(
                        workItem,
                        ownerId,
                        claimToken,
                        NormalizeUtc(clock.GetUtcNow()).Add(DispatchLease)),
                    cancellationToken).ConfigureAwait(false);
                if (!claimCommit.Succeeded)
                {
                    diagnostics.AddRange(claimCommit.Diagnostics.Select(diagnostic => diagnostic.Message));
                    continue;
                }

                var runningCommit = await engine.MarkClaimRunningAsync(
                    claimCommit.State,
                    CreateContext(requestedBy, iteration, "running"),
                    workItem.StepInstanceId,
                    claimToken,
                    cancellationToken).ConfigureAwait(false);
                if (!runningCommit.Succeeded)
                {
                    diagnostics.AddRange(runningCommit.Diagnostics.Select(diagnostic => diagnostic.Message));
                    continue;
                }

                var strategyFactory = await strategyFactoryResolver.ResolveAsync(
                    workItem.StrategyBinding,
                    cancellationToken).ConfigureAwait(false);
                var result = await dispatcher.InvokeAsync(
                    workItem,
                    plan,
                    strategyFactory,
                    cancellationToken).ConfigureAwait(false);
                var resultCommit = await engine.SubmitStrategyResultAsync(
                    runningCommit.State,
                    CreateContext(requestedBy, iteration, "result"),
                    new SubmitStrategyResultCommand(
                        workItem.StepInstanceId,
                        ownerId,
                        claimToken,
                        new StrategyResultIdempotencyKey(result.IdempotencyKey),
                        result),
                    cancellationToken).ConfigureAwait(false);
                if (!resultCommit.Succeeded)
                {
                    diagnostics.AddRange(resultCommit.Diagnostics.Select(diagnostic => diagnostic.Message));
                    continue;
                }

                await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);

                await ApplyBranchSignalsAsync(
                    resultCommit.State,
                    plan,
                    result,
                    requestedBy,
                    iteration,
                    cancellationToken).ConfigureAwait(false);

                state = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false) ?? resultCommit.State;
            }
        }

        var finalState = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Process run '{runId}' was not found after dispatch.");
        diagnostics.Add($"Dispatch stopped after {MaximumDispatchIterations} iterations.");
        return new ProcessRuntimeDispatchResult(runId, ToStage(finalState.Status), finalState.Status, diagnostics);
    }

    private async Task ApplyBranchSignalsAsync(
        ProcessRuntimeStateSnapshot state,
        ProcessInstancePlan plan,
        StrategyResultEnvelope result,
        string requestedBy,
        int iteration,
        CancellationToken cancellationToken)
    {
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
        var context = CreateContext(requestedBy, iteration, "branch");

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
                var unblocked = step with
                {
                    Status = ProcessRuntimeStepStatus.Pending
                };
                nextSteps.Add(unblocked);
                changed = true;
                continue;
            }

            var skipped = step with
            {
                Status = ProcessRuntimeStepStatus.Skipped,
                ActiveClaimToken = null
            };
            nextSteps.Add(skipped);
            changed = true;
            events.Add(CreateEvent(
                state,
                context,
                ProcessRuntimeEventTypes.StepSkipped,
                ComputeHash($"{completedPlanStep.StepKey}:{selectedOutcome}:{assignment.StepKey}")));
        }

        if (!changed)
        {
            return;
        }

        var next = state with
        {
            Steps = nextSteps,
            Status = ResolveRunStatus(state.Status, nextSteps),
            UpdatedAtUtc = context.OccurredAtUtc
        };
        var mutation = new ProcessRuntimeMutation(
            ProcessRuntimeTransitionOutcome.Applied,
            next,
            events,
            events.Select(runtimeEvent => new ProcessOutboxMessage(
                RuntimeOutboxMessageId.New(),
                runtimeEvent.EventId,
                ProcessOutboxSubscriberKind.RuntimeProjection,
                runtimeEvent.PayloadHash)).ToArray(),
            [],
            []);

        await unitOfWork.CommitAsync(
            new ProcessRuntimeCommitRequest(context.CommandId, state, mutation),
            cancellationToken).ConfigureAwait(false);
        await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ProcessRuntimeStatus ResolveRunStatus(
        ProcessRuntimeStatus current,
        IReadOnlyList<ProcessRuntimeStepState> steps)
    {
        if (current != ProcessRuntimeStatus.Active)
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

        return current;
    }

    private static RuntimeCommandContext CreateContext(
        string requestedBy,
        int iteration,
        string phase)
    {
        return new RuntimeCommandContext(
            RuntimeCommandId.New(),
            new ProcessEventActor(ProcessEventActorKind.System, new ProcessActorId("process-runtime-dispatcher")),
            new ProcessCorrelationId($"dispatch-{iteration}-{phase}-{Guid.NewGuid():N}"),
            NormalizeUtc(DateTimeOffset.UtcNow));
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

    private static DateTimeOffset NormalizeUtc(DateTimeOffset value)
    {
        return value.Offset == TimeSpan.Zero ? value : value.ToUniversalTime();
    }

    private static ProcessLaunchStage ToStage(ProcessRuntimeStatus status)
    {
        return status switch
        {
            ProcessRuntimeStatus.Completed => ProcessLaunchStage.Completed,
            ProcessRuntimeStatus.Failed or ProcessRuntimeStatus.Cancelled => ProcessLaunchStage.Failed,
            _ => ProcessLaunchStage.Running
        };
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
