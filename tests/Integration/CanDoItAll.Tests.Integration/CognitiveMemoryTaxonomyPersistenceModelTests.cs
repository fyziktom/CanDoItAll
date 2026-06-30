using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support.CognitiveMemory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using CanDoItAll.Tests.Support;
namespace CanDoItAll.Tests.Integration;

public sealed class CognitiveMemoryTaxonomyPersistenceModelTests
{
    [Fact]
    public async Task TaxonomyPersistenceModel_IndexesCanonicalRelationsEvidenceAndProjections()
    {
        await using var fixture = await CreateFixtureAsync();
        var entityTypes = fixture.DbContext.Model.GetEntityTypes().ToList();

        AssertEntityTable<CognitiveMemoryRecordEvidenceAnchorRecord>(entityTypes, "CognitiveMemory_RecordEvidenceAnchors");
        AssertEntityTable<CognitiveMemoryRelationEvidenceRecord>(entityTypes, "CognitiveMemory_RelationEvidence");
        AssertEntityTable<CognitiveMemoryProjectionRecord>(entityTypes, "CognitiveMemory_Projections");

        foreach (var expectation in CognitiveMemoryEfGuardrails.TaxonomyIndexExpectations)
        {
            var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == expectation.EntityType);
            Assert.True(
                CognitiveMemoryEfGuardrails.HasExpectedIndex(entityType, expectation),
                $"Missing expected taxonomy index on {expectation.EntityType.Name}: {string.Join(", ", expectation.PropertyNames)}.");
        }

