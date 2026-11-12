using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using CanDoItAll.Tests.Support;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessCancellationRecoveryMigrationTests(ITestOutputHelper output) {
    private const string Previous = "20261021085340_AddAcceptedProcessStepRequirements";
    private const string Index = "IX_process_runtime_states_stopped_root";

    [Fact]
    public async Task Upgrade_restart_and_empty_downgrade_preserve_legacy_roots_without_startup_backfill() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("cancellation-recovery-migration");
        await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
        await context.GetService<IMigrator>().MigrateAsync(Previous);
        await SeedHistoryAsync(context, 64);
        var before = await RuntimeRowsAsync(context);
        for (var restart = 0; restart < 2; restart++) {
            await using var reopened = new AppDbContext(lease.CreateAppDbContextOptions());
            await reopened.GetService<IMigrator>().MigrateAsync(Migration(reopened));
            Assert.False(reopened.Database.HasPendingModelChanges());
            Assert.Empty(await reopened.Set<ProcessCancellationRecoveryEntity>().ToArrayAsync());
            Assert.Equal(0, (await reopened.Set<ProcessCancellationRecoverySeedEntity>().SingleAsync()).Revision);
            Assert.Equal(before, await RuntimeRowsAsync(reopened));
            Assert.True(await IndexValidAsync(reopened));
        }
        await context.GetService<IMigrator>().MigrateAsync(Previous);
        Assert.Equal(before, await RuntimeRowsAsync(context));
        Assert.False(await IndexExistsAsync(context));
    }

    [Fact]
    public async Task Constraints_and_guarded_downgrade_preserve_original_recovery_and_seed_evidence() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("cancellation-recovery-migration");
        await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
        await context.GetService<IMigrator>().MigrateAsync(Migration(context));
        await SeedHistoryAsync(context, 64);
        var position = ProcessCancellationRecoveryCodec.WritePosition(ProcessCancellationRecoveryPosition.Initial);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO process_cancellation_recovery ("RootRunId", "Origin", "LeaseGeneration", "ProgressRevision", "Cycle", "PositionJson", "Pending", "Completed")
            SELECT "RunId", 1, 0, 0, 0, {position}, 0, FALSE FROM process_runtime_states WHERE "Status" = 'Cancelled' LIMIT 1
            """);
        var before = await QueueRowsAsync(context);
        foreach (var invalid in new[] {
            """UPDATE process_cancellation_recovery SET "Origin" = 0""",
            """UPDATE process_cancellation_recovery SET "LeaseToken" = gen_random_uuid()""",
            """UPDATE process_cancellation_recovery SET "LeaseGeneration" = -1""",
            """UPDATE process_cancellation_recovery SET "ProgressRevision" = -1""",
            """UPDATE process_cancellation_recovery SET "Pending" = 99""",
            """UPDATE process_cancellation_recovery SET "Completed" = TRUE, "IntentJson" = '[]'""",
            """UPDATE process_cancellation_recovery SET "PositionJson" = repeat('x', 1025)""",
            """UPDATE process_cancellation_recovery SET "IntentJson" = repeat('x', 8193)"""
        }) {
            Assert.Equal(PostgresErrorCodes.CheckViolation,
                (await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(invalid))).SqlState);
            Assert.Equal(before, await QueueRowsAsync(context));
        }
        var originalRoots = await RuntimeRowsAsync(context);
        var deletion = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(
            """DELETE FROM process_runtime_states WHERE "RunId" IN (SELECT "RootRunId" FROM process_cancellation_recovery)"""));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, deletion.SqlState);
        Assert.Equal("FK_process_cancellation_recovery_process_runtime_states_RootRu~", deletion.ConstraintName);
        Assert.Equal(originalRoots, await RuntimeRowsAsync(context));
        Assert.Equal(before, await QueueRowsAsync(context));
        await AssertDowngradeDeniedAsync(context);
        Assert.Equal(before, await QueueRowsAsync(context));
        await context.Set<ProcessCancellationRecoveryEntity>().ExecuteDeleteAsync();
        await context.Database.ExecuteSqlRawAsync("""UPDATE process_cancellation_recovery_seed SET "Revision" = 1, "Completed" = TRUE""");
        await AssertDowngradeDeniedAsync(context);
        await context.Database.ExecuteSqlRawAsync("""UPDATE process_cancellation_recovery_seed SET "Revision" = 0, "Completed" = FALSE""");
        await context.GetService<IMigrator>().MigrateAsync(Previous);
    }

    [Fact]
    public async Task Interrupted_schema_reuses_only_a_valid_exact_owned_index_and_rejects_invalid_or_conflicting_indexes() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("cancellation-recovery-migration");
        await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
        await context.GetService<IMigrator>().MigrateAsync(Previous);
        await SeedHistoryAsync(context, 64);
        var options = new DbContextOptionsBuilder<AppDbContext>(lease.CreateAppDbContextOptions()).AddInterceptors(new SchemaFault()).Options;
        await using (var broken = new AppDbContext(options)) {
            await Assert.ThrowsAsync<IOException>(() => broken.GetService<IMigrator>().MigrateAsync(Migration(broken)));
        }
        Assert.True(await IndexValidAsync(context));
        Assert.Equal(Previous, (await context.Database.GetAppliedMigrationsAsync()).Last());
        await context.GetService<IMigrator>().MigrateAsync(Migration(context));
        await context.GetService<IMigrator>().MigrateAsync(Previous);
        await context.Database.ExecuteSqlRawAsync("""CREATE INDEX "IX_process_runtime_states_stopped_root" ON process_runtime_states ("Status")""");
        await AssertIndexRejectedAsync(context);
        await context.Database.ExecuteSqlRawAsync("""DROP INDEX CONCURRENTLY "IX_process_runtime_states_stopped_root" """);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, (await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(
            """CREATE UNIQUE INDEX CONCURRENTLY "IX_process_runtime_states_stopped_root" ON process_runtime_states ("Status")"""))).SqlState);
        Assert.False(await IndexValidAsync(context));
        await AssertIndexRejectedAsync(context);
        await context.Database.ExecuteSqlRawAsync("""DROP INDEX CONCURRENTLY "IX_process_runtime_states_stopped_root" """);
        await context.GetService<IMigrator>().MigrateAsync(Migration(context));
        Assert.True(await IndexValidAsync(context));
    }

    [Fact]
    public async Task Historical_selection_uses_bounded_indexes_and_records_construction_and_write_cost() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("cancellation-recovery-migration");
        await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
        await context.GetService<IMigrator>().MigrateAsync(Previous);
        var timer = Stopwatch.StartNew();
        await SeedHistoryAsync(context, 20_000);
        var historyWriteMs = timer.Elapsed.TotalMilliseconds;
        timer.Restart();
        await context.GetService<IMigrator>().MigrateAsync(Migration(context));
        var indexAndSchemaMs = timer.Elapsed.TotalMilliseconds;
        var position = ProcessCancellationRecoveryCodec.WritePosition(ProcessCancellationRecoveryPosition.Initial);
        var completedPosition = ProcessCancellationRecoveryCodec.WritePosition(new(ProcessCancellationRecoveryStage.Completed));
        timer.Restart();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO process_cancellation_recovery ("RootRunId", "Origin", "LeaseGeneration", "ProgressRevision", "Cycle", "PositionJson", "Pending", "Completed")
            SELECT "RunId", 1, 0, 0, 0,
                CASE WHEN row_number() OVER (ORDER BY "RunId") > 16 THEN {completedPosition} ELSE {position} END,
                0, row_number() OVER (ORDER BY "RunId") > 16
            FROM process_runtime_states WHERE "Status" = 'Cancelled'
            """);
        var queueWriteMs = timer.Elapsed.TotalMilliseconds;
        await context.Database.ExecuteSqlRawAsync("ANALYZE process_runtime_states; ANALYZE process_cancellation_recovery");
        var seedPlan = await PlanAsync(context, """
            SELECT "RunId" FROM process_runtime_states WHERE "RootRunId" = "RunId" AND "Status" IN ('CancelRequested', 'Cancelled')
            AND "RunId" > '00000000-0000-0000-0000-000000000001'::uuid ORDER BY "RunId" LIMIT 16
            """);
        var queuePlan = await PlanAsync(context, """
            SELECT * FROM process_cancellation_recovery WHERE "Origin" = 1 AND NOT "Completed" AND "DueAtUtc" <= statement_timestamp()
            ORDER BY "DueAtUtc", "RootRunId" LIMIT 1 FOR UPDATE SKIP LOCKED
            """);
        Assert.Contains(Index, seedPlan, StringComparison.Ordinal);
        Assert.Contains("IX_process_cancellation_recovery_due", queuePlan, StringComparison.Ordinal);
        output.WriteLine(JsonSerializer.Serialize(new { historyRoots = 20_000, stoppedRoots = 625, pendingQueueRoots = 16,
            historyWriteMs, indexAndSchemaMs, queueWriteMs, seedPlan, queuePlan }));
    }

    private static Task SeedHistoryAsync(AppDbContext context, int count) => context.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO process_runtime_states ("RunId", "RootRunId", "PlanId", "PlanHash", "Status", "UpdatedAtUtc", "ConcurrencyToken", "BlockedRecoveryActionsJson")
        SELECT md5(ordinal::text)::uuid, md5(ordinal::text)::uuid, gen_random_uuid(), 'migration-fixture',
            CASE WHEN ordinal % 32 = 0 THEN 'Cancelled' ELSE 'Completed' END, clock_timestamp(), gen_random_uuid(), '[]'
        FROM generate_series(1, {count}) AS ordinal
        """);

    private static string Migration(AppDbContext context)
        => Assert.Single(context.Database.GetMigrations(), item => item.EndsWith("_AddProcessCancellationRecovery", StringComparison.Ordinal));
    private static Task<string[]> RuntimeRowsAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>(
        """SELECT to_jsonb(r)::text AS "Value" FROM process_runtime_states r ORDER BY "RunId" """).ToArrayAsync();
    private static Task<string[]> QueueRowsAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>(
        """SELECT to_jsonb(r)::text AS "Value" FROM process_cancellation_recovery r ORDER BY "RootRunId" """).ToArrayAsync();
    private static Task<bool> IndexValidAsync(AppDbContext context) => context.Database.SqlQueryRaw<bool>(
        """SELECT indisvalid AS "Value" FROM pg_index WHERE indexrelid = '"IX_process_runtime_states_stopped_root"'::regclass""").SingleAsync();
    private static Task<bool> IndexExistsAsync(AppDbContext context) => context.Database.SqlQueryRaw<bool>(
        """SELECT to_regclass('"IX_process_runtime_states_stopped_root"') IS NOT NULL AS "Value" """).SingleAsync();

    private static async Task AssertDowngradeDeniedAsync(AppDbContext context) {
        var failure = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(Previous));
        Assert.Contains("retained Process cancellation recovery", failure.MessageText, StringComparison.Ordinal);
        Assert.True(await IndexValidAsync(context));
        Assert.Equal(Migration(context), (await context.Database.GetAppliedMigrationsAsync()).Last());
    }

    private static async Task AssertIndexRejectedAsync(AppDbContext context) {
        var failure = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(Migration(context)));
        Assert.Contains("index is invalid or has a conflicting definition", failure.MessageText, StringComparison.Ordinal);
        Assert.Equal(Previous, (await context.Database.GetAppliedMigrationsAsync()).Last());
        Assert.True(await IndexExistsAsync(context));
    }

    private static async Task<string> PlanAsync(AppDbContext context, string sql) {
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + sql;
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private sealed class SchemaFault : DbCommandInterceptor {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => command.CommandText.Contains("CREATE TABLE process_cancellation_recovery", StringComparison.Ordinal)
                ? throw new IOException("Injected failure after concurrent index construction and before transactional schema creation.")
                : ValueTask.FromResult(result);
    }
}
