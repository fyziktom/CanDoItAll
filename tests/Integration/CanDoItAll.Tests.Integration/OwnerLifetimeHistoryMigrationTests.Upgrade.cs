using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CanDoItAll.Tests.Integration;

public sealed partial class OwnerLifetimeHistoryMigrationTests {
    [Fact]
    public async Task Empty_database_can_downgrade_and_reupgrade_the_complete_model() {
        await using var environment = CanDoItAllTestEnvironment.Create("owner-lifetime-empty-down");
        var profile = environment.CreatePostgreSqlProfile("empty");
        await using (var services = CreateProvider(profile)) {
            await using var database = await ContextAsync(services);
            await MigrateCurrentAsync(database);
            Assert.Empty(await SnapshotDataAsync(database));
            await database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            Assert.Equal(PreviousMigration, (await database.Database.GetAppliedMigrationsAsync()).Last());
            Assert.Empty(await SnapshotDataAsync(database, legacy: true));
        }
        await using var restarted = CreateProvider(profile);
        await using var readback = await ContextAsync(restarted);
        await MigrateCurrentAsync(readback);
        Assert.Empty(await SnapshotDataAsync(readback));
        Assert.Equal(161, readback.Model.GetEntityTypes().Count());
        Assert.False(readback.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Populated_159_upgrade_backfills_only_five_unambiguous_references_and_preserves_original_evidence() {
        await using var environment = CanDoItAllTestEnvironment.Create("owner-lifetime-upgrade");
        var profile = environment.CreatePostgreSqlProfile("legacy");
        string[] original;
        Guid projectId;
        Guid lifetimeId;
        await using (var services = CreateProvider(profile)) {
            await using var database = await ContextAsync(services);
            await database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            var project = await SeedLegacyAsync(database, includeUnambiguous: true);
            projectId = project.Id;
            lifetimeId = project.LifetimeId;
            original = await SnapshotDataAsync(database, legacy: true);
            Assert.Equal(PreviousMigration, (await database.Database.GetAppliedMigrationsAsync()).Last());
            await MigrateCurrentAsync(database);
            Assert.Equal(original, await SnapshotDataAsync(database, legacy: true));
            await AssertReferenceBackfillAsync(database, projectId, lifetimeId);
            await AssertNoNewAuthorityAsync(database);
        }
        for (var restart = 0; restart < 2; restart++) {
            await using var services = CreateProvider(profile);
            await using var database = await ContextAsync(services);
            await MigrateCurrentAsync(database);
            Assert.Equal(original, await SnapshotDataAsync(database, legacy: true));
            await AssertReferenceBackfillAsync(database, projectId, lifetimeId);
            await AssertNoNewAuthorityAsync(database);
            Assert.False(database.Database.HasPendingModelChanges());
        }
    }

    [Fact]
    public async Task Fresh_legacy_column_inserts_keep_null_reference_provenance_even_for_a_current_project() {
        await using var environment = CanDoItAllTestEnvironment.Create("owner-lifetime-fresh-reference");
        await using var services = CreateProvider(environment.CreatePostgreSqlProfile("fresh"));
        await using var database = await ContextAsync(services);
        await MigrateCurrentAsync(database);
        var project = new Project { Name = "New project", Slug = Guid.NewGuid().ToString("N") };
        database.Add(project);
        await database.SaveChangesAsync();
        await InsertLegacyReferencesAsync(database, project.Id);
        var references = await ReadReferencesAsync(database);
        Assert.Equal(5, references.Length);
        Assert.All(references, row => {
            Assert.Equal(project.Id, row.ProjectId);
            Assert.Null(row.LifetimeId);
        });
        await AssertNoNewAuthorityAsync(database);
    }

    [Fact]
    public async Task Unbound_legacy_database_can_downgrade_and_reupgrade_without_losing_later_human_edits() {
        await using var environment = CanDoItAllTestEnvironment.Create("owner-lifetime-safe-down");
        var profile = environment.CreatePostgreSqlProfile("unbound");
        string[] original;
        await using (var services = CreateProvider(profile)) {
            await using var database = await ContextAsync(services);
            await database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            await SeedLegacyAsync(database, includeUnambiguous: false);
            await MigrateCurrentAsync(database);
            Assert.All(await ReadReferencesAsync(database), row => Assert.Null(row.LifetimeId));
            await database.Database.ExecuteSqlRawAsync("""
                UPDATE "Workbench_WorkAssignments" SET "Notes" = 'Human Work edit after upgrade';
                UPDATE "CrmHr_ProjectPartyAssignments" SET "Notes" = 'Human participation edit after upgrade';
                UPDATE "Resources_ProjectResources" SET "Description" = 'Human resource edit after upgrade';
                UPDATE "TestLab_TestPlans" SET "CoverageGoal" = 'Human TestLab edit after upgrade';
                UPDATE "CrmHr_StaffingRequests" SET "Notes" = 'Human staffing edit after upgrade';
                """);
            original = await SnapshotDataAsync(database, legacy: true);
            await database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            Assert.Equal(PreviousMigration, (await database.Database.GetAppliedMigrationsAsync()).Last());
            Assert.Equal(original, await SnapshotDataAsync(database, legacy: true));
            Assert.False(await database.Database.SqlQueryRaw<bool>("""
                SELECT to_regclass('"Workbench_ProcessAssetContributions"') IS NOT NULL
                    OR to_regclass('"Workbench_WorkAssignmentHistory"') IS NOT NULL AS "Value"
                """).SingleAsync());
        }
        await using var restarted = CreateProvider(profile);
        await using var readback = await ContextAsync(restarted);
        Assert.Equal(original, await SnapshotDataAsync(readback, legacy: true));
        await MigrateCurrentAsync(readback);
        Assert.Equal(original, await SnapshotDataAsync(readback, legacy: true));
        Assert.All(await ReadReferencesAsync(readback), row => Assert.Null(row.LifetimeId));
        await AssertNoNewAuthorityAsync(readback);
        Assert.False(readback.Database.HasPendingModelChanges());
    }

    private static async Task AssertReferenceBackfillAsync(AppDbContext database, Guid projectId, Guid lifetimeId) {
        var rows = await ReadReferencesAsync(database);
        Assert.Equal(22, rows.Length);
        var bound = rows.Where(row => row.ProjectId == projectId).ToArray();
        Assert.Equal(5, bound.Length);
        Assert.Equal(5, bound.Select(row => row.Owner).Distinct().Count());
        Assert.All(bound, row => Assert.Equal(lifetimeId, row.LifetimeId));
        Assert.All(rows.Where(row => row.ProjectId != projectId), row => Assert.Null(row.LifetimeId));
    }

    private static Task<ReferenceState[]> ReadReferencesAsync(AppDbContext database) => database.Database.SqlQueryRaw<ReferenceState>("""
        SELECT 'resources' AS "Owner", "Id", "ProjectId", "ProjectLifetimeId" AS "LifetimeId" FROM "Resources_ProjectResources"
        UNION ALL SELECT 'testlab', "Id", "ProjectId", "ProjectLifetimeId" FROM "TestLab_TestPlans"
        UNION ALL SELECT 'work', "Id", "ProjectId", "ProjectLifetimeId" FROM "Workbench_WorkAssignments"
        UNION ALL SELECT 'participation', "Id", "ProjectId", "ProjectLifetimeId" FROM "CrmHr_ProjectPartyAssignments"
        UNION ALL SELECT 'staffing', "Id", "ProjectId", "ProjectLifetimeId" FROM "CrmHr_StaffingRequests"
        """).ToArrayAsync();

    private static async Task AssertNoNewAuthorityAsync(AppDbContext database) {
        Assert.Empty(await database.Database.SqlQueryRaw<string>("""
            SELECT 'move' AS "Value" FROM "CrmHr_ProjectPartyAssignmentMoveReceipts"
                WHERE "DatabaseProfileId" IS NOT NULL OR "SourceProjectLifetimeId" IS NOT NULL OR "TargetProjectLifetimeId" IS NOT NULL
            UNION ALL SELECT 'contribution' FROM "Workbench_WorkflowContributionReceipts"
                WHERE "DatabaseProfileId" IS NOT NULL OR "ProjectLifetimeId" IS NOT NULL OR "ImportedHistory" IS NOT NULL
            UNION ALL SELECT 'admission' FROM "Workbench_WorkflowAdmissions"
                WHERE "DatabaseProfileId" IS NOT NULL OR "ProjectLifetimeId" IS NOT NULL OR "ImportedHistory" IS NOT NULL
            UNION ALL SELECT 'output' FROM "AgentFramework_WorkflowStructureOutputs"
                WHERE "DatabaseProfileId" IS NOT NULL OR "ProjectId" IS NOT NULL OR "ProjectLifetimeId" IS NOT NULL
            UNION ALL SELECT 'retirement' FROM "Projects_ProjectRetirements" WHERE "ImportedHistory" IS NOT NULL
            UNION ALL SELECT 'reservation' FROM "Projects_ProjectCreationReservations" WHERE "ImportedHistory" IS NOT NULL
            """).ToArrayAsync());
        Assert.Empty(await database.Set<ProjectProcessAssetContributionRecord>().ToArrayAsync());
        Assert.Empty(await database.Set<ProjectWorkAssignmentHistoryRecord>().ToArrayAsync());
    }

    public sealed record ReferenceState(string Owner, Guid Id, Guid? ProjectId, Guid? LifetimeId);
}
