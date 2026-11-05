namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

internal sealed class LlmChatConversationAccessSession(ILlmChatConversationEditorAccess access, Func<Task> changed, Func<Task> retired)
    : IAsyncDisposable {
    private readonly Guid editorId = Guid.NewGuid();
    private readonly HashSet<LlmChatEditorOpening> openings = [];
    private LlmChatEditorOpening? catalog;
    private LlmChatEditorOpening? selection;
    private Task disposals = Task.CompletedTask;
    private long generation;
    private bool disposed;
    public bool IsCurrent => !disposed && catalog is { IsCurrent: true } && (selection is null || selection.IsCurrent);

    public async Task OpenCatalogAsync() {
        catalog = await OpenAsync(null, LlmChatConversationAccessKind.Read, changed);
    }

    public async Task OpenSelectionAsync(Guid id) {
        if (selection?.Target == id) {
            await AuthorizeReadAsync(CancellationToken.None);
            return;
        }
        var owner = ++generation;
        Retire(selection);
        selection = null;
        var opened = await OpenAsync(id, LlmChatConversationAccessKind.Execute, changed);
        if (disposed || owner != generation) {
            Retire(opened);
            throw new UnauthorizedAccessException("The original conversation selection was retired.");
        }
        selection = opened;
    }

    public async Task<LlmChatEditorOpening> OpenAsync(Guid? id, LlmChatConversationAccessKind kind, Func<Task> receive) {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (catalog is not null) {
            await AuthorizeReadAsync(CancellationToken.None);
        }
        var opened = await access.OpenAsync(id, kind, editorId, receive, retired, CancellationToken.None);
        if (disposed) {
            opened.Stop();
            await opened.DisposeAsync();
            throw new UnauthorizedAccessException("The conversation workspace was retired while opening access.");
        }
        openings.Add(opened);
        try {
            await opened.AuthorizeAsync(false, CancellationToken.None);
            if (catalog is not null) {
                await AuthorizeReadAsync(CancellationToken.None);
            }
            return opened;
        } catch {
            Retire(opened);
            throw;
        }
    }

    public async ValueTask AuthorizeReadAsync(CancellationToken token) {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (catalog is null) {
            throw new UnauthorizedAccessException("Open original conversation access before continuing.");
        }
        await catalog.AuthorizeAsync(false, token);
        if (selection is { } current) {
            await current.AuthorizeAsync(false, token);
        }
        await catalog.AuthorizeAsync(false, token);
    }

    public async Task<bool> RunOperationAsync(Func<Task<bool>> action, bool manage) {
        await AuthorizeReadAsync(CancellationToken.None);
        var opening = manage ? await OpenAsync(selection?.Target, LlmChatConversationAccessKind.Recovery, () => Task.CompletedTask)
            : selection ?? throw new UnauthorizedAccessException("Select a conversation before executing a command.");
        try {
            await opening.AuthorizeAsync(true, CancellationToken.None);
            using var admission = opening.EnterWrite(AuthorizeReadAsync);
            return await action();
        } finally {
            if (manage) {
                Retire(opening);
            }
        }
    }

    public void Retire(LlmChatEditorOpening? opening) {
        if (opening is null || !openings.Remove(opening)) {
            return;
        }
        opening.Stop();
        disposals = Task.WhenAll(disposals, opening.DisposeAsync().AsTask());
    }

    public void Stop() {
        if (disposed) {
            return;
        }
        disposed = true;
        generation++;
        foreach (var opening in openings.ToArray()) {
            Retire(opening);
        }
    }

    public async ValueTask DisposeAsync() {
        Stop();
        await disposals;
    }
}
