using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkflowProviderDisclosureMigrationTests {
    private const string PreviousMigration = "20260911094704_BindOwnerLifetimesAndRetainedHistory";
    private const string MigrationSuffix = "_BindWorkflowProviderDisclosureHistory";

    [Fact]
    public async Task Protocol_migration_preserves_existing_events_and_complete_schema_through_safe_downgrade_and_restart() {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-disclosure-migration");
        var profile = environment.CreatePostgreSqlProfile("legacy-events");
        string[] expectedEvents;
        string[] expectedSchema;
        await using (var services = CreateProvider(profile)) {
            await using var database = Context(services);
            await database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            database.Add(Event(WorkflowEventKind.ExecutorCompleted, "{\"legacy\":true}"));
            await database.SaveChangesAsync();
            expectedEvents = await SnapshotEventsAsync(database);
            expectedSchema = await SnapshotSchemaAsync(database);

            await database.GetService<IMigrator>().MigrateAsync(CurrentMigration(database));
            Assert.Equal(161, database.Model.GetEntityTypes().Count());
            Assert.False(database.Database.HasPendingModelChanges());
            Assert.Equal(CurrentMigration(database), (await database.Database.GetAppliedMigrationsAsync()).Last());
            Assert.Equal(expectedEvents, await SnapshotEventsAsync(database));
            Assert.Equal(expectedSchema, await SnapshotSchemaAsync(database));

            await database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            Assert.Equal(PreviousMigration, (await database.Database.GetAppliedMigrationsAsync()).Last());
            Assert.Equal(expectedEvents, await SnapshotEventsAsync(database));
            Assert.Equal(expectedSchema, await SnapshotSchemaAsync(database));
        }

        await using var restarted = CreateProvider(profile);
        await using var readback = Context(restarted);
        Assert.Equal(expectedEvents, await SnapshotEventsAsync(readback));
        await readback.GetService<IMigrator>().MigrateAsync(CurrentMigration(readback));
        Assert.Equal(expectedEvents, await SnapshotEventsAsync(readback));
        Assert.Equal(expectedSchema, await SnapshotSchemaAsync(readback));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retained_private_event_blocks_downgrade_without_parsing_its_payload_or_requiring_a_parent(bool malformed) {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-disclosure-down");
        var profile = environment.CreatePostgreSqlProfile("private-events");
        string[] expectedEvents;
        string[] expectedSchema;
        string[] expectedMigrations;
        await using (var services = CreateProvider(profile)) {
            await using var database = Context(services);
            await database.GetService<IMigrator>().MigrateAsync(CurrentMigration(database));
            database.Add(Event(WorkflowEventKind.ProviderReadEvidence, malformed ? "{malformed" : "{}"));
            await database.SaveChangesAsync();
            expectedEvents = await SnapshotEventsAsync(database);
            expectedSchema = await SnapshotSchemaAsync(database);
            expectedMigrations = (await database.Database.GetAppliedMigrationsAsync()).ToArray();

            var failure = await Assert.ThrowsAsync<PostgresException>(() =>
                database.GetService<IMigrator>().MigrateAsync(PreviousMigration));
            Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);
            Assert.Contains("private Workflow provider-disclosure evidence", failure.MessageText, StringComparison.Ordinal);
            Assert.Equal(expectedEvents, await SnapshotEventsAsync(database));
            Assert.Equal(expectedSchema, await SnapshotSchemaAsync(database));
            Assert.Equal(expectedMigrations, (await database.Database.GetAppliedMigrationsAsync()).ToArray());
        }

        await using var restarted = CreateProvider(profile);
        await using var readback = Context(restarted);
        Assert.Equal(expectedEvents, await SnapshotEventsAsync(readback));
        Assert.Equal(expectedSchema, await SnapshotSchemaAsync(readback));
        Assert.Equal(expectedMigrations, (await readback.Database.GetAppliedMigrationsAsync()).ToArray());
    }

    private static ServiceProvider CreateProvider(TestDatabaseProfile profile) {
        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(services, TestApplicationBootstrap.BuildConfiguration(profile),
            new TestHostEnvironment(profile.EnvironmentRootPath, "CanDoItAll.Tests.Integration"));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static AppDbContext Context(ServiceProvider services) =>
        services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();

    private static string CurrentMigration(AppDbContext database) =>
        Assert.Single(database.Database.GetMigrations(), migration => migration.EndsWith(MigrationSuffix, StringComparison.Ordinal));

    private static WorkflowEventRecordEntity Event(WorkflowEventKind kind, string payload) => new() {
        Id = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        Kind = kind,
        NodeId = "retained-node",
        Message = "Retained event",
        PayloadJson = payload,
        CreatedAtUtc = new(2026, 9, 11, 16, 0, 0, TimeSpan.Zero)
    };

    private static Task<string[]> SnapshotEventsAsync(AppDbContext database) => database.Database.SqlQueryRaw<string>("""
        SELECT to_jsonb(saved)::text AS "Value" FROM "AgentFramework_WorkflowEvents" saved ORDER BY "Id"
        """).ToArrayAsync();

    private static Task<string[]> SnapshotSchemaAsync(AppDbContext database) => database.Database.SqlQueryRaw<string>("""
        SELECT 'column:' || table_name || ':' || ordinal_position::text || ':' || column_name || ':' || data_type || ':' ||
            is_nullable || ':' || COALESCE(column_default, '') AS "Value"
        FROM information_schema.columns WHERE table_schema = 'public'
        UNION ALL SELECT 'constraint:' || relation.relname || ':' || constraint_row.conname || ':' || pg_get_constraintdef(constraint_row.oid)
            FROM pg_constraint constraint_row JOIN pg_class relation ON relation.oid = constraint_row.conrelid
            JOIN pg_namespace space ON space.oid = relation.relnamespace WHERE space.nspname = 'public'
        UNION ALL SELECT 'index:' || tablename || ':' || indexname || ':' || indexdef FROM pg_indexes WHERE schemaname = 'public'
        ORDER BY "Value"
        """).ToArrayAsync();
}
