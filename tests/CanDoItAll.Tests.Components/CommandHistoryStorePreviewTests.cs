using Bunit;
using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.ComponentKit.Components;

namespace CanDoItAll.Tests.Components;

public sealed class CommandHistoryStorePreviewTests
{
    [Fact]
    public void Factory_reports_undo_and_redo_availability()
    {
        var snapshot = CommandHistoryStorePreviewFactory.CreateForWorkbench(new CanvasWorkbenchSurface());

        Assert.Contains("12 entry cap", snapshot.Metrics);
        Assert.Contains("Undo available", snapshot.Metrics);
        Assert.Contains("Redo available", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_command_history_preview_card()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<CommandHistoryStorePreview>(
            parameters => parameters.Add(component => component.Snapshot, new CommandHistoryStorePreviewSnapshot
            {
                Title = "Undo and redo snapshots now flow through one bounded command store",
                Summary = "History stacks stay deduplicated and bounded.",
                StatePill = "Armed",
                Metrics = ["12 entry cap", "Undo available", "Redo available"]
            }));

        Assert.Contains("Undo and redo snapshots now flow through one bounded command store", cut.Markup);
        Assert.Contains("Redo available", cut.Markup);
    }
}
