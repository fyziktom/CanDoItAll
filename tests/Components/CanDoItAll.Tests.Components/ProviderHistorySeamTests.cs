using Bunit;
using Microsoft.AspNetCore.Components;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.UI.History;
using CanDoItAll.AgentFramework.UiSandbox;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components.History;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class ProviderHistorySeamTests {
    [Theory]
    [InlineData("not-requested")]
    [InlineData("loading")]
    [InlineData("canceled")]
    [InlineData("failure")]
    [InlineData("empty")]
    [InlineData("partial")]
    [InlineData("paged")]
    [InlineData("metadata")]
    public void Service_free_results_preserve_search_states_and_coverage(string scenario) {
        using var context = new BunitContext();
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        Assert.Null(context.Services.GetService<IProviderRequestHistory>());
        var cut = context.Render<ProviderHistoryResultsSurface>(p => p.Add(x => x.Presentation, HistorySandboxFixture.Create(scenario)));
        if (scenario == "partial") {
            Assert.Contains("Missing records do not mean there were no requests", cut.Markup);
            Assert.Contains("Indexed through", cut.Markup);
        }
        if (scenario == "paged") {
            Assert.Contains("paging uses the applied filters", cut.Markup);
            Assert.False(cut.Find("[data-testid='history-next']").HasAttribute("disabled"));
            Assert.False(cut.Find("[data-testid='history-previous']").HasAttribute("disabled"));
        }
        Assert.Empty(cut.FindAll("[data-testid='history-content-text']"));
    }

    [Fact]
    public async Task Details_intent_is_exactly_once_without_prefetch_or_controlled_state_mutation() {
        using var context = new BunitContext();
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var presentation = HistorySandboxFixture.Create("metadata");
        var intents = new List<HistoryResultsIntent>();
        var cut = context.Render<ProviderHistoryResultsSurface>(p => p.Add(x => x.Presentation, presentation).Add(x => x.OnIntent, intents.Add));
        await cut.Find("[data-testid='history-details']").ClickAsync();
        Assert.Equal(new HistoryResultsIntent.Details(HistorySandboxFixture.Entry.Id), Assert.Single(intents));
        Assert.Same(presentation, cut.Instance.Presentation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Removal_cancels_metadata_or_content_without_disposing_token_early(bool content) {
        var backend = new ProviderHistoryUiFixture();
        var metadata = new TaskCompletionSource<HistoryMetadata?>();
        var detail = new TaskCompletionSource<HistoryDetail>();
        CancellationToken token = default;
        if (content) {
            backend.Content = value => {
                token = value;
                return detail.Task;
            };
        } else {
            backend.Metadata = (_, value) => {
                token = value;
                return metadata.Task;
            };
        }
        using var context = backend.CreateContext();
        var cut = context.Render<DynamicComponent>(p => p.Add(x => x.Type, typeof(ProviderHistoryDetailsDialog))
            .Add(x => x.Parameters, new Dictionary<string, object> { [nameof(ProviderHistoryDetailsDialog.EntryId)] = backend.Entry.Id }));
        var operation = content ? cut.Find("[data-testid='history-load-content']").ClickAsync() : Task.CompletedTask;
        cut.Render(p => p.Add(x => x.Type, typeof(ProviderHistoryResultsSurface))
            .Add(x => x.Parameters, new Dictionary<string, object> { [nameof(ProviderHistoryResultsSurface.Presentation)] = HistoryResultsPresentation.Initial }));
        Assert.True(token.IsCancellationRequested);
        using (token.Register(() => { })) {
            Assert.True(token.WaitHandle.WaitOne(0));
        }
        await context.Renderer.Dispatcher.InvokeAsync(() => {
            metadata.TrySetResult(new(backend.Entry, []));
            detail.TrySetResult(new(backend.Entry.Id, HistoryDetailState.Captured, new("late content", 12, 12, HistoryDetailFlags.None)));
        });
        await operation;
        Assert.DoesNotContain("late content", cut.Markup);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Target_replacement_fences_old_success_failure_and_finally(bool failure) {
        var backend = new ProviderHistoryUiFixture();
        var first = backend.Entry;
        var pending = new TaskCompletionSource<HistoryMetadata?>();
        backend.Metadata = (id, _) => id == first.Id ? pending.Task : Task.FromResult<HistoryMetadata?>(new(backend.Entry, []));
        using var context = backend.CreateContext();
        var cut = context.Render<ProviderHistoryDetailsDialog>(p => p.Add(x => x.EntryId, first.Id));
        backend.Entry = first with { Id = HistoryEntryId.New(), Provider = first.Provider with { Name = "New target" } };
        cut.Render(p => p.Add(x => x.EntryId, backend.Entry.Id));
        cut.WaitForAssertion(() => Assert.Contains("New target", cut.Markup));
        await cut.InvokeAsync(() => {
            if (failure) {
                pending.SetException(new ProviderHistoryException(HistoryFailure.Denied, "history-api-key-sentinel"));
            } else {
                pending.SetResult(new(first, []));
            }
        });
        Assert.Contains("New target", cut.Markup);
        Assert.DoesNotContain("history-api-key-sentinel", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='history-detail-error']"));
        Assert.False(cut.Find("[data-testid='history-load-content']").HasAttribute("disabled"));
    }

    [Theory]
    [InlineData(HistoryFailure.Denied)]
    [InlineData(HistoryFailure.StaleContext)]
    [InlineData(HistoryFailure.Unavailable)]
    public void Typed_errors_are_distinct_without_key_or_infrastructure_payload(HistoryFailure failure) {
        var backend = new ProviderHistoryUiFixture { Metadata = (_, _) => throw new ProviderHistoryException(failure, "history-api-key-sentinel") };
        using var context = backend.CreateContext();
        var cut = context.Render<ProviderHistoryDetailsDialog>(p => p.Add(x => x.EntryId, backend.Entry.Id));
        Assert.Contains(HistoryPublicErrors.Message(failure), cut.Find("[data-testid='history-detail-error']").TextContent);
        Assert.DoesNotContain("history-api-key-sentinel", cut.Markup);
        Assert.Empty(backend.ContentReads);
    }

    [Fact]
    public void Authorized_content_preserves_exact_text_flags_bytes_and_expiry() {
        using var context = new BunitContext();
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = context.Render<ProviderHistoryContentDialog>(p => p.Add(x => x.Content, HistorySandboxFixture.Content));
        var areas = cut.FindAll("textarea");
        Assert.Equal(HistorySandboxFixture.SyntheticInput, areas[0].TextContent);
        Assert.Equal(HistorySandboxFixture.SyntheticResponse, areas[1].TextContent);
        Assert.Contains("PriorContextNotCaptured", cut.Markup);
        Assert.Contains("bytes", cut.Markup);
        Assert.Contains("2026-09-15 12:00:00 UTC", cut.Markup);
        Assert.Empty(cut.FindAll("script"));
    }
}
