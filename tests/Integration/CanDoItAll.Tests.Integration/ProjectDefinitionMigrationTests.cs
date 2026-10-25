using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectDefinitionMigrationTests {
    private const string PreviousMigration = "20261009124700_AddProcessAuthoring";
    private static readonly DateTimeOffset SavedAt = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public async Task Upgrade_backfills_distinct_revisions_and_preserves_all_definition_rows_across_restarts() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("project-definition-upgrade");
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        string[] retainedPayloads;
        await using (var previous = new AppDbContext(lease.CreateAppDbContextOptions())) {
            await previous.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            await InsertWithoutRevisionAsync(previous, parentId, "Retained parent");
            await InsertWithoutRevisionAsync(previous, childId, "Retained child");
            previous.AddRange(
                new ProjectPhase { ProjectId = childId, Name = "Retained phase", Goal = "Retained goal", OrderIndex = 3 },
                new ProjectOptionSelection { ProjectId = childId, Category = ProjectOptionCategory.Language, OptionName = "Retained option", Notes = "Retained notes" },
                new ProjectHierarchyLink { ParentProjectId = parentId, ChildProjectId = childId, CreatedAtUtc = SavedAt });
            await previous.SaveChangesAsync();
            retainedPayloads = await ReadDefinitionPayloadsAsync(previous);
            Assert.Equal(5, retainedPayloads.Length);
        }

        Guid[]? originalRevisions = null;
        for (var restart = 0; restart < 2; restart++) {
            await using var upgraded = new AppDbContext(lease.CreateAppDbContextOptions());
            await upgraded.Database.MigrateAsync();
            Assert.False(upgraded.Database.HasPendingModelChanges());
            Assert.Equal(retainedPayloads, await ReadDefinitionPayloadsAsync(upgraded));
            var revisions = await upgraded.Set<Project>().AsNoTracking().OrderBy(project => project.Id)
                .Select(project => project.ConcurrencyToken).ToArrayAsync();
            Assert.All(revisions, revision => Assert.NotEqual(Guid.Empty, revision));
            Assert.Equal(2, revisions.Distinct().Count());
            if (originalRevisions is not null) {
                Assert.Equal(originalRevisions, revisions);
            }
            originalRevisions = revisions;
        }
    }

    [Fact]
    public async Task Fresh_schema_assigns_nonempty_distinct_revisions_to_inserts_omitting_the_revision_column() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("project-definition-fresh");
        await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
        await context.Database.MigrateAsync();
        await InsertWithoutRevisionAsync(context, Guid.NewGuid(), "First project");
        await InsertWithoutRevisionAsync(context, Guid.NewGuid(), "Second project");

        var revisions = await context.Set<Project>().Select(project => project.ConcurrencyToken).ToArrayAsync();

        Assert.All(revisions, revision => Assert.NotEqual(Guid.Empty, revision));
        Assert.Equal(2, revisions.Distinct().Count());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static Task InsertWithoutRevisionAsync(AppDbContext context, Guid projectId, string name)
        => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Projects_Projects"
                ("Id", "LifetimeId", "Name", "Slug", "Description", "Objective", "Status", "CurrentPhase",
                 "ExternalNamespace", "ExternalKey", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ({projectId}, {Guid.NewGuid()}, {name}, {projectId.ToString("N")}, {"Retained description"},
                {"Retained objective"}, {(int)ProjectStatus.OnHold}, {"Review"}, {"synthetic"},
                {projectId.ToString("N")}, {SavedAt}, {SavedAt})
            """);

    private static Task<string[]> ReadDefinitionPayloadsAsync(AppDbContext context)
        => context.Database.SqlQueryRaw<string>("""
            SELECT "Kind" || ':' || "Payload" AS "Value"
            FROM (
                SELECT 'project' AS "Kind", entity."Id", (to_jsonb(entity) - 'ConcurrencyToken')::text AS "Payload"
                FROM "Projects_Projects" entity
                UNION ALL
                SELECT 'phase', entity."Id", to_jsonb(entity)::text FROM "Projects_ProjectPhases" entity
                UNION ALL
                SELECT 'option', entity."Id", to_jsonb(entity)::text FROM "Projects_ProjectOptionSelections" entity
                UNION ALL
                SELECT 'hierarchy', entity."Id", to_jsonb(entity)::text FROM "Projects_ProjectHierarchyLinks" entity
            ) retained
            ORDER BY "Kind", "Id"
            """).ToArrayAsync();
}
