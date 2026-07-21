using Bunit;
using CanDoItAll.Components.CanvasLib;
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

        await SaveSelectedNodeStateAsync(workbenchService, projectId, participantNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Freelancer Node", cut.Markup);
        });

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-party-editor", cut.Markup);
            Assert.Contains("Keep project-local only", cut.Markup);
        });

        cut.WaitForElement("[data-testid='project-structure-participant-local-only']");
        cut.Find("[data-testid='project-structure-participant-local-only']").Change(false);
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-participant-party", cut.Markup);
        });
        cut.WaitForElement($"[data-testid='project-structure-participant-party-option-{partyId:N}']");
        cut.Find($"[data-testid='project-structure-participant-party-option-{partyId:N}']").Click();
        cut.Find("[data-testid='project-structure-participant-save']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Participant linked to the directory.", cut.Markup);
        });

        var metadata = await ReadParticipantMetadataAsync(workbenchService, projectId, participantNode.Id);
        Assert.Equal("Linked Freelancer", metadata.LinkedPartyDisplayName);
        Assert.Contains(await bridge.ListAssignmentsDetailedAsync(projectId), item => item.NodeKey == participantNode.Id && item.PartyId == partyId);

        cut.Find("[data-testid='project-structure-participant-local-only']").Change(true);
        cut.Find("[data-testid='project-structure-participant-save']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Participant kept project-local only.", cut.Markup);
        });

        metadata = await ReadParticipantMetadataAsync(workbenchService, projectId, participantNode.Id);
        Assert.Equal(string.Empty, metadata.LinkedPartyDisplayName);
        Assert.DoesNotContain(await bridge.ListAssignmentsDetailedAsync(projectId), item => item.NodeKey == participantNode.Id);
    }

    [Fact]
    public async Task Meeting_editor_saves_assignments_while_work_items_hide_the_page_editor()
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

        await SaveSelectedNodeStateAsync(workbenchService, projectId, meetingNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Weekly Sync", cut.Markup);
            Assert.Contains("Prepare recap", cut.Markup);
        });

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
        Assert.Contains("Meeting Customer", meetingMetadata.RelatedPartySummary);
        Assert.Contains("Meeting Owner", meetingMetadata.RelatedPartySummary);

        await SaveSelectedNodeStateAsync(workbenchService, projectId, workItemNode.Id);
        cut.Dispose();
        cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Prepare recap", cut.Markup);
        });
        Assert.DoesNotContain("Work-item party assignment", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='project-structure-party-editor']"));
    }

    [Fact]
    public async Task Participant_and_meeting_editors_load_from_canonical_assignments_when_metadata_is_stale()
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
        await SaveSelectedNodeStateAsync(workbenchService, projectId, participantNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Stale participant node", cut.Markup);
            Assert.Contains("Stale meeting node", cut.Markup);
        });

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-party-editor", cut.Markup);
        });
        cut.WaitForElement("[data-testid='project-structure-participant-save']");
        cut.Find("[data-testid='project-structure-participant-save']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Participant linked to the directory.", cut.Markup);
        });

        var participantMetadata = await ReadParticipantMetadataAsync(workbenchService, projectId, participantNode.Id);
        Assert.Equal("Canonical Participant", participantMetadata.LinkedPartyDisplayName);

        await SaveSelectedNodeStateAsync(workbenchService, projectId, meetingNode.Id);
        cut.Dispose();
        cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Meeting party assignments", cut.Markup);
        });
        cut.WaitForElement("[data-testid='project-structure-meeting-save']");
        cut.Find("[data-testid='project-structure-meeting-save']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Meeting parties saved.", cut.Markup);
        });

        var meetingMetadata = await ReadMeetingMetadataAsync(workbenchService, projectId, meetingNode.Id);
        Assert.Contains("Canonical Meeting Customer", meetingMetadata.RelatedPartySummary);
        Assert.Contains("Canonical Meeting Owner", meetingMetadata.RelatedPartySummary);
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

    private static Task SaveSelectedNodeStateAsync(ProjectWorkbenchService workbenchService, Guid projectId, params string[] selectedNodeIds)
        => workbenchService.SaveViewStateAsync(
            projectId,
            "structure",
            new CanvasWorkbenchUiState
            {
                SelectedNodeIds = selectedNodeIds.ToList(),
                WindowStates = new Dictionary<string, CanvasWorkbenchWindowState>(StringComparer.Ordinal)
                {
                    ["project-structure.selection"] = new CanvasWorkbenchWindowState { IsVisible = true }
                }
            }.ToJson());

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

}
