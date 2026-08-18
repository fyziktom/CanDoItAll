using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components.Presentation;
using CanDoItAll.Conversations.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Conversations;

public sealed class ConversationShellHostTests
{
    [Fact]
    public void Catalog_merges_sources_filters_both_axes_and_routes_declared_actions_to_the_owner()
    {
        var agent = RecordingContributor.Create(
            "agents",
            ConversationParticipantKind.Agent,
            "agent:alpha",
            "Agent Alpha",
            "agent-action",
            failureMessage: "Agent source unavailable");
        var chat = RecordingContributor.Create(
            "simple-chats",
            ConversationParticipantKind.Chat,
            "chat:beta",
            "Chat Beta",
            "chat-action");
        using var context = CreateContext(agent, chat);
        var coordinator = context.Services.GetRequiredService<IConversationShellCoordinator>();
        coordinator.ShowCatalog();

        var cut = context.Render<ConversationShellHost>();

        cut.WaitForElement("[data-testid='agent-action']");
        Assert.Contains("Agent Alpha", cut.Markup);
        Assert.Contains("Chat Beta", cut.Markup);
        Assert.Contains("Agent source unavailable", cut.Markup);

        cut.Find("[data-testid='conversation-shell-filter-chats']").Click();

        Assert.DoesNotContain("Agent Alpha", cut.Markup);
        Assert.DoesNotContain("Agent source unavailable", cut.Markup);
        Assert.Contains("Chat Beta", cut.Markup);
        cut.Find("[data-testid='chat-action']").Click();
        Assert.Equal(
            new ParticipantActionRequest(new("chat:beta"), new("start")),
            Assert.Single(chat.ParticipantRequests));
        Assert.Empty(agent.ParticipantRequests);

        coordinator.ShowCatalog(
            ConversationCatalogKindFilter.All,
            ConversationCatalogLifecycle.Active);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Active Agent Alpha", cut.Markup);
            Assert.Contains("Active Chat Beta", cut.Markup);
        });
    }

    [Fact]
    public void Coordinator_renders_only_the_single_focused_descriptor_across_sources()
    {
        var agent = RecordingContributor.Create(
            "agents",
            ConversationParticipantKind.Agent,
            "agent:alpha",
            "Agent Alpha",
            "agent-action",
            windowId: "agent-window");
        var chat = RecordingContributor.Create(
            "simple-chats",
            ConversationParticipantKind.Chat,
            "chat:beta",
            "Chat Beta",
            "chat-action",
            windowId: "chat-window");
        using var context = CreateContext(agent, chat);
        var coordinator = context.Services.GetRequiredService<IConversationShellCoordinator>();
        var cut = context.Render<ConversationShellHost>();

        coordinator.FocusWindow("agents", "agent-window");

        cut.WaitForElement("[data-testid='agents-focused-window']");
        Assert.DoesNotContain("data-testid=\"simple-chats-focused-window\"", cut.Markup);

        coordinator.FocusWindow("simple-chats", "chat-window");

        cut.WaitForElement("[data-testid='simple-chats-focused-window']");
        Assert.DoesNotContain("data-testid=\"agents-focused-window\"", cut.Markup);
    }

    private static BunitContext CreateContext(params RecordingContributor[] contributors)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddConversationShell();
        foreach (var contributor in contributors)
        {
            context.Services.AddSingleton<IConversationShellContributor>(contributor);
        }

        return context;
    }

    private sealed class RecordingContributor(
        string sourceId,
        ConversationParticipantKind kind,
        ConversationShellContributorSnapshot snapshot) : IConversationShellContributor
    {
        public string SourceId => sourceId;

        public ConversationParticipantKind Kind => kind;

        public List<ParticipantActionRequest> ParticipantRequests { get; } = [];

        public event EventHandler? Changed;

        public static RecordingContributor Create(
            string sourceId,
            ConversationParticipantKind kind,
            string participantKey,
            string displayName,
            string actionTestId,
            string? failureMessage = null,
            string? windowId = null)
        {
            var participant = new ConversationParticipantPresentation(
                new(participantKey),
                displayName,
                searchText: displayName);
            var available = new ConversationShellParticipant(
                sourceId,
                kind,
                new(
                    participant,
                    [new(new("start"), "Start", "chat", actionTestId)]));
            var active = new ConversationShellActiveItem(
                sourceId,
                kind,
                new(
                    new($"active:{participantKey}"),
                    $"Active {displayName}",
                    [],
                    [new(new("open"), "Open", "open_in_new")]));
            ConversationShellWindowDescriptor[] windows = windowId is null
                ? []
                :
                [
                    new(
                        new(sourceId, windowId),
                        kind,
                        $"{sourceId}-focused-window",
                        $"{displayName} window",
                        "Conversation",
                        displayName,
                        null,
                        typeof(EmptyState),
                        new Dictionary<string, object>
                        {
                            [nameof(EmptyState.Title)] = displayName,
                            [nameof(EmptyState.Description)] = "Focused conversation content"
                        })
                ];
            return new(
                sourceId,
                kind,
                new([available], [active], windows, [], failureMessage));
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public ConversationShellContributorSnapshot Snapshot()
            => snapshot;

        public Task HandleParticipantActionAsync(
            ParticipantActionRequest request,
            CancellationToken cancellationToken = default)
        {
            ParticipantRequests.Add(request);
            return Task.CompletedTask;
        }

        public Task HandleActiveActionAsync(
            ConversationActionRequest request,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task HandleWindowCloseAsync(
            string windowId,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void PublishChanged()
            => Changed?.Invoke(this, EventArgs.Empty);
    }
}
