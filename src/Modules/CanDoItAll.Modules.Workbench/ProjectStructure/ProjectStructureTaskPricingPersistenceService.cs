using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureTaskPricingPersistenceService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock)
{
    internal async Task<ProjectStructureNode?> CommitAsync(
        ProjectStructureTaskPricingCommitPlan plan,
        Action<ProjectObjectMetadataEnvelope> metadataMutation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(metadataMutation);

        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(
            dbContext,
            cancellationToken);
        await using var mutationScope =
            await ProjectStructureSerializableMutationScope.BeginAsync(
                dbContext,
                ProjectStructureSerializableMutationScope.ForProject(
                    plan.ProjectId),
            cancellationToken);
        var task = await dbContext.Set<ProjectObjectRecord>()
            .FirstOrDefaultAsync(
                item =>
                    item.ProjectId == plan.ProjectId &&
                    item.NodeKey == plan.TaskNodeId &&
                    !item.IsSystemManaged,
                cancellationToken);
        if (task is null)
        {
            return null;
        }

        if (!ProjectStructureCanonicalTaskMutationPolicy.IsTask(
                task.ObjectType,
                task.ObjectSubtype))
        {
            throw new InvalidOperationException(
                $"Node '{plan.TaskNodeId}' is no longer an editable canonical task.");
        }

        if (!await ProjectStructureTaskResourceGraphPolicy.IsAttachedAsync(
                dbContext,
                plan.ProjectId,
                plan.TaskNodeId,
                plan.Resource,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "The selected task resource is no longer attached. Reload the project before committing its authoritative price.");
        }

        await ProjectNodeBindingStorage.LoadAsync(
            dbContext,
            [task],
            cancellationToken);
        var metadata = ProjectObjectMetadataSerializer.Parse(task.MetadataJson);
        metadataMutation(metadata);
        task.MetadataJson = ProjectWorkbenchObjectModeling.ResolveMetadataJson(
            task.ObjectType,
            task.ObjectSubtype,
            ProjectObjectMetadataSerializer.Serialize(metadata),
            task.MetadataJson,
            task.Notes,
            media: null);
        task.UpdatedAtUtc = clock.GetUtcNow();
        var bindingPlan = await ProjectNodeBindingStorage.PersistAsync(
            dbContext,
            task,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await mutationScope.CommitAsync(cancellationToken);

        ProjectNodeBindingStorage.Apply(task, bindingPlan);
        return ProjectWorkbenchNodeMapper.MapStructureNode(task);
    }
}
