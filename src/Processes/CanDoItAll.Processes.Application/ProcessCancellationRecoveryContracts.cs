using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public enum ProcessCancellationRecoveryOrigin { LiveBarrier, LegacySeed }
public enum ProcessCancellationRecoveryStage { Runtime, Decisions, SharedScope, Quiescence, Completed }
public enum ProcessCancellationRecoveryPending {
    None, ActiveWork, LeaseBusy, MissingJournal, UnresolvedEffects, CompletionPending,
    CapturePending, RuntimeWork, DecisionWork, BudgetWork, ScopeWork, ConflictingState, InvalidEvidence
}
public enum ProcessCancellationIntentOutcome { Committed, RefusedWithoutCommit }
public enum ProcessCancellationCheckpointStatus { Applied, Released, NotOwned }
public enum ProcessCancellationSeedStatus { Advanced, Completed, Busy }

public sealed record ProcessCancellationRecoveryPosition(ProcessCancellationRecoveryStage Stage,
    ProcessRunId? RunAfter = null, ProcessDecisionId? DecisionAfter = null,
    ProcessDecisionReference? CurrentDecision = null, ProcessDecisionChildId? ChildAfter = null,
    ProcessRunId? CurrentRun = null, ProcessExecutionRunId? ExecutionAfter = null, bool CycleHadPending = false) {
    public static ProcessCancellationRecoveryPosition Initial { get; } = new(ProcessCancellationRecoveryStage.Runtime);

    public void Validate() {
        CurrentDecision?.Validate();
        if (!Enum.IsDefined(Stage) || RunAfter?.Value == Guid.Empty || DecisionAfter?.Value == Guid.Empty || ChildAfter?.Value == Guid.Empty ||
                CurrentRun?.Value == Guid.Empty || ExecutionAfter?.Value == Guid.Empty ||
                Stage != ProcessCancellationRecoveryStage.Runtime && (RunAfter is not null || CurrentRun is not null || ExecutionAfter is not null) ||
                ExecutionAfter is not null && CurrentRun is null || CurrentRun is not null && CurrentRun == RunAfter ||
                Stage == ProcessCancellationRecoveryStage.Completed && CycleHadPending ||
                Stage != ProcessCancellationRecoveryStage.Decisions && (DecisionAfter is not null || CurrentDecision is not null || ChildAfter is not null) ||
                ChildAfter is not null && CurrentDecision is null || CurrentDecision?.DecisionId == DecisionAfter && DecisionAfter is not null) {
            throw new InvalidDataException("The cancellation recovery position has an invalid stage or keyset cursor.");
        }
    }
}

public sealed record ProcessCancellationRecoveryClaim(ProcessRunId RootRunId, ProcessCancellationRecoveryOrigin Origin,
    RuntimeCommandId? BarrierCommandId, Guid LeaseToken, long LeaseGeneration, long ProgressRevision,
    DateTimeOffset LeaseExpiresAtUtc, int Cycle, ProcessCancellationRecoveryPosition Position,
    ProcessCancellationRecoveryIntent? Intent, ProcessCancellationRecoveryPending Pending) {
    public void Validate() {
        if (RootRunId.Value == Guid.Empty || !Enum.IsDefined(Origin) || BarrierCommandId?.Value == Guid.Empty ||
                (Origin == ProcessCancellationRecoveryOrigin.LiveBarrier) != (BarrierCommandId is not null) ||
                LeaseToken == Guid.Empty || LeaseGeneration < 1 || ProgressRevision < 0 || Cycle < 0 ||
                LeaseExpiresAtUtc == default || LeaseExpiresAtUtc.Offset != TimeSpan.Zero || !Enum.IsDefined(Pending) ||
                Position.Stage == ProcessCancellationRecoveryStage.Completed) {
            throw new InvalidDataException("The cancellation recovery claim has an invalid original identity or lease.");
        }
        Position.Validate();
        Intent?.Validate(RootRunId, Position.Stage);
    }
}

public sealed record ProcessCancellationIntentResolution(RuntimeCommandId ActionId, ProcessCancellationIntentOutcome Outcome);

