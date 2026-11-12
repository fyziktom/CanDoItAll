using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessAcceptedSourceMigrationTests {
    private const string PreviousMigration = "20261020071655_AddInheritedTaskPlanReceiptIdentity";
    private const string Migration = "20261020131404_AddAcceptedProcessSourceProjection";

    [Theory]
    [InlineData(LegacyState.Empty)]
    [InlineData(LegacyState.Prepared)]
    [InlineData(LegacyState.Accepted)]
    public async Task Upgrade_restart_and_legacy_downgrade_preserve_original_bytes_without_backfill(LegacyState state) {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("accepted-source-migration");
        await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        var row = ProcessPreparedLaunchCodec.ToEntity(ProcessPreparedLaunchFixture.Create());
        if (state != LegacyState.Empty) {
            DateTimeOffset? accepted = state == LegacyState.Accepted ? row.PreparedAtUtc : null;
            bool? execute = accepted is null ? null : true;
            var continuation = (state == LegacyState.Accepted ? ProcessLaunchContinuationState.Accepted : ProcessLaunchContinuationState.Prepared).ToString();
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO process_prepared_launches
                    ("Id", "RunId", "PlanId", "RequestFingerprint", "PreparationFingerprint", "PayloadJson", "PreparedAtUtc",
                    "AcceptedAtUtc", "Execute", "State", "ContinuationGeneration", "LinkDeliveryState")
                VALUES ({row.Id}, {row.RunId}, {row.PlanId}, {row.RequestFingerprint}, {row.PreparationFingerprint}, {row.PayloadJson},
                    {row.PreparedAtUtc}, {accepted}, {execute}, {continuation}, 0, {row.LinkDeliveryState.ToString()})
                """);
        }
        var before = await LegacyRowsAsync(context);
        for (var restart = 0; restart < 2; restart++) {
            await using var reopened = new AppDbContext(lease.CreateAppDbContextOptions());
            await reopened.GetService<IMigrator>().MigrateAsync(Migration);
            Assert.Equal(before, await LegacyRowsAsync(reopened));
            Assert.Equal(0, await SourceCountAsync(reopened));
            Assert.False(reopened.Database.HasPendingModelChanges());
            if (state != LegacyState.Empty) {
                var retained = await reopened.Set<ProcessPreparedLaunchEntity>().SingleAsync();
                Assert.Equal(ProcessAcceptedSourceVersion.LegacyPreparedLaunch, retained.AcceptedSourceVersion);
                Assert.Null(retained.AcceptedSourceHeaderJson);
                Assert.Null(retained.AcceptedSourceHeaderFingerprint);
                Assert.Equal(row.PreparationFingerprint, ProcessPreparedLaunchCodec.Read(retained).PreparationFingerprint);
            }
        }
        await using var scoped = new ProcessPersistenceDbContext(new DbContextOptionsBuilder<ProcessPersistenceDbContext>().UseNpgsql(lease.ConnectionString).Options);
        foreach (var type in new[] { typeof(ProcessPreparedLaunchEntity), typeof(ProcessAcceptedAssignmentSourceEntity) }) {
            var canonical = context.GetService<IDesignTimeModel>().Model.FindEntityType(type)!;
            var owned = scoped.GetService<IDesignTimeModel>().Model.FindEntityType(type)!;
            Assert.Equal(canonical.ToDebugString(MetadataDebugStringOptions.LongDefault), owned.ToDebugString(MetadataDebugStringOptions.LongDefault));
        }
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        Assert.Equal(before, await LegacyRowsAsync(context));
        Assert.Equal(PreviousMigration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        if (state == LegacyState.Prepared) {
            await context.GetService<IMigrator>().MigrateAsync();
            var retained = await scoped.PreparedLaunches.SingleAsync();
            var saved = ProcessPreparedLaunchCodec.Read(retained);
            Assert.True((await new EfProcessRuntimeUnitOfWork(scoped).CommitAsync(ProcessPreparedLaunchFixture.Commit(saved))).Succeeded);
            var accepted = await scoped.PreparedLaunches.AsNoTracking().SingleAsync();
            Assert.Equal(ProcessAcceptedSourceVersion.OriginalProjection, accepted.AcceptedSourceVersion);
            var runtime = await scoped.RuntimeStates.AsNoTracking().SingleAsync();
            var original = await EfProcessAcceptedSource.ReadAsync(scoped, runtime,
                saved.Preparation.InitialCommit.InitialAssignments!.Single().StepInstanceId, CancellationToken.None);
            Assert.Equal(saved.PreparationFingerprint, original.Header.PreparationFingerprint);
        }
    }

    [Fact]
    public async Task Constraints_reject_invalid_source_and_downgrade_retains_authoritative_evidence_until_parent_cleanup() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("accepted-source-migration");
        await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
        await context.GetService<IMigrator>().MigrateAsync(Migration);
        var row = ProcessPreparedLaunchCodec.ToEntity(ProcessPreparedLaunchFixture.Create());
        var source = ProcessAcceptedSourceCodec.Capture(ProcessPreparedLaunchCodec.Read(row));
        row.AcceptedAtUtc = row.PreparedAtUtc;
        row.AcceptedSourceVersion = ProcessAcceptedSourceVersion.OriginalProjection;
        row.AcceptedSourceHeaderJson = source.Header.PayloadJson;
        row.AcceptedSourceHeaderFingerprint = source.Header.Fingerprint;
        context.Add(row);
        await context.SaveChangesAsync();
        foreach (var item in source.Assignments) {
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO process_accepted_assignment_sources ("AdmissionId", "StepInstanceId", "PayloadJson", "Fingerprint")
                VALUES ({row.Id}, {item.Key.Value}, {item.Value.PayloadJson}, {item.Value.Fingerprint})
                """);
        }
        var before = await SourceRowsAsync(context);
        foreach (var invalid in new[] {
            "UPDATE process_prepared_launches SET \"AcceptedSourceVersion\" = 2",
            "UPDATE process_prepared_launches SET \"AcceptedSourceVersion\" = 0",
            "UPDATE process_prepared_launches SET \"AcceptedAtUtc\" = NULL",
            "UPDATE process_prepared_launches SET \"AcceptedSourceHeaderJson\" = NULL",
            "UPDATE process_prepared_launches SET \"AcceptedSourceHeaderFingerprint\" = 'invalid'",
            "UPDATE process_prepared_launches SET \"AcceptedSourceHeaderJson\" = repeat('x', 1048577)"
        }) {
            var failure = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(invalid));
            Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
            Assert.Equal(before, await SourceRowsAsync(context));
        }
        foreach (var invalid in new[] {
            "UPDATE process_accepted_assignment_sources SET \"StepInstanceId\" = '00000000-0000-0000-0000-000000000000'::uuid",
            "UPDATE process_accepted_assignment_sources SET \"Fingerprint\" = 'invalid'",
            "UPDATE process_accepted_assignment_sources SET \"PayloadJson\" = repeat('x', 1048577)"
        }) {
            var failure = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(invalid));
            Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
            Assert.Equal(before, await SourceRowsAsync(context));
        }
        var refused = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(PreviousMigration));
        Assert.Equal(PostgresErrorCodes.RaiseException, refused.SqlState);
        Assert.Contains("retained accepted Process source evidence", refused.MessageText, StringComparison.Ordinal);
        Assert.Equal(before, await SourceRowsAsync(context));
        Assert.Equal(Migration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        await using var reopened = new AppDbContext(lease.CreateAppDbContextOptions());
        await reopened.GetService<IMigrator>().MigrateAsync(Migration);
        Assert.Equal(before, await SourceRowsAsync(reopened));
        await reopened.Set<ProcessPreparedLaunchEntity>().Where(item => item.Id == row.Id).ExecuteDeleteAsync();
        Assert.Equal(0, await SourceCountAsync(reopened));
        await reopened.GetService<IMigrator>().MigrateAsync(PreviousMigration);
    }

    private static Task<string[]> LegacyRowsAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT (to_jsonb(r) - 'AcceptedSourceVersion' - 'AcceptedSourceHeaderJson' - 'AcceptedSourceHeaderFingerprint')::text AS "Value"
        FROM process_prepared_launches r ORDER BY r."Id"
        """).ToArrayAsync();

    private static Task<string[]> SourceRowsAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT to_jsonb(r)::text AS "Value" FROM process_prepared_launches r
        UNION ALL SELECT to_jsonb(s)::text FROM process_accepted_assignment_sources s ORDER BY "Value"
        """).ToArrayAsync();

    private static Task<int> SourceCountAsync(AppDbContext context)
        => context.Database.SqlQueryRaw<int>("""SELECT count(*)::int AS "Value" FROM process_accepted_assignment_sources""").SingleAsync();

    public enum LegacyState { Empty, Prepared, Accepted }
}
