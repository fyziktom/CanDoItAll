using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectDeletionCompletionObservationTests {
    [Fact]
    public void Only_the_exact_released_state_and_timestamp_permit_the_original_obligation() {
        var (original, obligation, row) = Original();
        Assert.IsType<ProjectDeletionCompletionObservation.AwaitingOwnerRelease>(row.Interpret(original, obligation));
        var released = new DateTimeOffset(2100, 1, 2, 3, 4, 5, TimeSpan.Zero);
        row.ReleasedAtUtc = released;
        AssertUnavailable(row, original, obligation, ProjectDeletionCompletionUnavailableReason.InvalidStoredBinding);
        row.ReleaseState = (int)ProjectDeletionCompletionReleaseState.Released;
        Assert.Equal(released, Assert.IsType<ProjectDeletionCompletionObservation.Released>(row.Interpret(original, obligation)).ReleasedAtUtc);
        row.ReleasedAtUtc = DateTimeOffset.MaxValue;
        AssertUnavailable(row, original, obligation, ProjectDeletionCompletionUnavailableReason.InvalidStoredBinding);
        row.ReleasedAtUtc = null;
        AssertUnavailable(row, original, obligation, ProjectDeletionCompletionUnavailableReason.InvalidStoredBinding);
        row.ReleaseState = int.MaxValue;
        AssertUnavailable(row, original, obligation, ProjectDeletionCompletionUnavailableReason.InvalidStoredBinding);
    }

    [Fact]
    public void A_released_completion_cannot_authorize_another_participant_recovery_or_project_lifetime() {
        var (original, obligation, row) = Original();
        row.ReleaseState = (int)ProjectDeletionCompletionReleaseState.Released;
        row.ReleasedAtUtc = DateTimeOffset.UtcNow;
        AssertUnavailable(row, original, new(obligation.Participant, Guid.NewGuid()), ProjectDeletionCompletionUnavailableReason.ObligationMismatch);
        AssertUnavailable(row, original, new(new("different-participant"), obligation.RecoveryId), ProjectDeletionCompletionUnavailableReason.ObligationMismatch);
        var replacement = new ProjectDeletionPreparationContext(original.Completion,
            new(original.Project.DatabaseProfileId, original.Project.ProjectId, Guid.NewGuid()));
        AssertUnavailable(row, replacement, obligation, ProjectDeletionCompletionUnavailableReason.InvalidStoredBinding);
        row.LifetimeId = replacement.Project.LifetimeId;
        AssertUnavailable(row, replacement, obligation, ProjectDeletionCompletionUnavailableReason.InvalidStoredBinding);
        row.ReferencePayload = ProjectDeletionCompletionCodec.Write(new(replacement, [obligation]));
        AssertUnavailable(row, original, obligation, ProjectDeletionCompletionUnavailableReason.InvalidStoredBinding);
    }

    [Fact]
    public void Missing_legacy_corrupt_or_unavailable_evidence_never_defaults_to_released() {
        var (original, obligation, row) = Original();
        row.ReleaseState = (int)ProjectDeletionCompletionReleaseState.Released;
        row.ReleasedAtUtc = DateTimeOffset.UtcNow;
        row.CompletionId = null;
        AssertUnavailable(row, original, obligation, ProjectDeletionCompletionUnavailableReason.MissingCompletion);
        row.CompletionId = original.Completion.Id.Value;
        row.SchemaVersion = int.MaxValue;
        AssertUnavailable(row, original, obligation, ProjectDeletionCompletionUnavailableReason.UnsupportedVersion);
        row.SchemaVersion = ProjectDeletionCompletionProtocol.SchemaVersion;
        row.NativeRetirementMatches = false;
        AssertUnavailable(row, original, obligation, ProjectDeletionCompletionUnavailableReason.MissingNativeRetirement);
        row.NativeRetirementMatches = true;
        foreach (var payload in new byte[]?[] { null, [], [0xff], new byte[ProjectDeletionCompletionCodec.MaximumEncodedBytes + 1] }) {
            row.ReferencePayload = payload;
            AssertUnavailable(row, original, obligation, ProjectDeletionCompletionUnavailableReason.InvalidStoredBinding);
        }
    }

    [Fact]
    public void A_well_formed_reference_payload_does_not_override_foreign_or_unknown_indexed_metadata() {
        var (original, obligation, row) = Original();
        row.ReleaseState = (int)ProjectDeletionCompletionReleaseState.Released;
        row.ReleasedAtUtc = DateTimeOffset.UtcNow;
        row.DatabaseProfileId = Guid.NewGuid();
        AssertUnavailable(row, original, obligation, ProjectDeletionCompletionUnavailableReason.InvalidStoredBinding);
        row.DatabaseProfileId = original.Project.DatabaseProfileId;
        row.ProjectId = Guid.NewGuid();
        AssertUnavailable(row, original, obligation, ProjectDeletionCompletionUnavailableReason.InvalidStoredBinding);
        row.ProjectId = original.Project.ProjectId;
        row.OriginKind = int.MaxValue;
        AssertUnavailable(row, original, obligation, ProjectDeletionCompletionUnavailableReason.InvalidStoredBinding);
        row.OriginKind = (int)ProjectDeletionCompletionOrigin.LegacyInstallation;
        row.CreatedAtUtc = null;
        AssertUnavailable(row, original, obligation, ProjectDeletionCompletionUnavailableReason.InvalidStoredBinding);
    }

    private static void AssertUnavailable(ProjectDeletionCompletionRow row, ProjectDeletionPreparationContext original,
        ProjectDeletionObligation obligation, ProjectDeletionCompletionUnavailableReason reason)
        => Assert.Equal(reason, Assert.IsType<ProjectDeletionCompletionObservation.Unavailable>(row.Interpret(original, obligation)).Reason);

    private static (ProjectDeletionPreparationContext Original, ProjectDeletionObligation Obligation, ProjectDeletionCompletionRow Row) Original() {
        var profile = Guid.NewGuid();
        var original = new ProjectDeletionPreparationContext(new(profile, new(Guid.NewGuid()), new(Guid.NewGuid())),
            new(profile, Guid.NewGuid(), Guid.NewGuid()));
        var obligation = new ProjectDeletionObligation(new("owner"), Guid.NewGuid());
        return (original, obligation, new() {
            CompletionId = original.Completion.Id.Value,
            DatabaseProfileId = profile,
            ProjectId = original.Project.ProjectId,
            LifetimeId = original.Project.LifetimeId,
            SchemaVersion = ProjectDeletionCompletionProtocol.SchemaVersion,
            OriginKind = (int)ProjectDeletionCompletionOrigin.OrdinaryDeletion,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ReleaseState = (int)ProjectDeletionCompletionReleaseState.AwaitingOwnerRelease,
            NativeRetirementMatches = true,
            ReferencePayload = ProjectDeletionCompletionCodec.Write(new(original, [obligation]))
        });
    }
}
