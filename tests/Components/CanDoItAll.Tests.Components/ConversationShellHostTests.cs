using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components.Presentation;
using CanDoItAll.Conversations.Components;
using CanDoItAll.Components.OverlayLib;
using CanDoItAll.Conversations.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Components.Conversations;

public sealed class ConversationShellHostTests
{
    [Fact]
    public async Task Focusing_a_minimized_window_reveals_its_guard_without_replacing_the_editor_or_geometry() {
        var chat = RecordingContributor.Create("simple-chats", ConversationParticipantKind.Chat, "chat:beta", "Chat Beta",
            "chat-action", windowId: "chat-window", keepContentMounted: true);
        using var context = CreateContext(chat);
        var coordinator = context.Services.GetRequiredService<IConversationShellCoordinator>();
        var cut = context.Render<ConversationShellHost>();
        coordinator.FocusWindow("simple-chats", "chat-window");
        cut.WaitForElement("[data-testid='simple-chats-focused-window']");
        var content = cut.FindComponents<EmptyState>().Single(component => component.Instance.Title == "Chat Beta").Instance;
        var window = cut.FindComponent<ConversationFloatingWindow>();
        await cut.InvokeAsync(() => window.Instance.StateChanged.InvokeAsync(new OverlayWindowState {
            IsVisible = true, IsMinimized = true, Left = 30, Top = 40, Width = 740, Height = 640
        }));
        Assert.True(window.Instance.State.IsMinimized);

        coordinator.FocusWindow("simple-chats", "chat-window");

        cut.WaitForAssertion(() => Assert.False(window.Instance.State.IsMinimized));
        Assert.Same(content, cut.FindComponents<EmptyState>().Single(component => component.Instance.Title == "Chat Beta").Instance);
        Assert.Equal(30, window.Instance.State.Left);
        Assert.Equal(40, window.Instance.State.Top);
        Assert.Equal(740, window.Instance.State.Width);
        Assert.Equal(640, window.Instance.State.Height);
    }

    [Fact]
    public void Retained_window_keeps_the_same_content_when_hidden_or_another_source_gets_focus() {
        var chat = RecordingContributor.Create("simple-chats", ConversationParticipantKind.Chat, "chat:beta", "Chat Beta",
            "chat-action", windowId: "chat-window", keepContentMounted: true);
        var agent = RecordingContributor.Create("agents", ConversationParticipantKind.Agent, "agent:alpha", "Agent Alpha",
            "agent-action", windowId: "agent-window");
        using var context = CreateContext(chat, agent);
        var coordinator = context.Services.GetRequiredService<IConversationShellCoordinator>();
        var cut = context.Render<ConversationShellHost>();
        coordinator.FocusWindow("simple-chats", "chat-window");
        cut.WaitForElement("[data-testid='simple-chats-focused-window']");
        var content = cut.FindComponents<EmptyState>().Single(component => component.Instance.Title == "Chat Beta").Instance;

        coordinator.ClearFocusedWindow("simple-chats", "chat-window");
        cut.WaitForAssertion(() => Assert.Same(content,
            cut.FindComponents<EmptyState>().Single(component => component.Instance.Title == "Chat Beta").Instance));
        Assert.True(cut.Find("[data-testid='simple-chats-focused-window']").ParentElement!.HasAttribute("hidden"));
        coordinator.FocusWindow("agents", "agent-window");

        cut.WaitForAssertion(() => Assert.Same(content,
            cut.FindComponents<EmptyState>().Single(component => component.Instance.Title == "Chat Beta").Instance));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disposal_retires_initialization_without_waiting_for_a_contributor(bool observeCancellation) {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = RecordingContributor.Create("agents", ConversationParticipantKind.Agent, "a", "A", "a-action");
        var second = RecordingContributor.Create("chats", ConversationParticipantKind.Chat, "b", "B", "b-action");
        first.Initialize = async token => {
            started.SetResult();
            if (observeCancellation) {
                await release.Task.WaitAsync(token);
            } else {
                await release.Task;
                using var registration = token.Register(() => { });
            }
        };
        await using var context = CreateContext(first, second);
        var logger = new ShellLogger();
        context.Services.AddSingleton<ILogger<ConversationShellHost>>(logger);
        var cut = context.Render<ConversationShellHost>();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var initialization = cut.Instance.Initialization;

        await context.DisposeRenderedComponentsAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, first.Subscribers);
        Assert.Equal(0, second.Subscribers);
        Assert.Equal(0, second.InitializeCalls);
        var retiredRenderCount = cut.RenderCount;
        first.Initialize = _ => Task.CompletedTask;
        var replacement = context.Render<ConversationShellHost>();
        await replacement.Instance.Initialization.WaitAsync(TimeSpan.FromSeconds(10));
        var replacementRenderCount = replacement.RenderCount;
        release.TrySetResult();
        await initialization.WaitAsync(TimeSpan.FromSeconds(10));
        await context.Renderer.Dispatcher.InvokeAsync(() => { });

        Assert.Equal(1, second.InitializeCalls);
        Assert.Equal(retiredRenderCount, cut.RenderCount);
        Assert.Equal(replacementRenderCount, replacement.RenderCount);
        Assert.Empty(logger.Errors);
        Assert.False(initialization.IsFaulted);
    }

