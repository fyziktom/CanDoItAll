using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class ProjectLifecycleOperationTests {
    [Fact]
    public Task Offline_recovery_releases_only_the_original_gate_without_repeating_deletion_or_dispatching_cleanup() =>
        WithFixtureAsync(async fixture => {
            var completion = await PrepareHeldCompletionAsync(fixture);
            var recovery = new RecoveryControls(fixture);
            var coordinator = recovery.CreateCoordinator();
            var plan = Assert.IsType<ProjectDeletionRecoveryResult.Observed>(await coordinator.InspectAsync(completion.Original.Completion)).Plan;
            Assert.Null(plan.ReleasedAtUtc);
            var before = await fixture.StateAsync();
            var released = Assert.IsType<ProjectDeletionRecoveryResult.Released>(await coordinator.ReconcileAsync(
                completion.Original.Completion, plan.Digest, ProjectDeletionRecoveryPrecondition.AllWriterHostsStopped));
            Assert.False(released.CleanupUnconfirmed);
            Assert.Equal(1, recovery.SuccessfulMarks);
            Assert.False(recovery.SourceHeld);
            Assert.Equal(0, fixture.Participant.Completed);
            before.RequireEqual(await fixture.StateAsync());
            var repeated = Assert.IsType<ProjectDeletionRecoveryResult.Released>(await coordinator.ReconcileAsync(
                completion.Original.Completion, plan.Digest, ProjectDeletionRecoveryPrecondition.AllWriterHostsStopped));
            Assert.Equal(released.ReleasedAtUtc, repeated.ReleasedAtUtc);
            Assert.Equal(1, recovery.SuccessfulMarks);
            Assert.IsType<ProjectDeletionCompletionObservation.Released>(await fixture.Services.GetRequiredService<IProjectDeletionCompletionQuery>()
                .ObserveAsync(completion));
        }, new() { Failure = FailurePoint.OwnerDisposal });

    [Theory]
    [InlineData(RecoveryFailure.SourceAcquisition)]
    [InlineData(RecoveryFailure.SourceRelease)]
    [InlineData(RecoveryFailure.ProbeRollback)]
    [InlineData(RecoveryFailure.ProbeDisposal)]
    [InlineData(RecoveryFailure.ProfileAfterProbe)]
    [InlineData(RecoveryFailure.ReleaseUpdate)]
    public Task Offline_recovery_never_marks_after_probe_failure_or_uncertain_terminal_release(RecoveryFailure failure) =>
        WithFixtureAsync(async fixture => {
            var completion = await PrepareHeldCompletionAsync(fixture);
            var recovery = new RecoveryControls(fixture) { Failure = failure };
            var coordinator = recovery.CreateCoordinator();
            var plan = Assert.IsType<ProjectDeletionRecoveryResult.Observed>(await coordinator.InspectAsync(completion.Original.Completion)).Plan;
            var result = await coordinator.ReconcileAsync(completion.Original.Completion, plan.Digest,
                ProjectDeletionRecoveryPrecondition.AllWriterHostsStopped);
            Assert.IsType<ProjectDeletionRecoveryResult.Refused>(result);
            Assert.False(recovery.SourceHeld);
            Assert.Equal(0, fixture.Participant.Completed);
            Assert.Equal(failure == RecoveryFailure.ReleaseUpdate ? 1 : 0, recovery.MarkAttempts);
            Assert.IsType<ProjectDeletionCompletionObservation.AwaitingOwnerRelease>(await fixture.Services.GetRequiredService<IProjectDeletionCompletionQuery>()
                .ObserveAsync(completion));
        }, new() { Failure = FailurePoint.OwnerDisposal });

    [Theory]
    [InlineData(RecoveryFailure.ReleaseCommitAcknowledgement)]
    [InlineData(RecoveryFailure.ReleaseDisposal)]
    public Task Offline_recovery_preserves_a_real_committed_release_after_losing_commit_or_disposal_acknowledgement(RecoveryFailure failure) =>
        WithFixtureAsync(async fixture => {
            var completion = await PrepareHeldCompletionAsync(fixture);
            var recovery = new RecoveryControls(fixture) { Failure = failure };
            var coordinator = recovery.CreateCoordinator();
            var plan = Assert.IsType<ProjectDeletionRecoveryResult.Observed>(await coordinator.InspectAsync(completion.Original.Completion)).Plan;
            var result = Assert.IsType<ProjectDeletionRecoveryResult.Released>(await coordinator.ReconcileAsync(completion.Original.Completion,
                plan.Digest, ProjectDeletionRecoveryPrecondition.AllWriterHostsStopped));
            Assert.Equal(failure == RecoveryFailure.ReleaseCommitAcknowledgement, recovery.CommitAcknowledgementFaulted);
            Assert.True(result.CleanupUnconfirmed);
            Assert.Equal(1, recovery.SuccessfulMarks);
            Assert.Equal(0, fixture.Participant.Completed);
            Assert.False(recovery.SourceHeld);
            var gate = Assert.IsType<ProjectDeletionCompletionObservation.Released>(await fixture.Services.GetRequiredService<IProjectDeletionCompletionQuery>()
                .ObserveAsync(completion));
            Assert.Equal(gate.ReleasedAtUtc, result.ReleasedAtUtc);
        }, new() { Failure = FailurePoint.OwnerDisposal });

    [Fact]
    public Task Offline_recovery_observes_another_reconciler_committing_in_the_probe_to_mark_gap() =>
        WithFixtureAsync(async fixture => {
            var completion = await PrepareHeldCompletionAsync(fixture);
            var beforeMark = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var continueMark = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var first = new RecoveryControls(fixture) { BeforeMark = async () => {
                beforeMark.TrySetResult();
                await continueMark.Task.WaitAsync(deadline.Token);
            } };
            var second = new RecoveryControls(fixture);
            var coordinator = first.CreateCoordinator();
            var plan = Assert.IsType<ProjectDeletionRecoveryResult.Observed>(await coordinator.InspectAsync(completion.Original.Completion)).Plan;
            Task<ProjectDeletionRecoveryResult>? waiting = null;
            Exception? primary = null;
            try {
                waiting = coordinator.ReconcileAsync(completion.Original.Completion, plan.Digest,
                    ProjectDeletionRecoveryPrecondition.AllWriterHostsStopped, deadline.Token);
                await beforeMark.Task.WaitAsync(deadline.Token);
                var winner = Assert.IsType<ProjectDeletionRecoveryResult.Released>(await second.CreateCoordinator().ReconcileAsync(
                    completion.Original.Completion, plan.Digest, ProjectDeletionRecoveryPrecondition.AllWriterHostsStopped, deadline.Token));
                continueMark.TrySetResult();
                var found = Assert.IsType<ProjectDeletionRecoveryResult.Released>(await waiting);
                Assert.Equal(winner.ReleasedAtUtc, found.ReleasedAtUtc);
                Assert.Equal(1, first.MarkAttempts);
                Assert.Equal(0, first.SuccessfulMarks);
                Assert.Equal(1, second.SuccessfulMarks);
                Assert.Equal(0, fixture.Participant.Completed);
            } catch (Exception failure) {
                primary = failure;
                throw;
            } finally {
                continueMark.TrySetResult();
                await DrainOwnerTasksAsync(waiting is null ? [] : [waiting], deadline, primary);
            }
        }, new() { Failure = FailurePoint.OwnerDisposal });

    [Fact]
    public Task Offline_recovery_refuses_the_original_callers_session_lock_during_a_native_handoff() =>
        WithFixtureAsync(async fixture => {
            var completion = await PrepareHeldCompletionAsync(fixture);
            var recovery = new RecoveryControls(fixture);
            var coordinator = recovery.CreateCoordinator();
            var plan = Assert.IsType<ProjectDeletionRecoveryResult.Observed>(await coordinator.InspectAsync(completion.Original.Completion)).Plan;
            string key = await fixture.ReadAsync(async context => {
                byte[] bytes = await context.Set<ProjectDeletionCompletionRecord>().Select(row => row.RecoveryPayload).SingleAsync();
                var evidence = ProjectDeletionRecoveryCodec.Read(bytes, completion.Original);
                return Assert.IsType<ProjectDeletionOriginEvidence.Ordinary>(evidence.Origin).RequireOperationKey();
            });
            await using (var holder = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(fixture.Application.ActiveProfile.ConnectionString) {
                Pooling = false
            }.ConnectionString)) {
                await holder.OpenAsync();
                await using var command = new NpgsqlCommand("SELECT pg_advisory_lock(hashtextextended(@key, 0))", holder);
                command.Parameters.AddWithValue("key", key);
                await command.ExecuteNonQueryAsync();
                var refused = Assert.IsType<ProjectDeletionRecoveryResult.Refused>(await coordinator.ReconcileAsync(
                    completion.Original.Completion, plan.Digest, ProjectDeletionRecoveryPrecondition.AllWriterHostsStopped));
                Assert.Equal(ProjectDeletionRecoveryRefusal.ExclusionBusy, refused.Reason);
                Assert.Equal(0, recovery.MarkAttempts);
            }
            Assert.IsType<ProjectDeletionRecoveryResult.Released>(await coordinator.ReconcileAsync(completion.Original.Completion,
                plan.Digest, ProjectDeletionRecoveryPrecondition.AllWriterHostsStopped));
        }, new() { Failure = FailurePoint.OwnerDisposal });

    [Fact]
    public Task Offline_recovery_rejects_changed_plans_before_acquiring_sources() =>
        WithFixtureAsync(async fixture => {
            var completion = await PrepareHeldCompletionAsync(fixture);
            var recovery = new RecoveryControls(fixture);
            var refused = Assert.IsType<ProjectDeletionRecoveryResult.Refused>(await recovery.CreateCoordinator().ReconcileAsync(
                completion.Original.Completion, new(new string('0', 64)), ProjectDeletionRecoveryPrecondition.AllWriterHostsStopped));
            Assert.Equal(ProjectDeletionRecoveryRefusal.PlanChanged, refused.Reason);
            Assert.Equal(0, recovery.SourceAcquisitions);
            Assert.Equal(0, recovery.MarkAttempts);
        }, new() { Failure = FailurePoint.OwnerDisposal });

    [Fact]
    public Task Offline_recovery_probes_preparation_keys_even_when_the_participant_produced_no_obligation() =>
        WithFixtureAsync(async fixture => {
            var completion = await PrepareHeldCompletionAsync(fixture);
            Assert.Empty(completion.Obligations);
            var recovery = new RecoveryControls(fixture);
            var coordinator = recovery.CreateCoordinator();
            var plan = Assert.IsType<ProjectDeletionRecoveryResult.Observed>(await coordinator.InspectAsync(completion.Original.Completion)).Plan;
            await using (var holder = new ProjectsDbContext(recovery.Options)) {
                await using var transaction = await holder.Database.BeginTransactionAsync();
                string key = Assert.Single(fixture.Participant.PreparationScopeKeys).Value;
                await holder.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))");
                var refused = Assert.IsType<ProjectDeletionRecoveryResult.Refused>(await coordinator.ReconcileAsync(
                    completion.Original.Completion, plan.Digest, ProjectDeletionRecoveryPrecondition.AllWriterHostsStopped));
                Assert.Equal(ProjectDeletionRecoveryRefusal.ExclusionBusy, refused.Reason);
                Assert.Equal(0, recovery.MarkAttempts);
                Assert.False(recovery.SourceHeld);
                await transaction.RollbackAsync();
            }
            Assert.IsType<ProjectDeletionRecoveryResult.Released>(await coordinator.ReconcileAsync(completion.Original.Completion,
                plan.Digest, ProjectDeletionRecoveryPrecondition.AllWriterHostsStopped));
            Assert.Equal(0, recovery.ParticipantInspections);
        }, new() { Failure = FailurePoint.OwnerDisposal, ReturnNoPreparation = true });

    [Fact]
    public Task Offline_recovery_refuses_a_held_lifecycle_owner_without_waiting_for_it_to_finish() =>
        WithFixtureAsync(async fixture => {
            var completion = await PrepareHeldCompletionAsync(fixture);
            var recovery = new RecoveryControls(fixture);
            var coordinator = recovery.CreateCoordinator();
            var plan = Assert.IsType<ProjectDeletionRecoveryResult.Observed>(await coordinator.InspectAsync(completion.Original.Completion)).Plan;
            var active = await fixture.Services.GetRequiredService<DatasetActivationService>()
                .ObserveActiveBindingAsync(completion.Original.Completion.DatabaseProfileId);
            await using (var holder = new ProjectsDbContext(recovery.Options)) {
                await using var transaction = await holder.Database.BeginTransactionAsync();
                await DatasetLifecycleStore.RequireActiveForMutationAsync(holder, active.Record.Plan.Binding);
                var refused = Assert.IsType<ProjectDeletionRecoveryResult.Refused>(await coordinator.ReconcileAsync(
                    completion.Original.Completion, plan.Digest, ProjectDeletionRecoveryPrecondition.AllWriterHostsStopped));
                Assert.Equal(ProjectDeletionRecoveryRefusal.ExclusionBusy, refused.Reason);
                Assert.Equal(0, recovery.MarkAttempts);
                await transaction.RollbackAsync();
            }
            Assert.IsType<ProjectDeletionRecoveryResult.Released>(await coordinator.ReconcileAsync(completion.Original.Completion,
                plan.Digest, ProjectDeletionRecoveryPrecondition.AllWriterHostsStopped));
        }, new() { Failure = FailurePoint.OwnerDisposal });

    private static async Task<ProjectDeletionCompletionBinding> PrepareHeldCompletionAsync(LifecycleFixture fixture) {
        var target = await fixture.SeedAsync("Offline release subject");
        var submission = await fixture.SubmitIntentAsync(new ProjectLifecycleCommand.Delete(target));
        fixture.Faults.Armed = true;
        var accepted = Assert.IsType<ProjectLifecycleOutcome.Accepted>(await fixture.Writer.SubmitAsync(submission, fixture.Invocation));
        Assert.Equal(ProjectLifecycleCompletionIssue.OwnerReleaseUnconfirmed, accepted.CompletionIssue);
        Assert.True(fixture.Faults.Committed);
        Assert.True(fixture.Faults.OwnerDisposed);
        return Assert.IsType<ProjectLifecycleAcknowledgement.DeletionCommitted>(accepted.Result.Acknowledgement).Completion;
    }

    public enum RecoveryFailure {
        None, SourceAcquisition, SourceRelease, ProbeRollback, ProbeDisposal, ProfileAfterProbe, ReleaseUpdate, ReleaseCommitAcknowledgement, ReleaseDisposal
    }

    private sealed class RecoveryControls : IProjectDeletionRecoveryEnvironment, IProjectDeletionRecoveryParticipant {
        private readonly LifecycleFixture fixture;
        private readonly ResolvedDatabaseProfile profile;
        private readonly CoordinatedDatabaseTransaction transactions;
        private readonly ProjectDeletionRecoveryDigest participantDigest;
        internal DbContextOptions<ProjectsDbContext> Options { get; }
        internal RecoveryFailure Failure;
        internal bool SourceHeld;
        internal bool SourceReleased;
        internal int SourceAcquisitions;
        internal int ParticipantInspections;
        internal int MarkAttempts;
        internal int SuccessfulMarks;
        internal bool CommitAcknowledgementFaulted;
        internal DbContext? ProbeContext;
        internal DbContext? MarkContext;
        internal Func<Task>? BeforeMark;

        internal RecoveryControls(LifecycleFixture fixture) {
            this.fixture = fixture;
            profile = fixture.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
            transactions = CoordinatedDatabaseTransaction.ForProfile(profile);
            string connection = new NpgsqlConnectionStringBuilder(profile.ConnectionString) { Pooling = false, Timeout = 5, CommandTimeout = 5 }.ConnectionString;
            Options = new DbContextOptionsBuilder<ProjectsDbContext>().UseNpgsql(connection)
                .AddInterceptors(new RecoveryCommands(this), new RecoveryTransactions(this), new RecoveryConnections(this)).Options;
            participantDigest = new(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(fixture.Participant.Original)));
        }

        internal ProjectDeletionRecoveryCoordinator CreateCoordinator() => new(profile, Options, transactions,
            fixture.Services.GetRequiredService<DatasetActivationService>(), this, [this], TimeProvider.System,
            NullLogger<ProjectDeletionRecoveryCoordinator>.Instance);

        public ProjectDeletionRecoveryDigest ProfileIdentity { get; } = new(new string('a', 64));
        public ProjectDeletionParticipantId Id => fixture.Participant.Id;
        public int ProtocolVersion => fixture.Participant.RecoveryProtocolVersion;
        public IReadOnlyCollection<ProjectDeletionPreparationScopeKey> PreparationScopeKeys => fixture.Participant.PreparationScopeKeys;

        public async Task RequireOriginalProfileAsync(CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            if (SourceReleased && Failure == RecoveryFailure.ProfileAfterProbe) {
                throw new InvalidOperationException("Injected profile change after terminal source release.");
            }
            if (SourceReleased && BeforeMark is { } pause) {
                BeforeMark = null;
                await pause();
            }
        }

        public Task RequireOriginalSourceAsync(ProjectDeletionSourceRecovery source, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.IsType<ProjectDeletionSourceRecovery.NoExternalSourceLease>(source);
            return Task.CompletedTask;
        }

        public ValueTask<IAsyncDisposable> AcquireSourceExclusionAsync(ProjectDeletionSourceRecovery source, CancellationToken cancellationToken) {
            Assert.IsType<ProjectDeletionSourceRecovery.NoExternalSourceLease>(source);
            SourceAcquisitions++;
            if (Failure == RecoveryFailure.SourceAcquisition) {
                throw new IOException("Injected source acquisition failure.");
            }
            Assert.False(SourceHeld);
            SourceHeld = true;
            return ValueTask.FromResult<IAsyncDisposable>(new RecoverySource(this));
        }

        public async Task<ProjectDeletionRecoveryParticipantEvidence> InspectAsync(ProjectDeletionPreparationContext original,
            Guid recoveryId, ProjectDeletionRecoveryReadMode mode, CancellationToken cancellationToken = default) {
            Assert.Equal(fixture.Participant.Original, original);
            Assert.Equal(fixture.Participant.RecoveryId, recoveryId);
            if (mode == ProjectDeletionRecoveryReadMode.Exclusive) {
                Assert.True(SourceHeld);
            }
            await using var enrolled = await transactions.CreateEnlistedAsync(Options, configured => new ProjectsDbContext(configured), cancellationToken);
            Assert.NotNull(enrolled.Database.CurrentTransaction);
            ParticipantInspections++;
            return new(new(Id, recoveryId), ProtocolVersion, false, participantDigest);
        }
    }

    private sealed class RecoverySource(RecoveryControls controls) : IAsyncDisposable {
        public ValueTask DisposeAsync() {
            Assert.True(controls.SourceHeld);
            controls.SourceHeld = false;
            controls.SourceReleased = true;
            if (controls.Failure == RecoveryFailure.SourceRelease) {
                throw new IOException("Injected source release acknowledgement failure.");
            }
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecoveryCommands(RecoveryControls controls) : DbCommandInterceptor {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.StartsWith("LOCK TABLE \"Infrastructure_DatasetLifecycle\" IN EXCLUSIVE", StringComparison.Ordinal)) {
                controls.ProbeContext = data.Context;
            }
            if (command.CommandText.StartsWith("UPDATE \"Projects_DeletionCompletions\"", StringComparison.Ordinal)) {
                Assert.False(controls.SourceHeld);
                Assert.True(controls.SourceReleased);
                controls.MarkContext = data.Context;
                controls.MarkAttempts++;
            }
            return ValueTask.FromResult(result);
        }

        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData data,
            int result, CancellationToken cancellationToken = default) {
            if (command.CommandText.StartsWith("UPDATE \"Projects_DeletionCompletions\"", StringComparison.Ordinal)) {
                if (result == 1) {
                    controls.SuccessfulMarks++;
                }
                if (controls.Failure == RecoveryFailure.ReleaseUpdate) {
                    throw new IOException("Injected failure after UPDATE and before COMMIT.");
                }
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RecoveryTransactions(RecoveryControls controls) : DbTransactionInterceptor {
        public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData data,
            CancellationToken cancellationToken = default) {
            if (ReferenceEquals(data.Context, controls.ProbeContext) && controls.Failure == RecoveryFailure.ProbeRollback) {
                throw new IOException("Injected probe rollback acknowledgement failure.");
            }
            return Task.CompletedTask;
        }

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData data,
            CancellationToken cancellationToken = default) {
            if (ReferenceEquals(data.Context, controls.MarkContext) && controls.Failure == RecoveryFailure.ReleaseCommitAcknowledgement) {
                controls.CommitAcknowledgementFaulted = true;
                throw new TimeoutException("Injected lost release acknowledgement after the actual COMMIT.");
            }
            return Task.CompletedTask;
        }
    }

    private sealed class RecoveryConnections(RecoveryControls controls) : DbConnectionInterceptor {
        public override Task ConnectionDisposedAsync(DbConnection connection, ConnectionEndEventData data) {
            if (ReferenceEquals(data.Context, controls.ProbeContext) && controls.Failure == RecoveryFailure.ProbeDisposal) {
                throw new IOException("Injected probe context disposal acknowledgement failure.");
            }
            if (ReferenceEquals(data.Context, controls.MarkContext) && controls.Failure == RecoveryFailure.ReleaseDisposal) {
                throw new IOException("Injected disposal acknowledgement failure after the actual release COMMIT.");
            }
            return Task.CompletedTask;
        }
    }
}
