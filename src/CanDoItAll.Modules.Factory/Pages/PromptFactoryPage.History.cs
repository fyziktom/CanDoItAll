using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Factory.CanvasAdapters;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace CanDoItAll.Modules.Factory.Pages;

public partial class PromptFactoryPage : IAsyncDisposable
{
    [SupplyParameterFromQuery(Name = "preview")]
    public bool ShowPromptPreviewQuery { get; set; }

    private bool showPromptPreviewDialog;
    private bool isRestoringHistory;
    private DotNetObjectReference<PromptFactoryPage>? historyShortcutReference;
    private bool historyShortcutsRegistered;

    private bool CanUndo => undoRedoAdapter.CanUndo;

    private bool CanRedo => undoRedoAdapter.CanRedo;

    private void RememberHistoryCheckpoint()
    {
        if (isRestoringHistory)
        {
            return;
        }

        undoRedoAdapter.Remember(editor, selectedCanvasNodeIds, activeInspectorTab, selectedCanvasNodeId);
    }

    private async Task UndoAsync()
    {
        if (!CanUndo)
        {
            SetMessage("Nothing to undo.");
            return;
        }

        if (!undoRedoAdapter.TryUndo(editor, selectedCanvasNodeIds, activeInspectorTab, selectedCanvasNodeId, out var snapshot))
        {
            SetMessage("Nothing to undo.");
            return;
        }

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

        if (!undoRedoAdapter.TryRedo(editor, selectedCanvasNodeIds, activeInspectorTab, selectedCanvasNodeId, out var snapshot))
        {
            SetMessage("Nothing to redo.");
            return;
        }

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
            "CanDoItAll.promptFactoryUndoRedo.shouldHandleHistoryShortcut",
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
            await JS.InvokeVoidAsync("CanDoItAll.promptFactoryUndoRedo.registerHistoryShortcuts", historyShortcutReference);
            historyShortcutsRegistered = true;
        }

        await SyncFloatingInspectorAsync();
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

    private async Task RestoreHistorySnapshotAsync(PromptFactoryEditorModel snapshot)
    {
        isRestoringHistory = true;
        showPromptPreviewDialog = false;

        try
        {
            var snapshotCopy = PromptFactoryUndoRedoAdapter.CloneEditor(snapshot);
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

    public async ValueTask DisposeAsync()
    {
        CancelPendingCanvasUiStatePersistence();
        if (historyShortcutReference is not null)
        {
            try
            {
                await JS.InvokeVoidAsync("CanDoItAll.promptFactoryUndoRedo.unregisterHistoryShortcuts", historyShortcutReference);
            }
            catch (JSDisconnectedException)
            {
            }

            historyShortcutReference.Dispose();
            historyShortcutReference = null;
        }
    }
}


