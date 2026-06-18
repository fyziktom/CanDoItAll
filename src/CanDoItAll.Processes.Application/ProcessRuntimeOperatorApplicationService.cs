using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessRuntimeOperatorApplicationService(
    IProcessProjectionClock clock,
    IProcessRuntimeStateStore stateStore,
    IProcessRuntimeStepAssignmentStore assignmentStore,
    IProcessRuntimeUnitOfWork unitOfWork,
    IProcessRuntimeDispatchQueue dispatchQueue,
    ProcessRuntimeProjectionCatchupService projectionCatchupService,
    IEnumerable<IProcessRuntimeStepAssignmentRepairService> assignmentRepairServices)
{
    private const string OperatorActorId = "process-runtime-operator";
    private const string ReworkInstructionHeading = "Operator rework instruction";

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
