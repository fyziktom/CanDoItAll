using System.Collections.Immutable;
using Bunit;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AgentFramework.Llm.SimpleChats.UI;
using CanDoItAll.AgentFramework.UiSandbox;
using Microsoft.Extensions.Logging.Abstractions;
using static CanDoItAll.Tests.Components.LlmChats.LlmChatDefinitionUiTests;
using CatalogResult = CanDoItAll.AgentFramework.Llm.SimpleChats.Components.LlmChatUiResult<CanDoItAll.AgentFramework.Llm.SimpleChats.Common.LlmChatPage<CanDoItAll.AgentFramework.Llm.SimpleChats.Components.LlmChatDefinitionListItem, CanDoItAll.AgentFramework.Llm.SimpleChats.Common.LlmChatDefinitionCursor>>;

namespace CanDoItAll.Tests.Components.LlmChats;

public sealed class DefinitionCatalogSessionTests {
    private static readonly Guid A = DefinitionCatalogSandboxFixture.DefinitionA;
    private static readonly Guid B = DefinitionCatalogSandboxFixture.DefinitionB;

    [Fact]
    public async Task Parameter_targets_replace_clear_and_reopen_once_while_parent_echo_is_inert() {
        var gateway = Gateway();
        using var context = CreateContext(gateway, new StubProviderGateway(), new StubAuthorization(true, true));
        var selected = new List<Guid?>();
        var cut = context.Render<LlmChatDefinitionCatalogPanel>(p => p.Add(x => x.RequestedDefinitionId, A)
            .Add(x => x.SelectedDefinitionIdChanged, selected.Add));
        cut.WaitForAssertion(() => Assert.Equal([A], gateway.EditorTargets));
        cut.Render(p => p.Add(x => x.RequestedDefinitionId, A));
        Assert.Equal([A], gateway.EditorTargets);
        cut.Render(p => p.Add(x => x.RequestedDefinitionId, B));
        cut.WaitForAssertion(() => Assert.Equal([A, B], gateway.EditorTargets));
        Assert.Equal(B, cut.FindComponent<LlmChatDefinitionEditorDialog>().Instance.DefinitionId);
        cut.Render(p => p.Add(x => x.RequestedDefinitionId, (Guid?)null));
        Assert.Empty(cut.FindComponents<LlmChatDefinitionEditorDialog>());
        cut.Render(p => p.Add(x => x.RequestedDefinitionId, A));
        cut.WaitForAssertion(() => Assert.Equal([A, B, A], gateway.EditorTargets));
        Assert.Empty(selected);
        await cut.Find("[data-testid='llm-chat-definition-editor-cancel']").ClickAsync();
        Assert.Equal(new Guid?[] { null }, selected);
    }

