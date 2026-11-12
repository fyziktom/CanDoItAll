using CanDoItAll.AppComponents.Navigation;

namespace CanDoItAll.Tests.Unit.Navigation;

public sealed class UrlStateBindingTests {
    private static readonly Guid ItemA = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ItemB = Guid.Parse("22222222-2222-4222-8222-222222222222");

    [Fact]
    public void First_apply_reports_the_initial_state_and_canonicalizes_once_with_replace() {
        var navigation = new ScriptedNavigationManager($"/items?TAB=list&ItemId={ItemA}&section=ADVANCED&page=1&utm=x");
        var binding = new UrlStateBinding<SampleUrlState>(navigation, SampleUrlCodec.Instance);

        var change = binding.Apply();

        Assert.True(change.IsInitial);
        Assert.True(change.IsChanged);
        Assert.Equal(new SampleUrlState(SampleTab.Items, ItemA, SampleSection.Advanced), binding.State);
        var request = Assert.Single(navigation.Requests);
        Assert.True(request.Replace);
        Assert.Equal($"/items?tab=items&itemId={ItemA}&section=advanced&utm=x", request.Relative);

        navigation.Deliver(0);
        var arrival = binding.Apply();

        Assert.False(arrival.IsChanged);
        Assert.Single(navigation.Requests);
    }

    [Fact]
    public void Canonicalization_is_skipped_while_prerendering() {
        var navigation = new ScriptedNavigationManager("/items?tab=overview");
        var binding = new UrlStateBinding<SampleUrlState>(navigation, SampleUrlCodec.Instance);

        binding.Apply(canonicalize: false);

        Assert.Empty(navigation.Requests);
        Assert.Equal(new SampleUrlState(), binding.State);
    }

    [Fact]
    public void Navigate_applies_the_normalized_state_immediately_and_pushes_once() {
        var navigation = new ScriptedNavigationManager("/items");
        var binding = new UrlStateBinding<SampleUrlState>(navigation, SampleUrlCodec.Instance);
        binding.Apply();
        var version = binding.Version;

        Assert.True(binding.Navigate(new(SampleTab.History, ItemA, SampleSection.General, "ignored", 3)));
        Assert.False(binding.Navigate(new(SampleTab.History)));

        Assert.Equal(new SampleUrlState(SampleTab.History), binding.State);
        Assert.Equal(version + 1, binding.Version);
        var request = Assert.Single(navigation.Requests);
        Assert.False(request.Replace);
        Assert.Null(request.HistoryEntryState);
        Assert.Equal("/items?tab=history", request.Relative);
    }

    [Fact]
    public void Stale_and_original_locations_never_override_a_newer_intent() {
        var navigation = new ScriptedNavigationManager("/items");
        var binding = new UrlStateBinding<SampleUrlState>(navigation, SampleUrlCodec.Instance);
        binding.Apply();

        binding.Navigate(new(SampleTab.Items, ItemA));
        binding.Navigate(new(SampleTab.Items, ItemB));
        var whileInFlight = binding.Apply();
        navigation.Deliver(0);
        var staleArrival = binding.Apply();
        navigation.Deliver(1);
        var latestArrival = binding.Apply();

        Assert.False(whileInFlight.IsChanged);
        Assert.False(staleArrival.IsChanged);
        Assert.False(latestArrival.IsChanged);
        Assert.Equal(ItemB, binding.State.ItemId);
        Assert.Equal(2, navigation.Requests.Count);
        Assert.Equal($"/items?tab=items&itemId={ItemA}", navigation.Requests[0].Relative);
        Assert.Equal($"/items?tab=items&itemId={ItemB}", navigation.Requests[1].Relative);
    }

    [Fact]
    public void An_external_location_after_settled_navigation_is_applied() {
        var navigation = new ScriptedNavigationManager("/items") { DeliverImmediately = true };
        var binding = new UrlStateBinding<SampleUrlState>(navigation, SampleUrlCodec.Instance);
        binding.Apply();
        binding.Navigate(new(SampleTab.Items, ItemA));
        binding.Apply();

        navigation.External("/items");
        var back = binding.Apply();

        Assert.True(back.IsChanged);
        Assert.Equal(ItemA, back.Previous.ItemId);
        Assert.Equal(new SampleUrlState(), back.Current);
    }

    [Fact]
    public void An_external_location_supersedes_an_in_flight_navigation() {
        var navigation = new ScriptedNavigationManager("/items");
        var binding = new UrlStateBinding<SampleUrlState>(navigation, SampleUrlCodec.Instance);
        binding.Apply();
        binding.Navigate(new(SampleTab.Items, ItemA));

        navigation.External("/items?tab=history");
        var change = binding.Apply();
        navigation.Deliver(0);
        var lateArrival = binding.Apply();

        Assert.True(change.IsChanged);
        Assert.Equal(new SampleUrlState(SampleTab.History), change.Current);
        Assert.True(lateArrival.IsChanged);
        Assert.Equal(ItemA, binding.State.ItemId);
    }

