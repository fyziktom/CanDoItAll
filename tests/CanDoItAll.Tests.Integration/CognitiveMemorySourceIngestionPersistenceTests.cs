using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support.CognitiveMemory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;

using CanDoItAll.Tests.Support;
namespace CanDoItAll.Tests.Integration;

public sealed class CognitiveMemorySourceIngestionPersistenceTests
{
    [Fact]
    public async Task SourceIngestionPersistenceModel_IndexesSourceIngestionRows()
    {
        await using var fixture = await CreateFixtureAsync();
        var entityTypes = fixture.DbContext.Model.GetEntityTypes().ToList();

        AssertEntityTable<CognitiveMemorySourceItemLayoutRecord>(entityTypes, "CognitiveMemory_SourceItemLayouts");
        AssertEntityTable<CognitiveMemorySourceItemGraphLinkRecord>(entityTypes, "CognitiveMemory_SourceItemGraphLinks");
        AssertEntityTable<CognitiveMemorySourceItemContextHintRecord>(entityTypes, "CognitiveMemory_SourceItemContextHints");
        AssertEntityTable<CognitiveMemorySourceTombstoneRecord>(entityTypes, "CognitiveMemory_SourceTombstones");
        AssertEntityTable<CognitiveMemorySourceScanFailureRecord>(entityTypes, "CognitiveMemory_SourceScanFailures");

        var sourceItemType = Assert.Single(entityTypes, entityType => entityType.ClrType == typeof(CognitiveMemorySourceItemRecord));
        Assert.NotNull(sourceItemType.FindProperty(nameof(CognitiveMemorySourceItemRecord.ContentText)));
        foreach (var expectation in CognitiveMemoryEfGuardrails.SourceIngestionIndexExpectations)
        {
            var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == expectation.EntityType);
            Assert.True(
                CognitiveMemoryEfGuardrails.HasExpectedIndex(entityType, expectation),
                $"Missing expected source ingestion index on {expectation.EntityType.Name}: {string.Join(", ", expectation.PropertyNames)}.");
        }
    }

    [Fact]
    public async Task SourceIngestion_FirstWorkbenchScan_PersistsSourceItemsEvidenceLayoutLinksAndContextHints()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.NewGuid();
        var nodeB = CreateWorkbenchNode(projectId, "node-b", "Production Docker", "Production deployment context");
        var nodeA = CreateWorkbenchNode(
            projectId,
            "node-a",
            "Test Docker",
            "Local Docker simulation context",
            [new MemorySourceLink(NodeId(projectId, "node-a"), nodeB.Id, "RelatedButContextSeparated", IsUserAuthored: true)]);
        fixture.ProjectProvider.SetItems(projectId, [nodeA, nodeB]);

        var result = await fixture.Service.IngestAsync(new CognitiveMemorySourceIngestionRequest(
            MemorySourceKind.WorkbenchProjectStructure,
            projectId,
            new CognitiveMemoryIdempotencyKey("workbench-first-scan")));

        Assert.True(result.Status == CognitiveMemorySourceIngestionStatus.Ingested, await ReadFailureMessageAsync(fixture, result));
        Assert.False(result.HasMore);
        Assert.Equal(2, result.CreatedSourceItemCount);
        Assert.Equal(2, result.CreatedEvidenceAnchorCount);
        Assert.Equal(2, result.CreatedContextHintCount);
        Assert.Equal(2, result.CreatedLayoutCount);
        Assert.Equal(1, result.CreatedGraphLinkCount);

        Assert.Equal(1, await fixture.DbContext.Set<CognitiveMemorySourceManifestRecord>().CountAsync());
        Assert.Equal(2, await fixture.DbContext.Set<CognitiveMemorySourceItemRecord>().CountAsync());
        Assert.Equal(2, await fixture.DbContext.Set<CognitiveMemoryEvidenceAnchorRecord>().CountAsync());
        Assert.Equal(2, await fixture.DbContext.Set<CognitiveMemorySourceItemLayoutRecord>().CountAsync());
        Assert.Equal(1, await fixture.DbContext.Set<CognitiveMemorySourceItemGraphLinkRecord>().CountAsync());
        Assert.Equal(2, await fixture.DbContext.Set<CognitiveMemorySourceItemContextHintRecord>().CountAsync());
        Assert.Equal("Local Docker simulation context", (await fixture.DbContext.Set<CognitiveMemorySourceItemRecord>().SingleAsync(item => item.Title == "Test Docker")).ContentText);
    }

    [Fact]
    public async Task SourceIngestion_DuplicateIdempotencyKey_IsRejectedWithoutDuplicateWrites()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.NewGuid();
        fixture.ProjectProvider.SetItems(projectId, [CreateWorkbenchNode(projectId, "node-a", "Node A", "First content")]);
        var request = new CognitiveMemorySourceIngestionRequest(
            MemorySourceKind.WorkbenchProjectStructure,
            projectId,
            new CognitiveMemoryIdempotencyKey("duplicate-workbench-scan"));

        var first = await fixture.Service.IngestAsync(request);
        var second = await fixture.Service.IngestAsync(request);

        Assert.True(first.Status == CognitiveMemorySourceIngestionStatus.Ingested, await ReadFailureMessageAsync(fixture, first));
        Assert.Equal(CognitiveMemorySourceIngestionStatus.DuplicateRejected, second.Status);
        Assert.Equal("DuplicateIdempotencyKey", second.FailureCode);
        Assert.Equal(1, await fixture.DbContext.Set<CognitiveMemoryRunRecord>().CountAsync());
        Assert.Equal(1, await fixture.DbContext.Set<CognitiveMemorySourceManifestRecord>().CountAsync());
        Assert.Equal(1, await fixture.DbContext.Set<CognitiveMemorySourceItemRecord>().CountAsync());
    }

    [Fact]
    public async Task SourceIngestion_IncrementalRescan_CreatesNewManifestAndTombstoneForRemovedItem()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.NewGuid();
        fixture.ProjectProvider.SetItems(projectId, [
            CreateWorkbenchNode(projectId, "node-a", "Node A", "Original content"),
            CreateWorkbenchNode(projectId, "node-b", "Node B", "Removed content")
        ]);
        await fixture.Service.IngestAsync(new CognitiveMemorySourceIngestionRequest(
            MemorySourceKind.WorkbenchProjectStructure,
            projectId,
            new CognitiveMemoryIdempotencyKey("workbench-scan-before-delete")));

        fixture.ProjectProvider.SetItems(projectId, [
            CreateWorkbenchNode(projectId, "node-a", "Node A", "Changed content")
        ]);
        var second = await fixture.Service.IngestAsync(new CognitiveMemorySourceIngestionRequest(
            MemorySourceKind.WorkbenchProjectStructure,
            projectId,
            new CognitiveMemoryIdempotencyKey("workbench-scan-after-delete")));

        Assert.True(second.Status == CognitiveMemorySourceIngestionStatus.Ingested, await ReadFailureMessageAsync(fixture, second));
        Assert.Equal(1, second.CreatedTombstoneCount);
        Assert.Equal(2, await fixture.DbContext.Set<CognitiveMemorySourceManifestRecord>().CountAsync());
        Assert.Equal(3, await fixture.DbContext.Set<CognitiveMemorySourceItemRecord>().CountAsync());
        var tombstone = Assert.Single(await fixture.DbContext.Set<CognitiveMemorySourceTombstoneRecord>().ToListAsync());
        Assert.Contains("node-b", tombstone.SourceItemKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SourceIngestion_ProviderFailure_RecordsFailureAndFailedRun()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.NewGuid();

        var result = await fixture.Service.IngestAsync(new CognitiveMemorySourceIngestionRequest(
            MemorySourceKind.WorkbenchProjectStructure,
            projectId,
            new CognitiveMemoryIdempotencyKey("missing-provider-items")));

        Assert.Equal(CognitiveMemorySourceIngestionStatus.Failed, result.Status);
        Assert.NotNull(result.FailureId);
        var run = Assert.Single(await fixture.DbContext.Set<CognitiveMemoryRunRecord>().ToListAsync());
        var failure = Assert.Single(await fixture.DbContext.Set<CognitiveMemorySourceScanFailureRecord>().ToListAsync());
        Assert.Equal(CognitiveMemoryRunStatus.Failed, run.Status);
        Assert.Equal(run.Id, failure.RunId);
        Assert.Equal(CognitiveMemorySourceScanFailureRetryPolicy.Retryable, failure.RetryPolicy);
    }

    private static MemorySourceItem CreateWorkbenchNode(
        Guid projectId,
        string sourceEntityId,
        string title,
        string content,
        IReadOnlyList<MemorySourceLink>? links = null)
    {
        var itemId = NodeId(projectId, sourceEntityId);
        var contentHash = MemorySourceSnapshotHasher.Compute(sourceEntityId, title, content, "x:10", "y:20");
        return new MemorySourceItem(
            itemId,
            MemorySourceKind.WorkbenchProjectStructure,
            MemorySourceEntityKind.ProjectNode,
            title,
            content,
            contentHash,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            new MemorySourceProvenance(
                MemorySourceKind.WorkbenchProjectStructure,
                projectId,
                MemorySourceEntityKind.ProjectNode,
                sourceEntityId,
                $"/projects/{projectId:D}/structure/{sourceEntityId}"),
            new MemorySourcePermissionContext(
                MemorySourceAccessMode.ReadOnly,
                MemorySourceSensitivity.Internal,
                ContainsSensitivePayload: false,
                RedactionPolicy: "fake-no-redaction",
                AllowedFutureUsageSummary: "Integration test fixture."),
            new MemorySourceLayoutMetadata(10, 20, 3, null, null, null, "project-structure", "{\"z\":3}"),
            links ?? [],
            [new MemorySourceReference("project-node", sourceEntityId, 0)],
            null,
            new Dictionary<string, string>
            {
                ["sourceEntityId"] = sourceEntityId
            })
        {
            HashPolicy = MemorySourceHashPolicy.PublicRedactedContent
        };
    }

    private static async Task<string?> ReadFailureMessageAsync(
        SourceIngestionFixture fixture,
        CognitiveMemorySourceIngestionResult result)
    {
        if (result.FailureId is not Guid failureId)
        {
            return result.FailureCode;
        }

        var failure = await fixture.DbContext.Set<CognitiveMemorySourceScanFailureRecord>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == failureId);
        return $"{result.FailureCode}: {failure.Message}";
    }

    private static MemorySourceItemId NodeId(Guid projectId, string sourceEntityId)
        => MemorySourceItemId.Create(
            MemorySourceKind.WorkbenchProjectStructure,
            projectId,
            MemorySourceEntityKind.ProjectNode,
            sourceEntityId);

    private static async Task<SourceIngestionFixture> CreateFixtureAsync()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        var database = PostgresTestDatabaseLease.Create("cognitivememorysourceingestionpersistencetests");

        var options = database.CreateAppDbContextOptions();
        var factory = new TestDbContextFactory(options);
        var dbContext = await factory.CreateDbContextAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var projectProvider = new FakeProjectStructureSourceSnapshotProvider();
        var processProvider = new FakeProcessRuntimeEvidenceSourceProvider();
        var workflowProvider = new FakeWorkflowRuntimeEvidenceSourceProvider();
        var service = new CognitiveMemorySourceIngestionService(
            factory,
            projectProvider,
            processProvider,
            workflowProvider,
            new FixedClock(),
            NullLogger<CognitiveMemorySourceIngestionService>.Instance);

        return new SourceIngestionFixture(database, dbContext, projectProvider, service);
    }

    private static void AssertEntityTable<TEntity>(IReadOnlyList<IEntityType> entityTypes, string tableName)
    {
        var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == typeof(TEntity));
        Assert.Equal(tableName, entityType.GetTableName());
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class SourceIngestionFixture(
        PostgresTestDatabaseLease database,
        AppDbContext dbContext,
        FakeProjectStructureSourceSnapshotProvider projectProvider,
        CognitiveMemorySourceIngestionService service) : IAsyncDisposable
    {
        public AppDbContext DbContext { get; } = dbContext;

        public FakeProjectStructureSourceSnapshotProvider ProjectProvider { get; } = projectProvider;

        public CognitiveMemorySourceIngestionService Service { get; } = service;

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await database.DisposeAsync();
        }
    }
}
