namespace CanDoItAll.Modules.Projects;

public sealed class ProjectDeletionCompletionDispatcher(IProjectDeletionCompletionQuery query,
    IEnumerable<IProjectDeletionParticipant> registrations) {
    public async Task<ProjectDeletionParticipantCompletion> CompleteAsync(ProjectDeletionPreparationContext original,
        ProjectDeletionObligation obligation, IProjectLifecycleCurrentWriteAccess currentWrite,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(obligation);
        ArgumentNullException.ThrowIfNull(currentWrite);
        var participant = registrations.SingleOrDefault(candidate => candidate.Id == obligation.Participant)
            ?? throw new InvalidOperationException("The original deletion participant is not registered.");
        var observation = await query.ObserveAsync(original, obligation, cancellationToken);
        if (observation is not ProjectDeletionCompletionObservation.Released) {
            throw new ProjectDeletionParticipantCleanupException(obligation.RecoveryId,
                "The exact original deletion has no confirmed owner release. Keep its original completion binding for reconciliation.");
        }
        if (await currentWrite.ObserveAsync(cancellationToken) != ProjectLifecycleWriteAccessDecision.Allowed) {
            throw new ProjectDeletionParticipantCleanupException(obligation.RecoveryId,
                "Current deletion cleanup authority could not be confirmed.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        var completed = await participant.CompleteAsync(new(original.Project.ProjectId, obligation.RecoveryId), cancellationToken);
        if (completed.RecoveryId != obligation.RecoveryId) {
            throw new InvalidDataException("The deletion participant completed another recovery identity.");
        }
        return completed;
    }
}