    [Fact]
    public void Replace_keeps_the_current_history_entry_state() {
        var navigation = new ScriptedNavigationManager("/items?tab=items", "marker");
        var binding = new UrlStateBinding<SampleUrlState>(navigation, SampleUrlCodec.Instance);
        binding.Apply();

        binding.Navigate(new(SampleTab.Items, Search: "a"), UrlHistoryMode.Replace);

        var request = Assert.Single(navigation.Requests);
        Assert.True(request.Replace);
        Assert.Equal("marker", request.HistoryEntryState);
    }

    [Fact]
    public async Task Closing_an_overlay_opened_here_goes_back_to_its_parent() {
        var navigation = new ScriptedNavigationManager("/items?tab=items&q=red") { DeliverImmediately = true };
        var history = new ScriptedNavigationHistory(navigation, "/items?tab=items&q=red");
        var binding = new UrlStateBinding<SampleUrlState>(navigation, SampleUrlCodec.Instance, history);
        binding.Apply();

        binding.OpenOverlay(binding.State with { ItemId = ItemA });
        binding.Apply();
        binding.Navigate(binding.State with { Section = SampleSection.Advanced }, UrlHistoryMode.Replace);
        binding.Apply();
        await binding.CloseOverlayAsync(binding.State with { ItemId = null, Section = null });
        var closed = binding.Apply();

        Assert.Equal(1, history.BackCalls);
        Assert.Equal("cda-nav-parent:/items?tab=items&q=red", navigation.Requests[0].HistoryEntryState);
        Assert.Equal("cda-nav-parent:/items?tab=items&q=red", navigation.Requests[1].HistoryEntryState);
        Assert.Equal(2, navigation.Requests.Count);
        Assert.False(closed.IsChanged);
        Assert.Equal(new SampleUrlState(SampleTab.Items, Search: "red"), binding.State);
    }

    [Fact]
    public async Task An_overlay_opened_while_the_close_goes_back_is_pushed_once_the_back_has_arrived() {
        var navigation = new ScriptedNavigationManager("/items?tab=items") { DeliverImmediately = true };
        var history = new ScriptedNavigationHistory(navigation, "/items?tab=items") { HoldBack = true };
        var binding = new UrlStateBinding<SampleUrlState>(navigation, SampleUrlCodec.Instance, history);
        binding.Apply();
        binding.OpenOverlay(binding.State with { ItemId = ItemA });
        binding.Apply();

        await binding.CloseOverlayAsync(binding.State with { ItemId = null });
        binding.OpenOverlay(binding.State with { ItemId = ItemB });

        Assert.Equal(1, history.BackCalls);
        Assert.Single(navigation.Requests);
        Assert.Equal(ItemB, binding.State.ItemId);

        history.ArriveBack();
        binding.Apply();
        var opened = navigation.Requests[^1];
        Assert.Equal(2, navigation.Requests.Count);
        Assert.False(opened.Replace);
        Assert.Equal($"/items?tab=items&itemId={ItemB}", opened.Relative);
        Assert.Equal("cda-nav-parent:/items?tab=items", opened.HistoryEntryState);
        Assert.False(binding.Apply().IsChanged);
        Assert.Equal(ItemB, binding.State.ItemId);
    }

    [Fact]
    public async Task Another_location_arriving_before_the_back_supersedes_the_writes_it_held() {
        var navigation = new ScriptedNavigationManager("/items?tab=items") { DeliverImmediately = true };
        var history = new ScriptedNavigationHistory(navigation, "/items?tab=items") { HoldBack = true };
        var binding = new UrlStateBinding<SampleUrlState>(navigation, SampleUrlCodec.Instance, history);
        binding.Apply();
        binding.OpenOverlay(binding.State with { ItemId = ItemA });
        binding.Apply();
        await binding.CloseOverlayAsync(binding.State with { ItemId = null });
        binding.OpenOverlay(binding.State with { ItemId = ItemB });

        navigation.External("/items?tab=history");
        var change = binding.Apply();
        history.ArriveBack();
        binding.Apply();

        Assert.True(change.IsChanged);
        Assert.Single(navigation.Requests);
        Assert.Equal(new SampleUrlState(SampleTab.Items), binding.State);
    }

