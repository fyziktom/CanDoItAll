using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureNodeHelpersTests
{
    [Fact]
    public void BuildSimpleNoteTitle_uses_first_non_empty_line_and_truncates()
    {
        var text = $"""

            {new string('a', 80)}
            second line
            """;

        var title = ProjectStructureNodeHelpers.BuildSimpleNoteTitle(text);

        Assert.Equal($"{new string('a', 61)}...", title);
    }

    [Fact]
    public void ResolveAttachmentPreviewKind_returns_text_document_for_inline_text_asset()
    {
        var node = CreateNode(
            objectType: ProjectObjectType.File,
            objectSubtype: "markdown",
            notes: "# Notes",
            route: "/files/note.md");

        var kind = ProjectStructureNodeHelpers.ResolveAttachmentPreviewKind(node);

        Assert.Equal(AttachmentPreviewKind.TextDocument, kind);
        Assert.Equal("# Notes", ProjectStructureNodeHelpers.ResolveAttachmentTextContent(node));
    }

    [Fact]
    public void ResolveAttachmentPreviewSource_hides_pdf_toolbar()
    {
        var node = CreateNode(
            objectType: ProjectObjectType.File,
            route: "/managed-files/report.pdf",
            mediaOriginalFileName: "report.pdf");

        var source = ProjectStructureNodeHelpers.ResolveAttachmentPreviewSource(node);

        Assert.Equal("/managed-files/report.pdf#toolbar=0&navpanes=0&view=FitH", source);
    }

    [Fact]
    public void ResolveMarkerLabel_summarizes_more_than_three_markers()
    {
        var node = CreateNode(markers:
        [
            new ProjectNodeMarker("question", "sky", "Question"),
            new ProjectNodeMarker("risk", "danger", "Risk"),
            new ProjectNodeMarker("idea", "accent", "Idea"),
            new ProjectNodeMarker("pause", "warn", "Paused")
        ]);

        var label = ProjectStructureNodeHelpers.ResolveMarkerLabel(node);

        Assert.Equal("Question, Risk, Idea +1", label);
    }

    private static ProjectStructureNode CreateNode(
        ProjectObjectType objectType = ProjectObjectType.Note,
        string objectSubtype = "",
        string title = "Node",
        string notes = "",
        string route = "",
        string mediaContentType = "",
        string mediaOriginalFileName = "",
        IReadOnlyList<ProjectNodeMarker>? markers = null)
        => new(
            Id: "custom:test",
            ParentId: null,
            ObjectType: objectType,
            ObjectSubtype: objectSubtype,
            Title: title,
            Subtitle: string.Empty,
            Status: "Ready",
            Notes: notes,
            Route: route,
            ArtifactKind: string.Empty,
            ArtifactId: null,
            MediaRelativePath: string.Empty,
            MediaContentType: mediaContentType,
            MediaOriginalFileName: mediaOriginalFileName,
            X: 0,
            Y: 0,
            VisualProfile: new ProjectObjectVisualProfile("rectangle", "#000000", "note", string.Empty),
            Badges: [],
            ProgressMode: string.Empty,
            ProgressPercent: 0,
            MarkerIcon: string.Empty,
            MarkerTone: string.Empty,
            MarkerLabel: string.Empty,
            Markers: markers ?? [],
            Priority: 0);
}
