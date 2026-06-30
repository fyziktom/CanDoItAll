using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

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
    IEnumerable<IProcessRuntimeRunCancellationObserver>? cancellationObservers = null)
{
    private const string OperatorActorId = "process-runtime-operator";
    private const string ReworkInstructionHeading = "Operator rework instruction";
    private readonly IReadOnlyList<IProcessRuntimeRunCancellationObserver> cancellationObservers =
        (cancellationObservers ?? []).ToArray();

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
        if (state.RunId == state.RootRunId)
        {
            var descendantRunIds = await runHierarchyStore
                .FindCancellableDescendantRunIdsAsync(state.RunId, cancellationToken)
                .ConfigureAwait(false);
            foreach (var descendantRunId in descendantRunIds)
            {
                var descendantState = await stateStore.LoadAsync(descendantRunId, cancellationToken).ConfigureAwait(false);
                if (descendantState is null)
                {
                    cascadeDiagnostics.Add($"Descendant process run '{descendantRunId.Value:D}' disappeared before cancellation.");
                    continue;
                }

                try
                {
                    var descendantCommit = await engine.RequestCancellationAsync(
                        descendantState,
                        CreateContext(command.RequestedBy),
                        cancellationToken).ConfigureAwait(false);
                    if (descendantCommit.Succeeded)
                    {
                        cancelledDescendantRunIds.Add(descendantRunId);
                        cancelledRunIds.Add(descendantRunId);
                        continue;
                    }

                    cascadeDiagnostics.AddRange(descendantCommit.Diagnostics.Select(diagnostic =>
                        $"Descendant process run '{descendantRunId.Value:D}' was not cancelled: {diagnostic.Message}"));
                }
                catch (ProcessRuntimeOptimisticConcurrencyException exception)
                {
                    cascadeDiagnostics.Add(
                        $"Descendant process run '{descendantRunId.Value:D}' changed while cancellation was being applied: {exception.Message}");
                }
            }

            state = await stateStore.LoadAsync(command.RunId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Process run '{command.RunId}' was not found.");
        }

        var commit = await engine.RequestCancellationAsync(
            state,
            CreateContext(command.RequestedBy),
            cancellationToken).ConfigureAwait(false);

        if (commit.Succeeded)
        {
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
            cascadeDiagnostics.AddRange(await NotifyCancellationObserversAsync(
                    command,
                    cancelledRunIds,
                    cancellationToken)
                .ConfigureAwait(false));
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

        var commit = await engine.RequestStepReworkAsync(
            state,
            CreateContext(command.RequestedBy),
            new RequestStepReworkCommand(command.StepInstanceId, reason),
            cancellationToken).ConfigureAwait(false);

        if (commit.Succeeded)
        {
            await ApplyReworkInstructionAsync(command, reason, cancellationToken).ConfigureAwait(false);
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
        var prompt = AppendReworkInstruction(nextAssignment.Prompt, BuildInstructionReason(reason, repair));
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
        var normalizedPrompt = prompt.TrimEnd();
        var normalizedReason = reason.Trim();
        return $"""
        {normalizedPrompt}

        {ReworkInstructionHeading}:
        {normalizedReason}
        """;
    }

    private static string BuildInstructionReason(
        string reason,
        ProcessRuntimeStepAssignmentRepairResult repair)
    {
        if (!repair.Repaired || string.IsNullOrWhiteSpace(repair.Summary))
        {
            return reason;
        }

        return $"""
        {reason}

        Assignment repair:
        {repair.Summary.Trim()}
        """;
    }

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
