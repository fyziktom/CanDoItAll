using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

public sealed partial class FileSandboxWorkspaceStore {
    public async Task<ExecutionRunSourceReservationResult> ReserveBackgroundExecutionRunAsync(
        ExecutionRunSourceKey source, ExecutionRunDetail candidate, AgentToolBackgroundReservation reservation,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(reservation);
        var journal = candidate.Run.ToolAdmission ?? throw new InvalidOperationException("The candidate has no durable background admission.");
        journal.Validate();
        if (!source.RequiresBackgroundAdmission || !source.Matches(candidate.Run) || candidate.Run.ChatSessionId is not null ||
                candidate.Run.Id != reservation.CandidateExecutionRunId || journal.Session.Profile != reservation.Profile ||
                journal.Session.Reference.BackgroundSource?.OwnerFingerprint != reservation.OwnerFingerprint ||
                journal.Session.Reference.ExecutionRunId != candidate.Run.Id || journal.Support != AgentToolAdmissionSupport.Recoverable ||
                reservation.AcknowledgedExecutionRunIds.Contains(Guid.Empty)) {
            throw new InvalidOperationException("The background reservation has no matching source, profile, input or current owner binding.");
        }
        var acknowledged = reservation.AcknowledgedExecutionRunIds.ToHashSet();
        await gate.WaitAsync(cancellationToken);
        try {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureSplitFilesCoreAsync(cancellationToken);
            var prior = (await executionSliceStore.ListRunsAsync(cancellationToken))
                .Where(source.MatchesBackgroundLineage).OrderBy(run => run.Id).ToArray();
            var sameClaim = prior.Where(run => run.ToolAdmission is { } saved &&
                saved.Session.Reference.BackgroundSource?.OwnerFingerprint == reservation.OwnerFingerprint &&
                saved.Session.Profile == reservation.Profile).ToArray();
            if (sameClaim.Length > 1) {
                throw new AgentToolAdmissionException("tool-admission.duplicate-source-admission",
                    "Multiple executions already share this owner claim. Reconcile their exact journals before another dispatch.");
            }
            foreach (var previous in prior.Where(run => sameClaim.Length == 0 || run.Id != sameClaim[0].Id)) {
                previous.ToolAdmission?.Validate();
                if (previous.ToolAdmission is null && acknowledged.Contains(previous.Id) && previous.ChatSessionId is null &&
                        previous.State == ExecutionState.Completed && previous.Outcome == RunOutcome.Succeeded && previous.PendingApprovals.Count == 0) {
                    continue;
                }
                if (!acknowledged.Contains(previous.Id) || previous.State is not (ExecutionState.Completed or ExecutionState.Failed) ||
                        previous.ToolAdmission is not { Support: AgentToolAdmissionSupport.Recoverable } saved ||
                        saved.Session.Profile != reservation.Profile || saved.Session.Reference.BackgroundSource is null ||
                        saved.HasUnresolvedEffects) {
                    return new(ExecutionRunSourceDisposition.SourceReconciliationRequired, previous);
                }
            }
            if (sameClaim is [var existing]) {
                existing.ToolAdmission!.Validate();
                var disposition = existing.State switch {
                    ExecutionState.Completed when existing.Outcome == RunOutcome.Succeeded => ExecutionRunSourceDisposition.ReusedCompleted,
                    ExecutionState.Completed or ExecutionState.Failed => ExecutionRunSourceDisposition.ExistingAdmittedFailure,
                    _ => ExecutionRunSourceDisposition.ExistingActive
                };
                return new(disposition, existing);
            }
            var catalog = await LoadCatalogCoreAsync(cancellationToken);
            ValidateExecutionRunDetail(catalog, candidate);
            var persisted = await SaveExecutionRunDetailCoreAsync(null, candidate, catalog, cancellationToken);
            return new(ExecutionRunSourceDisposition.Created, persisted.Run);
        } finally {
            gate.Release();
        }
    }
}
