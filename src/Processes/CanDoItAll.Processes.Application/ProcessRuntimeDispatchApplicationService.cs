using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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

    public TimeSpan PreRunningClaimStaleAfter { get; init; } = TimeSpan.FromMinutes(2);
}

public sealed class ProcessRuntimeDispatchApplicationService(
    IProcessProjectionClock clock,
    IProcessRuntimeStateStore stateStore,
    IProcessRuntimeUnitOfWork unitOfWork,
    IProcessInstancePlanStore planStore,
    IProcessRuntimeStepAssignmentStore assignmentStore,
    IProcessRuntimeStrategyFactoryResolver strategyFactoryResolver,
    ProcessRuntimeProjectionCatchupService projectionCatchupService,
    ProcessRuntimeDispatchOptions? options = null,
    IProcessRuntimeDispatchQueue? dispatchQueue = null,
    IProcessRuntimeBranchSignalRouter? branchSignalRouterOverride = null,
    IProcessStepRecoveryInstructionBuilder? recoveryInstructionBuilder = null)
{
    private const int MaximumDispatchIterations = 200;
    private const int MaximumStepDispatchAttempts = 20;
    private const int MaximumClaimCleanupConcurrencyRetries = 3;
    private const string DispatcherActorId = "process-runtime-dispatcher";
    private const string ClaimReleaseFailureExceptionDataKey = "ProcessDispatchClaimReleaseFailure";
    private const string ManagerRecoveryInstructionHeading = ProcessRuntimeRecoveryInstructionHeadings.ManagerRecovery;
    private const string RuntimeDiagnosticRecoveryInstructionHeading = ProcessRuntimeRecoveryInstructionHeadings.RuntimeDiagnosticRecovery;
    private static readonly Regex PriorRuntimeRecoveryInstructionBlockRegex = new(
        $@"(?ms)^\s*(?:{Regex.Escape(ManagerRecoveryInstructionHeading)}|{Regex.Escape(RuntimeDiagnosticRecoveryInstructionHeading)}):\s*.*?(?=^\s*(?:{Regex.Escape(ManagerRecoveryInstructionHeading)}|{Regex.Escape(RuntimeDiagnosticRecoveryInstructionHeading)}):\s*|^\s*{Regex.Escape(ProcessAutomaticRecoveryPromptBuilder.ExecutionFocusHeading)}\s*|\z)",
        RegexOptions.CultureInvariant);
    private static readonly TimeSpan ClaimCleanupConcurrencyRetryDelay = TimeSpan.FromMilliseconds(100);
    private readonly ProcessRuntimeDispatchOptions dispatchOptions = NormalizeOptions(options);
    private readonly IProcessStepRecoveryInstructionBuilder recoveryInstructionBuilder =
        recoveryInstructionBuilder ?? ProcessStepRecoveryInstructionBuilder.Instance;
    private readonly IProcessRuntimeBranchSignalRouter branchSignalRouter =
        branchSignalRouterOverride ?? new ProcessRuntimeBranchSignalApplicationService(
            clock,
            stateStore,
            unitOfWork,
            assignmentStore,
            projectionCatchupService);

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

            if (state.Status == ProcessRuntimeStatus.Blocked)
            {
                return new ProcessRuntimeDispatchResult(runId, ToStage(state.Status), state.Status, diagnostics);
            }

            if (state.Status == ProcessRuntimeStatus.CancelRequested)
            {
                return new ProcessRuntimeDispatchResult(runId, ToStage(state.Status), state.Status, diagnostics);
            }

            var plan = await planStore.LoadAsync(state.PlanId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Process run '{runId}' references missing plan '{state.PlanId}'.");
            if (state.Status == ProcessRuntimeStatus.Created)
            {
                var activateCommit = await ExecuteLifecycleTransitionWithConcurrencyRetryAsync(
                    runId,
                    requestedBy,
                    iteration,
                    "activate",
                    reloadedState => engine.ActivateAsync(
                        reloadedState,
                        CreateContext(requestedBy, iteration, "activate"),
                        cancellationToken),
                    cancellationToken).ConfigureAwait(false);
                state = activateCommit.State;
            }

            if (state.Status == ProcessRuntimeStatus.Active)
            {
                var nowUtc = NormalizeUtc(clock.GetUtcNow());
                var expireCommit = await ExecuteLifecycleTransitionWithConcurrencyRetryAsync(
                    runId,
                    requestedBy,
                    iteration,
                    "expire-claims",
                    reloadedState => engine.ExpireClaimsAsync(
                        reloadedState,
                        CreateContext(requestedBy, iteration, "expire-claims"),
                        new ExpireDispatchClaimsCommand(nowUtc),
                        cancellationToken),
                    cancellationToken).ConfigureAwait(false);
                state = expireCommit.State;
                var stalePreRunningClaimCleanup = await ReleaseStalePreRunningClaimsWithConcurrencyRetryAsync(
                    engine,
                    state,
                    requestedBy,
                    iteration,
                    nowUtc,
                    diagnostics,
                    cancellationToken).ConfigureAwait(false);
                state = stalePreRunningClaimCleanup.State;
                if (stalePreRunningClaimCleanup.ReleasedAny)
                {
                    continue;
                }

                var scheduleCommit = await ExecuteLifecycleTransitionWithConcurrencyRetryAsync(
                    runId,
                    requestedBy,
                    iteration,
                    "schedule",
                    reloadedState => engine.ScheduleReadyAsync(
                        reloadedState,
                        CreateContext(requestedBy, iteration, "schedule"),
                        cancellationToken),
                    cancellationToken).ConfigureAwait(false);
                state = scheduleCommit.State;
                state = await branchSignalRouter.PropagateSkippedBranchGatesAsync(
                    state,
                    requestedBy,
                    cancellationToken).ConfigureAwait(false);
                if (ProcessRuntimeTerminalStates.IsRunTerminal(state.Status))
                {
                    return new ProcessRuntimeDispatchResult(runId, ToStage(state.Status), state.Status, diagnostics);
                }
            }

            var readyWork = scheduler.CalculateReadyWork(state, plan, NormalizeUtc(clock.GetUtcNow()));
            if (readyWork.Count == 0)
            {
                var blockedState = await BlockExhaustedRunWithConcurrencyRetryAsync(
                    state,
                    requestedBy,
                    iteration,
                    cancellationToken).ConfigureAwait(false);
                if (blockedState is not null)
                {
                    return new ProcessRuntimeDispatchResult(runId, ToStage(blockedState.Status), blockedState.Status, diagnostics);
                }

                return new ProcessRuntimeDispatchResult(runId, ProcessLaunchStage.Running, state.Status, diagnostics);
            }

            var reloadAfterConcurrentClaimChange = false;
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

                    var runningCommit = await MarkClaimRunningWithConcurrencyRetryAsync(
                        engine,
                        runId,
                        claimCommit.State,
                        workItem.StepInstanceId,
                        claimToken,
                        requestedBy,
                        iteration,
                        cancellationToken).ConfigureAwait(false);
                    if (runningCommit is null)
                    {
                        diagnostics.Add(CreateOptimisticConcurrencyRetryDiagnostic(runId, "marking a dispatch claim running"));
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

                        reloadAfterConcurrentClaimChange = true;
                        break;
                    }

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

                    var latestStateBeforeStrategy = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidOperationException($"Process run '{runId}' was not found after marking a dispatch claim running.");
                    if (ProcessRuntimeTerminalStates.IsRunTerminal(latestStateBeforeStrategy.Status))
                    {
                        state = latestStateBeforeStrategy;
                        reloadAfterConcurrentClaimChange = true;
                        break;
                    }

                    if (!IsClaimStillActive(latestStateBeforeStrategy, workItem.StepInstanceId, ownerId, claimToken))
                    {
                        diagnostics.Add(CreateOptimisticConcurrencyRetryDiagnostic(runId, "verifying a dispatch claim before strategy execution"));
                        state = latestStateBeforeStrategy;
                        reloadAfterConcurrentClaimChange = true;
                        break;
                    }

                    state = latestStateBeforeStrategy;

                    if (CountSubmittedResults(state, workItem.StepInstanceId) >= MaximumStepDispatchAttempts)
                    {
                        var overBudgetResult = CreateOverBudgetResult(workItem);
                        var overBudgetCommit = await engine.SubmitStrategyResultAsync(
                            state,
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
                    var resultCommand = new SubmitStrategyResultCommand(
                        workItem.StepInstanceId,
                        ownerId,
                        claimToken,
                        new StrategyResultIdempotencyKey(result.IdempotencyKey),
                        result);
                    var resultCommit = await SubmitStrategyResultWithConcurrencyRetryAsync(
                        engine,
                        runId,
                        state,
                        resultCommand,
                        requestedBy,
                        iteration,
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
                    AddBlockedManagerResultDiagnostics(diagnostics, resultCommit.State, workItem.StepInstanceId, result);

                    await ApplyStrategyRecoveryInstructionAsync(
                        workItem.RunId,
                        workItem.StepInstanceId,
                        resultCommit.State,
                        result,
                        cancellationToken).ConfigureAwait(false);

                    await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);

                    await branchSignalRouter.ApplyForResultAsync(
                        resultCommit.State,
                        plan,
                        result,
                        requestedBy,
                        cancellationToken).ConfigureAwait(false);

                    state = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false) ?? resultCommit.State;
                }
                catch (ProcessRuntimeOptimisticConcurrencyException) when (!claimCreated)
                {
                    diagnostics.Add(CreateOptimisticConcurrencyRetryDiagnostic(runId, "creating a dispatch claim"));
                    await Task.Delay(ClaimCleanupConcurrencyRetryDelay, cancellationToken).ConfigureAwait(false);
                    reloadAfterConcurrentClaimChange = true;
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

                    if (exception.DeferredRunId is { } deferredRunId &&
                        deferredRunId != runId &&
                        dispatchQueue is not null)
                    {
                        await dispatchQueue.EnqueueAsync(
                            new ProcessRuntimeDispatchQueueRequest(deferredRunId, requestedBy),
                            CancellationToken.None).ConfigureAwait(false);
                    }

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

                if (reloadAfterConcurrentClaimChange)
                {
                    break;
                }
            }

            if (reloadAfterConcurrentClaimChange)
            {
                continue;
            }
        }

        var finalState = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Process run '{runId}' was not found after dispatch.");
        diagnostics.Add($"Dispatch stopped after {MaximumDispatchIterations} iterations.");
        return new ProcessRuntimeDispatchResult(runId, ToStage(finalState.Status), finalState.Status, diagnostics);
    }

    private async Task<ProcessRuntimeStateSnapshot?> BlockExhaustedRunWithConcurrencyRetryAsync(
        ProcessRuntimeStateSnapshot initialState,
        string requestedBy,
        int iteration,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaximumClaimCleanupConcurrencyRetries; attempt++)
        {
            try
            {
                var state = initialState;
                if (attempt > 1)
                {
                    state = await stateStore.LoadAsync(initialState.RunId, cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidOperationException($"Process run '{initialState.RunId}' was not found.");
                }

                var context = CreateContext(
                    requestedBy,
                    iteration,
                    attempt == 1 ? "block-exhausted-run" : $"block-exhausted-run-retry-{attempt}");
                var mutation = CreateBlockedRunMutationIfExhausted(state, context);
                if (mutation is null)
                {
                    return null;
                }

                var commit = await unitOfWork.CommitAsync(
                    new ProcessRuntimeCommitRequest(context.CommandId, state, mutation),
                    cancellationToken).ConfigureAwait(false);
                await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);

                return commit.Succeeded ? commit.State : null;
            }
            catch (ProcessRuntimeOptimisticConcurrencyException) when (attempt < MaximumClaimCleanupConcurrencyRetries)
            {
                await Task.Delay(ClaimCleanupConcurrencyRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        var latestState = await stateStore.LoadAsync(initialState.RunId, cancellationToken).ConfigureAwait(false);
        return latestState?.Status == ProcessRuntimeStatus.Blocked ? latestState : null;
    }

    private static ProcessRuntimeMutation? CreateBlockedRunMutationIfExhausted(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context)
    {
        if (!ShouldBlockExhaustedRun(state))
        {
            return null;
        }

        var next = state with
        {
            Status = ProcessRuntimeStatus.Blocked,
            UpdatedAtUtc = context.OccurredAtUtc
        };
        var unresolvedStepIds = next.Steps
            .Where(step => step.IsExecutable && !ProcessRuntimeTerminalStates.IsStepTerminal(step.Status))
            .Select(step => step.StepInstanceId.ToString())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var runtimeEvent = CreateEvent(
            next,
            context,
            ProcessRuntimeEventTypes.ProcessRunBlocked,
            ComputeHash($"blocked:{next.RunId}:{string.Join(";", unresolvedStepIds)}"));

        return new ProcessRuntimeMutation(
            ProcessRuntimeTransitionOutcome.Applied,
            next,
            [runtimeEvent],
            [
                new ProcessOutboxMessage(
                    RuntimeOutboxMessageId.New(),
                    runtimeEvent.EventId,
                    ProcessOutboxSubscriberKind.RuntimeProjection,
                    runtimeEvent.PayloadHash)
            ],
            [],
            []);
    }

    private async Task ApplyBranchSignalsWithConcurrencyRetryAsync(
        ProcessRuntimeStateSnapshot initialState,
        ProcessInstancePlan plan,
        StrategyResultEnvelope result,
        string requestedBy,
        int iteration,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaximumClaimCleanupConcurrencyRetries; attempt++)
        {
            try
            {
                var state = initialState;
                if (attempt > 1)
                {
                    state = await stateStore.LoadAsync(initialState.RunId, cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidOperationException($"Process run '{initialState.RunId}' was not found.");
                }

                await ApplyBranchSignalsAsync(
                    state,
                    plan,
                    result,
                    requestedBy,
                    iteration,
                    attempt == 1 ? "branch" : $"branch-retry-{attempt}",
                    cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (ProcessRuntimeOptimisticConcurrencyException) when (attempt < MaximumClaimCleanupConcurrencyRetries)
            {
                await Task.Delay(ClaimCleanupConcurrencyRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        var latestState = await stateStore.LoadAsync(initialState.RunId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Process run '{initialState.RunId}' was not found.");
        await ApplyBranchSignalsAsync(
            latestState,
            plan,
            result,
            requestedBy,
            iteration,
            "branch-final-retry",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplyBranchSignalsAsync(
        ProcessRuntimeStateSnapshot state,
        ProcessInstancePlan plan,
        StrategyResultEnvelope result,
        string requestedBy,
        int iteration,
        string phase,
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
        var context = CreateContext(requestedBy, iteration, phase);

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

    private async Task<ProcessRuntimeStateSnapshot> ApplySkippedBranchGatePropagationWithConcurrencyRetryAsync(
        ProcessRuntimeStateSnapshot initialState,
        string requestedBy,
        int iteration,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaximumClaimCleanupConcurrencyRetries; attempt++)
        {
            try
            {
                var state = initialState;
                if (attempt > 1)
                {
                    state = await stateStore.LoadAsync(initialState.RunId, cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidOperationException($"Process run '{initialState.RunId}' was not found.");
                }

                return await ApplySkippedBranchGatePropagationAsync(
                    state,
                    requestedBy,
                    iteration,
                    attempt == 1 ? "branch-skip-propagation" : $"branch-skip-propagation-retry-{attempt}",
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ProcessRuntimeOptimisticConcurrencyException) when (attempt < MaximumClaimCleanupConcurrencyRetries)
            {
                await Task.Delay(ClaimCleanupConcurrencyRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        var latestState = await stateStore.LoadAsync(initialState.RunId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Process run '{initialState.RunId}' was not found.");
        return await ApplySkippedBranchGatePropagationAsync(
            latestState,
            requestedBy,
            iteration,
            "branch-skip-propagation-final-retry",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProcessRuntimeStateSnapshot> ApplySkippedBranchGatePropagationAsync(
        ProcessRuntimeStateSnapshot state,
        string requestedBy,
        int iteration,
        string phase,
        CancellationToken cancellationToken)
    {
        if (state.Status != ProcessRuntimeStatus.Active)
        {
            return state;
        }

        var assignments = await assignmentStore.LoadByRunAsync(state.RunId, cancellationToken).ConfigureAwait(false);
        var nextSteps = state.Steps.ToList();
        var events = new List<ProcessRuntimeEventEnvelope>();
        var context = CreateContext(requestedBy, iteration, phase);

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
        for (var attempt = 1; attempt <= MaximumClaimCleanupConcurrencyRetries; attempt++)
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
            catch (ProcessRuntimeOptimisticConcurrencyException) when (attempt < MaximumClaimCleanupConcurrencyRetries)
            {
                await Task.Delay(ClaimCleanupConcurrencyRetryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                return $"Dispatch claim deferral failed for run '{runId}', step '{stepInstanceId}', token '{claimToken}': {exception.Message}";
            }
        }

        return $"Dispatch claim deferral failed for run '{runId}', step '{stepInstanceId}', token '{claimToken}': state changed after {MaximumClaimCleanupConcurrencyRetries} retries.";
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
        for (var attempt = 1; attempt <= MaximumClaimCleanupConcurrencyRetries; attempt++)
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
            catch (ProcessRuntimeOptimisticConcurrencyException) when (attempt < MaximumClaimCleanupConcurrencyRetries)
            {
                await Task.Delay(ClaimCleanupConcurrencyRetryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                return $"Dispatch claim release failed for run '{runId}', step '{stepInstanceId}', token '{claimToken}': {exception.Message}";
            }
        }

        return $"Dispatch claim release failed for run '{runId}', step '{stepInstanceId}', token '{claimToken}': state changed after {MaximumClaimCleanupConcurrencyRetries} retries.";
    }

    private async Task ApplyStrategyRecoveryInstructionAsync(
        ProcessRunId runId,
        ProcessStepInstanceId stepInstanceId,
        ProcessRuntimeStateSnapshot state,
        StrategyResultEnvelope result,
        CancellationToken cancellationToken)
    {
        if (result.Outcome != StrategyOutcome.NeedsManager)
        {
            return;
        }

        var stepStatus = state.Steps.FirstOrDefault(step => step.StepInstanceId == stepInstanceId)?.Status;
        if (stepStatus is not (ProcessRuntimeStepStatus.Ready or ProcessRuntimeStepStatus.Blocked))
        {
            return;
        }

        var assignment = await assignmentStore.LoadAsync(runId, stepInstanceId, cancellationToken).ConfigureAwait(false);
        if (assignment is null)
        {
            return;
        }

        var receipt = FindLatestReceipt(state, stepInstanceId, result.ResultHash);
        var recoveryInstruction = recoveryInstructionBuilder.Build(new ProcessStepRecoveryInstructionBuildRequest(
            runId,
            stepInstanceId,
            assignment.StepKey,
            assignment,
            result,
            receipt,
            OperatorReason: string.Empty));
        if (stepStatus == ProcessRuntimeStepStatus.Ready &&
            !recoveryInstruction.HasInstruction)
        {
            return;
        }

        var instruction = stepStatus == ProcessRuntimeStepStatus.Blocked
            ? BuildManagerRecoveryInstruction(result, recoveryInstruction)
            : BuildRuntimeDiagnosticRecoveryInstruction(result, recoveryInstruction);
        var normalizedPrompt = RemovePriorRuntimeRecoveryInstructionBlocks(assignment.Prompt).TrimEnd();
        var prompt = stepStatus == ProcessRuntimeStepStatus.Ready && recoveryInstruction.HasInstruction
            ? ProcessAutomaticRecoveryPromptBuilder.Build(
                assignment with { Prompt = normalizedPrompt },
                instruction)
            : $"""
              {normalizedPrompt}

              {instruction}
              """;
        if (string.Equals(prompt, assignment.Prompt, StringComparison.Ordinal))
        {
            return;
        }

        await assignmentStore.SaveAsync([assignment with { Prompt = prompt }], cancellationToken).ConfigureAwait(false);
    }

    private static StrategyResultReceipt? FindLatestReceipt(
        ProcessRuntimeStateSnapshot state,
        ProcessStepInstanceId stepInstanceId,
        string resultHash)
        => state.AppliedResults
            .LastOrDefault(receipt =>
                receipt.StepInstanceId == stepInstanceId &&
                string.Equals(receipt.ResultHash, resultHash, StringComparison.Ordinal));

    private static string BuildRuntimeDiagnosticRecoveryInstruction(
        StrategyResultEnvelope result,
        ProcessStepRecoveryInstruction recoveryInstruction)
    {
        var packetText = recoveryInstruction.HasInstruction
            ? recoveryInstruction.Text.Trim()
            : "Retry the current step only after resolving the runtime diagnostic that rejected the previous result.";
        return $"""
        {RuntimeDiagnosticRecoveryInstructionHeading}:
        The previous result was safe and idempotent to rework, so the runtime reopened this same step instead of escalating to the process manager. Result hash: {result.ResultHash}
        {packetText}
        """;
    }

    private static string BuildManagerRecoveryInstruction(
        StrategyResultEnvelope result,
        ProcessStepRecoveryInstruction recoveryInstruction)
    {
        var diagnostics = result.Diagnostics
            .Select(diagnostic => $"- {diagnostic.Code.Value}: {diagnostic.SafeSummary}")
            .ToArray();
        var diagnosticText = diagnostics.Length == 0
            ? "- The previous completion result requires manager review."
            : string.Join(Environment.NewLine, diagnostics);
        var requestedArtifactText = result.RequestedArtifacts.Count == 0
            ? "- No artifact slots were explicitly requested by the strategy result."
            : string.Join(
                Environment.NewLine,
                result.RequestedArtifacts.Select(artifact => $"- {artifact.SlotId}: {artifact.RequestHash}"));

        return $"""
        {ManagerRecoveryInstructionHeading}:
        The previous result blocked the step and must be reviewed by the process manager before any rework is dispatched. Result hash: {result.ResultHash}
        Confirm every required input artifact, requested artifact slot, tool receipt, and expected output artifact before requesting rework. If a diagnostic points to a missing upstream artifact, rework the responsible producer step first; do not retry this step until the connected input receipt is available. Do not satisfy missing evidence by only rewriting summaries or deferring required work to a later step.
        Diagnostics:
        {diagnosticText}
        Diagnostic repair packet:
        {ResolveDiagnosticRepairPacketText(recoveryInstruction)}
        Requested artifacts:
        {requestedArtifactText}
        """;
    }

    private static string ResolveDiagnosticRepairPacketText(ProcessStepRecoveryInstruction recoveryInstruction)
        => recoveryInstruction.HasInstruction
            ? recoveryInstruction.Text.Trim()
            : "- No diagnostic-specific repair packet was available for this result; manager review must name the concrete evidence or contract change before redispatch.";

    private static string RemovePriorRuntimeRecoveryInstructionBlocks(string prompt)
        => PriorRuntimeRecoveryInstructionBlockRegex.Replace(prompt, string.Empty).TrimEnd();

    private static void AddBlockedManagerResultDiagnostics(
        List<string> diagnostics,
        ProcessRuntimeStateSnapshot state,
        ProcessStepInstanceId stepInstanceId,
        StrategyResultEnvelope result)
    {
        if (result.Outcome != StrategyOutcome.NeedsManager ||
            state.Steps.FirstOrDefault(step => step.StepInstanceId == stepInstanceId)?.Status != ProcessRuntimeStepStatus.Blocked)
        {
            return;
        }

        diagnostics.AddRange(result.Diagnostics
            .Select(diagnostic => diagnostic.SafeSummary)
            .Where(summary => !string.IsNullOrWhiteSpace(summary)));
    }

    private async Task<ProcessRuntimeCommitResult> SubmitStrategyResultWithConcurrencyRetryAsync(
        ProcessRuntimeEngine engine,
        ProcessRunId runId,
        ProcessRuntimeStateSnapshot initialState,
        SubmitStrategyResultCommand command,
        string requestedBy,
        int iteration,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaximumClaimCleanupConcurrencyRetries; attempt++)
        {
            try
            {
                var state = initialState;
                if (attempt > 1)
                {
                    state = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidOperationException($"Process run '{runId}' was not found.");
                }

                return await engine.SubmitStrategyResultAsync(
                    state,
                    CreateContext(requestedBy, iteration, attempt == 1 ? "result" : $"result-retry-{attempt}"),
                    command,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ProcessRuntimeOptimisticConcurrencyException) when (attempt < MaximumClaimCleanupConcurrencyRetries)
            {
                await Task.Delay(ClaimCleanupConcurrencyRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        var latestState = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Process run '{runId}' was not found.");
        return await engine.SubmitStrategyResultAsync(
            latestState,
            CreateContext(requestedBy, iteration, "result-final-retry"),
            command,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProcessRuntimeCommitResult> ExecuteLifecycleTransitionWithConcurrencyRetryAsync(
        ProcessRunId runId,
        string requestedBy,
        int iteration,
        string operation,
        Func<ProcessRuntimeStateSnapshot, Task<ProcessRuntimeCommitResult>> execute,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaximumClaimCleanupConcurrencyRetries; attempt++)
        {
            try
            {
                var state = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"Process run '{runId}' was not found.");
                return await execute(state).ConfigureAwait(false);
            }
            catch (ProcessRuntimeOptimisticConcurrencyException) when (attempt < MaximumClaimCleanupConcurrencyRetries)
            {
                await Task.Delay(ClaimCleanupConcurrencyRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        var latestState = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Process run '{runId}' was not found.");
        return await execute(latestState).ConfigureAwait(false);
    }

    private async Task<ProcessRuntimeCommitResult?> MarkClaimRunningWithConcurrencyRetryAsync(
        ProcessRuntimeEngine engine,
        ProcessRunId runId,
        ProcessRuntimeStateSnapshot initialState,
        ProcessStepInstanceId stepInstanceId,
        DispatchClaimToken claimToken,
        string requestedBy,
        int iteration,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaximumClaimCleanupConcurrencyRetries; attempt++)
        {
            try
            {
                var state = initialState;
                if (attempt > 1)
                {
                    state = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidOperationException($"Process run '{runId}' was not found.");
                }

                return await engine.MarkClaimRunningAsync(
                    state,
                    CreateContext(requestedBy, iteration, attempt == 1 ? "running" : $"running-retry-{attempt}"),
                    stepInstanceId,
                    claimToken,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ProcessRuntimeOptimisticConcurrencyException) when (attempt < MaximumClaimCleanupConcurrencyRetries)
            {
                await Task.Delay(ClaimCleanupConcurrencyRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        return null;
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

    private static bool ShouldBlockExhaustedRun(ProcessRuntimeStateSnapshot state)
    {
        if (state.Status != ProcessRuntimeStatus.Active || HasOpenDispatchClaim(state))
        {
            return false;
        }

        var hasUnresolvedExecutableStep = false;
        foreach (var step in state.Steps)
        {
            if (!step.IsExecutable)
            {
                continue;
            }

            if (step.ActiveClaimToken is not null)
            {
                return false;
            }

            if (step.Status is ProcessRuntimeStepStatus.Ready or
                ProcessRuntimeStepStatus.Claimed or
                ProcessRuntimeStepStatus.Running or
                ProcessRuntimeStepStatus.Waiting or
                ProcessRuntimeStepStatus.WaitingApproval)
            {
                return false;
            }

            if (ProcessRuntimeTerminalStates.IsStepTerminal(step.Status))
            {
                continue;
            }

            hasUnresolvedExecutableStep = true;
            if (step.Status == ProcessRuntimeStepStatus.Pending &&
                ProcessRuntimeArtifactContracts.DependenciesSatisfied(state, step) &&
                ProcessRuntimeArtifactContracts.RequiredArtifactsAvailable(state, step))
            {
                return false;
            }
        }

        return hasUnresolvedExecutableStep;
    }

    private static bool HasOpenDispatchClaim(ProcessRuntimeStateSnapshot state)
    {
        foreach (var claim in state.Claims)
        {
            if (claim.Status is DispatchClaimStatus.Claimed or
                DispatchClaimStatus.LeaseRenewed or
                DispatchClaimStatus.Reclaimed)
            {
                return true;
            }
        }

        return false;
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

    private static int CountSubmittedResults(
        ProcessRuntimeStateSnapshot state,
        ProcessStepInstanceId stepInstanceId)
    {
        var count = 0;
        foreach (var receipt in state.AppliedResults)
        {
            if (receipt.StepInstanceId == stepInstanceId)
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsClaimStillActive(
        ProcessRuntimeStateSnapshot state,
        ProcessStepInstanceId stepInstanceId,
        DispatcherOwnerId ownerId,
        DispatchClaimToken claimToken)
    {
        var step = state.Steps.FirstOrDefault(candidate => candidate.StepInstanceId == stepInstanceId);
        if (step is null ||
            step.ActiveClaimToken != claimToken ||
            step.Status is not (ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running))
        {
            return false;
        }

        return state.Claims.Any(candidate =>
            candidate.StepInstanceId == stepInstanceId &&
            candidate.OwnerId == ownerId &&
            candidate.ClaimToken == claimToken &&
            candidate.Status is DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed);
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

    private async Task<StalePreRunningClaimCleanupResult> ReleaseStalePreRunningClaimsWithConcurrencyRetryAsync(
        ProcessRuntimeEngine engine,
        ProcessRuntimeStateSnapshot state,
        string requestedBy,
        int iteration,
        DateTimeOffset nowUtc,
        List<string> diagnostics,
        CancellationToken cancellationToken)
    {
        var releasedAny = false;
        for (var releaseCount = 0; releaseCount < state.Claims.Count; releaseCount++)
        {
            if (!TryResolveStalePreRunningClaim(state, nowUtc, out var step, out var claim))
            {
                return new StalePreRunningClaimCleanupResult(state, releasedAny);
            }

            for (var attempt = 1; attempt <= MaximumClaimCleanupConcurrencyRetries; attempt++)
            {
                try
                {
                    var releaseCommit = await engine.ReleaseClaimAsync(
                        state,
                        CreateContext(requestedBy, iteration, "release-stale-pre-running-claim"),
                        new ReleaseDispatchClaimCommand(step.StepInstanceId, claim.OwnerId, claim.ClaimToken),
                        cancellationToken).ConfigureAwait(false);
                    if (!releaseCommit.Succeeded)
                    {
                        diagnostics.AddRange(releaseCommit.Diagnostics.Select(diagnostic => diagnostic.Message));
                        return new StalePreRunningClaimCleanupResult(state, releasedAny);
                    }

                    diagnostics.Add($"Released stale pre-running dispatch claim '{claim.ClaimToken}' for step '{step.StepInstanceId}'.");
                    state = releaseCommit.State;
                    releasedAny = true;
                    break;
                }
                catch (ProcessRuntimeOptimisticConcurrencyException) when (attempt < MaximumClaimCleanupConcurrencyRetries)
                {
                    await Task.Delay(ClaimCleanupConcurrencyRetryDelay, cancellationToken).ConfigureAwait(false);
                    state = await stateStore.LoadAsync(state.RunId, cancellationToken).ConfigureAwait(false) ?? state;
                }
            }
        }

        return new StalePreRunningClaimCleanupResult(state, releasedAny);
    }

    private sealed record StalePreRunningClaimCleanupResult(
        ProcessRuntimeStateSnapshot State,
        bool ReleasedAny);

    private bool TryResolveStalePreRunningClaim(
        ProcessRuntimeStateSnapshot state,
        DateTimeOffset nowUtc,
        out ProcessRuntimeStepState step,
        out DispatchClaimState claim)
    {
        var staleBeforeUtc = nowUtc - dispatchOptions.PreRunningClaimStaleAfter;
        foreach (var candidateStep in state.Steps.OrderBy(item => item.StepInstanceId.Value))
        {
            if (candidateStep.Status != ProcessRuntimeStepStatus.Claimed ||
                candidateStep.ActiveClaimToken is not { } claimToken)
            {
                continue;
            }

            var candidateClaim = state.Claims.FirstOrDefault(item => item.ClaimToken == claimToken);
            if (candidateClaim is null ||
                candidateClaim.Status is not (DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed) ||
                candidateClaim.CreatedAtUtc > staleBeforeUtc)
            {
                continue;
            }

            step = candidateStep;
            claim = candidateClaim;
            return true;
        }

        step = null!;
        claim = null!;
        return false;
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

        if (options.PreRunningClaimStaleAfter <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Process runtime pre-running claim stale threshold must be greater than zero.");
        }

        if (options.DispatchLease <= options.StepExecutionTimeout)
        {
            throw new InvalidOperationException("Process runtime dispatch lease must be greater than the step execution timeout so valid step results can be accepted before the claim expires.");
        }

        if (options.PreRunningClaimStaleAfter >= options.DispatchLease)
        {
            throw new InvalidOperationException("Process runtime pre-running claim stale threshold must be shorter than the dispatch lease.");
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
            ProcessRuntimeStatus.Blocked => ProcessLaunchStage.Blocked,
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

    private static string CreateOptimisticConcurrencyRetryDiagnostic(
        ProcessRunId runId,
        string phase)
        => $"Process run '{runId}' changed concurrently while {phase}; retrying with the latest runtime state.";

    private static Guid CreateDeterministicGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