    [Fact]
    public async Task Closing_a_deep_linked_overlay_replaces_the_location_with_its_parent() {
        var navigation = new ScriptedNavigationManager($"/items?tab=items&itemId={ItemA}") { DeliverImmediately = true };
        var history = new ScriptedNavigationHistory(navigation, "/elsewhere");
        var binding = new UrlStateBinding<SampleUrlState>(navigation, SampleUrlCodec.Instance, history);
        binding.Apply();

        await binding.CloseOverlayAsync(binding.State with { ItemId = null });

        Assert.Equal(0, history.BackCalls);
        var request = Assert.Single(navigation.Requests);
        Assert.True(request.Replace);
        Assert.Null(request.HistoryEntryState);
        Assert.Equal("/items?tab=items", navigation.Relative);
    }

    [Fact]
    public async Task A_reloaded_overlay_restores_its_parent_marker_so_sections_keep_it_and_close_goes_back() {
        var navigation = new ScriptedNavigationManager($"/items?tab=items&itemId={ItemA}") { DeliverImmediately = true };
        var history = new ScriptedNavigationHistory(navigation, "/items?tab=items") { EntryState = "cda-nav-parent:/items?tab=items" };
        var binding = new UrlStateBinding<SampleUrlState>(navigation, SampleUrlCodec.Instance, history);
        binding.Apply(canonicalize: false);
        Assert.Equal(0, history.EntryStateReads);
        binding.Apply();

        binding.Navigate(binding.State with { Section = SampleSection.Advanced }, UrlHistoryMode.Replace);
        binding.Apply();
        await binding.CloseOverlayAsync(binding.State with { ItemId = null, Section = null });

        var section = Assert.Single(navigation.Requests);
        Assert.True(section.Replace);
        Assert.Equal("cda-nav-parent:/items?tab=items", section.HistoryEntryState);
        Assert.Equal(1, history.BackCalls);
        Assert.Equal(1, history.EntryStateReads);
        Assert.Equal("/items?tab=items", navigation.Relative);
    }

    [Fact]
    public async Task Closing_asks_the_host_for_the_entry_state_when_blazor_does_not_know_it() {
        var navigation = new ScriptedNavigationManager($"/items?tab=items&itemId={ItemA}") { DeliverImmediately = true };
        var history = new ScriptedNavigationHistory(navigation, "/items?tab=items");
        var binding = new UrlStateBinding<SampleUrlState>(navigation, SampleUrlCodec.Instance, history);
        binding.Apply();
        history.EntryState = "cda-nav-parent:/items?tab=items";

        await binding.CloseOverlayAsync(binding.State with { ItemId = null });

        Assert.Equal(1, history.BackCalls);
        Assert.Equal(2, history.EntryStateReads);
        Assert.Empty(navigation.Requests);
    }

    [Fact]
    public async Task A_close_applies_at_once_and_an_intent_made_while_the_host_answers_wins() {
        var navigation = new ScriptedNavigationManager($"/items?tab=items&itemId={ItemA}");
        var history = new ScriptedNavigationHistory(navigation, "/items?tab=items");
        var binding = new UrlStateBinding<SampleUrlState>(navigation, SampleUrlCodec.Instance, history);
        binding.Apply();
        var answer = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        history.PendingAnswer = answer;

        var closing = binding.CloseOverlayAsync(binding.State with { ItemId = null });
        Assert.Null(binding.State.ItemId);
        binding.Navigate(binding.State with { Search = "red" });
        answer.SetResult("cda-nav-parent:/items?tab=items");
        await closing;

        Assert.Equal(0, history.BackCalls);
        var search = Assert.Single(navigation.Requests);
        Assert.False(search.Replace);
        Assert.Equal("/items?tab=items&q=red", search.Relative);
    }

    [Fact]
    public async Task An_entry_state_blazor_knows_wins_and_the_host_is_not_asked() {
        var navigation = new ScriptedNavigationManager($"/items?tab=items&itemId={ItemA}", "cda-nav-parent:/items?tab=history");
        var history = new ScriptedNavigationHistory(navigation, "/items?tab=history") { EntryState = "cda-nav-parent:/items?tab=items" };
        var binding = new UrlStateBinding<SampleUrlState>(navigation, SampleUrlCodec.Instance, history);
        binding.Apply();

        await binding.CloseOverlayAsync(binding.State with { ItemId = null });

        Assert.Equal(0, history.EntryStateReads);
        Assert.Equal(0, history.BackCalls);
        Assert.True(Assert.Single(navigation.Requests).Replace);
    }

