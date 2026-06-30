using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class InvalidationSchedulerPreviewTests
{
    [Fact]
    public void Factory_reports_consumed_invalidation_reasons()
    {
        var snapshot = InvalidationSchedulerPreviewFactory.CreateForWorkbench(
            new CanvasWorkbenchSurface
            {
                UiState = new CanvasWorkbenchUiState
                {
                    ShowDiagnostics = true
                }
            },
            SelectionModel.From(["alpha"]));

        Assert.Contains("Data", snapshot.Metrics);
        Assert.Contains("Viewport", snapshot.Metrics);
        Assert.Contains("Selection", snapshot.Metrics);
        Assert.Contains("Diagnostics", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_invalidation_scheduler_preview_card()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<InvalidationSchedulerPreview>(
            parameters => parameters.Add(component => component.Snapshot, new InvalidationSchedulerPreviewSnapshot
            {
                Title = "Invalidation reasons are batched before the canvas republishes state",
                Summary = "Refresh causes stay coalesced.",
                StatePill = "Drained",
                Metrics = ["Data", "Viewport", "Selection"]
            }));

        Assert.Contains("Invalidation reasons are batched before the canvas republishes state", cut.Markup);
        Assert.Contains("Selection", cut.Markup);
    }
}


