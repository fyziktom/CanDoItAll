namespace CanDoItAll.Mcp.SshOps.Coordination;

public sealed partial class TargetCoordinator
{
    public async Task<SshOpsToolResult<OperationStatusData>> OperationStatusAsync(
        string correlationId,
        string targetName,
        string operationId,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        var snapshot = await remoteJobRunner.GetSnapshotAsync(target, operationId, cancellationToken);
        return Result(
            ToStatusData(snapshot),
            target: target.Name,
            operationId: operationId,
            status: snapshot.State.ToString().ToLowerInvariant(),
            summary: snapshot.Summary);
    }

    public async Task<SshOpsToolResult<OperationWaitData>> OperationWaitAsync(
        string correlationId,
        string targetName,
        string operationId,
        int timeoutSeconds,
        int pollIntervalSeconds,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        var waitOutcome = await remoteJobRunner.WaitAsync(
            target,
            operationId,
            TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)),
            TimeSpan.FromSeconds(Math.Max(1, pollIntervalSeconds)),
            cancellationToken);

        var data = new OperationWaitData(
            operationId,
            waitOutcome.Snapshot.State.ToString().ToLowerInvariant(),
            waitOutcome.Completed,
            waitOutcome.TimedOut,
            waitOutcome.ElapsedMs,
            waitOutcome.Snapshot.ExitCode,
            waitOutcome.Snapshot.Summary);

        return Result(
            data,
            target: target.Name,
            operationId: operationId,
            status: waitOutcome.Completed ? data.State : "timeout",
            summary: waitOutcome.Completed
                ? waitOutcome.Snapshot.Summary
                : "Timed out while waiting for the remote operation.");
    }

    public async Task<SshOpsToolResult<OperationLogsData>> OperationLogsAsync(
        string correlationId,
        string targetName,
        string operationId,
        string stream,
        long cursor,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        var data = await remoteJobRunner.ReadLogsAsync(target, operationId, stream, cursor, maxBytes, cancellationToken);
        return Result(
            data,
            target: target.Name,
            operationId: operationId,
            status: "success",
            summary: $"Read {stream} logs for operation '{operationId}'.");
    }

    public async Task<SshOpsToolResult<OperationCancelData>> OperationCancelAsync(
        string correlationId,
        string targetName,
        string operationId,
        int graceSeconds,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        var snapshot = await remoteJobRunner.CancelAsync(target, operationId, TimeSpan.FromSeconds(Math.Max(1, graceSeconds)), cancellationToken);
        return Result(
            new OperationCancelData(operationId, snapshot.State.ToString().ToLowerInvariant(), snapshot.Summary),
            target: target.Name,
            operationId: operationId,
            status: snapshot.State.ToString().ToLowerInvariant(),
            summary: snapshot.Summary);
    }
}
