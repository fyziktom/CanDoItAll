using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using CanDoItAll.Tests.Support;
namespace CanDoItAll.Tests.Integration;

public sealed class CognitiveMemoryPersistenceModelTests
{
    [Fact]
    public async Task CognitiveMemoryEntityConfigurations_AreDiscoveredFromModuleAssembly()
    {
        Assert.Contains(
            ModuleAssemblies.All,
            assembly => assembly == typeof(CognitiveMemoryModuleAssemblyMarker).Assembly);

        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);

        await using var database = PostgresTestDatabaseLease.Create("cognitivememorypersistencemodeltests");

        var options = database.CreateAppDbContextOptions();

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var entityTypes = dbContext.Model.GetEntityTypes().ToList();
        AssertEntityTable<CognitiveMemorySourceManifestRecord>(entityTypes, "CognitiveMemory_SourceManifests");
        AssertEntityTable<CognitiveMemorySourceItemRecord>(entityTypes, "CognitiveMemory_SourceItems");
        AssertEntityTable<CognitiveMemoryRecord>(entityTypes, "CognitiveMemory_Records");
        AssertEntityTable<CognitiveMemoryRelationRecord>(entityTypes, "CognitiveMemory_Relations");
        AssertEntityTable<CognitiveMemoryProjectionStateRecord>(entityTypes, "CognitiveMemory_ProjectionStates");
        AssertEntityTable<CognitiveMemoryRecallTraceRecord>(entityTypes, "CognitiveMemory_RecallTraces");
        AssertEntityTable<CognitiveMemoryReviewItemRecord>(entityTypes, "CognitiveMemory_ReviewItems");
        AssertEntityTable<CognitiveMemoryRunRecord>(entityTypes, "CognitiveMemory_Runs");
        AssertEntityTable<CognitiveMemoryAutomationSettingsRecord>(entityTypes, "CognitiveMemory_AutomationSettings");
        AssertEntityTable<CognitiveMemoryExternalSourceIngestionRecord>(entityTypes, "CognitiveMemory_ExternalSourceIngestions");
        AssertFoundationIndexes(entityTypes);
        AssertEnumStateFieldsAreNotPersistedAsStrings(entityTypes);

        Assert.DoesNotContain(
            typeof(AppDbContext).GetProperties(),
            property => property.PropertyType.IsGenericType &&
                        property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>) &&
                        property.PropertyType.GetGenericArguments()[0].Namespace == typeof(CognitiveMemoryRecord).Namespace);
    }

    private static void AssertEntityTable<TEntity>(IReadOnlyList<IEntityType> entityTypes, string tableName)
    {
        var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == typeof(TEntity));
        Assert.Equal(tableName, entityType.GetTableName());
    }

    private static void AssertFoundationIndexes(IReadOnlyList<IEntityType> entityTypes)
    {
        foreach (var expectation in CognitiveMemoryEfGuardrails.FoundationIndexExpectations)
        {
            var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == expectation.EntityType);
            Assert.True(
                CognitiveMemoryEfGuardrails.HasExpectedIndex(entityType, expectation),
                $"Missing expected index on {expectation.EntityType.Name}: {string.Join(", ", expectation.PropertyNames)}.");
        }
    }

    private static void AssertEnumStateFieldsAreNotPersistedAsStrings(IReadOnlyList<IEntityType> entityTypes)
    {
        var stateProperties = new Dictionary<Type, string[]>
        {
            [typeof(CognitiveMemorySourceManifestRecord)] = [nameof(CognitiveMemorySourceManifestRecord.ScanStatus)],
            [typeof(CognitiveMemoryRecord)] =
            [
                nameof(CognitiveMemoryRecord.Kind),
                nameof(CognitiveMemoryRecord.Origin),
                nameof(CognitiveMemoryRecord.ValidationState),
                nameof(CognitiveMemoryRecord.StabilityState),
                nameof(CognitiveMemoryRecord.CreatedInMode),
                nameof(CognitiveMemoryRecord.AccessLevel),
                nameof(CognitiveMemoryRecord.RiskLevel)
            ],
            [typeof(CognitiveMemoryProjectionStateRecord)] = [nameof(CognitiveMemoryProjectionStateRecord.ProjectionKind), nameof(CognitiveMemoryProjectionStateRecord.Status)],
            [typeof(CognitiveMemoryRecallTraceRecord)] = [nameof(CognitiveMemoryRecallTraceRecord.OperationMode), nameof(CognitiveMemoryRecallTraceRecord.Outcome)],
            [typeof(CognitiveMemoryReviewItemRecord)] = [nameof(CognitiveMemoryReviewItemRecord.ReviewKind), nameof(CognitiveMemoryReviewItemRecord.Status), nameof(CognitiveMemoryReviewItemRecord.SubjectKind), nameof(CognitiveMemoryReviewItemRecord.RiskLevel)],
            [typeof(CognitiveMemoryRunRecord)] = [nameof(CognitiveMemoryRunRecord.RunKind), nameof(CognitiveMemoryRunRecord.Status), nameof(CognitiveMemoryRunRecord.OperationMode)],
            [typeof(CognitiveMemoryAutomationSettingsRecord)] = [nameof(CognitiveMemoryAutomationSettingsRecord.ScheduleMode)],
            [typeof(CognitiveMemoryExternalSourceIngestionRecord)] = [nameof(CognitiveMemoryExternalSourceIngestionRecord.SourceKind), nameof(CognitiveMemoryExternalSourceIngestionRecord.Status)]
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
