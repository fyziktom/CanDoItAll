using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.Prompts.UI.Gallery;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Prompts;

public sealed class PromptGallerySearchSessionTests
{
    private static readonly PromptGallerySearchContext ChatContext = new(
        PromptGalleryConsumer.Chat,
        "OpenAI",
        "gpt-5.4-mini",
        ShowActualChatModelFilter: true,
        Compact: true,
        PageSize: 15);

    [Fact]
    public async Task Stale_success_does_not_replace_the_current_page_or_clear_busy()
    {
        var gallery = new ScriptedPromptGalleryService();
        var pending = new Queue<TaskCompletionSource<PromptGalleryPage<PromptGallerySearchItem>>>();
        gallery.Search = (_, _) =>
        {
            var source = new TaskCompletionSource<PromptGalleryPage<PromptGallerySearchItem>>();
            pending.Enqueue(source);
            return source.Task;
        };
        var notices = new List<PromptGalleryNotice>();
        await using var session = new PromptGallerySearchSession(gallery)
        {
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };

        var first = session.ConfigureAsync(ChatContext);
        var second = session.ApplyAsync(new PromptGallerySearchIntent.ChangeFilters(
            session.Presentation.Filters with { Kind = PromptGalleryItemKind.Part },
            Debounce: false));
        Assert.Equal(2, pending.Count);
        var staleOperation = pending.Dequeue();
        var currentOperation = pending.Dequeue();

        // The backend ignores cancellation and completes the superseded request first.
        var staleItem = ScriptedPromptGalleryService.Item(title: "Stale");
        staleOperation.SetResult(ScriptedPromptGalleryService.Page(gallery.Queries[0], [staleItem]));
        await first;

        Assert.True(session.Presentation.IsLoading);
        Assert.Empty(session.Presentation.Items);

        var currentItem = ScriptedPromptGalleryService.Item(title: "Current");
        currentOperation.SetResult(ScriptedPromptGalleryService.Page(gallery.Queries[1], [currentItem]));
        await second;

        Assert.False(session.Presentation.IsLoading);
        Assert.Equal("Current", Assert.Single(session.Presentation.Items).Title);
        Assert.Empty(notices);
        Assert.True(gallery.SearchTokens[0].IsCancellationRequested);
    }

    [Fact]
    public async Task Stale_failure_does_not_publish_an_error_or_clear_busy()
    {
        var gallery = new ScriptedPromptGalleryService();
        var pending = new Queue<TaskCompletionSource<PromptGalleryPage<PromptGallerySearchItem>>>();
        gallery.Search = (_, _) =>
        {
            var source = new TaskCompletionSource<PromptGalleryPage<PromptGallerySearchItem>>();
            pending.Enqueue(source);
            return source.Task;
        };
        var notices = new List<PromptGalleryNotice>();
        await using var session = new PromptGallerySearchSession(gallery)
        {
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };

        var first = session.ConfigureAsync(ChatContext);
        var second = session.ApplyAsync(new PromptGallerySearchIntent.LoadPage(1));
        var staleOperation = pending.Dequeue();
        var currentOperation = pending.Dequeue();

        staleOperation.SetException(new IOException("Late search failure."));
        await first;

        Assert.True(session.Presentation.IsLoading);
        Assert.Null(session.Presentation.LoadError);
        Assert.Empty(notices);

        currentOperation.SetResult(ScriptedPromptGalleryService.Page(gallery.Queries[1], [ScriptedPromptGalleryService.Item()]));
        await second;

        Assert.False(session.Presentation.IsLoading);
        Assert.Null(session.Presentation.LoadError);
        Assert.Single(session.Presentation.Items);
    }

    [Fact]
    public async Task Pinned_context_change_during_load_issues_a_new_query_and_drops_the_old_result()
    {
        var gallery = new ScriptedPromptGalleryService();
        var pending = new Queue<TaskCompletionSource<PromptGalleryPage<PromptGallerySearchItem>>>();
        gallery.Search = (_, _) =>
        {
            var source = new TaskCompletionSource<PromptGalleryPage<PromptGallerySearchItem>>();
            pending.Enqueue(source);
            return source.Task;
        };
        await using var session = new PromptGallerySearchSession(gallery);

        var first = session.ConfigureAsync(ChatContext);
        var second = session.ConfigureAsync(ChatContext with { Model = "gpt-5.4" });

        Assert.Equal(2, gallery.Queries.Count);
        Assert.Equal("gpt-5.4-mini", gallery.Queries[0].Model);
        Assert.Equal("gpt-5.4", gallery.Queries[1].Model);

        pending.Dequeue().SetResult(ScriptedPromptGalleryService.Page(gallery.Queries[0], [ScriptedPromptGalleryService.Item(title: "Old model")]));
        await first;
        Assert.Empty(session.Presentation.Items);
        Assert.True(session.Presentation.IsLoading);

        pending.Dequeue().SetResult(ScriptedPromptGalleryService.Page(gallery.Queries[1], [ScriptedPromptGalleryService.Item(title: "New model")]));
        await second;
        Assert.Equal("New model", Assert.Single(session.Presentation.Items).Title);
        Assert.Equal("gpt-5.4", session.Presentation.ContextModel);
    }

