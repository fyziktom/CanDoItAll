using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Factory.CanvasAdapters;

namespace CanDoItAll.Tests.Components;

public sealed class PromptFactoryUndoRedoAdapterTests
{
    [Fact]
    public void Undo_restores_canvas_state_and_clears_non_prompt_root_selection()
    {
        var adapter = new PromptFactoryUndoRedoAdapter();
        var nodeId = Guid.NewGuid();
        var original = new PromptFactoryEditorModel
        {
            SessionName = "Original",
            CanvasUiStateJson = new CanvasWorkbenchUiState().ToJson()
        };
        var updated = new PromptFactoryEditorModel
        {
            SessionName = "Updated",
            CanvasUiStateJson = new CanvasWorkbenchUiState().ToJson()
        };

        adapter.Remember(original, ["session-root"], "context", "session-root");

        Assert.True(adapter.TryUndo(updated, [$"node:{nodeId:N}"], "review", $"node:{nodeId:N}", out var snapshot));

        var snapshotUiState = CanvasWorkbenchUiState.Parse(snapshot.CanvasUiStateJson);
        Assert.Equal("Original", snapshot.SessionName);
        Assert.Equal("context", snapshotUiState.ActiveInspectorTab);
        Assert.Null(snapshot.SelectedNodeId);
    }

    [Fact]
    public void Capture_snapshot_persists_selected_prompt_node_identifier()
    {
        var adapter = new PromptFactoryUndoRedoAdapter();
        var nodeId = Guid.NewGuid();

        var snapshot = adapter.CaptureSnapshot(
            new PromptFactoryEditorModel { CanvasUiStateJson = new CanvasWorkbenchUiState().ToJson() },
            [$"node:{nodeId:N}"],
            "review",
            $"node:{nodeId:N}");

        Assert.Equal(nodeId, snapshot.SelectedNodeId);
        Assert.Contains($"node:{nodeId:N}", snapshot.CanvasUiStateJson);
    }
}


