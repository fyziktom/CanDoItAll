using Fixture = CanDoItAll.Tests.Integration.DatasetActivationTestFixture;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class DatasetActivationRestorationIntegrationTests {
    [Theory]
    [InlineData(RestorationInterruption.BeforePrepared)]
    [InlineData(RestorationInterruption.AfterPrepared)]
    [InlineData(RestorationInterruption.BeforeActive)]
    [InlineData(RestorationInterruption.AfterActive)]
    public async Task Previous_selection_restoration_survives_publication_interruption_without_changing_SQL(RestorationInterruption interruption) {
        await using var fixture = await Fixture.CreateAsync();
        var pending = await PrepareReactivationAsync(fixture);
        string before = await fixture.InstallationSnapshotAsync();
        int publication = 0;
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory(), stage => {
            if (stage == DurableFileWriteStage.BeforeCommit) {
                publication++;
            }
            if (interruption switch {
                RestorationInterruption.BeforePrepared => publication == 1 && stage == DurableFileWriteStage.BeforeCommit,
                RestorationInterruption.AfterPrepared => publication == 1 && stage == DurableFileWriteStage.Committed,
                RestorationInterruption.BeforeActive => publication == 2 && stage == DurableFileWriteStage.BeforeCommit,
                RestorationInterruption.AfterActive => publication == 2 && stage == DurableFileWriteStage.Committed,
                _ => false
            }) {
                throw new IOException("Synthetic restoration publication acknowledgement loss.");
            }
        });
        Assert.IsType<DatasetRestorationOutcome.Unconfirmed>(await RestoreAsync(fixture, pending, writer));
        var interrupted = await fixture.ReadFileAsync();
        Assert.Equal(before, await fixture.InstallationSnapshotAsync());
        if (interruption == RestorationInterruption.BeforePrepared) {
            Assert.Equal(pending, interrupted);
        } else {
            Assert.Equal(pending.Plan, interrupted.Restoration!.AbandonedPlan);
            Assert.Equal(interruption == RestorationInterruption.AfterActive ? DatasetActivationFileState.Active : DatasetActivationFileState.Prepared,
                interrupted.State);
        }
        var restored = Assert.IsType<DatasetRestorationOutcome.Restored>(await RestoreAsync(fixture, pending));
        var active = await fixture.ReadFileAsync();
        Assert.False(restored.CleanupUnconfirmed);
        Assert.Equal(pending.Plan.ExpectedPreviousSqlBinding, restored.Binding);
        Assert.Equal(pending.Plan, active.Restoration!.AbandonedPlan);
        Assert.Equal(DatasetActivationFileState.Active, active.State);
        Assert.Equal(restored.ReplacementPlanId, active.Plan.PlanId);
        if (interrupted.Restoration is not null) {
            Assert.Equal(interrupted.Plan.PlanId, active.Plan.PlanId);
        }
        Assert.Equal(restored, await RestoreAsync(fixture, pending));
        Assert.Equal(active, await fixture.ReadFileAsync());
        Assert.Equal(before, await fixture.InstallationSnapshotAsync());
    }

    [Theory]
    [InlineData(RestorationRefusal.AdvancedNamespace)]
    [InlineData(RestorationRefusal.ChangedMaintenance)]
    [InlineData(RestorationRefusal.PoolVersion)]
    [InlineData(RestorationRefusal.OtherPlan)]
    [InlineData(RestorationRefusal.OtherNamespace)]
    public async Task Previous_selection_restoration_refuses_different_SQL_protocol_or_retained_identity(RestorationRefusal fault) {
        await using var fixture = await Fixture.CreateAsync();
        var pending = await PrepareReactivationAsync(fixture);
        await using (var context = fixture.Projects()) {
            if (fault == RestorationRefusal.AdvancedNamespace) {
                await context.Set<DatasetLifecycleRecord>().ExecuteUpdateAsync(update => update.SetProperty(row => row.NamespaceId, Guid.NewGuid()));
            } else if (fault == RestorationRefusal.ChangedMaintenance) {
                await context.Set<DatasetLifecycleRecord>().ExecuteUpdateAsync(update => update.SetProperty(row => row.MaintenanceId, Guid.NewGuid()));
            } else if (fault == RestorationRefusal.PoolVersion) {
                await context.Set<FiniteOperationStateRecord>().ExecuteUpdateAsync(update => update.SetProperty(row => row.RetentionProtocolVersion, fixture.Pool.ProtocolVersion + 1));
            }
        }
        string before = await fixture.InstallationSnapshotAsync();
        var result = await Restorer(fixture).RestorePreviousAsync(
            fault == RestorationRefusal.OtherPlan ? Guid.NewGuid() : pending.Plan.PlanId,
            fault == RestorationRefusal.OtherNamespace ? new(Guid.NewGuid()) : pending.Plan.ExpectedPreviousSqlBinding!.Namespace,
            DatasetRestorationPrecondition.AllWriterHostsStopped);
        Assert.IsType<DatasetRestorationOutcome.Refused>(result);
        Assert.Equal(pending, await fixture.ReadFileAsync());
        Assert.Equal(before, await fixture.InstallationSnapshotAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Previous_selection_restoration_refuses_held_file_or_SQL_exclusion_then_succeeds_after_release(bool sqlHeld) {
        await using var fixture = await Fixture.CreateAsync();
        var pending = await PrepareReactivationAsync(fixture);
        string before = await fixture.InstallationSnapshotAsync();
        if (sqlHeld) {
            await using var context = fixture.Projects();
            await using var transaction = await context.Database.BeginTransactionAsync();
            await DatasetLifecycleStore.RequireActiveForMaintenanceAsync(context, pending.Plan.ExpectedPreviousSqlBinding!);
            Assert.IsType<DatasetRestorationOutcome.Refused>(await RestoreAsync(fixture, pending));
        } else {
            using var held = fixture.Files().Acquire(fixture.Profile.Profile.Id);
            Assert.IsType<DatasetRestorationOutcome.Refused>(await RestoreAsync(fixture, pending));
        }
        Assert.Equal(pending, await fixture.ReadFileAsync());
        Assert.IsType<DatasetRestorationOutcome.Restored>(await RestoreAsync(fixture, pending));
        Assert.Equal(before, await fixture.InstallationSnapshotAsync());
    }

    [Fact]
    public async Task Previous_selection_restoration_retains_Prepared_when_profile_recheck_fails_after_SQL_release() {
        await using var fixture = await Fixture.CreateAsync();
        var pending = await PrepareReactivationAsync(fixture);
        string before = await fixture.InstallationSnapshotAsync();
        int observations = 0;
        var coordinator = Restorer(fixture, requireProfile: _ => {
            if (++observations == 3) {
                throw new InvalidOperationException("Synthetic explicit profile replacement.");
            }
            return Task.CompletedTask;
        });
        Assert.IsType<DatasetRestorationOutcome.Unconfirmed>(await coordinator.RestorePreviousAsync(pending.Plan.PlanId,
            pending.Plan.ExpectedPreviousSqlBinding!.Namespace, DatasetRestorationPrecondition.AllWriterHostsStopped));
        var retained = await fixture.ReadFileAsync();
        Assert.Equal(DatasetActivationFileState.Prepared, retained.State);
        Assert.Equal(pending.Plan, retained.Restoration!.AbandonedPlan);
        Assert.Equal(before, await fixture.InstallationSnapshotAsync());
        Assert.IsType<DatasetRestorationOutcome.Restored>(await RestoreAsync(fixture, pending));
        Assert.Equal(retained.Plan, (await fixture.ReadFileAsync()).Plan);
    }

    private static async Task<DatasetActivationFileRecord> PrepareReactivationAsync(Fixture fixture) {
        var binding = await fixture.Coordinator.EnsureReadyAsync(fixture.Profile);
        var pending = new DatasetActivationPlan(Guid.NewGuid(), DatasetActivationKind.Reactivate,
            new(new(Guid.NewGuid()), Guid.NewGuid(), DatasetLifecycleRecord.CurrentSchemaVersion, DatasetGuardProtocol.SharedRows), binding);
        using var file = fixture.Files().Acquire(fixture.Profile.Profile.Id);
        await file.WritePreparedAsync(pending);
        return (await file.ReadAsync())!;
    }

    private static Task<DatasetRestorationOutcome> RestoreAsync(Fixture fixture, DatasetActivationFileRecord pending, DurableFileWriter? writer = null)
        => Restorer(fixture, writer).RestorePreviousAsync(pending.Plan.PlanId, pending.Plan.ExpectedPreviousSqlBinding!.Namespace,
            DatasetRestorationPrecondition.AllWriterHostsStopped);

    private static DatasetActivationRestorationCoordinator Restorer(Fixture fixture, DurableFileWriter? writer = null,
        Func<CancellationToken, Task>? requireProfile = null) {
        string connection = new NpgsqlConnectionStringBuilder(fixture.Profile.ConnectionString) { Pooling = false, Timeout = 5, CommandTimeout = 5 }.ConnectionString;
        var transactions = CoordinatedDatabaseTransaction.ForProfile(fixture.Profile);
        var lifecycle = new DbContextOptionsBuilder<DatasetLifecycleDbContext>().UseNpgsql(connection).Options;
        var metadata = new DbContextOptionsBuilder<FiniteOperationDbContext>().UseNpgsql(connection).Options;
        return new(fixture.Profile.Profile.Id, fixture.Files(writer), lifecycle, new(metadata, transactions), transactions, [fixture.Pool],
            requireProfile ?? (_ => Task.CompletedTask), NullLogger<DatasetActivationRestorationCoordinator>.Instance);
    }

    public enum RestorationInterruption { BeforePrepared, AfterPrepared, BeforeActive, AfterActive }
    public enum RestorationRefusal { AdvancedNamespace, ChangedMaintenance, PoolVersion, OtherPlan, OtherNamespace }
}
