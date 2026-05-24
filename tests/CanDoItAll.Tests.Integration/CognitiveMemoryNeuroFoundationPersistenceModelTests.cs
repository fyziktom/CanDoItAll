using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using CanDoItAll.Tests.Support;
namespace CanDoItAll.Tests.Integration;

public sealed class CognitiveMemoryNeuroFoundationPersistenceModelTests
{
    [Fact]
    public async Task NeuroFoundationPersistenceModel_IndexesClaimsEvidenceContextsAndMutationAudit()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);

        await using var database = PostgresTestDatabaseLease.Create("cognitivememoryneurofoundationpersistencemodeltests");

        var options = database.CreateAppDbContextOptions();

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var entityTypes = dbContext.Model.GetEntityTypes().ToList();
        AssertEntityTable<CognitiveMemoryEvidenceAnchorRecord>(entityTypes, "CognitiveMemory_EvidenceAnchors");
        AssertEntityTable<CognitiveMemoryClaimRecord>(entityTypes, "CognitiveMemory_Claims");
        AssertEntityTable<CognitiveMemoryClaimEvidenceLinkRecord>(entityTypes, "CognitiveMemory_ClaimEvidenceLinks");
        AssertEntityTable<CognitiveMemoryBeliefStateRecord>(entityTypes, "CognitiveMemory_BeliefStates");
        AssertEntityTable<CognitiveMemoryEntityRecord>(entityTypes, "CognitiveMemory_Entities");
        AssertEntityTable<CognitiveMemoryEntityAliasRecord>(entityTypes, "CognitiveMemory_EntityAliases");
        AssertEntityTable<CognitiveMemoryContextFrameRecord>(entityTypes, "CognitiveMemory_ContextFrames");
        AssertEntityTable<CognitiveMemoryContextFrameDimensionRecord>(entityTypes, "CognitiveMemory_ContextFrameDimensions");
        AssertEntityTable<CognitiveMemoryContextBoundaryRecord>(entityTypes, "CognitiveMemory_ContextBoundaries");
        AssertEntityTable<CognitiveMemoryMutationCommandRecord>(entityTypes, "CognitiveMemory_MutationCommands");
        AssertEntityTable<CognitiveMemoryMutationAuditEventRecord>(entityTypes, "CognitiveMemory_MutationAuditEvents");
        AssertNeuroFoundationIndexes(entityTypes);
        AssertEnumStateFieldsAreNotPersistedAsStrings(entityTypes);
    }

    [Fact]
    public async Task MutationAuthority_RejectsGeneratedClaimMutationWithoutEvidenceAnchorsAndAuditsIt()
    {
        var (connection, authority, factory) = await CreateMutationAuthorityAsync();
        await using (connection)
        {
            var result = await authority.SubmitAsync(new CognitiveMemoryMutationCommand(
                ProjectId: Guid.NewGuid(),
                CognitiveMemoryMutationCommandKind.ProposeClaim,
                CognitiveMemoryActorKind.Agent,
                ActorId: "agent:test",
                new CognitiveMemoryIdempotencyKey("claim-without-evidence"),
                AffectedMemoryRecordIds: [],
                AffectedClaimIds: [],
                EvidenceAnchorIds: [],
                PayloadJson: "{\"claim\":\"Generated summary\"}",
                ExpectedVersionToken: null,
                RequiresHumanReview: false));

            Assert.False(result.Accepted);
            Assert.False(result.Applied);
            Assert.Contains("Evidence anchors are required", result.ReviewReason, StringComparison.Ordinal);
            Assert.Single(result.CreatedAuditEventIds);

            await using var dbContext = await factory.CreateDbContextAsync();
            var command = Assert.Single(await dbContext.Set<CognitiveMemoryMutationCommandRecord>().ToListAsync());
            var audit = Assert.Single(await dbContext.Set<CognitiveMemoryMutationAuditEventRecord>().ToListAsync());
            Assert.Equal(CognitiveMemoryMutationCommandStatus.Rejected, command.Status);
            Assert.Equal(CognitiveMemoryMutationAuditEventKind.Rejected, audit.EventKind);
        }
    }

    [Fact]
    public async Task MutationAuthority_ReplaysIdempotentCommandWithoutCreatingDuplicateAudit()
    {
        var (connection, authority, factory) = await CreateMutationAuthorityAsync();
        await using (connection)
        {
            var evidenceAnchorId = Guid.NewGuid();
            var command = new CognitiveMemoryMutationCommand(
                ProjectId: Guid.NewGuid(),
                CognitiveMemoryMutationCommandKind.SupportClaim,
                CognitiveMemoryActorKind.User,
                ActorId: "user:test",
                new CognitiveMemoryIdempotencyKey("support-claim-once"),
                AffectedMemoryRecordIds: [],
                AffectedClaimIds: [Guid.NewGuid()],
                EvidenceAnchorIds: [evidenceAnchorId],
                PayloadJson: "{\"direction\":\"supports\"}",
                ExpectedVersionToken: "v1",
                RequiresHumanReview: true);

            var first = await authority.SubmitAsync(command);
            var second = await authority.SubmitAsync(command);

            Assert.Equal(first.CommandId, second.CommandId);
            Assert.True(first.ReviewRequired);
            Assert.True(second.ReviewRequired);
            Assert.Contains("Idempotent replay", second.Warnings[0], StringComparison.Ordinal);

            await using var dbContext = await factory.CreateDbContextAsync();
            Assert.Equal(1, await dbContext.Set<CognitiveMemoryMutationCommandRecord>().CountAsync());
            Assert.Equal(1, await dbContext.Set<CognitiveMemoryMutationAuditEventRecord>().CountAsync());
        }
    }

    private static async Task<(PostgresTestDatabaseLease Database, CognitiveMemoryMutationAuthority Authority, TestDbContextFactory Factory)> CreateMutationAuthorityAsync()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);

        var database = PostgresTestDatabaseLease.Create("cognitivememoryneurofoundationmutationtests");
        var options = database.CreateAppDbContextOptions();
        var factory = new TestDbContextFactory(options);

        await using var dbContext = await factory.CreateDbContextAsync();
        await dbContext.Database.EnsureCreatedAsync();

        return (database, new CognitiveMemoryMutationAuthority(factory, new FixedClock()), factory);
    }

    private static void AssertEntityTable<TEntity>(IReadOnlyList<IEntityType> entityTypes, string tableName)
    {
        var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == typeof(TEntity));
        Assert.Equal(tableName, entityType.GetTableName());
    }

    private static void AssertNeuroFoundationIndexes(IReadOnlyList<IEntityType> entityTypes)
    {
        foreach (var expectation in CognitiveMemoryEfGuardrails.NeuroFoundationIndexExpectations)
        {
            var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == expectation.EntityType);
            Assert.True(
                CognitiveMemoryEfGuardrails.HasExpectedIndex(entityType, expectation),
                $"Missing expected neuro foundation index on {expectation.EntityType.Name}: {string.Join(", ", expectation.PropertyNames)}.");
        }
    }

    private static void AssertEnumStateFieldsAreNotPersistedAsStrings(IReadOnlyList<IEntityType> entityTypes)
    {
        var stateProperties = new Dictionary<Type, string[]>
        {
            [typeof(CognitiveMemoryEvidenceAnchorRecord)] = [nameof(CognitiveMemoryEvidenceAnchorRecord.AnchorKind), nameof(CognitiveMemoryEvidenceAnchorRecord.TrustLevel), nameof(CognitiveMemoryEvidenceAnchorRecord.RedactionState)],
            [typeof(CognitiveMemoryClaimRecord)] = [nameof(CognitiveMemoryClaimRecord.ClaimKind), nameof(CognitiveMemoryClaimRecord.CurrentBeliefState), nameof(CognitiveMemoryClaimRecord.CurrentBeliefBucket), nameof(CognitiveMemoryClaimRecord.ValidationState), nameof(CognitiveMemoryClaimRecord.StabilityState)],
            [typeof(CognitiveMemoryClaimEvidenceLinkRecord)] = [nameof(CognitiveMemoryClaimEvidenceLinkRecord.Direction)],
            [typeof(CognitiveMemoryBeliefStateRecord)] = [nameof(CognitiveMemoryBeliefStateRecord.StateKind), nameof(CognitiveMemoryBeliefStateRecord.ProjectionBucket)],
            [typeof(CognitiveMemoryEntityRecord)] = [nameof(CognitiveMemoryEntityRecord.EntityKind), nameof(CognitiveMemoryEntityRecord.ConfidenceBucket)],
            [typeof(CognitiveMemoryContextFrameRecord)] = [nameof(CognitiveMemoryContextFrameRecord.FrameKind), nameof(CognitiveMemoryContextFrameRecord.ConfidenceBucket)],
            [typeof(CognitiveMemoryContextFrameDimensionRecord)] = [nameof(CognitiveMemoryContextFrameDimensionRecord.DimensionKind)],
            [typeof(CognitiveMemoryContextBoundaryRecord)] = [nameof(CognitiveMemoryContextBoundaryRecord.BoundaryKind), nameof(CognitiveMemoryContextBoundaryRecord.BoundaryPolicy)],
            [typeof(CognitiveMemoryMutationCommandRecord)] = [nameof(CognitiveMemoryMutationCommandRecord.CommandKind), nameof(CognitiveMemoryMutationCommandRecord.Status), nameof(CognitiveMemoryMutationCommandRecord.ActorKind)],
            [typeof(CognitiveMemoryMutationAuditEventRecord)] = [nameof(CognitiveMemoryMutationAuditEventRecord.EventKind)]
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
}
