namespace CanDoItAll.Conversations.Shell;

public sealed class ConversationShellCoordinator : IConversationShellCoordinator
{
    private readonly object gate = new();
    private ConversationShellState state = new(
        false,
        ConversationCatalogKindFilter.All,
        ConversationCatalogLifecycle.Available,
        null);

    public event EventHandler? Changed;

    public ConversationShellState Snapshot()
    {
        lock (gate)
        {
            return state;
        }
    }

    public void ShowCatalog(
        ConversationCatalogKindFilter kindFilter = ConversationCatalogKindFilter.All,
        ConversationCatalogLifecycle lifecycle = ConversationCatalogLifecycle.Available)
    {
        lock (gate)
        {
            state = state with
            {
                IsCatalogVisible = true,
                KindFilter = kindFilter,
                Lifecycle = lifecycle
            };
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void HideCatalog()
    {
        lock (gate)
        {
            state = state with { IsCatalogVisible = false };
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void FocusWindow(string sourceId, string windowId)
    {
        var key = new ConversationShellWindowKey(sourceId, windowId);
        lock (gate)
        {
            state = state with { FocusedWindow = key };
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ClearFocusedWindow(string sourceId, string windowId)
    {
        lock (gate)
        {
            if (state.FocusedWindow is not { } focused ||
                !string.Equals(focused.SourceId, sourceId, StringComparison.Ordinal) ||
                !string.Equals(focused.WindowId, windowId, StringComparison.Ordinal))
            {
                return;
            }

            state = state with { FocusedWindow = null };
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
