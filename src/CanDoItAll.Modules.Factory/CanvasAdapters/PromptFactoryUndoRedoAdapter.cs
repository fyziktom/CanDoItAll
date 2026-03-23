using System.Text.Json;
using CanDoItAll.ComponentKit.Canvas;

namespace CanDoItAll.Modules.Factory.CanvasAdapters;

public sealed class PromptFactoryUndoRedoAdapter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CommandHistoryStore<PromptFactoryEditorModel> historyStore = new(SerializeSnapshot);

    public bool CanUndo => historyStore.CanUndo;

    public bool CanRedo => historyStore.CanRedo;

    public void Clear()
        => historyStore.Clear();

    public void Remember(
        PromptFactoryEditorModel editor,
        IReadOnlyList<string> selectedCanvasNodeIds,
        string activeInspectorTab,
        string? primaryCanvasNodeId)
    {
        historyStore.Remember(CaptureSnapshot(editor, selectedCanvasNodeIds, activeInspectorTab, primaryCanvasNodeId));
    }

    public bool TryUndo(
        PromptFactoryEditorModel currentEditor,
        IReadOnlyList<string> selectedCanvasNodeIds,
        string activeInspectorTab,
        string? primaryCanvasNodeId,
        out PromptFactoryEditorModel snapshot)
    {
        return historyStore.TryUndo(
            CaptureSnapshot(currentEditor, selectedCanvasNodeIds, activeInspectorTab, primaryCanvasNodeId),
            out snapshot);
    }

    public bool TryRedo(
        PromptFactoryEditorModel currentEditor,
        IReadOnlyList<string> selectedCanvasNodeIds,
        string activeInspectorTab,
        string? primaryCanvasNodeId,
        out PromptFactoryEditorModel snapshot)
    {
        return historyStore.TryRedo(
            CaptureSnapshot(currentEditor, selectedCanvasNodeIds, activeInspectorTab, primaryCanvasNodeId),
            out snapshot);
    }

    public PromptFactoryEditorModel CaptureSnapshot(
        PromptFactoryEditorModel editor,
        IReadOnlyList<string> selectedCanvasNodeIds,
        string activeInspectorTab,
        string? primaryCanvasNodeId)
    {
        var snapshot = CloneEditor(editor);
        var uiState = CanvasWorkbenchUiState.Parse(snapshot.CanvasUiStateJson);
        uiState.SelectedNodeIds = [.. selectedCanvasNodeIds];
        uiState.ActiveInspectorTab = activeInspectorTab;
        snapshot.CanvasUiStateJson = uiState.ToJson();
        snapshot.SelectedNodeId = ResolvePromptNodeId(primaryCanvasNodeId);
        return snapshot;
    }

    public static PromptFactoryEditorModel CloneEditor(PromptFactoryEditorModel model)
        => JsonSerializer.Deserialize<PromptFactoryEditorModel>(
               JsonSerializer.Serialize(model, SerializerOptions),
               SerializerOptions)
           ?? new PromptFactoryEditorModel();

    private static Guid? ResolvePromptNodeId(string? canvasNodeId)
    {
        if (string.IsNullOrWhiteSpace(canvasNodeId) ||
            !canvasNodeId.StartsWith("node:", StringComparison.Ordinal) ||
            !Guid.TryParse(canvasNodeId["node:".Length..], out var nodeId))
        {
            return null;
        }

        return nodeId;
    }

    private static string SerializeSnapshot(PromptFactoryEditorModel snapshot)
        => JsonSerializer.Serialize(snapshot, SerializerOptions);
}