    [Fact]
    public async Task A_parent_marker_for_another_location_falls_back_to_replace() {
        var navigation = new ScriptedNavigationManager($"/items?tab=items&itemId={ItemA}", "cda-nav-parent:/items?tab=history");
        var history = new ScriptedNavigationHistory(navigation, "/items?tab=history");
        var binding = new UrlStateBinding<SampleUrlState>(navigation, SampleUrlCodec.Instance, history);
        binding.Apply();

        await binding.CloseOverlayAsync(binding.State with { ItemId = null });

        Assert.Equal(0, history.BackCalls);
        Assert.True(Assert.Single(navigation.Requests).Replace);
    }

    [Fact]
    public void Intents_while_prerendering_update_the_state_without_writing_the_location() {
        var navigation = new ScriptedNavigationManager("/items");
        var binding = new UrlStateBinding<SampleUrlState>(navigation, SampleUrlCodec.Instance);
        binding.Apply(canonicalize: false);

        Assert.False(binding.Navigate(new(SampleTab.Items, ItemA), UrlHistoryMode.Replace));
        Assert.False(binding.OpenOverlay(new(SampleTab.Items, ItemB)));

        Assert.Empty(navigation.Requests);
        Assert.Equal(new SampleUrlState(SampleTab.Items, ItemB), binding.State);
        binding.Apply();
        Assert.True(binding.Navigate(new(SampleTab.History)));
        Assert.Single(navigation.Requests);
    }

    [Fact]
    public void A_bound_page_ignores_and_never_rewrites_the_location_of_another_page() {
        var navigation = new ScriptedNavigationManager($"/items/{ItemA}/details?tab=history") { DeliverImmediately = true };
        var binding = new UrlStateBinding<SampleUrlState>(navigation, SampleUrlCodec.Instance, null, typeof(SamplePage));
        binding.Apply();

        navigation.External("/elsewhere?tab=ITEMS&itemId=not-a-guid");
        var foreign = binding.Apply();
        var written = binding.Navigate(new(SampleTab.Items, ItemB));

        Assert.False(foreign.IsChanged);
        Assert.False(written);
        Assert.Empty(navigation.Requests);
        Assert.Equal(new SampleUrlState(SampleTab.History), binding.State);
        navigation.External($"/items/{ItemB}/details?tab=items");
        Assert.True(binding.Apply().IsChanged);
        Assert.Equal(new SampleUrlState(SampleTab.Items), binding.State);
    }

    [Fact]
    public void A_page_created_outside_its_routes_still_owns_the_location_it_was_created_at() {
        var navigation = new ScriptedNavigationManager("/?tab=history");
        var binding = new UrlStateBinding<SampleUrlState>(navigation, SampleUrlCodec.Instance, null, typeof(SamplePage));

        var initial = binding.Apply();
        var written = binding.Navigate(new(SampleTab.Items));

        Assert.True(initial.IsInitial);
        Assert.Equal(new SampleUrlState(SampleTab.History), initial.Current);
        Assert.True(written);
        Assert.Equal("/?tab=items", Assert.Single(navigation.Requests).Relative);
    }

    [Theory]
    [InlineData("items", true)]
    [InlineData("ITEMS/", true)]
    [InlineData("items/0f8fad5b-d9cb-469f-a165-70867728950e/details?tab=x", true)]
    [InlineData("items/a/b", false)]
    [InlineData("items-other", false)]
    [InlineData("archive", true)]
    [InlineData("archive/2026", true)]
    [InlineData("files/a/b/c", true)]
    [InlineData("files", true)]
    [InlineData("", false)]
    public void Route_templates_match_literals_ignoring_case_and_parameters_by_segment(string path, bool expected) {
        Assert.Equal(expected, UrlRoutes.Matches(UrlRoutes.Templates(typeof(SamplePage)), path));
    }

    [Fact]
    public void A_page_without_a_route_cannot_own_locations() {
        Assert.Throws<ArgumentException>(() => UrlRoutes.Templates(typeof(UrlStateBindingTests)));
    }

    [Microsoft.AspNetCore.Components.Route("/items")]
    [Microsoft.AspNetCore.Components.Route("/items/{ItemId:guid}/details")]
    [Microsoft.AspNetCore.Components.Route("/archive/{Year:int?}")]
    [Microsoft.AspNetCore.Components.Route("/files/{*Path}")]
    private sealed class SamplePage;

    [Fact]
    public void Href_composes_from_the_latest_requested_location() {
        var navigation = new ScriptedNavigationManager("/items?utm=x#top");
        var binding = new UrlStateBinding<SampleUrlState>(navigation, SampleUrlCodec.Instance);
        binding.Apply();
        binding.Navigate(new(SampleTab.Items));

        Assert.Equal($"/items?tab=items&itemId={ItemB}&utm=x#top", binding.Href(new(SampleTab.Items, ItemB)));
    }
}
