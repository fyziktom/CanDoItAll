using System.Text.Json;
using CanDoItAll.ComponentKit.Canvas;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace CanDoItAll.Modules.Factory.Pages;

public partial class PromptFactoryPage : IAsyncDisposable
{
    private const int MaxHistoryEntries = 40;
    private static readonly JsonSerializerOptions HistorySerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [SupplyParameterFromQuery(Name = "preview")]
    public bool ShowPromptPreviewQuery { get; set; }

    private readonly List<PromptFactoryEditorModel> undoHistory = [];
    private readonly List<PromptFactoryEditorModel> redoHistory = [];
    private bool showPromptPreviewDialog;
    private bool isRestoringHistory;
    private DotNetObjectReference<PromptFactoryPage>? historyShortcutReference;
    private bool historyShortcutsRegistered;

    private bool CanUndo => undoHistory.Count > 0;

    private bool CanRedo => redoHistory.Count > 0;

    private void RememberHistoryCheckpoint()
    {
        if (isRestoringHistory)
        {
            return;
        }

        var snapshot = CaptureHistorySnapshot();
        var snapshotJson = SerializeHistorySnapshot(snapshot);
        if (undoHistory.Count > 0 &&
            string.Equals(SerializeHistorySnapshot(undoHistory[^1]), snapshotJson, StringComparison.Ordinal))
        {
            return;
        }

        if (undoHistory.Count >= MaxHistoryEntries)
        {
            undoHistory.RemoveAt(0);
        }

        undoHistory.Add(snapshot);
        redoHistory.Clear();
    }

    private async Task UndoAsync()
    {
        if (!CanUndo)
        {
            SetMessage("Nothing to undo.");
            return;
        }

        redoHistory.Add(CaptureHistorySnapshot());
        var snapshot = undoHistory[^1];
        undoHistory.RemoveAt(undoHistory.Count - 1);
        await RestoreHistorySnapshotAsync(snapshot);
        SetMessage("Undo applied.");
    }

    private async Task RedoAsync()
    {
        if (!CanRedo)
        {
            SetMessage("Nothing to redo.");
            return;
        }

        undoHistory.Add(CaptureHistorySnapshot());
        var snapshot = redoHistory[^1];
        redoHistory.RemoveAt(redoHistory.Count - 1);
        await RestoreHistorySnapshotAsync(snapshot);
        SetMessage("Redo applied.");
    }

    private async Task HandleWindowKeyDownAsync(KeyboardEventArgs args)
    {
        if (!(args.CtrlKey || args.MetaKey) || args.AltKey)
        {
            return;
        }

        var key = args.Key?.Trim().ToLowerInvariant();
        if (key is not ("z" or "y"))
        {
            return;
        }

        var shouldHandleShortcut = await JS.InvokeAsync<bool>(
            "CanDoItAll.promptFactory.shouldHandleHistoryShortcut",
            Array.Empty<object>());
        if (!shouldHandleShortcut)
        {
            return;
        }

        if (key == "z" && args.ShiftKey)
        {
            await RedoAsync();
            return;
        }

        if (key == "z")
        {
            await UndoAsync();
            return;
        }

        await RedoAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!historyShortcutsRegistered)
        {
            historyShortcutReference ??= DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("CanDoItAll.promptFactory.registerHistoryShortcuts", historyShortcutReference);
            historyShortcutsRegistered = true;
        }
    }

    [JSInvokable]
    public Task HandleHistoryShortcutAsync(string key, bool ctrlKey, bool metaKey, bool shiftKey, bool altKey)
        => HandleWindowKeyDownAsync(new KeyboardEventArgs
        {
            Key = key,
            CtrlKey = ctrlKey,
            MetaKey = metaKey,
            ShiftKey = shiftKey,
            AltKey = altKey
        });

    private Task ClosePromptPreviewDialogAsync()
    {
        showPromptPreviewDialog = false;
        if (ShowPromptPreviewQuery && editor.SessionId.HasValue)
        {
            Navigation.NavigateTo(
                BuildPromptFactoryRoute(editor.SessionId.Value),
                new NavigationOptions { ReplaceHistoryEntry = true });
        }

        StateHasChanged();
        return Task.CompletedTask;
    }

    private string BuildPromptFactoryRoute(Guid sessionId, bool showPromptPreview = false)
        => showPromptPreview
            ? $"/prompt-factory?sessionId={sessionId}&preview=true"
            : $"/prompt-factory?sessionId={sessionId}";

    private PromptFactoryEditorModel CaptureHistorySnapshot()
    {
        var snapshot = CloneEditor(editor);
        var uiState = CanvasWorkbenchUiState.Parse(snapshot.CanvasUiStateJson);
        uiState.SelectedNodeIds = [.. selectedCanvasNodeIds];
        uiState.ActiveInspectorTab = activeInspectorTab;
        snapshot.CanvasUiStateJson = uiState.ToJson();
        snapshot.SelectedNodeId = TryParsePromptCanvasNodeId(selectedCanvasNodeId, out var nodeId) ? nodeId : null;
        return snapshot;
    }

    private async Task RestoreHistorySnapshotAsync(PromptFactoryEditorModel snapshot)
    {
        isRestoringHistory = true;
        showPromptPreviewDialog = false;

        try
        {
            var snapshotCopy = CloneEditor(snapshot);
            if (snapshotCopy.SessionId.HasValue)
            {
                var restoreResult = await PromptFactoryService.RestoreSessionStateAsync(snapshotCopy);
                if (restoreResult.IsFailure)
                {
                    SetError(restoreResult.Errors);
                    return;
                }

                editor = restoreResult.Value!;
            }
            else
            {
                editor = snapshotCopy;
            }

            await LoadResourcesAsync();
            HydrateCanvasSelection();
            RefreshCanvasSurface();
            SyncSelectedPromptNodeDraft();
        }
        finally
        {
            isRestoringHistory = false;
        }
    }

    private static PromptFactoryEditorModel CloneEditor(PromptFactoryEditorModel model)
        => JsonSerializer.Deserialize<PromptFactoryEditorModel>(
               JsonSerializer.Serialize(model, HistorySerializerOptions),
               HistorySerializerOptions)
           ?? new PromptFactoryEditorModel();

    private static string SerializeHistorySnapshot(PromptFactoryEditorModel snapshot)
        => JsonSerializer.Serialize(snapshot, HistorySerializerOptions);

    public async ValueTask DisposeAsync()
    {
        if (historyShortcutReference is not null)
        {
            try
            {
                await JS.InvokeVoidAsync("CanDoItAll.promptFactory.unregisterHistoryShortcuts", historyShortcutReference);
            }
            catch (JSDisconnectedException)
            {
            }

            historyShortcutReference.Dispose();
            historyShortcutReference = null;
        }
    }
}
