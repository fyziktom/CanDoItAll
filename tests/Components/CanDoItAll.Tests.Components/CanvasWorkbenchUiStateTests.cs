using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Tests.Components;

public sealed class CanvasWorkbenchUiStateTests
{
    [Fact]
    public void Ui_state_roundtrips_highlighted_node_ids_independently_from_selection()
    {
        var state = new CanvasWorkbenchUiState
        {
            SelectedNodeIds = ["selected"],
            HighlightedNodeIds = ["artifact", "artifact", " "]
        };

        var parsed = CanvasWorkbenchUiState.Parse(state.ToJson());

        Assert.Equal(["selected"], parsed.SelectedNodeIds);
        Assert.Equal(["artifact"], parsed.HighlightedNodeIds);
    }
}
