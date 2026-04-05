using CanDoItAll.Modules.Workbench;
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
    public void CanReclassify_allows_note_promotions_and_same_family_subtype_changes()
    {
        Assert.True(ProjectNodeKindRegistry.CanReclassify(ProjectObjectType.Note, string.Empty, ProjectObjectType.WorkItem, "task"));
        Assert.True(ProjectNodeKindRegistry.CanReclassify(ProjectObjectType.Note, string.Empty, ProjectObjectType.Decision, string.Empty));
        Assert.True(ProjectNodeKindRegistry.CanReclassify(ProjectObjectType.WorkItem, "task", ProjectObjectType.WorkItem, "issue"));
        Assert.False(ProjectNodeKindRegistry.CanReclassify(ProjectObjectType.WorkItem, "task", ProjectObjectType.Decision, string.Empty));
    }

    [Fact]
    public void NormalizeMetadata_updates_target_family_payload_and_clears_foreign_payloads()
    {
        var metadata = new ProjectObjectMetadataEnvelope
        {
            MarkerSet = new ProjectMarkerSetMetadata
            {
                Markers =
                [
                    new ProjectNodeMarker("question", "accent", "Question")
                ]
            },
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
        Assert.NotNull(normalizedWorkItem.MarkerSet);

        Assert.Null(normalizedDecision.WorkItem);
        Assert.NotNull(normalizedDecision.MarkerSet);
    }
}
