using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;

namespace CanDoItAll.Tests.Unit.Prompts;

public sealed class PromptGalleryQuietRefreshTests {
    [Fact]
    public async Task Burst_has_one_active_refresh_and_at_most_one_trailing_refresh() {
        var delay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var active = 0;
        var maximum = 0;
        await using var refresh = new PromptGalleryQuietRefresh(async () => {
            maximum = Math.Max(maximum, ++active);
            if (++calls == 1) {
                entered.TrySetResult();
                await release.Task;
            }
            active--;
        }, token => delay.Task.WaitAsync(token));
        var running = refresh.InvalidateAsync();
        for (var index = 0; index < 1000; index++) {
            Assert.Same(running, refresh.InvalidateAsync());
        }
        delay.SetResult();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        for (var index = 0; index < 1000; index++) {
            Assert.Same(running, refresh.InvalidateAsync());
        }
        release.SetResult();
        await running;
        Assert.Equal(2, calls);
        Assert.Equal(1, maximum);
    }

    [Fact]
    public async Task Retirement_cancels_the_pending_delay_without_a_refresh() {
        var delay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var refresh = new PromptGalleryQuietRefresh(() => {
            calls++;
            return Task.CompletedTask;
        }, token => delay.Task.WaitAsync(token));
        var running = refresh.InvalidateAsync();
        await refresh.DisposeAsync();
        await running;
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Quiet_search_retains_rows_filters_and_page_while_reading_then_clamps_the_page() {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new TaskCompletionSource<PromptGalleryPage<PromptGallerySearchItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gallery = new ScriptedPromptGalleryService();
        var old = ScriptedPromptGalleryService.Item(title: "Existing row");
        gallery.Search = (query, _) => Task.FromResult(ScriptedPromptGalleryService.Page(query, [old], totalCount: 40));
        await using var session = new PromptGallerySearchSession(gallery);
        await session.ConfigureAsync(new(null, null, null, false, false, 10));
        await session.ApplyAsync(new CanDoItAll.Prompts.UI.Gallery.PromptGallerySearchIntent.LoadPage(3));
        gallery.Search = (query, _) => {
            if (query.PageIndex == 3) {
                entered.TrySetResult();
                return result.Task;
            }
            return Task.FromResult(ScriptedPromptGalleryService.Page(query, [old], totalCount: 1));
        };
        var refreshing = session.InvalidateAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(session.Presentation.IsQuietRefresh);
        Assert.Same(old, Assert.Single(session.Presentation.Items));
        Assert.Equal(3, session.Presentation.PageIndex);
        result.SetResult(new([], 3, 10, 1));
        await refreshing;
        Assert.Equal(0, session.Presentation.PageIndex);
        Assert.Same(old, Assert.Single(session.Presentation.Items));
        Assert.Equal(1, session.Presentation.TotalCount);
    }
}
