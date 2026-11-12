using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using System.Text.RegularExpressions;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessRuntimeOperatorApplicationService(
    IProcessProjectionClock clock,
    IProcessRuntimeStateStore stateStore,
    IProcessRuntimeStepAssignmentStore assignmentStore,
    IProcessRuntimeUnitOfWork unitOfWork,
    IProcessRuntimeDispatchQueue dispatchQueue,
    ProcessRuntimeProjectionCatchupService projectionCatchupService,
    IEnumerable<IProcessRuntimeStepAssignmentRepairService> assignmentRepairServices,
    IEnumerable<IProcessRuntimeRunCancellationObserver>? cancellationObservers = null,
    IProcessStepRecoveryInstructionBuilder? recoveryInstructionBuilder = null)
{
    private const string OperatorActorId = "process-runtime-operator";
    private const string ReworkInstructionHeading = ProcessRuntimeRecoveryInstructionHeadings.OperatorRework;
    private const string ManagerRecoveryInstructionHeading = ProcessRuntimeRecoveryInstructionHeadings.ManagerRecovery;
    private const string RuntimeDiagnosticRecoveryInstructionHeading = ProcessRuntimeRecoveryInstructionHeadings.RuntimeDiagnosticRecovery;
    private const string BlockedRecoveryAuthorizationRejected = "Runtime.BlockedRecoveryAuthorizationRejected";
    private const string BlockedRecoveryConcurrencyRejected = "Runtime.BlockedRecoveryConcurrencyRejected";
    private static readonly Regex PriorReworkInstructionBlockRegex = new(
        $@"(?ms)^\s*(?:{Regex.Escape(ManagerRecoveryInstructionHeading)}|{Regex.Escape(ReworkInstructionHeading)}|{Regex.Escape(RuntimeDiagnosticRecoveryInstructionHeading)}):\s*.*?(?=^\s*(?:{Regex.Escape(ManagerRecoveryInstructionHeading)}|{Regex.Escape(ReworkInstructionHeading)}|{Regex.Escape(RuntimeDiagnosticRecoveryInstructionHeading)}):\s*|\z)",
        RegexOptions.CultureInvariant);
    private readonly IReadOnlyList<IProcessRuntimeRunCancellationObserver> cancellationObservers =
        (cancellationObservers ?? []).ToArray();
    private readonly IProcessStepRecoveryInstructionBuilder recoveryInstructionBuilder =
        recoveryInstructionBuilder ?? ProcessStepRecoveryInstructionBuilder.Instance;

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
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(command);
        var state = await stateStore.LoadAsync(command.RunId, cancellationToken).ConfigureAwait(false)
            ?? throw new ProcessRuntimeOperatorRunNotFoundException(command.RunId);
        if (state.RunId == state.RootRunId) {
            throw new ProcessRootCancellationAdmissionRequiredException(state.RunId);
        }
        var commit = await new ProcessRuntimeEngine(unitOfWork).RequestCancellationAsync(
            state, CreateContext(command.RequestedBy), cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> observerDiagnostics = [];
        if (commit.Succeeded) {
            await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);
            observerDiagnostics = await NotifyCancellationObserversAsync(command, [command.RunId], cancellationToken).ConfigureAwait(false);
        }
        return new(command.RunId, ProcessRuntimeOperatorActionKind.CancelRun, commit.Outcome, commit.State.Status,
            [.. observerDiagnostics, .. commit.Diagnostics.Select(diagnostic => diagnostic.Message)]);
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
            catch (Exception)
            {
                diagnostics.Add(
                    "A cancellation observer failed after cancelling process run(s). Review restricted host logs for details.");
            }
        }

        return diagnostics;
    }

    private async Task<ProcessRuntimeOperatorActionResult> RequestReworkAsync(
        ProcessRuntimeOperatorActionCommand command,
        CancellationToken cancellationToken)
    {
        var state = await stateStore.LoadAsync(command.RunId, cancellationToken).ConfigureAwait(false)
            ?? throw new ProcessRuntimeOperatorRunNotFoundException(command.RunId);
        var authorizationIssue = ValidateBlockedRecoveryAuthorization(command, state);
        if (authorizationIssue is not null)
        {
            return RejectBlockedRecovery(command, state, BlockedRecoveryAuthorizationRejected, authorizationIssue);
        }

        var reason = NormalizeReason(command.Reason);
        var engine = new ProcessRuntimeEngine(unitOfWork);
        if (command.BlockedRecoveryAuthorization is null &&
            HasExpiredActiveClaim(state, command.StepInstanceId, NormalizeUtc(clock.GetUtcNow())))
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
        ProcessRuntimeCommitResult commit;
        try
        {
            commit = await engine.RequestStepReworkAsync(
                state,
                CreateContext(command.RequestedBy),
                new RequestStepReworkCommand(command.StepInstanceId, reason)
                {
                    BlockedRecoveryAuthorization = command.BlockedRecoveryAuthorization
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (ProcessRuntimeOptimisticConcurrencyException exception)
            when (command.BlockedRecoveryAuthorization is not null)
        {
            return RejectBlockedRecovery(
                command,
                state,
                BlockedRecoveryConcurrencyRejected,
                $"The blocked run changed before the authorized rework could commit: {exception.Message}");
        }

        if (commit.Succeeded)
        {
            await ApplyReworkInstructionAsync(command, reason, stateBeforeRework, cancellationToken).ConfigureAwait(false);
            await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);
            dispatchQueue.EnqueueOrDefer(
                new ProcessRuntimeDispatchQueueRequest(
                    command.RunId,
                    NormalizeRequestedBy(command.RequestedBy)));
        }

        return new ProcessRuntimeOperatorActionResult(
            command.RunId,
            command.StepInstanceId,
            command.Kind,
            commit.Outcome,
            commit.State.Status,
            commit.Diagnostics.Select(diagnostic => diagnostic.Message).ToArray());
    }

    private static string? ValidateBlockedRecoveryAuthorization(
        ProcessRuntimeOperatorActionCommand command,
        ProcessRuntimeStateSnapshot state)
    {
        var authorization = command.BlockedRecoveryAuthorization;
        return authorization is null
            ? null
            : ProcessRuntimeBlockedRecoveryAuthorizationRules.FindIssue(
                state,
                command.StepInstanceId,
                authorization);
    }

    private static ProcessRuntimeOperatorActionResult RejectBlockedRecovery(
        ProcessRuntimeOperatorActionCommand command,
        ProcessRuntimeStateSnapshot state,
        string code,
        string issue)
    {
        return new ProcessRuntimeOperatorActionResult(
            command.RunId,
            command.StepInstanceId,
            command.Kind,
            ProcessRuntimeTransitionOutcome.Rejected,
            state.Status,
            [$"{code}: {issue}"]);
    }

    private async Task ApplyReworkInstructionAsync(
        ProcessRuntimeOperatorActionCommand command,
        string reason,
        ProcessRuntimeStateSnapshot state,
        CancellationToken cancellationToken)
    {
        var assignment = await assignmentStore.LoadAsync(command.RunId, command.StepInstanceId, cancellationToken)
            .ConfigureAwait(false);
        if (assignment is null || ProcessLaunchExecutorKinds.IsWorkflow(assignment.ExecutorKind)) {
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
        => ProcessRuntimeBlockedRecoveryAuthorizationRules.FindLatestReceipt(state, stepInstanceId);

    private RuntimeCommandContext CreateContext(string requestedBy)
    {
        return new RuntimeCommandContext(
            RuntimeCommandId.New(),
            new ProcessEventActor(ProcessEventActorKind.System, new ProcessActorId(OperatorActorId)),
            new ProcessCorrelationId($"operator-{Guid.NewGuid():N}"),
            NormalizeUtc(clock.GetUtcNow()));
    }

    private static string NormalizeRequestedBy(string requestedBy)
        => ProcessActorIdentityPolicy.Normalize(requestedBy, OperatorActorId);

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
