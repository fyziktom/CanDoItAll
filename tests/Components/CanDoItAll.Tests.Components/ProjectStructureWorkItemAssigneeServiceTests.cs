using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureWorkItemAssigneeServiceTests
{
    [Fact]
    public async Task Joe_Doe_is_assigned_directly_to_task_without_child_node_or_uses_link()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var assigneeService = harness.Context.Services.GetRequiredService<ProjectStructureWorkItemAssigneeService>();
        var bridge = harness.Context.Services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var projectId = await CreateProjectAsync(projectsService);
        var joeId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Joe Doe");
        var organizationId = await CreatePartyAsync(partyDirectoryService, PartyType.Organization, "Not an assignee");
        var task = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Prepare CRM handoff",
                "Delivery",
                string.Empty,
                $"project:{projectId}",
                420,
                260,
                ObjectSubtype: "task",
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    WorkItem = new ProjectWorkItemMetadata
                    {
                        WorkItemKind = ProjectWorkItemKind.Task
                    }
                })));

        var options = await assigneeService.ListOptionsAsync(projectId);
        Assert.Contains(options, option => option.ResourceId == joeId && option.Kind == ProjectStructureTaskResourceKind.Person);
        Assert.DoesNotContain(options, option => option.ResourceId == organizationId);

        await assigneeService.ReplaceAsync(
            projectId,
            task.Id,
            new ProjectStructureTaskResourceSelection(ProjectStructureTaskResourceKind.Person, joeId),
            "component-tests");

        var assignment = Assert.Single(await bridge.ListAssignmentsDetailedAsync(projectId), item =>
            item.NodeKey == task.Id &&
            item.Role == ProjectPartyAssignmentRole.WorkItemAssignee);
        Assert.Equal(joeId, assignment.PartyId);
        Assert.True(assignment.IsPrimary);

        var surface = await workbenchService.GetStructureAsync(projectId);
        var refreshedTask = surface.Nodes.Single(node => node.Id == task.Id);
        Assert.Equal("Joe Doe", ProjectObjectMetadataSerializer.Parse(refreshedTask.MetadataJson).WorkItem!.AssigneePartyDisplayName);
        Assert.Null(refreshedTask.NodeReferences?.WorkItemAssigneeNodeId);
        Assert.DoesNotContain(surface.Nodes, node => node.ParentId == task.Id && node.Title == "Joe Doe");
        Assert.DoesNotContain(surface.Links, link =>
            link.SourceId == task.Id &&
            link.Kind == ProjectObjectLinkKind.Uses &&
            link.TargetId == joeId.ToString("D"));
    }

    [Fact]
    public async Task Existing_non_task_work_item_can_still_receive_a_direct_assignee()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var assigneeService = harness.Context.Services.GetRequiredService<ProjectStructureWorkItemAssigneeService>();
        var bridge = harness.Context.Services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var projectId = await CreateProjectAsync(projectsService);
        var assigneeId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Issue owner");
        var dueUtc = new DateTimeOffset(2026, 7, 18, 15, 0, 0, TimeSpan.Zero);
        var issue = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Investigate regression",
                "P1",
                "Preserve non-task assignment behavior.",
                $"project:{projectId}",
                420,
                260,
                ObjectSubtype: "issue",
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    WorkItem = new ProjectWorkItemMetadata
                    {
                        WorkItemKind = ProjectWorkItemKind.Issue,
                        Description = "Preserve this issue detail.",
                        DueUtc = dueUtc
                    }
                })));

        await assigneeService.ReplaceAsync(
            projectId,
            issue.Id,
            new ProjectStructureTaskResourceSelection(ProjectStructureTaskResourceKind.Person, assigneeId),
            "component-tests");

        var assignment = Assert.Single(await bridge.ListAssignmentsDetailedAsync(projectId), item =>
            item.NodeKey == issue.Id &&
            item.Role == ProjectPartyAssignmentRole.WorkItemAssignee);
        Assert.Equal(assigneeId, assignment.PartyId);
        var refreshedIssue = (await workbenchService.GetStructureAsync(projectId)).Nodes.Single(node => node.Id == issue.Id);
        var metadata = ProjectObjectMetadataSerializer.Parse(refreshedIssue.MetadataJson).WorkItem!;
        Assert.Equal(ProjectWorkItemKind.Issue, metadata.WorkItemKind);
        Assert.Equal("Preserve this issue detail.", metadata.Description);
        Assert.Equal(dueUtc, metadata.DueUtc);
        Assert.Equal("Issue owner", metadata.AssigneePartyDisplayName);
    }

    [Fact]
    public async Task Canonical_bridge_rejects_organization_as_work_item_assignee()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var bridge = harness.Context.Services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var projectId = await CreateProjectAsync(projectsService);
        var organizationId = await CreatePartyAsync(partyDirectoryService, PartyType.Organization, "Architecture Guild");
        var task = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Review design",
                string.Empty,
                string.Empty,
                $"project:{projectId}",
                420,
                260,
                ObjectSubtype: "task"));

        var result = await bridge.ReplaceNodeAssignmentsAsync(
            projectId,
            new ProjectNodeReference(task.Id),
            [
                new ProjectPartyAssignmentUpsertRequest
                {
                    ProjectId = projectId,
                    PartyId = organizationId,
                    Role = ProjectPartyAssignmentRole.WorkItemAssignee,
                    NodeKey = task.Id,
                    IsPrimary = true,
                    Source = "component-tests"
                }
            ],
            [ProjectPartyAssignmentRole.WorkItemAssignee]);
        var saveResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = organizationId,
            Role = ProjectPartyAssignmentRole.WorkItemAssignee,
            NodeKey = task.Id,
            IsPrimary = true,
            Source = "component-tests"
        });

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "crmhr.project-assignment.work-item-assignee-party-type-invalid");
        Assert.True(saveResult.IsFailure);
        Assert.Contains(saveResult.Errors, error => error.Code == "crmhr.project-assignment.work-item-assignee-party-type-invalid");
        Assert.DoesNotContain(await bridge.ListAssignmentsDetailedAsync(projectId), assignment => assignment.NodeKey == task.Id);
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = $"Task assignee proof {Guid.NewGuid():N}",
            Description = "Direct task assignment proof.",
            Objective = "Keep person and agent identity off the project node tree.",
            CurrentPhase = "Delivery"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        PartyType partyType,
        string displayName)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} component-test record.",
            LastChangedBy = "component-tests"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
