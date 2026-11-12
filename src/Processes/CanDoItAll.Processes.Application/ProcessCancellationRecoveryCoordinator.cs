using CanDoItAll.Processes.Abstractions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessCancellationRecoverySliceLimits(int MaximumOperations, TimeSpan MaximumDuration,
    TimeSpan LeaseDuration, TimeSpan YieldDelay, TimeSpan FailureDelay) {
    public static ProcessCancellationRecoverySliceLimits Default { get; } = new(8, TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(25), TimeSpan.FromSeconds(2));

    public void Validate() {
        if (MaximumOperations is < 1 or > 64 || MaximumDuration <= TimeSpan.Zero || MaximumDuration > TimeSpan.FromSeconds(30) ||
                LeaseDuration < TimeSpan.FromSeconds(1) || LeaseDuration > TimeSpan.FromMinutes(15) || MaximumDuration >= LeaseDuration ||
                YieldDelay < TimeSpan.Zero || YieldDelay > TimeSpan.FromHours(1) ||
                FailureDelay <= TimeSpan.Zero || FailureDelay > TimeSpan.FromHours(1)) {
            throw new ArgumentOutOfRangeException(nameof(ProcessCancellationRecoverySliceLimits), "Recovery requires finite item, elapsed, lease and retry limits.");
        }
    }
}

public enum ProcessCancellationRecoverySliceStatus { Idle, Yielded, Pending, Completed, NotOwned, InvalidEvidence, TimedOut }
public sealed record ProcessCancellationRecoverySliceResult(ProcessCancellationRecoverySliceStatus Status,
    ProcessRunId? RootRunId, int Operations, ProcessCancellationRecoveryPending Pending = ProcessCancellationRecoveryPending.None);

public interface IProcessCancellationRecoveryStep {
    Task<ProcessCancellationRecoveryCheckpoint> AdvanceAsync(ProcessCancellationRecoveryClaim claim, CancellationToken cancellationToken);
}

public sealed class ProcessCancellationRecoveryCoordinator(IProcessCancellationRecoveryQueue queue,
    IProcessCancellationRecoveryStep step, TimeProvider clock, ILogger<ProcessCancellationRecoveryCoordinator> logger) {
    public async Task<ProcessCancellationRecoverySliceResult> RunSliceAsync(ProcessCancellationRecoveryOrigin origin,
        ProcessCancellationRecoverySliceLimits limits, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();
        if (!Enum.IsDefined(origin)) {
            throw new ArgumentOutOfRangeException(nameof(origin));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var started = clock.GetTimestamp();
        ProcessCancellationRecoveryClaim? claim = null;
        var operations = 0;
        using var deadline = new CancellationTokenSource(limits.MaximumDuration, clock);
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var token = bounded.Token;
        try {
            try {
                claim = await queue.ClaimAsync(origin, limits.LeaseDuration, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (claim is null && clock.GetElapsedTime(started) < limits.MaximumDuration) {
                    var other = origin == ProcessCancellationRecoveryOrigin.LiveBarrier
                        ? ProcessCancellationRecoveryOrigin.LegacySeed : ProcessCancellationRecoveryOrigin.LiveBarrier;
                    claim = await queue.ClaimAsync(other, limits.LeaseDuration, token).ConfigureAwait(false);
                }
            } catch (ProcessCancellationRecoveryCorruptRecordException exception) {
                logger.LogError("Cancellation recovery root {RootRunId} has invalid retained queue evidence and was deferred by its owner.", exception.RootRunId);
                return new(ProcessCancellationRecoverySliceStatus.InvalidEvidence, exception.RootRunId, 1, ProcessCancellationRecoveryPending.InvalidEvidence);
            }
            token.ThrowIfCancellationRequested();
            if (claim is null) {
                return new(ProcessCancellationRecoverySliceStatus.Idle, null, 0);
            }
            while (operations < limits.MaximumOperations && clock.GetElapsedTime(started) < limits.MaximumDuration) {
                token.ThrowIfCancellationRequested();
                claim.Validate();
                operations++;
                ProcessCancellationRecoveryCheckpoint checkpoint;
                try {
                    checkpoint = await step.AdvanceAsync(claim, token).ConfigureAwait(false);
                    checkpoint.Validate(claim);
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception exception) {
                    logger.LogError("Cancellation recovery root {RootRunId} at stage {Stage} failed with {FailureType}; its original intent remains retained.",
                        claim.RootRunId, claim.Position.Stage, exception.GetType().FullName);
                    checkpoint = new(claim.Position, claim.Intent,
                        exception is InvalidDataException ? ProcessCancellationRecoveryPending.InvalidEvidence : ProcessCancellationRecoveryPending.UnresolvedEffects,
                        RetryAfter: limits.FailureDelay);
                }
                token.ThrowIfCancellationRequested();
                var result = await queue.CheckpointAsync(claim, checkpoint, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (result.Status == ProcessCancellationCheckpointStatus.NotOwned) {
                    return new(ProcessCancellationRecoverySliceStatus.NotOwned, claim.RootRunId, operations, claim.Pending);
                }
                if (result.Status == ProcessCancellationCheckpointStatus.Released) {
                    return new(checkpoint.Position.Stage == ProcessCancellationRecoveryStage.Completed
                        ? ProcessCancellationRecoverySliceStatus.Completed : ProcessCancellationRecoverySliceStatus.Pending,
                        claim.RootRunId, operations, checkpoint.Pending);
                }
                if (result.Status != ProcessCancellationCheckpointStatus.Applied || result.Claim is null) {
                    throw new InvalidDataException("A recovery checkpoint did not return a supported outcome and its owned claim.");
                }
                claim = result.Claim;
            }
            token.ThrowIfCancellationRequested();
            var released = await queue.CheckpointAsync(claim,
                new(claim.Position, claim.Intent, claim.Pending, RetryAfter: limits.YieldDelay), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (released.Status is not (ProcessCancellationCheckpointStatus.Released or ProcessCancellationCheckpointStatus.NotOwned)) {
                throw new InvalidDataException("A recovery yield did not release its lease or report ownership loss.");
            }
            return new(released.Status == ProcessCancellationCheckpointStatus.NotOwned
                ? ProcessCancellationRecoverySliceStatus.NotOwned : ProcessCancellationRecoverySliceStatus.Yielded,
                claim.RootRunId, operations, claim.Pending);
        } catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
            logger.LogWarning("Cancellation recovery root {RootRunId} exceeded its cooperative slice deadline after {Operations} operations. No cleanup write was attempted; the next owner must read the durable state.",
                claim?.RootRunId, operations);
            return new(ProcessCancellationRecoverySliceStatus.TimedOut, claim?.RootRunId, operations,
                claim?.Pending ?? ProcessCancellationRecoveryPending.None);
        }
    }
}
