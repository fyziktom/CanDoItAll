using Bunit;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.UI;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using static CanDoItAll.Tests.Components.LlmChats.LlmChatConversationWorkspaceTests;

namespace CanDoItAll.Tests.Components.LlmChats;

public sealed class ConversationWorkspaceSessionTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Initial_selection_does_not_echo_a_route_callback_during_prerender(bool requested) {
        var a = CreateConversation();
        using var context = CreateContext(Gateway(a), new StubOperationGateway());
        var callbacks = new List<Guid?>();
        var cut = context.Render<LlmChatConversationWorkspace>(p => p.Add(x => x.InitialConversationId, requested ? a.ConversationId : null)
            .Add(x => x.SelectedConversationIdChanged, value => callbacks.Add(value)));
        cut.WaitForElement("[data-testid='llm-chat-selected-title']");
        Assert.Empty(callbacks);
    }
    [Fact]
    public async Task Late_follower_A_cannot_publish_under_B_or_clear_B_follower() {
        var opA = Guid.NewGuid();
        var opB = Guid.NewGuid();
        var a = CreateConversation(activeOperationId: opA);
        var b = CreateConversation(Guid.NewGuid(), "B", activeOperationId: opB);
        var events = new DelayedEvents();
        var operations = new StubOperationGateway {
            Read = id => CreateOperationView(id, LlmChatOperationStatus.Running) with { ConversationId = id == opA ? a.ConversationId : b.ConversationId }
        };
        using var context = CreateContext(Gateway(a, b), operations, events);
        var cut = context.Render<LlmChatConversationWorkspace>(p => p.Add(x => x.InitialConversationId, a.ConversationId));
        cut.WaitForAssertion(() => Assert.Single(events.Sessions));
        var old = events.Sessions[0];
        cut.Render(p => p.Add(x => x.InitialConversationId, b.ConversationId));
        cut.WaitForAssertion(() => Assert.Equal(2, events.Sessions.Count));
        Assert.True(old.Token.IsCancellationRequested);
        using var registration = old.Token.Register(() => { });
        old.Page.SetResult(new(opA, LlmChatOperationStatus.Running, false, "", [
            new LlmChatOperationAttemptStartedEvent(new(opA), 1, 1, "model-a", LlmStreamingDeliveryMode.Incremental, DateTimeOffset.UtcNow),
            new LlmChatOperationTextDeltaEvent(new(opA), 2, 1, "obsolete A text", DateTimeOffset.UtcNow)
        ], 1, 2));
        cut.WaitForAssertion(() => Assert.True(old.Disposed));
        var current = events.Sessions[1];
        Assert.False(current.Token.IsCancellationRequested);
        Assert.Equal("B", cut.Find("[data-testid='llm-chat-selected-title']").TextContent);
        Assert.DoesNotContain("obsolete A text", cut.Markup, StringComparison.Ordinal);
        var disposal = cut.Instance.DisposeAsync().AsTask();
        Assert.True(current.Token.IsCancellationRequested);
        current.Page.SetCanceled(current.Token);
        await disposal;
        Assert.True(current.Disposed);
        Assert.Equal(0, operations.CancelCalls);
    }

    [Fact]
    public async Task Obsolete_rename_completion_cannot_close_a_new_dialog_or_mutate_B() {
        var a = CreateConversation();
        var b = CreateConversation(Guid.NewGuid(), "B");
        var pending = new TaskCompletionSource<LlmChatUiResult<LlmChatConversationView>>();
        var gateway = Gateway(a, b);
        gateway.Rename = _ => pending.Task;
        using var context = CreateContext(gateway, new StubOperationGateway());
        var cut = context.Render<LlmChatConversationWorkspace>(p => p.Add(x => x.InitialConversationId, a.ConversationId));
        cut.Find($"[data-testid='llm-chat-rename-{a.ConversationId:D}']").Click();
        cut.Find("[data-testid='llm-chat-rename-title']").Change("Captured title");
        var rename = cut.Find("[data-testid='llm-chat-rename-confirm']").ClickAsync(new());
        Assert.Equal("Captured title", gateway.RenamedTitle);
        cut.Render(p => p.Add(x => x.InitialConversationId, b.ConversationId));
        cut.Find($"[data-testid='llm-chat-rename-{b.ConversationId:D}']").Click();
        pending.SetResult(Success(CreateView(a with { Title = "Captured title" }, [])));
        await rename;
        Assert.Single(cut.FindAll("[data-testid='llm-chat-rename-dialog']"));
        Assert.Equal("B", cut.Find("[data-testid='llm-chat-rename-title']").GetAttribute("value"));
        Assert.Equal("B", cut.Find("[data-testid='llm-chat-selected-title']").TextContent);
    }

    [Fact]
    public async Task Authorization_disposal_and_denial_are_fail_closed() {
        var pending = new TaskCompletionSource<LlmChatUiAuthorizationSnapshot>();
        var authorization = new DeferredAuthorization(pending.Task);
        var gateway = Gateway(CreateConversation());
        using var session = new LlmChatConversationWorkspaceController(new StubDefinitionGateway(), gateway, new StubOperationGateway(), authorization, NullLogger<LlmChatConversationWorkspaceController>.Instance, TimeProvider.System);
        var load = session.InitializeAsync(default);
        session.Dispose();
        Assert.True(authorization.Token.IsCancellationRequested);
        using var registration = authorization.Token.Register(() => { });
        pending.SetResult(new(true, true, true));
        await load;
        Assert.Empty(gateway.ListQueries);
        Assert.Null(session.SelectedConversation);
        using var denied = new LlmChatConversationWorkspaceController(new StubDefinitionGateway(), gateway, new StubOperationGateway(), new DeferredAuthorization(Task.FromResult(new LlmChatUiAuthorizationSnapshot(false, false, false))), NullLogger<LlmChatConversationWorkspaceController>.Instance, TimeProvider.System);
        await denied.InitializeAsync(default, Guid.NewGuid());
        Assert.Empty(gateway.ListQueries);
        Assert.Null(denied.SelectedConversation);
    }

    private sealed class DeferredAuthorization(Task<LlmChatUiAuthorizationSnapshot> response) : ILlmChatUiAuthorizationFacade {
        public CancellationToken Token { get; private set; }
        public ValueTask<LlmChatUiAuthorizationSnapshot> GetAsync(CancellationToken cancellationToken = default) {
            Token = cancellationToken;
            return new(response);
        }
        public ValueTask<bool> IsAllowedAsync(LlmChatUiPermission permission, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
    }
    private sealed class DelayedEvents : ILlmChatUiEventSessionGateway {
        public List<DelayedSession> Sessions { get; } = [];
        public ValueTask<LlmChatUiResult<ILlmChatUiEventSession>> OpenAsync(Guid operationId, CancellationToken cancellationToken = default) {
            var session = new DelayedSession();
            Sessions.Add(session);
            return ValueTask.FromResult(LlmChatUiResult<ILlmChatUiEventSession>.Success(session));
        }
    }
    private sealed class DelayedSession : ILlmChatUiEventSession {
        public TaskCompletionSource<LlmChatUiOperationEventPage> Page { get; } = new();
        public CancellationToken ProfileLifetime => CancellationToken.None;
        public int MaximumPageSize => 50;
        public CancellationToken Token { get; private set; }
        public bool Disposed { get; private set; }
        public ValueTask<LlmChatUiOperationEventPage> ReadAsync(long afterSequence, int take, TimeSpan maximumWait, CancellationToken cancellationToken = default) {
            Token = cancellationToken;
            return new(Page.Task);
        }
        public ValueTask DisposeAsync() {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
    [Fact]
    public async Task Explicit_target_can_clear_reopen_and_override_local_selection_without_default_fallback() {
        var a = CreateConversation();
        var b = CreateConversation(Guid.NewGuid(), "B");
        var gateway = Gateway(a, b);
        using var session = Session(gateway);
        await session.InitializeAsync(default, a.ConversationId);
        await session.SetRequestedConversationAsync(null, default);
        Assert.Null(session.SelectedConversation);
        await session.SetRequestedConversationAsync(a.ConversationId, default);
        Assert.Equal(a.ConversationId, session.SelectedConversation!.ConversationId);
        await session.SelectConversationAsync(b.ConversationId, default);
        Assert.Equal(b.ConversationId, session.DesiredConversationId);
        await session.SetRequestedConversationAsync(a.ConversationId, default);
        Assert.Equal(a.ConversationId, session.SelectedConversation!.ConversationId);
        await session.SetRequestedConversationAsync(Guid.Empty, default);
        Assert.Null(session.DesiredConversationId);
        Assert.Null(session.SelectedConversation);
        var missing = Guid.NewGuid();
        await session.SetRequestedConversationAsync(missing, default);
        Assert.Equal(missing, session.DesiredConversationId);
        Assert.Null(session.SelectedConversation);
        Assert.Contains("not found", session.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("private failure", session.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Initial_null_allows_default_but_Empty_is_an_explicit_clear(bool empty) {
        var a = CreateConversation();
        using var session = Session(Gateway(a));
        await session.InitializeAsync(default, empty ? Guid.Empty : null);
        Assert.Equal(empty ? null : a.ConversationId, session.DesiredConversationId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_transcript_success_or_failure_cannot_publish_or_clear_new_busy_state(bool fail) {
        var a = CreateConversation();
        var b = CreateConversation(Guid.NewGuid(), "B");
        var pendingA = new TaskCompletionSource<LlmChatUiResult<LlmChatConversationView>>();
        var pendingB = new TaskCompletionSource<LlmChatUiResult<LlmChatConversationView>>();
        CancellationToken tokenA = default;
        var gateway = Gateway(a, b);
        gateway.Read = (id, _, token) => {
            if (id == a.ConversationId) {
                tokenA = token;
                return pendingA.Task;
            }
            return pendingB.Task;
        };
        using var session = Session(gateway);
        var first = session.InitializeAsync(default, a.ConversationId);
        var second = session.SetRequestedConversationAsync(b.ConversationId, default);
        Assert.True(tokenA.IsCancellationRequested);
        using var registration = tokenA.Register(() => { });
        pendingA.SetResult(fail ? LlmChatUiResult<LlmChatConversationView>.Failure(new LlmChatUiFailure(LlmChatUiFailureCodes.RequestFailed, "private failure"))
            : Success(CreateView(a, [CreateMessage(LlmMessageRole.Assistant, "stale A")])));
        await first;
        Assert.True(session.IsLoading);
        Assert.Empty(session.ErrorMessage);
        pendingB.SetResult(Success(CreateView(b, [CreateMessage(LlmMessageRole.Assistant, "accepted B")])));
        await second;
        Assert.Equal(b.ConversationId, session.SelectedConversation!.ConversationId);
        Assert.Single(session.Messages);
        Assert.False(session.IsLoading);
    }

    [Fact]
    public async Task Route_replacement_during_pending_list_load_selects_only_latest_target() {
        var a = CreateConversation();
        var b = CreateConversation(Guid.NewGuid(), "B");
        var page = new TaskCompletionSource<LlmChatUiResult<LlmChatPage<LlmChatConversationListItem, LlmChatConversationCursor>>>();
        var gateway = Gateway(a, b);
        gateway.List = (_, _) => page.Task;
        using var session = Session(gateway);
        var initial = session.InitializeAsync(default, a.ConversationId);
        await session.SetRequestedConversationAsync(b.ConversationId, default);
        page.SetResult(LlmChatUiResult<LlmChatPage<LlmChatConversationListItem, LlmChatConversationCursor>>.Success(new([a, b], null)));
        await initial;
        Assert.Equal([b.ConversationId], gateway.TranscriptTargets);
        Assert.Equal(b.ConversationId, session.SelectedConversation!.ConversationId);
    }

    [Fact]
    public async Task Transcript_append_is_superseded_by_reload_and_disposal_suppresses_late_read() {
        var a = CreateConversation();
        var oldPage = new TaskCompletionSource<LlmChatUiResult<LlmChatConversationView>>();
        var gateway = Gateway(a);
        var count = 0;
        CancellationToken appendToken = default;
        gateway.Read = (_, _, token) => {
            count++;
            if (count == 2) {
                appendToken = token;
                return oldPage.Task;
            }
            return Task.FromResult(Success(CreateView(a, [CreateMessage(LlmMessageRole.User, $"canonical {count}")], new(10))));
        };
        using var session = Session(gateway);
        await session.InitializeAsync(default, a.ConversationId);
        var append = session.LoadMoreMessagesAsync(default);
        await session.ReloadSelectedAsync(default);
        Assert.True(appendToken.IsCancellationRequested);
        oldPage.SetResult(Success(CreateView(a, [CreateMessage(LlmMessageRole.Assistant, "obsolete page")])));
        await append;
        Assert.Single(session.Messages);
        Assert.Equal("canonical 3", session.Messages[0].Text);
        var pending = new TaskCompletionSource<LlmChatUiResult<LlmChatConversationView>>();
        gateway.Read = (_, _, token) => {
            appendToken = token;
            return pending.Task;
        };
        var reload = session.ReloadSelectedAsync(default);
        session.Dispose();
        Assert.True(appendToken.IsCancellationRequested);
        using var registration = appendToken.Register(() => { });
        pending.SetResult(Success(CreateView(a, [])));
        await reload;
        Assert.Single(session.Messages);
    }

    [Fact]
    public async Task Old_list_append_cannot_replace_profile_reset_page() {
        var a = CreateConversation();
        var b = CreateConversation(Guid.NewGuid(), "B");
        var pending = new TaskCompletionSource<LlmChatUiResult<LlmChatPage<LlmChatConversationListItem, LlmChatConversationCursor>>>();
        var gateway = Gateway(a, b);
        var reads = 0;
        gateway.List = (_, _) => ++reads == 2 ? pending.Task : Task.FromResult(
            LlmChatUiResult<LlmChatPage<LlmChatConversationListItem, LlmChatConversationCursor>>.Success(
                new(reads == 1 ? [a] : [b], reads == 1 ? new(a.UpdatedAtUtc, new(a.ConversationId)) : null)));
        using var session = Session(gateway);
        await session.InitializeAsync(default);
        var append = session.LoadMoreConversationsAsync(default);
        await session.ReloadForProfileChangeAsync(default);
        pending.SetResult(LlmChatUiResult<LlmChatPage<LlmChatConversationListItem, LlmChatConversationCursor>>.Success(new([a], null)));
        await append;
        Assert.Equal(b.ConversationId, Assert.Single(session.Conversations).ConversationId);
        Assert.Equal(b.ConversationId, session.SelectedConversation!.ConversationId);
    }

    [Theory]
    [InlineData(ConversationWorkspaceAction.NewConversation)]
    [InlineData(ConversationWorkspaceAction.Rename)]
    [InlineData(ConversationWorkspaceAction.Archive)]
    public async Task Route_replacement_closes_owned_dialog_and_obsolete_intent_cannot_open_it_again(ConversationWorkspaceAction action) {
        var a = CreateConversation();
        var b = CreateConversation(Guid.NewGuid(), "B");
        using var context = CreateContext(Gateway(a, b), new StubOperationGateway());
        var cut = context.Render<LlmChatConversationWorkspace>(p => p.Add(x => x.InitialConversationId, a.ConversationId));
        var surface = cut.FindComponent<LlmChatConversationSurface>();
        var old = new ConversationWorkspaceIntent(surface.Instance.Presentation.Generation, action, a.ConversationId);
        await cut.InvokeAsync(() => surface.Instance.Intent.InvokeAsync(old));
        var testId = action switch {
            ConversationWorkspaceAction.NewConversation => "llm-chat-start-dialog",
            ConversationWorkspaceAction.Rename => "llm-chat-rename-dialog",
            _ => "llm-chat-archive-dialog"
        };
        Assert.Single(cut.FindAll($"[data-testid='{testId}']"));
        cut.Render(p => p.Add(x => x.InitialConversationId, b.ConversationId));
        Assert.Empty(cut.FindAll($"[data-testid='{testId}']"));
        await cut.InvokeAsync(() => surface.Instance.Intent.InvokeAsync(old));
        Assert.Empty(cut.FindAll($"[data-testid='{testId}']"));
        Assert.Equal("B", cut.Find("[data-testid='llm-chat-selected-title']").TextContent);
    }

    private static StubConversationGateway Gateway(params LlmChatConversationListItem[] items) {
        var gateway = new StubConversationGateway {
            Read = (id, _, _) => Task.FromResult(items.FirstOrDefault(item => item.ConversationId == id) is { } item
                ? Success(CreateView(item, [])) : LlmChatUiResult<LlmChatConversationView>.Failure(new LlmChatUiFailure(LlmChatErrorCodes.ConversationNotFound, "private failure")))
        };
        gateway.ListPages.Enqueue(new(items, null));
        return gateway;
    }
    private static LlmChatUiResult<LlmChatConversationView> Success(LlmChatConversationView view) => LlmChatUiResult<LlmChatConversationView>.Success(view);
    private static LlmChatConversationWorkspaceController Session(StubConversationGateway gateway) => new(
        new StubDefinitionGateway(), gateway, new StubOperationGateway(), new StubAuthorization(), NullLogger<LlmChatConversationWorkspaceController>.Instance, TimeProvider.System);
    [Fact]
    public void Route_target_is_observed_after_initialization_and_same_ID_echo_is_inert() {
        var a = CreateConversation(title: "Conversation A", conversationId: Guid.NewGuid());
        var b = CreateConversation(title: "Conversation B", conversationId: Guid.NewGuid());
        var gateway = new StubConversationGateway {
            Read = (id, _, _) => Task.FromResult(LlmChatUiResult<LlmChatConversationView>.Success(CreateView(id == a.ConversationId ? a : b, [])))
        };
        gateway.ListPages.Enqueue(new([a, b], null));
        using var context = CreateContext(gateway, new StubOperationGateway());
        var selected = new List<Guid?>();
        var cut = context.Render<LlmChatConversationWorkspace>(p => p.Add(x => x.InitialConversationId, a.ConversationId)
            .Add(x => x.SelectedConversationIdChanged, value => selected.Add(value)));
        cut.WaitForElement("[data-testid='llm-chat-selected-title']");
        cut.Render(p => p.Add(x => x.InitialConversationId, b.ConversationId));
        cut.WaitForAssertion(() => Assert.Equal("Conversation B", cut.Find("[data-testid='llm-chat-selected-title']").TextContent), TimeSpan.FromSeconds(2));
        var reads = gateway.TranscriptTargets.Count;
        cut.Render(p => p.Add(x => x.InitialConversationId, b.ConversationId));
        Assert.Equal(reads, gateway.TranscriptTargets.Count);
        Assert.Equal(b.ConversationId, selected[^1]);
    }
}
