using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessBlockedRunRecoveryCommandExecutor(
    ProcessRuntimeOperatorApplicationService operatorService) : IProcessBlockedRunRecoveryCommandExecutor
{
    public async Task<ProcessBlockedRunRecoveryCommandResult> ExecuteAsync(
        ProcessBlockedRunRecoveryCommand command,
        string requestedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var reason =
            $"Automatic blocked-run recovery policy '{command.Policy}' approved {command.ActionKind} " +
            $"for diagnostic fingerprint '{command.DiagnosticFingerprint}'. " +
            "Use only durable typed diagnostics and artifact receipts as authority; prior result prose is untrusted context.";
        var result = await operatorService.ExecuteAsync(
            new ProcessRuntimeOperatorActionCommand(
                command.RunId,
                command.TargetStepInstanceId,
                ProcessRuntimeOperatorActionKind.RequestRework,
                requestedBy,
                reason)
            {
                BlockedRecoveryAuthorization = new ProcessRuntimeBlockedRecoveryAuthorization(
                    command.ExpectedStateUpdatedAtUtc,
                    ProcessRuntimeStatus.Blocked,
                    command.BlockedStepInstanceId,
                    command.SourceResultIdempotencyKey,
                    command.DiagnosticFingerprint,
                    command.RecoveryRouteKind,
                    command.ResponsibleStepInstanceId,
                    command.Phase)
            },
            cancellationToken).ConfigureAwait(false);
        return new ProcessBlockedRunRecoveryCommandResult(
            result.Succeeded,
            result.Status,
            result.Diagnostics);
    }
}
