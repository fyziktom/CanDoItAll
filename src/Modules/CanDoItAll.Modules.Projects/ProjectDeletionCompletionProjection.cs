using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Projects;

public enum ProjectDeletionParticipantObservationFailure { MissingRecord, OriginalBindingMismatch, InvalidStoredState }

public abstract record ProjectDeletionParticipantObservation {
    private ProjectDeletionParticipantObservation() {
    }

    public sealed record Completed(ProjectDeletionParticipantCompletion Completion) : ProjectDeletionParticipantObservation;
    public sealed record Pending(ProjectDeletionRecoveryStatus Status) : ProjectDeletionParticipantObservation;
    public sealed record Unavailable(ProjectDeletionParticipantObservationFailure Reason) : ProjectDeletionParticipantObservation;
}

public abstract record ProjectDeletionCompletionProjectionResult {
    private ProjectDeletionCompletionProjectionResult() {
    }

    public sealed record Completed(ProjectDeletionResult Result) : ProjectDeletionCompletionProjectionResult;
    public sealed record Pending(ProjectDeletionRecovery Recovery, IReadOnlyList<ProjectDeletionWarning> Warnings,
        bool DiagnosticsUnavailable = false) : ProjectDeletionCompletionProjectionResult;
}

public sealed class ProjectDeletionCompletionProjection(IProjectDeletionCompletionQuery releases,
    IEnumerable<IProjectDeletionParticipant> registrations, ILogger<ProjectDeletionCompletionProjection> logger) {
    public async Task<ProjectDeletionCompletionProjectionResult> ObserveAsync(ProjectDeletionCompletionBinding original,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var warnings = new List<ProjectDeletionWarning>();
        var pending = new List<ProjectDeletionRecoveryFailure>();
        var ownerReleased = false;
        try {
            if (await releases.ObserveAsync(original, deadline.Token) is not ProjectDeletionCompletionObservation.Released) {
                return PendingAll();
            }
            ownerReleased = true;
            foreach (var obligation in original.Obligations) {
                var participant = registrations.SingleOrDefault(candidate => candidate.Id == obligation.Participant)
                    ?? throw new InvalidOperationException("The original deletion participant is not registered.");
                var observation = await participant.ObserveCompletionAsync(original.Original, obligation.RecoveryId, deadline.Token);
                if (observation is ProjectDeletionParticipantObservation.Completed completed) {
                    if (completed.Completion.RecoveryId != obligation.RecoveryId) {
                        throw new InvalidDataException("The deletion observation returned another recovery identity.");
                    }
                    warnings.AddRange(completed.Completion.Warnings.Select(warning => new ProjectDeletionWarning(warning.Kind,
                        obligation.Participant, obligation.RecoveryId, warning.RetainedObject, warning.Message, warning.Remediation)));
                } else {
                    pending.Add(Failure(obligation));
                }
            }
            if (await releases.ObserveAsync(original, deadline.Token) is not ProjectDeletionCompletionObservation.Released) {
                ownerReleased = false;
                return PendingAll();
            }
            return pending.Count == 0
                ? new ProjectDeletionCompletionProjectionResult.Completed(new(original.Original.Project.ProjectId, warnings.ToArray()))
                : new ProjectDeletionCompletionProjectionResult.Pending(new(original.Original.Project.ProjectId, pending,
                    PendingGuidance), warnings.ToArray());
        } catch (Exception failure) {
            ownerReleased = false;
            var diagnosticsUnavailable = false;
            try {
                logger.LogWarning("Deletion completion projection {CompletionId} in dataset {NamespaceId} failed with {FailureType}.",
                    original.Original.Completion.Id.Value, original.Original.Completion.Namespace.Value, failure.GetType().FullName);
            } catch (Exception) {
                diagnosticsUnavailable = true;
            }
            return PendingAll(diagnosticsUnavailable);
        }

        ProjectDeletionCompletionProjectionResult PendingAll(bool diagnosticsUnavailable = false)
            => new ProjectDeletionCompletionProjectionResult.Pending(new(original.Original.Project.ProjectId,
                original.Obligations.Select(Failure).ToArray(), ownerReleased ? PendingGuidance : OwnerGuidance,
                OwnerCompletionUnconfirmed: !ownerReleased), warnings.ToArray(), diagnosticsUnavailable);
    }

    private static ProjectDeletionRecoveryFailure Failure(ProjectDeletionObligation obligation)
        => new(ProjectDeletionRecoveryOperation.ParticipantCleanup, obligation.Participant, obligation.RecoveryId);

    private const string PendingGuidance = "The deletion committed. Retain the exact completion and recovery identities; current cleanup observation is incomplete. Any retry must independently establish release and current authority.";
    private const string OwnerGuidance = "The deletion committed, but original owner release is unconfirmed. Retain the exact completion binding for operator recovery; a pending-list read does not establish release.";
}
