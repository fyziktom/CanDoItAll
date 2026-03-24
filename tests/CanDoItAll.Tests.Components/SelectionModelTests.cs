using CanDoItAll.ComponentKit.Canvas;

namespace CanDoItAll.Tests.Components;

public sealed class SelectionModelTests
{
    [Fact]
    public void From_deduplicates_ids_and_promotes_the_primary_selection()
    {
        var selection = SelectionModel.From(["beta", "alpha", "beta", "gamma"], "alpha");

        Assert.Equal("alpha", selection.PrimaryNodeId);
        Assert.Equal(["alpha", "beta", "gamma"], selection.SelectedNodeIds);
    }

    [Fact]
    public void Toggle_removes_the_primary_id_and_promotes_the_next_item()
    {
        var selection = SelectionModel.From(["alpha", "beta", "gamma"]);

        var updated = selection.Toggle("alpha");

        Assert.Equal("beta", updated.PrimaryNodeId);
        Assert.Equal(["beta", "gamma"], updated.SelectedNodeIds);
    }

    [Fact]
    public void RemoveMissing_filters_deleted_ids_and_keeps_valid_order()
    {
        var selection = SelectionModel.From(["alpha", "beta", "gamma"], "beta");

        var updated = selection.RemoveMissing(["alpha", "gamma"]);

        Assert.Equal("alpha", updated.PrimaryNodeId);
        Assert.Equal(["alpha", "gamma"], updated.SelectedNodeIds);
    }
}
