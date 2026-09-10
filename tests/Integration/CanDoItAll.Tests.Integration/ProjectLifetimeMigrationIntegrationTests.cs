using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectLifetimeMigrationIntegrationTests {
    private const string StartingMigration = "20260830104752_AddProviderHistoryExternalReference";
    private const string PreviousMigration = "20260910163827_AddLlmChatDefinitionCreateReceipts";
    private static readonly DateTimeOffset SavedAt = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public async Task Fresh_schema_assigns_distinct_lifetimes_to_legacy_column_inserts_without_legacy_grant_eligibility() {
        await using var environment = CanDoItAllTestEnvironment.Create("project-lifetime-fresh");
        await using var provider = CreateProvider(environment.CreatePostgreSqlProfile("fresh"));
        await using var context = await provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await context.Database.MigrateAsync();
        await InsertLegacyProjectAsync(context, Guid.NewGuid(), "First");
        await InsertLegacyProjectAsync(context, Guid.NewGuid(), "Second");
        var projects = await context.Set<Project>().AsNoTracking().ToArrayAsync();
        Assert.Equal(2, projects.Select(project => project.LifetimeId).Distinct().Count());
        Assert.All(projects, project => {
            Assert.NotEqual(Guid.Empty, project.LifetimeId);
            Assert.False(project.LegacyAgentAccessBindingEligible);
        });
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Empty(await context.Set<ProjectRetirementRecord>().ToArrayAsync());
        Assert.False(await context.Database.SqlQueryRaw<bool>("""
            SELECT EXISTS (SELECT 1 FROM pg_constraint
                WHERE conrelid = '"Projects_ProjectRetirements"'::regclass AND contype = 'f') AS "Value"
            """).SingleAsync());
    }

    [Fact]
    public async Task Starting_schema_upgrade_preserves_business_rows_and_legacy_recoveries_across_two_restarts() {
        await using var environment = CanDoItAllTestEnvironment.Create("project-lifetime-upgrade");
        var profile = environment.CreatePostgreSqlProfile("upgrade");
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var recoveryId = Guid.NewGuid();
        string[] legacyProjects;
        string legacyRecovery;
        await using (var original = CreateProvider(profile)) {
            await using var context = await original.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            await context.GetService<IMigrator>().MigrateAsync(StartingMigration);
            await InsertLegacyProjectAsync(context, firstId, "Retained parent");
            await InsertLegacyProjectAsync(context, secondId, "Retained child");
            context.AddRange(new ProjectPhase { Id = Guid.NewGuid(), ProjectId = secondId, Name = "Retained phase", Goal = "Original goal" },
                new ProjectOptionSelection { Id = Guid.NewGuid(), ProjectId = secondId, OptionName = "Retained option", Notes = "Original notes" },
                new ProjectHierarchyLink { Id = Guid.NewGuid(), ParentProjectId = firstId, ChildProjectId = secondId, CreatedAtUtc = SavedAt });
            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "AgentFramework_ProjectAccessRevocations"
                    ("Id", "ProjectId", "Status", "CreatedAtUtc", "UpdatedAtUtc", "AttemptCount", "LastAttemptAtUtc", "CompletedAtUtc", "LastFailureCode")
                VALUES ({recoveryId}, {firstId}, {(int)AgentProjectStructureAccessRevocationStatus.Failed}, {SavedAt}, {SavedAt}, {4}, {SavedAt}, NULL, {"legacy-failure"})
                """);
            legacyProjects = await ReadProjectPayloadsAsync(context);
            legacyRecovery = await ReadLegacyRecoveryAsync(context);
        }
        Guid[]? lifetimes = null;
        for (var restart = 0; restart < 2; restart++) {
            await using var provider = CreateProvider(profile);
            await using var context = await provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            await context.Database.MigrateAsync();
            Assert.False(context.Database.HasPendingModelChanges());
            Assert.Equal(legacyProjects, await ReadProjectPayloadsAsync(context));
            Assert.Equal(legacyRecovery, await ReadLegacyRecoveryAsync(context));
            var projects = await context.Set<Project>().AsNoTracking().OrderBy(project => project.Id).ToArrayAsync();
            Assert.All(projects, project => Assert.True(project.LegacyAgentAccessBindingEligible));
            var currentLifetimes = projects.Select(project => project.LifetimeId).ToArray();
            Assert.All(currentLifetimes, lifetime => Assert.NotEqual(Guid.Empty, lifetime));
            Assert.Equal(2, currentLifetimes.Distinct().Count());
            if (lifetimes is not null) {
                Assert.Equal(lifetimes, currentLifetimes);
            }
            lifetimes = currentLifetimes;
            var recovery = await context.Set<AgentProjectStructureAccessRevocationRecord>().SingleAsync();
            Assert.Equal(recoveryId, recovery.Id);
            Assert.Null(recovery.DatabaseProfileId);
            Assert.Null(recovery.ProjectLifetimeId);
            Assert.Equal("Original goal", (await context.Set<ProjectPhase>().SingleAsync()).Goal);
            Assert.Equal("Original notes", (await context.Set<ProjectOptionSelection>().SingleAsync()).Notes);
            Assert.Equal((firstId, secondId), await context.Set<ProjectHierarchyLink>()
                .Select(link => new ValueTuple<Guid, Guid>(link.ParentProjectId, link.ChildProjectId)).SingleAsync());
        }
    }

    [Fact]
    public async Task Schema_without_retained_lifetime_evidence_can_downgrade_and_reupgrade_without_losing_projects() {
        await using var environment = CanDoItAllTestEnvironment.Create("project-lifetime-empty-down");
        await using var provider = CreateProvider(environment.CreatePostgreSqlProfile("downgrade"));
        await using var context = await provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await context.Database.MigrateAsync();
        await InsertLegacyProjectAsync(context, Guid.NewGuid(), "Preserved project");
        var before = await ReadProjectPayloadsAsync(context);
        var oldLifetime = await context.Set<Project>().Select(project => project.LifetimeId).SingleAsync();
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        Assert.Equal(before, await ReadProjectPayloadsAsync(context));
        await context.Database.MigrateAsync();
        Assert.Equal(before, await ReadProjectPayloadsAsync(context));
        Assert.NotEqual(oldLifetime, await context.Set<Project>().Select(project => project.LifetimeId).SingleAsync());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Retained_retirement_or_typed_recovery_blocks_schema_downgrade_atomically(bool retirement) {
        await using var environment = CanDoItAllTestEnvironment.Create("project-lifetime-retained-down");
        var profile = environment.CreatePostgreSqlProfile("retained");
        var projectId = Guid.NewGuid();
        var lifetimeId = Guid.NewGuid();
        await using (var provider = CreateProvider(profile)) {
            await using var context = await provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            await context.Database.MigrateAsync();
            if (retirement) {
                context.Add(new ProjectRetirementRecord { ProjectId = projectId, LifetimeId = lifetimeId, RetiredAtUtc = SavedAt });
            } else {
                context.Add(new AgentProjectStructureAccessRevocationRecord {
                    Id = Guid.NewGuid(), ProjectId = projectId, DatabaseProfileId = Guid.NewGuid(), ProjectLifetimeId = lifetimeId,
                    Status = AgentProjectStructureAccessRevocationStatus.Failed, CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt
                });
            }
            await context.SaveChangesAsync();
            var failure = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(PreviousMigration));
            Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);
        }
        await using var restarted = CreateProvider(profile);
        await using var readback = await restarted.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        Assert.False(readback.Database.HasPendingModelChanges());
        Assert.NotEqual(PreviousMigration, (await readback.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Equal(retirement ? 1 : 0, await readback.Set<ProjectRetirementRecord>().CountAsync());
        Assert.Equal(retirement ? 0 : 1, await readback.Set<AgentProjectStructureAccessRevocationRecord>().CountAsync());
    }

    [Fact]
    public async Task Physical_constraints_preserve_one_legacy_recovery_and_one_recovery_per_lifetime() {
        await using var environment = CanDoItAllTestEnvironment.Create("project-lifetime-constraints");
        await using var provider = CreateProvider(environment.CreatePostgreSqlProfile("constraints"));
        await using var context = await provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await context.Database.MigrateAsync();
        var projectId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var firstLifetime = Guid.NewGuid();
        await InsertRecoveryAsync(context, projectId, null, null);
        await InsertRecoveryAsync(context, projectId, profileId, firstLifetime);
        await InsertRecoveryAsync(context, projectId, profileId, Guid.NewGuid());
        var duplicateLegacy = await Assert.ThrowsAsync<PostgresException>(() => InsertRecoveryAsync(context, projectId, null, null));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicateLegacy.SqlState);
        var duplicateLifetime = await Assert.ThrowsAsync<PostgresException>(() => InsertRecoveryAsync(context, projectId, profileId, firstLifetime));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicateLifetime.SqlState);
        var malformed = await Assert.ThrowsAsync<PostgresException>(() => InsertRecoveryAsync(context, Guid.NewGuid(), null, Guid.NewGuid()));
        Assert.Equal(PostgresErrorCodes.CheckViolation, malformed.SqlState);
        Assert.Equal(3, await context.Set<AgentProjectStructureAccessRevocationRecord>().CountAsync());
    }

    private static ServiceProvider CreateProvider(TestDatabaseProfile profile) {
        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(services, TestApplicationBootstrap.BuildConfiguration(profile),
            new TestHostEnvironment(profile.EnvironmentRootPath, "CanDoItAll.Tests.Integration"));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static Task InsertLegacyProjectAsync(AppDbContext context, Guid projectId, string name)
        => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Projects_Projects"
                ("Id", "Name", "Slug", "Description", "Objective", "Status", "CurrentPhase", "TargetDateUtc", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ({projectId}, {name}, {projectId.ToString("N")}, {"Retained description"}, {"Retained objective"},
                {(int)ProjectStatus.OnHold}, {"Review"}, NULL, {SavedAt}, {SavedAt})
            """);

    private static Task InsertRecoveryAsync(AppDbContext context, Guid projectId, Guid? profileId, Guid? lifetimeId)
        => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AgentFramework_ProjectAccessRevocations"
                ("Id", "ProjectId", "DatabaseProfileId", "ProjectLifetimeId", "Status", "CreatedAtUtc", "UpdatedAtUtc", "AttemptCount")
            VALUES ({Guid.NewGuid()}, {projectId}, {profileId}, {lifetimeId},
                {(int)AgentProjectStructureAccessRevocationStatus.Pending}, {SavedAt}, {SavedAt}, {0})
            """);

    private static Task<string[]> ReadProjectPayloadsAsync(AppDbContext context)
        => context.Database.SqlQueryRaw<string>("""
            SELECT (to_jsonb(project) - 'LifetimeId' - 'LegacyAgentAccessBindingEligible')::text AS "Value"
            FROM "Projects_Projects" project ORDER BY project."Id"
            """).ToArrayAsync();

    private static Task<string> ReadLegacyRecoveryAsync(AppDbContext context)
        => context.Database.SqlQueryRaw<string>("""
            SELECT (to_jsonb(recovery) - 'DatabaseProfileId' - 'ProjectLifetimeId')::text AS "Value"
            FROM "AgentFramework_ProjectAccessRevocations" recovery
            """).SingleAsync();
}
