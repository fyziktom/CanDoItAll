using CanDoItAll.Components.Gantt;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench.CanvasAdapters;

public sealed class ProjectStructureGanttProjectionAdapter
{
    private const string TaskSubtype = "task";

    public ProjectStructureGanttProjectionResult Build(
        ProjectStructureSurface surface,
        IReadOnlyCollection<ProjectPartyAssignmentDetail> partyAssignments,
        ProjectStructureGanttProjectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(partyAssignments);
        ArgumentNullException.ThrowIfNull(options);

        var issues = new List<ProjectStructureGanttProjectionIssue>();
        var taskNodes = surface.Nodes
            .Where(IsCanonicalTask)
            .ToArray();
        ValidateTaskIdentities(taskNodes, issues);
        if (HasErrors(issues))
        {
            return Invalid(issues);
        }

        var schedules = new Dictionary<GanttTaskId, ProjectedTaskSchedule>();
        foreach (var taskNode in taskNodes)
        {
            var taskId = new GanttTaskId(taskNode.Id);
            var schedule = BuildSchedule(taskNode, taskId, options, issues);
            if (schedule is not null)
            {
                schedules.Add(taskId, schedule);
            }
        }

        if (HasErrors(issues))
        {
            return Invalid(issues);
        }

        var dependencies = BuildDependencies(surface.Links, schedules.Keys, issues);
        if (HasErrors(issues))
        {
            return Invalid(issues);
        }

        var taskOrder = taskNodes.Select(node => new GanttTaskId(node.Id)).ToArray();
        var validation = ValidateDependencyGraph(taskOrder, schedules, dependencies, issues);
        if (validation is null)
        {
            return Invalid(issues);
        }

        ApplyDependencyScheduleConstraints(schedules, dependencies, validation, issues);
        if (HasErrors(issues))
        {
            return Invalid(issues);
        }

        var nodesById = surface.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Id))
            .GroupBy(node => node.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var taskNodesById = taskNodes.ToDictionary(node => new GanttTaskId(node.Id));
        var tasks = validation.TopologicalOrder
            .Select(taskId =>
            {
                var node = taskNodesById[taskId];
                var schedule = schedules[taskId];
                return new GanttTask(
                    taskId,
                    node.Title,
                    schedule.Start,
                    schedule.End,
                    BuildAssignments(surface, node, nodesById, partyAssignments, issues));
            })
            .ToArray();
        var projectionOnlyTaskIds = schedules
            .Where(pair => pair.Value.Kind != ScheduleProjectionKind.Canonical)
            .Select(pair => pair.Key);

        return new ProjectStructureGanttProjectionResult(
            tasks,
            dependencies,
            projectionOnlyTaskIds,
            issues);
    }

    private static void ValidateTaskIdentities(
        IReadOnlyCollection<ProjectStructureNode> taskNodes,
        ICollection<ProjectStructureGanttProjectionIssue> issues)
    {
        foreach (var taskNode in taskNodes.Where(node => string.IsNullOrWhiteSpace(node.Id)))
        {
            issues.Add(Error(
                ProjectStructureGanttProjectionIssueCode.InvalidTaskId,
                "A canonical task has no project node identifier."));
        }

        foreach (var duplicate in taskNodes
                     .Where(node => !string.IsNullOrWhiteSpace(node.Id))
                     .GroupBy(node => node.Id, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            var taskId = new GanttTaskId(duplicate.Key);
            issues.Add(Error(
                ProjectStructureGanttProjectionIssueCode.DuplicateTaskId,
                $"Canonical task identifier '{taskId}' is duplicated.",
                taskId));
        }

        foreach (var taskNode in taskNodes.Where(node => string.IsNullOrWhiteSpace(node.Title)))
        {
            GanttTaskId? taskId = string.IsNullOrWhiteSpace(taskNode.Id)
                ? null
                : new GanttTaskId(taskNode.Id);
            issues.Add(Error(
                ProjectStructureGanttProjectionIssueCode.InvalidTaskTitle,
                "A canonical task has no title.",
                taskId));
        }
    }

    private static ProjectedTaskSchedule? BuildSchedule(
        ProjectStructureNode node,
        GanttTaskId taskId,
        ProjectStructureGanttProjectionOptions options,
        ICollection<ProjectStructureGanttProjectionIssue> issues)
    {
        if (node.DurationSeconds is <= 0)
        {
            issues.Add(Error(
                ProjectStructureGanttProjectionIssueCode.InvalidTaskDuration,
                $"Task '{taskId}' has a non-positive persisted duration.",
                taskId));
            return null;
        }

        if (node.StartUtc is { } persistedStart && node.EndUtc is { } persistedEnd)
        {
            var start = persistedStart.ToUniversalTime();
            var end = persistedEnd.ToUniversalTime();
            if (end <= start)
            {
                issues.Add(Error(
                    ProjectStructureGanttProjectionIssueCode.InvalidTaskSchedule,
                    $"Task '{taskId}' must end after it starts.",
                    taskId));
                return null;
            }

            if (node.DurationSeconds is { } durationSeconds &&
                Math.Abs((end - start).TotalSeconds - durationSeconds) >= 0.5)
            {
                issues.Add(Warning(
                    ProjectStructureGanttProjectionIssueCode.CanonicalDurationMismatch,
                    $"Task '{taskId}' persists a duration that differs from its start/end interval; the interval is displayed.",
                    taskId));
            }

            return new ProjectedTaskSchedule(start, end, ScheduleProjectionKind.Canonical);
        }

        var duration = node.DurationSeconds is { } persistedDurationSeconds
            ? TimeSpan.FromSeconds(persistedDurationSeconds)
            : options.DefaultTaskDuration;

        try
        {
            if (node.StartUtc is { } startOnly)
            {
                var start = startOnly.ToUniversalTime();
                issues.Add(Warning(
                    ProjectStructureGanttProjectionIssueCode.ScheduleEndSynthesized,
                    $"Task '{taskId}' has no persisted end; its display end is synthesized from the explicit projection duration.",
                    taskId));
                return new ProjectedTaskSchedule(
                    start,
                    start.Add(duration),
                    ScheduleProjectionKind.EndSynthesized);
            }

            if (node.EndUtc is { } endOnly)
            {
                var end = endOnly.ToUniversalTime();
                issues.Add(Warning(
                    ProjectStructureGanttProjectionIssueCode.ScheduleStartSynthesized,
                    $"Task '{taskId}' has no persisted start; its display start is synthesized from the explicit projection duration.",
                    taskId));
                return new ProjectedTaskSchedule(
                    end.Subtract(duration),
                    end,
                    ScheduleProjectionKind.StartSynthesized);
            }

            issues.Add(Warning(
                ProjectStructureGanttProjectionIssueCode.ScheduleSynthesized,
                $"Task '{taskId}' has no persisted schedule; its display interval is synthesized from the explicit projection origin and dependencies.",
                taskId));
            return new ProjectedTaskSchedule(
                options.ProjectionOriginUtc,
                options.ProjectionOriginUtc.Add(duration),
                ScheduleProjectionKind.IntervalSynthesized);
        }
        catch (ArgumentOutOfRangeException)
        {
            issues.Add(Error(
                ProjectStructureGanttProjectionIssueCode.InvalidTaskSchedule,
                $"Task '{taskId}' cannot be projected within the supported date range.",
                taskId));
            return null;
        }
    }

    private static IReadOnlyList<GanttDependency> BuildDependencies(
        IReadOnlyCollection<ProjectStructureLink> links,
        IEnumerable<GanttTaskId> taskIds,
        ICollection<ProjectStructureGanttProjectionIssue> issues)
    {
        var taskIdByNodeKey = taskIds.ToDictionary(taskId => taskId.Value, StringComparer.Ordinal);
        var dependencies = new List<GanttDependency>();
        var dependencyIds = new HashSet<GanttDependencyId>();
        var dependencyEdges = new HashSet<DependencyEdge>();

        foreach (var link in links.Where(link => link.IsUserAuthored && link.Kind == ProjectObjectLinkKind.DependsOn))
        {
            var hasSuccessor = taskIdByNodeKey.TryGetValue(link.SourceId, out var successorId);
            var hasPredecessor = taskIdByNodeKey.TryGetValue(link.TargetId, out var predecessorId);
            if (!hasSuccessor && !hasPredecessor)
            {
                continue;
            }

            if (!hasSuccessor || !hasPredecessor)
            {
                issues.Add(Warning(
                    ProjectStructureGanttProjectionIssueCode.DependencyEndpointNotTask,
                    "A task dependency references a non-task project node and is not included in the Gantt projection.",
                    hasSuccessor ? successorId : predecessorId));
                continue;
            }

            if (predecessorId == successorId)
            {
                issues.Add(Error(
                    ProjectStructureGanttProjectionIssueCode.SelfDependency,
                    $"Task '{successorId}' cannot depend on itself.",
                    successorId));
                continue;
            }

            if (link.RecordId is not { } recordId || recordId == Guid.Empty)
            {
                issues.Add(Error(
                    ProjectStructureGanttProjectionIssueCode.MissingDependencyRecordId,
                    $"The persisted dependency from '{predecessorId}' to '{successorId}' has no record identifier.",
                    successorId,
                    predecessorId));
                continue;
            }

            var dependencyId = ProjectStructureGanttMutationConventions.DependencyId(recordId);
            if (!dependencyIds.Add(dependencyId))
            {
                issues.Add(Error(
                    ProjectStructureGanttProjectionIssueCode.DuplicateDependencyId,
                    $"Dependency identifier '{dependencyId}' is duplicated.",
                    successorId,
                    predecessorId,
                    dependencyId));
                continue;
            }

            var edge = new DependencyEdge(predecessorId, successorId);
            if (!dependencyEdges.Add(edge))
            {
                issues.Add(Error(
                    ProjectStructureGanttProjectionIssueCode.DuplicateDependency,
                    $"Dependency from '{predecessorId}' to '{successorId}' is duplicated.",
                    successorId,
                    predecessorId,
                    dependencyId));
                continue;
            }

            dependencies.Add(new GanttDependency(dependencyId, predecessorId, successorId));
        }

        return Array.AsReadOnly(dependencies.ToArray());
    }

    private static GanttDagValidationResult? ValidateDependencyGraph(
        IReadOnlyCollection<GanttTaskId> taskOrder,
        IReadOnlyDictionary<GanttTaskId, ProjectedTaskSchedule> schedules,
        IReadOnlyCollection<GanttDependency> dependencies,
        ICollection<ProjectStructureGanttProjectionIssue> issues)
    {
        var tasks = taskOrder
            .Select(taskId => new GanttTask(taskId, taskId.Value, schedules[taskId].Start, schedules[taskId].End))
            .ToArray();
        try
        {
            return GanttDagValidator.Validate(tasks, dependencies);
        }
        catch (GanttScheduleException exception) when (exception.Code == GanttScheduleErrorCode.CycleDetected)
        {
            issues.Add(Error(
                ProjectStructureGanttProjectionIssueCode.DependencyCycle,
                "The canonical task dependency graph contains a cycle."));
            return null;
        }
    }

    private static void ApplyDependencyScheduleConstraints(
        IDictionary<GanttTaskId, ProjectedTaskSchedule> schedules,
        IReadOnlyCollection<GanttDependency> dependencies,
        GanttDagValidationResult validation,
        ICollection<ProjectStructureGanttProjectionIssue> issues)
    {
        var predecessorsBySuccessor = dependencies
            .GroupBy(dependency => dependency.SuccessorId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(dependency => dependency.PredecessorId).ToArray());

        foreach (var taskId in validation.TopologicalOrder)
        {
            if (!predecessorsBySuccessor.TryGetValue(taskId, out var predecessorIds))
            {
                continue;
            }

            var latestPredecessorId = predecessorIds.MaxBy(predecessorId => schedules[predecessorId].End);
            var requiredStart = schedules[latestPredecessorId].End;
            var schedule = schedules[taskId];
            if (schedule.Kind == ScheduleProjectionKind.IntervalSynthesized && schedule.Start < requiredStart)
            {
                try
                {
                    schedules[taskId] = schedule with
                    {
                        Start = requiredStart,
                        End = requiredStart.Add(schedule.End - schedule.Start)
                    };
                    continue;
                }
                catch (ArgumentOutOfRangeException)
                {
                    issues.Add(Error(
                        ProjectStructureGanttProjectionIssueCode.InvalidTaskSchedule,
                        $"Task '{taskId}' cannot be projected after predecessor '{latestPredecessorId}' within the supported date range.",
                        taskId,
                        latestPredecessorId));
                    continue;
                }
            }

            if (schedule.Start < requiredStart)
            {
                issues.Add(Error(
                    ProjectStructureGanttProjectionIssueCode.DependencyScheduleConflict,
                    $"Task '{taskId}' starts before predecessor '{latestPredecessorId}' ends.",
                    taskId,
                    latestPredecessorId));
            }
        }
    }

    private static IReadOnlyList<GanttAssignment> BuildAssignments(
        ProjectStructureSurface surface,
        ProjectStructureNode taskNode,
        IReadOnlyDictionary<string, ProjectStructureNode> nodesById,
        IReadOnlyCollection<ProjectPartyAssignmentDetail> partyAssignments,
        ICollection<ProjectStructureGanttProjectionIssue> issues)
    {
        var taskId = new GanttTaskId(taskNode.Id);
        var assignments = new List<ProjectedAssignment>();
        var processNodeIds = surface.Links
            .Where(link =>
                link.IsUserAuthored &&
                link.Kind == ProjectObjectLinkKind.Uses &&
                string.Equals(link.SourceId, taskNode.Id, StringComparison.Ordinal))
            .Select(link => link.TargetId)
            .Distinct(StringComparer.Ordinal);
        foreach (var processNodeId in processNodeIds)
        {
            if (!nodesById.TryGetValue(processNodeId, out var processNode) ||
                processNode.ObjectType != ProjectObjectType.ProcessDefinition)
            {
                continue;
            }

            AddAssignment(
                assignments,
                issues,
                taskId,
                GanttAssignmentKind.Process,
                processNode.Title,
                new AssignmentIdentity(GanttAssignmentKind.Process, processNode.Id));
        }

        foreach (var workflowNode in surface.Nodes.Where(node =>
                     node.ObjectType == ProjectObjectType.WorkflowDefinition &&
                     string.Equals(node.ParentId, taskNode.Id, StringComparison.Ordinal) &&
                     !node.IsSystemManaged))
        {
            AddAssignment(
                assignments,
                issues,
                taskId,
                GanttAssignmentKind.Workflow,
                workflowNode.Title,
                new AssignmentIdentity(GanttAssignmentKind.Workflow, workflowNode.Id));
        }

        foreach (var partyAssignment in partyAssignments.Where(assignment =>
                     assignment.ProjectId == surface.ProjectId &&
                     assignment.Role == ProjectPartyAssignmentRole.WorkItemAssignee &&
                     string.Equals(assignment.NodeKey, taskNode.Id, StringComparison.Ordinal)))
        {
            if (!TryResolvePartyAssignmentKind(partyAssignment.PartyType, out var assignmentKind))
            {
                issues.Add(Warning(
                    ProjectStructureGanttProjectionIssueCode.InvalidAssignment,
                    $"Task '{taskId}' has an unsupported assignee party type and that decoration is omitted.",
                    taskId));
                continue;
            }

            AddAssignment(
                assignments,
                issues,
                taskId,
                assignmentKind,
                partyAssignment.PartyDisplayName,
                new AssignmentIdentity(assignmentKind, partyAssignment.PartyId.ToString("N")));
        }

        return assignments
            .GroupBy(assignment => assignment.Identity)
            .Select(group => group.First())
            .OrderBy(assignment => assignment.Kind)
            .ThenBy(assignment => assignment.Name, StringComparer.OrdinalIgnoreCase)
            .Select(assignment => new GanttAssignment(assignment.Kind, assignment.Name))
            .ToArray();
    }

    private static void AddAssignment(
        ICollection<ProjectedAssignment> assignments,
        ICollection<ProjectStructureGanttProjectionIssue> issues,
        GanttTaskId taskId,
        GanttAssignmentKind kind,
        string name,
        AssignmentIdentity identity)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            issues.Add(Warning(
                ProjectStructureGanttProjectionIssueCode.InvalidAssignment,
                $"Task '{taskId}' has an unnamed {kind} assignment and that decoration is omitted.",
                taskId));
            return;
        }

        assignments.Add(new ProjectedAssignment(kind, name.Trim(), identity));
    }

    private static bool TryResolvePartyAssignmentKind(
        ProjectPartyType partyType,
        out GanttAssignmentKind assignmentKind)
    {
        switch (partyType)
        {
            case ProjectPartyType.AiAgent:
                assignmentKind = GanttAssignmentKind.Agent;
                return true;
            case ProjectPartyType.Person:
                assignmentKind = GanttAssignmentKind.Person;
                return true;
            default:
                assignmentKind = default;
                return false;
        }
    }

    private static bool IsCanonicalTask(ProjectStructureNode node)
    {
        return node.ObjectType == ProjectObjectType.WorkItem &&
            string.Equals(node.ObjectSubtype, TaskSubtype, StringComparison.OrdinalIgnoreCase) &&
            !node.IsSystemManaged;
    }

    private static bool HasErrors(IEnumerable<ProjectStructureGanttProjectionIssue> issues)
    {
        return issues.Any(issue => issue.Severity == ProjectStructureGanttProjectionIssueSeverity.Error);
    }

    private static ProjectStructureGanttProjectionResult Invalid(
        IEnumerable<ProjectStructureGanttProjectionIssue> issues)
    {
        return new ProjectStructureGanttProjectionResult([], [], [], issues);
    }

    private static ProjectStructureGanttProjectionIssue Error(
        ProjectStructureGanttProjectionIssueCode code,
        string message,
        GanttTaskId? taskId = null,
        GanttTaskId? relatedTaskId = null,
        GanttDependencyId? dependencyId = null)
    {
        return new ProjectStructureGanttProjectionIssue(
            code,
            ProjectStructureGanttProjectionIssueSeverity.Error,
            message,
            taskId,
            relatedTaskId,
            dependencyId);
    }

    private static ProjectStructureGanttProjectionIssue Warning(
        ProjectStructureGanttProjectionIssueCode code,
        string message,
        GanttTaskId? taskId = null,
        GanttTaskId? relatedTaskId = null,
        GanttDependencyId? dependencyId = null)
    {
        return new ProjectStructureGanttProjectionIssue(
            code,
            ProjectStructureGanttProjectionIssueSeverity.Warning,
            message,
            taskId,
            relatedTaskId,
            dependencyId);
    }

    private enum ScheduleProjectionKind
    {
        Canonical,
        StartSynthesized,
        EndSynthesized,
        IntervalSynthesized
    }

    private sealed record ProjectedTaskSchedule(
        DateTimeOffset Start,
        DateTimeOffset End,
        ScheduleProjectionKind Kind);

    private sealed record ProjectedAssignment(
        GanttAssignmentKind Kind,
        string Name,
        AssignmentIdentity Identity);

    private readonly record struct AssignmentIdentity(
        GanttAssignmentKind Kind,
        string Value);

    private readonly record struct DependencyEdge(
        GanttTaskId PredecessorId,
        GanttTaskId SuccessorId);
}
