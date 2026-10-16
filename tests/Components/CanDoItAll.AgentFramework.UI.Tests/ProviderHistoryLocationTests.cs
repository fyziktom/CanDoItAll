using Bunit;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.UI.History;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class ProviderHistoryLocationTests {
    private static readonly DateTimeOffset To = new(2026, 8, 28, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Controlled_search_publishes_one_anchored_location_and_ignores_its_echo() {
        var backend = new ProviderHistoryUiFixture();
        var changes = new List<ProviderHistoryLocationChange>();
        using var context = backend.CreateContext();
        var cut = Render(context, backend, null, changes);

        cut.Find("[data-testid='history-search-form']").Submit();
        var search = Assert.Single(changes);
        cut.Render(parameters => parameters.Add(component => component.Location, search.Location));

        Assert.Equal(ProviderHistoryLocationChangeKind.Search, search.Kind);
        Assert.NotNull(search.Location);
        Assert.Equal(ProviderHistoryRange.Last24Hours, search.Location.Filter.Range);
        Assert.Equal(0, search.Location.Filter.To.UtcTicks % TimeSpan.TicksPerSecond);
        Assert.Null(search.Location.Cursor);
        Assert.Equal(1, search.Location.Page);
        var query = Assert.Single(backend.Queries);
        Assert.Equal(search.Location.Filter.To, query.ToUtc);
    }

    [Fact]
    public void Paging_publishes_cursors_and_external_back_and_forward_keep_the_previous_trail() {
        var backend = new ProviderHistoryUiFixture();
        backend.Search = (query, _) => Task.FromResult(Page(backend, query.Cursor switch {
            null => "page-2",
            "page-2" => "page-3",
            _ => null
        }));
        var changes = new List<ProviderHistoryLocationChange>();
        using var context = backend.CreateContext();
        var cut = Render(context, backend, null, changes);
        cut.Find("[data-testid='history-search-form']").Submit();
        var first = changes[^1].Location;
        cut.Render(parameters => parameters.Add(component => component.Location, first));

        cut.Find("[data-testid='history-next']").Click();
        var second = changes[^1];
        cut.Render(parameters => parameters.Add(component => component.Location, second.Location));
        cut.Render(parameters => parameters.Add(component => component.Location, first));
        cut.WaitForAssertion(() => {
            Assert.Equal(1, Presentation(cut).PageNumber);
            Assert.False(Presentation(cut).CanPrevious);
        });
        cut.Render(parameters => parameters.Add(component => component.Location, second.Location));
        cut.WaitForAssertion(() => {
            Assert.Equal(2, Presentation(cut).PageNumber);
            Assert.True(Presentation(cut).CanPrevious);
        });

        Assert.Equal(ProviderHistoryLocationChangeKind.Page, second.Kind);
        Assert.Equal(new ProviderHistoryLocation(first!.Filter, "page-2", 2), second.Location);
        Assert.Equal([null, "page-2", null, "page-2"], backend.Queries.Select(query => query.Cursor));
        Assert.Equal(2, changes.Count);
    }

    [Fact]
    public void An_external_location_restores_its_filter_into_the_form_and_reads_its_page() {
        var backend = new ProviderHistoryUiFixture();
        var changes = new List<ProviderHistoryLocationChange>();
        var location = new ProviderHistoryLocation(new(ProviderHistoryRange.Custom, To) {
            From = To.AddHours(-6),
            Model = "Vendor/Exact",
            Outcome = HistoryOutcome.Failed,
            PageSize = 20
        }, "opaque-page-4", 4);
        using var context = backend.CreateContext();

        var cut = Render(context, backend, location, changes);

        var query = Assert.Single(backend.Queries);
        Assert.Equal(To.AddHours(-6), query.FromUtc);
        Assert.Equal(To, query.ToUtc);
        Assert.Equal("Vendor/Exact", query.Model!.Value.Value);
        Assert.Equal(HistoryOutcome.Failed, query.Outcome);
        Assert.Equal(20, query.PageSize);
        Assert.Equal("opaque-page-4", query.Cursor);
        cut.WaitForAssertion(() => {
            var presentation = Presentation(cut);
            Assert.Equal(HistorySearchPhase.Ready, presentation.Phase);
            Assert.Equal(4, presentation.PageNumber);
            Assert.False(presentation.CanPrevious);
            Assert.True(presentation.HasEarlierPages);
        });
        Assert.Equal("Vendor/Exact", cut.Find("[data-testid='history-model']").GetAttribute("value"));
        Assert.Empty(changes);
    }

    [Fact]
    public async Task Entry_details_follow_the_location_and_closing_publishes_the_parent_location() {
        var backend = new ProviderHistoryUiFixture();
        var changes = new List<ProviderHistoryLocationChange>();
        var parent = new ProviderHistoryLocation(new(ProviderHistoryRange.Last7Days, To));
        using var context = backend.CreateContext();
        var cut = Render(context, backend, parent with { EntryId = backend.Entry.Id }, changes);
        cut.WaitForElement("[data-testid='history-detail-dialog']");

        await cut.InvokeAsync(() => cut.Find("[data-testid='history-detail-close']").ClickAsync(new()));
        var close = Assert.Single(changes);
        cut.Render(parameters => parameters.Add(component => component.Location, close.Location));

        Assert.Equal(ProviderHistoryLocationChangeKind.CloseEntry, close.Kind);
        Assert.Equal(parent, close.Location);
        Assert.Empty(cut.FindAll("[data-testid='history-detail-dialog']"));
        Assert.Single(backend.Queries);
        cut.Render(parameters => parameters.Add(component => component.Location, parent with { EntryId = backend.Entry.Id }));
        cut.WaitForElement("[data-testid='history-detail-dialog']");
        Assert.Equal(2, backend.MetadataReads);
    }

    [Fact]
    public void Clear_publishes_no_location_and_an_external_empty_location_resets_results() {
        var backend = new ProviderHistoryUiFixture();
        var changes = new List<ProviderHistoryLocationChange>();
        var location = new ProviderHistoryLocation(new(ProviderHistoryRange.Last24Hours, To));
        using var context = backend.CreateContext();
        var cut = Render(context, backend, location, changes);
        cut.WaitForElement("[data-testid='history-details']");

        cut.Find("[data-testid='history-clear']").Click();
        var clear = Assert.Single(changes);
        cut.Render(parameters => parameters.Add(component => component.Location, (ProviderHistoryLocation?)null));
        cut.WaitForAssertion(() => Assert.Equal(HistorySearchPhase.NotRequested, Presentation(cut).Phase));
        Assert.Single(backend.Queries);

        cut.Render(parameters => parameters.Add(component => component.Location, location));
        cut.WaitForElement("[data-testid='history-details']");
        cut.Render(parameters => parameters.Add(component => component.Location, (ProviderHistoryLocation?)null));
        cut.WaitForAssertion(() => Assert.Equal(HistorySearchPhase.NotRequested, Presentation(cut).Phase));

        Assert.Equal(new ProviderHistoryLocationChange(null, ProviderHistoryLocationChangeKind.Clear), clear);
        Assert.Equal(2, backend.Queries.Count);
        Assert.Single(changes);
    }

    private static IRenderedComponent<ProviderHistoryWorkspace> Render(BunitContext context, ProviderHistoryUiFixture backend,
        ProviderHistoryLocation? location, List<ProviderHistoryLocationChange> changes) =>
        context.Render<ProviderHistoryWorkspace>(parameters => parameters
            .Add(component => component.History, backend)
            .Add(component => component.Scope, new HistoryProviderScope.AllAuthorized())
            .Add(component => component.Location, location)
            .Add(component => component.LocationChanged, (ProviderHistoryLocationChange change) => changes.Add(change)));

    private static HistoryResultsPresentation Presentation(IRenderedComponent<ProviderHistoryWorkspace> cut)
        => cut.FindComponent<ProviderHistoryResultsSurface>().Instance.Presentation;

    private static HistoryPage Page(ProviderHistoryUiFixture backend, string? next) =>
        new([backend.Entry], next, new(HistoryCoverageState.Current, null), ProviderHistoryUiFixture.Now);
}
