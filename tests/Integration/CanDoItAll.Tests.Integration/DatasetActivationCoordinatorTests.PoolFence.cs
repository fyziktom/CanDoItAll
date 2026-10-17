using System.Text.Json;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed partial class DatasetActivationCoordinatorTests {
    [Fact]
    public async Task An_older_compiled_pool_set_refuses_every_readiness_and_activation_entry_without_changing_SQL() {
        await using var fixture = await Fixture.CreateAsync();
        var binding = await fixture.Coordinator.EnsureReadyAsync(fixture.Profile);
        var added = AdditionalPool(fixture);
        await InstallPoolAsync(fixture, binding, added);
        var before = await InstallationSnapshotAsync(fixture);
        var active = await fixture.ReadFileAsync();

        await Assert.ThrowsAsync<FiniteOperationProtocolException>(() => fixture.Coordinator.EnsureReadyAsync(fixture.Profile));
        await Assert.ThrowsAsync<FiniteOperationProtocolException>(() => fixture.Coordinator.AttachAsync(fixture.Profile));
        await Assert.ThrowsAsync<FiniteOperationProtocolException>(() => fixture.Coordinator.ResumeAsync(fixture.Profile));
        Assert.Equal(active, await fixture.ReadFileAsync());
        Assert.Equal(before, await InstallationSnapshotAsync(fixture));
        await using var aliasEnvironment = CanDoItAllTestEnvironment.Create("dataset-pool-fence-alias");
        var alias = fixture.CoordinatorFor(aliasEnvironment);
        await Assert.ThrowsAsync<FiniteOperationProtocolException>(() => alias.AttachAsync(fixture.Profile));
        var prepared = await fixture.ReadFileAsync(aliasEnvironment);
        Assert.Equal(DatasetActivationFileState.Prepared, prepared.State);
        Assert.Equal(DatasetActivationKind.Attach, prepared.Plan.Kind);
        Assert.Equal(binding, prepared.Plan.Binding);
        await Assert.ThrowsAsync<FiniteOperationProtocolException>(() => alias.ResumeAsync(fixture.Profile));
        Assert.Equal(prepared, await fixture.ReadFileAsync(aliasEnvironment));
        Assert.Equal(before, await InstallationSnapshotAsync(fixture));

        await Assert.ThrowsAsync<FiniteOperationProtocolException>(() => fixture.Coordinator.ReactivateAsync(
            fixture.Profile, binding.Namespace, Guid.NewGuid()));

        Assert.Equal(before, await InstallationSnapshotAsync(fixture));
        Assert.Equal(binding, (await fixture.Lifecycle.ReadAsync())!.Binding);
        var refused = await fixture.ReadFileAsync();
        Assert.Equal(DatasetActivationFileState.Prepared, refused.State);
        Assert.Equal(DatasetActivationKind.Reactivate, refused.Plan.Kind);
        Assert.NotEqual(binding, refused.Plan.Binding);
        await using var read = fixture.Projects();
        Assert.Equal(2, await read.Set<FiniteOperationEpochRecord>().CountAsync(epoch => epoch.State == FiniteOperationEpochState.Open));
    }

    [Fact]
    public async Task Global_installation_audit_refuses_unknown_or_open_history_and_does_not_bootstrap_over_lost_lifecycle() {
        foreach (var fault in Enum.GetValues<InstallationFault>()) {
            await using var fixture = await Fixture.CreateAsync();
            var binding = await fixture.Coordinator.EnsureReadyAsync(fixture.Profile);
            await using (var context = fixture.Projects()) {
                switch (fault) {
                    case InstallationFault.MissingActivePool:
                        await context.Set<FiniteOperationEpochRecord>().ExecuteDeleteAsync();
                        await context.Set<FiniteOperationStateRecord>().ExecuteDeleteAsync();
                        break;
                    case InstallationFault.UnsupportedVersion:
                        await context.Set<FiniteOperationStateRecord>().ExecuteUpdateAsync(update => update.SetProperty(state => state.RetentionProtocolVersion, 2));
                        break;
                    case InstallationFault.UnknownHistoricalState:
                        AddHistory(context, AdditionalPool(fixture), includeState: true, open: false);
                        await context.SaveChangesAsync();
                        break;
                    case InstallationFault.UnknownHistoricalEpoch:
                        AddHistory(context, AdditionalPool(fixture), includeState: false, open: false);
                        await context.SaveChangesAsync();
                        break;
                    case InstallationFault.OrphanKnownEpoch:
                        AddHistory(context, fixture.Pool, includeState: false, open: false);
                        await context.SaveChangesAsync();
                        break;
                    case InstallationFault.OpenHistoricalEpoch:
                        AddHistory(context, fixture.Pool, includeState: true, open: true);
                        await context.SaveChangesAsync();
                        break;
                    case InstallationFault.MissingPreviousRetainedPrefix:
                        await context.Database.ExecuteSqlInterpolatedAsync($"""
                            UPDATE "Infrastructure_FiniteOperationEpochs" SET "Epoch" = 2
                            WHERE "NamespaceId" = {binding.Namespace.Value} AND "PoolId" = {fixture.Pool.Pool.Value};
                            UPDATE "Infrastructure_FiniteOperationState" SET "CurrentEpoch" = 2
                            WHERE "NamespaceId" = {binding.Namespace.Value} AND "PoolId" = {fixture.Pool.Pool.Value};
                            """);
                        break;
                    case InstallationFault.ClosedPreviousCurrentEpoch:
                        await context.Database.ExecuteSqlInterpolatedAsync($"""
                            UPDATE "Infrastructure_FiniteOperationEpochs" SET "State" = {(int)FiniteOperationEpochState.Closed},
                                "ClosedAtUtc" = clock_timestamp()
                            WHERE "NamespaceId" = {binding.Namespace.Value} AND "PoolId" = {fixture.Pool.Pool.Value}
                            """);
                        break;
                    case InstallationFault.LostLifecycleWithOpenHistory:
                        await context.Set<DatasetLifecycleRecord>().ExecuteDeleteAsync();
                        break;
                    case InstallationFault.LostLifecycleWithUnknownHistory:
                        await context.Set<FiniteOperationEpochRecord>().ExecuteDeleteAsync();
                        await context.Set<FiniteOperationStateRecord>().ExecuteDeleteAsync();
                        AddHistory(context, AdditionalPool(fixture), includeState: true, open: false);
                        await context.SaveChangesAsync();
                        await context.Set<DatasetLifecycleRecord>().ExecuteDeleteAsync();
                        break;
                }
            }
            var before = await InstallationSnapshotAsync(fixture);
            if (fault is InstallationFault.LostLifecycleWithOpenHistory or InstallationFault.LostLifecycleWithUnknownHistory) {
                await using var newHost = CanDoItAllTestEnvironment.Create("dataset-pool-fence-lost-lifecycle");
                await Assert.ThrowsAsync<FiniteOperationProtocolException>(() => fixture.CoordinatorFor(newHost).EnsureReadyAsync(fixture.Profile));
                Assert.Null(await fixture.Lifecycle.ReadAsync());
                Assert.Equal(DatasetActivationFileState.Prepared, (await fixture.ReadFileAsync(newHost)).State);
            } else {
                await Assert.ThrowsAsync<FiniteOperationProtocolException>(() => fixture.Coordinator.EnsureReadyAsync(fixture.Profile));
                Assert.Equal(binding, (await fixture.Lifecycle.ReadAsync())!.Binding);
                Assert.Equal(DatasetActivationFileState.Active, (await fixture.ReadFileAsync()).State);
            }
            Assert.Equal(before, await InstallationSnapshotAsync(fixture));
            if (fault is InstallationFault.MissingPreviousRetainedPrefix or InstallationFault.ClosedPreviousCurrentEpoch) {
                await Assert.ThrowsAsync<FiniteOperationProtocolException>(() => fixture.Coordinator.ReactivateAsync(
                    fixture.Profile, binding.Namespace, Guid.NewGuid()));
                Assert.Equal(before, await InstallationSnapshotAsync(fixture));
                Assert.Equal(binding, (await fixture.Lifecycle.ReadAsync())!.Binding);
                var prepared = await fixture.ReadFileAsync();
                Assert.Equal(DatasetActivationFileState.Prepared, prepared.State);
                Assert.Equal(DatasetActivationKind.Reactivate, prepared.Plan.Kind);
                Assert.Equal(binding, prepared.Plan.ExpectedPreviousSqlBinding);
                Assert.NotEqual(binding, prepared.Plan.Binding);
            }
        }

        await using var valid = await Fixture.CreateAsync();
        var current = await valid.Coordinator.EnsureReadyAsync(valid.Profile);
        var laterPool = AdditionalPool(valid);
        await InstallPoolAsync(valid, current, laterPool);
        await using (var historical = valid.Projects()) {
            AddHistory(historical, valid.Pool, includeState: true, open: false);
            await historical.SaveChangesAsync();
        }
        var complete = valid.CoordinatorFor(valid.Environment, registrations: [valid.Pool, laterPool]);
        Assert.Equal(current, await complete.EnsureReadyAsync(valid.Profile));
    }

    [Fact]
    public async Task Prepared_resume_of_an_already_committed_binding_never_reinitializes_missing_issued_metadata() {
        foreach (var fault in Enum.GetValues<ResumeMetadataFault>()) {
            await using var fixture = await Fixture.CreateAsync();
            var writes = 0;
            var interrupted = fixture.CoordinatorFor(fixture.Environment, new(new PhysicalFileSystemPathPolicyFactory(), stage => {
                if (stage == DurableFileWriteStage.BeforeCommit && ++writes == 2) {
                    throw new IOException("Synthetic completed SQL activation before file acknowledgement.");
                }
            }));
            await Assert.ThrowsAsync<IOException>(() => interrupted.EnsureReadyAsync(fixture.Profile));
            var prepared = await fixture.ReadFileAsync();
            Assert.Equal(DatasetActivationFileState.Prepared, prepared.State);
            await using (var context = fixture.Projects()) {
                if (fault == ResumeMetadataFault.MissingAll) {
                    await context.Set<FiniteOperationEpochRecord>().ExecuteDeleteAsync();
                    await context.Set<FiniteOperationStateRecord>().ExecuteDeleteAsync();
                } else if (fault == ResumeMetadataFault.MissingEpoch) {
                    await context.Set<FiniteOperationEpochRecord>().ExecuteDeleteAsync();
                }
            }
            if (fault == ResumeMetadataFault.ExtraPool) {
                await InstallPoolAsync(fixture, prepared.Plan.Binding, AdditionalPool(fixture));
            }
            var before = await InstallationSnapshotAsync(fixture);
            if (fault == ResumeMetadataFault.None) {
                Assert.Equal(prepared.Plan.Binding, await fixture.Coordinator.ResumeAsync(fixture.Profile));
                Assert.Equal(prepared.Plan.Binding, await fixture.Coordinator.ResumeAsync(fixture.Profile));
                Assert.Equal(DatasetActivationFileState.Active, (await fixture.ReadFileAsync()).State);
            } else {
                await Assert.ThrowsAsync<FiniteOperationProtocolException>(() => fixture.Coordinator.ResumeAsync(fixture.Profile));
                Assert.Equal(DatasetActivationFileState.Prepared, (await fixture.ReadFileAsync()).State);
            }
            Assert.Equal(prepared.Plan, (await fixture.ReadFileAsync()).Plan);
            Assert.Equal(before, await InstallationSnapshotAsync(fixture));
        }
    }

    [Fact]
    public async Task Readiness_waits_for_actual_installation_then_sees_its_complete_pool_set_and_reactivation_closes_every_pool() {
        await using var fixture = await Fixture.CreateAsync();
        var binding = await fixture.Coordinator.EnsureReadyAsync(fixture.Profile);
        var added = AdditionalPool(fixture);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readiness = Task.Run(async () => {
            await start.Task.WaitAsync(deadline.Token);
            return await fixture.Coordinator.EnsureReadyAsync(fixture.Profile, deadline.Token);
        });
        Exception? refusal = null;
        try {
            await using var owner = fixture.Projects();
            await using var transaction = await owner.Database.BeginTransactionAsync(deadline.Token);
            await owner.Database.ExecuteSqlRawAsync("LOCK TABLE \"Infrastructure_DatasetLifecycle\" IN EXCLUSIVE MODE", deadline.Token);
            var enrollment = CoordinatedDatabaseTransaction.ForProfile(fixture.Profile);
            using (enrollment.Enter(owner)) {
                await fixture.EpochsFor(enrollment).InitializeNewNamespaceEnlistedAsync(binding, added, deadline.Token);
            }
            var holder = ((NpgsqlConnection)owner.Database.GetDbConnection()).ProcessID;
            start.TrySetResult();
            await using var observation = fixture.Projects();
            while (!await observation.Database.SqlQuery<bool>($"""
                SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE {holder} = ANY(pg_blocking_pids(pid))) AS "Value"
                """).SingleAsync(deadline.Token)) {
                await Task.Delay(TimeSpan.FromMilliseconds(20), deadline.Token);
            }
            Assert.False(readiness.IsCompleted);
            await transaction.CommitAsync(deadline.Token);
            refusal = await Record.ExceptionAsync(() => readiness);
        } finally {
            start.TrySetCanceled(deadline.Token);
            _ = await Record.ExceptionAsync(() => readiness);
        }
        Assert.IsType<FiniteOperationProtocolException>(refusal);
        var complete = fixture.CoordinatorFor(fixture.Environment, registrations: [fixture.Pool, added]);
        Assert.Equal(binding, await complete.EnsureReadyAsync(fixture.Profile, deadline.Token));
        var replacement = await complete.ReactivateAsync(fixture.Profile, binding.Namespace, Guid.NewGuid(), deadline.Token);
        Assert.Equal(replacement, await complete.ResumeAsync(fixture.Profile, deadline.Token));
        await using var read = fixture.Projects();
        var epochs = await read.Set<FiniteOperationEpochRecord>().AsNoTracking().ToArrayAsync(deadline.Token);
        Assert.Equal(4, epochs.Length);
        Assert.All(epochs.Where(epoch => epoch.NamespaceId == binding.Namespace.Value), epoch => Assert.Equal(FiniteOperationEpochState.Closed, epoch.State));
        Assert.All(epochs.Where(epoch => epoch.NamespaceId == replacement.Namespace.Value), epoch => Assert.Equal(FiniteOperationEpochState.Open, epoch.State));
        Assert.Equal(2, epochs.Count(epoch => epoch.NamespaceId == binding.Namespace.Value));
        Assert.Equal(2, epochs.Count(epoch => epoch.NamespaceId == replacement.Namespace.Value));
        await Assert.ThrowsAsync<FiniteOperationProtocolException>(() => fixture.Coordinator.EnsureReadyAsync(fixture.Profile, deadline.Token));
    }

    private static FiniteOperationPoolRegistration AdditionalPool(Fixture fixture)
        => new(new(Guid.NewGuid()), 1, fixture.Pool.Timing, [new(new(Guid.NewGuid()), 1024)]);

    private static async Task InstallPoolAsync(Fixture fixture, DatasetActivationBinding binding, FiniteOperationPoolRegistration pool) {
        await using var owner = fixture.Projects();
        await using var transaction = await owner.Database.BeginTransactionAsync();
        var enrollment = CoordinatedDatabaseTransaction.ForProfile(fixture.Profile);
        using (enrollment.Enter(owner)) {
            await fixture.EpochsFor(enrollment).InitializeNewNamespaceEnlistedAsync(binding, pool, CancellationToken.None);
        }
        await transaction.CommitAsync();
    }

    private static void AddHistory(AppDbContext context, FiniteOperationPoolRegistration pool, bool includeState, bool open) {
        var historicalNamespace = Guid.NewGuid();
        if (includeState) {
            context.Add(new FiniteOperationStateRecord {
                NamespaceId = historicalNamespace, PoolId = pool.Pool.Value,
                RetentionProtocolVersion = pool.ProtocolVersion, CurrentEpoch = 1, RetiredThroughEpoch = 0
            });
        }
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        context.Add(new FiniteOperationEpochRecord {
            NamespaceId = historicalNamespace, PoolId = pool.Pool.Value, Epoch = 1,
            State = open ? FiniteOperationEpochState.Open : FiniteOperationEpochState.Closed,
            OpenedAtUtc = now, AdmissionClosesAtUtc = now.AddDays(1), ClosedAtUtc = open ? null : now.AddDays(2),
            MutationDeadlineMilliseconds = 30000, ReplayRetentionSeconds = 604800
        });
    }

    private static async Task<string> InstallationSnapshotAsync(Fixture fixture) {
        await using var context = fixture.Projects();
        return JsonSerializer.Serialize(new {
            Lifecycle = await context.Set<DatasetLifecycleRecord>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(),
            State = await context.Set<FiniteOperationStateRecord>().AsNoTracking().OrderBy(row => row.NamespaceId).ThenBy(row => row.PoolId).ToArrayAsync(),
            Epochs = await context.Set<FiniteOperationEpochRecord>().AsNoTracking().OrderBy(row => row.NamespaceId).ThenBy(row => row.PoolId)
                .ThenBy(row => row.Epoch).ToArrayAsync()
        });
    }

    private enum InstallationFault {
        MissingActivePool, UnsupportedVersion, UnknownHistoricalState, UnknownHistoricalEpoch, OrphanKnownEpoch,
        OpenHistoricalEpoch, MissingPreviousRetainedPrefix, ClosedPreviousCurrentEpoch,
        LostLifecycleWithOpenHistory, LostLifecycleWithUnknownHistory
    }

    private enum ResumeMetadataFault { None, MissingAll, MissingEpoch, ExtraPool }
}
