using Bunit;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.UI.History;
using CanDoItAll.AgentFramework.UiSandbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class ProviderHistoryFilterTests {
    [Theory]
    [InlineData("history-provider", "00000000-0000-0000-0000-000000000000")]
    [InlineData("history-credential", "invalid")]
    [InlineData("history-model", "   ")]
    [InlineData("history-external-reference-type", "UpperCase")]
    [InlineData("history-external-reference-value", " invalid ")]
    public void Invalid_raw_fields_do_not_query(string testId, string value) {
        var backend = new ProviderHistoryUiFixture();
        using var context = backend.CreateContext();
        var cut = context.Render<ProviderHistoryWorkspace>(p => p.Add(x => x.History, backend).Add(x => x.Scope, new HistoryProviderScope.AllAuthorized()));
        cut.Find("[data-testid='history-more-filters']").Click();
        cut.Find($"[data-testid='{testId}']").Change(value);
        cut.Find("[data-testid='history-search-form']").Submit();
        Assert.Empty(backend.Queries);
        Assert.NotEmpty(cut.FindAll(".validation-message"));
    }

    [Theory]
    [InlineData(ProviderHistoryRange.Last24Hours, 1)]
    [InlineData(ProviderHistoryRange.Last7Days, 7)]
    public void Relative_query_freezes_utc_before_await(ProviderHistoryRange range, int days) {
        var instant = new DateTimeOffset(2026, 10, 3, 12, 30, 0, TimeSpan.FromHours(-4));
        var query = new ProviderHistoryFilterDraft(instant) { Range = range }.ToQuery(new HistoryProviderScope.AllAuthorized(), instant);
        Assert.Equal(TimeSpan.Zero, query.ToUtc.Offset);
        Assert.Equal(instant.ToUniversalTime(), query.ToUtc);
        Assert.Equal(query.ToUtc.AddDays(-days), query.FromUtc);
    }

    [Fact]
    public async Task Custom_interval_is_half_open_and_keeps_exact_31_day_boundary() {
        using var reader = new HistoryScenarioReader(HistoryScenario.Normal);
        var now = HistorySandboxFixture.Now;
        var draft = new ProviderHistoryFilterDraft(now) {
            Range = ProviderHistoryRange.Custom,
            FromUtc = now.AddDays(-31).UtcDateTime,
            ToUtc = now.UtcDateTime
        };
        var query = draft.ToQuery(new HistoryProviderScope.SingleProvider(HistoryScenarioReader.Provider), now);
        var first = (await reader.SearchAsync(query, default)).Entries[0];
        var excluded = await reader.SearchAsync(query with { ToUtc = first.SortAtUtc }, default);
        Assert.DoesNotContain(excluded.Entries, entry => entry.Id == first.Id);
        var included = await reader.SearchAsync(query with { FromUtc = first.SortAtUtc }, default);
        Assert.Equal(first.Id, Assert.Single(included.Entries).Id);
    }

    [Fact]
    public async Task Disposing_search_state_removes_retained_query_page_and_callbacks() {
        using var reader = new HistoryScenarioReader(HistoryScenario.Normal);
        var state = new ProviderHistorySearchState(reader, NullLogger<ProviderHistorySearchState>.Instance);
        await state.SearchAsync(new(new HistoryProviderScope.AllAuthorized(), HistorySandboxFixture.Now.AddDays(-1), HistorySandboxFixture.Now));
        var callbacks = 0;
        state.Changed += () => callbacks++;
        var origin = state.Origin;
        state.Dispose();
        Assert.Null(state.Page);
        Assert.Null(state.AppliedQuery);
        Assert.Null(state.Error);
        Assert.False(state.IsCurrent(origin));
        state.Cancel();
        state.InvalidateIntents();
        Assert.Equal(0, callbacks);
    }
}
