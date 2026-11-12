using Bunit;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.UI.History;
using CanDoItAll.AgentFramework.UiSandbox;
using CanDoItAll.AgentFramework.UiSandbox.Components;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class ProviderHistoryScenarioTests {
    public static IEnumerable<object[]> Scenarios => HistoryScenarios.Options.Select(option => new object[] { option.Value });

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task Each_scenario_drives_the_real_workspace_and_separate_read_layers(HistoryScenario scenario) {
        using var context = new ProviderHistoryUiFixture().CreateContext();
        var cut = context.Render<HistoryScenarioWorkspace>(p => p.Add(x => x.Scenario, scenario).Add(x => x.CompleteAutomatically, false));
        Assert.Contains("Search: 0 · Metadata: 0 · Content: 0", cut.Markup);
        var search = cut.Find("[data-testid='history-search-form']").SubmitAsync();
        if (scenario == HistoryScenario.DelayedSearch) {
            cut.WaitForAssertion(() => Assert.Contains("Pending: 1", cut.Markup));
            cut.Find("[data-testid='sandbox-history-complete']").Click();
        }
        await search;
        Assert.Contains("Search: 1 · Metadata: 0 · Content: 0", cut.Markup);
        if (scenario is HistoryScenario.Denied or HistoryScenario.Failure) {
            cut.Find("[data-testid='history-error']");
            Assert.Empty(cut.FindAll("[data-testid='history-details']"));
            return;
        }
        if (scenario == HistoryScenario.Empty) {
            Assert.Contains("No matching requests", cut.Markup);
            return;
        }
        if (scenario == HistoryScenario.Partial) {
            Assert.Contains("Coverage is incomplete", cut.Markup);
        }
        var metadata = cut.FindAll("[data-testid='history-details']")[0].ClickAsync();
        if (scenario == HistoryScenario.DelayedMetadata) {
            cut.WaitForAssertion(() => Assert.Contains("Pending: 1", cut.Markup));
            cut.Find("[data-testid='sandbox-history-complete']").Click();
        }
        await metadata;
        Assert.Contains("Search: 1 · Metadata: 1 · Content: 0", cut.Markup);
        if (scenario == HistoryScenario.MetadataDenied) {
            cut.WaitForElement("[data-testid='history-detail-error']");
            Assert.Empty(cut.FindAll("[data-testid='history-load-content']"));
            return;
        }
        var selector = scenario == HistoryScenario.Canonical ? "[data-testid='history-owner-content']" : "[data-testid='history-load-content']";
        var content = cut.WaitForElement(selector).ClickAsync();
        if (scenario == HistoryScenario.DelayedContent) {
            cut.WaitForAssertion(() => Assert.Contains("Pending: 1", cut.Markup));
            cut.Find("[data-testid='sandbox-history-complete']").Click();
        }
        await content;
        Assert.Contains("Search: 1 · Metadata: 1 · Content: 1", cut.Markup);
        if (scenario == HistoryScenario.ContentDenied) {
            cut.Find("[data-testid='history-detail-error']");
            Assert.Empty(cut.FindComponents<ProviderHistoryMetadataView>());
            Assert.Empty(cut.FindAll("[data-testid='history-content-dialog']"));
            return;
        }
        var state = cut.WaitForElement("[data-testid='history-content-state']").TextContent;
        Assert.Contains(scenario switch {
            HistoryScenario.Expired => nameof(HistoryDetailState.Expired),
            HistoryScenario.Pending => nameof(HistoryDetailState.PendingCanonical),
            HistoryScenario.Unavailable => nameof(HistoryDetailState.Unavailable),
            HistoryScenario.Canonical => nameof(HistoryDetailState.Canonical),
            _ => nameof(HistoryDetailState.Captured)
        }, state);
        if (scenario == HistoryScenario.Redacted) {
            Assert.Contains("Truncated, Redacted", cut.Markup);
            var capture = cut.FindComponent<ProviderHistoryContentDialog>().Instance.Content.Input!;
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(capture.Text), capture.CapturedBytes);
            Assert.True(capture.OriginalBytes > capture.CapturedBytes);
        }
    }

    [Fact]
    public async Task Fixture_cursor_is_query_bound_and_canonical_owner_is_exact() {
        using var reader = new HistoryScenarioReader(HistoryScenario.Canonical);
        var query = new ProviderRequestHistoryQuery(new HistoryProviderScope.AllAuthorized(), HistorySandboxFixture.Now.AddDays(-1), HistorySandboxFixture.Now) { PageSize = 2 };
        var page = await reader.SearchAsync(query, default);
        Assert.Equal(2, page.Entries.Count);
        Assert.NotNull(page.NextCursor);
        var refused = await Assert.ThrowsAsync<ProviderHistoryException>(() => reader.SearchAsync(query with {
            Cursor = page.NextCursor, Model = new("different exact identity")
        }, default));
        Assert.Equal(HistoryFailure.InvalidCursor, refused.Failure);
        var next = await reader.SearchAsync(query with { Cursor = page.NextCursor }, default);
        Assert.DoesNotContain(next.Entries, item => page.Entries.Any(previous => previous.Id == item.Id));
        var entry = next.Entries[1];
        var metadata = await reader.GetMetadataAsync(entry.Id, default);
        Assert.NotNull(metadata);
        Assert.All(metadata.Owners, link => Assert.Equal(entry.Id, link.EntryId));
        var owner = metadata.Owners[0].Source;
        Assert.Equal(HistoryDetailState.Canonical, (await reader.GetDetailAsync(entry.Id, owner, default)).State);
        Assert.Equal(HistoryDetailState.Unavailable, (await reader.GetDetailAsync(entry.Id, owner with { Evidence = new("other") }, default)).State);
    }
}