    [Fact]
    public async Task Same_null_parameter_preserves_local_create_and_save_closes_reloads_once() {
        var gateway = Gateway();
        using var context = CreateContext(gateway, new StubProviderGateway(), new StubAuthorization(true, true));
        var selected = new List<Guid?>();
        var cut = context.Render<LlmChatDefinitionCatalogPanel>(p => p.Add(x => x.SelectedDefinitionIdChanged, selected.Add));
        Assert.Single(gateway.ListQueries);
        await cut.Find("[data-testid='llm-chat-definition-create']").ClickAsync();
        var child = cut.FindComponent<LlmChatDefinitionEditorDialog>();
        cut.Render(p => p.Add(x => x.RequestedDefinitionId, (Guid?)null));
        Assert.Same(child.Instance, cut.FindComponent<LlmChatDefinitionEditorDialog>().Instance);
        Assert.Empty(gateway.EditorTargets);
        var saved = child.Instance.Saved;
        await cut.InvokeAsync(() => saved.InvokeAsync(CreateEditor().Definition));
        Assert.Empty(cut.FindComponents<LlmChatDefinitionEditorDialog>());
        Assert.Equal(2, gateway.ListQueries.Count);
        await cut.InvokeAsync(() => saved.InvokeAsync(CreateEditor().Definition));
        Assert.Equal(2, gateway.ListQueries.Count);
        Assert.Equal(new Guid?[] { null, null }, selected);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Authorization_and_empty_guid_never_request_unauthorized_editor_data(bool read, bool manage) {
        var gateway = Gateway();
        using var context = CreateContext(gateway, new StubProviderGateway(), new StubAuthorization(read, manage));
        var selected = new List<Guid?>();
        var cut = context.Render<LlmChatDefinitionCatalogPanel>(p => p.Add(x => x.RequestedDefinitionId, manage ? Guid.Empty : A)
            .Add(x => x.SelectedDefinitionIdChanged, selected.Add));
        Assert.Equal(read ? 1 : 0, gateway.ListQueries.Count);
        Assert.Empty(gateway.EditorTargets);
        Assert.Empty(selected);
        Assert.Empty(cut.FindComponents<LlmChatDefinitionEditorDialog>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Latest_route_wins_pending_authorization_and_disposal_retains_token_until_completion(bool dispose) {
        var gateway = Gateway();
        var pending = new TaskCompletionSource<LlmChatUiAuthorizationSnapshot>();
        CancellationToken token = default;
        var authorization = new StubAuthorization(true, true) { Read = value => {
            token = value;
            return new(pending.Task);
        } };
        using var context = CreateContext(gateway, new StubProviderGateway(), authorization);
        var cut = context.Render<LlmChatDefinitionCatalogPanel>(p => p.Add(x => x.RequestedDefinitionId, A));
        cut.Render(p => p.Add(x => x.RequestedDefinitionId, B));
        Assert.Empty(gateway.ListQueries);
        Assert.Empty(gateway.EditorTargets);
        if (dispose) {
            await context.DisposeRenderedComponentsAsync();
            Assert.True(token.IsCancellationRequested);
            Assert.True(token.WaitHandle.WaitOne(0));
        }
        authorization.Read = null;
        await context.Renderer.Dispatcher.InvokeAsync(() => pending.SetResult(new(true, true, false)));
        if (dispose) {
            Assert.Empty(gateway.ListQueries);
            Assert.Empty(gateway.EditorTargets);
        } else {
            cut.WaitForAssertion(() => Assert.Equal([B], gateway.EditorTargets));
            Assert.Single(gateway.ListQueries);
        }
    }

    [Fact]
    public async Task Route_change_during_pending_list_and_old_editor_completion_cannot_restore_old_target() {
        var gateway = Gateway();
        var list = new TaskCompletionSource<CatalogResult>();
        var editor = new TaskCompletionSource<LlmChatUiResult<LlmChatDefinitionEditor>>();
        gateway.ListHandler = (_, _) => list.Task;
        gateway.EditorHandler = id => id == A ? editor.Task : Task.FromResult(LlmChatUiResult<LlmChatDefinitionEditor>.Success(CreateEditor(definitionId: B)));
        using var context = CreateContext(gateway, new StubProviderGateway(), new StubAuthorization(true, true));
        var cut = context.Render<LlmChatDefinitionCatalogPanel>(p => p.Add(x => x.RequestedDefinitionId, A));
        var old = cut.FindComponent<LlmChatDefinitionEditorDialog>().Instance;
        cut.Render(p => p.Add(x => x.RequestedDefinitionId, B));
        cut.WaitForAssertion(() => Assert.Equal([A, B], gateway.EditorTargets));
        await cut.InvokeAsync(() => old.Cancelled.InvokeAsync());
        await cut.InvokeAsync(() => old.Saved.InvokeAsync(CreateEditor(definitionId: A).Definition));
        await cut.InvokeAsync(() => {
            editor.SetResult(LlmChatUiResult<LlmChatDefinitionEditor>.Success(CreateEditor(definitionId: A)));
            list.SetResult(Page());
        });
        Assert.Equal(B, cut.FindComponent<LlmChatDefinitionEditorDialog>().Instance.DefinitionId);
        Assert.Single(gateway.ListQueries);
    }

    [Fact]
    public async Task Save_reload_failure_does_not_reopen_editor_or_repeat_save() {
        var gateway = Gateway();
        using var context = CreateContext(gateway, new StubProviderGateway(), new StubAuthorization(true, true));
        var cut = context.Render<LlmChatDefinitionCatalogPanel>(p => p.Add(x => x.RequestedDefinitionId, A));
        var saved = cut.FindComponent<LlmChatDefinitionEditorDialog>().Instance.Saved;
        gateway.ListHandler = (_, _) => throw new IOException("private prompt/schema");
        await cut.InvokeAsync(() => saved.InvokeAsync(CreateEditor().Definition));
        Assert.Empty(cut.FindComponents<LlmChatDefinitionEditorDialog>());
        Assert.Equal(2, gateway.ListQueries.Count);
        Assert.Contains("Definitions could not be loaded", cut.Markup);
        Assert.DoesNotContain("private prompt/schema", cut.Markup);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Superseded_success_failure_and_finally_cannot_replace_newer_loading_or_results(bool fail) {
        var gateway = Gateway();
        var first = new TaskCompletionSource<CatalogResult>();
        var second = new TaskCompletionSource<CatalogResult>();
        CancellationToken token = default;
        gateway.ListHandler = (_, value) => {
            token = value;
            return first.Task;
        };
        using var session = Session(gateway);
        var old = session.ReloadAsync();
        gateway.ListHandler = (_, _) => second.Task;
        var current = session.ApplyAsync(new DefinitionCatalogIntent.StatusChanged(LlmChatDefinitionStatusFilter.Active));
        Assert.True(token.IsCancellationRequested);
        Assert.True(token.WaitHandle.WaitOne(0));
        if (fail) {
            first.SetException(new IOException("private failure"));
        } else {
            first.SetResult(Page(A));
        }
        await old;
        Assert.True(session.Presentation.IsBusy);
        Assert.Null(session.Presentation.Error);
        second.SetResult(Page(B));
        await current;
        Assert.Equal(B, Assert.Single(session.Presentation.Cards).DefinitionId);
        Assert.False(session.Presentation.IsBusy);
    }

    [Fact]
    public async Task Search_change_immediately_fences_append_and_debounces_only_latest_query() {
        var gateway = Gateway();
        gateway.ListHandler = (_, _) => Task.FromResult(Page(A, more: true));
        using var session = Session(gateway);
        await session.ReloadAsync();
        var append = new TaskCompletionSource<CatalogResult>();
        CancellationToken token = default;
        gateway.ListHandler = (_, value) => {
            token = value;
            return append.Task;
        };
        var oldAppend = session.ApplyAsync(new DefinitionCatalogIntent.LoadMore());
        await session.ApplyAsync(new DefinitionCatalogIntent.LoadMore());
        Assert.Equal(2, gateway.ListQueries.Count);
        var oldSearch = session.ApplyAsync(new DefinitionCatalogIntent.SearchChanged("old"));
        Assert.True(token.IsCancellationRequested);
        append.SetResult(Page(B));
        await oldAppend;
        Assert.Equal(A, Assert.Single(session.Presentation.Cards).DefinitionId);
        gateway.ListHandler = (_, _) => Task.FromResult(Page(B));
        var newSearch = session.ApplyAsync(new DefinitionCatalogIntent.SearchChanged("latest"));
        await Task.WhenAll(oldSearch, newSearch);
        Assert.Equal(3, gateway.ListQueries.Count);
        Assert.Equal("latest", session.AppliedQuery!.SearchText);
        Assert.Null(session.AppliedQuery.Cursor);
        Assert.Equal(B, Assert.Single(session.Presentation.Cards).DefinitionId);
    }

    [Fact]
    public async Task Paging_filters_reset_and_immutable_acceptance_preserve_query_contract() {
        var gateway = Gateway();
        var tags = new List<string> { "source" };
        var items = new List<LlmChatDefinitionListItem> { CreateEditor(definitionId: A, tags: tags).Definition };
        gateway.ListHandler = (_, _) => Task.FromResult(CatalogResult.Success(new(items, Cursor())));
        using var session = Session(gateway);
        await session.ReloadAsync();
        items.Clear();
        tags.Clear();
        Assert.Equal(new[] { "source" }, Assert.Single(session.Presentation.Cards).Tags.ToArray());
        gateway.ListHandler = (_, _) => Task.FromResult(Page(B));
        await session.ApplyAsync(new DefinitionCatalogIntent.LoadMore());
        Assert.Equal(new[] { A, B }, session.Presentation.Cards.Select(card => card.DefinitionId));
        Assert.NotNull(session.AppliedQuery!.Cursor);
        await session.ApplyAsync(new DefinitionCatalogIntent.TagsChanged(["Research", "research"]));
        Assert.Equal(["research"], session.AppliedQuery!.Tags);
        await session.ApplyAsync(new DefinitionCatalogIntent.StatusChanged(LlmChatDefinitionStatusFilter.Active));
        Assert.Equal(CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions.LlmChatDefinitionStatus.Active, session.AppliedQuery!.Status);
        await session.ApplyAsync(new DefinitionCatalogIntent.ResetFilters());
        Assert.Empty(session.AppliedQuery!.Tags);
        Assert.Null(session.AppliedQuery.Status);
        Assert.Equal("", session.AppliedQuery.SearchText);
        Assert.Null(session.AppliedQuery.Cursor);
        Assert.Equal(24, session.AppliedQuery.Take);
        Assert.Equal(DefinitionCatalogSandboxFixture.Limits, LlmChatDefinitionCatalogSession.Limits);
    }

    [Fact]
    public async Task Dispose_cancels_debounce_and_read_without_early_token_disposal_or_late_publication() {
        var gateway = Gateway();
        var pending = new TaskCompletionSource<CatalogResult>();
        CancellationToken token = default;
        gateway.ListHandler = (_, value) => {
            token = value;
            return pending.Task;
        };
        using var session = Session(gateway);
        var read = session.ReloadAsync();
        var search = session.ApplyAsync(new DefinitionCatalogIntent.SearchChanged("pending"));
        var changes = 0;
        session.Changed += () => changes++;
        session.Dispose();
        Assert.True(token.IsCancellationRequested);
        using (token.Register(() => { })) {
            Assert.True(token.WaitHandle.WaitOne(0));
        }
        pending.SetResult(Page());
        await Task.WhenAll(read, search);
        Assert.Equal(0, changes);
        Assert.Single(gateway.ListQueries);
        Assert.Throws<ObjectDisposedException>(() => token.WaitHandle);
    }

    private static StubDefinitionGateway Gateway() => new(CreateEditor(definitionId: A)) {
        EditorHandler = id => Task.FromResult(LlmChatUiResult<LlmChatDefinitionEditor>.Success(CreateEditor(definitionId: id)))
    };
    private static LlmChatDefinitionCatalogSession Session(StubDefinitionGateway gateway) {
        var session = new LlmChatDefinitionCatalogSession(gateway, item => new(item.DefinitionId, item.Name, item.Summary,
            item.AvatarImageUrl, LlmChatDefinitionStatusFilter.Draft, item.Revision, item.UpdatedAtUtc, item.Tags.ToImmutableArray()),
            NullLogger<LlmChatDefinitionCatalogSession>.Instance);
        session.AcceptAuthorization(new(true, true, false));
        return session;
    }
    private static LlmChatDefinitionCursor Cursor() => new(new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero), new(A));
    private static CatalogResult Page(Guid? id = null, bool more = false) => CatalogResult.Success(
        new([CreateEditor(definitionId: id ?? A).Definition], more ? Cursor() : null));
}
