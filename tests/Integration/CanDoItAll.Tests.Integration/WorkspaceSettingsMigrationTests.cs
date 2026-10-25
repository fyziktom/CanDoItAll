using CanDoItAll.Composition;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed partial class WorkspaceSettingsMigrationTests {
    private const string PreviousMigration = "20261016163615_AddProjectDefinitionOperationLifecycle";
    private static readonly DateTimeOffset SavedAt = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public async Task Empty_upgrade_and_repeated_readiness_leave_the_head_empty_with_matching_owner_and_canonical_models() {
        await using var fixture = await Fixture.CreateAsync();
        await using (var upgraded = fixture.Canonical()) {
            await upgraded.Database.MigrateAsync();
            Assert.Empty(await upgraded.Set<WorkspaceSettings>().ToArrayAsync());
            Assert.False(upgraded.Database.HasPendingModelChanges());
            await using var owner = fixture.Owner();
            var canonicalEntity = upgraded.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(WorkspaceSettings))!;
            var ownerEntity = owner.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(WorkspaceSettings))!;
            Assert.Equal(canonicalEntity.GetTableName(), ownerEntity.GetTableName());
            Assert.Equal(canonicalEntity.FindPrimaryKey()!.Properties.Select(property => property.Name),
                ownerEntity.FindPrimaryKey()!.Properties.Select(property => property.Name));
            foreach (var entity in new[] { canonicalEntity, ownerEntity }) {
                var revision = entity.FindProperty(nameof(WorkspaceSettings.ConcurrencyToken))!;
                Assert.Equal(typeof(Guid), revision.ClrType);
                Assert.False(revision.IsNullable);
                Assert.True(revision.IsConcurrencyToken);
                Assert.Equal("gen_random_uuid()", revision.GetDefaultValueSql());
                var current = entity.FindProperty(nameof(WorkspaceSettings.IsCurrent))!;
                Assert.Equal(typeof(bool), current.ClrType);
                Assert.False(current.IsNullable);
                var index = Assert.Single(entity.GetIndexes(), item => item.Properties.Count == 1 &&
                    item.Properties[0].Name == nameof(WorkspaceSettings.IsCurrent));
                Assert.True(index.IsUnique);
                Assert.Equal("\"IsCurrent\"", index.GetFilter());
                var policy = entity.FindProperty(nameof(WorkspaceSettings.DecisionDataPolicyJson))!;
                Assert.Equal(typeof(string), policy.ClrType);
                Assert.True(policy.IsNullable);
                Assert.Equal("TEXT", policy.GetColumnType());
                Assert.Null(policy.GetDefaultValueSql());
                Assert.Equal("\"DecisionDataPolicyJson\" IS NULL OR octet_length(\"DecisionDataPolicyJson\") BETWEEN 1 AND 1024",
                    Assert.Single(entity.GetCheckConstraints(), check => check.Name == "CK_Workspace_Settings_DecisionDataPolicyJson").Sql);
            }
        }
        var binding = await fixture.Coordinator.EnsureReadyAsync(fixture.Profile);
        Assert.Equal(binding, await fixture.Coordinator.EnsureReadyAsync(fixture.Profile));
        await using var restarted = fixture.Canonical();
        await restarted.Database.MigrateAsync();
        Assert.Empty(await restarted.Set<WorkspaceSettings>().ToArrayAsync());
    }

    [Fact]
    public async Task Retained_single_multiple_and_tied_rows_keep_all_values_and_choose_one_stable_original_head() {
        var first = new Guid("00000000-0000-0000-0000-000000000001");
        var second = new Guid("00000000-0000-0000-0000-000000000002");
        var third = new Guid("00000000-0000-0000-0000-000000000003");
        var cases = new[] {
            new RetainedCase([new(second, SavedAt, null)], second),
            new RetainedCase([new(first, SavedAt, null), new(second, SavedAt.AddSeconds(1), Guid.NewGuid())], second),
            new RetainedCase([new(third, SavedAt.AddSeconds(1), Guid.NewGuid()), new(second, SavedAt, null),
                new(first, SavedAt.AddSeconds(1), Guid.NewGuid())], first)
        };
        foreach (var scenario in cases) {
            await using var fixture = await Fixture.CreateAsync();
            string[] payloads;
            await using (var previous = fixture.Canonical()) {
                foreach (var row in scenario.Rows) {
                    await InsertLegacyAsync(previous, row);
                }
                payloads = await PayloadsAsync(previous);
            }
            HeadObservation[]? firstUpgrade = null;
            for (var restart = 0; restart < 2; restart++) {
                await using var upgraded = fixture.Canonical();
                await upgraded.Database.MigrateAsync();
                Assert.False(upgraded.Database.HasPendingModelChanges());
                Assert.Equal(payloads, await PayloadsAsync(upgraded));
                Assert.All(await upgraded.Set<WorkspaceSettings>().AsNoTracking().ToArrayAsync(), row => Assert.Null(row.DecisionDataPolicyJson));
                var rows = await upgraded.Set<WorkspaceSettings>().AsNoTracking().OrderBy(row => row.Id)
                    .Select(row => new HeadObservation(row.Id, row.IsCurrent, row.ConcurrencyToken)).ToArrayAsync();
                Assert.Equal(scenario.Rows.Length, rows.Length);
                Assert.Equal(scenario.ExpectedHead, Assert.Single(rows, row => row.IsCurrent).Id);
                Assert.All(rows, row => Assert.NotEqual(Guid.Empty, row.Revision));
                Assert.Equal(rows.Length, rows.Select(row => row.Revision).Distinct().Count());
                if (firstUpgrade is not null) {
                    Assert.Equal(firstUpgrade, rows);
                }
                firstUpgrade = rows;
                var binding = await fixture.Coordinator.EnsureReadyAsync(fixture.Profile);
                Assert.Equal(binding, await fixture.Coordinator.EnsureReadyAsync(fixture.Profile));
                Assert.Equal(payloads, await PayloadsAsync(upgraded));
                await using var owner = fixture.Owner();
                var selected = await owner.Set<WorkspaceSettings>().AsNoTracking().SingleAsync(row => row.IsCurrent);
                Assert.Equal(scenario.ExpectedHead, selected.Id);
                Assert.Equal(Assert.Single(rows, row => row.IsCurrent).Revision, selected.ConcurrencyToken);
            }
        }
    }

    [Fact]
    public async Task Filtered_unique_index_allows_inactive_rows_and_rejects_a_second_current_head_without_changing_the_first() {
        await using var fixture = await Fixture.CreateAsync();
        await using var context = fixture.Canonical();
        await context.Database.MigrateAsync();
        var current = new LegacyRow(Guid.NewGuid(), SavedAt, null);
        var inactive = new LegacyRow(Guid.NewGuid(), SavedAt.AddSeconds(1), Guid.NewGuid());
        var rejected = new LegacyRow(Guid.NewGuid(), SavedAt.AddSeconds(2), null);
        await InsertInactiveAsync(context, current);
        await InsertInactiveAsync(context, inactive);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Workspace_Settings" SET "IsCurrent" = true WHERE "Id" = {current.Id}
            """);
        var before = await context.Set<WorkspaceSettings>().AsNoTracking().OrderBy(row => row.Id)
            .Select(row => new HeadObservation(row.Id, row.IsCurrent, row.ConcurrencyToken)).ToArrayAsync();
        Assert.All(before, row => Assert.NotEqual(Guid.Empty, row.Revision));
        Assert.Equal(2, before.Select(row => row.Revision).Distinct().Count());
        var failure = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Workspace_Settings" SET "IsCurrent" = true WHERE "Id" = {inactive.Id}
            """));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, failure.SqlState);
        Assert.Equal(before, await context.Set<WorkspaceSettings>().AsNoTracking().OrderBy(row => row.Id)
            .Select(row => new HeadObservation(row.Id, row.IsCurrent, row.ConcurrencyToken)).ToArrayAsync());
        await InsertInactiveAsync(context, rejected);
        Assert.Equal(3, await context.Set<WorkspaceSettings>().CountAsync());
        Assert.Equal(current.Id, (await context.Set<WorkspaceSettings>().SingleAsync(row => row.IsCurrent)).Id);
    }

    private static Task InsertLegacyAsync(AppDbContext context, LegacyRow row)
        => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Workspace_Settings" ("Id", "WorkspaceName", "DefaultProviderProfileId", "DefaultPromptOutputFormat",
                "CurrencyCode", "CurrencyCultureName", "Notes", "UpdatedAtUtc")
            VALUES ({row.Id}, {"  Retained workspace  "}, {row.Provider}, {" JSON "}, {"CZK"}, {"cs-CZ"},
                {"Retained first line\nSecond line — untouched"}, {row.UpdatedAtUtc})
            """);

    private static Task InsertInactiveAsync(AppDbContext context, LegacyRow row)
        => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Workspace_Settings" ("Id", "WorkspaceName", "DefaultProviderProfileId", "DefaultPromptOutputFormat",
                "CurrencyCode", "CurrencyCultureName", "Notes", "UpdatedAtUtc", "IsCurrent")
            VALUES ({row.Id}, {"  Retained workspace  "}, {row.Provider}, {" JSON "}, {"CZK"}, {"cs-CZ"},
                {"Retained first line\nSecond line — untouched"}, {row.UpdatedAtUtc}, false)
            """);

    private static Task<string[]> PayloadsAsync(AppDbContext context)
        => context.Database.SqlQueryRaw<string>("""
            SELECT to_jsonb(legacy)::text AS "Value"
            FROM (SELECT "Id", "WorkspaceName", "DefaultProviderProfileId", "DefaultPromptOutputFormat",
                "CurrencyCode", "CurrencyCultureName", "Notes", "UpdatedAtUtc"
                FROM "Workspace_Settings") AS legacy ORDER BY legacy."Id"
            """).ToArrayAsync();

    private sealed record LegacyRow(Guid Id, DateTimeOffset UpdatedAtUtc, Guid? Provider);
    private sealed record RetainedCase(LegacyRow[] Rows, Guid ExpectedHead);
    private sealed record HeadObservation(Guid Id, bool IsCurrent, Guid Revision);

    private sealed partial class Fixture(PostgresTestDatabaseLease lease, CanDoItAllTestEnvironment environment) : IAsyncDisposable {
        internal ResolvedDatabaseProfile Profile { get; } = new(new DatabaseProfileRecord {
            Id = Guid.NewGuid(), ProviderKind = DatabaseProviderKind.PostgreSql
        }, DatabaseProfileResolutionSource.ExplicitOverride, lease.ConnectionString);
        internal AppDbContext Canonical() => new(lease.CreateAppDbContextOptions());
        internal AppDbContext Canonical(IInterceptor interceptor) => new(new DbContextOptionsBuilder<AppDbContext>(lease.CreateAppDbContextOptions())
            .AddInterceptors(interceptor).Options);
        internal string ConnectionString => lease.ConnectionString;
        internal CanDoItAllTestEnvironment Environment => environment;
        internal WorkspaceSettingsDbContext Owner() => new(new DbContextOptionsBuilder<WorkspaceSettingsDbContext>()
            .UseNpgsql(lease.ConnectionString).Options);
        internal DatasetActivationCoordinator Coordinator {
            get {
                var policies = new PhysicalFileSystemPathPolicyFactory();
                var writer = new DurableFileWriter(policies);
                var paths = new ControlPlanePathResolver(Options.Create(new ControlPlaneOptions { RootPath = environment.ControlPlaneRootPath }),
                    environment.CreateHostEnvironment(nameof(WorkspaceSettingsMigrationTests)), writer);
                return new(new(paths, writer, policies), [ProjectFiniteOperationPool.Create(new())]);
            }
        }

        internal static Task<Fixture> CreateAsync() => CreateAtAsync(PreviousMigration);
        internal static Task<Fixture> CreatePolicyPredecessorAsync() => CreateAtAsync(PolicyPreviousMigration);
        internal static Task<Fixture> CreateEmptyAsync() => CreateAtAsync(null);

        private static async Task<Fixture> CreateAtAsync(string? migration) {
            AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
            PostgresTestDatabaseLease? database = null;
            CanDoItAllTestEnvironment? files = null;
            try {
                database = PostgresTestDatabaseLease.Create("workspace-settings-migration");
                files = CanDoItAllTestEnvironment.Create("workspace-settings-migration");
                var fixture = new Fixture(database, files);
                if (migration is not null) {
                    await using var previous = fixture.Canonical();
                    await previous.GetService<IMigrator>().MigrateAsync(migration);
                }
                return fixture;
            } catch (Exception failure) {
                await TestFixtureCleanup.DisposeAfterFailureAsync(failure, database, files);
                throw;
            }
        }

        public ValueTask DisposeAsync() => TestFixtureCleanup.DisposeAsync(lease, environment);
    }
}
