using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.AgentFramework.UI.Overview;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentsOverviewPresentationTests {
    [Fact]
    public void Presentation_copy_owns_collections_and_never_carries_source_error_details() {
        var teams = new List<AgentTeamOverviewShortcutRow> { new(Guid.NewGuid(), "Team", "", "groups", 1) };
        var consumers = new List<ProviderUsageConsumerRow> { new(ProviderUsageConsumerKind.Agent, "id", "Consumer", ProviderUsageTotals.Empty, null) };
        var providers = new List<ProviderUsageProviderRow> { new(null, "Provider", ProviderKind.OpenAi, ProviderUsageTotals.Empty, null) };
        var sources = new List<ProviderUsageSourceStatus> { new("Source", ProviderUsageWorkloadKind.Agent, ProviderUsageSourceState.Partial, DateTimeOffset.UnixEpoch, new("private", "Internal source detail")) };
        var avatars = new Dictionary<string, string?> { ["id"] = "safe-avatar.svg" };
        var state = AgentsOverviewPresentation.Create(AgentOverviewSnapshot.Empty with { TeamShortcuts = teams },
            ProviderUsageSnapshot.Empty(ProviderUsageWorkloadSelection.Both) with { Consumers = consumers, Providers = providers, Sources = sources },
            ProviderUsageWorkloadSelection.Both, false, false, null, null, avatars);
        teams.Clear();
        consumers.Clear();
        providers.Clear();
        sources.Clear();
        avatars.Clear();
        Assert.Single(state.Teams);
        Assert.Single(state.Consumers);
        Assert.Single(state.Providers);
        Assert.Equal(ProviderUsageSourceState.Partial, Assert.Single(state.UsageSources));
        Assert.Equal("safe-avatar.svg", state.Avatars["id"]);
        Assert.True(state.UsagePartial);
    }

    [Fact]
    public void Mismatched_usage_snapshot_preserves_observed_identity_but_omits_its_data() {
        var state = AgentsOverviewPresentation.Create(AgentOverviewSnapshot.Empty,
            ProviderUsageSnapshot.Empty(ProviderUsageWorkloadSelection.Agents), ProviderUsageWorkloadSelection.SimpleChats,
            false, false, null, "Selected scope is unavailable.", new Dictionary<string, string?>());
        Assert.Equal(ProviderUsageWorkloadSelection.Agents, state.AcceptedScope);
        Assert.Null(state.UsageTotals);
        Assert.Empty(state.Consumers);
        Assert.Empty(state.Providers);
        Assert.False(state.HasUsage);
        Assert.True(state.HasOverview);
        Assert.Equal(AgentsOverviewReadPhase.Unavailable, state.UsagePhase);
    }

    [Theory]
    [InlineData(false, true, false, AgentsOverviewReadPhase.Loading)]
    [InlineData(false, false, true, AgentsOverviewReadPhase.Unavailable)]
    [InlineData(true, false, false, AgentsOverviewReadPhase.Ready)]
    [InlineData(true, true, false, AgentsOverviewReadPhase.Refreshing)]
    [InlineData(true, false, true, AgentsOverviewReadPhase.Stale)]
    public void Read_phase_preserves_loading_accepted_and_error_meanings(bool accepted, bool loading, bool failed, AgentsOverviewReadPhase expected) {
        var state = AgentsOverviewPresentation.Create(accepted ? AgentOverviewSnapshot.Empty : null,
            accepted ? ProviderUsageSnapshot.Empty(ProviderUsageWorkloadSelection.Both) : null,
            ProviderUsageWorkloadSelection.Both, loading, loading, failed ? "Unavailable" : null, failed ? "Unavailable" : null,
            new Dictionary<string, string?>());
        Assert.Equal(expected, state.OverviewPhase);
        Assert.Equal(expected, state.UsagePhase);
    }
}
