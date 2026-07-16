using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectPlanAnalyticsQueryService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IProjectPartyIntegrationBridge partyIntegrationBridge,
    ProjectPlanSummaryCalculator calculator)
{
    public async Task<ProjectPlanSummary> GetSummaryAsync(
        Guid projectId,
        ProjectPlanSummaryQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        EnsureProjectId(projectId);
        ValidateQuery(query);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var projectName = await dbContext.Set<Project>()
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => project.Name)
            .SingleOrDefaultAsync(cancellationToken);
        if (projectName is null)
        {
            throw new ProjectStructureAgentException(
                404,
                "ProjectNotFound",
                $"Project '{projectId:D}' was not found.");
        }

        var assignmentsTask = partyIntegrationBridge.ListWorkItemAssigneeBindingsAsync(
            projectId,
            cancellationToken);
        var projectObjects = dbContext.Set<ProjectObjectRecord>();
        var canonicalTaskNodeKeys = projectObjects
            .Where(node =>
                node.ProjectId == projectId &&
                !node.IsSystemManaged &&
                node.ObjectType == ProjectObjectType.WorkItem &&
                node.ObjectSubtype == ProjectObjectSubtypePolicy.Task)
            .Select(node => node.NodeKey);
        var nodes = await projectObjects
            .AsNoTracking()
            .Where(node =>
                node.ProjectId == projectId &&
                !node.IsSystemManaged &&
                ((node.ObjectType == ProjectObjectType.WorkItem && node.ObjectSubtype == ProjectObjectSubtypePolicy.Task) ||
                 (node.ObjectType == ProjectObjectType.WorkflowDefinition &&
                  node.ParentNodeKey != null &&
                  canonicalTaskNodeKeys.Contains(node.ParentNodeKey))))
            .Select(node => new ProjectPlanNodeProjection(
                node.NodeKey,
                node.ParentNodeKey,
                node.ObjectType,
                node.ObjectType == ProjectObjectType.WorkItem ? node.Title : string.Empty,
                node.ObjectType == ProjectObjectType.WorkItem ? node.Status : string.Empty,
                node.ObjectType == ProjectObjectType.WorkItem ? node.ProgressPercent : ProjectProgressPolicy.UntrackedPercent,
                node.ObjectType == ProjectObjectType.WorkItem ? node.StartUtc : null,
                node.ObjectType == ProjectObjectType.WorkItem ? node.EndUtc : null,
                node.ObjectType == ProjectObjectType.WorkItem ? node.MetadataJson : "{}"))
            .ToListAsync(cancellationToken);
        var links = await dbContext.Set<ProjectObjectLinkRecord>()
            .AsNoTracking()
            .Where(link =>
                link.ProjectId == projectId &&
                !link.IsSystemManaged &&
                (((link.LinkKind == ProjectObjectLinkKind.DependsOn ||
                   link.LinkKind == ProjectObjectLinkKind.Blocks) &&
                  canonicalTaskNodeKeys.Contains(link.SourceNodeKey) &&
                  canonicalTaskNodeKeys.Contains(link.TargetNodeKey)) ||
                 (link.LinkKind == ProjectObjectLinkKind.Uses &&
                  canonicalTaskNodeKeys.Contains(link.SourceNodeKey) &&
                  link.TargetNodeKey.StartsWith(ProjectStructureProcessNodeKeys.ProcessDefinitionPrefix))))
            .Select(link => new ProjectPlanLinkFact(
                link.SourceNodeKey,
                link.TargetNodeKey,
                link.LinkKind))
            .ToListAsync(cancellationToken);
        var assignments = await assignmentsTask;

        return calculator.Build(
            BuildSnapshot(projectId, projectName, nodes, links, assignments),
            query);
    }

    public ProjectPlanSummary BuildSummary(
        ProjectStructureSurface surface,
        IReadOnlyCollection<ProjectWorkItemAssigneeBinding> assigneeBindings,
        ProjectPlanSummaryQuery? query = null)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(assigneeBindings);
        ProjectPlanSummaryCalculator.ValidateQuery(query);

        var nodes = new List<ProjectPlanNodeProjection>(surface.Nodes.Count);
        foreach (var node in surface.Nodes)
        {
            if (node.IsSystemManaged ||
                (node.ObjectType != ProjectObjectType.WorkflowDefinition && !IsCanonicalTask(node)))
            {
                continue;
            }

            nodes.Add(new ProjectPlanNodeProjection(
                node.Id,
                node.ParentId,
                node.ObjectType,
                node.Title,
                node.Status,
                node.ProgressPercent,
                node.StartUtc,
                node.EndUtc,
                node.MetadataJson));
        }

        var links = new List<ProjectPlanLinkFact>(surface.Links.Count);
        foreach (var link in surface.Links)
        {
            if (!link.IsUserAuthored ||
                link.Kind is not (ProjectObjectLinkKind.DependsOn or ProjectObjectLinkKind.Blocks or ProjectObjectLinkKind.Uses))
            {
                continue;
            }
            links.Add(new ProjectPlanLinkFact(link.SourceId, link.TargetId, link.Kind));
        }

        return calculator.Build(
            BuildSnapshot(surface.ProjectId, surface.ProjectName, nodes, links, assigneeBindings),
            query);
    }

    private static ProjectPlanSnapshot BuildSnapshot(
        Guid projectId,
        string projectName,
        IReadOnlyList<ProjectPlanNodeProjection> nodes,
        IReadOnlyList<ProjectPlanLinkFact> links,
        IReadOnlyCollection<ProjectWorkItemAssigneeBinding> assigneeBindings)
    {
        var tasks = new List<ProjectPlanTaskFact>(nodes.Count);
        var workflowNodes = new List<ProjectPlanNodeProjection>();
        foreach (var node in nodes)
        {
            if (node.ObjectType == ProjectObjectType.WorkItem)
            {
                tasks.Add(new ProjectPlanTaskFact(
                    node.NodeId,
                    node.Title,
                    node.Status,
                    node.ProgressPercent,
                    node.StartUtc,
                    node.EndUtc,
                    node.MetadataJson));
            }
            else if (node.ObjectType == ProjectObjectType.WorkflowDefinition)
            {
                workflowNodes.Add(node);
            }
        }

        var taskIds = tasks.Select(task => task.NodeId).ToHashSet(StringComparer.Ordinal);
        var bindings = BuildAssigneeBindings(projectId, taskIds, assigneeBindings);

        foreach (var workflowNode in workflowNodes)
        {
            if (workflowNode.ParentNodeId is not null && taskIds.Contains(workflowNode.ParentNodeId))
            {
                bindings.Add(new ProjectPlanResourceBindingFact(
                    workflowNode.ParentNodeId,
                    ProjectPlanResourceGroup.Workflow,
                    workflowNode.NodeId));
            }
        }

        foreach (var link in links)
        {
            if (link.Kind == ProjectObjectLinkKind.Uses &&
                taskIds.Contains(link.SourceNodeId) &&
                link.TargetNodeId.StartsWith(
                    ProjectStructureProcessNodeKeys.ProcessDefinitionPrefix,
                    StringComparison.Ordinal))
            {
                bindings.Add(new ProjectPlanResourceBindingFact(
                    link.SourceNodeId,
                    ProjectPlanResourceGroup.Process,
                    link.TargetNodeId));
            }
        }

        return new ProjectPlanSnapshot(projectId, projectName, tasks, links, bindings);
    }

    internal static List<ProjectPlanResourceBindingFact> BuildAssigneeBindings(
        Guid projectId,
        IReadOnlySet<string> taskIds,
        IReadOnlyCollection<ProjectWorkItemAssigneeBinding> assigneeBindings)
    {
        var bindings = new List<ProjectPlanResourceBindingFact>(assigneeBindings.Count);
        foreach (var assignment in assigneeBindings)
        {
            if (assignment.ProjectId != projectId ||
                !taskIds.Contains(assignment.NodeKey) ||
                !TryMapResourceGroup(assignment.PartyType, out var group))
            {
                continue;
            }

            bindings.Add(new ProjectPlanResourceBindingFact(
                assignment.NodeKey,
                group,
                assignment.PartyId.ToString("D")));
        }

        return bindings;
    }

    private static bool TryMapResourceGroup(
        ProjectPartyType partyType,
        out ProjectPlanResourceGroup group)
    {
        switch (partyType)
        {
            case ProjectPartyType.Person:
                group = ProjectPlanResourceGroup.Person;
                return true;
            case ProjectPartyType.AiAgent:
                group = ProjectPlanResourceGroup.Agent;
                return true;
            default:
                group = default;
                return false;
        }
    }

    private static bool IsCanonicalTask(ProjectStructureNode node)
    {
        return node.ObjectType == ProjectObjectType.WorkItem &&
            string.Equals(node.ObjectSubtype, ProjectObjectSubtypePolicy.Task, StringComparison.Ordinal) &&
            !node.IsSystemManaged;
    }

    private static void EnsureProjectId(Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ProjectStructureAgentException(400, "ProjectIdRequired", "A project id is required.");
        }
    }

    private static void ValidateQuery(ProjectPlanSummaryQuery? query)
    {
        try
        {
            ProjectPlanSummaryCalculator.ValidateQuery(query);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new ProjectStructureAgentException(
                400,
                "PlanSummaryQueryInvalid",
                exception.Message);
        }
    }

    private sealed record ProjectPlanNodeProjection(
        string NodeId,
        string? ParentNodeId,
        ProjectObjectType ObjectType,
        string Title,
        string Status,
        int ProgressPercent,
        DateTimeOffset? StartUtc,
        DateTimeOffset? EndUtc,
        string MetadataJson);
}
