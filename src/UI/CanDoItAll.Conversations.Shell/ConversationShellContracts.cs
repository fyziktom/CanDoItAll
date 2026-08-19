using CanDoItAll.Components.OverlayLib;
using CanDoItAll.Conversations.Components.Presentation;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Conversations.Shell;

public enum ConversationParticipantKind
{
    Agent,
    Chat
}

public enum ConversationCatalogKindFilter
{
    All,
    Agents,
    Chats
}

public enum ConversationCatalogLifecycle
{
    Available,
    Active
}

public sealed record ConversationShellWindowKey
{
    public ConversationShellWindowKey(string sourceId, string windowId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(windowId);
        SourceId = sourceId;
        WindowId = windowId;
    }

    public string SourceId { get; }

    public string WindowId { get; }
}

public sealed record ConversationShellParticipant(
    string SourceId,
    ConversationParticipantKind Kind,
    ConversationParticipantCompactItemPresentation Presentation);

public sealed record ConversationShellActiveItem(
    string SourceId,
    ConversationParticipantKind Kind,
    ConversationActiveItemPresentation Presentation);

public sealed record ConversationShellWindowDescriptor
{
    public ConversationShellWindowDescriptor(
        ConversationShellWindowKey key,
        ConversationParticipantKind kind,
        string testId,
        string ariaLabel,
        string kicker,
        string title,
        string? summary,
        Type componentType,
        IDictionary<string, object>? parameters = null,
        string defaultPlacement = "top-left",
        double defaultWidth = 760,
        double defaultHeight = 720,
        double minWidth = 520,
        double minHeight = 420,
        double maxWidth = 1100,
        double maxHeight = 920)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(testId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ariaLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(kicker);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(componentType);
        if (!typeof(IComponent).IsAssignableFrom(componentType))
        {
            throw new ArgumentException("A conversation window descriptor requires a Blazor component type.", nameof(componentType));
        }

        Key = key;
        Kind = kind;
        TestId = testId;
        AriaLabel = ariaLabel;
        Kicker = kicker;
        Title = title;
        Summary = summary;
        ComponentType = componentType;
        Parameters = parameters is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(parameters, StringComparer.Ordinal);
        DefaultPlacement = defaultPlacement;
        DefaultWidth = defaultWidth;
        DefaultHeight = defaultHeight;
        MinWidth = minWidth;
        MinHeight = minHeight;
        MaxWidth = maxWidth;
        MaxHeight = maxHeight;
    }

    public ConversationShellWindowKey Key { get; }

    public ConversationParticipantKind Kind { get; }

    public string TestId { get; }

    public string AriaLabel { get; }

    public string Kicker { get; }

    public string Title { get; }

    public string? Summary { get; }

    public Type ComponentType { get; }

    public IDictionary<string, object> Parameters { get; }

    public string DefaultPlacement { get; }

    public double DefaultWidth { get; }

    public double DefaultHeight { get; }

    public double MinWidth { get; }

    public double MinHeight { get; }

    public double MaxWidth { get; }

    public double MaxHeight { get; }
}

public sealed record ConversationShellContributorSnapshot(
    IReadOnlyList<ConversationShellParticipant> Available,
    IReadOnlyList<ConversationShellActiveItem> Active,
    IReadOnlyList<ConversationShellWindowDescriptor> Windows,
    IReadOnlyList<PresentationBadge> StatusBadges,
    string? FailureMessage = null)
{
    public static ConversationShellContributorSnapshot Empty { get; } = new([], [], [], []);
}

public interface IConversationShellContributor
{
    string SourceId { get; }

    ConversationParticipantKind Kind { get; }

    event EventHandler? Changed;

    Task InitializeAsync(CancellationToken cancellationToken = default);

    ConversationShellContributorSnapshot Snapshot();

    Task HandleParticipantActionAsync(
        ParticipantActionRequest request,
        CancellationToken cancellationToken = default);

    Task HandleActiveActionAsync(
        ConversationActionRequest request,
        CancellationToken cancellationToken = default);

    Task HandleWindowCloseAsync(
        string windowId,
        CancellationToken cancellationToken = default);
}

public sealed record ConversationShellState(
    bool IsCatalogVisible,
    ConversationCatalogKindFilter KindFilter,
    ConversationCatalogLifecycle Lifecycle,
    ConversationShellWindowKey? FocusedWindow);

public interface IConversationShellLauncher
{
    void ShowCatalog(
        ConversationCatalogKindFilter kindFilter = ConversationCatalogKindFilter.All,
        ConversationCatalogLifecycle lifecycle = ConversationCatalogLifecycle.Available);

    void HideCatalog();

    void FocusWindow(string sourceId, string windowId);

    void ClearFocusedWindow(string sourceId, string windowId);
}

public interface IConversationShellCoordinator : IConversationShellLauncher
{
    event EventHandler? Changed;

    ConversationShellState Snapshot();
}
