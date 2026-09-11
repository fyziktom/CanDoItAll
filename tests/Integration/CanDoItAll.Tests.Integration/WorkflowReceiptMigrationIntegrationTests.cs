using CanDoItAll.SharedKernel;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkflowReceiptMigrationIntegrationTests {
    private const string PreviousMigration = "20260910213323_AddCrmTechnicalProjectionProvenance";
    private static readonly DateTimeOffset SavedAt = new(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);
    private const string OriginalJson = "{\"original\":true,\"unknownExtension\":{\"revision\":7}}";

    [Fact]
    public async Task Upgrade_preserves_existing_native_data_and_project_lifetimes_across_restarts() {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-receipt-upgrade");
        var profile = environment.CreatePostgreSqlProfile("upgrade");
        string[] original;
        await using (var provider = CreateProvider(profile)) {
            await using var context = await provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            await SeedNativeDataAsync(context);
            original = await NativePayloadsAsync(context);
        }
        for (var restart = 0; restart < 2; restart++) {
            await using var provider = CreateProvider(profile);
            await using var context = await provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            await context.Database.MigrateAsync();
            Assert.Equal(original, await NativePayloadsAsync(context));
            Assert.False(context.Database.HasPendingModelChanges());
            Assert.Equal(156, context.Model.GetEntityTypes().Count());
            Assert.Equal(0, await context.Set<WorkflowStructureOutputRecord>().CountAsync());
            Assert.Equal(0, await context.Set<StoragePlacementIntentRecord>().CountAsync());
            Assert.Equal(0, await context.Set<ProjectWorkflowAdmissionRecord>().CountAsync());
            Assert.Equal(0, await context.Set<ProjectWorkflowContributionRecord>().CountAsync());
            Assert.Equal(0, await context.Database.SqlQueryRaw<int>("""
                SELECT COUNT(*)::int AS "Value" FROM pg_constraint
                WHERE contype = 'f' AND conrelid IN (
                    '"AgentFramework_WorkflowStructureOutputs"'::regclass,
                    '"Storage_PlacementIntents"'::regclass,
                    '"Workbench_WorkflowAdmissions"'::regclass,
                    '"Workbench_WorkflowContributionReceipts"'::regclass)
                """).SingleAsync());
        }
    }

    [Fact]
    public async Task Empty_receipt_schema_can_downgrade_and_reupgrade_without_losing_native_data() {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-receipt-empty-down");
        await using var provider = CreateProvider(environment.CreatePostgreSqlProfile("empty"));
        await using var context = await provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await context.Database.MigrateAsync();
        await SeedNativeDataAsync(context);
        var original = await NativePayloadsAsync(context);
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        Assert.Equal(original, await NativePayloadsAsync(context));
        await context.Database.MigrateAsync();
        Assert.Equal(original, await NativePayloadsAsync(context));
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData(RecoveryOwner.Workflow)]
    [InlineData(RecoveryOwner.Storage)]
    [InlineData(RecoveryOwner.Admission)]
    [InlineData(RecoveryOwner.Contribution)]
    public async Task Any_retained_recovery_evidence_blocks_downgrade_atomically(RecoveryOwner owner) {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-receipt-retained-down");
        var profile = environment.CreatePostgreSqlProfile("retained");
        string original;
        await using (var provider = CreateProvider(profile)) {
            await using var context = await provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            await context.Database.MigrateAsync();
            var id = Guid.NewGuid();
            var path = new string('A', 64);
            switch (owner) {
                case RecoveryOwner.Workflow:
                    context.Add(new WorkflowStructureOutputRecord { RunId = id, OccurrencePath = path, PlanJson = OriginalJson,
                        ReceiptJson = OriginalJson, AssetDispatchStarted = true, NextInspectionAtUtc = SavedAt });
                    break;
                case RecoveryOwner.Storage:
                    context.Add(new StoragePlacementIntentRecord { Id = id, StorageId = Guid.NewGuid(), RequestFingerprint = path,
                        PlanJson = OriginalJson, ReceiptJson = OriginalJson, State = StorageStablePlacementState.Uncertain,
                        CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt });
                    break;
                case RecoveryOwner.Admission:
                    context.Add(new ProjectWorkflowAdmissionRecord { IntentId = id, ProjectId = Guid.NewGuid(), NodeId = "legacy-node",
                        NativeNodeId = Guid.NewGuid(), RunId = Guid.NewGuid(), Sequence = 1, AdmissionJson = OriginalJson,
                        StatusJson = OriginalJson, NextAttemptAtUtc = SavedAt });
                    break;
                case RecoveryOwner.Contribution:
                    context.Add(new ProjectWorkflowContributionRecord { RunId = id, OccurrencePath = path,
                        ProjectId = Guid.NewGuid(), NativeObjectId = Guid.NewGuid(), PlanJson = OriginalJson,
                        RequestJson = OriginalJson, NodeJson = OriginalJson, ReceiptJson = OriginalJson, PreparedAtUtc = SavedAt });
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(owner));
            }
            await context.SaveChangesAsync();
            original = await EvidencePayloadAsync(context, owner);
            var failure = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(PreviousMigration));
            Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);
        }
        await using var restarted = CreateProvider(profile);
        await using var readback = await restarted.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        Assert.Equal(original, await EvidencePayloadAsync(readback, owner));
        Assert.False(readback.Database.HasPendingModelChanges());
        Assert.NotEqual(PreviousMigration, (await readback.Database.GetAppliedMigrationsAsync()).Last());
    }

    public enum RecoveryOwner {
        Workflow,
        Storage,
        Admission,
        Contribution
    }

    private static ServiceProvider CreateProvider(TestDatabaseProfile profile) {
        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(services, TestApplicationBootstrap.BuildConfiguration(profile),
            new TestHostEnvironment(profile.EnvironmentRootPath, "CanDoItAll.Tests.Integration"));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static async Task SeedNativeDataAsync(AppDbContext context) {
        var project = new Project { Name = "Original project", Slug = Guid.NewGuid().ToString("N"), CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt };
        var node = new ProjectObjectRecord { ProjectId = project.Id, NodeKey = "legacy-node", ObjectType = ProjectObjectType.ProjectBlock,
            Title = "Original native title", Notes = "Human notes", MetadataJson = OriginalJson, CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt };
        context.AddRange(project, node, new ProjectNodeBindingRecord { ProjectObjectId = node.Id, Route = "/legacy/route",
            ExternalArtifactKind = "legacy", ExternalArtifactId = Guid.NewGuid(), CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt });
        var party = new Party {
            PartyType = PartyType.AiAgent, DisplayName = "Human-maintained CRM name", Summary = "Retained human enrichment",
            CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt
        };
        context.Add(party);
        context.Add(new AiResourceBinding {
            PartyId = party.Id, TechnicalAgentId = Guid.NewGuid(), BindingStatus = AiResourceBindingStatus.Bound,
            SourceDatabaseProfileId = Guid.NewGuid(), SourceScopeKind = WorkspaceScopeKind.Organization,
            SourceScopeKey = "original-scope", SourceCatalogRevision = 6,
            ProjectionAvailability = AiTechnicalProjectionAvailability.Missing,
            ProjectedDisplayName = "Retained technical name", ProjectedSummary = "Retained technical summary",
            ProjectedLifecycleStatus = AgentLifecycleStatus.Active, CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt
        });
        context.Add(new AiTechnicalProjectionCursor {
            DatabaseProfileId = Guid.NewGuid(), SourceScopeKind = WorkspaceScopeKind.Organization,
            SourceScopeKey = "original-scope", CatalogRevision = 7,
            ProjectionSha256 = new string('a', 64), UpdatedAtUtc = SavedAt
        });
        await context.SaveChangesAsync();
    }

    private static Task<string[]> NativePayloadsAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT 'project:' || to_jsonb(row)::text AS "Value" FROM "Projects_Projects" row
        UNION ALL SELECT 'native:' || to_jsonb(row)::text AS "Value" FROM "Workbench_ProjectObjects" row
        UNION ALL SELECT 'binding:' || to_jsonb(row)::text AS "Value" FROM "Workbench_ProjectNodeBindings" row
        UNION ALL SELECT 'party:' || to_jsonb(row)::text AS "Value" FROM "CrmHr_Parties" row
        UNION ALL SELECT 'ai-binding:' || to_jsonb(row)::text AS "Value" FROM "CrmHr_AiResourceBindings" row
        UNION ALL SELECT 'cursor:' || to_jsonb(row)::text AS "Value" FROM "CrmHr_AiTechnicalProjectionCursors" row
        ORDER BY "Value"
        """).ToArrayAsync();

    private static Task<string> EvidencePayloadAsync(AppDbContext context, RecoveryOwner owner) =>
        context.Database.SqlQueryRaw<string>(owner switch {
            RecoveryOwner.Workflow => "SELECT to_jsonb(row)::text AS \"Value\" FROM \"AgentFramework_WorkflowStructureOutputs\" row",
            RecoveryOwner.Storage => "SELECT to_jsonb(row)::text AS \"Value\" FROM \"Storage_PlacementIntents\" row",
            RecoveryOwner.Admission => "SELECT to_jsonb(row)::text AS \"Value\" FROM \"Workbench_WorkflowAdmissions\" row",
            RecoveryOwner.Contribution => "SELECT to_jsonb(row)::text AS \"Value\" FROM \"Workbench_WorkflowContributionReceipts\" row",
            _ => throw new ArgumentOutOfRangeException(nameof(owner))
        }).SingleAsync();
}
