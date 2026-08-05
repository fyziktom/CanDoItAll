using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureTaskEditCompensationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock)
{
    public async Task<ProjectStructureNode> RestorePricingAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskEditState expectedCurrentState,
        ProjectStructureTaskEditState stateToRestore,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskNodeId);
        ArgumentNullException.ThrowIfNull(expectedCurrentState);
        ArgumentNullException.ThrowIfNull(stateToRestore);

        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(
            dbContext,
            cancellationToken);
        await using var mutationScope =
            await ProjectStructureSerializableMutationScope.BeginBindingWriteAsync(
                dbContext,
                ProjectStructureSerializableMutationScope.ForProject(projectId),
                cancellationToken);
        var task = await dbContext.Set<ProjectObjectRecord>()
            .FirstOrDefaultAsync(
                item =>
                    item.ProjectId == projectId &&
                    item.NodeKey == taskNodeId &&
                    !item.IsSystemManaged,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Task '{taskNodeId}' disappeared while its previous pricing was being restored.");
        if (!ProjectStructureCanonicalTaskMutationPolicy.IsTask(
                task.ObjectType,
                task.ObjectSubtype))
        {
            throw new InvalidOperationException(
                $"Node '{taskNodeId}' is no longer an editable canonical task.");
        }

        await ProjectNodeBindingStorage.LoadAsync(
            dbContext,
            [task],
            cancellationToken);
        var metadata = ProjectObjectMetadataSerializer.Parse(task.MetadataJson);
        if (ProjectStructureTaskEditStatePolicy.Read(metadata) !=
            expectedCurrentState)
        {
            throw new InvalidOperationException(
                "The task changed while its previous assignee was being restored. Its previous pricing was not applied.");
        }

        ProjectStructureTaskEditStatePolicy.WritePricingAndExecution(
            metadata,
            stateToRestore.Estimate,
            stateToRestore.Execution,
            stateToRestore.CostBasis);
        ProjectObjectMetadataSerializer.Validate(
            task.ObjectType,
            task.ObjectSubtype,
            metadata);
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