public sealed record ProcessCancellationRecoveryCheckpoint(ProcessCancellationRecoveryPosition Position,
    ProcessCancellationRecoveryIntent? Intent = null, ProcessCancellationRecoveryPending Pending = ProcessCancellationRecoveryPending.None,
    ProcessCancellationIntentResolution? Resolution = null, TimeSpan? RetryAfter = null) {
    public void Validate(ProcessCancellationRecoveryClaim saved) {
        saved.Validate();
        Position.Validate();
        Intent?.Validate(saved.RootRunId, Position.Stage);
        if (!Enum.IsDefined(Pending) || RetryAfter is { } delay && (delay < TimeSpan.Zero || delay > TimeSpan.FromHours(1))) {
            throw new InvalidDataException("Recovery requires a known pending reason and bounded retry delay.");
        }
        var restarting = saved.Position.Stage == ProcessCancellationRecoveryStage.Quiescence &&
            Position == ProcessCancellationRecoveryPosition.Initial && Pending != ProcessCancellationRecoveryPending.None && Intent is null;
        var sameSelection = Position with { CycleHadPending = saved.Position.CycleHadPending } == saved.Position;
        if (saved.Position.CycleHadPending && !Position.CycleHadPending && !restarting ||
                Pending != ProcessCancellationRecoveryPending.None && !sameSelection && !restarting && !Position.CycleHadPending) {
            throw new InvalidDataException("Advancing past pending work must retain the cycle evidence until an explicit cycle restart.");
        }
        if (saved.Intent is { } original) {
            if (Intent is not null) {
                if (Intent != original || !sameSelection || Resolution is not null) {
                    throw new InvalidDataException("An unresolved recovery intent and its position cannot be replaced.");
                }
            } else if (Resolution is null || Resolution.ActionId != original.Context.CommandId || !Enum.IsDefined(Resolution.Outcome)) {
                throw new InvalidDataException("Clearing a recovery intent requires its exact action and a definite owner outcome.");
            }
        } else if (Resolution is not null || Intent is not null && !sameSelection) {
            throw new InvalidDataException("A new recovery intent must retain its original selection position.");
        }
        if (Position.Stage == ProcessCancellationRecoveryStage.Completed &&
                (saved.Position.Stage != ProcessCancellationRecoveryStage.Quiescence || saved.Intent is not ProcessCancellationFinalizeRoot ||
                Resolution?.Outcome != ProcessCancellationIntentOutcome.Committed || Intent is not null || Pending != ProcessCancellationRecoveryPending.None)) {
            throw new InvalidDataException("Recovery completion requires the verified final-root intent without pending work.");
        }
        if (Position.Stage != saved.Position.Stage && Position.Stage != saved.Position.Stage + 1 &&
                !restarting) {
            throw new InvalidDataException("Recovery stages must advance in order or restart a pending completed cycle.");
        }
    }
}

public sealed record ProcessCancellationCheckpointResult(ProcessCancellationCheckpointStatus Status, ProcessCancellationRecoveryClaim? Claim = null);
public sealed record ProcessCancellationSeedResult(ProcessCancellationSeedStatus Status, int Selected, int Inserted);

public sealed class ProcessCancellationRecoveryCorruptRecordException(ProcessRunId rootRunId, Exception innerException)
    : Exception($"Cancellation recovery for root '{rootRunId.Value:D}' contains invalid retained evidence and requires owner repair.", innerException) {
    public ProcessRunId RootRunId { get; } = rootRunId;
}

public interface IProcessCancellationRecoveryQueue {
    Task<ProcessCancellationRecoveryClaim?> ClaimAsync(ProcessCancellationRecoveryOrigin origin, TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);
    Task<ProcessCancellationCheckpointResult> CheckpointAsync(ProcessCancellationRecoveryClaim claim,
        ProcessCancellationRecoveryCheckpoint checkpoint, CancellationToken cancellationToken = default);
    Task<ProcessCancellationSeedResult> SeedLegacyAsync(int take = 16, CancellationToken cancellationToken = default);
}
