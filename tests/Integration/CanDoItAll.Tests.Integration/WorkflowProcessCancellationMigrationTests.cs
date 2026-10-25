using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowProcessCancellationMigrationTests {
    private const string Previous = "20261022183302_IndexProcessCancellationSelections";
    private const string Current = "20261023024006_IndexWorkflowProcessCancellation";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Upgrade_resets_only_an_advanced_seed_once_and_keeps_valid_owned_indexes_on_restart(bool advanced) {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("workflow-process-cancellation-migration");
        await using var database = new AppDbContext(lease.CreateAppDbContextOptions());
        await database.GetService<IMigrator>().MigrateAsync(Previous);
        if (advanced) {
            await database.Database.ExecuteSqlRawAsync("""
                UPDATE process_cancellation_recovery_seed SET "LastRootRunId" = gen_random_uuid(), "Completed" = TRUE, "Revision" = 10
                """);
        }
        for (var restart = 0; restart < 2; restart++) {
            await database.GetService<IMigrator>().MigrateAsync(Current);
            Assert.False(database.Database.HasPendingModelChanges());
            var seed = await database.Set<ProcessCancellationRecoverySeedEntity>().AsNoTracking().SingleAsync();
            Assert.Null(seed.LastRootRunId);
            Assert.False(seed.Completed);
            Assert.Equal(advanced ? 11 : 0, seed.Revision);
            Assert.Equal(2, await IndexCountAsync(database));
        }
        await database.GetService<IMigrator>().MigrateAsync(Previous);
        Assert.Equal(0, await IndexCountAsync(database));
        Assert.Equal(advanced ? 11 : 0, (await database.Set<ProcessCancellationRecoverySeedEntity>().AsNoTracking().SingleAsync()).Revision);
    }

    [Fact]
    public async Task Conflicting_index_is_preserved_and_refuses_upgrade_before_advancing_the_seed() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("workflow-process-cancellation-migration");
        await using var database = new AppDbContext(lease.CreateAppDbContextOptions());
        await database.GetService<IMigrator>().MigrateAsync(Previous);
        await database.Database.ExecuteSqlRawAsync("""
            CREATE INDEX "IX_WorkflowRuns_ProcessRecovery" ON "AgentFramework_WorkflowRuns" ("RunId");
            UPDATE process_cancellation_recovery_seed SET "Completed" = TRUE, "Revision" = 10
            """);
        var failure = await Assert.ThrowsAsync<PostgresException>(() => database.GetService<IMigrator>().MigrateAsync(Current));
        Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);
        Assert.Equal(Previous, (await database.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Equal(10, (await database.Set<ProcessCancellationRecoverySeedEntity>().AsNoTracking().SingleAsync()).Revision);
        Assert.Equal(1, await IndexCountAsync(database));
        await database.Database.ExecuteSqlRawAsync("""DROP INDEX "IX_WorkflowRuns_ProcessRecovery" """);
        await database.GetService<IMigrator>().MigrateAsync(Current);
        Assert.Equal(2, await IndexCountAsync(database));
    }

    private static Task<int> IndexCountAsync(AppDbContext database) => database.Database.SqlQueryRaw<int>("""
        SELECT count(*)::int AS "Value" FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid
        WHERE c.relname IN ('IX_WorkflowRuns_ProcessRecovery', 'IX_WorkflowRequests_ProcessRecovery') AND i.indisvalid AND i.indisready
        """).SingleAsync();
}