    [Fact]
    public async Task Unchanged_context_echo_and_display_only_changes_do_not_reload()
    {
        var gallery = new ScriptedPromptGalleryService();
        await using var session = new PromptGallerySearchSession(gallery);

        await session.ConfigureAsync(ChatContext);
        await session.ConfigureAsync(ChatContext);
        await session.ConfigureAsync(ChatContext with { Compact = false, PageSize = 25 });

        Assert.Single(gallery.Queries);
        Assert.False(session.Presentation.IsLoading);
    }

    [Fact]
    public async Task Typed_text_is_debounced_and_only_the_latest_value_is_queried()
    {
        var gallery = new ScriptedPromptGalleryService();
        var delays = new List<(TaskCompletionSource Source, CancellationToken Token)>();
        await using var session = new PromptGallerySearchSession(gallery, token =>
        {
            var source = new TaskCompletionSource();
            token.Register(() => source.TrySetCanceled(token));
            delays.Add((source, token));
            return source.Task;
        });
        await session.ConfigureAsync(ChatContext);

        var typedA = session.ApplyAsync(new PromptGallerySearchIntent.ChangeFilters(
            session.Presentation.Filters with { Text = "a" }, Debounce: true));
        var typedAb = session.ApplyAsync(new PromptGallerySearchIntent.ChangeFilters(
            session.Presentation.Filters with { Text = "ab" }, Debounce: true));

        Assert.Equal("ab", session.Presentation.Filters.Text);
        Assert.Equal(2, delays.Count);
        Assert.True(delays[0].Token.IsCancellationRequested);
        await typedA;
        Assert.Single(gallery.Queries);

        delays[1].Source.SetResult();
        await typedAb;

        Assert.Equal(2, gallery.Queries.Count);
        Assert.Equal("ab", gallery.Queries[1].Text);
        Assert.Equal(0, gallery.Queries[1].PageIndex);
    }

    [Fact]
    public async Task Favorite_write_success_with_failed_reload_reports_a_refresh_problem_not_a_write_failure()
    {
        var gallery = new ScriptedPromptGalleryService();
        var item = ScriptedPromptGalleryService.Item();
        gallery.Search = (query, _) => gallery.Queries.Count == 1
            ? Task.FromResult(ScriptedPromptGalleryService.Page(query, [item]))
            : Task.FromException<PromptGalleryPage<PromptGallerySearchItem>>(new IOException("Reload failed."));
        var notices = new List<PromptGalleryNotice>();
        await using var session = new PromptGallerySearchSession(gallery)
        {
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };
        await session.ConfigureAsync(ChatContext);

        await session.ApplyAsync(new PromptGallerySearchIntent.ToggleFavorite(item));

        Assert.Equal((item.Id, true), Assert.Single(gallery.FavoriteWrites));
        var notice = Assert.Single(notices);
        Assert.Equal(PromptGalleryNoticeSeverity.Error, notice.Severity);
        Assert.Equal("Favorite saved, but the gallery could not be refreshed", notice.Summary);
        Assert.NotNull(session.Presentation.LoadError);
        Assert.Null(session.Presentation.FavoriteBusyItemId);
    }

