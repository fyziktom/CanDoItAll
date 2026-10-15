using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.UI.History;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages;
using CanDoItAll.Modules.AgentFramework.Pages.Navigation;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentsUrlCodecTests {
    private static readonly AgentsUrlCodec Codec = AgentsUrlCodec.Instance;
    private static readonly Guid Agent = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
    private static readonly Guid Team = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
    private static readonly Guid Provider = Guid.Parse("4a0a9a2e-7c39-4d3e-9a7f-1d2c3b4a5e6f");
    private static readonly Guid Entry = Guid.Parse("a3bb189e-8bf9-4888-9912-ace4e6543002");
    private static readonly Guid Conversation = Guid.Parse("9b2f4c1e-3d5a-4e6b-8c7d-0e1f2a3b4c5d");
    private static readonly Guid Definition = Guid.Parse("1d2e3f4a-5b6c-4d7e-8f90-a1b2c3d4e5f6");

    [Theory]
    [InlineData(AgentWorkspaceSection.Overview, "")]
    [InlineData(AgentWorkspaceSection.Agents, "?tab=agents")]
    [InlineData(AgentWorkspaceSection.SimpleChats, "?tab=simple-chats")]
    [InlineData(AgentWorkspaceSection.Providers, "?tab=providers")]
    [InlineData(AgentWorkspaceSection.RequestHistory, "?tab=request-history")]
    [InlineData(AgentWorkspaceSection.Voice, "?tab=voice")]
    [InlineData(AgentWorkspaceSection.FloatingChat, "?tab=floating-chat")]
    [InlineData(AgentWorkspaceSection.Chat, "?tab=chat")]
    [InlineData(AgentWorkspaceSection.Capabilities, "?tab=capabilities")]
    [InlineData(AgentWorkspaceSection.Governance, "?tab=governance")]
    [InlineData(AgentWorkspaceSection.Diagnostics, "?tab=diagnostics")]
    public void Every_tab_keeps_its_existing_token_and_round_trips(AgentWorkspaceSection tab, string canonical) {
        var state = new AgentsUrlState { Tab = tab };

        Assert.Equal(canonical, Codec.Format(state));
        Assert.Equal(state, Read(canonical));
        Assert.Equal(tab.ToTabKey(), AgentsUrlCodec.Tabs.Format(tab));
        Assert.Equal(tab, AgentWorkspaceSections.FromTabKey(tab.ToTabKey()));
    }

    [Theory]
    [InlineData("?tab=scenarios")]
    [InlineData("?tab=")]
    [InlineData("?tab=Agents&agentSection=runtime")]
    public void Unknown_tabs_and_orphan_values_fall_back_to_a_clean_canonical_form(string query) {
        var canonical = Codec.Format(Read(query));

        Assert.Equal(query.Contains("Agents", StringComparison.Ordinal) ? "?tab=agents" : "", canonical);
    }

    [Fact]
    public void Agent_editor_is_addressed_by_agent_and_section_only_on_the_agents_tab() {
        var editor = Read($"?tab=agents&agentId={Agent}&agentSection=RUNTIME&teamId={Team}");

        Assert.Equal(Agent, editor.OpenAgentId);
        Assert.Equal(AgentEditorSection.Runtime, editor.AgentSection);
        Assert.Equal(Team, editor.TeamId);
        Assert.Equal($"?tab=agents&agentId={Agent}&agentSection=runtime&teamId={Team}", Codec.Format(editor));
        Assert.Equal($"?tab=agents&agentId={Agent}", Codec.Format(Read($"?tab=agents&agentId={Agent}&agentSection=identity")));
        Assert.Equal($"?tab=agents&agentId={Agent}", Codec.Format(Read($"?tab=agents&agentId={Agent}&agentSection=unknown")));
        Assert.Equal("?tab=agents", Codec.Format(Read("?tab=agents&agentSection=runtime&agentId=not-a-guid")));
    }

    [Theory]
    [InlineData(AgentWorkspaceSection.Chat, true)]
    [InlineData(AgentWorkspaceSection.Capabilities, true)]
    [InlineData(AgentWorkspaceSection.Governance, true)]
    [InlineData(AgentWorkspaceSection.Providers, false)]
    [InlineData(AgentWorkspaceSection.Overview, false)]
    public void Agent_selection_is_kept_only_by_tabs_that_use_it(AgentWorkspaceSection tab, bool kept) {
        var state = Codec.Normalize(new AgentsUrlState { Tab = tab, AgentId = Agent, AgentSection = AgentEditorSection.Voice, TeamId = Team });

        Assert.Equal(kept ? Agent : null, state.AgentId);
        Assert.Null(state.OpenAgentId);
        Assert.Null(state.TeamId);
        Assert.Equal(AgentEditorSection.Identity, state.AgentSection);
    }

    [Fact]
    public void Simple_chat_state_keeps_its_existing_vocabulary_and_discards_incompatible_identifiers() {
        Assert.Equal("?tab=simple-chats", Codec.Format(Read("?tab=simple-chats")));
        Assert.Equal(new SimpleChatWorkspaceRouteState(SimpleChatWorkspaceView.Conversations, null, Conversation),
            Read($"?tab=simple-chats&conversationId={Conversation}").SimpleChat);
        Assert.Equal($"?tab=simple-chats&simpleChatView=conversations&conversationId={Conversation}",
            Codec.Format(Read($"?tab=simple-chats&simpleChatView=conversations&definitionId={Definition}&conversationId={Conversation}")));
        Assert.Equal($"?tab=simple-chats&simpleChatView=definitions&definitionId={Definition}",
            Codec.Format(Read($"?tab=simple-chats&definitionId={Definition}")));
        Assert.Equal("", Codec.Format(Read($"?conversationId={Conversation}")));
    }

    [Fact]
    public void Chats_compatibility_redirect_keeps_recognized_state_and_drops_foreign_parameters() {
        var route = AgentsUrlCodec.BuildChatsCompatibilityRedirect(
            new Uri($"https://localhost/chats?simpleChatView=definitions&definitionId={Definition:D}&ignored=secret"));

        Assert.Equal($"/agents?tab=simple-chats&simpleChatView=definitions&definitionId={Definition:D}", route);
    }

    [Theory]
    [InlineData("7d", ProviderUsagePeriod.SevenDays)]
    [InlineData("14d", ProviderUsagePeriod.FourteenDays)]
    [InlineData("1m", ProviderUsagePeriod.Month)]
    [InlineData("1q", ProviderUsagePeriod.Quarter)]
    [InlineData("1y", ProviderUsagePeriod.Year)]
    [InlineData("invalid", ProviderUsagePeriod.SevenDays)]
    public void Usage_period_is_an_overview_filter_with_wire_tokens(string token, ProviderUsagePeriod period) {
        var overview = Read($"?usagePeriod={token}&usageScope=agents");

        Assert.Equal(period, overview.UsagePeriod);
        Assert.Equal(ProviderUsageWorkloadSelection.Agents, overview.UsageSelection);
        Assert.Equal(period == ProviderUsagePeriod.SevenDays ? "?usageScope=agents" : $"?usageScope=agents&usagePeriod={token}", Codec.Format(overview));
        Assert.Equal("?tab=providers", Codec.Format(Read($"?tab=providers&usagePeriod={token}&usageScope=agents")));
    }

    [Theory]
    [InlineData("agents", ProviderUsageWorkloadSelection.Agents)]
    [InlineData("simple-chats", ProviderUsageWorkloadSelection.SimpleChats)]
    [InlineData("both", ProviderUsageWorkloadSelection.Both)]
    [InlineData("invalid", ProviderUsageWorkloadSelection.Both)]
    public void Usage_scope_parsing_is_typed_and_deterministic(string token, ProviderUsageWorkloadSelection expected) {
        Assert.Equal(expected, Read($"?usageScope={token}").UsageSelection);
    }

    [Fact]
    public void Provider_selection_section_and_shared_connections_dialog_belong_to_the_providers_tab() {
        var state = Read($"?tab=providers&providerId={Provider}&providerSection=sharing&dialog=shared-connections");

        Assert.Equal(Provider, state.ProviderId);
        Assert.Equal(ProviderEditorSection.Sharing, state.ProviderSection);
        Assert.True(state.IsSharedConnectionsOpen);
        Assert.Equal($"?tab=providers&providerId={Provider}&providerSection=sharing&dialog=shared-connections", Codec.Format(state));
        Assert.Equal("?tab=providers&dialog=shared-connections", Codec.Format(Read("?tab=providers&providerSection=sharing&dialog=shared-connections")));
        Assert.Equal("?tab=agents", Codec.Format(Read($"?tab=agents&providerId={Provider}&dialog=shared-connections")));
    }

    [Fact]
    public void Request_history_writes_its_applied_search_with_anchor_and_without_personal_filters() {
        var to = new DateTimeOffset(2026, 10, 14, 8, 30, 15, TimeSpan.Zero);
        var location = new ProviderHistoryLocation(new(ProviderHistoryRange.Last7Days, to) {
            ProviderId = Provider,
            Outcome = HistoryOutcome.Failed,
            Workload = HistoryWorkload.SimpleChat,
            Subject = "person@example.test",
            Issuer = "https://issuer.example.test",
            ExternalReferenceType = "crm.contact",
            ExternalReferenceValue = "private-42",
            PageSize = 25
        }, "opaque-cursor", 3, new HistoryEntryId(Entry));

        var state = new AgentsUrlState { Tab = AgentWorkspaceSection.RequestHistory, History = AgentsHistoryUrlState.FromLocation(location) };
        var query = Codec.Format(state);

        Assert.Equal(
            $"?tab=request-history&range=7d&to=2026-10-14T08%3A30%3A15Z&providerId={Provider}&workload=simple-chat&outcome=failed&size=25&cursor=opaque-cursor&page=3&entryId={Entry}",
            query);
        Assert.DoesNotContain("example", query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private", query, StringComparison.OrdinalIgnoreCase);
        var restored = Read(query);
        Assert.Equal(state, restored);
        var shown = restored.History!.ToLocation();
        Assert.Null(shown.Filter.Subject);
        Assert.Equal(new HistoryEntryId(Entry), shown.EntryId);
        Assert.Equal(new HistoryProviderScope.SingleProvider(new(Provider)), shown.Filter.ToQuery(new HistoryProviderScope.AllAuthorized()).Scope);
    }

    [Fact]
    public void An_unanchored_history_link_is_applied_once_anchored_and_never_keeps_a_cursor() {
        var state = Read("?tab=request-history&outcome=failed&cursor=stale&page=4");

        Assert.False(state.History!.IsAnchored);
        Assert.Null(state.History.Cursor);
        Assert.Equal(1, state.History.Page);
        var anchored = state.History.AnchoredAt(new DateTimeOffset(2026, 10, 14, 8, 30, 15, 987, TimeSpan.Zero));
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 8, 30, 15, TimeSpan.Zero), anchored.To);
        Assert.Equal("?tab=request-history&range=24h&to=2026-10-14T08%3A30%3A15Z&outcome=failed",
            Codec.Format(state with { History = anchored }));
        Assert.Equal("?tab=request-history", Codec.Format(Read("?tab=request-history")));
    }

    [Theory]
    [InlineData("&from=2026-10-01T00:00:00Z&to=2026-10-02T00:00:00Z", true)]
    [InlineData("&from=2026-10-02T00:00:00Z&to=2026-10-01T00:00:00Z", false)]
    [InlineData("&from=2026-08-01T00:00:00Z&to=2026-10-01T00:00:00Z", false)]
    [InlineData("&to=2026-10-01T00:00:00Z", false)]
    public void A_custom_history_interval_must_be_positive_bounded_and_complete(string interval, bool valid) {
        var state = Read("?tab=request-history&range=custom" + interval);

        Assert.Equal(valid, state.History is not null);
    }

    [Fact]
    public void Provider_scoped_history_needs_the_history_section_and_never_widens_its_scope() {
        var to = "2026-10-14T08%3A30%3A15Z";
        var scoped = Read($"?tab=providers&providerId={Provider}&providerSection=history&range=24h&to={to}&outcome=failed");

        Assert.NotNull(scoped.History);
        Assert.Null(scoped.History.ProviderId);
        Assert.Equal($"?tab=providers&providerId={Provider}&providerSection=history&range=24h&to={to}&outcome=failed", Codec.Format(scoped));
        Assert.Null(Read($"?tab=providers&providerId={Provider}&providerSection=prices&range=24h&to={to}").History);
    }

    [Fact]
    public void Every_owned_key_has_a_query_trigger_on_the_page() {
        Assert.Empty(UrlStateTriggers.Missing(typeof(AgentsHomePage), Codec.Keys));
    }

    private static AgentsUrlState Read(string query) => Codec.Read(UrlQuery.Parse(query));
}
