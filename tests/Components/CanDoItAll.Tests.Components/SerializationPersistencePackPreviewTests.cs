using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class SerializationPersistencePackPreviewTests
{
    [Fact]
    public void Factory_reports_round_trip_restore_counts()
    {
        var snapshot = SerializationPersistencePackPreviewFactory.CreateForWorkbench(new CanvasWorkbenchSurface
        {
            Nodes = [new CanvasWorkbenchNode { Id = "alpha" }],
            Links = [new CanvasWorkbenchLink { SourceId = "alpha", TargetId = "alpha" }],
            UiState = new CanvasWorkbenchUiState
            {
                SelectedNodeIds = ["alpha"]
            }
        });

        Assert.Contains("1 nodes restored", snapshot.Metrics);
        Assert.Contains("1 links restored", snapshot.Metrics);
        Assert.Contains("1 selected restored", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_serialization_preview_card()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<SerializationPersistencePackPreview>(
            parameters => parameters.Add(component => component.Snapshot, new SerializationPersistencePackPreviewSnapshot
            {
                Title = "Shared serialization now owns canvas state persistence and replay",
                Summary = "Round-trip JSON is shared.",
                StatePill = "Round-trip",
                Metrics = ["128 json chars", "11 nodes restored"]
            }));

        Assert.Contains("Shared serialization now owns canvas state persistence and replay", cut.Markup);
        Assert.Contains("11 nodes restored", cut.Markup);
    }
}