    [Fact]
    public async Task Favorite_write_failure_keeps_the_page_and_reports_only_the_write()
    {
        var gallery = new ScriptedPromptGalleryService();
        gallery.SetFavorite = (_, _) => Task.FromResult(Result.Failure(Error.Failure("Item changed.", "prompts.gallery.concurrency-conflict")));
        var notices = new List<PromptGalleryNotice>();
        await using var session = new PromptGallerySearchSession(gallery)
        {
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };
        await session.ConfigureAsync(ChatContext);
        var item = Assert.Single(session.Presentation.Items);

        await session.ApplyAsync(new PromptGallerySearchIntent.ToggleFavorite(item));

        var notice = Assert.Single(notices);
        Assert.Equal(PromptGalleryNoticeSeverity.Warning, notice.Severity);
        Assert.Equal("Favorite was not updated", notice.Summary);
        Assert.Contains("Item changed.", notice.Detail, StringComparison.Ordinal);
        Assert.Single(gallery.Queries);
        Assert.Single(session.Presentation.Items);
        Assert.Null(session.Presentation.LoadError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disposal_cancels_the_request_and_ignores_its_late_completion(bool fail)
    {
        var gallery = new ScriptedPromptGalleryService();
        var pending = new TaskCompletionSource<PromptGalleryPage<PromptGallerySearchItem>>();
        gallery.Search = (_, _) => pending.Task;
        var changes = 0;
        var notices = new List<PromptGalleryNotice>();
        var session = new PromptGallerySearchSession(gallery)
        {
            Changed = () => { changes++; return Task.CompletedTask; },
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };
        var load = session.ConfigureAsync(ChatContext);
        var changesBeforeDispose = changes;

        await session.DisposeAsync();
        Assert.True(gallery.SearchTokens[0].IsCancellationRequested);
        if (fail)
        {
            pending.SetException(new IOException("Late failure."));
        }
        else
        {
            pending.SetResult(ScriptedPromptGalleryService.Page(gallery.Queries[0], [ScriptedPromptGalleryService.Item()]));
        }

        await load;

        Assert.Equal(changesBeforeDispose, changes);
        Assert.Empty(notices);
        Assert.Empty(session.Presentation.Items);
    }

    [Fact]
    public async Task Pinned_provider_and_model_apply_only_while_the_actual_chat_model_filter_is_on()
    {
        var gallery = new ScriptedPromptGalleryService();
        await using var session = new PromptGallerySearchSession(gallery);

        await session.ConfigureAsync(ChatContext);
        Assert.Equal("OpenAI", gallery.Queries[0].Provider);
        Assert.Equal("gpt-5.4-mini", gallery.Queries[0].Model);
        Assert.Equal(PromptGalleryConsumer.Chat, gallery.Queries[0].Consumer);

        await session.ApplyAsync(new PromptGallerySearchIntent.ClearFilters());
        Assert.False(session.Presentation.Filters.ActualChatModelOnly);
        Assert.Null(gallery.Queries[1].Provider);
        Assert.Null(gallery.Queries[1].Model);

        await session.ApplyAsync(new PromptGallerySearchIntent.ChangeFilters(
            session.Presentation.Filters with { ActualChatModelOnly = true }, Debounce: false));
        Assert.Equal("OpenAI", gallery.Queries[2].Provider);
        Assert.Equal("gpt-5.4-mini", gallery.Queries[2].Model);

        // A new pinned context re-enables the pinned filter even after it was cleared.
        await session.ApplyAsync(new PromptGallerySearchIntent.ClearFilters());
        await session.ConfigureAsync(ChatContext with { Provider = "Ollama" });
        Assert.True(session.Presentation.Filters.ActualChatModelOnly);
        Assert.Equal("Ollama", gallery.Queries[^1].Provider);
    }

    [Fact]
    public async Task Desktop_list_uses_local_provider_and_model_filters_when_no_context_is_pinned()
    {
        var gallery = new ScriptedPromptGalleryService();
        await using var session = new PromptGallerySearchSession(gallery);
        await session.ConfigureAsync(new PromptGallerySearchContext(null, null, null, false, Compact: false, PageSize: 25));

        await session.ApplyAsync(new PromptGallerySearchIntent.ChangeFilters(
            session.Presentation.Filters with { ProviderFilter = "Ollama", ModelFilter = "llama" }, Debounce: false));

        Assert.Equal("Ollama", gallery.Queries[^1].Provider);
        Assert.Equal("llama", gallery.Queries[^1].Model);
        Assert.Null(gallery.Queries[^1].Consumer);
    }

    [Fact]
    public async Task Refresh_reloads_the_current_page_and_clamps_when_the_page_disappears()
    {
        var gallery = new ScriptedPromptGalleryService();
        var totalCount = 45;
        gallery.Search = (query, _) => Task.FromResult(ScriptedPromptGalleryService.Page(
            query,
            query.PageIndex * query.PageSize < totalCount ? [ScriptedPromptGalleryService.Item()] : [],
            totalCount));
        await using var session = new PromptGallerySearchSession(gallery);
        await session.ConfigureAsync(ChatContext);
        await session.ApplyAsync(new PromptGallerySearchIntent.LoadPage(2));
        Assert.Equal(2, session.Presentation.PageIndex);

        totalCount = 20;
        await session.RefreshAsync();

        Assert.Equal(1, session.Presentation.PageIndex);
        Assert.Equal(2, gallery.Queries[^2].PageIndex);
        Assert.Equal(1, gallery.Queries[^1].PageIndex);
        Assert.Equal(2, session.Presentation.TotalPages);
    }

    [Fact]
    public async Task Search_failure_publishes_an_inline_error_and_retry_reloads()
    {
        var gallery = new ScriptedPromptGalleryService();
        var fail = true;
        gallery.Search = (query, _) => fail
            ? Task.FromException<PromptGalleryPage<PromptGallerySearchItem>>(new IOException("Driver offline."))
            : Task.FromResult(ScriptedPromptGalleryService.Page(query, [ScriptedPromptGalleryService.Item()]));
        var notices = new List<PromptGalleryNotice>();
        await using var session = new PromptGallerySearchSession(gallery)
        {
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };

        await session.ConfigureAsync(ChatContext);
        Assert.NotNull(session.Presentation.LoadError);
        Assert.False(session.Presentation.IsLoading);
        Assert.Equal("Prompt gallery search failed", Assert.Single(notices).Summary);

        fail = false;
        await session.ApplyAsync(new PromptGallerySearchIntent.Retry());
        Assert.Null(session.Presentation.LoadError);
        Assert.Single(session.Presentation.Items);
    }
}
