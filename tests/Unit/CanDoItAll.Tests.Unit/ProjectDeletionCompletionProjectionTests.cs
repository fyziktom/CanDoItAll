using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectDeletionCompletionProjectionTests {
    [Fact]
    public void Full_binding_release_requires_the_complete_exact_obligation_set_including_empty() {
        var binding = Original();
        var row = Row(binding);
        Assert.IsType<ProjectDeletionCompletionObservation.AwaitingOwnerRelease>(row.Interpret(binding));
        row.ReleaseState = (int)ProjectDeletionCompletionReleaseState.Released;
        row.ReleasedAtUtc = DateTimeOffset.UtcNow;
        Assert.IsType<ProjectDeletionCompletionObservation.Released>(row.Interpret(binding));
        var obligation = new ProjectDeletionObligation(new("participant"), Guid.NewGuid());
        Assert.IsType<ProjectDeletionCompletionObservation.Unavailable>(row.Interpret(new(binding.Original, [obligation])));
        row.ReferencePayload = ProjectDeletionCompletionCodec.Write(new(binding.Original, [obligation]));
        Assert.Equal(ProjectDeletionCompletionUnavailableReason.ObligationMismatch,
            Assert.IsType<ProjectDeletionCompletionObservation.Unavailable>(row.Interpret(binding)).Reason);
        Assert.IsType<ProjectDeletionCompletionObservation.Released>(row.Interpret(new(binding.Original, [obligation])));
        Assert.IsType<ProjectDeletionCompletionObservation.Unavailable>(row.Interpret(new(binding.Original,
            [new(obligation.Participant, Guid.NewGuid())])));
    }

    [Fact]
    public async Task Empty_obligations_do_not_prove_release_and_projection_never_dispatches_effects() {
        var binding = Original();
        var query = new ReleaseProbe();
        var projection = Projection(query);
        var pending = Assert.IsType<ProjectDeletionCompletionProjectionResult.Pending>(await projection.ObserveAsync(binding));
        Assert.True(pending.Recovery.OwnerCompletionUnconfirmed);
        Assert.Empty(pending.Recovery.Failures);
        Assert.Equal(1, query.Calls);
        query.Result = new ProjectDeletionCompletionObservation.Released(DateTimeOffset.UtcNow);
        var complete = Assert.IsType<ProjectDeletionCompletionProjectionResult.Completed>(await projection.ObserveAsync(binding));
        Assert.Equal(binding.Original.Project.ProjectId, complete.Result.ProjectId);
        Assert.Empty(complete.Result.Warnings);
        Assert.Equal(3, query.Calls);
    }

    [Fact]
    public async Task Exact_completed_warnings_and_pending_identity_are_preserved_without_inventory_or_claim_calls() {
        var completed = new Participant(new("completed"));
        var pending = new Participant(new("pending"));
        var binding = Original(new(completed.Id, completed.RecoveryId), new(pending.Id, pending.RecoveryId));
        var warning = new ProjectDeletionParticipantWarning(ProjectDeletionWarningKind.ManagedStorageRetainedByProvider,
            new(StorageProviderKind.Ipfs, Guid.NewGuid(), StorageLocatorKind.ContentAddress, "retained-reference", "Immutable provider"),
            "Retained", "Review original provider reference");
        completed.Observation = new ProjectDeletionParticipantObservation.Completed(new(completed.RecoveryId, [warning]));
        var query = new ReleaseProbe { Result = new ProjectDeletionCompletionObservation.Released(DateTimeOffset.UtcNow) };
        var result = Assert.IsType<ProjectDeletionCompletionProjectionResult.Pending>(await Projection(query, completed, pending).ObserveAsync(binding));
        Assert.False(result.Recovery.OwnerCompletionUnconfirmed);
        Assert.Equal(pending.RecoveryId, Assert.Single(result.Recovery.Failures).RecoveryId);
        var retained = Assert.Single(result.Warnings);
        Assert.Equal(warning.RetainedObject, retained.RetainedObject);
        Assert.Equal(completed.Id, retained.ParticipantId);
        Assert.Equal(completed.RecoveryId, retained.RecoveryId);
        Assert.Equal(binding.Original, completed.Original);
        Assert.Equal(binding.Original, pending.Original);
        pending.Observation = new ProjectDeletionParticipantObservation.Completed(ProjectDeletionParticipantCompletion.Empty(pending.RecoveryId));
        var complete = Assert.IsType<ProjectDeletionCompletionProjectionResult.Completed>(await Projection(query, completed, pending).ObserveAsync(binding));
        Assert.Single(complete.Result.Warnings);
    }

    [Fact]
    public async Task Missing_mismatched_or_failed_observation_keeps_exact_recovery_and_never_claims_cleanup_finished() {
        var participant = new Participant(new("participant"));
        var binding = Original(new ProjectDeletionObligation(participant.Id, participant.RecoveryId));
        var query = new ReleaseProbe { Result = new ProjectDeletionCompletionObservation.Released(DateTimeOffset.UtcNow) };
        var projection = Projection(query, participant);
        participant.Observation = new ProjectDeletionParticipantObservation.Unavailable(ProjectDeletionParticipantObservationFailure.MissingRecord);
        var missing = Assert.IsType<ProjectDeletionCompletionProjectionResult.Pending>(await projection.ObserveAsync(binding));
        Assert.Equal(participant.RecoveryId, Assert.Single(missing.Recovery.Failures).RecoveryId);
        participant.Observation = new ProjectDeletionParticipantObservation.Completed(ProjectDeletionParticipantCompletion.Empty(Guid.NewGuid()));
        var mismatched = Assert.IsType<ProjectDeletionCompletionProjectionResult.Pending>(await projection.ObserveAsync(binding));
        Assert.True(mismatched.Recovery.OwnerCompletionUnconfirmed);
        Assert.Equal(participant.RecoveryId, Assert.Single(mismatched.Recovery.Failures).RecoveryId);
        participant.Observation = new ProjectDeletionParticipantObservation.Completed(ProjectDeletionParticipantCompletion.Empty(participant.RecoveryId));
        query.FailOnCall = query.Calls + 2;
        var failedFinalRead = Assert.IsType<ProjectDeletionCompletionProjectionResult.Pending>(await projection.ObserveAsync(binding));
        Assert.True(failedFinalRead.Recovery.OwnerCompletionUnconfirmed);
        Assert.Equal(participant.RecoveryId, Assert.Single(failedFinalRead.Recovery.Failures).RecoveryId);
    }

    private static ProjectDeletionCompletionProjection Projection(ReleaseProbe query, params Participant[] participants)
        => new(query, participants, NullLogger<ProjectDeletionCompletionProjection>.Instance);

    private static ProjectDeletionCompletionBinding Original(params ProjectDeletionObligation[] obligations) {
        var profile = Guid.NewGuid();
        return new(new(new(profile, new(Guid.NewGuid()), new(Guid.NewGuid())),
            new(profile, Guid.NewGuid(), Guid.NewGuid())), obligations);
    }

    private static ProjectDeletionCompletionRow Row(ProjectDeletionCompletionBinding binding) => new() {
        CompletionId = binding.Original.Completion.Id.Value,
        DatabaseProfileId = binding.Original.Project.DatabaseProfileId,
        ProjectId = binding.Original.Project.ProjectId,
        LifetimeId = binding.Original.Project.LifetimeId,
        SchemaVersion = ProjectDeletionCompletionProtocol.SchemaVersion,
        OriginKind = (int)ProjectDeletionCompletionOrigin.OrdinaryDeletion,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        ReleaseState = (int)ProjectDeletionCompletionReleaseState.AwaitingOwnerRelease,
        NativeRetirementMatches = true,
        ReferencePayload = ProjectDeletionCompletionCodec.Write(binding)
    };

    private sealed class ReleaseProbe : IProjectDeletionCompletionQuery {
        internal ProjectDeletionCompletionObservation Result { get; set; } = new ProjectDeletionCompletionObservation.AwaitingOwnerRelease();
        internal int Calls { get; private set; }
        internal int? FailOnCall { get; set; }

        public Task<ProjectDeletionCompletionObservation> ObserveAsync(ProjectDeletionCompletionBinding original,
            CancellationToken cancellationToken = default) {
            Calls++;
            if (Calls == FailOnCall) {
                throw new IOException("Exact release read failed.");
            }
            return Task.FromResult(Result);
        }

        public Task<ProjectDeletionCompletionObservation> ObserveAsync(ProjectDeletionPreparationContext original,
            ProjectDeletionObligation obligation, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Projection must validate the complete original binding.");
    }

    private sealed class Participant(ProjectDeletionParticipantId id) : IProjectDeletionParticipant {
        public ProjectDeletionParticipantId Id => id;
        public int RecoveryProtocolVersion => 1;
        public IReadOnlyCollection<ProjectDeletionPreparationScopeKey> PreparationScopeKeys => [];
        internal Guid RecoveryId { get; } = Guid.NewGuid();
        internal ProjectDeletionPreparationContext? Original { get; private set; }
        internal ProjectDeletionParticipantObservation Observation { get; set; } = new ProjectDeletionParticipantObservation.Pending(ProjectDeletionRecoveryStatus.Pending);

        public Task<ProjectDeletionParticipantObservation> ObserveCompletionAsync(ProjectDeletionPreparationContext original,
            Guid recoveryId, CancellationToken cancellationToken = default) {
            Assert.Equal(RecoveryId, recoveryId);
            Original = original;
            return Task.FromResult(Observation);
        }

        public Task<ProjectDeletionParticipantPreparation?> PrepareAsync(Guid projectId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Projection cannot prepare effects.");
        public Task<ProjectDeletionParticipantCompletion> CompleteAsync(ProjectDeletionParticipantPreparation preparation,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("Projection cannot dispatch cleanup.");
        public Task<IReadOnlyList<ProjectDeletionParticipantRecovery>> ListPendingRecoveriesAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Projection cannot enumerate pending inventory.");
        public Task<IReadOnlyList<ProjectDeletionParticipantCompletionNotice>> ListCompletionNoticesAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Projection cannot enumerate completed inventory.");
    }
}
