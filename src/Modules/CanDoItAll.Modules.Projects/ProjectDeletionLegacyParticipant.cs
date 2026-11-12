namespace CanDoItAll.Modules.Projects;

public sealed record ProjectDeletionLegacyParticipantEvidence(ProjectDeletionRecoveryParticipantEvidence State,
    ProjectDeletionPreparationContext? Binding);

public interface IProjectDeletionLegacyParticipant : IProjectDeletionRecoveryParticipant {
    Task<IReadOnlyList<ProjectDeletionLegacyParticipantEvidence>> InspectLegacyAsync(ProjectWriteAdmission project,
        ProjectDeletionRecoveryReadMode mode, CancellationToken cancellationToken = default);
    Task BindLegacyAsync(ProjectDeletionPreparationContext original, ProjectDeletionLegacyParticipantEvidence expected,
        CancellationToken cancellationToken = default);
}