    [Fact]
    public async Task Genuine_live_initialization_failure_is_observed_and_the_next_source_still_initializes() {
        var first = RecordingContributor.Create("agents", ConversationParticipantKind.Agent, "a", "A", "a-action");
        var second = RecordingContributor.Create("chats", ConversationParticipantKind.Chat, "b", "B", "b-action");
        first.Initialize = _ => Task.FromException(new InvalidOperationException("Controlled source failure"));
        await using var context = CreateContext(first, second);
        var logger = new ShellLogger();
        context.Services.AddSingleton<ILogger<ConversationShellHost>>(logger);
        var cut = context.Render<ConversationShellHost>();
        await cut.Instance.Initialization.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, second.InitializeCalls);
        var error = Assert.Single(logger.Errors);
        Assert.IsType<InvalidOperationException>(error.Exception);
        Assert.Contains("agents", error.Message);
        Assert.Contains(nameof(InvalidOperationException), error.Message);
    }

    [Fact]
    public async Task Notifications_queued_before_disposal_do_not_read_or_render_the_retired_host() {
        var source = RecordingContributor.Create("agents", ConversationParticipantKind.Agent, "a", "A", "a-action");
        await using var context = CreateContext(source);
        var coordinator = new CountingCoordinator();
        context.Services.AddSingleton<IConversationShellCoordinator>(coordinator);
        var logger = new ShellLogger();
        context.Services.AddSingleton<ILogger<ConversationShellHost>>(logger);
        var cut = context.Render<ConversationShellHost>();
        await cut.Instance.Initialization;
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = Task.Run(() => context.Renderer.Dispatcher.InvokeAsync(async () => {
            entered.SetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10))) {
                throw new TimeoutException("The test did not release the shell dispatcher.");
            }
            await cut.Instance.DisposeAsync();
        }));
        var reads = coordinator.SnapshotCalls;
        var renders = cut.RenderCount;
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            coordinator.Publish();
            source.PublishChanged();
        } finally {
            release.Set();
            await work.WaitAsync(TimeSpan.FromSeconds(10));
        }
        await context.Renderer.Dispatcher.InvokeAsync(() => { });
        Assert.Equal(reads, coordinator.SnapshotCalls);
        Assert.Equal(renders, cut.RenderCount);
        Assert.Empty(logger.Errors);
        await context.DisposeRenderedComponentsAsync();
    }

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

        private EventHandler? changed;
        public int Subscribers { get; private set; }
        public int InitializeCalls { get; private set; }
        public Func<CancellationToken, Task> Initialize { get; set; } = _ => Task.CompletedTask;
        public event EventHandler? Changed {
            add {
                changed += value;
                Subscribers++;
            }
            remove {
                changed -= value;
                Subscribers--;
            }
        }

        public static RecordingContributor Create(
            string sourceId,
            ConversationParticipantKind kind,
            string participantKey,
            string displayName,
            string actionTestId,
            string? failureMessage = null,
            string? windowId = null,
            bool keepContentMounted = false)
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
                        }, keepContentMounted: keepContentMounted)
                ];
            return new(
                sourceId,
                kind,
                new([available], [active], windows, [], failureMessage));
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default) {
            InitializeCalls++;
            return Initialize(cancellationToken);
        }

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
            => changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class ShellLogger : ILogger<ConversationShellHost> {
        public List<(Exception? Exception, string Message)> Errors { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) {
            if (logLevel >= LogLevel.Error) {
                Errors.Add((exception, formatter(state, exception)));
            }
        }
    }

    private sealed class CountingCoordinator : IConversationShellCoordinator {
        private readonly ConversationShellCoordinator inner = new();
        public int SnapshotCalls { get; private set; }
        public event EventHandler? Changed;
        public ConversationShellState Snapshot() {
            SnapshotCalls++;
            return inner.Snapshot();
        }
        public void Publish() => Changed?.Invoke(this, EventArgs.Empty);
        public void ShowCatalog(ConversationCatalogKindFilter kindFilter = ConversationCatalogKindFilter.All,
            ConversationCatalogLifecycle lifecycle = ConversationCatalogLifecycle.Available) => inner.ShowCatalog(kindFilter, lifecycle);
        public void HideCatalog() => inner.HideCatalog();
        public void FocusWindow(string sourceId, string windowId) => inner.FocusWindow(sourceId, windowId);
        public void ClearFocusedWindow(string sourceId, string windowId) => inner.ClearFocusedWindow(sourceId, windowId);
    }
}
