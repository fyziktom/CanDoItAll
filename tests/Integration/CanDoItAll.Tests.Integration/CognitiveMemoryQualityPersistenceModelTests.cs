using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using CanDoItAll.Tests.Support;
namespace CanDoItAll.Tests.Integration;

public sealed class CognitiveMemoryQualityPersistenceModelTests
{
    [Fact]
    public async Task QualityPersistenceModel_RegistersClustersDreamAggregatesValidationAndSynthesisTables()
    {
        await using var fixture = await CreateFixtureAsync();
        var entityTypes = fixture.DbContext.Model.GetEntityTypes().ToList();

        AssertEntityTable<CognitiveMemoryQualityClusterRecord>(entityTypes, "CognitiveMemory_QualityClusters");
        AssertEntityTable<CognitiveMemoryQualityClusterKeyRecord>(entityTypes, "CognitiveMemory_QualityClusterKeys");
        AssertEntityTable<CognitiveMemoryQualityClusterMemberRecord>(entityTypes, "CognitiveMemory_QualityClusterMembers");
        AssertEntityTable<CognitiveMemoryDreamRunRecord>(entityTypes, "CognitiveMemory_DreamRuns");
        AssertEntityTable<CognitiveMemoryDreamRunClusterRecord>(entityTypes, "CognitiveMemory_DreamRunClusters");
        AssertEntityTable<CognitiveMemoryDreamAggregateCandidateRecord>(entityTypes, "CognitiveMemory_DreamAggregateCandidates");
        AssertEntityTable<CognitiveMemoryDreamAggregateClaimRecord>(entityTypes, "CognitiveMemory_DreamAggregateClaims");
        AssertEntityTable<CognitiveMemoryDreamAggregateClaimSourceMapRecord>(entityTypes, "CognitiveMemory_DreamAggregateClaimSourceMaps");
        AssertEntityTable<CognitiveMemoryDreamValidationRecord>(entityTypes, "CognitiveMemory_DreamValidations");
        AssertEntityTable<CognitiveMemorySynthesizedRecallRecord>(entityTypes, "CognitiveMemory_SynthesizedRecalls");
        AssertEntityTable<CognitiveMemorySynthesizedStatementRecord>(entityTypes, "CognitiveMemory_SynthesizedStatements");
        AssertEntityTable<CognitiveMemorySynthesizedStatementSourceMapRecord>(entityTypes, "CognitiveMemory_SynthesizedStatementSourceMaps");
        AssertEnumStateFieldsAreNotPersistedAsStrings(entityTypes);
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
            [typeof(CognitiveMemoryQualityClusterRecord)] =
            [
                nameof(CognitiveMemoryQualityClusterRecord.PrimaryKeyFamily),
                nameof(CognitiveMemoryQualityClusterRecord.Readiness),
                nameof(CognitiveMemoryQualityClusterRecord.AccessLevel),
                nameof(CognitiveMemoryQualityClusterRecord.RiskLevel)
            ],
            [typeof(CognitiveMemoryDreamRunRecord)] =
            [
                nameof(CognitiveMemoryDreamRunRecord.Mode),
                nameof(CognitiveMemoryDreamRunRecord.TriggerKind),
                nameof(CognitiveMemoryDreamRunRecord.Status)
            ],
            [typeof(CognitiveMemoryDreamAggregateCandidateRecord)] =
            [
                nameof(CognitiveMemoryDreamAggregateCandidateRecord.Mode),
                nameof(CognitiveMemoryDreamAggregateCandidateRecord.Status),
                nameof(CognitiveMemoryDreamAggregateCandidateRecord.AccessLevel),
                nameof(CognitiveMemoryDreamAggregateCandidateRecord.RiskLevel)
            ],
            [typeof(CognitiveMemoryDreamValidationRecord)] =
            [
                nameof(CognitiveMemoryDreamValidationRecord.Decision)
            ]
        };

        foreach (var (entityClrType, propertyNames) in stateProperties)
        {
            var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == entityClrType);
            foreach (var propertyName in propertyNames)
            {
                var property = Assert.IsAssignableFrom<IProperty>(entityType.FindProperty(propertyName));
                Assert.True(property.ClrType.IsEnum || Nullable.GetUnderlyingType(property.ClrType)?.IsEnum == true, $"{entityClrType.Name}.{propertyName} should remain a typed enum.");
                Assert.NotEqual(typeof(string), property.ClrType);
            }
        }
    }

    private static async Task<QualityFixture> CreateFixtureAsync()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        var database = PostgresTestDatabaseLease.Create("cognitivememoryqualitypersistencemodeltests");

        var options = database.CreateAppDbContextOptions();
        var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return new QualityFixture(database, dbContext);
    }

    private sealed class QualityFixture(
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
