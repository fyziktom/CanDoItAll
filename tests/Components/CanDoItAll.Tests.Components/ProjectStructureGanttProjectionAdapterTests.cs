using CanDoItAll.Components.Gantt;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureGanttProjectionAdapterTests
{
    private static readonly DateTimeOffset ProjectionOrigin = new(2026, 7, 14, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DependsOn_maps_target_to_predecessor_and_preserves_multiple_dependencies()
    {
        var projectId = Guid.NewGuid();
        var firstDependencyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondDependencyId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var surface = CreateSurface(
            projectId,
            [
                CreateTask("task-c", "C", ProjectionOrigin.AddHours(3), ProjectionOrigin.AddHours(4)),
                CreateTask("task-b", "B", ProjectionOrigin, ProjectionOrigin.AddHours(3)),
                CreateTask("task-a", "A", ProjectionOrigin, ProjectionOrigin.AddHours(2)),
                CreateNode("projected-task", ProjectObjectType.WorkItem, "task", "Projected task", isSystemManaged: true)
            ],
            [
                new ProjectStructureLink("task-c", "task-a", ProjectObjectLinkKind.DependsOn, true, firstDependencyId),
                new ProjectStructureLink("task-c", "task-b", ProjectObjectLinkKind.DependsOn, true, secondDependencyId),
                new ProjectStructureLink("task-b", "task-a", ProjectObjectLinkKind.Blocks, true, Guid.NewGuid())
            ]);

        var result = Build(surface);

        Assert.True(result.IsValid);
        Assert.Collection(
            result.Dependencies,
            dependency =>
            {
                Assert.Equal($"project-link:{firstDependencyId:N}", dependency.Id.Value);
                Assert.Equal("task-a", dependency.PredecessorId.Value);
                Assert.Equal("task-c", dependency.SuccessorId.Value);
            },
            dependency =>
            {
                Assert.Equal($"project-link:{secondDependencyId:N}", dependency.Id.Value);
                Assert.Equal("task-b", dependency.PredecessorId.Value);
                Assert.Equal("task-c", dependency.SuccessorId.Value);
            });
        Assert.Equal(2, result.Dependencies.Count);
        Assert.DoesNotContain(result.Tasks, task => task.Id == new GanttTaskId("projected-task"));
        Assert.Equal(
            ["task-c", "task-b", "task-a"],
            result.Tasks.Select(task => task.Id.Value));
    }

    [Fact]
    public void Preferred_row_order_controls_rendering_while_dependencies_control_schedule_propagation()
    {
        var projectId = Guid.NewGuid();
        var predecessor = CreateTask("task-a", "Predecessor", ProjectionOrigin, ProjectionOrigin.AddHours(2));
        var independent = CreateTask("task-b", "Independent", ProjectionOrigin, ProjectionOrigin.AddHours(1));
        var successor = CreateTask("task-c", "Successor", durationSeconds: 60 * 60);
        var surface = CreateSurface(
            projectId,
            [predecessor, independent, successor],
            [new ProjectStructureLink(successor.Id, predecessor.Id, ProjectObjectLinkKind.DependsOn, true, Guid.NewGuid())]);

        var result = Build(surface, preferredTaskNodeIds: [successor.Id, independent.Id, predecessor.Id]);

        Assert.True(result.IsValid);
        Assert.Equal(
            [successor.Id, independent.Id, predecessor.Id],
            result.Tasks.Select(task => task.Id.Value));
        Assert.Equal(
            (ProjectionOrigin.AddHours(2), ProjectionOrigin.AddHours(3)),
            Dates(result, successor.Id));
    }

    [Fact]
    public void Mermaid_export_keeps_row_order_and_uses_explicit_dates_for_forward_dependency_references()
    {
        var projectId = Guid.NewGuid();
        var predecessor = CreateTask(
            "task-a",
            "Predecessor",
            ProjectionOrigin,
            ProjectionOrigin.AddHours(2));
        var successor = CreateTask("task-b", "Successor", durationSeconds: 60 * 60);
        var surface = CreateSurface(
            projectId,
            [successor, predecessor],
            [new ProjectStructureLink(successor.Id, predecessor.Id, ProjectObjectLinkKind.DependsOn, true, Guid.NewGuid())]);
        var projection = Build(surface);

        var source = ProjectStructureGanttMermaidExporter.Build("Gantt test", projection);
        var taskLines = source
            .Split('\n', StringSplitOptions.TrimEntries)
            .Where(line => line.Contains(" :", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(2, taskLines.Length);
        Assert.StartsWith("Successor", taskLines[0], StringComparison.Ordinal);
        Assert.Contains("2026-07-14 10:00:00", taskLines[0], StringComparison.Ordinal);
        Assert.DoesNotContain("after", taskLines[0], StringComparison.Ordinal);
        Assert.StartsWith("Predecessor", taskLines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Mermaid_export_preserves_start_only_and_end_only_schedules_with_dependencies()
    {
        var projectId = Guid.NewGuid();
        var predecessor = CreateTask(
            "task-a",
            "Predecessor",
            ProjectionOrigin,
            ProjectionOrigin.AddHours(2));
        var startOnly = CreateTask(
            "task-b",
            "Start only",
            start: ProjectionOrigin.AddHours(4),
            durationSeconds: 60 * 60);
        var endOnly = CreateTask(
            "task-c",
            "End only",
            end: ProjectionOrigin.AddHours(8),
            durationSeconds: 2 * 60 * 60);
        var surface = CreateSurface(
            projectId,
            [predecessor, startOnly, endOnly],
            [
                new ProjectStructureLink(startOnly.Id, predecessor.Id, ProjectObjectLinkKind.DependsOn, true, Guid.NewGuid()),
                new ProjectStructureLink(endOnly.Id, predecessor.Id, ProjectObjectLinkKind.DependsOn, true, Guid.NewGuid())
            ]);
        var projection = Build(surface);

        var source = ProjectStructureGanttMermaidExporter.Build("Gantt test", projection);
        var taskLines = source
            .Split('\n', StringSplitOptions.TrimEntries)
            .Where(line => line.Contains(" :", StringComparison.Ordinal))
            .ToArray();

        Assert.True(projection.IsValid);
        Assert.Equal("Start only :task2, 2026-07-14 12:00:00, 1h", taskLines[1]);
        Assert.Equal("End only :task3, 2026-07-14 14:00:00, 2h", taskLines[2]);
    }

    [Fact]
    public void Assignments_include_all_processes_workflow_person_and_agent()
    {
        var projectId = Guid.NewGuid();
        var task = CreateTask("task-a", "Task", ProjectionOrigin, ProjectionOrigin.AddHours(2));
        var firstProcess = CreateNode("process-a", ProjectObjectType.ProcessDefinition, string.Empty, "Build process", isSystemManaged: true);
        var secondProcess = CreateNode("process-b", ProjectObjectType.ProcessDefinition, string.Empty, "Review process", isSystemManaged: true);
        var systemProcess = CreateNode("process-system", ProjectObjectType.ProcessDefinition, string.Empty, "System process", isSystemManaged: true);
        var workflow = CreateNode("workflow-a", ProjectObjectType.WorkflowDefinition, string.Empty, "Release workflow", task.Id);
        var systemWorkflow = CreateNode("workflow-system", ProjectObjectType.WorkflowDefinition, string.Empty, "System workflow", task.Id, isSystemManaged: true);
        var surface = CreateSurface(
            projectId,
            [task, firstProcess, secondProcess, systemProcess, workflow, systemWorkflow],
            [
                new ProjectStructureLink(task.Id, firstProcess.Id, ProjectObjectLinkKind.Uses, true, Guid.NewGuid()),
                new ProjectStructureLink(task.Id, secondProcess.Id, ProjectObjectLinkKind.Uses, true, Guid.NewGuid()),
                new ProjectStructureLink(task.Id, systemProcess.Id, ProjectObjectLinkKind.Uses, false, Guid.NewGuid())
            ]);
        var partyAssignments = new[]
        {
            CreatePartyAssignment(projectId, task.Id, "Grace Hopper", ProjectPartyType.Person, ProjectPartyAssignmentRole.WorkItemAssignee),
            CreatePartyAssignment(projectId, task.Id, "Ada", ProjectPartyType.AiAgent, ProjectPartyAssignmentRole.WorkItemAssignee),
            CreatePartyAssignment(projectId, task.Id, "Ignored reviewer", ProjectPartyType.Person, ProjectPartyAssignmentRole.Reviewer)
        };

        var result = Build(surface, partyAssignments);

        var assignments = Assert.Single(result.Tasks).Assignments;
        Assert.True(result.IsValid);
        Assert.Collection(
            assignments,
            assignment => Assert.Equal(new GanttAssignment(GanttAssignmentKind.Process, "Build process"), assignment),
            assignment => Assert.Equal(new GanttAssignment(GanttAssignmentKind.Process, "Review process"), assignment),
            assignment => Assert.Equal(new GanttAssignment(GanttAssignmentKind.Workflow, "Release workflow"), assignment),
            assignment => Assert.Equal(new GanttAssignment(GanttAssignmentKind.Agent, "Ada"), assignment),
            assignment => Assert.Equal(new GanttAssignment(GanttAssignmentKind.Person, "Grace Hopper"), assignment));
    }

    [Fact]
    public void Organization_assignees_are_known_external_resources_without_a_gantt_decoration()
    {
        var projectId = Guid.NewGuid();
        var task = CreateTask(
            "task-a",
            "External delivery",
            ProjectionOrigin,
            ProjectionOrigin.AddHours(2));
        var surface = CreateSurface(projectId, [task], []);
        var partyAssignments = new[]
        {
            CreatePartyAssignment(
                projectId,
                task.Id,
                "Northwind partner",
                ProjectPartyType.Organization,
                ProjectPartyAssignmentRole.WorkItemAssignee),
            CreatePartyAssignment(
                projectId,
                task.Id,
                "Northwind delivery unit",
                ProjectPartyType.OrganizationUnit,
                ProjectPartyAssignmentRole.WorkItemAssignee)
        };

        var result = Build(surface, partyAssignments);

        Assert.True(result.IsValid);
        Assert.Empty(Assert.Single(result.Tasks).Assignments);
        Assert.DoesNotContain(
            result.Issues,
            issue => issue.Code == ProjectStructureGanttProjectionIssueCode.InvalidAssignment);
    }

    [Fact]
    public void Unknown_assignee_party_type_is_reported()
    {
        var projectId = Guid.NewGuid();
        var task = CreateTask(
            "task-a",
            "Unknown assignment",
            ProjectionOrigin,
            ProjectionOrigin.AddHours(2));
        var surface = CreateSurface(projectId, [task], []);
        var assignment = CreatePartyAssignment(
            projectId,
            task.Id,
            "Unknown resource",
            (ProjectPartyType)999,
            ProjectPartyAssignmentRole.WorkItemAssignee);

        var result = Build(surface, [assignment]);

        Assert.True(result.IsValid);
        var issue = Assert.Single(
            result.Issues,
            issue => issue.Code == ProjectStructureGanttProjectionIssueCode.InvalidAssignment);
        Assert.Equal(new GanttTaskId(task.Id), issue.TaskId);
    }

    [Fact]
    public void Task_metrics_keep_one_week_delivery_separate_from_one_man_day_effort_and_cost()
    {
        var projectId = Guid.NewGuid();
        var metadataJson = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            WorkItem = new ProjectWorkItemMetadata
            {
                WorkItemKind = ProjectWorkItemKind.Task,
                ExpectedEffortHours = 8m,
                ExpectedEffortUnit = ProjectWorkItemEffortUnit.ManDays,
                ExpectedCostAmount = 1400m,
                ExpectedCostCurrencyCode = "usd"
            }
        });
        var task = CreateTask(
            "task-a",
            "Customer acceptance",
            ProjectionOrigin,
            ProjectionOrigin.AddDays(7),
            metadataJson: metadataJson,
            progressPercent: 65);
        var surface = CreateSurface(projectId, [task], []);

        var result = Build(surface);

        Assert.True(result.IsValid);
        var projectedTask = Assert.Single(result.Tasks);
        Assert.Equal(TimeSpan.FromDays(7), projectedTask.Duration);
        Assert.Equal(TimeSpan.FromHours(8), projectedTask.ExpectedEffort);
        Assert.Equal(65, projectedTask.ProgressPercent);
        Assert.Equal(
            new ProjectStructureGanttExpectedCostTotal("USD", 1400m),
            Assert.Single(result.ExpectedCostTotals));
        Assert.DoesNotContain(
            result.Issues,
            issue => issue.Code is ProjectStructureGanttProjectionIssueCode.InvalidTaskEstimate or
                ProjectStructureGanttProjectionIssueCode.InvalidTaskProgress);
    }

    [Fact]
    public void Invalid_estimate_is_reported_and_omitted_without_hiding_valid_progress()
    {
        var projectId = Guid.NewGuid();
        var metadataJson = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            WorkItem = new ProjectWorkItemMetadata
            {
                WorkItemKind = ProjectWorkItemKind.Task,
                ExpectedEffortHours = -2m,
                ExpectedEffortUnit = ProjectWorkItemEffortUnit.Hours,
                ExpectedCostAmount = 20m,
                ExpectedCostCurrencyCode = "USD"
            }
        });
        var task = CreateTask(
            "task-a",
            "Invalid estimate",
            ProjectionOrigin,
            ProjectionOrigin.AddHours(4),
            metadataJson: metadataJson,
            progressPercent: 25);
        var surface = CreateSurface(projectId, [task], []);

        var result = Build(surface);

        Assert.True(result.IsValid);
        var projectedTask = Assert.Single(result.Tasks);
        Assert.Null(projectedTask.ExpectedEffort);
        Assert.Equal(25, projectedTask.ProgressPercent);
        Assert.Empty(result.ExpectedCostTotals);
        var issue = Assert.Single(
            result.Issues,
            issue => issue.Code == ProjectStructureGanttProjectionIssueCode.InvalidTaskEstimate);
        Assert.Equal(ProjectStructureGanttProjectionIssueSeverity.Warning, issue.Severity);
        Assert.Equal(new GanttTaskId(task.Id), issue.TaskId);
    }

    [Fact]
    public void Unknown_progress_maps_to_no_decoration_and_corrupt_progress_is_reported()
    {
        var projectId = Guid.NewGuid();
        var unknown = CreateTask(
            "task-a",
            "Unknown",
            ProjectionOrigin,
            ProjectionOrigin.AddHours(1),
            progressPercent: -1);
        var corrupt = CreateTask(
            "task-b",
            "Corrupt",
            ProjectionOrigin,
            ProjectionOrigin.AddHours(1),
            progressPercent: 101);
        var surface = CreateSurface(projectId, [unknown, corrupt], []);

        var result = Build(surface);

        Assert.True(result.IsValid);
        Assert.All(result.Tasks, task => Assert.Null(task.ProgressPercent));
        var issue = Assert.Single(
            result.Issues,
            issue => issue.Code == ProjectStructureGanttProjectionIssueCode.InvalidTaskProgress);
        Assert.Equal(new GanttTaskId(corrupt.Id), issue.TaskId);
    }

    [Fact]
    public void Missing_and_partial_schedules_are_explicit_projection_only_intervals()
    {
        var projectId = Guid.NewGuid();
        var first = CreateTask("task-a", "First", durationSeconds: null);
        var second = CreateTask("task-b", "Second", durationSeconds: 2 * 60 * 60);
        var startOnly = CreateTask("task-c", "Start only", ProjectionOrigin.AddHours(20), durationSeconds: 2 * 60 * 60);
        var endOnly = CreateTask("task-d", "End only", end: ProjectionOrigin.AddHours(6), durationSeconds: 60 * 60);
        var canonical = CreateTask("task-e", "Canonical", ProjectionOrigin.AddHours(24), ProjectionOrigin.AddHours(25));
        var surface = CreateSurface(
            projectId,
            [first, second, startOnly, endOnly, canonical],
            [
                new ProjectStructureLink(second.Id, first.Id, ProjectObjectLinkKind.DependsOn, true, Guid.NewGuid())
            ]);

        var result = Build(surface);

        Assert.True(result.IsValid);
        Assert.Equal((ProjectionOrigin, ProjectionOrigin.AddHours(8)), Dates(result, first.Id));
        Assert.Equal((ProjectionOrigin.AddHours(8), ProjectionOrigin.AddHours(10)), Dates(result, second.Id));
        Assert.Equal((ProjectionOrigin.AddHours(20), ProjectionOrigin.AddHours(22)), Dates(result, startOnly.Id));
        Assert.Equal((ProjectionOrigin.AddHours(5), ProjectionOrigin.AddHours(6)), Dates(result, endOnly.Id));
        Assert.Equal((ProjectionOrigin.AddHours(24), ProjectionOrigin.AddHours(25)), Dates(result, canonical.Id));
        Assert.Equal(
            new[] { first.Id, second.Id, startOnly.Id, endOnly.Id }.Order(),
            result.ProjectionOnlyTaskIds.Select(taskId => taskId.Value).Order());
        Assert.Equal(2, result.Issues.Count(issue => issue.Code == ProjectStructureGanttProjectionIssueCode.ScheduleSynthesized));
        Assert.Contains(result.Issues, issue => issue.Code == ProjectStructureGanttProjectionIssueCode.ScheduleEndSynthesized);
        Assert.Contains(result.Issues, issue => issue.Code == ProjectStructureGanttProjectionIssueCode.ScheduleStartSynthesized);
        Assert.DoesNotContain(new GanttTaskId(canonical.Id), result.ProjectionOnlyTaskIds);
    }

    [Fact]
    public void Cyclic_canonical_graph_is_rejected_with_a_typed_issue()
    {
        var projectId = Guid.NewGuid();
        var first = CreateTask("task-a", "First", ProjectionOrigin, ProjectionOrigin.AddHours(1));
        var second = CreateTask("task-b", "Second", ProjectionOrigin.AddHours(1), ProjectionOrigin.AddHours(2));
        var surface = CreateSurface(
            projectId,
            [first, second],
            [
                new ProjectStructureLink(first.Id, second.Id, ProjectObjectLinkKind.DependsOn, true, Guid.NewGuid()),
                new ProjectStructureLink(second.Id, first.Id, ProjectObjectLinkKind.DependsOn, true, Guid.NewGuid())
            ]);

        var result = Build(surface);

        Assert.False(result.IsValid);
        Assert.Empty(result.Tasks);
        Assert.Empty(result.Dependencies);
        var issue = Assert.Single(result.Issues, issue => issue.Code == ProjectStructureGanttProjectionIssueCode.DependencyCycle);
        Assert.Equal(ProjectStructureGanttProjectionIssueSeverity.Error, issue.Severity);
    }

    [Fact]
    public void Persisted_dependency_without_record_id_is_rejected()
    {
        var projectId = Guid.NewGuid();
        var first = CreateTask("task-a", "First", ProjectionOrigin, ProjectionOrigin.AddHours(1));
        var second = CreateTask("task-b", "Second", ProjectionOrigin.AddHours(1), ProjectionOrigin.AddHours(2));
        var surface = CreateSurface(
            projectId,
            [first, second],
            [new ProjectStructureLink(second.Id, first.Id, ProjectObjectLinkKind.DependsOn, true)]);

        var result = Build(surface);

        Assert.False(result.IsValid);
        Assert.Empty(result.Tasks);
        var issue = Assert.Single(result.Issues, issue => issue.Code == ProjectStructureGanttProjectionIssueCode.MissingDependencyRecordId);
        Assert.Equal(new GanttTaskId(second.Id), issue.TaskId);
        Assert.Equal(new GanttTaskId(first.Id), issue.RelatedTaskId);
    }

    private static ProjectStructureGanttProjectionResult Build(
        ProjectStructureSurface surface,
        IReadOnlyCollection<ProjectPartyAssignmentDetail>? assignments = null,
        IReadOnlyList<string>? preferredTaskNodeIds = null)
    {
        return new ProjectStructureGanttProjectionAdapter().Build(
            surface,
            assignments ?? [],
            new ProjectStructureGanttProjectionOptions(
                ProjectionOrigin,
                TimeSpan.FromHours(8),
                preferredTaskNodeIds));
    }

    private static ProjectStructureSurface CreateSurface(
        Guid projectId,
        IReadOnlyList<ProjectStructureNode> nodes,
        IReadOnlyList<ProjectStructureLink> links)
    {
        return new ProjectStructureSurface(projectId, "Gantt test", nodes, links, null);
    }

    private static ProjectStructureNode CreateTask(
        string id,
        string title,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        int? durationSeconds = null,
        string metadataJson = "{}",
        int progressPercent = 0)
    {
        return CreateNode(
            id,
            ProjectObjectType.WorkItem,
            "task",
            title,
            startUtc: start,
            endUtc: end,
            durationSeconds: durationSeconds,
            metadataJson: metadataJson,
            progressPercent: progressPercent);
    }

    private static ProjectStructureNode CreateNode(
        string id,
        ProjectObjectType objectType,
        string objectSubtype,
        string title,
        string? parentId = null,
        DateTimeOffset? startUtc = null,
        DateTimeOffset? endUtc = null,
        int? durationSeconds = null,
        bool isSystemManaged = false,
        string metadataJson = "{}",
        int progressPercent = 0)
    {
        return new ProjectStructureNode(
            Id: id,
            ParentId: parentId,
            ObjectType: objectType,
            ObjectSubtype: objectSubtype,
            Title: title,
            Subtitle: string.Empty,
            Status: "Planned",
            Notes: string.Empty,
            Route: string.Empty,
            ArtifactKind: string.Empty,
            ArtifactId: null,
            MediaRelativePath: string.Empty,
            MediaContentType: string.Empty,
            MediaOriginalFileName: string.Empty,
            X: 0,
            Y: 0,
            VisualProfile: new ProjectObjectVisualProfile("pill", "#2563eb", "TK", "Task"),
            Badges: [],
            ProgressMode: string.Empty,
            ProgressPercent: progressPercent,
            MarkerIcon: string.Empty,
            MarkerTone: string.Empty,
            MarkerLabel: string.Empty,
            Markers: [],
            Priority: 0,
            StartUtc: startUtc,
            EndUtc: endUtc,
            MetadataJson: metadataJson,
            DurationSeconds: durationSeconds,
            IsSystemManaged: isSystemManaged);
    }

    private static ProjectPartyAssignmentDetail CreatePartyAssignment(
        Guid projectId,
        string nodeKey,
        string displayName,
        ProjectPartyType partyType,
        ProjectPartyAssignmentRole role)
    {
        return new ProjectPartyAssignmentDetail(
            Guid.NewGuid(),
            projectId,
            Guid.NewGuid(),
            role,
            displayName,
            partyType == ProjectPartyType.AiAgent ? "AI agent" : partyType.ToString(),
            partyType,
            nodeKey,
            false,
            null,
            null,
            null,
            string.Empty,
            string.Empty);
    }

    private static (DateTimeOffset Start, DateTimeOffset End) Dates(
        ProjectStructureGanttProjectionResult result,
        string taskId)
    {
        var task = Assert.Single(result.Tasks, task => task.Id == new GanttTaskId(taskId));
        return (task.Start, task.End);
    }
}
