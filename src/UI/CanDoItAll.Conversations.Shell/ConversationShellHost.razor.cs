using CanDoItAll.Components.OverlayLib;
using CanDoItAll.Conversations.Components.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Conversations.Shell;

public partial class ConversationShellHost
{
    private static readonly (ConversationCatalogKindFilter Value, string Label, string Icon)[] KindFilters =
    [
        (ConversationCatalogKindFilter.All, "All", "forum"),
        (ConversationCatalogKindFilter.Agents, "Agents", "smart_toy"),
        (ConversationCatalogKindFilter.Chats, "Chats", "chat")
    ];

    private readonly CancellationTokenSource lifetime = new();
    private readonly Dictionary<ConversationShellWindowKey, OverlayWindowState> windowStates = [];
    private IReadOnlyList<IConversationShellContributor> contributors = [];
    private ConversationShellState state = new(
        false,
        ConversationCatalogKindFilter.All,
        ConversationCatalogLifecycle.Available,
        null);
    private OverlayWindowState catalogWindowState = new() { IsVisible = true };
    private string searchText = string.Empty;
    private bool attached;
    private bool isInitializing;
    private int disposed;

    private int SelectedLifecycleIndex
        => state.Lifecycle == ConversationCatalogLifecycle.Active ? 1 : 0;

    private IReadOnlyList<ConversationParticipantCompactItemPresentation> AvailableItems
        => ContributorSnapshots
            .SelectMany(snapshot => snapshot.Snapshot.Available)
            .Where(item => MatchesKind(item.Kind))
            .Where(item => MatchesSearch(item.Presentation.Participant.SearchText))
            .OrderBy(item => item.Presentation.Participant.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Presentation)
            .ToArray();

    private IReadOnlyList<ConversationActiveItemPresentation> ActiveItems
        => ContributorSnapshots
            .SelectMany(snapshot => snapshot.Snapshot.Active)
            .Where(item => MatchesKind(item.Kind))
            .Where(item => MatchesSearch(item.Presentation.DisplayName))
            .OrderBy(item => item.Presentation.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Presentation)
            .ToArray();

    private IReadOnlyList<PresentationBadge> VisibleStatusBadges
        => state.KindFilter switch
        {
            ConversationCatalogKindFilter.Agents => ContributorSnapshots
                .Where(snapshot => snapshot.Kind == ConversationParticipantKind.Agent)
                .SelectMany(snapshot => snapshot.Snapshot.StatusBadges)
                .ToArray(),
            ConversationCatalogKindFilter.Chats => ContributorSnapshots
                .Where(snapshot => snapshot.Kind == ConversationParticipantKind.Chat)
                .SelectMany(snapshot => snapshot.Snapshot.StatusBadges)
                .ToArray(),
            _ => []
        };

    private IReadOnlyList<string> VisibleFailureMessages
        => ContributorSnapshots
            .Where(snapshot => MatchesKind(snapshot.Kind))
            .Select(snapshot => snapshot.Snapshot.FailureMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Select(message => message!)
            .ToArray();

    private ConversationShellWindowDescriptor? FocusedWindow
    {
        get
        {
            if (state.FocusedWindow is not { } focused)
            {
                return null;
            }

            return ContributorSnapshots
                .SelectMany(snapshot => snapshot.Snapshot.Windows)
                .FirstOrDefault(window => window.Key == focused);
        }
    }

    private IReadOnlyList<(ConversationParticipantKind Kind, ConversationShellContributorSnapshot Snapshot)> ContributorSnapshots
        => contributors.Select(item => (item.Kind, item.Snapshot())).ToArray();

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || Volatile.Read(ref disposed) != 0)
        {
            return Task.CompletedTask;
        }

