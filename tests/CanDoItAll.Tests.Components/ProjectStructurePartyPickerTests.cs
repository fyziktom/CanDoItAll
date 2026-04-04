using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructurePartyPickerTests
{
    [Fact]
    public async Task Participant_editor_can_link_and_unlink_directory_party()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var bridge = harness.Context.Services.GetRequiredService<IProjectPartyIntegrationBridge>();

        var projectId = await CreateProjectAsync(projectsService, "Participant Sync Project");
        var partyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Linked Freelancer");
        var participantNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Participant,
                "Freelancer Node",
                "Local participant",
                "Starts local.",
                $"project:{projectId}",
                420,
                260,
                ObjectSubtype: "freelancer",
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    Participant = new ProjectParticipantMetadata
                    {
                        ParticipantKind = ProjectParticipantKind.Freelancer,
                        Role = "Designer"
                    }
                })));

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Freelancer Node", cut.Markup);
        });

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Freelancer Node", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-party-editor", cut.Markup);
            Assert.Contains("Keep project-local only", cut.Markup);
        });

        cut.Find("[data-testid='project-structure-participant-local-only']").Change(false);
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-participant-party", cut.Markup);
        });
        cut.Find("[data-testid='project-structure-participant-party']").Change(partyId.ToString());
        cut.Find("[data-testid='project-structure-participant-save']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Participant linked to the directory.", cut.Markup);
        });

        var metadata = await ReadParticipantMetadataAsync(workbenchService, projectId, participantNode.Id);
        Assert.Equal(partyId, metadata.LinkedPartyId);
        Assert.Equal("Linked Freelancer", metadata.LinkedPartyName);
        Assert.Contains(await bridge.ListAssignmentsDetailedAsync(projectId), item => item.NodeKey == participantNode.Id && item.PartyId == partyId);

        cut.Find("[data-testid='project-structure-participant-local-only']").Change(true);
        cut.Find("[data-testid='project-structure-participant-save']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Participant kept project-local only.", cut.Markup);
        });

        metadata = await ReadParticipantMetadataAsync(workbenchService, projectId, participantNode.Id);
        Assert.Null(metadata.LinkedPartyId);
        Assert.DoesNotContain(await bridge.ListAssignmentsDetailedAsync(projectId), item => item.NodeKey == participantNode.Id);
    }

    [Fact]
    public async Task Meeting_and_work_item_editor_save_central_party_assignments()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var bridge = harness.Context.Services.GetRequiredService<IProjectPartyIntegrationBridge>();

        var projectId = await CreateProjectAsync(projectsService, "Meeting Assignment Project");
        var customerId = await CreatePartyAsync(partyDirectoryService, PartyType.Organization, "Meeting Customer");
        var ownerId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Meeting Owner");

        await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = customerId,
            Role = ProjectPartyAssignmentRole.Customer,
            IsPrimary = true,
            Source = "component-tests"
        });
        await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = ownerId,
            Role = ProjectPartyAssignmentRole.Manager,
            IsPrimary = true,
            Source = "component-tests"
        });

        var meetingNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Meeting,
                "Weekly Sync",
                "Discovery",
                "Meeting node",
                $"project:{projectId}",
                420,
                260,
                ObjectSubtype: "online"));
        var workItemNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Prepare recap",
                "Follow-up",
                "Work item",
                $"project:{projectId}",
                620,
                260,
                ObjectSubtype: "task"));

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Weekly Sync", cut.Markup);
            Assert.Contains("Prepare recap", cut.Markup);
        });

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Weekly Sync", StringComparison.Ordinal))
            .Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-party-editor", cut.Markup);
            Assert.Contains("Meeting Customer", cut.Markup);
            Assert.Contains("Meeting Owner", cut.Markup);
        });
        cut.Find("[data-testid='project-structure-meeting-project-defaults']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("2 selected", cut.Markup);
        });
        cut.Find("[data-testid='project-structure-meeting-save']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Meeting parties saved.", cut.Markup);
        });

        var meetingMetadata = await ReadMeetingMetadataAsync(workbenchService, projectId, meetingNode.Id);
        Assert.Contains("Meeting Customer", meetingMetadata.RelatedPartyNames);
        Assert.Contains("Meeting Owner", meetingMetadata.RelatedPartyNames);

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Prepare recap", StringComparison.Ordinal))
            .Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Work-item party assignment", cut.Markup);
        });
        cut.Find("[data-testid='project-structure-work-item-party']").Change(ownerId.ToString());
        cut.Find("[data-testid='project-structure-work-item-save']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Work-item assignee saved.", cut.Markup);
        });

        var workItemMetadata = await ReadWorkItemMetadataAsync(workbenchService, projectId, workItemNode.Id);
        Assert.Equal(ownerId, workItemMetadata.AssigneePartyId);
        Assert.Equal("Meeting Owner", workItemMetadata.AssigneePartyName);
        Assert.Contains(await bridge.ListAssignmentsDetailedAsync(projectId), item => item.NodeKey == workItemNode.Id && item.Role == ProjectPartyAssignmentRole.WorkItemAssignee);
    }

    [Fact]
    public async Task Editors_load_from_canonical_assignments_when_metadata_is_stale()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var bridge = harness.Context.Services.GetRequiredService<IProjectPartyIntegrationBridge>();

        var projectId = await CreateProjectAsync(projectsService, "Canonical Read Project");
        var participantPartyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Canonical Participant");
        var meetingCustomerId = await CreatePartyAsync(partyDirectoryService, PartyType.Organization, "Canonical Meeting Customer");
        var meetingOwnerId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Canonical Meeting Owner");
        var workItemPartyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Canonical Work Owner");

        var participantNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Participant,
                "Stale participant node",
                "Local participant",
                "Metadata starts stale.",
                $"project:{projectId}",
                420,
                260,
                ObjectSubtype: "freelancer",
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    Participant = new ProjectParticipantMetadata
                    {
                        ParticipantKind = ProjectParticipantKind.Freelancer,
                        Role = "Designer"
                    }
                })));
        var meetingNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Meeting,
                "Stale meeting node",
                "Discovery",
                "Metadata starts stale.",
                $"project:{projectId}",
                620,
                260,
                ObjectSubtype: "online",
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    Meeting = new ProjectMeetingMetadata()
                })));
        var workItemNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Stale work item node",
                "Follow-up",
                "Metadata starts stale.",
                $"project:{projectId}",
                820,
                260,
                ObjectSubtype: "task",
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    WorkItem = new ProjectWorkItemMetadata
                    {
                        WorkItemKind = ProjectWorkItemKind.Task
                    }
                })));

        Assert.True((await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = participantPartyId,
            Role = ProjectPartyAssignmentRole.TeamMember,
            NodeKey = participantNode.Id,
            IsPrimary = true,
            Source = "component-tests"
        })).IsSuccess);
        Assert.True((await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = meetingCustomerId,
            Role = ProjectPartyAssignmentRole.MeetingParticipant,
            NodeKey = meetingNode.Id,
            IsPrimary = true,
            Source = "component-tests"
        })).IsSuccess);
        Assert.True((await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = meetingOwnerId,
            Role = ProjectPartyAssignmentRole.MeetingParticipant,
            NodeKey = meetingNode.Id,
            IsPrimary = false,
            Source = "component-tests"
        })).IsSuccess);
        Assert.True((await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = workItemPartyId,
            Role = ProjectPartyAssignmentRole.WorkItemAssignee,
            NodeKey = workItemNode.Id,
            IsPrimary = true,
            Source = "component-tests"
        })).IsSuccess);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Stale participant node", cut.Markup);
            Assert.Contains("Stale meeting node", cut.Markup);
            Assert.Contains("Stale work item node", cut.Markup);
        });

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Stale participant node", StringComparison.Ordinal))
            .Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-party-editor", cut.Markup);
        });
        cut.Find("[data-testid='project-structure-participant-save']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Participant linked to the directory.", cut.Markup);
        });

        var participantMetadata = await ReadParticipantMetadataAsync(workbenchService, projectId, participantNode.Id);
        Assert.Equal(participantPartyId, participantMetadata.LinkedPartyId);
        Assert.Equal("Canonical Participant", participantMetadata.LinkedPartyName);

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Stale meeting node", StringComparison.Ordinal))
            .Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Meeting party assignments", cut.Markup);
        });
        cut.Find("[data-testid='project-structure-meeting-save']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Meeting parties saved.", cut.Markup);
        });

        var meetingMetadata = await ReadMeetingMetadataAsync(workbenchService, projectId, meetingNode.Id);
        Assert.Contains("Canonical Meeting Customer", meetingMetadata.RelatedPartyNames);
        Assert.Contains("Canonical Meeting Owner", meetingMetadata.RelatedPartyNames);

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Stale work item node", StringComparison.Ordinal))
            .Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Work-item party assignment", cut.Markup);
        });
        cut.Find("[data-testid='project-structure-work-item-save']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Work-item assignee saved.", cut.Markup);
        });

        var workItemMetadata = await ReadWorkItemMetadataAsync(workbenchService, projectId, workItemNode.Id);
        Assert.Equal(workItemPartyId, workItemMetadata.AssigneePartyId);
        Assert.Equal("Canonical Work Owner", workItemMetadata.AssigneePartyName);
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
            LastChangedBy = "component-tests"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<ProjectParticipantMetadata> ReadParticipantMetadataAsync(ProjectWorkbenchService workbenchService, Guid projectId, string nodeId)
    {
        var surface = await workbenchService.GetStructureAsync(projectId);
        var node = surface.Nodes.Single(item => item.Id == nodeId);
        return ProjectObjectMetadataSerializer.Parse(node.MetadataJson).Participant!;
    }

    private static async Task<ProjectMeetingMetadata> ReadMeetingMetadataAsync(ProjectWorkbenchService workbenchService, Guid projectId, string nodeId)
    {
        var surface = await workbenchService.GetStructureAsync(projectId);
        var node = surface.Nodes.Single(item => item.Id == nodeId);
        return ProjectObjectMetadataSerializer.Parse(node.MetadataJson).Meeting!;
    }

    private static async Task<ProjectWorkItemMetadata> ReadWorkItemMetadataAsync(ProjectWorkbenchService workbenchService, Guid projectId, string nodeId)
    {
        var surface = await workbenchService.GetStructureAsync(projectId);
        var node = surface.Nodes.Single(item => item.Id == nodeId);
        return ProjectObjectMetadataSerializer.Parse(node.MetadataJson).WorkItem!;
    }
}
