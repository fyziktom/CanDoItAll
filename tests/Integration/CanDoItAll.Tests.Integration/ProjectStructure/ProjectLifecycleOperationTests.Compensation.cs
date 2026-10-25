using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class ProjectLifecycleOperationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Creation_compensation_commits_the_original_receipt_and_replays_without_participant_dispatch(bool reserved) =>
        WithFixtureAsync(async fixture => {
            var original = await CreateCompensationIntentAsync(fixture, reserved);
            var (owner, reader) = CompensationOwners(fixture);
            fixture.Faults.Armed = true;

            var committed = Assert.IsType<ProjectCreationCompensationOutcome.Committed>(
                await owner.CompensateAsync(original, fixture.Write));
            Assert.False(committed.Replayed);
            Assert.Null(committed.CompletionIssue);
            Assert.Equal(original.Original, committed.Completion.Original);
            Assert.Equal(1, fixture.Participant.Prepared);
            Assert.Equal(1, fixture.Participant.Completed);
            var retained = await fixture.ReadAsync(context => context.Set<ProjectDeletionCompletionRecord>().AsNoTracking().SingleAsync());
            Assert.Equal(original.Original.Completion.Id.Value, retained.CompletionId);
            Assert.Equal(original.Receipt.Namespace.Value, retained.NamespaceId);
            Assert.Equal(ProjectDeletionCompletionOrigin.CreationCompensation, retained.OriginKind);
            var evidence = ProjectDeletionRecoveryCodec.Read(retained.RecoveryPayload, original.Original);
            Assert.Equal(new ProjectDeletionOriginEvidence.Compensation(original.Receipt.Project,
                original.Receipt.Fingerprint, original.Receipt.Reservation), evidence.Origin);
            if (original.Receipt.Reservation is { } reservation) {
                var row = await fixture.ReadAsync(context => context.Set<ProjectCreationReservationRecord>().AsNoTracking()
                    .SingleAsync(value => value.Id == reservation.Id));
                Assert.Equal(ProjectCreationReservationState.Cancelled, row.State);
                Assert.NotNull(row.ConsumedAtUtc);
                Assert.NotNull(row.CancelledAtUtc);
                Assert.Equal(original.Receipt.Namespace.Value, row.NamespaceId);
            }
            var state = await fixture.StateAsync();
            Assert.Empty(state.Projects);
            Assert.Single(state.Retired);
            Assert.Empty(state.Receipts);
            Assert.Equal(1, state.Completions);
            Assert.IsType<ProjectCreationCompensationObservation.Committed>(await reader.ObserveAsync(original));
            var replay = Assert.IsType<ProjectCreationCompensationOutcome.Committed>(
                await owner.CompensateAsync(ProjectCreationCompensationRequest.FromReceipt(original.Receipt), fixture.Write));
            Assert.True(replay.Replayed);
            Assert.Equal(ProjectDeletionCompletionCodec.Write(committed.Completion), ProjectDeletionCompletionCodec.Write(replay.Completion));
            Assert.Equal(1, fixture.Participant.Prepared);
            Assert.Equal(1, fixture.Participant.Completed);
            state.RequireEqual(await fixture.StateAsync());
        });

    [Fact]
    public Task Compensation_refuses_an_edited_creation_or_foreign_original_namespace_without_adopting_current_state() =>
        WithFixtureAsync(async fixture => {
            var original = await CreateCompensationIntentAsync(fixture, true);
            var (owner, _) = CompensationOwners(fixture);
            await fixture.MutateAsync(async context => {
                var target = await context.Set<Project>().SingleAsync(value => value.Id == original.Receipt.Project.ProjectId);
                target.Name = "A later authored project";
                await context.SaveChangesAsync();
            });
            var before = await fixture.StateAsync();
            var refused = Assert.IsType<ProjectCreationCompensationOutcome.Declined>(await owner.CompensateAsync(original, fixture.Write));
            Assert.Equal(ProjectCreationCompensationRefusal.OriginalCreationChanged, refused.Reason);
            var foreign = ProjectCreationCompensationRequest.FromReceipt(new(original.Receipt.Project, new(Guid.NewGuid()),
                original.Receipt.Reservation, original.Receipt.Fingerprint));
            Assert.IsType<ProjectCreationCompensationOutcome.Unconfirmed>(await owner.CompensateAsync(foreign, fixture.Write));
            Assert.Equal(0, fixture.Participant.Prepared);
            Assert.Equal(0, fixture.Participant.Completed);
            before.RequireEqual(await fixture.StateAsync());
            var reservation = await fixture.ReadAsync(context => context.Set<ProjectCreationReservationRecord>().AsNoTracking().SingleAsync());
            Assert.Equal(ProjectCreationReservationState.Consumed, reservation.State);
            Assert.Null(reservation.CancelledAtUtc);
        });

    [Fact]
    public Task Compensation_completion_insert_failure_rolls_back_native_effects_and_exact_reservation_cancellation() {
        var faults = new OwnerFaults { Failure = FailurePoint.ReceiptInsert };
        return WithFixtureAsync(async fixture => {
            var original = await CreateCompensationIntentAsync(fixture, true);
            var (owner, reader) = CompensationOwners(fixture);
            var before = await fixture.StateAsync();
            faults.Armed = true;
            Assert.IsType<ProjectCreationCompensationOutcome.Declined>(await owner.CompensateAsync(original, fixture.Write));
            Assert.True(faults.Triggered);
            Assert.False(faults.Committed);
            Assert.Equal(1, fixture.Participant.Prepared);
            Assert.Equal(0, fixture.Participant.Completed);
            before.RequireEqual(await fixture.StateAsync());
            var reservation = await fixture.ReadAsync(context => context.Set<ProjectCreationReservationRecord>().AsNoTracking().SingleAsync());
            Assert.Equal(ProjectCreationReservationState.Consumed, reservation.State);
            Assert.Null(reservation.CancelledAtUtc);
            Assert.IsType<ProjectCreationCompensationObservation.NotObserved>(await reader.ObserveAsync(original));
        }, faults);
    }

    [Fact]
    public Task Compensation_final_write_revocation_rolls_back_staged_deletion_and_completion() =>
        WithFixtureAsync(async fixture => {
            var original = await CreateCompensationIntentAsync(fixture, false);
            var (owner, _) = CompensationOwners(fixture);
            var before = await fixture.StateAsync();
            fixture.Write.DenyAt = 2;
            fixture.Faults.Armed = true;
            Assert.IsType<ProjectCreationCompensationOutcome.Declined>(await owner.CompensateAsync(original, fixture.Write));
            Assert.Equal(2, fixture.Write.Observations);
            Assert.True(fixture.Faults.ReceiptReached.Task.IsCompleted);
            Assert.False(fixture.Faults.Committed);
            Assert.Equal(0, fixture.Participant.Completed);
            before.RequireEqual(await fixture.StateAsync());
        });

    [Fact]
    public Task Compensation_known_commit_survives_owner_disposal_failure_and_keeps_participants_blocked() {
        var faults = new OwnerFaults { Failure = FailurePoint.OwnerDisposal };
        return WithFixtureAsync(async fixture => {
            var original = await CreateCompensationIntentAsync(fixture, false);
            var (owner, reader) = CompensationOwners(fixture);
            faults.Armed = true;
            var committed = Assert.IsType<ProjectCreationCompensationOutcome.Committed>(await owner.CompensateAsync(original, fixture.Write));
            Assert.Equal(ProjectLifecycleCompletionIssue.OwnerReleaseUnconfirmed, committed.CompletionIssue);
            Assert.True(faults.Committed);
            Assert.True(faults.Triggered);
            Assert.Equal(0, fixture.Participant.Completed);
            Assert.IsType<ProjectCreationCompensationObservation.Committed>(await reader.ObserveAsync(original));
            Assert.IsType<ProjectDeletionCompletionObservation.AwaitingOwnerRelease>(await fixture.Services
                .GetRequiredService<IProjectDeletionCompletionQuery>().ObserveAsync(committed.Completion.Original,
                    Assert.Single(committed.Completion.Obligations)));
            Assert.Empty((await fixture.StateAsync()).Projects);
        }, faults);
    }

    [Fact]
    public async Task Compensation_lost_acknowledgement_recovers_after_provider_recreation_without_reexecuting() {
        CanDoItAllTestEnvironment? environment = null;
        LifecycleFixture? first = null;
        LifecycleFixture? restarted = null;
        Exception? primary = null;
        try {
            environment = CanDoItAllTestEnvironment.Create("project-compensation-recovery");
            var profile = environment.CreatePostgreSqlProfile("original-compensation-owner");
            var faults = new OwnerFaults { Failure = FailurePoint.CommitAcknowledgement, BlockRecovery = true };
            first = await LifecycleFixture.CreateAsync(faults, environment, profile);
            var original = await CreateCompensationIntentAsync(first, true);
            var (owner, _) = CompensationOwners(first);
            faults.Armed = true;
            var unknown = Assert.IsType<ProjectCreationCompensationOutcome.Unconfirmed>(await owner.CompensateAsync(original, first.Write));
            Assert.Same(original, unknown.Original);
            Assert.True(faults.Committed);
            Assert.True(faults.OwnerDisposed);
            Assert.True(faults.RecoveryBlocked);
            Assert.Equal(0, first.Participant.Completed);
            var ownedFirst = first;
            first = null;
            await ownedFirst.DisposeAsync();

            restarted = await LifecycleFixture.CreateAsync(new(), environment, profile);
            var (newOwner, reader) = CompensationOwners(restarted);
            var found = Assert.IsType<ProjectCreationCompensationObservation.Committed>(await reader.ObserveAsync(original));
            var state = await restarted.StateAsync();
            Assert.Empty(state.Projects);
            Assert.Single(state.Retired);
            Assert.Equal(1, state.Completions);
            var replay = Assert.IsType<ProjectCreationCompensationOutcome.Committed>(await newOwner.CompensateAsync(original, restarted.Write));
            Assert.True(replay.Replayed);
            Assert.Equal(ProjectDeletionCompletionCodec.Write(found.Completion), ProjectDeletionCompletionCodec.Write(replay.Completion));
            Assert.Equal(0, restarted.Participant.Prepared);
            Assert.Equal(0, restarted.Participant.Completed);
            state.RequireEqual(await restarted.StateAsync());
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            await DisposeAsync(primary, restarted, first, environment);
        }
    }

    [Fact]
    public Task Racing_compensation_of_the_same_original_waits_then_recovers_the_single_committed_completion() {
        var faults = new OwnerFaults { PauseReceipt = true };
        return WithFixtureAsync(async fixture => {
            var original = await CreateCompensationIntentAsync(fixture, false);
            var (owner, _) = CompensationOwners(fixture);
            faults.Armed = true;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var tasks = new List<Task>();
            Exception? primary = null;
            try {
                var first = owner.CompensateAsync(original, fixture.Write, deadline.Token);
                tasks.Add(first);
                await faults.ReceiptReached.Task.WaitAsync(deadline.Token);
                var second = owner.CompensateAsync(original, fixture.Write, deadline.Token);
                tasks.Add(second);
                var waiter = await faults.WaiterPid.Task.WaitAsync(deadline.Token);
                Assert.NotEqual(faults.OwnerPid, waiter);
                await RequireActualWaitAsync(fixture, waiter, faults.OwnerPid, deadline.Token);
                Assert.Equal(0, fixture.Participant.Completed);
                faults.ReleaseReceipt.TrySetResult();
                var all = Task.WhenAll(first, second);
                ProjectCreationCompensationOutcome[] outcomes;
                try {
                    outcomes = await all;
                } catch {
                    if (all.Exception is { } aggregate) {
                        throw aggregate;
                    }
                    throw;
                }
                var known = outcomes.Select(value => Assert.IsType<ProjectCreationCompensationOutcome.Committed>(value)).ToArray();
                Assert.Single(known, value => value.Replayed);
                Assert.Single(known, value => !value.Replayed);
                Assert.Equal(ProjectDeletionCompletionCodec.Write(known[0].Completion), ProjectDeletionCompletionCodec.Write(known[1].Completion));
                Assert.Equal(1, fixture.Participant.Prepared);
                Assert.Equal(1, fixture.Participant.Completed);
                var state = await fixture.StateAsync();
                Assert.Empty(state.Projects);
                Assert.Equal(1, state.Completions);
            } catch (Exception failure) {
                primary = failure;
                throw;
            } finally {
                faults.ReleaseReceipt.TrySetResult();
                await DrainOwnerTasksAsync(tasks, deadline, primary);
            }
        }, faults);
    }

    private static async Task<ProjectCreationCompensationRequest> CreateCompensationIntentAsync(LifecycleFixture fixture, bool reserved) {
        var projects = fixture.Services.GetRequiredService<ProjectsService>();
        var reservation = reserved ? await fixture.Services.GetRequiredService<ProjectWriteAdmissionService>()
            .ReserveCreationAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()) : null;
        var result = reservation is null
            ? await projects.CreateWithReceiptAsync(Guid.NewGuid(), new() { Name = "Original compensable creation" })
            : await projects.CreateWithReceiptAsync(reservation, new() { Name = "Original compensable creation" });
        Assert.True(result.IsSuccess);
        return ProjectCreationCompensationRequest.FromReceipt(result.Value);
    }

    private static (ProjectCreationCompensationOwner Owner, ProjectCreationCompensationReader Reader) CompensationOwners(LifecycleFixture fixture) {
        var factory = new OwnerFactory(new DbContextOptionsBuilder<ProjectsDbContext>(fixture.Services
            .GetRequiredService<DbContextOptions<ProjectsDbContext>>()).AddInterceptors(new CompensationCommands(fixture.Faults),
                new OwnerTransactions(fixture.Faults), new OwnerConnections(fixture.Faults)).Options);
        var reader = ActivatorUtilities.CreateInstance<ProjectCreationCompensationReader>(fixture.Services, factory);
        var completion = ActivatorUtilities.CreateInstance<ProjectDeletionCompletionOwner>(fixture.Services, factory);
        var owner = ActivatorUtilities.CreateInstance<ProjectCreationCompensationOwner>(fixture.Services, factory, reader, completion);
        return (owner, reader);
    }

    private sealed class CompensationCommands(OwnerFaults faults) : DbCommandInterceptor {
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            await BeforeAsync(command, eventData.Context, cancellationToken);
            return result;
        }
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            await BeforeAsync(command, eventData.Context, cancellationToken);
            return result;
        }
        private async Task BeforeAsync(DbCommand command, DbContext? context, CancellationToken cancellationToken) {
            if (faults.Committed && faults.BlockRecovery && command.Transaction is null &&
                    command.CommandText.Contains("JOIN \"Projects_DeletionCompletions\" AS c", StringComparison.Ordinal)) {
                faults.RecoveryBlocked = true;
                throw new IOException("Owned original compensation observation failure after actual COMMIT.");
            }
            if (!faults.Armed) {
                return;
            }
            if (faults.ReceiptReached.Task.IsCompleted && !ReferenceEquals(faults.OwnerContext, context) &&
                    command.CommandText.Contains("pg_advisory_lock(", StringComparison.Ordinal)) {
                faults.WaiterPid.TrySetResult(((NpgsqlConnection)command.Connection!).ProcessID);
            }
            if (!command.CommandText.Contains("INSERT INTO \"Projects_DeletionCompletions\"", StringComparison.Ordinal)) {
                return;
            }
            faults.OwnerContext = context;
            faults.OwnerPid = ((NpgsqlConnection)command.Connection!).ProcessID;
            faults.ReceiptReached.TrySetResult();
            if (faults.Failure == FailurePoint.ReceiptInsert) {
                faults.Triggered = true;
                throw new InvalidOperationException("Owned compensation completion insertion failure after effect staging.");
            }
            if (faults.PauseReceipt) {
                await faults.ReleaseReceipt.Task.WaitAsync(cancellationToken);
            }
        }
    }
}
