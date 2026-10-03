using Bunit;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.UI.History;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class ProviderHistoryWorkspaceTests {
    [Theory]
    [InlineData("invalid")]
    [InlineData("0")]
    [InlineData("201")]
    public void Collapsing_advanced_filters_preserves_raw_validation(string value) {
        var backend = new ProviderHistoryUiFixture();
        using var context = backend.CreateContext();
        var cut = Render(context, backend);
        cut.Find("[data-testid='history-more-filters']").Click();
        cut.Find("[data-testid='history-page-size']").Change(value);
        cut.Find("[data-testid='history-more-filters']").Click();
        cut.Find("[data-testid='history-search-form']").Submit();
        Assert.Empty(backend.Queries);
        Assert.NotEmpty(cut.FindAll(".validation-message"));
        cut.Find("[data-testid='history-more-filters']").Click();
        cut.Find("[data-testid='history-page-size']").Change("2");
        cut.Find("[data-testid='history-search-form']").Submit();
        Assert.Equal(2, Assert.Single(backend.Queries).PageSize);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Old_cancel_clear_and_finally_cannot_affect_a_replacement_search(bool lateFailure) {
        var first = new TaskCompletionSource<HistoryPage>();
        var second = new TaskCompletionSource<HistoryPage>();
        var tokens = new List<CancellationToken>();
        var backend = new ProviderHistoryUiFixture {
            Search = (_, token) => {
                tokens.Add(token);
                return tokens.Count == 1 ? first.Task : second.Task;
            }
        };
        using var context = backend.CreateContext();
        var cut = Render(context, backend);
        var oldSearch = cut.Find("[data-testid='history-search-form']").SubmitAsync();
        cut.WaitForElement("[data-testid='history-cancel']");
        var oldCancel = Click(cut, "history-cancel");
        var oldClear = Click(cut, "history-clear");
        var newSearch = cut.Find("[data-testid='history-search-form']").SubmitAsync();
        Assert.Equal(2, tokens.Count);
        await cut.InvokeAsync(() => oldCancel.InvokeAsync());
        await cut.InvokeAsync(() => oldClear.InvokeAsync());
        Assert.True(tokens[0].IsCancellationRequested);
        Assert.False(tokens[1].IsCancellationRequested);
        if (lateFailure) {
            first.SetException(new ProviderHistoryException(HistoryFailure.Denied, "old synthetic failure"));
        } else {
            first.SetResult(Page(backend));
        }
        await oldSearch;
        Assert.Equal(HistorySearchPhase.Loading, cut.FindComponent<ProviderHistoryResultsSurface>().Instance.Presentation.Phase);
        second.SetResult(Page(backend));
        await newSearch;
        cut.WaitForElement("[data-testid='history-details']");
        Assert.Empty(cut.FindAll("[data-testid='history-error']"));
    }

    [Fact]
    public async Task Old_page_actions_and_same_entry_details_are_rejected_after_new_search() {
        var backend = new ProviderHistoryUiFixture();
        backend.Search = (_, _) => Task.FromResult(Page(backend, "opaque-next"));
        using var context = backend.CreateContext();
        var cut = Render(context, backend);
        cut.Find("[data-testid='history-search-form']").Submit();
        var oldNext = Click(cut, "history-next");
        var oldDetails = Click(cut, "history-details");
        var oldClear = Click(cut, "history-clear");
        cut.Find("[data-testid='history-next']").Click();
        var oldPrevious = Click(cut, "history-previous");
        cut.Find("[data-testid='history-search-form']").Submit();
        foreach (var callback in new[] { oldNext, oldDetails, oldClear, oldPrevious }) {
            await cut.InvokeAsync(() => callback.InvokeAsync());
        }
        Assert.Equal(3, backend.Queries.Count);
        Assert.Equal(0, backend.MetadataReads);
        Assert.Equal(1, cut.FindComponent<ProviderHistoryResultsSurface>().Instance.Presentation.PageNumber);
        cut.Find("[data-testid='history-details']").Click();
        Assert.Equal(1, backend.MetadataReads);
    }

    [Fact]
    public async Task Closed_detail_callbacks_cannot_reopen_or_close_a_successor_for_the_same_entry() {
        var backend = new ProviderHistoryUiFixture();
        using var context = backend.CreateContext();
        var cut = Render(context, backend);
        cut.Find("[data-testid='history-search-form']").Submit();
        var oldRow = Click(cut, "history-details");
        cut.Find("[data-testid='history-details']").Click();
        var oldClose = Click(cut, "history-detail-close");
        cut.Find("[data-testid='history-detail-close']").Click();
        await cut.InvokeAsync(() => oldRow.InvokeAsync());
        Assert.Empty(cut.FindAll("[data-testid='history-detail-dialog']"));
        cut.Find("[data-testid='history-details']").Click();
        await cut.InvokeAsync(() => oldClose.InvokeAsync());
        cut.Find("[data-testid='history-detail-dialog']");
        Assert.Equal(2, backend.MetadataReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Old_content_action_cannot_read_from_a_replaced_metadata_activation(bool sameEntry) {
        var first = new ProviderHistoryUiFixture();
        var second = new ProviderHistoryUiFixture();
        if (sameEntry) {
            second.Entry = second.Entry with { Id = first.Entry.Id };
        }
        using var context = first.CreateContext();
        var cut = context.Render<ProviderHistoryDetailsDialog>(p => p.Add(x => x.History, first).Add(x => x.EntryId, first.Entry.Id));
        var oldLoad = Click(cut, "history-load-content");
        var oldClose = Click(cut, "history-detail-close");
        cut.Render(p => p.Add(x => x.History, second).Add(x => x.EntryId, second.Entry.Id));
        await cut.InvokeAsync(() => oldLoad.InvokeAsync());
        await cut.InvokeAsync(() => oldClose.InvokeAsync());
        Assert.Empty(first.ContentReads);
        Assert.Empty(second.ContentReads);
        cut.Find("[data-testid='history-load-content']").Click();
        Assert.Single(second.ContentReads);
    }

    [Fact]
    public async Task Old_content_close_cannot_clear_a_new_content_dialog() {
        var backend = new ProviderHistoryUiFixture();
        using var context = backend.CreateContext();
        var cut = context.Render<ProviderHistoryDetailsDialog>(p => p.Add(x => x.History, backend).Add(x => x.EntryId, backend.Entry.Id));
        cut.Find("[data-testid='history-load-content']").Click();
        var oldClose = Click(cut, "history-content-close");
        var retiredContent = cut.FindComponent<ProviderHistoryContentDialog>().Instance;
        cut.Find("[data-testid='history-content-close']").Click();
        Assert.Null(retiredContent.Content);
        Assert.False(retiredContent.OnClose.HasDelegate);
        cut.Find("[data-testid='history-load-content']").Click();
        await cut.InvokeAsync(() => oldClose.InvokeAsync());
        cut.Find("[data-testid='history-content-dialog']");
        Assert.Equal(2, backend.ContentReads.Count);
    }

    [Fact]
    public async Task Scope_replacement_and_disposal_remove_owned_data_and_callbacks() {
        var backend = new ProviderHistoryUiFixture();
        using var context = backend.CreateContext();
        var cut = Render(context, backend);
        cut.Find("[data-testid='history-search-form']").Submit();
        cut.Find("[data-testid='history-details']").Click();
        cut.Find("[data-testid='history-load-content']").Click();
        var metadata = cut.FindComponent<ProviderHistoryMetadataView>().Instance;
        var content = cut.FindComponent<ProviderHistoryContentDialog>().Instance;
        var details = cut.FindComponent<ProviderHistoryDetailsDialog>().Instance;
        cut.Render(p => p.Add(x => x.Scope, new HistoryProviderScope.SingleProvider(new(Guid.NewGuid()))));
        Assert.Null(metadata.Entry);
        Assert.Null(content.Content);
        Assert.Null(details.History);
        Assert.False(content.OnClose.HasDelegate);
        Assert.False(details.OnClose.HasDelegate);
        Assert.Empty(cut.FindAll("[data-testid='history-content-text']"));
        Assert.Single(backend.Queries);
        var results = cut.FindComponent<ProviderHistoryResultsSurface>().Instance;
        var workspace = cut.Instance;
        await context.DisposeRenderedComponentsAsync();
        Assert.Null(workspace.History);
        Assert.Empty(results.Presentation.Entries);
        Assert.False(results.OnIntent.HasDelegate);
    }

    [Fact]
    public void Independent_workspaces_never_share_filter_result_or_detail_state() {
        var first = new ProviderHistoryUiFixture();
        var second = new ProviderHistoryUiFixture();
        using var context = first.CreateContext();
        var one = Render(context, first);
        var two = Render(context, second);
        Assert.Empty(first.Queries);
        Assert.Empty(second.Queries);
        one.Find("[data-testid='history-model']").Change("one-exact-model");
        one.Find("[data-testid='history-search-form']").Submit();
        one.Find("[data-testid='history-details']").Click();
        one.Find("[data-testid='history-load-content']").Click();
        Assert.Empty(second.Queries);
        Assert.Equal(0, second.MetadataReads);
        Assert.Empty(second.ContentReads);
        Assert.DoesNotContain("one-exact-model", two.Markup);
        two.Find("[data-testid='history-search-form']").Submit();
        one.Dispose();
        two.Find("[data-testid='history-details']").Click();
        Assert.Equal(1, second.MetadataReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Mismatched_metadata_or_content_identity_fails_closed(bool content) {
        var backend = new ProviderHistoryUiFixture();
        if (content) {
            backend.Content = _ => Task.FromResult(new HistoryDetail(HistoryEntryId.New(), HistoryDetailState.Captured));
        } else {
            backend.Metadata = (_, _) => Task.FromResult<HistoryMetadata?>(new(backend.Entry with { Id = HistoryEntryId.New() }, []));
        }
        using var context = backend.CreateContext();
        var cut = context.Render<ProviderHistoryDetailsDialog>(p => p.Add(x => x.History, backend).Add(x => x.EntryId, backend.Entry.Id));
        if (content) {
            cut.Find("[data-testid='history-load-content']").Click();
        }
        Assert.Contains(HistoryPublicErrors.Message(HistoryFailure.StaleContext), cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='history-content-dialog']"));
        Assert.Empty(cut.FindComponents<ProviderHistoryMetadataView>());
    }

    private static IRenderedComponent<ProviderHistoryWorkspace> Render(BunitContext context, ProviderHistoryUiFixture backend) =>
        context.Render<ProviderHistoryWorkspace>(p => p.Add(x => x.History, backend).Add(x => x.Scope, new HistoryProviderScope.AllAuthorized()));

    private static EventCallback Click<T>(IRenderedComponent<T> cut, string testId) where T : class, IComponent =>
        cut.FindComponents<Button>().Single(button => button.FindAll($"[data-testid='{testId}']").Count != 0).Instance.Click;

    private static HistoryPage Page(ProviderHistoryUiFixture backend, string? next = null) =>
        new([backend.Entry], next, new(HistoryCoverageState.Current, null), ProviderHistoryUiFixture.Now);
}
