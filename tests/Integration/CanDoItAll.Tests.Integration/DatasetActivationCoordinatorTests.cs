using Fixture = CanDoItAll.Tests.Integration.DatasetActivationTestFixture;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration;

public sealed partial class DatasetActivationCoordinatorTests {
    [Fact]
    public async Task Bootstrap_restart_and_explicit_alias_preserve_SQL_identity_and_existing_epoch_deadline() {
        await using var fixture = await Fixture.CreateAsync();
        var initial = await fixture.Coordinator.EnsureReadyAsync(fixture.Profile);
        Assert.Equal(initial, await fixture.Coordinator.EnsureReadyAsync(fixture.Profile));
        await using var aliasEnvironment = CanDoItAllTestEnvironment.Create("dataset-activation-alias-host");
        var alias = fixture.CoordinatorFor(aliasEnvironment);
        await Assert.ThrowsAsync<InvalidOperationException>(() => alias.EnsureReadyAsync(fixture.Profile));
        Assert.Equal(initial, await alias.AttachAsync(fixture.Profile));
        Assert.Equal(initial, (await fixture.Lifecycle.ReadAsync())!.Binding);
        await using (var context = fixture.Projects()) {
            Assert.Single(await context.Set<FiniteOperationStateRecord>().ToArrayAsync());
            Assert.Single(await context.Set<FiniteOperationEpochRecord>().ToArrayAsync());
            await context.Database.ExecuteSqlRawAsync("""
                UPDATE "Infrastructure_FiniteOperationEpochs" SET "OpenedAtUtc" = clock_timestamp() - interval '2 hours',
                    "AdmissionClosesAtUtc" = clock_timestamp() - interval '1 hour'
                """);
        }

        Assert.Equal(initial, await alias.EnsureReadyAsync(fixture.Profile));
        var expired = await fixture.Epochs.ReadCurrentAsync(initial, fixture.Pool, CancellationToken.None);
        Assert.True(expired.AdmissionClosesAtUtc <= expired.ObservedAtUtc);
        Assert.Equal(FiniteOperationRotationResult.Rotated, await fixture.Epochs.RotateExpiredAsync(initial, fixture.Pool, CancellationToken.None));
        Assert.Equal(2, (await fixture.Epochs.ReadCurrentAsync(initial, fixture.Pool, CancellationToken.None)).Epoch);
    }

