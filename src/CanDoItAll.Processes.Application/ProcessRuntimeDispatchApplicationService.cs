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

public sealed class ProcessRuntimeDispatchOptions
{
    public TimeSpan DispatchLease { get; init; } = TimeSpan.FromMinutes(25);

    public TimeSpan StepExecutionTimeout { get; init; } = TimeSpan.FromMinutes(20);
}

public sealed class ProcessRuntimeDispatchApplicationService(
    IProcessProjectionClock clock,
    IProcessRuntimeStateStore stateStore,
    IProcessRuntimeUnitOfWork unitOfWork,
    IProcessInstancePlanStore planStore,
    IProcessRuntimeStepAssignmentStore assignmentStore,
    IProcessRuntimeStrategyFactoryResolver strategyFactoryResolver,
    ProcessRuntimeProjectionCatchupService projectionCatchupService,
    ProcessRuntimeDispatchOptions? options = null)
{
    private const int MaximumDispatchIterations = 200;
    private const int MaximumStepDispatchAttempts = 20;
    private const string DispatcherActorId = "process-runtime-dispatcher";
    private const string ClaimReleaseFailureExceptionDataKey = "ProcessDispatchClaimReleaseFailure";
    private readonly ProcessRuntimeDispatchOptions dispatchOptions = NormalizeOptions(options);

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
                var nowUtc = NormalizeUtc(clock.GetUtcNow());
                var expireCommit = await engine.ExpireClaimsAsync(
                    state,
                    CreateContext(requestedBy, iteration, "expire-claims"),
                    new ExpireDispatchClaimsCommand(nowUtc),
                    cancellationToken).ConfigureAwait(false);
                state = expireCommit.State;

                var scheduleCommit = await engine.ScheduleReadyAsync(
                    state,
                    CreateContext(requestedBy, iteration, "schedule"),
                    cancellationToken).ConfigureAwait(false);
                state = scheduleCommit.State;
                state = await ApplySkippedBranchGatePropagationAsync(
                    state,
                    requestedBy,
                    iteration,
                    cancellationToken).ConfigureAwait(false);
                if (ProcessRuntimeTerminalStates.IsRunTerminal(state.Status))
                {
                    return new ProcessRuntimeDispatchResult(runId, ToStage(state.Status), state.Status, diagnostics);
                }
            }

            var readyWork = scheduler.CalculateReadyWork(state, plan, NormalizeUtc(clock.GetUtcNow()));
            if (readyWork.Count == 0)
            {
                return new ProcessRuntimeDispatchResult(runId, ProcessLaunchStage.Running, state.Status, diagnostics);
            }

            foreach (var workItem in readyWork)
            {
                var claimToken = DispatchClaimToken.New();
                var ownerId = new DispatcherOwnerId(DispatcherActorId);
                var claimCreated = false;
                var resultSubmitted = false;
                try
                {
                    var claimCommit = await engine.CreateClaimAsync(
                        state,
                        CreateContext(requestedBy, iteration, "claim"),
                        new CreateDispatchClaimCommand(
                            workItem,
                            ownerId,
                            claimToken,
                            NormalizeUtc(clock.GetUtcNow()).Add(dispatchOptions.DispatchLease)),
                        cancellationToken).ConfigureAwait(false);
                    if (!claimCommit.Succeeded)
                    {
                        diagnostics.AddRange(claimCommit.Diagnostics.Select(diagnostic => diagnostic.Message));
                        continue;
                    }

                    claimCreated = true;

                    var runningCommit = await engine.MarkClaimRunningAsync(
                        claimCommit.State,
                        CreateContext(requestedBy, iteration, "running"),
                        workItem.StepInstanceId,
                        claimToken,
                        cancellationToken).ConfigureAwait(false);
                    if (!runningCommit.Succeeded)
                    {
                        diagnostics.AddRange(runningCommit.Diagnostics.Select(diagnostic => diagnostic.Message));
                        var releaseDiagnostic = await ReleaseClaimBestEffortAsync(
                            runId,
                            workItem.StepInstanceId,
                            ownerId,
                            claimToken,
                            requestedBy,
                            iteration,
                            CancellationToken.None).ConfigureAwait(false);
                        if (releaseDiagnostic is not null)
                        {
                            diagnostics.Add(releaseDiagnostic);
                        }

                        continue;
                    }

                    if (workItem.AttemptNumber > MaximumStepDispatchAttempts)
                    {
                        var overBudgetResult = CreateOverBudgetResult(workItem);
                        var overBudgetCommit = await engine.SubmitStrategyResultAsync(
                            runningCommit.State,
                            CreateContext(requestedBy, iteration, "attempt-budget"),
                            new SubmitStrategyResultCommand(
                                workItem.StepInstanceId,
                                ownerId,
                                claimToken,
                                new StrategyResultIdempotencyKey(overBudgetResult.IdempotencyKey),
                                overBudgetResult),
                            CancellationToken.None).ConfigureAwait(false);
                        if (!overBudgetCommit.Succeeded)
                        {
                            diagnostics.AddRange(overBudgetCommit.Diagnostics.Select(diagnostic => diagnostic.Message));
                            var releaseDiagnostic = await ReleaseClaimBestEffortAsync(
                                runId,
                                workItem.StepInstanceId,
                                ownerId,
                                claimToken,
                                requestedBy,
                                iteration,
                                CancellationToken.None).ConfigureAwait(false);
                            if (releaseDiagnostic is not null)
                            {
                                diagnostics.Add(releaseDiagnostic);
                            }

                            continue;
                        }

                        resultSubmitted = true;
                        diagnostics.Add($"Step '{workItem.StepInstanceId}' exceeded the dispatch retry limit of {MaximumStepDispatchAttempts} attempts.");
                        await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);
                        state = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false) ?? overBudgetCommit.State;
                        continue;
                    }

                    var strategyFactory = await strategyFactoryResolver.ResolveAsync(
                        workItem.StrategyBinding,
                        cancellationToken).ConfigureAwait(false);
                    var result = await InvokeStrategyWithTimeoutAsync(
                        dispatcher,
                        workItem,
                        plan,
                        strategyFactory,
                        dispatchOptions.StepExecutionTimeout,
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
                        CancellationToken.None).ConfigureAwait(false);
                    if (!resultCommit.Succeeded)
                    {
                        diagnostics.AddRange(resultCommit.Diagnostics.Select(diagnostic => diagnostic.Message));
                        var releaseDiagnostic = await ReleaseClaimBestEffortAsync(
                            runId,
                            workItem.StepInstanceId,
                            ownerId,
                            claimToken,
                            requestedBy,
                            iteration,
                            CancellationToken.None).ConfigureAwait(false);
                        if (releaseDiagnostic is not null)
                        {
                            diagnostics.Add(releaseDiagnostic);
                        }

                        continue;
                    }

                    resultSubmitted = true;

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
                catch (ProcessRuntimeDispatchDeferredException exception) when (claimCreated && !resultSubmitted)
                {
                    diagnostics.Add(exception.Message);
                    var deferDiagnostic = await DeferClaimBestEffortAsync(
                        runId,
                        workItem.StepInstanceId,
                        ownerId,
                        claimToken,
                        exception.DeferredRunId,
                        requestedBy,
                        iteration,
                        CancellationToken.None).ConfigureAwait(false);
                    if (deferDiagnostic is not null)
                    {
                        diagnostics.Add(deferDiagnostic);
                    }

                    await projectionCatchupService.CatchUpAsync(CancellationToken.None).ConfigureAwait(false);

                    var deferredState = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false) ?? state;
                    return new ProcessRuntimeDispatchResult(runId, ProcessLaunchStage.Running, deferredState.Status, diagnostics);
                }
                catch (Exception exception) when (claimCreated && !resultSubmitted)
                {
                    var releaseDiagnostic = await ReleaseClaimBestEffortAsync(
                        runId,
                        workItem.StepInstanceId,
                        ownerId,
                        claimToken,
                        requestedBy,
                        iteration,
                        CancellationToken.None).ConfigureAwait(false);
                    if (releaseDiagnostic is not null)
                    {
                        exception.Data[ClaimReleaseFailureExceptionDataKey] = releaseDiagnostic;
                    }

                    throw;
                }
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

        if (PropagateSkippedBranchGates(state, assignments, nextSteps, events, context))
        {
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        var next = CreateBranchStepMutationState(state, nextSteps, events, context);
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

    private async Task<ProcessRuntimeStateSnapshot> ApplySkippedBranchGatePropagationAsync(
        ProcessRuntimeStateSnapshot state,
        string requestedBy,
        int iteration,
        CancellationToken cancellationToken)
    {
        if (state.Status != ProcessRuntimeStatus.Active)
        {
            return state;
        }

        var assignments = await assignmentStore.LoadByRunAsync(state.RunId, cancellationToken).ConfigureAwait(false);
        var nextSteps = state.Steps.ToList();
        var events = new List<ProcessRuntimeEventEnvelope>();
        var context = CreateContext(requestedBy, iteration, "branch-skip-propagation");

        if (!PropagateSkippedBranchGates(state, assignments, nextSteps, events, context))
        {
            return state;
        }

        var next = CreateBranchStepMutationState(state, nextSteps, events, context);
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

    private async Task<string?> DeferClaimBestEffortAsync(
        ProcessRunId runId,
        ProcessStepInstanceId stepInstanceId,
        DispatcherOwnerId ownerId,
        DispatchClaimToken claimToken,
        ProcessRunId? deferredRunId,
        string requestedBy,
        int iteration,
        CancellationToken cancellationToken)
    {
        try
        {
            var state = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false);
            if (state is null)
            {
                return $"Dispatch claim deferral skipped because process run '{runId}' was not found.";
            }

            var engine = new ProcessRuntimeEngine(unitOfWork);
            var deferCommit = await engine.DeferClaimAsync(
                state,
                CreateContext(requestedBy, iteration, "defer-claim"),
                new DeferDispatchClaimCommand(stepInstanceId, ownerId, claimToken, deferredRunId),
                cancellationToken).ConfigureAwait(false);
            if (!deferCommit.Succeeded)
            {
                var deferMessages = string.Join("; ", deferCommit.Diagnostics.Select(diagnostic => diagnostic.Message));
                return $"Dispatch claim deferral rejected for run '{runId}', step '{stepInstanceId}', token '{claimToken}': {deferMessages}";
            }

            await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);

            return null;
        }
        catch (Exception exception)
        {
            return $"Dispatch claim deferral failed for run '{runId}', step '{stepInstanceId}', token '{claimToken}': {exception.Message}";
        }
    }

    private async Task<string?> ReleaseClaimBestEffortAsync(
        ProcessRunId runId,
        ProcessStepInstanceId stepInstanceId,
        DispatcherOwnerId ownerId,
        DispatchClaimToken claimToken,
        string requestedBy,
        int iteration,
        CancellationToken cancellationToken)
    {
        try
        {
            var state = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false);
            if (state is null)
            {
                return $"Dispatch claim release skipped because process run '{runId}' was not found.";
            }

            var engine = new ProcessRuntimeEngine(unitOfWork);
            var releaseCommit = await engine.ReleaseClaimAsync(
                state,
                CreateContext(requestedBy, iteration, "release-claim"),
                new ReleaseDispatchClaimCommand(stepInstanceId, ownerId, claimToken),
                cancellationToken).ConfigureAwait(false);
            if (!releaseCommit.Succeeded)
            {
                var releaseMessages = string.Join("; ", releaseCommit.Diagnostics.Select(diagnostic => diagnostic.Message));
                return $"Dispatch claim release rejected for run '{runId}', step '{stepInstanceId}', token '{claimToken}': {releaseMessages}";
            }

            await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);

            return null;
        }
        catch (Exception exception)
        {
            return $"Dispatch claim release failed for run '{runId}', step '{stepInstanceId}', token '{claimToken}': {exception.Message}";
        }
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

    private static StrategyResultEnvelope CreateOverBudgetResult(DispatchWorkItem workItem)
    {
        var summary = $"Step '{workItem.StepInstanceId}' exceeded the dispatch retry limit of {MaximumStepDispatchAttempts} attempts and requires manager review before another retry.";
        var stableKey = $"process-runtime:dispatch-attempt-budget:{workItem.RunId}:{workItem.StepInstanceId}:{MaximumStepDispatchAttempts}";
        var resultHash = ComputeHash($"{stableKey}:{workItem.AttemptNumber}");
        return new StrategyResultEnvelope(
            workItem.StrategyBinding.StrategyId,
            workItem.StrategyBinding.StrategyVersion,
            CreateDeterministicGuid(stableKey),
            StrategyOutcome.NeedsManager,
            [],
            [],
            [
                new StrategyDiagnosticRef(
                    new StrategyDiagnosticCode("process.runtime.dispatch_attempt_budget_exceeded"),
                    StrategyDiagnosticSensitivity.Normal,
                    resultHash,
                    summary,
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.UnsafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ],
            [
                new ManagerSignal(
                    new ManagerSignalCode("process.runtime.dispatch_attempt_budget_exceeded"),
                    resultHash,
                    summary)
            ],
            resultHash);
    }

    private static async Task<StrategyResultEnvelope> InvokeStrategyWithTimeoutAsync(
        ProcessStrategyDispatcher dispatcher,
        DispatchWorkItem workItem,
        ProcessInstancePlan plan,
        IProcessStrategyFactory strategyFactory,
        TimeSpan stepExecutionTimeout,
        CancellationToken cancellationToken)
    {
        var stepExecution = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stepExecution.CancelAfter(stepExecutionTimeout);
        var invocationTask = Task.Run(
            async () => await dispatcher
                .InvokeAsync(workItem, plan, strategyFactory, stepExecution.Token)
                .ConfigureAwait(false),
            CancellationToken.None);

        try
        {
            var result = await invocationTask.WaitAsync(stepExecutionTimeout, cancellationToken).ConfigureAwait(false);
            stepExecution.Dispose();
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ObserveLateStrategyCompletion(invocationTask, stepExecution);
            return CreateExecutionTimeoutResult(workItem, stepExecutionTimeout);
        }
        catch (TimeoutException)
        {
            await stepExecution.CancelAsync().ConfigureAwait(false);
            ObserveLateStrategyCompletion(invocationTask, stepExecution);
            return CreateExecutionTimeoutResult(workItem, stepExecutionTimeout);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await stepExecution.CancelAsync().ConfigureAwait(false);
            ObserveLateStrategyCompletion(invocationTask, stepExecution);
            throw;
        }
    }

    private static StrategyResultEnvelope CreateExecutionTimeoutResult(
        DispatchWorkItem workItem,
        TimeSpan stepExecutionTimeout)
    {
        var summary = $"Step '{workItem.StepInstanceId}' exceeded the per-step execution timeout of {stepExecutionTimeout.TotalMinutes:N0} minutes and requires operator review before another retry.";
        var stableKey = $"process-runtime:step-execution-timeout:{workItem.RunId}:{workItem.StepInstanceId}:{workItem.AttemptNumber}";
        var resultHash = ComputeHash(stableKey);
        return new StrategyResultEnvelope(
            workItem.StrategyBinding.StrategyId,
            workItem.StrategyBinding.StrategyVersion,
            CreateDeterministicGuid(stableKey),
            StrategyOutcome.Failed,
            [],
            [],
            [
                new StrategyDiagnosticRef(
                    new StrategyDiagnosticCode("process.runtime.step_execution_timeout"),
                    StrategyDiagnosticSensitivity.Normal,
                    resultHash,
                    summary,
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.SafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ],
            [
                new ManagerSignal(
                    new ManagerSignalCode("process.runtime.step_execution_timeout"),
                    resultHash,
                    summary)
            ],
            resultHash);
    }

    private static void ObserveLateStrategyCompletion(
        Task<StrategyResultEnvelope> invocationTask,
        CancellationTokenSource stepExecution)
    {
        _ = invocationTask.ContinueWith(
            static (task, state) =>
            {
                ((CancellationTokenSource)state!).Dispose();
                _ = task.Exception;
            },
            stepExecution,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static ProcessRuntimeDispatchOptions NormalizeOptions(ProcessRuntimeDispatchOptions? options)
    {
        options ??= new ProcessRuntimeDispatchOptions();
        if (options.DispatchLease <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Process runtime dispatch lease must be greater than zero.");
        }

        if (options.StepExecutionTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Process runtime step execution timeout must be greater than zero.");
        }

        if (options.DispatchLease <= options.StepExecutionTimeout)
        {
            throw new InvalidOperationException("Process runtime dispatch lease must be greater than the step execution timeout so valid step results can be accepted before the claim expires.");
        }

        return options;
    }

    private static RuntimeCommandContext CreateContext(
        string requestedBy,
        int iteration,
        string phase)
    {
        return new RuntimeCommandContext(
            RuntimeCommandId.New(),
            new ProcessEventActor(ProcessEventActorKind.System, new ProcessActorId(DispatcherActorId)),
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

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static Guid CreateDeterministicGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
