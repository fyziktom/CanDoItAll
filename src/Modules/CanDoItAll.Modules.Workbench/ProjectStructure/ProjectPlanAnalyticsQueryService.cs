using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectPlanAnalyticsQueryService
{
    private readonly IDbContextFactory<AppDbContext> dbContextFactory;
    private readonly IProjectPartyIntegrationBridge partyIntegrationBridge;
    private readonly ProjectPlanSummaryCalculator calculator;
    private readonly ProjectPlanAnalyticsLimits limits;

    public ProjectPlanAnalyticsQueryService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IProjectPartyIntegrationBridge partyIntegrationBridge,
        ProjectPlanSummaryCalculator calculator)
        : this(
            dbContextFactory,
            partyIntegrationBridge,
            calculator,
            ProjectPlanAnalyticsLimits.Default)
    {
    }

    internal ProjectPlanAnalyticsQueryService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IProjectPartyIntegrationBridge partyIntegrationBridge,
        ProjectPlanSummaryCalculator calculator,
        ProjectPlanAnalyticsLimits limits)
    {
        this.dbContextFactory = dbContextFactory;
        this.partyIntegrationBridge = partyIntegrationBridge;
        this.calculator = calculator;
        this.limits = limits.Validate();
    }

    public async Task<ProjectPlanSummary> GetSummaryAsync(
        Guid projectId,
        ProjectPlanSummaryQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        EnsureProjectId(projectId);
        var summaries = await GetSummariesAsync([projectId], query, cancellationToken);
        return summaries[0];
    }

    public async Task<IReadOnlyList<ProjectPlanSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> projectIds,
        ProjectPlanSummaryQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var distinctProjectIds = NormalizeProjectIds(projectIds);
        ValidateQuery(query);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var projectNames = await dbContext.Set<Project>()
            .AsNoTracking()
            .Where(project => distinctProjectIds.Contains(project.Id))
            .ToDictionaryAsync(project => project.Id, project => project.Name, cancellationToken);
        if (projectNames.Count != distinctProjectIds.Length)
        {
            var missingProjectId = distinctProjectIds.First(projectId => !projectNames.ContainsKey(projectId));
            throw new ProjectStructureAgentException(
                404,
                "ProjectNotFound",
                $"Project '{missingProjectId:D}' was not found.");
        }

        _ = await PreflightAsync(
            dbContext,
            distinctProjectIds,
            cancellationToken);
        var nodes = await BuildPlanNodeQuery(dbContext, distinctProjectIds)
            .OrderBy(static node => node.ProjectId)
            .ThenBy(static node => node.NodeKey)
            .Select(node => new ProjectPlanNodeProjection(
                node.ProjectId,
                node.NodeKey,
                node.ParentNodeKey,
                node.ObjectType,
                node.ObjectType == ProjectObjectType.WorkItem ? node.Title : string.Empty,
                node.ObjectType == ProjectObjectType.WorkItem ? node.Status : string.Empty,
                node.ObjectType == ProjectObjectType.WorkItem ? node.ProgressPercent : ProjectProgressPolicy.UntrackedPercent,
                node.ObjectType == ProjectObjectType.WorkItem ? node.StartUtc : null,
                node.ObjectType == ProjectObjectType.WorkItem ? node.EndUtc : null,
                node.ObjectType == ProjectObjectType.WorkItem ? node.MetadataJson : "{}"))
            .Take(limits.MaximumNodeCount + 1)
            .ToListAsync(cancellationToken);
        EnsureMaterializedPayloadWithinLimits(
            distinctProjectIds.Length,
            nodes.Count,
            linkCount: null);
        var links = await BuildPlanLinkQuery(dbContext, distinctProjectIds)
            .OrderBy(static link => link.ProjectId)
            .ThenBy(static link => link.SourceNodeKey)
            .ThenBy(static link => link.TargetNodeKey)
            .ThenBy(static link => link.LinkKind)
            .Select(link => new ProjectPlanLinkProjection(
                link.ProjectId,
                link.SourceNodeKey,
                link.TargetNodeKey,
                link.LinkKind))
            .Take(limits.MaximumLinkCount + 1)
            .ToListAsync(cancellationToken);
        EnsureMaterializedPayloadWithinLimits(
            distinctProjectIds.Length,
            nodes.Count,
            links.Count);
        var assignments = await partyIntegrationBridge.ListWorkItemAssigneeBindingsAsync(
            distinctProjectIds,
            cancellationToken);
        var nodesByProject = nodes.ToLookup(static node => node.ProjectId);
        var linksByProject = links.ToLookup(static link => link.ProjectId);
        var assignmentsByProject = assignments.ToLookup(static assignment => assignment.ProjectId);
        var summaries = new List<ProjectPlanSummary>(distinctProjectIds.Length);
        foreach (var projectId in distinctProjectIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var summary = calculator.Build(
                BuildSnapshot(
                    projectId,
                    projectNames[projectId],
                    nodesByProject[projectId].ToArray(),
                    linksByProject[projectId]
                        .Select(static link => new ProjectPlanLinkFact(
                            link.SourceNodeId,
                            link.TargetNodeId,
                            link.Kind))
                        .ToArray(),
                    assignmentsByProject[projectId].ToArray()),
                query,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            summaries.Add(summary);
        }

        return summaries;
    }

    public async Task<IReadOnlyList<ProjectPlanManagerSummary>> GetManagerSummariesAsync(
        IReadOnlyCollection<Guid> projectIds,
        ProjectPlanManagerSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        var distinctProjectIds = NormalizeProjectIds(projectIds);
        ValidateManagerQuery(query);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var projectNames = await LoadProjectNamesAsync(
            dbContext,
            distinctProjectIds,
            cancellationToken);

        return query.Mode switch
        {
            ProjectPlanManagerSummaryMode.ScheduleOnly =>
                await LoadManagerScheduleSummariesAsync(
                    dbContext,
                    distinctProjectIds,
                    projectNames,
                    query,
                    cancellationToken),
            ProjectPlanManagerSummaryMode.ScheduleAndRemainingCosts =>
                await LoadManagerForecastSummariesAsync(
                    dbContext,
                    distinctProjectIds,
                    projectNames,
                    query,
                    cancellationToken),
            _ => throw new ArgumentOutOfRangeException(
                nameof(query),
                query.Mode,
                "The manager plan summary mode is not supported.")
        };
    }

    public async Task<ProjectPlanAnalyticsPreflight> PreflightAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default)
    {
        var distinctProjectIds = NormalizeProjectIds(projectIds);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await PreflightAsync(
            dbContext,
            distinctProjectIds,
            cancellationToken);
    }

    public async Task<ProjectPlanAnalyticsPreflight> PreflightManagerSummaryAsync(
        IReadOnlyCollection<Guid> projectIds,
        ProjectPlanManagerSummaryMode mode,
        CancellationToken cancellationToken = default)
    {
        var distinctProjectIds = NormalizeProjectIds(projectIds);
        ValidateManagerQuery(new ProjectPlanManagerSummaryQuery(mode));
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var taskCount = await BuildCanonicalTaskQuery(dbContext, distinctProjectIds)
            .LongCountAsync(cancellationToken);
        if (taskCount > limits.MaximumNodeCount)
        {
            throw CreatePayloadLimitExceeded(
                distinctProjectIds.Length,
                taskCount,
                linkCount: null);
        }

        if (mode == ProjectPlanManagerSummaryMode.ScheduleOnly)
        {
            return BuildPreflight(
                distinctProjectIds.Length,
                taskCount,
                linkCount: 0);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var workflowBindingCount = await BuildWorkflowBindingNodeQuery(
                dbContext,
                distinctProjectIds)
            .LongCountAsync(cancellationToken);
        var nodeCount = checked(taskCount + workflowBindingCount);
        if (nodeCount > limits.MaximumNodeCount)
        {
            throw CreatePayloadLimitExceeded(
                distinctProjectIds.Length,
                nodeCount,
                linkCount: null);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var processBindingCount = await BuildProcessResourceLinkQuery(
                dbContext,
                distinctProjectIds)
            .LongCountAsync(cancellationToken);
        return BuildPreflight(
            distinctProjectIds.Length,
            nodeCount,
            processBindingCount);
    }

    private async Task<IReadOnlyList<ProjectPlanManagerSummary>> LoadManagerScheduleSummariesAsync(
        AppDbContext dbContext,
        Guid[] projectIds,
        IReadOnlyDictionary<Guid, string> projectNames,
        ProjectPlanManagerSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var tasks = await BuildCanonicalTaskQuery(dbContext, projectIds)
            .OrderBy(static task => task.ProjectId)
            .ThenBy(static task => task.NodeKey)
            .Select(static task => new ProjectPlanScheduleProjection(
                task.ProjectId,
                task.StartUtc,
                task.EndUtc))
            .Take(limits.MaximumNodeCount + 1)
            .ToArrayAsync(cancellationToken);
        EnsureMaterializedPayloadWithinLimits(
            projectIds.Length,
            tasks.Length,
            linkCount: 0);

        var tasksByProject = tasks.ToLookup(static task => task.ProjectId);
        var summaries = new List<ProjectPlanManagerSummary>(projectIds.Length);
        foreach (var projectId in projectIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = new ProjectPlanManagerScheduleSnapshot(
                projectId,
                projectNames[projectId],
                tasksByProject[projectId]
                    .Select(static task => new ProjectPlanScheduleTaskFact(
                        task.StartUtc,
                        task.EndUtc))
                    .ToArray());
            summaries.Add(calculator.BuildManagerSummary(snapshot, query, cancellationToken));
        }

        return summaries;
    }

    private async Task<IReadOnlyList<ProjectPlanManagerSummary>> LoadManagerForecastSummariesAsync(
        AppDbContext dbContext,
        Guid[] projectIds,
        IReadOnlyDictionary<Guid, string> projectNames,
        ProjectPlanManagerSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var tasks = await BuildCanonicalTaskQuery(dbContext, projectIds)
            .OrderBy(static task => task.ProjectId)
            .ThenBy(static task => task.NodeKey)
            .Select(static task => new ProjectPlanManagerTaskProjection(
                task.ProjectId,
                task.NodeKey,
                task.Status,
                task.ProgressPercent,
                task.StartUtc,
                task.EndUtc,
                task.MetadataJson))
            .Take(limits.MaximumNodeCount + 1)
            .ToArrayAsync(cancellationToken);
        EnsureMaterializedPayloadWithinLimits(
            projectIds.Length,
            tasks.Length,
            linkCount: null);

        var remainingNodeCapacity = limits.MaximumNodeCount - tasks.Length;
        var workflowBindings = await BuildWorkflowBindingNodeQuery(dbContext, projectIds)
            .OrderBy(static node => node.ProjectId)
            .ThenBy(static node => node.NodeKey)
            .Select(static node => new ProjectPlanWorkflowBindingProjection(
                node.ProjectId,
                node.ParentNodeKey!,
                node.NodeKey))
            .Take(remainingNodeCapacity + 1)
            .ToArrayAsync(cancellationToken);
        EnsureMaterializedPayloadWithinLimits(
            projectIds.Length,
            tasks.Length + workflowBindings.Length,
            linkCount: null);

        var processBindings = await BuildProcessResourceLinkQuery(dbContext, projectIds)
            .OrderBy(static link => link.ProjectId)
            .ThenBy(static link => link.SourceNodeKey)
            .ThenBy(static link => link.TargetNodeKey)
            .Select(static link => new ProjectPlanProcessBindingProjection(
                link.ProjectId,
                link.SourceNodeKey,
                link.TargetNodeKey))
            .Take(limits.MaximumLinkCount + 1)
            .ToArrayAsync(cancellationToken);
        EnsureMaterializedPayloadWithinLimits(
            projectIds.Length,
            tasks.Length + workflowBindings.Length,
            processBindings.Length);

        var assigneeBindings = await partyIntegrationBridge.ListWorkItemAssigneeBindingsAsync(
            projectIds,
            cancellationToken);
        var tasksByProject = tasks.ToLookup(static task => task.ProjectId);
        var workflowsByProject = workflowBindings.ToLookup(static binding => binding.ProjectId);
        var processesByProject = processBindings.ToLookup(static binding => binding.ProjectId);
        var assigneesByProject = assigneeBindings.ToLookup(static binding => binding.ProjectId);
        var summaries = new List<ProjectPlanManagerSummary>(projectIds.Length);
        foreach (var projectId in projectIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var projectTasks = tasksByProject[projectId].ToArray();
            var taskIds = projectTasks
                .Select(static task => task.NodeId)
                .ToHashSet(StringComparer.Ordinal);
            var resourceBindings = BuildAssigneeBindings(
                projectId,
                taskIds,
                assigneesByProject[projectId].ToArray());
            foreach (var workflow in workflowsByProject[projectId])
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (taskIds.Contains(workflow.TaskNodeId))
                {
                    resourceBindings.Add(new ProjectPlanResourceBindingFact(
                        workflow.TaskNodeId,
                        ProjectPlanResourceGroup.Workflow,
                        workflow.WorkflowNodeId));
                }
            }

            foreach (var process in processesByProject[projectId])
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (taskIds.Contains(process.TaskNodeId))
                {
                    resourceBindings.Add(new ProjectPlanResourceBindingFact(
                        process.TaskNodeId,
                        ProjectPlanResourceGroup.Process,
                        process.ProcessNodeId));
                }
            }

            var snapshot = new ProjectPlanManagerForecastSnapshot(
                projectId,
                projectNames[projectId],
                projectTasks
                    .Select(static task => new ProjectPlanTaskFact(
                        task.NodeId,
                        Title: string.Empty,
                        task.Status,
                        task.ProgressPercent,
                        task.StartUtc,
                        task.EndUtc,
                        task.MetadataJson))
                    .ToArray(),
                resourceBindings);
            summaries.Add(calculator.BuildManagerSummary(snapshot, query, cancellationToken));
        }

        return summaries;
    }

    private static async Task<IReadOnlyDictionary<Guid, string>> LoadProjectNamesAsync(
        AppDbContext dbContext,
        Guid[] projectIds,
        CancellationToken cancellationToken)
    {
        var projectNames = await dbContext.Set<Project>()
            .AsNoTracking()
            .Where(project => projectIds.Contains(project.Id))
            .ToDictionaryAsync(project => project.Id, project => project.Name, cancellationToken);
        if (projectNames.Count == projectIds.Length)
        {
            return projectNames;
        }

        var missingProjectId = projectIds.First(projectId => !projectNames.ContainsKey(projectId));
        throw new ProjectStructureAgentException(
            404,
            "ProjectNotFound",
            $"Project '{missingProjectId:D}' was not found.");
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
                surface.ProjectId,
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
            case ProjectPartyType.Organization:
            case ProjectPartyType.OrganizationUnit:
                group = ProjectPlanResourceGroup.External;
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

    private static IQueryable<ProjectObjectRecord> BuildCanonicalTaskQuery(
        AppDbContext dbContext,
        IReadOnlyCollection<Guid> projectIds)
    {
        return dbContext.Set<ProjectObjectRecord>()
            .AsNoTracking()
            .Where(node =>
                projectIds.Contains(node.ProjectId) &&
                !node.IsSystemManaged &&
                node.ObjectType == ProjectObjectType.WorkItem &&
                node.ObjectSubtype == ProjectObjectSubtypePolicy.Task);
    }

    private static IQueryable<ProjectObjectRecord> BuildWorkflowBindingNodeQuery(
        AppDbContext dbContext,
        IReadOnlyCollection<Guid> projectIds)
    {
        var canonicalTasks = BuildCanonicalTaskQuery(dbContext, projectIds);
        return dbContext.Set<ProjectObjectRecord>()
            .AsNoTracking()
            .Where(node =>
                projectIds.Contains(node.ProjectId) &&
                !node.IsSystemManaged &&
                node.ObjectType == ProjectObjectType.WorkflowDefinition &&
                node.ParentNodeKey != null &&
                canonicalTasks.Any(task =>
                    task.ProjectId == node.ProjectId &&
                    task.NodeKey == node.ParentNodeKey));
    }

    private static IQueryable<ProjectObjectRecord> BuildPlanNodeQuery(
        AppDbContext dbContext,
        IReadOnlyCollection<Guid> projectIds)
    {
        var canonicalTasks = BuildCanonicalTaskQuery(dbContext, projectIds);
        return dbContext.Set<ProjectObjectRecord>()
            .AsNoTracking()
            .Where(node =>
                projectIds.Contains(node.ProjectId) &&
                !node.IsSystemManaged &&
                ((node.ObjectType == ProjectObjectType.WorkItem &&
                  node.ObjectSubtype == ProjectObjectSubtypePolicy.Task) ||
                 (node.ObjectType == ProjectObjectType.WorkflowDefinition &&
                  node.ParentNodeKey != null &&
                  canonicalTasks.Any(task =>
                      task.ProjectId == node.ProjectId &&
                      task.NodeKey == node.ParentNodeKey))));
    }

    private static IQueryable<ProjectObjectLinkRecord> BuildPlanLinkQuery(
        AppDbContext dbContext,
        IReadOnlyCollection<Guid> projectIds)
    {
        var canonicalTasks = BuildCanonicalTaskQuery(dbContext, projectIds);
        return dbContext.Set<ProjectObjectLinkRecord>()
            .AsNoTracking()
            .Where(link =>
                projectIds.Contains(link.ProjectId) &&
                !link.IsSystemManaged &&
                (((link.LinkKind == ProjectObjectLinkKind.DependsOn ||
                   link.LinkKind == ProjectObjectLinkKind.Blocks) &&
                  canonicalTasks.Any(task =>
                      task.ProjectId == link.ProjectId &&
                      task.NodeKey == link.SourceNodeKey) &&
                  canonicalTasks.Any(task =>
                      task.ProjectId == link.ProjectId &&
                      task.NodeKey == link.TargetNodeKey)) ||
                 (link.LinkKind == ProjectObjectLinkKind.Uses &&
                  canonicalTasks.Any(task =>
                      task.ProjectId == link.ProjectId &&
                      task.NodeKey == link.SourceNodeKey) &&
                  link.TargetNodeKey.StartsWith(
                      ProjectStructureProcessNodeKeys.ProcessDefinitionPrefix))));
    }

    private static IQueryable<ProjectObjectLinkRecord> BuildProcessResourceLinkQuery(
        AppDbContext dbContext,
        IReadOnlyCollection<Guid> projectIds)
    {
        var canonicalTasks = BuildCanonicalTaskQuery(dbContext, projectIds);
        return dbContext.Set<ProjectObjectLinkRecord>()
            .AsNoTracking()
            .Where(link =>
                projectIds.Contains(link.ProjectId) &&
                !link.IsSystemManaged &&
                link.LinkKind == ProjectObjectLinkKind.Uses &&
                canonicalTasks.Any(task =>
                    task.ProjectId == link.ProjectId &&
                    task.NodeKey == link.SourceNodeKey) &&
                link.TargetNodeKey.StartsWith(
                    ProjectStructureProcessNodeKeys.ProcessDefinitionPrefix));
    }

    private async Task<ProjectPlanAnalyticsPreflight> PreflightAsync(
        AppDbContext dbContext,
        Guid[] projectIds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var nodeCount = await BuildPlanNodeQuery(dbContext, projectIds)
            .LongCountAsync(cancellationToken);
        if (nodeCount > limits.MaximumNodeCount)
        {
            throw CreatePayloadLimitExceeded(
                projectIds.Length,
                nodeCount,
                linkCount: null);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var linkCount = await BuildPlanLinkQuery(dbContext, projectIds)
            .LongCountAsync(cancellationToken);
        if (linkCount > limits.MaximumLinkCount)
        {
            throw CreatePayloadLimitExceeded(
                projectIds.Length,
                nodeCount,
                linkCount);
        }

        return BuildPreflight(projectIds.Length, nodeCount, linkCount);
    }

    private ProjectPlanAnalyticsPreflight BuildPreflight(
        int projectCount,
        long nodeCount,
        long linkCount)
    {
        if (nodeCount > limits.MaximumNodeCount)
        {
            throw CreatePayloadLimitExceeded(
                projectCount,
                nodeCount,
                linkCount: null);
        }
        if (linkCount > limits.MaximumLinkCount)
        {
            throw CreatePayloadLimitExceeded(
                projectCount,
                nodeCount,
                linkCount);
        }

        var warnings = new List<string>(2);
        if (nodeCount >= limits.ConfirmationNodeCount)
        {
            warnings.Add(
                $"The selected scope contains {nodeCount:N0} task-plan nodes. " +
                "Review the scope before loading the manager summary.");
        }

        if (linkCount >= limits.ConfirmationLinkCount)
        {
            warnings.Add(
                $"The selected scope contains {linkCount:N0} task-plan links. " +
                "Review the scope before loading the manager summary.");
        }

        return new ProjectPlanAnalyticsPreflight(
            projectCount,
            nodeCount,
            linkCount,
            warnings);
    }

    private void EnsureMaterializedPayloadWithinLimits(
        int projectCount,
        int nodeCount,
        int? linkCount)
    {
        if (nodeCount > limits.MaximumNodeCount)
        {
            throw CreatePayloadLimitExceeded(
                projectCount,
                nodeCount,
                linkCount: null);
        }
        if (linkCount > limits.MaximumLinkCount)
        {
            throw CreatePayloadLimitExceeded(
                projectCount,
                nodeCount,
                linkCount);
        }
    }

    private Guid[] NormalizeProjectIds(IReadOnlyCollection<Guid> projectIds)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        var distinctProjectIds = projectIds
            .Distinct()
            .Take(limits.MaximumProjectCount + 1)
            .ToArray();
        if (distinctProjectIds.Length == 0)
        {
            throw new ArgumentException(
                "At least one project id is required.",
                nameof(projectIds));
        }

        if (distinctProjectIds.Any(static projectId => projectId == Guid.Empty))
        {
            throw new ArgumentException(
                "Project identifiers cannot contain an empty value.",
                nameof(projectIds));
        }

        if (distinctProjectIds.Length > limits.MaximumProjectCount)
        {
            throw new ProjectStructureAgentException(
                413,
                ProjectPlanAnalyticsErrorCodes.ScopeLimitExceeded,
                $"A plan summary cannot span more than {limits.MaximumProjectCount:N0} projects. " +
                "Select the current project or a smaller subtree.",
                new ProjectPlanAnalyticsLimitDetails(
                    distinctProjectIds.Length,
                    PlanNodeCount: null,
                    PlanLinkCount: null,
                    limits.MaximumProjectCount,
                    limits.MaximumNodeCount,
                    limits.MaximumLinkCount));
        }

        return distinctProjectIds;
    }

    private ProjectStructureAgentException CreatePayloadLimitExceeded(
        int projectCount,
        long nodeCount,
        long? linkCount)
    {
        var observedLinks = linkCount.HasValue
            ? $"{linkCount.Value:N0}"
            : "not counted because the node limit was already exceeded";
        return new ProjectStructureAgentException(
            413,
            ProjectPlanAnalyticsErrorCodes.PayloadLimitExceeded,
            $"The selected plan contains {nodeCount:N0} relevant nodes and {observedLinks} relevant links. " +
            $"The hard safety limits are {limits.MaximumNodeCount:N0} nodes and " +
            $"{limits.MaximumLinkCount:N0} links. Reduce the project task graph or select a smaller scope.",
            new ProjectPlanAnalyticsLimitDetails(
                projectCount,
                nodeCount,
                linkCount,
                limits.MaximumProjectCount,
                limits.MaximumNodeCount,
                limits.MaximumLinkCount));
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

    private static void ValidateManagerQuery(ProjectPlanManagerSummaryQuery query)
    {
        try
        {
            ProjectPlanSummaryCalculator.ValidateManagerQuery(query);
        }
        catch (ArgumentException exception)
        {
            throw new ProjectStructureAgentException(
                400,
                "PlanSummaryQueryInvalid",
                exception.Message);
        }
    }

    private sealed record ProjectPlanScheduleProjection(
        Guid ProjectId,
        DateTimeOffset? StartUtc,
        DateTimeOffset? EndUtc);

    private sealed record ProjectPlanManagerTaskProjection(
        Guid ProjectId,
        string NodeId,
        string Status,
        int ProgressPercent,
        DateTimeOffset? StartUtc,
        DateTimeOffset? EndUtc,
        string MetadataJson);

    private sealed record ProjectPlanWorkflowBindingProjection(
        Guid ProjectId,
        string TaskNodeId,
        string WorkflowNodeId);

    private sealed record ProjectPlanProcessBindingProjection(
        Guid ProjectId,
        string TaskNodeId,
        string ProcessNodeId);

    private sealed record ProjectPlanNodeProjection(
        Guid ProjectId,
        string NodeId,
        string? ParentNodeId,
        ProjectObjectType ObjectType,
        string Title,
        string Status,
        int ProgressPercent,
        DateTimeOffset? StartUtc,
        DateTimeOffset? EndUtc,
        string MetadataJson);

    private sealed record ProjectPlanLinkProjection(
        Guid ProjectId,
        string SourceNodeId,
        string TargetNodeId,
        ProjectObjectLinkKind Kind);
}

internal sealed record ProjectPlanAnalyticsLimits(
    int ConfirmationNodeCount,
    int ConfirmationLinkCount,
    int MaximumProjectCount,
    int MaximumNodeCount,
    int MaximumLinkCount)
{
    public static ProjectPlanAnalyticsLimits Default { get; } = new(
        ProjectPlanAnalyticsPayloadPolicy.ConfirmationNodeCount,
        ProjectPlanAnalyticsPayloadPolicy.ConfirmationLinkCount,
        ProjectPlanAnalyticsPayloadPolicy.MaximumProjectCount,
        ProjectPlanAnalyticsPayloadPolicy.MaximumNodeCount,
        ProjectPlanAnalyticsPayloadPolicy.MaximumLinkCount);

    public ProjectPlanAnalyticsLimits Validate()
    {
        if (ConfirmationNodeCount < 0 || ConfirmationNodeCount > MaximumNodeCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ConfirmationNodeCount),
                ConfirmationNodeCount,
                "The node confirmation threshold must be between zero and the maximum node count.");
        }

        if (ConfirmationLinkCount < 0 || ConfirmationLinkCount > MaximumLinkCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ConfirmationLinkCount),
                ConfirmationLinkCount,
                "The link confirmation threshold must be between zero and the maximum link count.");
        }

        if (MaximumProjectCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumProjectCount),
                MaximumProjectCount,
                "The maximum project count must be positive.");
        }

        if (MaximumNodeCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumNodeCount),
                MaximumNodeCount,
                "The maximum node count must be positive.");
        }

        if (MaximumLinkCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumLinkCount),
                MaximumLinkCount,
                "The maximum link count must be positive.");
        }

        return this;
    }
}
