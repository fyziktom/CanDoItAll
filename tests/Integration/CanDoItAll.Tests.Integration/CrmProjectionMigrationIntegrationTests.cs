using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class CrmProjectionMigrationIntegrationTests {
    private const string PreviousMigration = "20260910183745_AddProjectLifetimes";
    private static readonly DateTimeOffset SavedAt = new(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);

    [Fact]
    public async Task Upgrade_preserves_legacy_binding_and_human_enrichment_without_inventing_provenance() {
        await using var environment = CanDoItAllTestEnvironment.Create("crm-projection-upgrade");
        var profile = environment.CreatePostgreSqlProfile("upgrade");
        string[] original;
        await using (var provider = CreateProvider(profile)) {
            await using var context = await provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            await SeedLegacyBindingAsync(context);
            original = await LegacyPayloadsAsync(context);
        }
        for (var restart = 0; restart < 2; restart++) {
            await using var provider = CreateProvider(profile);
            await using var context = await provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            await context.Database.MigrateAsync();
            Assert.Equal(original, await LegacyPayloadsAsync(context));
            Assert.False(context.Database.HasPendingModelChanges());
            await using var owner = await provider.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
            var binding = await owner.Set<AiResourceBinding>().SingleAsync();
            Assert.Null(binding.SourceDatabaseProfileId);
            Assert.Null(binding.SourceScopeKind);
            Assert.Null(binding.SourceCatalogRevision);
            Assert.Empty(binding.SourceScopeKey);
            Assert.Equal(AiTechnicalProjectionAvailability.Unknown, binding.ProjectionAvailability);
            Assert.Empty(binding.ProjectedDisplayName);
            Assert.Empty(binding.ProjectedSummary);
            Assert.Null(binding.ProjectedLifecycleStatus);
            Assert.Empty(await owner.Set<AiTechnicalProjectionCursor>().ToArrayAsync());
            Assert.Equal("Human-maintained name", (await owner.Set<Party>().SingleAsync()).DisplayName);
        }
    }

    [Fact]
    public async Task Unused_projection_schema_can_downgrade_and_reupgrade_without_losing_legacy_data() {
        await using var environment = CanDoItAllTestEnvironment.Create("crm-projection-empty-down");
        await using var provider = CreateProvider(environment.CreatePostgreSqlProfile("empty"));
        await using var context = await provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        await SeedLegacyBindingAsync(context);
        var original = await LegacyPayloadsAsync(context);
        await context.Database.MigrateAsync();
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        Assert.Equal(original, await LegacyPayloadsAsync(context));
        await context.Database.MigrateAsync();
        Assert.Equal(original, await LegacyPayloadsAsync(context));
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData(RetainedEvidence.EmptyCatalogCursor)]
    [InlineData(RetainedEvidence.MissingAgentBinding)]
    public async Task Retained_projection_ordering_or_unavailable_identity_blocks_downgrade_atomically(RetainedEvidence evidence) {
        await using var environment = CanDoItAllTestEnvironment.Create("crm-projection-retained-down");
        var profile = environment.CreatePostgreSqlProfile("retained");
        string[] original;
        await using (var provider = CreateProvider(profile)) {
            await using var context = await provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            await context.Database.MigrateAsync();
            if (evidence == RetainedEvidence.EmptyCatalogCursor) {
                context.Add(new AiTechnicalProjectionCursor {
                    DatabaseProfileId = Guid.NewGuid(), SourceScopeKind = WorkspaceScopeKind.Organization,
                    SourceScopeKey = "original-scope", CatalogRevision = 7, ProjectionSha256 = new string('a', 64), UpdatedAtUtc = SavedAt
                });
            } else {
                var party = new Party { PartyType = PartyType.AiAgent, DisplayName = "Retained human enrichment", CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt };
                context.Add(party);
                context.Add(new AiResourceBinding {
                    PartyId = party.Id, TechnicalAgentId = Guid.NewGuid(), BindingStatus = AiResourceBindingStatus.Bound,
                    SourceDatabaseProfileId = Guid.NewGuid(), SourceScopeKind = WorkspaceScopeKind.Organization,
                    SourceScopeKey = "original-scope", SourceCatalogRevision = 6, ProjectionAvailability = AiTechnicalProjectionAvailability.Missing,
                    ProjectedDisplayName = "Retained technical name", ProjectedSummary = "Retained technical summary",
                    ProjectedLifecycleStatus = AgentLifecycleStatus.Active, CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt
                });
            }
            await context.SaveChangesAsync();
            original = await ProjectionPayloadsAsync(context);
            var failure = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(PreviousMigration));
            Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);
        }
        await using var restarted = CreateProvider(profile);
        await using var readback = await restarted.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        Assert.Equal(original, await ProjectionPayloadsAsync(readback));
        Assert.False(readback.Database.HasPendingModelChanges());
        Assert.NotEqual(PreviousMigration, (await readback.Database.GetAppliedMigrationsAsync()).Last());
    }

    public enum RetainedEvidence {
        EmptyCatalogCursor,
        MissingAgentBinding
    }

    private static ServiceProvider CreateProvider(TestDatabaseProfile profile) {
        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(services, TestApplicationBootstrap.BuildConfiguration(profile),
            new TestHostEnvironment(profile.EnvironmentRootPath, "CanDoItAll.Tests.Integration"));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static async Task SeedLegacyBindingAsync(AppDbContext context) {
        var party = new Party {
            PartyType = PartyType.AiAgent, DisplayName = "Human-maintained name", Summary = "Human-maintained summary",
            CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt
        };
        context.Add(party);
        await context.SaveChangesAsync();
        var bindingId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var capabilitiesJson = """[{"name":"legacy","extension":7}]""";
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "CrmHr_AiResourceBindings" (
                "Id", "PartyId", "TechnicalAgentId", "BindingStatus", "BindingReason", "LastError",
                "ProjectedProviderName", "ProjectedDefaultModel", "ProjectedCapabilityCount", "ProjectedRoleTitle",
                "ProjectedInstructions", "ProjectedTemplateKey", "ProjectedTagsJson", "ProjectedCapabilitiesJson",
                "ProjectionUpdatedAtUtc", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ({bindingId}, {party.Id}, {agentId}, 'Bound', 'Legacy binding', '',
                'Legacy provider', 'legacy-model', 1, 'Original role', 'Original instructions', 'legacy-template',
                '["legacy-tag"]', {capabilitiesJson}, {SavedAt}, {SavedAt}, {SavedAt})
            """);
    }

    private static Task<string[]> LegacyPayloadsAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT 'party:' || to_jsonb(row)::text AS "Value" FROM "CrmHr_Parties" row
        UNION ALL SELECT 'binding:' || (to_jsonb(row) - ARRAY[
            'SourceDatabaseProfileId', 'SourceScopeKind', 'SourceScopeKey', 'SourceCatalogRevision',
            'ProjectionAvailability', 'ProjectedDisplayName', 'ProjectedSummary', 'ProjectedLifecycleStatus'])::text AS "Value"
            FROM "CrmHr_AiResourceBindings" row
        ORDER BY "Value"
        """).ToArrayAsync();

    private static Task<string[]> ProjectionPayloadsAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT 'party:' || to_jsonb(row)::text AS "Value" FROM "CrmHr_Parties" row
        UNION ALL SELECT 'binding:' || to_jsonb(row)::text AS "Value" FROM "CrmHr_AiResourceBindings" row
        UNION ALL SELECT 'cursor:' || to_jsonb(row)::text AS "Value" FROM "CrmHr_AiTechnicalProjectionCursors" row
        ORDER BY "Value"
        """).ToArrayAsync();
}
