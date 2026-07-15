using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectPartyAssignmentIntegrationTests
{
    [Fact]
    public async Task Bridge_persists_project_and_node_assignments_and_enriches_portfolio_context()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();

        var projectId = await CreateProjectAsync(projectsService, "B10 Integration Project");
        var customerId = await CreatePartyAsync(partyDirectoryService, PartyType.Organization, "Acme Customer");
        var deliveryUnitId = await CreatePartyAsync(partyDirectoryService, PartyType.OrganizationUnit, "Platform Guild");
        var ownerId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Morgan Owner");

        Assert.True((await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = customerId,
            Role = ProjectPartyAssignmentRole.Customer,
            IsPrimary = true,
            Source = "integration-tests"
        })).IsSuccess);
        Assert.True((await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = deliveryUnitId,
            Role = ProjectPartyAssignmentRole.DeliveryUnit,
            IsPrimary = true,
            AllocationPercent = 70m,
            Source = "integration-tests"
        })).IsSuccess);
        Assert.True((await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = ownerId,
            Role = ProjectPartyAssignmentRole.Manager,
            IsPrimary = true,
            NodeKey = "work-item-alpha",
            Source = "integration-tests"
        })).IsSuccess);

        var quickCreate = await bridge.CreatePartyAsync(new ProjectPartyQuickCreateRequest
        {
            ProjectId = projectId,
            PartyKind = ProjectPartyQuickCreateKind.AiAgent,
            DisplayName = "Review Agent",
            Summary = "Assists with structured review."
        });

        Assert.True(quickCreate.IsSuccess);
        var createdParty = quickCreate.Value;
        Assert.NotNull(createdParty);

        var detailedAssignments = await bridge.ListAssignmentsDetailedAsync(projectId);
        Assert.Equal(3, detailedAssignments.Count);
        Assert.Contains(detailedAssignments, item => item.Role == ProjectPartyAssignmentRole.Customer && item.PartyDisplayName == "Acme Customer");
        Assert.Contains(detailedAssignments, item => item.Role == ProjectPartyAssignmentRole.DeliveryUnit && item.AllocationPercent == 70m);
        Assert.Contains(detailedAssignments, item => item.Role == ProjectPartyAssignmentRole.Manager && item.NodeKey == "work-item-alpha");

        var contexts = await bridge.GetPortfolioContextsAsync([projectId]);
        var context = Assert.Single(contexts).Value;
        Assert.Equal("Acme Customer", context.PrimaryCustomerName);
        Assert.Equal("Platform Guild", context.PrimaryDeliveryUnitName);
        Assert.Contains("Acme Customer", context.SearchText, StringComparison.Ordinal);

        var options = await bridge.ListPartyOptionsAsync(projectId);
        Assert.Contains(options, item =>
            item.PartyId == createdParty!.PartyId &&
            item.PartyType == ProjectPartyType.AiAgent &&
            item.PartyTypeLabel == "AI agent");
    }

    [Fact]
    public async Task Bridge_rejects_missing_and_cross_project_canonical_node_assignments()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();

        var firstProjectId = await CreateProjectAsync(projectsService, "Canonical assignment A");
        var secondProjectId = await CreateProjectAsync(projectsService, "Canonical assignment B");
        var assigneeId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Willa Worker");
        var foreignWorkItem = await workbench.CreateObjectAsync(
            secondProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Foreign work item",
                string.Empty,
                "Belongs to another project.",
                null,
                420,
                240,
                null,
                null,
                "task"));

        var missingNodeResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = firstProjectId,
            PartyId = assigneeId,
            Role = ProjectPartyAssignmentRole.WorkItemAssignee,
            NodeKey = "custom:missing-work-item",
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.False(missingNodeResult.IsSuccess);
        Assert.Contains(missingNodeResult.Errors, error => error.Code == "crmhr.project-assignment.node-not-found");

        var foreignNodeResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = firstProjectId,
            PartyId = assigneeId,
            Role = ProjectPartyAssignmentRole.WorkItemAssignee,
            NodeKey = foreignWorkItem.Id,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.False(foreignNodeResult.IsSuccess);
        Assert.Contains(foreignNodeResult.Errors, error => error.Code == "crmhr.project-assignment.node-project-mismatch");
    }

    [Fact]
    public async Task Bridge_rejects_disallowed_canonical_node_role_combinations()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();

        var projectId = await CreateProjectAsync(projectsService, "Canonical role policy");
        var participantId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Mina Meeting");
        var workItemAssigneeId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Ari Assignee");
        var noteNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Loose note",
                string.Empty,
                "Not a meeting or work item.",
                null,
                420,
                260));
        var meetingNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Meeting,
                "Stand-up",
                string.Empty,
                "Meeting node.",
                null,
                680,
                260));

        var invalidMeetingRole = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = participantId,
            Role = ProjectPartyAssignmentRole.MeetingParticipant,
            NodeKey = noteNode.Id,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.False(invalidMeetingRole.IsSuccess);
        Assert.Contains(invalidMeetingRole.Errors, error => error.Code == "crmhr.project-assignment.node-role-not-allowed");

        var validMeetingRole = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = participantId,
            Role = ProjectPartyAssignmentRole.MeetingParticipant,
            NodeKey = meetingNode.Id,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.True(validMeetingRole.IsSuccess);

        var invalidWorkItemRole = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = workItemAssigneeId,
            Role = ProjectPartyAssignmentRole.WorkItemAssignee,
            NodeKey = meetingNode.Id,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.False(invalidWorkItemRole.IsSuccess);
        Assert.Contains(invalidWorkItemRole.Errors, error => error.Code == "crmhr.project-assignment.node-role-not-allowed");
    }

    [Fact]
    public async Task Bridge_rejects_projection_only_node_targets_and_uses_participant_capabilities_for_optional_node_scope()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();

        var projectId = await CreateProjectAsync(projectsService, "Participant role policy");
        var teamMemberId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Taylor Team Member");
        var partnerId = await CreatePartyAsync(partyDirectoryService, PartyType.Organization, "Partner Org");
        var participantNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Participant,
                "Freelancer node",
                string.Empty,
                "Participant node for optional node scope validation.",
                null,
                420,
                260,
                null,
                null,
                "freelancer"));

        var projectionOnlyResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = teamMemberId,
            Role = ProjectPartyAssignmentRole.TeamMember,
            NodeKey = $"project:{projectId}",
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.False(projectionOnlyResult.IsSuccess);
        Assert.Contains(projectionOnlyResult.Errors, error => error.Code == "crmhr.project-assignment.node-projection-not-allowed");

        var invalidParticipantRole = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = partnerId,
            Role = ProjectPartyAssignmentRole.Partner,
            NodeKey = participantNode.Id,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.False(invalidParticipantRole.IsSuccess);
        Assert.Contains(invalidParticipantRole.Errors, error => error.Code == "crmhr.project-assignment.node-role-not-allowed");

        var validParticipantRole = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = teamMemberId,
            Role = ProjectPartyAssignmentRole.TeamMember,
            NodeKey = participantNode.Id,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.True(validParticipantRole.IsSuccess);

        var projectLevelRole = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = teamMemberId,
            Role = ProjectPartyAssignmentRole.TeamMember,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.True(projectLevelRole.IsSuccess);
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Discovery"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<Guid> CreatePartyAsync(PartyDirectoryService partyDirectoryService, PartyType partyType, string displayName)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "integration-tests"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
