using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureTaskCreationServiceTests
{
    private static readonly DateTimeOffset StartUtc = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_adds_canonical_task_to_single_main_backlog_and_requested_row_order()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var creationService = harness.Context.Services.GetRequiredService<ProjectStructureTaskCreationService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projectsService, "Gantt task creation");

        var first = await creationService.CreateAsync(
            projectId,
            new ProjectStructureTaskCreateRequest("First task", StartUtc, StartUtc.AddHours(8)),
            CreateAgent(projectId));
        var second = await creationService.CreateAsync(
            projectId,
            new ProjectStructureTaskCreateRequest(
                "Second task",
                StartUtc.AddHours(8),
                StartUtc.AddHours(12),
                first.TaskNodeId),
            CreateAgent(projectId));

        var surface = await workbenchService.GetStructureAsync(projectId);
        var backlogs = surface.Nodes.Where(node =>
            node.ObjectType == ProjectObjectType.ProjectBlock &&
            string.Equals(node.ObjectSubtype, "backlog", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(node.Title, "Main", StringComparison.OrdinalIgnoreCase)).ToList();
        var backlog = Assert.Single(backlogs);
        var firstTask = surface.Nodes.Single(node => node.Id == first.TaskNodeId);
        var secondTask = surface.Nodes.Single(node => node.Id == second.TaskNodeId);
        var rowState = await workbenchService.LoadGanttViewStateAsync(projectId);

        Assert.Equal(backlog.Id, first.BacklogNodeId);
        Assert.Equal(backlog.Id, second.BacklogNodeId);
        Assert.Equal(backlog.Id, firstTask.ParentId);
        Assert.Equal(backlog.Id, secondTask.ParentId);
        Assert.Equal(ProjectObjectType.WorkItem, firstTask.ObjectType);
        Assert.Equal("task", firstTask.ObjectSubtype);
        Assert.Equal(StartUtc, firstTask.StartUtc);
        Assert.Equal(StartUtc.AddHours(8), firstTask.EndUtc);
        Assert.Equal(8 * 60 * 60, firstTask.DurationSeconds);
        Assert.Equal([first.TaskNodeId, second.TaskNodeId], rowState.OrderedTaskNodeIds);
    }

    [Fact]
    public async Task Parallel_create_uses_one_main_backlog_and_retains_both_tasks()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var creationService = harness.Context.Services.GetRequiredService<ProjectStructureTaskCreationService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projectsService, "Concurrent Gantt task creation");

        var createFirst = creationService.CreateAsync(
            projectId,
            new ProjectStructureTaskCreateRequest("Parallel first", StartUtc, StartUtc.AddHours(4)),
            CreateAgent(projectId, "first"));
        var createSecond = creationService.CreateAsync(
            projectId,
            new ProjectStructureTaskCreateRequest("Parallel second", StartUtc.AddHours(4), StartUtc.AddHours(8)),
            CreateAgent(projectId, "second"));

        var createdTasks = await Task.WhenAll(createFirst, createSecond);
        var surface = await workbenchService.GetStructureAsync(projectId);
        var backlog = Assert.Single(surface.Nodes, node =>
            node.ObjectType == ProjectObjectType.ProjectBlock &&
            string.Equals(node.ObjectSubtype, "backlog", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(node.Title, "Main", StringComparison.OrdinalIgnoreCase));
        var taskNodeIds = createdTasks.Select(static result => result.TaskNodeId).ToHashSet(StringComparer.Ordinal);
        var retainedTasks = surface.Nodes
            .Where(node => taskNodeIds.Contains(node.Id))
            .ToList();
        var rowState = await workbenchService.LoadGanttViewStateAsync(projectId);

        Assert.Equal(2, retainedTasks.Count);
        Assert.All(retainedTasks, task => Assert.Equal(backlog.Id, task.ParentId));
        Assert.True(taskNodeIds.SetEquals(rowState.OrderedTaskNodeIds));
    }

    [Fact]
    public async Task Missing_resource_compensates_task_and_reports_resource_stage()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var creationService = harness.Context.Services.GetRequiredService<ProjectStructureTaskCreationService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projectsService, "Gantt resource compensation");

        var exception = await Assert.ThrowsAsync<ProjectStructureTaskCreationException>(() =>
            creationService.CreateAsync(
                projectId,
                new ProjectStructureTaskCreateRequest(
                    "Task with missing person",
                    StartUtc,
                    StartUtc.AddHours(1),
                    Resource: new ProjectStructureTaskResourceSelection(
                        ProjectStructureTaskResourceKind.Person,
                        Guid.NewGuid())),
                CreateAgent(projectId)));

        var surface = await workbenchService.GetStructureAsync(projectId);

        Assert.Equal(ProjectStructureTaskCreationFailureStage.ResourceAttachment, exception.Stage);
        Assert.Equal(ProjectStructureTaskCreationErrorCode.ResourceAttachmentFailed, exception.Code);
        Assert.True(exception.CompensationSucceeded);
        Assert.DoesNotContain(surface.Nodes, node => node.Id == exception.TaskNodeId);
        Assert.DoesNotContain(surface.Nodes, node => node.Title == "Task with missing person");
    }

    [Fact]
    public async Task Missing_row_anchor_compensates_task_and_preserves_main_backlog()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var creationService = harness.Context.Services.GetRequiredService<ProjectStructureTaskCreationService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projectsService, "Gantt row compensation");

        var exception = await Assert.ThrowsAsync<ProjectStructureTaskCreationException>(() =>
            creationService.CreateAsync(
                projectId,
                new ProjectStructureTaskCreateRequest(
                    "Task with missing anchor",
                    StartUtc,
                    StartUtc.AddHours(1),
                    "missing-anchor"),
                CreateAgent(projectId)));

        var surface = await workbenchService.GetStructureAsync(projectId);

        Assert.Equal(ProjectStructureTaskCreationFailureStage.RowOrdering, exception.Stage);
        Assert.Equal(ProjectStructureTaskCreationErrorCode.RowOrderingFailed, exception.Code);
        Assert.True(exception.CompensationSucceeded);
        Assert.DoesNotContain(surface.Nodes, node => node.Id == exception.TaskNodeId);
        Assert.Contains(surface.Nodes, node =>
            node.ObjectType == ProjectObjectType.ProjectBlock &&
            string.Equals(node.ObjectSubtype, "backlog", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(node.Title, "Main", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Person_resource_uses_canonical_assignment_and_updates_task_metadata()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var creationService = harness.Context.Services.GetRequiredService<ProjectStructureTaskCreationService>();
        var partyBridge = harness.Context.Services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projectsService, "Gantt person assignment");
        var partyId = await CreatePartyAsync(partyDirectoryService, "Gantt Owner");

        var result = await creationService.CreateAsync(
            projectId,
            new ProjectStructureTaskCreateRequest(
                "Assigned task",
                StartUtc,
                StartUtc.AddHours(4),
                Resource: new ProjectStructureTaskResourceSelection(
                    ProjectStructureTaskResourceKind.Person,
                    partyId)),
            CreateAgent(projectId));

        var assignments = await partyBridge.ListAssignmentsDetailedAsync(projectId);
        var surface = await workbenchService.GetStructureAsync(projectId);
        var task = surface.Nodes.Single(node => node.Id == result.TaskNodeId);
        var metadata = ProjectObjectMetadataSerializer.Parse(task.MetadataJson);

        Assert.Contains(assignments, assignment =>
            assignment.NodeKey == result.TaskNodeId &&
            assignment.PartyId == partyId &&
            assignment.Role == ProjectPartyAssignmentRole.WorkItemAssignee &&
            assignment.IsPrimary);
        Assert.Equal("Gantt Owner", metadata.WorkItem?.AssigneePartyDisplayName);
    }

    [Fact]
    public async Task Agent_resource_uses_canonical_assignment_and_updates_task_metadata()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var creationService = harness.Context.Services.GetRequiredService<ProjectStructureTaskCreationService>();
        var partyBridge = harness.Context.Services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projectsService, "Gantt agent assignment");
        var partyId = await CreatePartyAsync(partyDirectoryService, "Planning agent", PartyType.AiAgent);

        var result = await creationService.CreateAsync(
            projectId,
            new ProjectStructureTaskCreateRequest(
                "Agent task",
                StartUtc,
                StartUtc.AddHours(4),
                Resource: new ProjectStructureTaskResourceSelection(
                    ProjectStructureTaskResourceKind.Agent,
                    partyId)),
            CreateAgent(projectId));

        var assignments = await partyBridge.ListAssignmentsDetailedAsync(projectId);
        var surface = await workbenchService.GetStructureAsync(projectId);
        var task = surface.Nodes.Single(node => node.Id == result.TaskNodeId);
        var metadata = ProjectObjectMetadataSerializer.Parse(task.MetadataJson);

        Assert.Contains(assignments, assignment =>
            assignment.NodeKey == result.TaskNodeId &&
            assignment.PartyId == partyId &&
            assignment.Role == ProjectPartyAssignmentRole.WorkItemAssignee &&
            assignment.IsPrimary);
        Assert.Equal("Planning agent", metadata.WorkItem?.AssigneePartyDisplayName);
    }

    [Fact]
    public async Task Resource_options_do_not_expose_party_email_or_phone_to_ui_search()
    {
        const string rawEmail = "restricted.gantt.owner@example.test";
        const string rawPhone = "+591-7000-1234";
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var resourceService = harness.Context.Services.GetRequiredService<ProjectStructureTaskResourceService>();
        var projectId = await CreateProjectAsync(projectsService, "Gantt resource privacy");
        var partyId = await CreatePartyAsync(
            partyDirectoryService,
            "Restricted Gantt Owner",
            isSensitive: true,
            primaryEmail: rawEmail,
            primaryPhone: rawPhone);

        var options = await resourceService.ListOptionsAsync(projectId);
        var party = Assert.Single(options, option => option.ResourceId == partyId);
        var searchableText = string.Join(
            " ",
            options.Select(static option => $"{option.DisplayName} {option.TypeLabel} {option.Description}"));

        Assert.True(party.IsSensitive);
        Assert.Equal(string.Empty, party.Description);
        Assert.DoesNotContain(rawEmail, searchableText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(rawPhone, searchableText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cancellation_after_party_attachment_compensates_and_rethrows_original_cancellation()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var projectsService = services.GetRequiredService<ProjectsService>();
        var partyDirectoryService = services.GetRequiredService<PartyDirectoryService>();
        var partyBridge = services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var workbenchService = services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projectsService, "Gantt cancellation compensation");
        var partyId = await CreatePartyAsync(partyDirectoryService, "Canceled Gantt Owner");
        using var cancellationSource = new CancellationTokenSource();
        var creationService = CreateTaskCreationService(
            services,
            new CancelAfterAssignmentProjectPartyIntegrationBridge(partyBridge, cancellationSource));

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            creationService.CreateAsync(
                projectId,
                new ProjectStructureTaskCreateRequest(
                    "Canceled assigned task",
                    StartUtc,
                    StartUtc.AddHours(4),
                    Resource: new ProjectStructureTaskResourceSelection(
                        ProjectStructureTaskResourceKind.Person,
                        partyId)),
                CreateAgent(projectId),
                cancellationSource.Token));

        var assignments = await partyBridge.ListAssignmentsDetailedAsync(projectId);
        var surface = await workbenchService.GetStructureAsync(projectId);

        Assert.True(cancellationSource.IsCancellationRequested);
        Assert.Equal(cancellationSource.Token, exception.CancellationToken);
        Assert.DoesNotContain(assignments, assignment => assignment.PartyId == partyId);
        Assert.DoesNotContain(surface.Nodes, node => node.Title == "Canceled assigned task");
    }

    [Fact]
    public async Task Workflow_resource_creates_canonical_child_under_task()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workflowCatalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var creationService = harness.Context.Services.GetRequiredService<ProjectStructureTaskCreationService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projectsService, "Gantt workflow resource");
        var workflow = await CreateWorkflowAsync(workflowCatalogService, "Gantt workflow");

        var result = await creationService.CreateAsync(
            projectId,
            new ProjectStructureTaskCreateRequest(
                "Workflow task",
                StartUtc,
                StartUtc.AddHours(8),
                Resource: new ProjectStructureTaskResourceSelection(
                    ProjectStructureTaskResourceKind.Workflow,
                    workflow.Id.Value,
                    workflow.VersionId.Value)),
            CreateAgent(projectId));

        var surface = await workbenchService.GetStructureAsync(projectId);
        var workflowNode = Assert.Single(surface.Nodes, node =>
            node.ParentId == result.TaskNodeId &&
            node.ObjectType == ProjectObjectType.WorkflowDefinition);
        var metadata = ProjectObjectMetadataSerializer.Parse(workflowNode.MetadataJson);

        Assert.Equal(workflow.Id, metadata.Workflow?.WorkflowId);
        Assert.Equal(workflow.VersionId, metadata.Workflow?.WorkflowVersionId);
        Assert.Equal(workflow.Name, workflowNode.Title);
    }

    [Fact]
    public async Task Process_resource_creates_canonical_uses_link_from_task()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var resourceService = harness.Context.Services.GetRequiredService<ProjectStructureTaskResourceService>();
        var creationService = harness.Context.Services.GetRequiredService<ProjectStructureTaskCreationService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projectsService, "Gantt process resource");
        var process = (await resourceService.ListOptionsAsync(projectId))
            .First(option => option.Kind == ProjectStructureTaskResourceKind.Process);

        var result = await creationService.CreateAsync(
            projectId,
            new ProjectStructureTaskCreateRequest(
                "Process task",
                StartUtc,
                StartUtc.AddHours(8),
                Resource: new ProjectStructureTaskResourceSelection(
                    ProjectStructureTaskResourceKind.Process,
                    process.ResourceId)),
            CreateAgent(projectId));

        var surface = await workbenchService.GetStructureAsync(projectId);
        var link = Assert.Single(surface.Links, link =>
            link.SourceId == result.TaskNodeId &&
            link.Kind == ProjectObjectLinkKind.Uses);
        var processNode = surface.Nodes.Single(node => node.Id == link.TargetId);

        Assert.Equal(ProjectObjectType.ProcessDefinition, processNode.ObjectType);
    }

    [Fact]
    public async Task Row_order_failure_after_agent_attachment_removes_canonical_assignment()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var creationService = harness.Context.Services.GetRequiredService<ProjectStructureTaskCreationService>();
        var partyBridge = harness.Context.Services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projectsService, "Gantt assigned row compensation");
        var partyId = await CreatePartyAsync(partyDirectoryService, "Compensated agent", PartyType.AiAgent);

        var exception = await Assert.ThrowsAsync<ProjectStructureTaskCreationException>(() =>
            creationService.CreateAsync(
                projectId,
                new ProjectStructureTaskCreateRequest(
                    "Compensated assigned task",
                    StartUtc,
                    StartUtc.AddHours(4),
                    AfterTaskNodeId: "missing-anchor",
                    Resource: new ProjectStructureTaskResourceSelection(
                        ProjectStructureTaskResourceKind.Agent,
                        partyId)),
                CreateAgent(projectId)));

        var assignments = await partyBridge.ListAssignmentsDetailedAsync(projectId);
        var surface = await workbenchService.GetStructureAsync(projectId);

        Assert.Equal(ProjectStructureTaskCreationFailureStage.RowOrdering, exception.Stage);
        Assert.True(exception.CompensationSucceeded);
        Assert.DoesNotContain(assignments, assignment => assignment.NodeKey == exception.TaskNodeId);
        Assert.DoesNotContain(surface.Nodes, node => node.Id == exception.TaskNodeId);
    }

    [Fact]
    public async Task Row_order_failure_after_workflow_attachment_removes_task_descendants_and_links()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workflowCatalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var creationService = harness.Context.Services.GetRequiredService<ProjectStructureTaskCreationService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projectsService, "Gantt workflow row compensation");
        var workflow = await CreateWorkflowAsync(workflowCatalogService, "Compensated workflow");

        var exception = await Assert.ThrowsAsync<ProjectStructureTaskCreationException>(() =>
            creationService.CreateAsync(
                projectId,
                new ProjectStructureTaskCreateRequest(
                    "Compensated workflow task",
                    StartUtc,
                    StartUtc.AddHours(4),
                    AfterTaskNodeId: "missing-anchor",
                    Resource: new ProjectStructureTaskResourceSelection(
                        ProjectStructureTaskResourceKind.Workflow,
                        workflow.Id.Value,
                        workflow.VersionId.Value)),
                CreateAgent(projectId)));

        var surface = await workbenchService.GetStructureAsync(projectId);

        Assert.Equal(ProjectStructureTaskCreationFailureStage.RowOrdering, exception.Stage);
        Assert.True(exception.CompensationSucceeded);
        Assert.DoesNotContain(surface.Nodes, node =>
            node.Id == exception.TaskNodeId ||
            node.ParentId == exception.TaskNodeId);
        Assert.DoesNotContain(surface.Links, link =>
            link.SourceId == exception.TaskNodeId ||
            link.TargetId == exception.TaskNodeId);
    }

    [Fact]
    public async Task Row_order_failure_after_process_attachment_removes_task_and_uses_link_but_retains_process_definition()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var resourceService = harness.Context.Services.GetRequiredService<ProjectStructureTaskResourceService>();
        var creationService = harness.Context.Services.GetRequiredService<ProjectStructureTaskCreationService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projectsService, "Gantt process row compensation");
        var process = (await resourceService.ListOptionsAsync(projectId))
            .First(option => option.Kind == ProjectStructureTaskResourceKind.Process);

        var exception = await Assert.ThrowsAsync<ProjectStructureTaskCreationException>(() =>
            creationService.CreateAsync(
                projectId,
                new ProjectStructureTaskCreateRequest(
                    "Compensated process task",
                    StartUtc,
                    StartUtc.AddHours(4),
                    AfterTaskNodeId: "missing-anchor",
                    Resource: new ProjectStructureTaskResourceSelection(
                        ProjectStructureTaskResourceKind.Process,
                        process.ResourceId)),
                CreateAgent(projectId)));

        var surface = await workbenchService.GetStructureAsync(projectId);
        var retainedProcess = Assert.Single(
            await resourceService.ListOptionsAsync(projectId),
            option =>
                option.Kind == ProjectStructureTaskResourceKind.Process &&
                option.ResourceId == process.ResourceId);

        Assert.Equal(ProjectStructureTaskCreationFailureStage.RowOrdering, exception.Stage);
        Assert.True(exception.CompensationSucceeded);
        Assert.DoesNotContain(surface.Nodes, node => node.Id == exception.TaskNodeId);
        Assert.DoesNotContain(surface.Links, link =>
            link.SourceId == exception.TaskNodeId ||
            link.TargetId == exception.TaskNodeId);
        Assert.Equal(process.DisplayName, retainedProcess.DisplayName);
    }

    [Fact]
    public async Task Invalid_dates_and_overlong_title_are_rejected_before_creating_backlog()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var creationService = harness.Context.Services.GetRequiredService<ProjectStructureTaskCreationService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projectsService, "Gantt date validation");

        var dateException = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            creationService.CreateAsync(
                projectId,
                new ProjectStructureTaskCreateRequest("Invalid task", StartUtc, StartUtc),
                CreateAgent(projectId)));
        var titleException = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            creationService.CreateAsync(
                projectId,
                new ProjectStructureTaskCreateRequest(new string('x', 201), StartUtc, StartUtc.AddHours(1)),
                CreateAgent(projectId)));
        var surface = await workbenchService.GetStructureAsync(projectId);

        Assert.Equal("TaskDateRangeInvalid", dateException.ErrorCode);
        Assert.Equal("TaskTitleTooLong", titleException.ErrorCode);
        Assert.DoesNotContain(surface.Nodes, node =>
            node.ObjectType == ProjectObjectType.ProjectBlock &&
            string.Equals(node.ObjectSubtype, "backlog", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(node.Title, "Main", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Planning"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        PartyType partyType = PartyType.Person,
        bool isSensitive = false,
        string primaryEmail = "",
        string primaryPhone = "")
    {
        var contactPoints = new List<PartyContactPointEditorModel>();
        if (!string.IsNullOrWhiteSpace(primaryEmail))
        {
            contactPoints.Add(new PartyContactPointEditorModel
            {
                ContactType = PartyContactType.Email,
                Label = "Primary email",
                Value = primaryEmail,
                IsPrimary = true
            });
        }

        if (!string.IsNullOrWhiteSpace(primaryPhone))
        {
            contactPoints.Add(new PartyContactPointEditorModel
            {
                ContactType = PartyContactType.Phone,
                Label = "Primary phone",
                Value = primaryPhone,
                IsPrimary = true
            });
        }

        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            IsSensitive = isSensitive,
            ContactPoints = contactPoints,
            LastChangedBy = "component-tests"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static ProjectStructureTaskCreationService CreateTaskCreationService(
        IServiceProvider services,
        IProjectPartyIntegrationBridge partyIntegrationBridge)
    {
        var agentService = services.GetRequiredService<ProjectStructureAgentService>();
        var workbenchService = services.GetRequiredService<ProjectWorkbenchService>();
        var assigneeService = new ProjectStructureWorkItemAssigneeService(
            partyIntegrationBridge,
            workbenchService,
            services.GetRequiredService<ILogger<ProjectStructureWorkItemAssigneeService>>());
        var resourceService = new ProjectStructureTaskResourceService(
            assigneeService,
            services.GetRequiredService<IWorkflowCatalogService>(),
            services.GetRequiredService<ProcessDefinitionCatalogProjectionService>(),
            services.GetRequiredService<ProjectStructureWorkflowNodeService>(),
            agentService,
            workbenchService);
        return new ProjectStructureTaskCreationService(
            agentService,
            resourceService,
            services.GetRequiredService<ProjectStructureGanttRowOrderService>(),
            workbenchService,
            services.GetRequiredService<ILogger<ProjectStructureTaskCreationService>>());
    }

    private static Task<WorkflowDefinition> CreateWorkflowAsync(
        IWorkflowCatalogService workflowCatalogService,
        string name)
    {
        var start = new WorkflowNodeId("start");
        var end = new WorkflowNodeId("end");
        return workflowCatalogService.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            name,
            $"{name} description",
            WorkflowLifecycleStatus.Active,
            new WorkflowGraph(
                start,
                [
                    CreateWorkflowNode(start, WorkflowNodeKind.Start),
                    CreateWorkflowNode(end, WorkflowNodeKind.End)
                ],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-to-end"),
                        start,
                        SourcePortId: null,
                        end,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty)
                ]),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)));
    }

    private static WorkflowNode CreateWorkflowNode(WorkflowNodeId id, WorkflowNodeKind kind)
    {
        return new WorkflowNode(
            id,
            kind,
            id.Value,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));
    }

    private static ProjectStructureAgentContext CreateAgent(Guid projectId, string owner = "default")
    {
        return new ProjectStructureAgentContext(
            $"component-tests-{owner}",
            "Component tests",
            Environment.MachineName,
            AppContext.BaseDirectory,
            string.Empty,
            $"{projectId:D}-{owner}");
    }

    private sealed class CancelAfterAssignmentProjectPartyIntegrationBridge(
        IProjectPartyIntegrationBridge inner,
        CancellationTokenSource cancellationSource)
        : IProjectPartyIntegrationBridge
    {
        public Task<IReadOnlyDictionary<Guid, ProjectPortfolioPartyContext>> GetPortfolioContextsAsync(
            IReadOnlyCollection<Guid> projectIds,
            CancellationToken cancellationToken = default)
        {
            return inner.GetPortfolioContextsAsync(projectIds, cancellationToken);
        }

        public Task<IReadOnlyList<ProjectPartyOption>> ListPartyOptionsAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return inner.ListPartyOptionsAsync(projectId, cancellationToken);
        }

        public Task<ProjectPartyOption?> GetPartyOptionAsync(
            Guid partyId,
            CancellationToken cancellationToken = default)
        {
            return inner.GetPartyOptionAsync(partyId, cancellationToken);
        }

        public Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return inner.ListAssignmentsDetailedAsync(projectId, cancellationToken);
        }

        public Task<Result<Guid>> SaveAssignmentAsync(
            ProjectPartyAssignmentUpsertRequest request,
            CancellationToken cancellationToken = default)
        {
            return inner.SaveAssignmentAsync(request, cancellationToken);
        }

        public async Task<Result> ReplaceNodeAssignmentsAsync(
            Guid projectId,
            ProjectNodeReference nodeReference,
            IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
            IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
            CancellationToken cancellationToken = default)
        {
            var result = await inner.ReplaceNodeAssignmentsAsync(
                projectId,
                nodeReference,
                desiredAssignments,
                targetRoles,
                cancellationToken);
            if (result.IsSuccess)
            {
                cancellationSource.Cancel();
            }

            return result;
        }

        public Task DeleteAssignmentAsync(
            Guid assignmentId,
            CancellationToken cancellationToken = default)
        {
            return inner.DeleteAssignmentAsync(assignmentId, cancellationToken);
        }

        public Task DeleteAssignmentsForNodesAsync(
            Guid projectId,
            IReadOnlyCollection<ProjectNodeReference> nodeReferences,
            CancellationToken cancellationToken = default)
        {
            return inner.DeleteAssignmentsForNodesAsync(projectId, nodeReferences, cancellationToken);
        }

        public Task MoveAssignmentsToProjectAsync(
            Guid sourceProjectId,
            IReadOnlyCollection<ProjectNodeReference> nodeReferences,
            Guid targetProjectId,
            CancellationToken cancellationToken = default)
        {
            return inner.MoveAssignmentsToProjectAsync(
                sourceProjectId,
                nodeReferences,
                targetProjectId,
                cancellationToken);
        }

        public Task<Result<ProjectPartyQuickCreateResult>> CreatePartyAsync(
            ProjectPartyQuickCreateRequest request,
            CancellationToken cancellationToken = default)
        {
            return inner.CreatePartyAsync(request, cancellationToken);
        }
    }
}
