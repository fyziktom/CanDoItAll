using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration;

internal static class HistoricalProjectMigrationAssertions {
    internal static async Task<Guid> CurrentMetadataAsync(AppDbContext context, Guid? expectedRevision = null) {
        Assert.Equal(169, context.Model.GetEntityTypes().Count());
        (Type EntityType, string TableName)[] lifecycleEntities = [
            (typeof(DatasetLifecycleRecord), DatasetLifecycleRecord.TableName),
            (typeof(FiniteOperationStateRecord), FiniteOperationStateRecord.TableName),
            (typeof(FiniteOperationEpochRecord), FiniteOperationEpochRecord.TableName),
            (typeof(ProjectDefinitionReceiptRecord), ProjectDefinitionReceiptRecord.TableName),
            (typeof(ProjectTaskPlanReceiptRecord), ProjectTaskPlanReceiptRecord.TableName)
        ];
        foreach (var (entityType, tableName) in lifecycleEntities) {
            Assert.Equal(tableName, context.Model.FindEntityType(entityType)?.GetTableName());
        }
        return await RevisionAsync(context, expectedRevision);
    }

    internal static async Task<Guid> RevisionAsync(AppDbContext context, Guid? expectedRevision = null) {
        var revision = await context.Set<Project>().Select(project => project.ConcurrencyToken).SingleAsync();
        Assert.NotEqual(Guid.Empty, revision);
        if (expectedRevision is { } retained) {
            Assert.Equal(retained, revision);
        }
        return revision;
    }
}
