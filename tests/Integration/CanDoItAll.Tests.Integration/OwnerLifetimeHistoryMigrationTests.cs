using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed partial class OwnerLifetimeHistoryMigrationTests {
    private const string PreviousMigration = "20260911010404_AddSourceBoundExecutionAdmissions";
    private const string MigrationSuffix = "_BindOwnerLifetimesAndRetainedHistory";
    private static readonly DateTimeOffset SavedAt = new(2026, 9, 10, 11, 12, 13, TimeSpan.Zero);

    [Fact]
    public async Task Fresh_complete_schema_preserves_canonical_membership_and_retained_table_mappings() {
        await using var environment = CanDoItAllTestEnvironment.Create("owner-lifetime-schema");
        await using var services = CreateProvider(environment.CreatePostgreSqlProfile("fresh"));
        await using var database = await ContextAsync(services);
        await MigrateCurrentAsync(database);
        Assert.Equal(161, database.Model.GetEntityTypes().Count());
        Assert.False(database.Database.HasPendingModelChanges());
        Assert.Equal(CurrentMigration(database), (await database.Database.GetAppliedMigrationsAsync()).Last());
        foreach (var table in new[] { EvidenceTable.ProcessAssets, EvidenceTable.WorkHistory }) {
            Assert.True(await database.Database.SqlQuery<bool>($"""
                SELECT to_regclass({TableName(table)}) IS NOT NULL AS "Value"
                """).SingleAsync());
            Assert.False(await database.Database.SqlQuery<bool>($"""
                SELECT EXISTS (SELECT 1 FROM pg_constraint
                    WHERE conrelid = to_regclass({TableName(table)}) AND contype = 'f') AS "Value"
                """).SingleAsync());
        }
        var asset = database.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ProjectProcessAssetContributionRecord))!;
        Assert.Contains(asset.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(["NativeObjectId"]));
        Assert.Contains(asset.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(["StorageIntentId"]));
        Assert.Contains(asset.GetIndexes(), index => !index.IsUnique && index.Properties.Select(property => property.Name)
            .SequenceEqual(["DatabaseProfileId", "ProjectId", "ProjectLifetimeId"]));
        Assert.Contains(asset.GetIndexes(), index => !index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(["SourceExecutionRunId"]));
        var history = database.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ProjectWorkAssignmentHistoryRecord))!;
        Assert.False(history.FindProperty("ImportedHistory")!.IsNullable);
        Assert.Contains(history.GetIndexes(), index => !index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(["ProjectId", "NodeKey"]));
        Assert.Contains(history.GetIndexes(), index => !index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(["AssignmentId"]));
    }

    [Fact]
    public async Task Native_reservations_remain_unique_while_imported_reserved_history_keeps_its_original_state() {
        await using var environment = CanDoItAllTestEnvironment.Create("owner-lifetime-reservations");
        var profile = environment.CreatePostgreSqlProfile("target");
        string[] expected;
        await using (var services = CreateProvider(profile)) {
            await using var database = await ContextAsync(services);
            await MigrateCurrentAsync(database);
            var projectId = Guid.NewGuid();
            var native = Reservation(projectId);
            var imported = Reservation(projectId);
            imported.ImportedHistory = ImportChain();
            var secondHistory = Reservation(projectId);
            secondHistory.ImportedHistory = ImportChain();
            database.AddRange(native, imported, secondHistory);
            await database.SaveChangesAsync();
            expected = await SnapshotDataAsync(database);

            database.Add(Reservation(projectId));
            var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
            database.ChangeTracker.Clear();
            var clearMarker = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Projects_ProjectCreationReservations" SET "ImportedHistory" = NULL WHERE "Id" = {imported.Id}
                """));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, clearMarker.SqlState);
            var reusedLifetime = Reservation(Guid.NewGuid());
            reusedLifetime.LifetimeId = imported.LifetimeId;
            reusedLifetime.ImportedHistory = ImportChain();
            database.Add(reusedLifetime);
            var lifetimeConflict = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(lifetimeConflict.InnerException).SqlState);
            database.ChangeTracker.Clear();
            Assert.Equal(expected, await SnapshotDataAsync(database));
        }
        await using var restarted = CreateProvider(profile);
        await using var readback = await ContextAsync(restarted);
        Assert.Equal(expected, await SnapshotDataAsync(readback));
        var rows = await readback.Set<ProjectCreationReservationRecord>().AsNoTracking().ToArrayAsync();
        Assert.Equal(3, rows.Length);
        Assert.All(rows, row => Assert.Equal(ProjectCreationReservationState.Reserved, row.State));
        Assert.Single(rows, row => row.ImportedHistory is null);
        Assert.Equal(2, rows.Count(row => row.ImportedHistory is not null));
    }

    private static ServiceProvider CreateProvider(TestDatabaseProfile profile) {
        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(services, TestApplicationBootstrap.BuildConfiguration(profile),
            new TestHostEnvironment(profile.EnvironmentRootPath, "CanDoItAll.Tests.Integration"));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static Task<AppDbContext> ContextAsync(ServiceProvider services)
        => services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();

    private static string CurrentMigration(AppDbContext database)
        => Assert.Single(database.Database.GetMigrations(), migration => migration.EndsWith(MigrationSuffix, StringComparison.Ordinal));

    private static Task MigrateCurrentAsync(AppDbContext database)
        => database.GetService<IMigrator>().MigrateAsync(CurrentMigration(database));

    private static async Task AssertDownBlockedAsync(Func<AppDbContext, Task> seed, bool malformed = false) {
        await using var environment = CanDoItAllTestEnvironment.Create("owner-lifetime-down-guard");
        var profile = environment.CreatePostgreSqlProfile("retained");
        string[] expected;
        string[] schema;
        string[] migrations;
        await using (var services = CreateProvider(profile)) {
            await using var database = await ContextAsync(services);
            await MigrateCurrentAsync(database);
            await seed(database);
            Assert.Empty(await database.Set<Project>().ToArrayAsync());
            Assert.Empty(await database.Set<ProjectObjectRecord>().ToArrayAsync());
            expected = await SnapshotDataAsync(database);
            Assert.NotEmpty(expected);
            schema = await SnapshotSchemaAsync(database);
            migrations = (await database.Database.GetAppliedMigrationsAsync()).ToArray();
            var failure = await Assert.ThrowsAsync<PostgresException>(() => database.GetService<IMigrator>().MigrateAsync(PreviousMigration));
            if (malformed) {
                Assert.Contains(failure.SqlState, new[] { PostgresErrorCodes.RaiseException, PostgresErrorCodes.InvalidTextRepresentation });
            } else {
                Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);
            }
            Assert.Equal(expected, await SnapshotDataAsync(database));
            Assert.Equal(schema, await SnapshotSchemaAsync(database));
            Assert.Equal(migrations, (await database.Database.GetAppliedMigrationsAsync()).ToArray());
        }
        await using var restarted = CreateProvider(profile);
        await using var readback = await ContextAsync(restarted);
        Assert.Equal(expected, await SnapshotDataAsync(readback));
        Assert.Equal(schema, await SnapshotSchemaAsync(readback));
        Assert.Equal(migrations, (await readback.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.False(readback.Database.HasPendingModelChanges());
    }

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

    private static Task<string[]> SnapshotDataAsync(AppDbContext database, bool legacy = false, bool excludeMappedProjections = false) {
        var parts = Enum.GetValues<EvidenceTable>().Where(table => !legacy || table is not (EvidenceTable.ProcessAssets or EvidenceTable.WorkHistory))
            .Select(table => {
                var removed = legacy ? RemovedColumns(table) : [];
                if (excludeMappedProjections && table == EvidenceTable.WorkflowRuns) {
                    removed = [.. removed, "OriginProcessRunId"];
                }
                var projection = removed.Length == 0 ? "to_jsonb(saved)" :
                    $"(to_jsonb(saved) - ARRAY[{string.Join(", ", removed.Select(column => $"'{column}'"))}])";
                return $"SELECT '{table}:' || {projection}::text AS \"Value\" FROM {TableName(table)} saved";
            });
        var query = string.Join("\nUNION ALL\n", parts) + "\nORDER BY \"Value\"";
        return database.Database.SqlQueryRaw<string>(query).ToArrayAsync();
    }

    private static string[] RemovedColumns(EvidenceTable table) => table switch {
        EvidenceTable.Resources or EvidenceTable.TestPlans or EvidenceTable.WorkAssignments or EvidenceTable.Participation or EvidenceTable.Staffing => ["ProjectLifetimeId"],
        EvidenceTable.MoveReceipts => ["DatabaseProfileId", "SourceProjectLifetimeId", "TargetProjectLifetimeId"],
        EvidenceTable.Retirements or EvidenceTable.Reservations => ["ImportedHistory"],
        EvidenceTable.WorkflowContributions or EvidenceTable.WorkflowAdmissions => ["DatabaseProfileId", "ProjectLifetimeId", "ImportedHistory"],
        EvidenceTable.WorkflowOutputs => ["DatabaseProfileId", "ProjectId", "ProjectLifetimeId"],
        EvidenceTable.WorkflowRuns => ["OriginProcessAssignmentId"],
        _ => []
    };

    private static string TableName(EvidenceTable table) => table switch {
        EvidenceTable.Projects => "\"Projects_Projects\"",
        EvidenceTable.Resources => "\"Resources_ProjectResources\"",
        EvidenceTable.TestPlans => "\"TestLab_TestPlans\"",
        EvidenceTable.WorkAssignments => "\"Workbench_WorkAssignments\"",
        EvidenceTable.Participation => "\"CrmHr_ProjectPartyAssignments\"",
        EvidenceTable.Staffing => "\"CrmHr_StaffingRequests\"",
        EvidenceTable.MoveReceipts => "\"CrmHr_ProjectPartyAssignmentMoveReceipts\"",
        EvidenceTable.Retirements => "\"Projects_ProjectRetirements\"",
        EvidenceTable.Reservations => "\"Projects_ProjectCreationReservations\"",
        EvidenceTable.WorkflowContributions => "\"Workbench_WorkflowContributionReceipts\"",
        EvidenceTable.WorkflowAdmissions => "\"Workbench_WorkflowAdmissions\"",
        EvidenceTable.WorkflowOutputs => "\"AgentFramework_WorkflowStructureOutputs\"",
        EvidenceTable.WorkflowRuns => "\"AgentFramework_WorkflowRuns\"",
        EvidenceTable.WorkflowUsage => "\"AgentFramework_WorkflowUsageObservations\"",
        EvidenceTable.WorkflowLaunches => "\"AgentFramework_WorkflowLaunchIdempotency\"",
        EvidenceTable.SchedulerPlans => "\"SchedulerPlanner_Plans\"",
        EvidenceTable.SchedulerFires => "\"SchedulerPlanner_FireAdmissions\"",
        EvidenceTable.Cleanup => "\"Workbench_ProjectCrossModuleMutations\"",
        EvidenceTable.ProcessAssets => "\"Workbench_ProcessAssetContributions\"",
        EvidenceTable.WorkHistory => "\"Workbench_WorkAssignmentHistory\"",
        _ => throw new ArgumentOutOfRangeException(nameof(table))
    };

    public enum EvidenceTable {
        Projects, Resources, TestPlans, WorkAssignments, Participation, Staffing, MoveReceipts, Retirements, Reservations,
        WorkflowContributions, WorkflowAdmissions, WorkflowOutputs, WorkflowRuns, WorkflowUsage, WorkflowLaunches,
        SchedulerPlans, SchedulerFires, Cleanup, ProcessAssets, WorkHistory
    }
}
