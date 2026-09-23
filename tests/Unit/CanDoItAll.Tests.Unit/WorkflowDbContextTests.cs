using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Prompts;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowDbContextTests {
    [Fact]
    public void Runtime_model_contains_all_seventeen_owned_records_and_preserves_distinct_concurrency_protocols() {
        using var context = new WorkflowDbContext(new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseNpgsql("Host=localhost;Database=workflow_owner_model").Options);
        Type[] owned = [typeof(WorkflowDefinitionRecord), typeof(WorkflowDefinitionHeadRecord), typeof(WorkflowComponentRecord),
            typeof(WorkflowSettingsRecord), typeof(WorkflowRunRecordEntity), typeof(WorkflowEventRecordEntity),
            typeof(WorkflowExternalRequestRecordEntity), typeof(WorkflowCheckpointRecordEntity), typeof(WorkflowArtifactRecordEntity),
            typeof(WorkflowLaunchIdempotencyRecordEntity), typeof(WorkflowUsageObservationRecordEntity),
            typeof(WorkflowExecutorInvocationRecordEntity), typeof(WorkflowBackendCheckpointPayloadEntity),
            typeof(WorkflowBackendCheckpointSessionEntity), typeof(WorkflowExternalRequestBoundaryEntity),
            typeof(WorkflowExternalResponseOperationEntity), typeof(WorkflowStructureOutputRecord)];
        Assert.Equal(owned.OrderBy(type => type.Name), context.Model.GetEntityTypes().Select(entity => entity.ClrType).OrderBy(type => type.Name));
        var tokens = context.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties())
            .Where(property => property.IsConcurrencyToken)
            .Select(property => (property.DeclaringType.ClrType, property.Name)).ToArray();
        Assert.Equal(3, tokens.Length);
        Assert.Contains((typeof(WorkflowDefinitionHeadRecord), nameof(WorkflowDefinitionHeadRecord.VersionId)), tokens);
        Assert.Contains((typeof(WorkflowExternalRequestBoundaryEntity), nameof(WorkflowExternalRequestBoundaryEntity.ConcurrencyToken)), tokens);
        Assert.Contains((typeof(WorkflowExternalResponseOperationEntity), nameof(WorkflowExternalResponseOperationEntity.ConcurrencyToken)), tokens);
        Assert.Empty(context.Model.FindEntityType(typeof(WorkflowComponentRecord))!.GetForeignKeys());
        Assert.Throws<InvalidOperationException>(() => context.Set<PromptArtifact>().ToQueryString());
        Assert.Throws<InvalidOperationException>(() => context.Set<PromptVersion>().ToQueryString());
        Assert.Throws<InvalidOperationException>(() => context.Set<Project>().ToQueryString());
        Assert.Throws<InvalidOperationException>(() => context.Set<AgentHistoryLocator>().ToQueryString());
    }

    [Theory]
    [InlineData(SaveOverload.SynchronousDefault)]
    [InlineData(SaveOverload.SynchronousAccept)]
    [InlineData(SaveOverload.SynchronousRetain)]
    [InlineData(SaveOverload.AsynchronousDefault)]
    [InlineData(SaveOverload.AsynchronousAccept)]
    [InlineData(SaveOverload.AsynchronousRetain)]
    public async Task Save_stamps_only_GUID_tokens_and_leaves_definition_heads_and_claim_generations_unchanged(SaveOverload overload) {
        await using var context = new WorkflowDbContext(new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"workflow-owner-tokens-{Guid.NewGuid():N}").Options);
        var supplied = Guid.NewGuid();
        var head = new WorkflowDefinitionHeadRecord { WorkflowId = Guid.NewGuid(), VersionId = Guid.NewGuid() };
        var boundary = new WorkflowExternalRequestBoundaryEntity { RequestId = Guid.NewGuid(), ConcurrencyToken = supplied, RequestVersion = 7 };
        var operation = new WorkflowExternalResponseOperationEntity { Id = Guid.NewGuid(), RequestId = boundary.RequestId, OperationVersion = 13, LeaseEpoch = 17 };
        context.AddRange(head, boundary, operation);
        await SaveAsync();
        var version = head.VersionId;
        Assert.Equal(supplied, boundary.ConcurrencyToken);
        Assert.NotEqual(Guid.Empty, operation.ConcurrencyToken);
        var generated = operation.ConcurrencyToken;
        await SaveAsync();
        Assert.Equal(supplied, boundary.ConcurrencyToken);
        Assert.Equal(generated, operation.ConcurrencyToken);
        boundary.ResponseContractJson = "{\"edited\":true}";
        operation.SafeMessage = "Edited";
        head.ExternalKey = "edited";
        await SaveAsync();
        Assert.NotEqual(supplied, boundary.ConcurrencyToken);
        Assert.NotEqual(generated, operation.ConcurrencyToken);
        Assert.Equal(version, head.VersionId);
        Assert.Equal(7, boundary.RequestVersion);
        Assert.Equal(13, operation.OperationVersion);
        Assert.Equal(17, operation.LeaseEpoch);
        var beforeDelete = operation.ConcurrencyToken;
        context.Remove(operation);
        await SaveAsync();
        Assert.Equal(beforeDelete, operation.ConcurrencyToken);

        async Task SaveAsync() {
            switch (overload) {
                case SaveOverload.SynchronousDefault:
                    context.SaveChanges();
                    break;
                case SaveOverload.SynchronousAccept:
                    context.SaveChanges(true);
                    break;
                case SaveOverload.SynchronousRetain:
                    context.SaveChanges(false);
                    context.ChangeTracker.AcceptAllChanges();
                    break;
                case SaveOverload.AsynchronousDefault:
                    await context.SaveChangesAsync();
                    break;
                case SaveOverload.AsynchronousAccept:
                    await context.SaveChangesAsync(true);
                    break;
                case SaveOverload.AsynchronousRetain:
                    await context.SaveChangesAsync(false);
                    context.ChangeTracker.AcceptAllChanges();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(overload));
            }
        }
    }

    public enum SaveOverload {
        SynchronousDefault,
        SynchronousAccept,
        SynchronousRetain,
        AsynchronousDefault,
        AsynchronousAccept,
        AsynchronousRetain
    }
}
