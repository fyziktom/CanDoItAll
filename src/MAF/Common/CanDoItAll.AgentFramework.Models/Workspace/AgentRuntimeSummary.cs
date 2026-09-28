namespace CanDoItAll.AgentFramework.Models;

public sealed record AgentRuntimeTotals(int AgentCount, int TemplateCount, int TeamCount,
    int ProviderCount, int CapabilityCount, int SessionCount, int MemoryCount, int ActiveRuns, int FailedRuns) {
    public static AgentRuntimeTotals Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
}

public sealed record AgentRuntimeSummary(AgentRuntimeTotals Totals, IReadOnlyList<AgentTeamOverviewShortcutRow> TeamShortcuts) {
    public static AgentRuntimeSummary Empty { get; } = new(AgentRuntimeTotals.Empty, []);
}
