using System.Globalization;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureNodeEditorTests
{
    [Theory]
    [InlineData("2026-10-26T10:00:00-04:00")]
    [InlineData("2026-10-26T10:00:00+05:30")]
    [InlineData("2026-10-26T10:00")]
    public void Created_and_changed_dates_preserve_the_instant_as_utc(string input) {
        var expected = DateTimeOffset.Parse(input, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal).ToUniversalTime();
        var node = DateNode();
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(node.ObjectType, node.ObjectSubtype, out var definition));
        var request = DateRequest(node, [new() { Key = "startUtc", Value = input },
            new() { Key = "endUtc", Value = input }, new() { Key = "dueUtc", Value = input }]);
        var created = ProjectStructureCreateRequestComposer.Compose(definition, request, node.ParentId, (null, null)).Request;
        var edited = ProjectStructureNodeEditor.ComposeUpdate(definition, node, request);
        DateTimeOffset?[] dates = [created.StartUtc, created.EndUtc,
            ProjectObjectMetadataSerializer.Parse(created.MetadataJson).WorkItem?.DueUtc,
            edited.StartUtc, edited.EndUtc, ProjectObjectMetadataSerializer.Parse(edited.MetadataJson).WorkItem?.DueUtc];
        Assert.All(dates, value => {
            Assert.Equal(expected, value);
            Assert.Equal(TimeSpan.Zero, value!.Value.Offset);
        });
    }

    [Theory]
    [InlineData("startUtc")]
    [InlineData("endUtc")]
    [InlineData("dueUtc")]
    public void Invalid_creation_date_is_rejected_before_dispatch(string key) {
        var node = DateNode();
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(node.ObjectType, node.ObjectSubtype, out var definition));
        Assert.Throws<ArgumentException>(() => ProjectStructureCreateRequestComposer.Compose(definition,
            DateRequest(node, [new() { Key = key, Value = "not-a-date" }]), node.ParentId, (null, null)));
    }

    [Fact]
    public void Missing_and_blank_optional_creation_dates_remain_unset() {
        var node = DateNode();
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(node.ObjectType, node.ObjectSubtype, out var definition));
        var created = ProjectStructureCreateRequestComposer.Compose(definition,
            DateRequest(node, [new() { Key = "endUtc", Value = " " }]), node.ParentId, (null, null)).Request;
        Assert.Null(created.StartUtc);
        Assert.Null(created.EndUtc);
        Assert.Null(ProjectObjectMetadataSerializer.Parse(created.MetadataJson).WorkItem?.DueUtc);
    }

    private static CanvasWorkbenchCreateActionRequest DateRequest(ProjectStructureNode node, IReadOnlyList<CanvasWorkbenchInputValue> values)
        => new("date-fixture", node.Id, 0, 0, node.ParentId, node.Title, node.Subtitle, node.Notes,
            "child", "dialog", node.ObjectSubtype, null, values);

    private static ProjectStructureNode DateNode() => new("node", "parent", ProjectObjectType.WorkItem, "issue", "Issue", "", "Draft", "", "", "", null,
        "", "", "", 0, 0, new("rect", "#0369a1", "I", "Issue"), [], "progress", 0, "", "", "", [], 0);

    [Theory]
    [InlineData("mermaidText")]
    [InlineData("imageProviderProfileId")]
    [InlineData("imageModel")]
    [InlineData("imageSize")]
    [InlineData("imageQuality")]
    [InlineData("imageOutputFormat")]
    public void Content_generation_fields_are_not_advertised_as_metadata_edit_fields(string fieldKey)
    {
        Assert.False(ProjectStructureNodeEditor.SupportsEditingField(fieldKey));
    }
}