        AssertEnumStateFieldsAreNotPersistedAsStrings(entityTypes);
    }

    [Fact]
    public async Task ProjectionRows_AreRebuildableAndDeleteableWithoutChangingCanonicalMemory()
    {
        await using var fixture = await CreateFixtureAsync();
        var graph = await SeedCanonicalGraphAsync(fixture.DbContext);
        var sourceHash = CognitiveMemoryHash.FromUtf8("source-v1");
        var payloadHash = CognitiveMemoryHash.FromUtf8("payload-v1");
        var projection = CreateProjection(graph.RecordA.Id, sourceHash, payloadHash);
        fixture.DbContext.Add(projection);
        await fixture.DbContext.SaveChangesAsync();

        var service = new CognitiveMemoryProjectionLifecycleService(
            new FakeCognitiveMemoryEmbeddingProvider(dimensions: 3),
            new NoopProjectionAdapter(),
            new CognitiveMemoryTaxonomyValidator(new CognitiveMemoryRecordValidator()),
            new FixedClock(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CognitiveMemoryProjectionLifecycleService>.Instance);

        var decision = service.EvaluateLifecycle(new CognitiveMemoryProjectionLifecycleEvaluationRequest(
            projection,
            CognitiveMemoryHash.FromUtf8("source-v2"),
            payloadHash,
            new CognitiveMemoryProjectionProfileId("projection-v1"),
            new CognitiveMemoryEmbeddingProfileId("embedding-v1"),
            new CognitiveMemoryPayloadSchemaVersion("projection-payload-v1"),
            new CognitiveMemoryAlgorithmVersion("taxonomy-v1"),
            SourceTombstoned: false));
        projection.Status = CognitiveMemoryProjectionStatus.RebuildRequired;
        projection.RebuildRequired = true;
        projection.StaleReason = decision.StaleReason;
        projection.UpdatedAtUtc = DateTimeOffset.UnixEpoch.AddMinutes(1);
        await fixture.DbContext.SaveChangesAsync();

        var staleProjection = await fixture.DbContext.Set<CognitiveMemoryProjectionRecord>()
            .SingleAsync(item => item.RebuildRequired);
        var canonicalRecord = await fixture.DbContext.Set<CognitiveMemoryRecord>()
            .SingleAsync(item => item.Id == graph.RecordA.Id);

        Assert.Equal(CognitiveMemoryProjectionStaleReason.SourceHashChanged, staleProjection.StaleReason);
        Assert.Equal(graph.RecordA.ContentHash, canonicalRecord.ContentHash);

        fixture.DbContext.Remove(staleProjection);
        await fixture.DbContext.SaveChangesAsync();

        Assert.Equal(0, await fixture.DbContext.Set<CognitiveMemoryProjectionRecord>().CountAsync());
        Assert.Equal(2, await fixture.DbContext.Set<CognitiveMemoryRecord>().CountAsync());
        Assert.Equal(1, await fixture.DbContext.Set<CognitiveMemoryRecordEvidenceAnchorRecord>().CountAsync());
        Assert.Equal(1, await fixture.DbContext.Set<CognitiveMemoryRelationEvidenceRecord>().CountAsync());
    }

    private static async Task<CanonicalGraph> SeedCanonicalGraphAsync(AppDbContext dbContext)
    {
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var manifest = new CognitiveMemorySourceManifestRecord
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            ProjectId = projectId,
            SourceSystem = "workbench",
            SourceScopeKey = projectId.ToString("D"),
            SourceSnapshotId = "workbench-snapshot-1",
            SnapshotHash = CognitiveMemoryHash.FromUtf8("snapshot").Value,
            ProviderVersion = "test-provider-v1",
            ScanStatus = CognitiveMemoryRunStatus.Succeeded,
            ObservedAtUtc = DateTimeOffset.UnixEpoch,
            CreatedAtUtc = DateTimeOffset.UnixEpoch,
            UpdatedAtUtc = DateTimeOffset.UnixEpoch
        };
        var sourceItem = new CognitiveMemorySourceItemRecord
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000002"),
            SourceManifestId = manifest.Id,
            ProjectId = projectId,
            SourceSystem = "workbench",
            SourceItemKey = "node-1",
            SourceItemType = "ProjectNode",
            Title = "Docker contexts",
            ContentText = "Docker local and production contexts are related but not substitutable.",
            ContentHash = CognitiveMemoryHash.FromUtf8("source-item").Value,
            AccessScope = "project",
            ObservedAtUtc = DateTimeOffset.UnixEpoch,
            CreatedAtUtc = DateTimeOffset.UnixEpoch,
            UpdatedAtUtc = DateTimeOffset.UnixEpoch
        };
        var anchor = new CognitiveMemoryEvidenceAnchorRecord
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000003"),
            ProjectId = projectId,
            SourceManifestId = manifest.Id,
            SourceItemId = sourceItem.Id,
            AnchorKind = CognitiveMemoryEvidenceAnchorKind.TextSpan,
            SourceSystem = "workbench",
            Locator = "/projects/docker/node-1",
            StructuredPath = "$.nodes[0]",
            TextStart = 0,
            TextEnd = 32,
            QuoteHash = CognitiveMemoryHash.FromUtf8("quote").Value,
            SourceHash = sourceItem.ContentHash,
            TrustLevel = CognitiveMemorySourceTrustLevel.RuntimeSource,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            ObservedAtUtc = DateTimeOffset.UnixEpoch,
            CreatedAtUtc = DateTimeOffset.UnixEpoch
        };
        var recordA = CreateRecord(
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            projectId,
            "docker.contexts.local",
            "Local Docker simulation context");
        var recordB = CreateRecord(
            Guid.Parse("20000000-0000-0000-0000-000000000002"),
            projectId,
            "docker.contexts.production",
            "Production Docker deployment context");
        var sourceLink = new CognitiveMemorySourceLinkRecord
        {
            MemoryRecordId = recordA.Id,
            SourceManifestId = manifest.Id,
            SourceItemId = sourceItem.Id,
            EvidenceRole = CognitiveMemoryEvidenceRole.PrimarySource,
            QuoteHash = anchor.QuoteHash,
            Summary = "Workbench node source.",
            CreatedAtUtc = DateTimeOffset.UnixEpoch
        };
        var recordAnchor = new CognitiveMemoryRecordEvidenceAnchorRecord
        {
            MemoryRecordId = recordA.Id,
            EvidenceAnchorId = anchor.Id,
            EvidenceRole = CognitiveMemoryEvidenceRole.PrimarySource,
            Summary = "Canonical memory source anchor.",
            CreatedAtUtc = DateTimeOffset.UnixEpoch
        };
        var relation = new CognitiveMemoryRelationRecord
        {
            Id = Guid.Parse("30000000-0000-0000-0000-000000000001"),
            ProjectId = projectId,
            SourceMemoryRecordId = recordA.Id,
            TargetMemoryRecordId = recordB.Id,
            RelationKind = CognitiveMemoryRelationKind.SemanticallyRelatedButContextSeparated,
            EvidenceCount = 1,
            RelationBucket = CognitiveMemoryScoreProjectionBucket.Inhibit,
            DisplayStrengthProjection = 0.92,
            Reason = "Local simulation context is related but must not substitute production deployment context.",
            AlgorithmVersion = "taxonomy-v1",
            CreatedAtUtc = DateTimeOffset.UnixEpoch
        };
        var relationEvidence = new CognitiveMemoryRelationEvidenceRecord
        {
            RelationId = relation.Id,
            EvidenceAnchorId = anchor.Id,
            Direction = CognitiveMemoryEvidenceDirection.NarrowsScope,
            Summary = "Context boundary evidence.",
            CreatedAtUtc = DateTimeOffset.UnixEpoch
        };

        dbContext.AddRange(manifest, sourceItem, anchor, recordA, recordB, sourceLink, recordAnchor, relation, relationEvidence);
        await dbContext.SaveChangesAsync();
        return new CanonicalGraph(recordA, recordB);
    }

    private static CognitiveMemoryRecord CreateRecord(
        Guid id,
        Guid projectId,
        string topicKey,
        string title)
        => new()
        {
            Id = id,
            ProjectId = projectId,
            Kind = CognitiveMemoryRecordKind.Semantic,
            Origin = CognitiveMemoryRecordOrigin.SourceDerived,
            Title = title,
            TopicKey = topicKey,
            CanonicalText = $"{title} is source-backed.",
            SummaryText = title,
            ValidationState = CognitiveMemoryValidationState.Approved,
            StabilityState = CognitiveMemoryStabilityState.Active,
            CreatedInMode = CognitiveMemoryOperationMode.Observe,
            AlgorithmVersion = "taxonomy-v1",
            ContentHash = CognitiveMemoryHash.FromUtf8(title).Value,
            SourceEvidenceCount = 1,
            EvidenceAnchorCount = 1,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RiskLevel = CognitiveMemoryRiskLevel.Low,
            CreatedAtUtc = DateTimeOffset.UnixEpoch,
            UpdatedAtUtc = DateTimeOffset.UnixEpoch
        };

    private static CognitiveMemoryProjectionRecord CreateProjection(
        Guid memoryRecordId,
        CognitiveMemoryHash sourceHash,
        CognitiveMemoryHash payloadHash)
        => new()
        {
            Id = Guid.Parse("40000000-0000-0000-0000-000000000001"),
            ProjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            MemoryRecordId = memoryRecordId,
            ProjectionStoreKind = CognitiveMemoryProjectionStoreKind.GenericRag,
            ProjectionKind = CognitiveMemoryProjectionKind.VectorCollection,
            TargetProviderName = "fake-rag",
            CollectionName = "cm-project-semantic",
            PointId = "point-1",
            ProjectionProfileId = "projection-v1",
            EmbeddingProfileId = "embedding-v1",
            ProjectionSchemaVersion = "projection-payload-v1",
            AlgorithmVersion = "taxonomy-v1",
            VectorDimensions = 3,
            SourceHash = sourceHash.Value,
            PayloadHash = payloadHash.Value,
            Status = CognitiveMemoryProjectionStatus.Projected,
            CreatedAtUtc = DateTimeOffset.UnixEpoch,
            UpdatedAtUtc = DateTimeOffset.UnixEpoch,
            LastProjectedAtUtc = DateTimeOffset.UnixEpoch
        };

    private static async Task<TaxonomyFixture> CreateFixtureAsync()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        var database = PostgresTestDatabaseLease.Create("cognitivememorytaxonomypersistencemodeltests");

        var options = database.CreateAppDbContextOptions();
        var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return new TaxonomyFixture(database, dbContext);
    }

    private static void AssertEntityTable<TEntity>(IReadOnlyList<IEntityType> entityTypes, string tableName)
    {
        var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == typeof(TEntity));
        Assert.Equal(tableName, entityType.GetTableName());
    }

    private static void AssertEnumStateFieldsAreNotPersistedAsStrings(IReadOnlyList<IEntityType> entityTypes)
    {
        var stateProperties = new Dictionary<Type, string[]>
        {
            [typeof(CognitiveMemoryProjectionRecord)] =
            [
                nameof(CognitiveMemoryProjectionRecord.ProjectionStoreKind),
                nameof(CognitiveMemoryProjectionRecord.ProjectionKind),
                nameof(CognitiveMemoryProjectionRecord.Status),
                nameof(CognitiveMemoryProjectionRecord.StaleReason)
            ],
            [typeof(CognitiveMemoryRecord)] =
            [
                nameof(CognitiveMemoryRecord.ConfidenceBucket),
                nameof(CognitiveMemoryRecord.ActivationBucket)
            ],
            [typeof(CognitiveMemoryRelationRecord)] =
            [
                nameof(CognitiveMemoryRelationRecord.RelationKind),
                nameof(CognitiveMemoryRelationRecord.RelationBucket)
            ]
        };

        foreach (var (entityClrType, propertyNames) in stateProperties)
        {
            var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == entityClrType);
            foreach (var propertyName in propertyNames)
            {
                var property = Assert.IsAssignableFrom<IProperty>(entityType.FindProperty(propertyName));
                Assert.True(property.ClrType.IsEnum, $"{entityClrType.Name}.{propertyName} should remain a typed enum.");
                Assert.NotEqual(typeof(string), property.ClrType);
            }
        }
    }

    private sealed record CanonicalGraph(CognitiveMemoryRecord RecordA, CognitiveMemoryRecord RecordB);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }

    private sealed class NoopProjectionAdapter : ICognitiveMemoryProjectionAdapter
    {
        public CognitiveMemoryProjectionAdapterCapabilities Capabilities { get; } = new(
            "fake-rag",
            SupportsFilters: true,
            SupportsPayloadIndexes: true,
            SupportsDeleteByFilter: true,
            SupportsNamedVectors: false);

        public ValueTask EnsureCollectionAsync(
            CognitiveMemoryProjectionCollectionRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<CognitiveMemoryProjectionPayloadIndexResult>> EnsurePayloadIndexesAsync(
            CognitiveMemoryProjectionPayloadIndexRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<CognitiveMemoryProjectionPayloadIndexResult>>([]);

        public ValueTask ProjectAsync(
            CognitiveMemoryProjectionWriteRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<CognitiveMemoryProjectionSearchResult> SearchAsync(
            CognitiveMemoryProjectionSearchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<CognitiveMemoryProjectionDeleteResult> DeleteBySourceAsync(
            CognitiveMemoryProjectionDeleteBySourceRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TaxonomyFixture(
        PostgresTestDatabaseLease database,
        AppDbContext dbContext) : IAsyncDisposable
    {
        public AppDbContext DbContext { get; } = dbContext;

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await database.DisposeAsync();
        }
    }
}
