namespace CanDoItAll.AgentFramework.Models;

public sealed record AgentOverviewSnapshot(
    AgentOverviewTotals Totals,
    IReadOnlyList<AgentOverviewUsageRow> TopAgents,
    IReadOnlyList<AgentOverviewUsageRow> TopFailingAgents,
    IReadOnlyList<ProviderOverviewUsageRow> ProviderUsage,
    IReadOnlyList<ModelOverviewUsageRow> ModelUsage,
    IReadOnlyList<AgentTeamOverviewShortcutRow> TeamShortcuts,
    DateTimeOffset UpdatedAtUtc,
    ExecutionBoundaryDescriptor ToolExecutionBoundary)
{
    public static AgentOverviewSnapshot Empty { get; } = new(
        AgentOverviewTotals.Empty,
        [],
        [],
        [],
        [],
        [],
        DateTimeOffset.UnixEpoch,
        ExecutionBoundaryDescriptor.Unknown);
}

public sealed record AgentOverviewTotals(
    int AgentCount,
    int TemplateCount,
    int TeamCount,
    int ProviderCount,
    int CapabilityCount,
    int SessionCount,
    int MemoryCount,
    int ActiveRuns,
    int FailedRuns,
    int UsageObservationCount,
    int KnownUsageObservationCount,
    int UnknownUsageObservationCount,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    int ReasoningTokens,
    int TotalTokens,
    decimal KnownCostUsd)
{
    public static AgentOverviewTotals Empty { get; } = new(
        AgentCount: 0,
        TemplateCount: 0,
        TeamCount: 0,
        ProviderCount: 0,
        CapabilityCount: 0,
        SessionCount: 0,
        MemoryCount: 0,
        ActiveRuns: 0,
        FailedRuns: 0,
        UsageObservationCount: 0,
        KnownUsageObservationCount: 0,
        UnknownUsageObservationCount: 0,
        InputTokens: 0,
        CachedInputTokens: 0,
        OutputTokens: 0,
        ReasoningTokens: 0,
        TotalTokens: 0,
        KnownCostUsd: 0m);
}

public sealed record AgentOverviewUsageRow(
    Guid AgentId,
    string AgentName,
    string? AvatarImageUrl,
    int RunCount,
    int FailedRunCount,
    int UsageObservationCount,
    int KnownUsageObservationCount,
    int UnknownUsageObservationCount,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    int ReasoningTokens,
    int TotalTokens,
    decimal KnownCostUsd,
    DateTimeOffset? LastUsedAtUtc);

public sealed record AgentTeamOverviewShortcutRow(
    Guid TeamId,
    string Name,
    string Description,
    string Icon,
    int AgentCount);

public sealed record ProviderOverviewUsageRow(
    string ProviderName,
    ProviderKind ProviderKind,
    int UsageObservationCount,
    int KnownUsageObservationCount,
    int UnknownUsageObservationCount,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    int ReasoningTokens,
    int TotalTokens,
    decimal KnownCostUsd,
    int FailedRunCount,
    DateTimeOffset? LastUsedAtUtc);

public sealed record ModelOverviewUsageRow(
    string ProviderName,
    ProviderKind ProviderKind,
    string Model,
    int UsageObservationCount,
    int KnownUsageObservationCount,
    int UnknownUsageObservationCount,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    int ReasoningTokens,
    int TotalTokens,
    decimal KnownCostUsd,
    DateTimeOffset? LastUsedAtUtc);

public sealed record AgentUsageDetailSnapshot(
    IReadOnlyList<AgentOverviewUsageRow> Agents,
    AgentOverviewTotals Totals,
    DateTimeOffset UpdatedAtUtc)
{
    public static AgentUsageDetailSnapshot Empty { get; } = new([], AgentOverviewTotals.Empty, DateTimeOffset.UnixEpoch);
}

public sealed record ProviderUsageDetailSnapshot(
    IReadOnlyList<ProviderOverviewUsageRow> Providers,
    AgentOverviewTotals Totals,
    DateTimeOffset UpdatedAtUtc)
{
    public static ProviderUsageDetailSnapshot Empty { get; } = new([], AgentOverviewTotals.Empty, DateTimeOffset.UnixEpoch);
}

public sealed record ModelUsageDetailSnapshot(
    IReadOnlyList<ModelOverviewUsageRow> Models,
    AgentOverviewTotals Totals,
    DateTimeOffset UpdatedAtUtc)
{
    public static ModelUsageDetailSnapshot Empty { get; } = new([], AgentOverviewTotals.Empty, DateTimeOffset.UnixEpoch);
}

public sealed record AgentUsageProjection(
    string Version,
    long Revision,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<AgentUsageProjectionRow> Agents,
    IReadOnlyList<ProviderUsageProjectionRow> Providers,
    IReadOnlyList<ModelUsageProjectionRow> Models)
{
    public static AgentUsageProjection Empty { get; } = new(
        Version: "1.0",
        Revision: 0L,
        UpdatedAtUtc: DateTimeOffset.UnixEpoch,
        Agents: [],
        Providers: [],
        Models: []);

    public int UsageObservationCount => Providers.Sum(item => item.UsageObservationCount);

    public int KnownUsageObservationCount => Providers.Sum(item => item.KnownUsageObservationCount);

    public int UnknownUsageObservationCount => Providers.Sum(item => item.UnknownUsageObservationCount);

    public int InputTokens => Providers.Sum(item => item.InputTokens);

    public int CachedInputTokens => Providers.Sum(item => item.CachedInputTokens);

    public int OutputTokens => Providers.Sum(item => item.OutputTokens);

    public int ReasoningTokens => Providers.Sum(item => item.ReasoningTokens);

    public int TotalTokens => Providers.Sum(item => item.TotalTokens);

    public decimal KnownCostUsd => Providers.Sum(item => item.KnownCostUsd);
}

public sealed record AgentUsageProjectionRow(
    Guid AgentId,
    int RunCount,
    int FailedRunCount,
    int UsageObservationCount,
    int KnownUsageObservationCount,
    int UnknownUsageObservationCount,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    int ReasoningTokens,
    int TotalTokens,
    decimal KnownCostUsd,
    DateTimeOffset? LastUsedAtUtc);

public sealed record ProviderUsageProjectionRow(
    string ProviderName,
    ProviderKind ProviderKind,
    int UsageObservationCount,
    int KnownUsageObservationCount,
    int UnknownUsageObservationCount,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    int ReasoningTokens,
    int TotalTokens,
    decimal KnownCostUsd,
    int FailedRunCount,
    DateTimeOffset? LastUsedAtUtc);

public sealed record ModelUsageProjectionRow(
    string ProviderName,
    ProviderKind ProviderKind,
    string Model,
    int UsageObservationCount,
    int KnownUsageObservationCount,
    int UnknownUsageObservationCount,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    int ReasoningTokens,
    int TotalTokens,
    decimal KnownCostUsd,
    DateTimeOffset? LastUsedAtUtc);
