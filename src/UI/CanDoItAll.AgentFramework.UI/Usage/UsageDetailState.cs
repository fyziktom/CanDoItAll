using CanDoItAll.AgentFramework.Usage;

namespace CanDoItAll.AgentFramework.UI.Usage;

public sealed record UsageDetailState(Guid Origin, ProviderUsageQuery Query) {
    public ProviderUsageSnapshot? Snapshot { get; init; }
    public bool Loading { get; init; } = true;
    public bool Retired { get; init; }
    public string? Error { get; init; }
}

public enum UsageDetailAction { Close, Retry }

public sealed record UsageDetailIntent(Guid Origin, ProviderUsageQuery Query, UsageDetailAction Action);
