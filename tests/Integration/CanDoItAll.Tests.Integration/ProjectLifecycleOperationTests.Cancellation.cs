using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class ProjectLifecycleOperationTests {
    [Fact]
    public Task Legacy_reservation_cancellation_refuses_before_effects() => WithFixtureAsync(async fixture => {
        var original = await CreateCancellationIntentAsync(fixture, false);
        await Assert.ThrowsAsync<NotSupportedException>(() => fixture.Services.GetRequiredService<ProjectWriteAdmissionService>()
            .CancelCreationAsync(original.Reservation));
        Assert.Equal(ProjectCreationReservationState.Reserved, (await ReadCancellationReservationAsync(fixture, original)).State);
        Assert.Equal(0, fixture.Participant.Prepared);
        Assert.Equal(0, (await fixture.StateAsync()).Completions);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Reservation_cancellation_retains_native_never_consumed_evidence_and_replays_without_effects(bool child) =>
        WithFixtureAsync(async fixture => {
            var original = await CreateCancellationIntentAsync(fixture, child);
            var (owner, reader) = CancellationOwners(fixture);
            fixture.Faults.Armed = true;
            var committed = Assert.IsType<ProjectReservationCancellationOutcome.Committed>(
                await owner.CancelAsync(original, fixture.Write));
            Assert.Null(committed.CompletionIssue);
            Assert.False(committed.Replayed);
            Assert.Equal(original.Original, committed.Completion.Original);
            var reservation = await ReadCancellationReservationAsync(fixture, original);
            Assert.Equal(ProjectCreationReservationState.Cancelled, reservation.State);
            Assert.Null(reservation.ConsumedAtUtc);
            Assert.NotNull(reservation.CancelledAtUtc);
            Assert.Equal(original.Original.Completion.Namespace.Value, reservation.NamespaceId);
            var record = await fixture.ReadAsync(context => context.Set<ProjectDeletionCompletionRecord>().AsNoTracking().SingleAsync());
            Assert.Equal(ProjectDeletionCompletionOrigin.ReservedCancellation, record.OriginKind);
            Assert.Equal(new ProjectDeletionOriginEvidence.ReservedCancellation(original.Preparation.Target),
                ProjectDeletionRecoveryCodec.Read(record.RecoveryPayload, original.Original).Origin);
            var state = await fixture.StateAsync();
            Assert.Empty(state.Retired);
            Assert.Empty(state.Receipts);
            Assert.DoesNotContain(original.Reservation.ProjectId, state.Projects);
            Assert.Equal(1, fixture.Participant.Prepared);
            Assert.Equal(1, fixture.Participant.Completed);
            Assert.IsType<ProjectReservationCancellationObservation.Committed>(await reader.ObserveAsync(original));
            var replay = Assert.IsType<ProjectReservationCancellationOutcome.Committed>(await owner.CancelAsync(original, fixture.Write));
            Assert.True(replay.Replayed);
            Assert.Equal(ProjectDeletionCompletionCodec.Write(committed.Completion), ProjectDeletionCompletionCodec.Write(replay.Completion));
            Assert.Equal(1, fixture.Participant.Prepared);
            Assert.Equal(1, fixture.Participant.Completed);
            state.RequireEqual(await fixture.StateAsync());
        });

    [Fact]
    public Task Cancellation_rejects_foreign_namespace_requester_parent_and_legacy_namespace_without_rebinding() =>
        WithFixtureAsync(async fixture => {
            var original = await CreateCancellationIntentAsync(fixture, true);
            var (owner, _) = CancellationOwners(fixture);
            var foreign = new ProjectReservationCancellationRequest(new(Guid.NewGuid()), original.Reservation);
            Assert.IsType<ProjectReservationCancellationOutcome.Unconfirmed>(await owner.CancelAsync(foreign, fixture.Write));
            foreach (var reservation in new[] {
                original.Reservation with { RequesterId = Guid.NewGuid() },
                original.Reservation with { ParentLifetimeId = Guid.NewGuid() }
            }) {
                var changed = new ProjectReservationCancellationRequest(original.Original.Completion.Namespace, reservation);
                var refused = await owner.CancelAsync(changed, fixture.Write);
                Assert.True(refused is ProjectReservationCancellationOutcome.Declined or ProjectReservationCancellationOutcome.Unconfirmed);
            }
            await fixture.MutateAsync(async context => {
                var row = await context.Set<ProjectCreationReservationRecord>().SingleAsync(value => value.Id == original.Reservation.Id);
                row.NamespaceId = null;
                await context.SaveChangesAsync();
            });
            Assert.IsType<ProjectReservationCancellationOutcome.Declined>(await owner.CancelAsync(original, fixture.Write));
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Services.GetRequiredService<ProjectWriteAdmissionService>()
                .CaptureReservationCancellationAsync(original.Reservation));
            Assert.Equal(0, fixture.Participant.Prepared);
            Assert.Equal(ProjectCreationReservationState.Reserved, (await ReadCancellationReservationAsync(fixture, original)).State);
            Assert.Equal(0, (await fixture.StateAsync()).Completions);
        });

    [Fact]
    public Task Cancellation_refuses_a_reservation_consumed_by_a_live_project() =>
        WithFixtureAsync(async fixture => {
            var original = await CreateCancellationIntentAsync(fixture, false);
            var created = await fixture.Services.GetRequiredService<ProjectsService>().CreateWithReceiptAsync(
                original.Reservation, new() { Name = "Already consumed" });
            Assert.True(created.IsSuccess);
            var before = await fixture.StateAsync();
            var (owner, _) = CancellationOwners(fixture);
            Assert.IsType<ProjectReservationCancellationOutcome.Declined>(await owner.CancelAsync(original, fixture.Write));
            before.RequireEqual(await fixture.StateAsync());
            Assert.Equal(ProjectCreationReservationState.Consumed, (await ReadCancellationReservationAsync(fixture, original)).State);
            Assert.Equal(0, fixture.Participant.Prepared);
        });

    [Fact]
    public Task Cancellation_completion_insert_failure_rolls_back_reservation_and_participant_preparation() {
        var faults = new OwnerFaults { Failure = FailurePoint.ReceiptInsert };
        return WithFixtureAsync(async fixture => {
            var original = await CreateCancellationIntentAsync(fixture, false);
            var (owner, reader) = CancellationOwners(fixture);
            faults.Armed = true;
            Assert.IsType<ProjectReservationCancellationOutcome.Declined>(await owner.CancelAsync(original, fixture.Write));
            Assert.True(faults.Triggered);
            Assert.False(faults.Committed);
            var row = await ReadCancellationReservationAsync(fixture, original);
            Assert.Equal(ProjectCreationReservationState.Reserved, row.State);
            Assert.Null(row.ConsumedAtUtc);
            Assert.Null(row.CancelledAtUtc);
            Assert.Equal(1, fixture.Participant.Prepared);
            Assert.Equal(0, fixture.Participant.Completed);
            Assert.Equal(0, (await fixture.StateAsync()).Completions);
            Assert.IsType<ProjectReservationCancellationObservation.NotObserved>(await reader.ObserveAsync(original));
        }, faults);
    }

    [Fact]
    public Task Cancellation_final_write_revocation_rolls_back_the_saved_cancelled_row() =>
        WithFixtureAsync(async fixture => {
            var original = await CreateCancellationIntentAsync(fixture, false);
            var (owner, _) = CancellationOwners(fixture);
            fixture.Write.DenyAt = 2;
            fixture.Faults.Armed = true;
            Assert.IsType<ProjectReservationCancellationOutcome.Declined>(await owner.CancelAsync(original, fixture.Write));
            Assert.True(fixture.Faults.ReceiptReached.Task.IsCompleted);
            Assert.False(fixture.Faults.Committed);
            Assert.Equal(ProjectCreationReservationState.Reserved, (await ReadCancellationReservationAsync(fixture, original)).State);
            Assert.Equal(0, fixture.Participant.Completed);
        });

    [Fact]
    public Task Cancellation_known_commit_with_uncertain_owner_release_keeps_participants_blocked() {
        var faults = new OwnerFaults { Failure = FailurePoint.OwnerDisposal };
        return WithFixtureAsync(async fixture => {
            var original = await CreateCancellationIntentAsync(fixture, false);
            var (owner, reader) = CancellationOwners(fixture);
            faults.Armed = true;
            var committed = Assert.IsType<ProjectReservationCancellationOutcome.Committed>(await owner.CancelAsync(original, fixture.Write));
            Assert.Equal(ProjectLifecycleCompletionIssue.OwnerReleaseUnconfirmed, committed.CompletionIssue);
            Assert.True(faults.Committed);
            Assert.Equal(0, fixture.Participant.Completed);
            Assert.IsType<ProjectReservationCancellationObservation.Committed>(await reader.ObserveAsync(original));
            Assert.IsType<ProjectDeletionCompletionObservation.AwaitingOwnerRelease>(await fixture.Services
                .GetRequiredService<IProjectDeletionCompletionQuery>().ObserveAsync(committed.Completion));
            Assert.Empty((await fixture.StateAsync()).Retired);
        }, faults);
    }

    [Fact]
    public Task Cancellation_lost_acknowledgement_keeps_original_intent_and_recovers_read_only() {
        var faults = new OwnerFaults { Failure = FailurePoint.CommitAcknowledgement, BlockRecovery = true };
        return WithFixtureAsync(async fixture => {
            var original = await CreateCancellationIntentAsync(fixture, false);
            var (owner, reader) = CancellationOwners(fixture);
            faults.Armed = true;
            var unknown = Assert.IsType<ProjectReservationCancellationOutcome.Unconfirmed>(await owner.CancelAsync(original, fixture.Write));
            Assert.Same(original, unknown.Original);
            Assert.True(faults.Committed);
            Assert.True(faults.RecoveryBlocked);
            Assert.Equal(0, fixture.Participant.Completed);
            faults.BlockRecovery = false;
            var before = await fixture.StateAsync();
            Assert.IsType<ProjectReservationCancellationObservation.Committed>(await reader.ObserveAsync(original));
            Assert.True(Assert.IsType<ProjectReservationCancellationOutcome.Committed>(await owner.CancelAsync(original, fixture.Write)).Replayed);
            Assert.Equal(1, fixture.Participant.Prepared);
            Assert.Equal(0, fixture.Participant.Completed);
            before.RequireEqual(await fixture.StateAsync());
        }, faults);
    }

    private static async Task<ProjectReservationCancellationRequest> CreateCancellationIntentAsync(LifecycleFixture fixture, bool child) {
        var parent = child ? await fixture.SeedAsync("Original reservation parent") : null;
        var admissions = fixture.Services.GetRequiredService<ProjectWriteAdmissionService>();
        var reservation = await admissions.ReserveCreationAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), parent?.Project.ProjectId,
            expectedParentAdmission: parent?.Project);
        return await admissions.CaptureReservationCancellationAsync(reservation);
    }

    private static Task<ProjectCreationReservationRecord> ReadCancellationReservationAsync(LifecycleFixture fixture,
        ProjectReservationCancellationRequest original)
        => fixture.ReadAsync(context => context.Set<ProjectCreationReservationRecord>().AsNoTracking()
            .SingleAsync(value => value.Id == original.Reservation.Id));

    private static (ProjectReservationCancellationOwner Owner, ProjectReservationCancellationReader Reader) CancellationOwners(LifecycleFixture fixture) {
        var factory = new OwnerFactory(new DbContextOptionsBuilder<ProjectsDbContext>(fixture.Services
            .GetRequiredService<DbContextOptions<ProjectsDbContext>>()).AddInterceptors(new CompensationCommands(fixture.Faults),
                new OwnerTransactions(fixture.Faults), new OwnerConnections(fixture.Faults)).Options);
        var reader = ActivatorUtilities.CreateInstance<ProjectReservationCancellationReader>(fixture.Services, factory);
        var completion = ActivatorUtilities.CreateInstance<ProjectDeletionCompletionOwner>(fixture.Services, factory);
        return (ActivatorUtilities.CreateInstance<ProjectReservationCancellationOwner>(fixture.Services, factory, reader, completion), reader);
    }
}
