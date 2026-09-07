using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;

namespace CanDoItAll.AgentFramework.UI.Overview;

public enum AgentsOverviewReadPhase { Loading, Unavailable, Ready, Refreshing, Stale }
public enum AgentsOverviewDetail { Consumers, Providers, Models }

public sealed record AgentsOverviewState {
    public required ProviderUsageWorkloadSelection DesiredScope { get; init; }
    public ProviderUsageWorkloadSelection? AcceptedScope { get; init; }
    public AgentOverviewTotals? Totals { get; init; }
    public ProviderUsageTotals? UsageTotals { get; init; }
    public ImmutableArray<ProviderUsageConsumerRow> Consumers { get; init; } = [];
    public ImmutableArray<ProviderUsageProviderRow> Providers { get; init; } = [];
    public ImmutableArray<AgentTeamOverviewShortcutRow> Teams { get; init; } = [];
    public ImmutableArray<ProviderUsageSourceState> UsageSources { get; init; } = [];
    public ImmutableDictionary<string, string?> Avatars { get; init; } = ImmutableDictionary<string, string?>.Empty;
    public AgentsOverviewReadPhase OverviewPhase { get; init; }
    public AgentsOverviewReadPhase UsagePhase { get; init; }
    public string? OverviewError { get; init; }
    public string? UsageError { get; init; }
    public string? HeaderWarning { get; init; }
    public bool HeaderLoading { get; init; }
    public ImmutableHashSet<AgentsOverviewDetail> OpenDetails { get; init; } = [];
    public bool HasOverview => Totals is not null;
    public bool HasUsage => AcceptedScope == DesiredScope && UsageTotals is not null;
    public bool OverviewLoading => OverviewPhase is AgentsOverviewReadPhase.Loading or AgentsOverviewReadPhase.Refreshing;
    public bool UsageLoading => UsagePhase is AgentsOverviewReadPhase.Loading or AgentsOverviewReadPhase.Refreshing;
    public bool UsagePartial => HasUsage && UsageSources.Any(source => source != ProviderUsageSourceState.Complete);
    public bool CanOpen(AgentsOverviewDetail detail) => HasUsage && !UsageLoading && !OpenDetails.Contains(detail);
}

public abstract record AgentsOverviewIntent {
    public sealed record SelectUsage(ProviderUsageWorkloadSelection Selection) : AgentsOverviewIntent;
    public sealed record RetryOverview : AgentsOverviewIntent;
    public sealed record RetryUsage : AgentsOverviewIntent;
    public sealed record RetryHeader : AgentsOverviewIntent;
    public sealed record OpenDetail(AgentsOverviewDetail Detail, ProviderUsageWorkloadSelection Selection) : AgentsOverviewIntent;
    public sealed record OpenTeam(Guid TeamId) : AgentsOverviewIntent;
}
