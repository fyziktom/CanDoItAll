using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit.Abstractions;
using static CanDoItAll.Tests.Integration.Processes.ProcessRecoverySelectionIndexFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessRecoverySelectionIndexMigrationTests(ITestOutputHelper output) {
    [Fact]
    public async Task Upgrade_restart_and_downgrade_preserve_original_rows_and_one_root_index_per_table() {
        await using var lease = CreateLease();
        await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
        await context.GetService<IMigrator>().MigrateAsync(Previous);
        await SeedAsync(context, 1, 16);
        var before = await RowsAsync(context);
        for (var restart = 0; restart < 2; restart++) {
            await using var reopened = new AppDbContext(lease.CreateAppDbContextOptions());
            await UpgradeAsync(reopened);
            Assert.False(reopened.Database.HasPendingModelChanges());
            Assert.Equal(before, await RowsAsync(reopened));
            foreach (var decision in new[] { false, true }) {
                Assert.True(await ValidAsync(reopened, Index(decision)));
                Assert.False(await ExistsAsync(reopened, OldIndex(decision)));
            }
        }
        await context.GetService<IMigrator>().MigrateAsync(Previous);
        Assert.Equal(before, await RowsAsync(context));
        foreach (var decision in new[] { false, true }) {
            Assert.True(await ValidAsync(context, OldIndex(decision)));
            Assert.False(await ExistsAsync(context, Index(decision)));
        }
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(false, 4)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    [InlineData(true, 4)]
    public async Task Interrupted_concurrent_commands_reenter_without_a_query_index_gap_or_row_changes(bool down, int after) {
        await using var lease = CreateLease();
        await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
        await context.GetService<IMigrator>().MigrateAsync(Previous);
        await SeedAsync(context, 1, 16);
        if (down) {
            await UpgradeAsync(context);
        }
        var before = await RowsAsync(context);
        var fault = new SchemaFault(after);
        var options = new DbContextOptionsBuilder<AppDbContext>(lease.CreateAppDbContextOptions()).AddInterceptors(fault).Options;
        await using (var interrupted = new AppDbContext(options)) {
            await Assert.ThrowsAsync<IOException>(() => interrupted.GetService<IMigrator>().MigrateAsync(down ? Previous : Migration(interrupted)));
        }
        Assert.Equal(after, fault.Completed);
        Assert.Equal(before, await RowsAsync(context));
        foreach (var decision in new[] { false, true }) {
            var remaining = await ExistsAsync(context, Index(decision)) ? Index(decision) : OldIndex(decision);
            Assert.True(await ValidAsync(context, remaining));
        }
        Assert.Equal(down ? Migration(context) : Previous, (await context.Database.GetAppliedMigrationsAsync()).Last());
        await context.GetService<IMigrator>().MigrateAsync(down ? Previous : Migration(context));
        Assert.Equal(before, await RowsAsync(context));
        foreach (var decision in new[] { false, true }) {
            Assert.True(await ValidAsync(context, down ? OldIndex(decision) : Index(decision)));
            Assert.False(await ExistsAsync(context, down ? Index(decision) : OldIndex(decision)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Conflicting_and_invalid_named_indexes_require_explicit_repair_and_are_never_dropped(bool decision) {
        await using var lease = CreateLease();
        await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
        await context.GetService<IMigrator>().MigrateAsync(Previous);
        await SeedAsync(context, 1, 16);
        var before = await RowsAsync(context);
        var table = Table(decision);
        var key = Key(decision);
        var name = Index(decision);
        foreach (var definition in new[] {
            $"(\"{key}\", \"RootRunId\")", $"(\"RootRunId\", \"{key}\" DESC)",
            $"(\"RootRunId\", \"{key}\") WHERE \"RootRunId\" IS NOT NULL",
            $"(\"RootRunId\", \"{key}\") INCLUDE (\"{(decision ? "Revision" : "Status")}\")"
        }) {
            await context.Database.ExecuteSqlRawAsync($"CREATE INDEX \"{name}\" ON {table} {definition}");
            await RequireRejectedAsync();
            await context.Database.ExecuteSqlRawAsync($"DROP INDEX \"{name}\"");
        }
        await context.Database.ExecuteSqlRawAsync($"CREATE UNIQUE INDEX \"{name}\" ON {table} (\"RootRunId\", \"{key}\")");
        await RequireRejectedAsync();
        await context.Database.ExecuteSqlRawAsync($"DROP INDEX \"{name}\"");
        Assert.Equal(PostgresErrorCodes.UniqueViolation, (await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlRawAsync($"CREATE UNIQUE INDEX CONCURRENTLY \"{name}\" ON {table} (\"RootRunId\")"))).SqlState);
        Assert.False(await ValidAsync(context, name));
        await RequireRejectedAsync();
        await context.Database.ExecuteSqlRawAsync($"DROP INDEX CONCURRENTLY \"{name}\"");
        var old = OldIndex(decision);
        await context.Database.ExecuteSqlRawAsync($"DROP INDEX \"{old}\"");
        await context.Database.ExecuteSqlRawAsync($"CREATE INDEX \"{old}\" ON {table} (\"{key}\")");
        var failure = await Assert.ThrowsAsync<PostgresException>(() => UpgradeAsync(context));
        Assert.Contains(old, failure.MessageText, StringComparison.Ordinal);
        Assert.True(await ExistsAsync(context, old));
        Assert.False(await ExistsAsync(context, name));
        Assert.Equal(before, await RowsAsync(context));

        async Task RequireRejectedAsync() {
            var failure = await Assert.ThrowsAsync<PostgresException>(() => UpgradeAsync(context));
            Assert.Contains("invalid or has a conflicting definition", failure.MessageText, StringComparison.Ordinal);
            Assert.Contains(name, failure.MessageText, StringComparison.Ordinal);
            Assert.Equal(Previous, (await context.Database.GetAppliedMigrationsAsync()).Last());
            Assert.True(await ExistsAsync(context, name));
            Assert.True(await ValidAsync(context, OldIndex(decision)));
            Assert.False(await ExistsAsync(context, Index(!decision)));
            Assert.Equal(before, await RowsAsync(context));
        }
    }

    [Fact]
    public async Task Concurrent_construction_waits_for_original_writer_without_blocking_other_writes() {
        await using var lease = CreateLease();
        await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
        await context.GetService<IMigrator>().MigrateAsync(Previous);
        await SeedAsync(context, 1, 16);
        await using var writer = new AppDbContext(lease.CreateAppDbContextOptions());
        await using var transaction = await writer.Database.BeginTransactionAsync();
        await writer.Database.ExecuteSqlRawAsync("""UPDATE process_runtime_states SET "PlanHash" = 'held-writer' WHERE "RunId" = md5('1')::uuid""");
        await using var migrating = new AppDbContext(lease.CreateAppDbContextOptions());
        var migration = UpgradeAsync(migrating);
        try {
            var waiting = Stopwatch.StartNew();
            while (!await context.Database.SqlQueryRaw<bool>("""
                SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname = current_database()
                    AND state = 'active' AND query LIKE 'CREATE INDEX CONCURRENTLY%'
                    AND wait_event_type = 'Lock') AS "Value"
                """).SingleAsync()) {
                Assert.False(migration.IsCompleted);
                Assert.True(waiting.Elapsed < TimeSpan.FromSeconds(10));
                await Task.Delay(25);
            }
            Assert.True(await ValidAsync(context, OldIndex(false)));
            context.Database.SetCommandTimeout(TimeSpan.FromSeconds(3));
            Assert.Equal(1, await context.Database.ExecuteSqlRawAsync("""UPDATE process_runtime_states SET "PlanHash" = 'concurrent-writer' WHERE "RunId" = md5('2')::uuid"""));
        } finally {
            await transaction.CommitAsync();
            await migration.WaitAsync(TimeSpan.FromSeconds(30));
        }
        Assert.True(await ValidAsync(context, RuntimeIndex));
        Assert.Equal(new[] { "concurrent-writer", "held-writer" }, await context.Database.SqlQueryRaw<string>("""
            SELECT "PlanHash" AS "Value" FROM process_runtime_states WHERE "PlanHash" <> 'index-fixture' ORDER BY "PlanHash"
            """).ToArrayAsync());
    }

    [Fact]
    public async Task Actual_recovery_queries_use_bounded_composite_plans_with_retained_size_and_write_cost() {
        await using var lease = CreateLease();
        await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
        await context.GetService<IMigrator>().MigrateAsync(Previous);
        var timer = Stopwatch.StartNew();
        for (var start = 1; start <= 50_000; start += 1000) {
            await SeedAsync(context, start, 1000);
        }
        var seedMs = timer.Elapsed.TotalMilliseconds;
        var before = await MeasureAsync(composite: false);
        timer.Restart();
        await UpgradeAsync(context);
        var constructionMs = timer.Elapsed.TotalMilliseconds;
        var after = await MeasureAsync(composite: true);
        output.WriteLine(JsonSerializer.Serialize(new { history = 50_000, hotRoot = 20_000, sparseRoot = 1000, seedMs, constructionMs, before, after,
            limitations = "Single isolated server, warm metadata, seven insertion batches per schema; not sustained 1/8/32 capacity or a production migration SLA." }));

        async Task<object> MeasureAsync(bool composite) {
            await context.Database.ExecuteSqlRawAsync("ANALYZE process_runtime_states; ANALYZE process_decision_heads");
            var measurements = new List<object>();
            foreach (var root in new[] { HotRoot, SparseRoot }) {
                var cursor = await context.Database.SqlQuery<Guid>($"""
                    SELECT "RunId" AS "Value" FROM process_runtime_states WHERE "RootRunId" = {root} ORDER BY "RunId" DESC OFFSET 8 LIMIT 1
                    """).SingleAsync();
                var probe = new QueryProbe();
                var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>().UseNpgsql(lease.ConnectionString).AddInterceptors(probe).Options;
                var factory = new ProcessCancellationRecoveryFixture.Factory(options);
                var runtime = await new EfProcessCancellationRuntimeReader(factory).NextAsync(new(root), new(cursor), default);
                var decision = await new EfProcessCancellationDecisionReader(factory).NextAsync(new(root), new(cursor), default);
                Assert.NotNull(runtime);
                Assert.Equal(runtime.Value.Value, decision!.DecisionId.Value);
                Assert.Equal(2, probe.Commands.Count);
                foreach (var query in probe.Commands) {
                    Assert.Equal(5, query.Timeout);
                    await context.Database.OpenConnectionAsync();
                    await using var command = context.Database.GetDbConnection().CreateCommand();
                    command.CommandText = "EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + query.Sql;
                    command.Parameters.AddRange(query.Parameters);
                    var plan = (string)(await command.ExecuteScalarAsync())!;
                    using var parsed = JsonDocument.Parse(plan);
                    var top = parsed.RootElement[0].GetProperty("Plan");
                    if (composite) {
                        Assert.Contains(query.Sql.Contains("process_runtime_states", StringComparison.Ordinal) ? RuntimeIndex : DecisionIndex,
                            plan, StringComparison.Ordinal);
                        Assert.DoesNotContain("\"Node Type\": \"Sort\"", plan, StringComparison.Ordinal);
                        Assert.InRange(top.GetProperty("Shared Hit Blocks").GetInt32() + top.GetProperty("Shared Read Blocks").GetInt32(), 1, 32);
                    }
                    measurements.Add(new { root, query.Sql, query.Timeout, plan });
                }
            }
            var writeMs = new List<double>();
            for (var batch = 0; batch < 7; batch++) {
                var start = 50_001 + (composite ? 7 * 256 : 0) + batch * 256;
                var watch = Stopwatch.StartNew();
                await SeedAsync(context, start, 256);
                writeMs.Add(watch.Elapsed.TotalMilliseconds);
            }
            var sizes = await context.Database.SqlQueryRaw<long>($"""
                SELECT pg_relation_size('"{(composite ? RuntimeIndex : OldIndex(false))}"'::regclass) AS "Value"
                UNION ALL SELECT pg_relation_size('"{(composite ? DecisionIndex : OldIndex(true))}"'::regclass) AS "Value"
                """).ToArrayAsync();
            return new { measurements, writeMs, medianBatchMs = writeMs.Order().ElementAt(3), indexBytes = sizes };
        }
    }
}
