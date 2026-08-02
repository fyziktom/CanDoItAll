using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectNodeKindRegistryTests
{
    [Fact]
    public void ResolveDescriptor_returns_canonical_label_and_visual_profile_for_known_kind()
    {
        var descriptor = ProjectNodeKindRegistry.ResolveDescriptor(ProjectObjectType.ProjectBlock, "router");
        var profile = ProjectNodeKindRegistry.ResolveVisualProfile(ProjectObjectType.ProjectBlock, "router", "Draft");

        Assert.Equal(ProjectNodeKindFamily.ProjectBlock, descriptor.Family);
        Assert.Equal("Router block", descriptor.Label);
        Assert.Equal(ProjectObjectPaletteKeys.Info, profile.PaletteKey);
        Assert.Equal("RT", profile.Icon);
    }

    [Fact]
    public void Folder_file_kind_has_typed_metadata_and_visual_profile()
    {
        var metadata = ProjectNodeKindRegistry.NormalizeMetadata(
            ProjectObjectType.File,
            "folder",
            new ProjectObjectMetadataEnvelope(),
            string.Empty,
            null);
        var profile = ProjectNodeKindRegistry.ResolveVisualProfile(
            ProjectObjectType.File,
            "folder",
            string.Empty);

        Assert.Equal(ProjectFileSubtype.Folder, metadata.File?.FileSubtype);
        Assert.Equal("Folder", ProjectNodeKindRegistry.ResolveLabel(ProjectObjectType.File, "folder"));
        Assert.Equal("FD", profile.Icon);
    }

    [Fact]
    public void Mermaid_metadata_prefers_the_stored_source_fact_over_descriptive_notes()
    {
        var media = new SavedMediaDescriptor(
            "managed-files/project-media/files/diagram.mmd",
            "/files/diagram.mmd",
            "text/vnd.mermaid",
            "diagram.mmd",
            ProjectObjectType.File.ToString(),
            "{}",
            MermaidDiagramKind.SequenceDiagram);

        ProjectObjectMetadataEnvelope metadata = ProjectNodeKindRegistry.NormalizeMetadata(
            ProjectObjectType.File,
            "mermaid",
            new ProjectObjectMetadataEnvelope(),
            "gantt purpose notes must not define the source kind",
            media);

        Assert.Equal(MermaidDiagramKind.SequenceDiagram, metadata.File?.MermaidDiagramKind);
    }

    [Fact]
    public void CanReclassify_allows_note_promotions_and_same_family_subtype_changes()
    {
        Assert.True(ProjectNodeKindRegistry.CanReclassify(ProjectObjectType.Note, string.Empty, ProjectObjectType.WorkItem, "task"));
        Assert.True(ProjectNodeKindRegistry.CanReclassify(ProjectObjectType.Note, string.Empty, ProjectObjectType.Decision, string.Empty));
        Assert.True(ProjectNodeKindRegistry.CanReclassify(ProjectObjectType.WorkItem, "task", ProjectObjectType.WorkItem, "issue"));
        Assert.True(ProjectNodeKindRegistry.CanReclassify(ProjectObjectType.Script, "powershell", ProjectObjectType.Environment, "dotnet-watch"));
        Assert.True(ProjectNodeKindRegistry.CanReclassify(ProjectObjectType.Environment, "dotnet-watch", ProjectObjectType.Infrastructure, "docker-mode"));
        Assert.False(ProjectNodeKindRegistry.CanReclassify(ProjectObjectType.WorkItem, "task", ProjectObjectType.Decision, string.Empty));
    }

    [Theory]
    [InlineData("database")]
    [InlineData("remote-server")]
    [InlineData("deployment-folder")]
    public void CanReclassify_rejects_cross_family_promotion_to_non_runnable_infrastructure(
        string infrastructureSubtype)
    {
        Assert.False(ProjectNodeKindRegistry.CanReclassify(
            ProjectObjectType.Script,
            "powershell",
            ProjectObjectType.Infrastructure,
            infrastructureSubtype));
    }

    [Fact]
    public void NormalizeMetadata_updates_target_family_payload_and_clears_foreign_payloads()
    {
        var metadata = new ProjectObjectMetadataEnvelope
        {
            WorkItem = new ProjectWorkItemMetadata
            {
                WorkItemKind = ProjectWorkItemKind.Task,
                Description = "Keep description"
            }
        };

        var normalizedWorkItem = ProjectNodeKindRegistry.NormalizeMetadata(ProjectObjectType.WorkItem, "payment", metadata, "Keep description", null);
        var normalizedDecision = ProjectNodeKindRegistry.NormalizeMetadata(ProjectObjectType.Decision, string.Empty, metadata, "Keep description", null);

        Assert.NotNull(normalizedWorkItem.WorkItem);
        Assert.Equal(ProjectWorkItemKind.Payment, normalizedWorkItem.WorkItem!.WorkItemKind);
        Assert.Equal("Keep description", normalizedWorkItem.WorkItem.Description);

        Assert.Null(normalizedDecision.WorkItem);
    }

    [Fact]
    public void Workflow_metadata_is_scoped_and_requires_workflow_id()
    {
        var workflowId = WorkflowId.New();
        var metadata = new ProjectObjectMetadataEnvelope
        {
            Workflow = new ProjectWorkflowNodeMetadata
            {
                WorkflowId = workflowId,
                WorkflowName = "Order reconciliation"
            },
            WorkItem = new ProjectWorkItemMetadata()
        };

        var normalized = ProjectNodeKindRegistry.NormalizeMetadata(ProjectObjectType.WorkflowDefinition, string.Empty, metadata, string.Empty, null);

        Assert.Equal(ProjectNodeKindFamily.Workflow, ProjectNodeKindRegistry.ResolveDescriptor(ProjectObjectType.WorkflowDefinition, string.Empty).Family);
        Assert.NotNull(normalized.Workflow);
        Assert.Equal(workflowId, normalized.Workflow!.WorkflowId);
        Assert.Null(normalized.WorkItem);
        Assert.Throws<InvalidOperationException>(() =>
            ProjectObjectMetadataSerializer.Validate(
                ProjectObjectType.WorkflowDefinition,
                string.Empty,
                new ProjectObjectMetadataEnvelope
                {
                    Workflow = new ProjectWorkflowNodeMetadata()
                }));
    }

    [Fact]
    public void ResolveReplacementRoles_and_preferred_role_follow_descriptor_policy()
    {
        var participantRoles = ProjectNodeKindRegistry.ResolveReplacementRoles(ProjectObjectType.Participant, "partner");
        var participantPreferredRole = ProjectNodeKindRegistry.ResolvePreferredRole(ProjectObjectType.Participant, "partner");
        var meetingRoles = ProjectNodeKindRegistry.ResolveReplacementRoles(ProjectObjectType.Meeting, "online");

        Assert.Equal(
            [
                ProjectPartyAssignmentRole.TeamMember,
                ProjectPartyAssignmentRole.DeliveryUnit,
                ProjectPartyAssignmentRole.Partner,
                ProjectPartyAssignmentRole.AiAgent
            ],
            participantRoles);
        Assert.Equal(ProjectPartyAssignmentRole.Partner, participantPreferredRole);
        Assert.Equal([ProjectPartyAssignmentRole.MeetingParticipant], meetingRoles);
    }

    [Fact]
    public void Canonical_node_scope_policy_distinguishes_optional_and_required_roles()
    {
        Assert.True(ProjectNodeKindRegistry.SupportsCanonicalNodeScope(ProjectPartyAssignmentRole.TeamMember));
        Assert.False(ProjectNodeKindRegistry.RequiresCanonicalNodeScope(ProjectPartyAssignmentRole.TeamMember));
        Assert.True(ProjectNodeKindRegistry.SupportsCanonicalNodeScope(ProjectPartyAssignmentRole.WorkItemAssignee));
        Assert.True(ProjectNodeKindRegistry.RequiresCanonicalNodeScope(ProjectPartyAssignmentRole.WorkItemAssignee));
        Assert.False(ProjectNodeKindRegistry.SupportsCanonicalNodeScope(ProjectPartyAssignmentRole.Manager));
    }
}