        contributors = Contributors
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.SourceId, StringComparer.Ordinal)
            .ToArray();
        ValidateContributors(contributors);
        Coordinator.Changed += HandleShellChanged;
        foreach (var contributor in contributors)
        {
            contributor.Changed += HandleContributorChanged;
        }

        attached = true;
        state = Coordinator.Snapshot();
        _ = InitializeContributorsAsync();
        return Task.CompletedTask;
    }

    private async Task InitializeContributorsAsync()
    {
        isInitializing = true;
        await InvokeAsync(StateHasChanged);
        foreach (var contributor in contributors)
        {
            try
            {
                await contributor.InitializeAsync(lifetime.Token);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    exception,
                    "Unable to initialize conversation shell contributor. SourceId={SourceId} FailureType={FailureType}.",
                    contributor.SourceId,
                    exception.GetType().Name);
            }
        }

        isInitializing = false;
        if (Volatile.Read(ref disposed) == 0)
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private Task HandleLifecycleChangedAsync(int selectedIndex)
    {
        Coordinator.ShowCatalog(
            state.KindFilter,
            selectedIndex == 1
                ? ConversationCatalogLifecycle.Active
                : ConversationCatalogLifecycle.Available);
        return Task.CompletedTask;
    }

    private void ChangeKindFilter(ConversationCatalogKindFilter kindFilter)
        => Coordinator.ShowCatalog(kindFilter, state.Lifecycle);

    private Task HandleSearchChangedAsync(string? value)
    {
        searchText = value?.Trim() ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleCatalogWindowStateChangedAsync(OverlayWindowState value)
    {
        catalogWindowState = OverlayWindowState.Normalize(value);
        if (!catalogWindowState.IsVisible)
        {
            Coordinator.HideCatalog();
        }

        return Task.CompletedTask;
    }

    private OverlayWindowState ResolveCatalogWindowState()
    {
        catalogWindowState = OverlayWindowState.Normalize(catalogWindowState);
        catalogWindowState.IsVisible = true;
        return catalogWindowState;
    }

    private OverlayWindowState ResolveFocusedWindowState(ConversationShellWindowDescriptor window)
    {
        if (!windowStates.TryGetValue(window.Key, out var windowState))
        {
            windowState = new OverlayWindowState { IsVisible = true };
        }

        windowState = OverlayWindowState.Normalize(windowState);
        windowState.IsVisible = true;
        windowStates[window.Key] = windowState;
        return windowState;
    }

    private async Task HandleFocusedWindowStateChangedAsync(
        ConversationShellWindowDescriptor window,
        OverlayWindowState value)
    {
        var normalized = OverlayWindowState.Normalize(value);
        var closeRequested = !normalized.IsVisible;
        normalized.IsVisible = true;
        windowStates[window.Key] = normalized;
        if (!closeRequested)
        {
            return;
        }

        var contributor = ResolveContributor(window.Key.SourceId);
        await contributor.HandleWindowCloseAsync(window.Key.WindowId, lifetime.Token);
    }

    private async Task HandleParticipantDoubleClickedAsync(ConversationPresentationKey key)
    {
        var participant = ResolveParticipant(key);
        var action = participant.Presentation.Actions.FirstOrDefault(item => !item.IsDisabled);
        if (action is null)
        {
            return;
        }

        await ResolveContributor(participant.SourceId).HandleParticipantActionAsync(
            new(key, action.Key),
            lifetime.Token);
    }

    private Task HandleParticipantActionAsync(ParticipantActionRequest request)
    {
        var participant = ResolveParticipant(request.ParticipantKey);
        return ResolveContributor(participant.SourceId).HandleParticipantActionAsync(request, lifetime.Token);
    }

    private Task HandleActiveActionAsync(ConversationActionRequest request)
    {
        var activeItem = ResolveActiveItem(request.ItemKey);
        return ResolveContributor(activeItem.SourceId).HandleActiveActionAsync(request, lifetime.Token);
    }

    private ConversationShellParticipant ResolveParticipant(ConversationPresentationKey key)
        => ContributorSnapshots
            .SelectMany(snapshot => snapshot.Snapshot.Available)
            .SingleOrDefault(item => item.Presentation.Participant.Key == key)
            ?? throw new InvalidOperationException($"Conversation participant '{key.Value}' is not available.");

    private ConversationShellActiveItem ResolveActiveItem(ConversationPresentationKey key)
        => ContributorSnapshots
            .SelectMany(snapshot => snapshot.Snapshot.Active)
            .SingleOrDefault(item => item.Presentation.Key == key)
            ?? throw new InvalidOperationException($"Active conversation '{key.Value}' is not available.");

    private IConversationShellContributor ResolveContributor(string sourceId)
        => contributors.Single(item => string.Equals(item.SourceId, sourceId, StringComparison.Ordinal));

    private bool MatchesKind(ConversationParticipantKind kind)
        => state.KindFilter switch
        {
            ConversationCatalogKindFilter.All => true,
            ConversationCatalogKindFilter.Agents => kind == ConversationParticipantKind.Agent,
            ConversationCatalogKindFilter.Chats => kind == ConversationParticipantKind.Chat,
            _ => throw new ArgumentOutOfRangeException(nameof(state.KindFilter), state.KindFilter, "Unknown conversation kind filter.")
        };

    private bool MatchesSearch(string value)
        => string.IsNullOrWhiteSpace(searchText) ||
           value.Contains(searchText, StringComparison.OrdinalIgnoreCase);

    private void HandleShellChanged(object? sender, EventArgs eventArgs)
    {
        if (Volatile.Read(ref disposed) != 0)
        {
            return;
        }

        _ = InvokeAsync(() =>
        {
            state = Coordinator.Snapshot();
            StateHasChanged();
        });
    }

    private void HandleContributorChanged(object? sender, EventArgs eventArgs)
    {
        if (Volatile.Read(ref disposed) == 0)
        {
            _ = InvokeAsync(StateHasChanged);
        }
    }

    private static void ValidateContributors(IReadOnlyList<IConversationShellContributor> items)
    {
        var duplicate = items
            .GroupBy(item => item.SourceId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Conversation contributor source id '{duplicate.Key}' is registered more than once.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        if (attached)
        {
            Coordinator.Changed -= HandleShellChanged;
            foreach (var contributor in contributors)
            {
                contributor.Changed -= HandleContributorChanged;
            }
        }

        await lifetime.CancelAsync();
        lifetime.Dispose();
    }
}
