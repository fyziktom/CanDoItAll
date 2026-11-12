using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectLifecycleReceiptRowTests {
    [Fact]
    public void Deletion_receipt_is_found_without_a_live_target_or_a_release_claim() {
        var (row, accepted) = StoredDeletion();

        var found = Assert.IsType<ProjectLifecycleLookup.Found>(row.Interpret(accepted.Original.Operation));

        Assert.Equal(accepted.Original, found.Accepted.Original);
        var deletion = Assert.IsType<ProjectLifecycleAcknowledgement.DeletionCommitted>(found.Accepted.Acknowledgement);
        Assert.Single(deletion.Completion.Obligations);
        Assert.Equal(ProjectLifecycleOperationCodec.EncodeAccepted(accepted),
            ProjectLifecycleOperationCodec.EncodeAccepted(found.Accepted));
    }

    [Fact]
    public void Existing_corrupt_or_unsupported_receipt_never_becomes_not_observed() {
        AssertInvalid(row => row.AcceptedPayload = null, ProjectLifecycleUnavailableReason.InvalidStoredResult);
        AssertInvalid(row => row.AcceptedPayload = [1, 2, 3], ProjectLifecycleUnavailableReason.InvalidStoredResult);
        AssertInvalid(row => row.AcceptedPayload = new byte[ProjectLifecycleAccepted.MaximumPayloadBytes + 1], ProjectLifecycleUnavailableReason.InvalidStoredResult);
        AssertInvalid(row => row.SemanticDigest = new byte[31], ProjectLifecycleUnavailableReason.InvalidStoredResult);
        AssertInvalid(row => row.ResultVersion = 2, ProjectLifecycleUnavailableReason.UnsupportedResult);
        AssertInvalid(row => row.ProjectId = Guid.NewGuid(), ProjectLifecycleUnavailableReason.InvalidStoredResult);
        AssertInvalid(row => row.LifetimeId = Guid.NewGuid(), ProjectLifecycleUnavailableReason.InvalidStoredResult);
        AssertInvalid(row => row.CommandKind = (int)ProjectLifecycleCommandKind.EnsureLink, ProjectLifecycleUnavailableReason.InvalidStoredResult);
        AssertInvalid(row => row.DatabaseProfileId = Guid.NewGuid(), ProjectLifecycleUnavailableReason.InvalidStoredResult);
    }

    [Fact]
    public void Acceptance_is_bounded_by_mutation_time_and_the_original_replay_promise() {
        AssertInvalid(row => row.AcceptedAtUtc = row.AdmissionClosesAtUtc!.Value.AddMilliseconds(row.MutationDeadlineMilliseconds!.Value),
            ProjectLifecycleUnavailableReason.InvalidStoredResult);
        AssertInvalid(row => row.AcceptedAtUtc = row.OpenedAtUtc!.Value.AddTicks(-1), ProjectLifecycleUnavailableReason.InvalidStoredResult);
        AssertInvalid(row => row.ReplayRetentionSeconds += 1, ProjectLifecycleUnavailableReason.InvalidStoredResult);
        AssertInvalid(row => row.MutationDeadlineMilliseconds = 60_001, ProjectLifecycleUnavailableReason.InconsistentMetadata);
        AssertInvalid(row => row.ReplayRetentionSeconds = 31_536_001, ProjectLifecycleUnavailableReason.InconsistentMetadata);
        AssertInvalid(row => row.AdmissionClosesAtUtc = DateTimeOffset.MaxValue.AddTicks(-1),
            ProjectLifecycleUnavailableReason.InconsistentMetadata);
    }

    [Fact]
    public void Missing_receipt_distinguishes_open_draining_closed_expired_and_future_epochs() {
        var (row, accepted) = StoredDeletion();
        var original = accepted.Original.Operation;
        row.ReceiptPresent = false;
        Assert.Equal(ProjectOperationObservationState.Open,
            Assert.IsType<ProjectLifecycleLookup.NotObserved>(row.Interpret(original)).State);
        row.ObservedAtUtc = row.AdmissionClosesAtUtc!.Value;
        Assert.Equal(ProjectOperationObservationState.Draining,
            Assert.IsType<ProjectLifecycleLookup.NotObserved>(row.Interpret(original)).State);
        row.EpochState = (int)FiniteOperationEpochState.Closed;
        row.ClosedAtUtc = row.ObservedAtUtc.AddMinutes(2);
        row.ObservedAtUtc = row.ClosedAtUtc.Value.AddSeconds(1);
        var closed = Assert.IsType<ProjectLifecycleLookup.ClosedAbsent>(row.Interpret(original));
        Assert.Equal(row.ClosedAtUtc.Value.AddSeconds(row.ReplayRetentionSeconds!.Value), closed.ReplayUntilUtc);
        row.CurrentEpoch = original.Epoch.Value + 1;
        row.RetiredThroughEpoch = original.Epoch.Value;
        Assert.IsType<ProjectLifecycleLookup.Expired>(row.Interpret(original));
        var future = new ProjectLifecycleOperation(original.DatabaseProfileId, original.Namespace,
            new(row.CurrentEpoch.Value + 1), original.CallerBinding, original.Id);
        Assert.Equal(ProjectLifecycleUnavailableReason.InconsistentMetadata,
            Assert.IsType<ProjectLifecycleLookup.Unavailable>(row.Interpret(future)).Reason);
        row.EpochState = null;
        row.OpenedAtUtc = null;
        row.AdmissionClosesAtUtc = null;
        row.ClosedAtUtc = null;
        row.MutationDeadlineMilliseconds = null;
        row.ReplayRetentionSeconds = null;
        Assert.IsType<ProjectLifecycleLookup.UnknownFutureEpoch>(row.Interpret(future));
        row.ReceiptPresent = true;
        Assert.Equal(ProjectLifecycleUnavailableReason.InconsistentMetadata,
            Assert.IsType<ProjectLifecycleLookup.Unavailable>(row.Interpret(future)).Reason);
    }

    [Fact]
    public void Missing_or_incompatible_pool_metadata_cannot_claim_absence() {
        AssertInvalid(row => row.RetentionProtocolVersion = null, ProjectLifecycleUnavailableReason.MissingMetadata);
        AssertInvalid(row => row.RetentionProtocolVersion = 1, ProjectLifecycleUnavailableReason.UnsupportedProtocol);
        AssertInvalid(row => row.RetiredThroughEpoch = row.CurrentEpoch, ProjectLifecycleUnavailableReason.InconsistentMetadata);
        AssertInvalid(row => row.OpenedAtUtc = null, ProjectLifecycleUnavailableReason.MissingMetadata);
        AssertInvalid(row => row.EpochState = 42, ProjectLifecycleUnavailableReason.InconsistentMetadata);
        AssertInvalid(row => row.ClosedAtUtc = row.ObservedAtUtc, ProjectLifecycleUnavailableReason.InconsistentMetadata);
        AssertInvalid(row => row.ObservedAtUtc = DateTimeOffset.MaxValue, ProjectLifecycleUnavailableReason.InconsistentMetadata);
        AssertInvalid(row => {
            row.ReceiptPresent = false;
            row.EpochState = (int)FiniteOperationEpochState.Closed;
            row.ClosedAtUtc = row.ObservedAtUtc.AddTicks(1);
        }, ProjectLifecycleUnavailableReason.InconsistentMetadata);
    }

    private static void AssertInvalid(Action<ProjectLifecycleReceiptRow> change, ProjectLifecycleUnavailableReason reason) {
        var (row, accepted) = StoredDeletion();
        change(row);
        Assert.Equal(reason, Assert.IsType<ProjectLifecycleLookup.Unavailable>(row.Interpret(accepted.Original.Operation)).Reason);
    }

    private static (ProjectLifecycleReceiptRow Row, ProjectLifecycleAccepted Accepted) StoredDeletion() {
        var profile = Guid.NewGuid();
        var ns = new DatasetNamespaceId(Guid.NewGuid());
        var operation = new ProjectLifecycleOperation(profile, ns, new(1), ProjectMutationCallerScope.LocalWorkspace.Bind(ns), Guid.NewGuid());
        var project = new ProjectWriteAdmission(profile, Guid.NewGuid(), Guid.NewGuid());
        var opened = new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
        var admissionCloses = opened.AddMinutes(10);
        var acceptedAt = opened.AddMinutes(1);
        var replayUntil = admissionCloses.AddSeconds(30).AddHours(1);
        var completion = new ProjectDeletionCompletionBinding(new(new(profile, ns, new(Guid.NewGuid())), project),
            [new(new("workbench"), Guid.NewGuid())]);
        var accepted = new ProjectLifecycleAccepted(new(operation, ProjectLifecycleInterpretation.VersionOne,
            new ProjectLifecycleCommand.Delete(new(project, Guid.NewGuid()))), 1, acceptedAt, replayUntil,
            new ProjectLifecycleAcknowledgement.DeletionCommitted(completion));
        return (new() {
            CurrentEpoch = 1,
            RetiredThroughEpoch = 0,
            RetentionProtocolVersion = ProjectLifecycleReceiptRow.RequiredRetentionProtocol,
            EpochState = (int)FiniteOperationEpochState.Open,
            OpenedAtUtc = opened,
            AdmissionClosesAtUtc = admissionCloses,
            MutationDeadlineMilliseconds = 30_000,
            ReplayRetentionSeconds = 3600,
            ObservedAtUtc = opened.AddMinutes(2),
            ReceiptPresent = true,
            DatabaseProfileId = profile,
            SchemaVersion = 1,
            NormalizationVersion = 1,
            ResultVersion = ProjectLifecycleAccepted.VersionOne,
            CommandKind = (int)ProjectLifecycleCommandKind.Delete,
            SemanticDigest = ProjectLifecycleOperationCodec.ComputeDigest(accepted.Original,
                ProjectMutationCallerScope.LocalWorkspace, ProjectLifecycleEffectBinding.None).ToArray(),
            ProjectId = project.ProjectId,
            LifetimeId = project.LifetimeId,
            AcceptedAtUtc = acceptedAt,
            AcceptedPayload = ProjectLifecycleOperationCodec.EncodeAccepted(accepted)
        }, accepted);
    }
}
