using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using System.Text.RegularExpressions;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessRuntimeOperatorApplicationService(
    IProcessProjectionClock clock,
    IProcessRuntimeStateStore stateStore,
    IProcessRuntimeRunHierarchyStore runHierarchyStore,
    IProcessRuntimeStepAssignmentStore assignmentStore,
    IProcessRuntimeUnitOfWork unitOfWork,
    IProcessRuntimeDispatchQueue dispatchQueue,
    ProcessRuntimeProjectionCatchupService projectionCatchupService,
    IEnumerable<IProcessRuntimeStepAssignmentRepairService> assignmentRepairServices,
    IEnumerable<IProcessRuntimeRunCancellationObserver>? cancellationObservers = null,
    IProcessStepRecoveryInstructionBuilder? recoveryInstructionBuilder = null)
{
    private const string OperatorActorId = "process-runtime-operator";
    private const int MaximumCancellationConcurrencyRetries = 3;
    private const int MaximumCancellationCascadePasses = 5;
    private const string ReworkInstructionHeading = ProcessRuntimeRecoveryInstructionHeadings.OperatorRework;
    private const string ManagerRecoveryInstructionHeading = ProcessRuntimeRecoveryInstructionHeadings.ManagerRecovery;
    private const string RuntimeDiagnosticRecoveryInstructionHeading = ProcessRuntimeRecoveryInstructionHeadings.RuntimeDiagnosticRecovery;
    private static readonly Regex PriorReworkInstructionBlockRegex = new(
        $@"(?ms)^\s*(?:{Regex.Escape(ManagerRecoveryInstructionHeading)}|{Regex.Escape(ReworkInstructionHeading)}|{Regex.Escape(RuntimeDiagnosticRecoveryInstructionHeading)}):\s*.*?(?=^\s*(?:{Regex.Escape(ManagerRecoveryInstructionHeading)}|{Regex.Escape(ReworkInstructionHeading)}|{Regex.Escape(RuntimeDiagnosticRecoveryInstructionHeading)}):\s*|\z)",
        RegexOptions.CultureInvariant);
    private readonly IReadOnlyList<IProcessRuntimeRunCancellationObserver> cancellationObservers =
        (cancellationObservers ?? []).ToArray();
    private readonly IProcessStepRecoveryInstructionBuilder recoveryInstructionBuilder =
        recoveryInstructionBuilder ?? ProcessStepRecoveryInstructionBuilder.Instance;
    private static readonly TimeSpan CancellationConcurrencyRetryDelay = TimeSpan.FromMilliseconds(50);

    public async Task<ProcessRuntimeOperatorActionResult> ExecuteAsync(
        ProcessRuntimeOperatorActionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command.Kind switch
        {
            ProcessRuntimeOperatorActionKind.RequestRework => await RequestReworkAsync(command, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command.Kind, "Unsupported process runtime operator action.")
        };
    }

    public async Task<ProcessRuntimeRunCancellationResult> RequestCancellationAsync(
        ProcessRuntimeRunCancellationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var state = await stateStore.LoadAsync(command.RunId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Process run '{command.RunId}' was not found.");
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var cascadeDiagnostics = new List<string>();
        var cancelledDescendantRunIds = new List<ProcessRunId>();
        var cancelledRunIds = new List<ProcessRunId>();
        var observerNotifiedRunIds = new HashSet<ProcessRunId>();
        ProcessRuntimeCommitResult commit;
        if (state.RunId != state.RootRunId)
        {
            commit = await engine.RequestCancellationAsync(
                state,
                CreateContext(command.RequestedBy),
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var beginCommit = await ExecuteRootCancellationTransitionWithRetryAsync(
                command.RunId,
                reloadedState => engine.BeginRootCancellationAsync(
                    reloadedState,
                    CreateContext(command.RequestedBy),
                    cancellationToken),
                cancellationToken).ConfigureAwait(false);
            if (!beginCommit.Succeeded)
            {
                return new ProcessRuntimeRunCancellationResult(
                    command.RunId,
                    ProcessRuntimeOperatorActionKind.CancelRun,
                    beginCommit.Outcome,
                    beginCommit.State.Status,
                    beginCommit.Diagnostics.Select(diagnostic => diagnostic.Message).ToArray());
            }

            cascadeDiagnostics.AddRange(await NotifyCancellationObserversAsync(
                    command,
                    [command.RunId],
                    cancellationToken)
                .ConfigureAwait(false));
            observerNotifiedRunIds.Add(command.RunId);

            var failedDescendantRunIds = new HashSet<ProcessRunId>();
            for (var pass = 0; pass < MaximumCancellationCascadePasses; pass++)
            {
                var descendantRunIds = await runHierarchyStore
                    .FindCancellableDescendantRunIdsAsync(state.RunId, cancellationToken)
                    .ConfigureAwait(false);
                if (descendantRunIds.Count == 0)
                {
                    break;
                }

                foreach (var descendantRunId in descendantRunIds)
                {
                    var descendantCancelled = await CancelDescendantWithRetryAsync(
                        engine,
                        descendantRunId,
                        command.RequestedBy,
                        cascadeDiagnostics,
                        cancelledDescendantRunIds,
                        cancellationToken).ConfigureAwait(false);
                    if (!descendantCancelled)
                    {
                        failedDescendantRunIds.Add(descendantRunId);
                    }
                }

                if (failedDescendantRunIds.Count > 0)
                {
                    break;
                }
            }

            if (cancelledDescendantRunIds.Count > 0)
            {
                var distinctCancelledDescendantRunIds = cancelledDescendantRunIds.Distinct().ToArray();
                cascadeDiagnostics.AddRange(await NotifyCancellationObserversAsync(
                        command,
                        distinctCancelledDescendantRunIds,
                        cancellationToken)
                    .ConfigureAwait(false));
                observerNotifiedRunIds.UnionWith(distinctCancelledDescendantRunIds);
            }

            var remainingDescendantRunIds = await runHierarchyStore
                .FindCancellableDescendantRunIdsAsync(state.RunId, cancellationToken)
                .ConfigureAwait(false);
            if (remainingDescendantRunIds.Count > 0)
            {
                var failedIds = string.Join(
                    ", ",
                    remainingDescendantRunIds
                        .OrderBy(runId => runId.Value)
                        .Select(runId => runId.Value.ToString("D")));
                cascadeDiagnostics.Add(
                    $"Root cancellation remains pending because these descendant process runs are still cancellable: {failedIds}.");
                var pendingRoot = await stateStore.LoadAsync(command.RunId, cancellationToken).ConfigureAwait(false)
                    ?? beginCommit.State;
                return new ProcessRuntimeRunCancellationResult(
                    command.RunId,
                    ProcessRuntimeOperatorActionKind.CancelRun,
                    ProcessRuntimeTransitionOutcome.Rejected,
                    pendingRoot.Status,
                    cascadeDiagnostics);
            }

            commit = await ExecuteRootCancellationTransitionWithRetryAsync(
                command.RunId,
                reloadedState => engine.FinalizeRootCancellationAsync(
                    reloadedState,
                    CreateContext(command.RequestedBy),
                    cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        if (commit.Succeeded)
        {
            cancelledRunIds.AddRange(cancelledDescendantRunIds);
            cancelledRunIds.Add(command.RunId);
            await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);
        }

        if (cancelledDescendantRunIds.Count > 0)
        {
            var descendants = string.Join(", ", cancelledDescendantRunIds.Select(runId => runId.Value.ToString("D")));
            cascadeDiagnostics.Insert(0, $"Cancellation cascaded to {cancelledDescendantRunIds.Count} descendant process run(s): {descendants}.");
        }

        if (cancelledRunIds.Count > 0)
        {
            var unobservedCancelledRunIds = cancelledRunIds
                .Distinct()
                .Where(runId => !observerNotifiedRunIds.Contains(runId))
                .ToArray();
            if (unobservedCancelledRunIds.Length > 0)
            {
                cascadeDiagnostics.AddRange(await NotifyCancellationObserversAsync(
                        command,
                        unobservedCancelledRunIds,
                        cancellationToken)
                    .ConfigureAwait(false));
            }
        }

        return new ProcessRuntimeRunCancellationResult(
            command.RunId,
            ProcessRuntimeOperatorActionKind.CancelRun,
            commit.Outcome,
            commit.State.Status,
            cascadeDiagnostics
                .Concat(commit.Diagnostics.Select(diagnostic => diagnostic.Message))
                .ToArray());
    }

    private async Task<bool> CancelDescendantWithRetryAsync(
        ProcessRuntimeEngine engine,
        ProcessRunId descendantRunId,
        string requestedBy,
        List<string> diagnostics,
        List<ProcessRunId> cancelledDescendantRunIds,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaximumCancellationConcurrencyRetries; attempt++)
        {
            var descendantState = await stateStore.LoadAsync(descendantRunId, cancellationToken).ConfigureAwait(false);
            if (descendantState is null)
            {
                diagnostics.Add($"Descendant process run '{descendantRunId.Value:D}' disappeared before cancellation.");
                return false;
            }

            if (!IsCancellable(descendantState.Status))
            {
                if (descendantState.Status == ProcessRuntimeStatus.Cancelled)
                {
                    cancelledDescendantRunIds.Add(descendantRunId);
                }

                return true;
            }

            try
            {
                var descendantCommit = await engine.RequestCancellationAsync(
                    descendantState,
                    CreateContext(requestedBy),
                    cancellationToken).ConfigureAwait(false);
                if (descendantCommit.Succeeded)
                {
                    cancelledDescendantRunIds.Add(descendantRunId);
                    return true;
                }

                diagnostics.AddRange(descendantCommit.Diagnostics.Select(diagnostic =>
                    $"Descendant process run '{descendantRunId.Value:D}' was not cancelled: {diagnostic.Message}"));
            }
            catch (ProcessRuntimeOptimisticConcurrencyException exception)
            {
                if (attempt + 1 < MaximumCancellationConcurrencyRetries)
                {
                    await Task.Delay(CancellationConcurrencyRetryDelay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                diagnostics.Add(
                    $"Descendant process run '{descendantRunId.Value:D}' changed during every cancellation attempt: {exception.Message}");
            }

            var currentState = await stateStore.LoadAsync(descendantRunId, cancellationToken).ConfigureAwait(false);
            if (currentState is not null && !IsCancellable(currentState.Status))
            {
                if (currentState.Status == ProcessRuntimeStatus.Cancelled)
                {
                    cancelledDescendantRunIds.Add(descendantRunId);
                }

                return true;
            }

            if (attempt + 1 < MaximumCancellationConcurrencyRetries)
            {
                await Task.Delay(CancellationConcurrencyRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        diagnostics.Add(
            $"Descendant process run '{descendantRunId.Value:D}' remained cancellable after {MaximumCancellationConcurrencyRetries} cancellation attempts.");
        return false;
    }

    private async Task<ProcessRuntimeCommitResult> ExecuteRootCancellationTransitionWithRetryAsync(
        ProcessRunId rootRunId,
        Func<ProcessRuntimeStateSnapshot, Task<ProcessRuntimeCommitResult>> transition,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var state = await stateStore.LoadAsync(rootRunId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Process run '{rootRunId}' was not found.");
            try
            {
                return await transition(state).ConfigureAwait(false);
            }
            catch (ProcessRuntimeOptimisticConcurrencyException) when (attempt + 1 < MaximumCancellationConcurrencyRetries)
            {
                await Task.Delay(CancellationConcurrencyRetryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (ProcessRuntimeOptimisticConcurrencyException exception)
            {
                return ProcessRuntimeCommitResult.FromMutation(
                    ProcessRuntimeMutation.Rejected(
                        state,
                        "Runtime.RootCancellationConcurrencyExhausted",
                        $"Root process run '{rootRunId}' remained concurrent after {MaximumCancellationConcurrencyRetries} cancellation attempts: {exception.Message}"));
            }
        }
    }

    private static bool IsCancellable(ProcessRuntimeStatus status)
        => status != ProcessRuntimeStatus.CancelRequested &&
           !ProcessRuntimeTerminalStates.IsRunTerminal(status);

    private async ValueTask<IReadOnlyList<string>> NotifyCancellationObserversAsync(
        ProcessRuntimeRunCancellationCommand command,
        IReadOnlyList<ProcessRunId> cancelledRunIds,
        CancellationToken cancellationToken)
    {
        if (cancellationObservers.Count == 0)
        {
            return [];
        }

        var diagnostics = new List<string>();
        var observation = new ProcessRuntimeRunCancellationObservation(
            command.RunId,
            cancelledRunIds.Distinct().ToArray(),
            NormalizeRequestedBy(command.RequestedBy),
            NormalizeReason(command.Reason),
            NormalizeUtc(clock.GetUtcNow()));

        foreach (var observer in cancellationObservers)
        {
            try
            {
                var result = await observer.OnRunsCancelledAsync(observation, cancellationToken).ConfigureAwait(false);
                diagnostics.AddRange(result.Diagnostics);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                diagnostics.Add(
                    $"Cancellation observer '{observer.GetType().FullName}' failed after cancelling process run(s): {exception.Message}");
            }
        }

        return diagnostics;
    }

    private async Task<ProcessRuntimeOperatorActionResult> RequestReworkAsync(
        ProcessRuntimeOperatorActionCommand command,
        CancellationToken cancellationToken)
    {
        var state = await stateStore.LoadAsync(command.RunId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Process run '{command.RunId}' was not found.");
        var reason = NormalizeReason(command.Reason);
        var engine = new ProcessRuntimeEngine(unitOfWork);
        if (HasExpiredActiveClaim(state, command.StepInstanceId, NormalizeUtc(clock.GetUtcNow())))
        {
            var expireCommit = await engine.ExpireClaimsAsync(
                state,
                CreateContext(command.RequestedBy),
                new ExpireDispatchClaimsCommand(NormalizeUtc(clock.GetUtcNow())),
                cancellationToken).ConfigureAwait(false);
            if (!expireCommit.Succeeded)
            {
                return new ProcessRuntimeOperatorActionResult(
                    command.RunId,
                    command.StepInstanceId,
                    command.Kind,
                    expireCommit.Outcome,
                    expireCommit.State.Status,
                    expireCommit.Diagnostics.Select(diagnostic => diagnostic.Message).ToArray());
            }

            state = expireCommit.State;
        }

        var stateBeforeRework = state;
        var commit = await engine.RequestStepReworkAsync(
            state,
            CreateContext(command.RequestedBy),
            new RequestStepReworkCommand(command.StepInstanceId, reason),
            cancellationToken).ConfigureAwait(false);

        if (commit.Succeeded)
        {
            await ApplyReworkInstructionAsync(command, reason, stateBeforeRework, cancellationToken).ConfigureAwait(false);
            await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);
            await dispatchQueue.EnqueueAsync(
                new ProcessRuntimeDispatchQueueRequest(command.RunId, NormalizeRequestedBy(command.RequestedBy)),
                cancellationToken).ConfigureAwait(false);
        }

        return new ProcessRuntimeOperatorActionResult(
            command.RunId,
            command.StepInstanceId,
            command.Kind,
            commit.Outcome,
            commit.State.Status,
            commit.Diagnostics.Select(diagnostic => diagnostic.Message).ToArray());
    }

    private async Task ApplyReworkInstructionAsync(
        ProcessRuntimeOperatorActionCommand command,
        string reason,
        ProcessRuntimeStateSnapshot state,
        CancellationToken cancellationToken)
    {
        var assignment = await assignmentStore.LoadAsync(command.RunId, command.StepInstanceId, cancellationToken)
            .ConfigureAwait(false);
        if (assignment is null)
        {
            return;
        }

        var repair = await RepairAssignmentAsync(assignment, reason, cancellationToken).ConfigureAwait(false);
        var nextAssignment = repair.Assignment;
        var recoveryInstruction = recoveryInstructionBuilder.Build(new ProcessStepRecoveryInstructionBuildRequest(
            command.RunId,
            command.StepInstanceId,
            nextAssignment.StepKey,
            nextAssignment,
            StrategyResult: null,
            FindLatestReceipt(state, command.StepInstanceId),
            reason));
        var prompt = AppendReworkInstruction(nextAssignment.Prompt, BuildInstructionReason(reason, repair, recoveryInstruction));
        if (string.Equals(prompt, nextAssignment.Prompt, StringComparison.Ordinal) && !repair.Repaired)
        {
            return;
        }

        await assignmentStore
            .SaveAsync([nextAssignment with { Prompt = prompt }], cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<ProcessRuntimeStepAssignmentRepairResult> RepairAssignmentAsync(
        ProcessRuntimeStepAssignment assignment,
        string reason,
        CancellationToken cancellationToken)
    {
        var current = assignment;
        foreach (var repairService in assignmentRepairServices)
        {
            var repair = await repairService
                .RepairAsync(current, reason, cancellationToken)
                .ConfigureAwait(false);
            current = repair.Assignment;
            if (repair.Repaired)
            {
                return repair;
            }
        }

        return new ProcessRuntimeStepAssignmentRepairResult(current, false, string.Empty);
    }

    private static string AppendReworkInstruction(string prompt, string reason)
    {
        var normalizedPrompt = RemovePriorReworkInstructionBlocks(prompt).TrimEnd();
        var normalizedReason = reason.Trim();
        return $"""
        {normalizedPrompt}

        {ReworkInstructionHeading}:
        {normalizedReason}
        """;
    }

    private static string RemovePriorReworkInstructionBlocks(string prompt)
        => PriorReworkInstructionBlockRegex.Replace(prompt, string.Empty).TrimEnd();

    private static string BuildInstructionReason(
        string reason,
        ProcessRuntimeStepAssignmentRepairResult repair,
        ProcessStepRecoveryInstruction recoveryInstruction)
    {
        var builder = new List<string> { reason };
        if (recoveryInstruction.HasInstruction)
        {
            builder.Add($"""
            Diagnostic recovery packet:
            {recoveryInstruction.Text.Trim()}
            """);
        }

        if (repair.Repaired && !string.IsNullOrWhiteSpace(repair.Summary))
        {
            builder.Add($"""
            Assignment repair:
            {repair.Summary.Trim()}
            """);
        }

        return string.Join(Environment.NewLine + Environment.NewLine, builder);
    }

    private static StrategyResultReceipt? FindLatestReceipt(
        ProcessRuntimeStateSnapshot state,
        ProcessStepInstanceId stepInstanceId)
        => state.AppliedResults.LastOrDefault(receipt => receipt.StepInstanceId == stepInstanceId);

    private RuntimeCommandContext CreateContext(string requestedBy)
    {
        return new RuntimeCommandContext(
            RuntimeCommandId.New(),
            new ProcessEventActor(ProcessEventActorKind.System, new ProcessActorId(OperatorActorId)),
            new ProcessCorrelationId($"operator-{NormalizeRequestedBy(requestedBy)}-{Guid.NewGuid():N}"),
            NormalizeUtc(clock.GetUtcNow()));
    }

    private static string NormalizeRequestedBy(string requestedBy)
        => string.IsNullOrWhiteSpace(requestedBy)
            ? OperatorActorId
            : requestedBy.Trim();

    private static string NormalizeReason(string reason)
        => string.IsNullOrWhiteSpace(reason)
            ? "Operator requested step rework from LiveProcesses."
            : reason.Trim();

    private static bool HasExpiredActiveClaim(
        ProcessRuntimeStateSnapshot state,
        ProcessStepInstanceId stepInstanceId,
        DateTimeOffset nowUtc)
    {
        var step = state.Steps.FirstOrDefault(candidate => candidate.StepInstanceId == stepInstanceId);
        if (step?.ActiveClaimToken is not { } activeClaimToken ||
            step.Status is not (ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running))
        {
            return false;
        }

        var claim = state.Claims.FirstOrDefault(candidate => candidate.ClaimToken == activeClaimToken);
        return claim is not null &&
               claim.Status is (DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed) &&
               claim.ExpiresAtUtc <= nowUtc;
    }

    private static DateTimeOffset NormalizeUtc(DateTimeOffset value)
        => value.Offset == TimeSpan.Zero ? value : value.ToUniversalTime();
}
