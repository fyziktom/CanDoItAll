using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CanDoItAll.Tests.Integration;

public sealed class CognitiveMemoryScoreGeometryPersistenceModelTests
{
    [Fact]
    public async Task ScoreGeometryPersistenceModel_IndexesQueryCriticalTraceAndComponentFields()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var entityTypes = dbContext.Model.GetEntityTypes().ToList();
        AssertEntityTable<CognitiveMemoryScoreEvaluationTraceRecord>(entityTypes, "CognitiveMemory_ScoreEvaluations");
        AssertEntityTable<CognitiveMemoryScoreComponentRecord>(entityTypes, "CognitiveMemory_ScoreComponents");
        AssertScoreGeometryIndexes(entityTypes);
        AssertEnumStateFieldsAreNotPersistedAsStrings(entityTypes);
    }

    private static void AssertEntityTable<TEntity>(IReadOnlyList<IEntityType> entityTypes, string tableName)
    {
        var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == typeof(TEntity));
        Assert.Equal(tableName, entityType.GetTableName());
    }

    private static void AssertScoreGeometryIndexes(IReadOnlyList<IEntityType> entityTypes)
    {
        foreach (var expectation in CognitiveMemoryEfGuardrails.ScoreGeometryIndexExpectations)
        {
            var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == expectation.EntityType);
            Assert.True(
                CognitiveMemoryEfGuardrails.HasExpectedIndex(entityType, expectation),
                $"Missing expected score geometry index on {expectation.EntityType.Name}: {string.Join(", ", expectation.PropertyNames)}.");
        }
    }

    private static void AssertEnumStateFieldsAreNotPersistedAsStrings(IReadOnlyList<IEntityType> entityTypes)
    {
        var stateProperties = new Dictionary<Type, string[]>
        {
            [typeof(CognitiveMemoryScoreEvaluationTraceRecord)] =
            [
                nameof(CognitiveMemoryScoreEvaluationTraceRecord.OwnerKind),
                nameof(CognitiveMemoryScoreEvaluationTraceRecord.SpaceKind),
                nameof(CognitiveMemoryScoreEvaluationTraceRecord.ScalarProjectionKind),
                nameof(CognitiveMemoryScoreEvaluationTraceRecord.ProjectionBucket)
            ],
            [typeof(CognitiveMemoryScoreComponentRecord)] =
            [
                nameof(CognitiveMemoryScoreComponentRecord.OwnerKind),
                nameof(CognitiveMemoryScoreComponentRecord.SpaceKind),
                nameof(CognitiveMemoryScoreComponentRecord.DimensionKind),
                nameof(CognitiveMemoryScoreComponentRecord.EvidenceKind)
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
}