    [Fact]
    public async Task Prepared_plan_survives_SQL_initialization_rollback_and_resumes_the_same_identity() {
        await using var fixture = await Fixture.CreateAsync();
        await using (var context = fixture.Projects()) {
            await context.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION activation_test_reject_epoch() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Synthetic epoch initialization interruption'; END $$;
                CREATE TRIGGER activation_test_reject_epoch BEFORE INSERT ON "Infrastructure_FiniteOperationEpochs"
                FOR EACH ROW EXECUTE FUNCTION activation_test_reject_epoch();
                """);
        }

        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Coordinator.EnsureReadyAsync(fixture.Profile));

        var prepared = await fixture.ReadFileAsync();
        Assert.Equal(DatasetActivationFileState.Prepared, prepared.State);
        Assert.Null(await fixture.Lifecycle.ReadAsync());
        await using (var context = fixture.Projects()) {
            Assert.Empty(await context.Set<FiniteOperationEpochRecord>().ToArrayAsync());
            Assert.Empty(await context.Set<FiniteOperationStateRecord>().ToArrayAsync());
            await context.Database.ExecuteSqlRawAsync("DROP TRIGGER activation_test_reject_epoch ON \"Infrastructure_FiniteOperationEpochs\"");
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Coordinator.EnsureReadyAsync(fixture.Profile));

        var resumed = await fixture.CoordinatorFor(fixture.Environment).ResumeAsync(fixture.Profile);

        Assert.Equal(prepared.Plan.Binding, resumed);
        var active = await fixture.ReadFileAsync();
        Assert.Equal(prepared.Plan, active.Plan);
        Assert.Equal(DatasetActivationFileState.Active, active.State);
        Assert.Equal(resumed, (await fixture.Lifecycle.ReadAsync())!.Binding);
        await using var final = fixture.Projects();
        Assert.Single(await final.Set<FiniteOperationStateRecord>().ToArrayAsync());
        Assert.Single(await final.Set<FiniteOperationEpochRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task SQL_commit_before_file_completion_resumes_exactly_without_allocating_another_namespace() {
        await using var fixture = await Fixture.CreateAsync();
        var writes = 0;
        var interruptedFiles = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory(), stage => {
            if (stage == DurableFileWriteStage.BeforeCommit && ++writes == 2) {
                throw new IOException("Synthetic activation completion interruption.");
            }
        });
        var interrupted = fixture.CoordinatorFor(fixture.Environment, interruptedFiles);

        await Assert.ThrowsAsync<IOException>(() => interrupted.EnsureReadyAsync(fixture.Profile));

        var prepared = await fixture.ReadFileAsync();
        Assert.Equal(DatasetActivationFileState.Prepared, prepared.State);
        Assert.Equal(prepared.Plan.Binding, (await fixture.Lifecycle.ReadAsync())!.Binding);
        await using (var read = fixture.Projects()) {
            Assert.Single(await read.Set<FiniteOperationEpochRecord>().ToArrayAsync());
        }
        Assert.Equal(prepared.Plan.Binding, await fixture.Coordinator.ResumeAsync(fixture.Profile));
        Assert.Equal(prepared.Plan, (await fixture.ReadFileAsync()).Plan);
        Assert.Equal(DatasetActivationFileState.Active, (await fixture.ReadFileAsync()).State);
        Assert.Equal(prepared.Plan.Binding, await fixture.Coordinator.ResumeAsync(fixture.Profile));
    }

    [Fact]
    public async Task Separate_host_bootstrap_plans_have_one_SQL_winner_and_do_not_autoheal_the_losing_plan() {
        await using var fixture = await Fixture.CreateAsync();
        await using var secondEnvironment = CanDoItAllTestEnvironment.Create("dataset-activation-second-host");
        using var firstGate = new PreparedGate();
        using var secondGate = new PreparedGate();
        var first = fixture.CoordinatorFor(fixture.Environment,
            new(new PhysicalFileSystemPathPolicyFactory(), firstGate.Observe));
        var second = fixture.CoordinatorFor(secondEnvironment,
            new(new PhysicalFileSystemPathPolicyFactory(), secondGate.Observe));
        var firstRun = Task.Run(() => first.EnsureReadyAsync(fixture.Profile));
        var secondRun = Task.Run(() => second.EnsureReadyAsync(fixture.Profile));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        DatasetActivationBinding? winner = null;
        Exception? losingFailure = null;
        try {
            await Task.WhenAll(firstGate.Prepared.Task, secondGate.Prepared.Task).WaitAsync(deadline.Token);
            firstGate.Release.Set();
            winner = await firstRun.WaitAsync(deadline.Token);
        } finally {
            firstGate.Release.Set();
            secondGate.Release.Set();
            _ = await Record.ExceptionAsync(() => firstRun);
            losingFailure = await Record.ExceptionAsync(() => secondRun);
        }

        Assert.IsType<InvalidOperationException>(losingFailure);
        Assert.Equal(winner, (await fixture.Lifecycle.ReadAsync())!.Binding);
        var loser = await fixture.ReadFileAsync(secondEnvironment);
        Assert.Equal(DatasetActivationFileState.Prepared, loser.State);
        Assert.NotEqual(winner, loser.Plan.Binding);
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.EnsureReadyAsync(fixture.Profile));
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.ResumeAsync(fixture.Profile));
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.AttachAsync(fixture.Profile));
        Assert.Equal(loser.Plan, (await fixture.ReadFileAsync(secondEnvironment)).Plan);
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.AttachAfterSupersededBootstrapAsync(
            fixture.Profile, Guid.NewGuid(), winner!.Namespace));
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.AttachAfterSupersededBootstrapAsync(
            fixture.Profile, loser.Plan.PlanId, new(Guid.NewGuid())));
        Assert.Equal(loser.Plan, (await fixture.ReadFileAsync(secondEnvironment)).Plan);
        var interruptedRecovery = fixture.CoordinatorFor(secondEnvironment, new(new PhysicalFileSystemPathPolicyFactory(), stage => {
            if (stage == DurableFileWriteStage.BeforeCommit) {
                throw new IOException("Synthetic superseded bootstrap replacement interruption.");
            }
        }));
        await Assert.ThrowsAsync<IOException>(() => interruptedRecovery.AttachAfterSupersededBootstrapAsync(
            fixture.Profile, loser.Plan.PlanId, winner!.Namespace));
        Assert.Equal(loser.Plan, (await fixture.ReadFileAsync(secondEnvironment)).Plan);

        var attached = await second.AttachAfterSupersededBootstrapAsync(fixture.Profile, loser.Plan.PlanId, winner!.Namespace);

        Assert.Equal(winner, attached);
        var recovered = await fixture.ReadFileAsync(secondEnvironment);
        Assert.Equal(DatasetActivationFileState.Active, recovered.State);
        Assert.Equal(DatasetActivationKind.Attach, recovered.Plan.Kind);
        Assert.Equal(winner, recovered.Plan.Binding);
        Assert.Equal(winner, await second.ResumeAsync(fixture.Profile));
        await using var read = fixture.Projects();
        Assert.Single(await read.Set<FiniteOperationStateRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task Reactivation_mints_a_new_namespace_preserves_exact_resume_and_closes_previous_owner_epoch() {
        await using var fixture = await Fixture.CreateAsync();
        var original = await fixture.Coordinator.EnsureReadyAsync(fixture.Profile);
        var maintenanceId = Guid.NewGuid();

        var current = await fixture.Coordinator.ReactivateAsync(fixture.Profile, original.Namespace, maintenanceId);

        Assert.NotEqual(original.Namespace, current.Namespace);
        Assert.Equal(maintenanceId, current.MaintenanceId);
        Assert.Equal(current, await fixture.Coordinator.ResumeAsync(fixture.Profile));
        Assert.Equal(current, (await fixture.Lifecycle.ReadAsync())!.Binding);
        Assert.Equal(original, (await fixture.ReadFileAsync()).Plan.ExpectedPreviousSqlBinding);
        await using var read = fixture.Projects();
        Assert.Equal(FiniteOperationEpochState.Closed,
            (await read.Set<FiniteOperationEpochRecord>().SingleAsync(epoch => epoch.NamespaceId == original.Namespace.Value)).State);
        Assert.Equal(FiniteOperationEpochState.Open,
            (await read.Set<FiniteOperationEpochRecord>().SingleAsync(epoch => epoch.NamespaceId == current.Namespace.Value)).State);

        var subsequent = new DatasetActivationPlan(Guid.NewGuid(), DatasetActivationKind.Reactivate,
            new(new(Guid.NewGuid()), Guid.NewGuid(), DatasetLifecycleRecord.CurrentSchemaVersion, DatasetGuardProtocol.SharedRows), current);
        using (var file = fixture.Files().Acquire(fixture.Profile.Profile.Id)) {
            await file.WritePreparedAsync(subsequent);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Coordinator.AttachAfterSupersededBootstrapAsync(
            fixture.Profile, subsequent.PlanId, current.Namespace));
        Assert.Equal(subsequent, (await fixture.ReadFileAsync()).Plan);
    }

    private sealed class PreparedGate : IDisposable {
        private int committed;
        public TaskCompletionSource Prepared { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new();

        public void Observe(DurableFileWriteStage stage) {
            if (stage == DurableFileWriteStage.Committed && Interlocked.Increment(ref committed) == 1) {
                Prepared.TrySetResult();
                if (!Release.Wait(TimeSpan.FromSeconds(15))) {
                    throw new TimeoutException("The activation bootstrap test barrier was not released.");
                }
            }
        }

        public void Dispose() => Release.Dispose();
    }

}
