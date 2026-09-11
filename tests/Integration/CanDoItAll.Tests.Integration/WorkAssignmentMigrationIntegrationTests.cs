using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkAssignmentMigrationIntegrationTests {
    private const string PreviousMigration = "20260910225242_AddWorkflowStructureReceipts";
    private const string CurrentMigration = "20260911000227_MoveWorkItemAssignments";
    private const string OwnerLifetimeMigration = "20260911094704_BindOwnerLifetimesAndRetainedHistory";
    private const string LatestMigration = "20260911194528_BindWorkflowProviderDisclosureHistory";
    private static readonly DateTimeOffset SavedAt = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    [Fact]
    public async Task Populated_upgrade_preserves_every_assignment_field_duplicate_orphan_and_other_owner_across_restarts() {
        await using var environment = CanDoItAllTestEnvironment.Create("work-assignment-upgrade");
        var profile = environment.CreatePostgreSqlProfile("original");
        RetainedState expected;
        await using (var services = CreateProvider(profile)) {
            await using var context = await services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            await SeedAsync(context);
            expected = await ReadStateAsync(context, ownerTable: false);
            Assert.Equal(3, expected.Work.Length);
            Assert.Single(expected.Participation);
            await context.GetService<IMigrator>().MigrateAsync(CurrentMigration);
            Assert.Equal(CurrentMigration, (await context.Database.GetAppliedMigrationsAsync()).Last());
            AssertState(expected, await ReadStateAsync(context, ownerTable: true));
            await context.Database.MigrateAsync();
            AssertState(expected, await ReadStateAsync(context, ownerTable: true));
            await AssertCurrentReferencesOnlyAsync(context);
        }

        for (var restart = 0; restart < 2; restart++) {
            await using var services = CreateProvider(profile);
            await using var context = await services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            await context.Database.MigrateAsync();
            AssertState(expected, await ReadStateAsync(context, ownerTable: true));
            Assert.False(context.Database.HasPendingModelChanges());
            Assert.Equal(161, context.Model.GetEntityTypes().Count());
            Assert.Equal(LatestMigration, (await context.Database.GetAppliedMigrationsAsync()).Last());
            await AssertCurrentReferencesOnlyAsync(context);
            await using var work = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
            Assert.Equal(3, await work.Set<ProjectWorkAssignmentRecord>().CountAsync());
            Assert.Equal(2, await work.Set<ProjectWorkAssignmentRecord>().CountAsync(row => row.NodeKey == "legacy-task"));
            Assert.Single(await work.Set<ProjectWorkAssignmentRecord>().Where(row => row.NodeKey == "removed-node").ToListAsync());
        }
    }

    [Fact]
    public async Task Populated_down_and_reupgrade_preserve_later_human_edits_and_existing_workflow_receipts() {
        await using var environment = CanDoItAllTestEnvironment.Create("work-assignment-down");
        var profile = environment.CreatePostgreSqlProfile("roundtrip");
        RetainedState expected;
        await using (var services = CreateProvider(profile)) {
            await using var context = await services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            await SeedAsync(context);
            await context.GetService<IMigrator>().MigrateAsync(CurrentMigration);
            Assert.Equal(CurrentMigration, (await context.Database.GetAppliedMigrationsAsync()).Last());
            context.ChangeTracker.Clear();
            var editedId = await context.Database.SqlQueryRaw<Guid>("""
                SELECT "Id" AS "Value" FROM "Workbench_WorkAssignments" ORDER BY "Id" LIMIT 1
                """).SingleAsync();
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Workbench_WorkAssignments" SET "Notes" = {"Human edit after cutover\nwith retained whitespace  "},
                    "AllocationPercent" = {19.123456789m} WHERE "Id" = {editedId}
                """);
            expected = await ReadStateAsync(context, ownerTable: true);
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            AssertState(expected, await ReadStateAsync(context, ownerTable: false));
            Assert.Equal(PreviousMigration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        }

        await using var restarted = CreateProvider(profile);
        await using var restored = await restarted.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        AssertState(expected, await ReadStateAsync(restored, ownerTable: false));
        await restored.GetService<IMigrator>().MigrateAsync(CurrentMigration);
        Assert.Equal(CurrentMigration, (await restored.Database.GetAppliedMigrationsAsync()).Last());
        AssertState(expected, await ReadStateAsync(restored, ownerTable: true));
        await restored.Database.MigrateAsync();
        AssertState(expected, await ReadStateAsync(restored, ownerTable: true));
        await AssertCurrentReferencesOnlyAsync(restored);
        Assert.False(restored.Database.HasPendingModelChanges());
        Assert.Equal(161, restored.Model.GetEntityTypes().Count());
        Assert.Equal(LatestMigration, (await restored.Database.GetAppliedMigrationsAsync()).Last());
    }

    [Fact]
    public async Task Current_reference_backfill_blocks_assignment_downgrade_and_retains_every_original_field() {
        await using var environment = CanDoItAllTestEnvironment.Create("work-assignment-current-down");
        var profile = environment.CreatePostgreSqlProfile("bound");
        RetainedState expected;
        string[] migrations;
        string[] currentMigrations;
        await using (var services = CreateProvider(profile)) {
            await using var context = await services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            await SeedAsync(context);
            await context.Database.MigrateAsync();
            await AssertCurrentReferencesOnlyAsync(context);
            expected = await ReadStateAsync(context, ownerTable: true);
            currentMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
            Assert.Equal(LatestMigration, currentMigrations.Last());
            await context.GetService<IMigrator>().MigrateAsync(OwnerLifetimeMigration);
            AssertState(expected, await ReadStateAsync(context, ownerTable: true));
            await AssertCurrentReferencesOnlyAsync(context);
            migrations = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
            Assert.Equal(OwnerLifetimeMigration, migrations.Last());
            var failure = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(PreviousMigration));
            Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);
            Assert.Contains("Cannot remove retained owner lifetime", failure.MessageText, StringComparison.Ordinal);
            AssertState(expected, await ReadStateAsync(context, ownerTable: true));
            Assert.Equal(migrations, (await context.Database.GetAppliedMigrationsAsync()).ToArray());
            await AssertCurrentReferencesOnlyAsync(context);
        }
        await using var restarted = CreateProvider(profile);
        await using var readback = await restarted.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        AssertState(expected, await ReadStateAsync(readback, ownerTable: true));
        Assert.Equal(migrations, (await readback.Database.GetAppliedMigrationsAsync()).ToArray());
        await AssertCurrentReferencesOnlyAsync(readback);
        await readback.Database.MigrateAsync();
        AssertState(expected, await ReadStateAsync(readback, ownerTable: true));
        Assert.Equal(currentMigrations, (await readback.Database.GetAppliedMigrationsAsync()).ToArray());
        await AssertCurrentReferencesOnlyAsync(readback);
    }

    [Fact]
    public async Task Conflicting_identity_blocks_down_before_any_data_constraint_or_history_change() {
        await using var environment = CanDoItAllTestEnvironment.Create("work-assignment-down-conflict");
        await using var services = CreateProvider(environment.CreatePostgreSqlProfile("conflict"));
        await using var context = await services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await context.Database.MigrateAsync();
        var id = Guid.NewGuid();
        context.Add(new ProjectWorkAssignmentRecord { Id = id, ProjectId = Guid.NewGuid(), PartyId = Guid.NewGuid(), Notes = "Work row" });
        context.Add(new ProjectPartyAssignment { Id = id, ProjectId = Guid.NewGuid(), PartyId = Guid.NewGuid(),
            AssignmentKind = ProjectPartyAssignmentKind.TeamMember, Notes = "Distinct participation row" });
        await context.SaveChangesAsync();
        var current = await ReadStateAsync(context, ownerTable: true);
        var currentMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        await context.GetService<IMigrator>().MigrateAsync(CurrentMigration);
        Assert.Equal(CurrentMigration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        var expected = await ReadStateAsync(context, ownerTable: true);
        var migrations = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        var failure = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(PreviousMigration));
        Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);
        Assert.Contains("already uses an assignment identity", failure.MessageText, StringComparison.Ordinal);
        AssertState(expected, await ReadStateAsync(context, ownerTable: true));
        Assert.Contains(CurrentMigration, await context.Database.GetAppliedMigrationsAsync());
        Assert.Equal(migrations, (await context.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Equal(1, await context.Database.SqlQueryRaw<int>("""
            SELECT count(*)::int AS "Value" FROM pg_constraint
            WHERE conname = 'CK_CrmHr_ProjectPartyAssignments_ParticipationRole'
            """).SingleAsync());
        await context.Database.MigrateAsync();
        AssertState(current, await ReadStateAsync(context, ownerTable: true));
        Assert.Equal(currentMigrations, (await context.Database.GetAppliedMigrationsAsync()).ToArray());
    }

    [Fact]
    public async Task Canonical_schema_rejects_legacy_work_writes_and_missing_work_affiliations() {
        await using var environment = CanDoItAllTestEnvironment.Create("work-assignment-constraints");
        await using var services = CreateProvider(environment.CreatePostgreSqlProfile("constraints"));
        await using var context = await services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await context.Database.MigrateAsync();
        var legacy = new ProjectPartyAssignment { ProjectId = Guid.NewGuid(), PartyId = Guid.NewGuid(),
            AssignmentKind = ProjectPartyAssignmentKind.WorkItemAssignee };
        context.Add(legacy);
        var legacyFailure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var check = Assert.IsType<PostgresException>(legacyFailure.InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, check.SqlState);
        Assert.Equal("CK_CrmHr_ProjectPartyAssignments_ParticipationRole", check.ConstraintName);
        context.Entry(legacy).State = EntityState.Detached;
        var invalid = new ProjectWorkAssignmentRecord { ProjectId = Guid.NewGuid(), PartyId = Guid.NewGuid(),
            PartyOrganizationAffiliationId = Guid.NewGuid() };
        context.Add(invalid);
        var ownerFailure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var foreignKey = Assert.IsType<PostgresException>(ownerFailure.InnerException);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, foreignKey.SqlState);
        Assert.StartsWith("FK_Workbench_WorkAssignments_", foreignKey.ConstraintName, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RetainedOwner.Work)]
    [InlineData(RetainedOwner.Contribution)]
    [InlineData(RetainedOwner.Admission)]
    [InlineData(RetainedOwner.Workflow)]
    [InlineData(RetainedOwner.Storage)]
    public async Task Maintenance_target_guard_retains_the_new_owner_rows_even_without_a_live_project(RetainedOwner owner) {
        await using var environment = CanDoItAllTestEnvironment.Create("work-assignment-target-residue");
        await using var services = CreateProvider(environment.CreatePostgreSqlProfile("target"));
        await using var scope = services.CreateAsyncScope();
        await using var context = await services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await context.Database.MigrateAsync();
        var id = Guid.NewGuid();
        object row = owner switch {
            RetainedOwner.Work => new ProjectWorkAssignmentRecord { Id = id, ProjectId = Guid.NewGuid(), PartyId = Guid.NewGuid() },
            RetainedOwner.Contribution => new ProjectWorkflowContributionRecord { RunId = id, OccurrencePath = "retained", ProjectId = Guid.NewGuid() },
            RetainedOwner.Admission => new ProjectWorkflowAdmissionRecord { IntentId = id, ProjectId = Guid.NewGuid(), NodeId = "deleted-node" },
            RetainedOwner.Workflow => new WorkflowStructureOutputRecord { RunId = id, OccurrencePath = "retained" },
            RetainedOwner.Storage => new StoragePlacementIntentRecord { Id = id, ProjectId = Guid.NewGuid(), StorageId = Guid.NewGuid() },
            _ => throw new ArgumentOutOfRangeException(nameof(owner))
        };
        context.Add(row);
        await context.SaveChangesAsync();
        var area = owner switch {
            RetainedOwner.Workflow => ProjectTransferTargetStateArea.AgentFramework,
            RetainedOwner.Storage => ProjectTransferTargetStateArea.Infrastructure,
            _ => ProjectTransferTargetStateArea.Workbench
        };
        var participant = scope.ServiceProvider.GetServices<IProjectTransferTargetStateParticipant>().Single(item => item.Area == area);
        Assert.Contains(row.GetType(), participant.EntityTypesToLock);
        var transfers = scope.ServiceProvider.GetRequiredService<DatabaseTransferOperationRunner>();
        var residues = await transfers.RunIndependentAsync(
            scope.ServiceProvider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile,
            (session, token) => transfers.InspectTargetAsync(session, participant.FindResiduesAsync, token));
        Assert.NotEmpty(residues);
    }

    private static ServiceProvider CreateProvider(TestDatabaseProfile profile) {
        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(services, TestApplicationBootstrap.BuildConfiguration(profile),
            new TestHostEnvironment(profile.EnvironmentRootPath, "CanDoItAll.Tests.Integration"));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static async Task SeedAsync(AppDbContext context) {
        var project = new Project { Name = "Preserved project", Slug = Guid.NewGuid().ToString("N"), CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt };
        var person = new Party { PartyType = PartyType.Person, DisplayName = "Original person", CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt };
        var organization = new Party { PartyType = PartyType.Organization, DisplayName = "Original organization", CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt };
        var affiliation = new PartyOrganizationAffiliation { PersonPartyId = person.Id, OrganizationPartyId = organization.Id,
            AffiliationKind = PartyOrganizationAffiliationKind.Employee, CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt };
        context.AddRange(project, person, organization, affiliation);
        context.Add(new ProjectObjectRecord { ProjectId = project.Id, NodeKey = "legacy-task", ObjectType = ProjectObjectType.WorkItem,
            Title = "Preserved native task", MetadataJson = "{\"unknown\":{\"key\":7}}", CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt });
        ProjectPartyAssignment[] assignments = [
            new ProjectPartyAssignment { ProjectId = project.Id, PartyId = person.Id, PartyOrganizationAffiliationId = affiliation.Id,
                AssignmentKind = ProjectPartyAssignmentKind.WorkItemAssignee, NodeKey = "legacy-task", PhaseName = " Original phase ",
                OpportunityId = Guid.NewGuid(), AllocationPercent = 12.123456789m, StartsAtUtc = SavedAt, EndsAtUtc = SavedAt.AddDays(2),
                IsPrimary = true, Source = " original source ", Notes = "Original notes\nwith trailing spaces  " },
            new ProjectPartyAssignment { ProjectId = project.Id, PartyId = person.Id, AssignmentKind = ProjectPartyAssignmentKind.WorkItemAssignee,
                NodeKey = "legacy-task", Notes = "Duplicate business key remains a separate assignment" },
            new ProjectPartyAssignment { ProjectId = Guid.NewGuid(), PartyId = Guid.NewGuid(), AssignmentKind = ProjectPartyAssignmentKind.WorkItemAssignee,
                NodeKey = "removed-node", PhaseName = "Historical", AllocationPercent = 99.999m, Notes = "Missing project and party remain observable" },
            new ProjectPartyAssignment { ProjectId = project.Id, PartyId = person.Id, AssignmentKind = ProjectPartyAssignmentKind.TeamMember,
                NodeKey = "legacy-task", Notes = "Participation stays in CRM" }
        ];
        await context.SaveChangesAsync();
        foreach (var assignment in assignments) {
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "CrmHr_ProjectPartyAssignments" ("Id", "ProjectId", "PartyId", "AssignmentKind", "NodeKey", "PhaseName",
                    "OpportunityId", "PartyOrganizationAffiliationId", "AllocationPercent", "StartsAtUtc", "EndsAtUtc", "IsPrimary", "Source", "Notes")
                VALUES ({assignment.Id}, {assignment.ProjectId}, {assignment.PartyId}, {assignment.AssignmentKind.ToString()}, {assignment.NodeKey},
                    {assignment.PhaseName}, {assignment.OpportunityId}, {assignment.PartyOrganizationAffiliationId}, {assignment.AllocationPercent},
                    {assignment.StartsAtUtc}, {assignment.EndsAtUtc}, {assignment.IsPrimary}, {assignment.Source}, {assignment.Notes})
                """);
        }
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AgentFramework_WorkflowStructureOutputs" ("RunId", "OccurrencePath", "Slot", "PlanJson", "ReceiptJson",
                "StoragePlacementIntentId", "AssetDispatchStarted", "IsComplete", "NextInspectionAtUtc")
            VALUES ({Guid.NewGuid()}, 'older-receipt', 0, {"{\"original\":true}"}, {"{\"retained\":true}"}, NULL, FALSE, FALSE, {SavedAt})
            """);
    }

    private static async Task<RetainedState> ReadStateAsync(AppDbContext context, bool ownerTable) {
        var work = await context.Database.SqlQueryRaw<string>(ownerTable
            ? "SELECT (to_jsonb(row) - 'ProjectLifetimeId')::text AS \"Value\" FROM \"Workbench_WorkAssignments\" row ORDER BY \"Value\""
            : "SELECT (to_jsonb(row) - ARRAY['AssignmentKind', 'ProjectLifetimeId'])::text AS \"Value\" FROM \"CrmHr_ProjectPartyAssignments\" row WHERE \"AssignmentKind\" = 'WorkItemAssignee' ORDER BY \"Value\"").ToArrayAsync();
        var participation = await context.Database.SqlQueryRaw<string>("""
            SELECT (to_jsonb(row) - 'ProjectLifetimeId')::text AS "Value" FROM "CrmHr_ProjectPartyAssignments" row
            WHERE "AssignmentKind" <> 'WorkItemAssignee' ORDER BY "Value"
            """).ToArrayAsync();
        var other = await context.Database.SqlQueryRaw<string>("""
            SELECT 'project:' || to_jsonb(row)::text AS "Value" FROM "Projects_Projects" row
            UNION ALL SELECT 'native:' || to_jsonb(row)::text AS "Value" FROM "Workbench_ProjectObjects" row
            UNION ALL SELECT 'party:' || to_jsonb(row)::text AS "Value" FROM "CrmHr_Parties" row
            UNION ALL SELECT 'affiliation:' || to_jsonb(row)::text AS "Value" FROM "CrmHr_PartyOrganizationAffiliations" row
            UNION ALL SELECT 'workflow:' || (to_jsonb(row) - ARRAY['DatabaseProfileId', 'ProjectId', 'ProjectLifetimeId'])::text AS "Value"
                FROM "AgentFramework_WorkflowStructureOutputs" row
            ORDER BY "Value"
            """).ToArrayAsync();
        return new(work, participation, other);
    }

    private static void AssertState(RetainedState expected, RetainedState actual) {
        Assert.Equal(expected.Work, actual.Work);
        Assert.Equal(expected.Participation, actual.Participation);
        Assert.Equal(expected.Other, actual.Other);
    }

    private static async Task AssertCurrentReferencesOnlyAsync(AppDbContext context) {
        var project = await context.Set<Project>().AsNoTracking().SingleAsync();
        var assignments = await context.Set<ProjectWorkAssignmentRecord>().AsNoTracking().ToArrayAsync();
        Assert.Equal(3, assignments.Length);
        var current = assignments.Where(row => row.ProjectId == project.Id).ToArray();
        Assert.Equal(2, current.Length);
        Assert.All(current, row => Assert.Equal(project.LifetimeId, row.ProjectLifetimeId));
        Assert.Null(Assert.Single(assignments, row => row.ProjectId != project.Id).ProjectLifetimeId);
        var participation = await context.Set<ProjectPartyAssignment>().AsNoTracking().SingleAsync();
        Assert.Equal(project.Id, participation.ProjectId);
        Assert.Equal(project.LifetimeId, participation.ProjectLifetimeId);
        var receipt = await context.Set<WorkflowStructureOutputRecord>().AsNoTracking().SingleAsync();
        Assert.Null(receipt.DatabaseProfileId);
        Assert.Null(receipt.ProjectId);
        Assert.Null(receipt.ProjectLifetimeId);
    }

    private sealed record RetainedState(string[] Work, string[] Participation, string[] Other);

    public enum RetainedOwner {
        Work,
        Contribution,
        Admission,
        Workflow,
        Storage
    }
}
