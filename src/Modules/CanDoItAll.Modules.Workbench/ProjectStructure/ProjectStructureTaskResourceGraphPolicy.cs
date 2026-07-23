using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureTaskResourceGraphPolicy
{
    public static bool IsResourceChildType(ProjectObjectType objectType)
        => objectType == ProjectObjectType.WorkflowDefinition;

    public static Task<bool> IsAttachedAsync(
        AppDbContext dbContext,
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceSelection resource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(resource);
        return resource.Kind switch
        {
            ProjectStructureTaskResourceKind.Workflow =>
                IsWorkflowAttachedAsync(
                    dbContext,
                    projectId,
                    taskNodeId,
                    resource,
                    cancellationToken),
            ProjectStructureTaskResourceKind.Process =>
                dbContext.Set<ProjectObjectLinkRecord>().AnyAsync(
                    link =>
                        link.ProjectId == projectId &&
                        link.SourceNodeKey == taskNodeId &&
                        link.TargetNodeKey ==
                        ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(
                            resource.ResourceId) &&
                        link.LinkKind == ProjectObjectLinkKind.Uses &&
                        !link.IsSystemManaged,
                    cancellationToken),
            _ => Task.FromResult(true)
        };
    }

    public static async Task ReconcileAfterStructureMutationAsync(
        AppDbContext dbContext,
        Guid projectId,
        IEnumerable<string> candidateTaskNodeIds,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(candidateTaskNodeIds);
        var taskNodeIds = candidateTaskNodeIds
            .Where(static nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (taskNodeIds.Length == 0)
        {
            return;
        }

        var tasks = await dbContext.Set<ProjectObjectRecord>()
            .Where(node =>
                node.ProjectId == projectId &&
                taskNodeIds.Contains(node.NodeKey) &&
                !node.IsSystemManaged)
            .ToListAsync(cancellationToken);
        foreach (var task in tasks)
        {
            if (!ProjectStructureCanonicalTaskMutationPolicy.IsTask(
                    task.ObjectType,
                    task.ObjectSubtype))
            {
                continue;
            }

            var metadata = ProjectObjectMetadataSerializer.Parse(task.MetadataJson);
            var workItem = metadata.WorkItem;
            if (workItem is null ||
                workItem.ExecutionState != ProjectTaskExecutionState.NotStarted ||
                workItem.ExpectedCostBasis is not { } basis)
            {
                continue;
            }

            var resource = ProjectTaskExpectedCostBasisPolicy.ToResource(basis);
            if (await IsAttachedAsync(
                    dbContext,
                    projectId,
                    task.NodeKey,
                    resource,
                    cancellationToken))
            {
                continue;
            }

            workItem.ExpectedCostAmount = null;
            workItem.ExpectedCostCurrencyCode = string.Empty;
            workItem.ExpectedCostBasis = null;
            task.MetadataJson = ProjectObjectMetadataSerializer.Serialize(metadata);
            task.UpdatedAtUtc = updatedAtUtc;
        }
    }

    private static async Task<bool> IsWorkflowAttachedAsync(
        AppDbContext dbContext,
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceSelection resource,
        CancellationToken cancellationToken)
    {
        var candidates = await dbContext.Set<ProjectObjectRecord>()
            .Where(node =>
                node.ProjectId == projectId &&
                node.ParentNodeKey == taskNodeId &&
                node.ObjectType == ProjectObjectType.WorkflowDefinition &&
                !node.IsSystemManaged)
            .Select(node => node.MetadataJson)
            .ToListAsync(cancellationToken);
        return candidates.Any(metadataJson =>
        {
            var workflow = ProjectObjectMetadataSerializer.Parse(metadataJson).Workflow;
            return workflow is not null &&
                workflow.WorkflowId is { } workflowId &&
                workflowId.Value == resource.ResourceId &&
                workflow.WorkflowVersionId is { } workflowVersionId &&
                resource.VersionId.HasValue &&
                workflowVersionId.Value == resource.VersionId.Value;
        });
    }
}
