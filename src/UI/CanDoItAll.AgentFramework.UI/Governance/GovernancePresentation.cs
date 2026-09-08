using System.Collections.Immutable;

namespace CanDoItAll.AgentFramework.UI.Governance;

public enum GovernanceReadPhase { Idle, Loading, Refreshing, Ready, Stale, Failed, Unavailable }
public enum GovernanceTone { Neutral, Info, Success, Warning, Danger }

public sealed record GovernanceLaneState(GovernanceReadPhase Phase, string? Error = null) {
    public static GovernanceLaneState Idle { get; } = new(GovernanceReadPhase.Idle);
    public static GovernanceLaneState Ready { get; } = new(GovernanceReadPhase.Ready);
    public bool IsBusy => Phase is GovernanceReadPhase.Loading or GovernanceReadPhase.Refreshing;
}

public sealed record GovernanceViewState(
    Guid? DesiredAgentId,
    Guid? AcceptedAgentId,
    bool AgentResolved,
    Guid? DesiredRunId,
    Guid? AcceptedRunId,
    GovernanceLaneState Catalog,
    GovernanceLaneState List,
    GovernanceLaneState Detail) {
    public static GovernanceViewState Initial { get; } = new(null, null, false, null, null,
        new(GovernanceReadPhase.Loading), GovernanceLaneState.Idle, GovernanceLaneState.Idle);
    public bool IsBusy => Catalog.IsBusy || List.IsBusy || Detail.IsBusy;
}

public sealed record GovernanceAgentOption(Guid Id, string Label);

public sealed record GovernanceEntry(
    string Title,
    string Eyebrow,
    string Timestamp,
    string Summary,
    string Status = "",
    GovernanceTone Tone = GovernanceTone.Neutral);

public sealed record GovernanceRunPresentation(
    Guid Id,
    Guid AgentId,
    string Title,
    string AgentLabel,
    string State,
    string? Outcome,
    GovernanceTone Tone,
    string Updated,
    string Provider,
    string Model,
    string Summary,
    string SourceKind,
    string SourceIdentity,
    string ProcessIdentity,
    string StepIdentity,
    bool ProcessLinked);

public sealed record ExecutionMetricTotals(int Runs, int Succeeded, int Failed,
    decimal InputTokens, decimal OutputTokens, decimal ToolCalls);

public sealed record ExecutionMetricsPresentation(ExecutionMetricTotals Totals, ImmutableArray<GovernanceEntry> Rows) {
    public static ExecutionMetricsPresentation Empty { get; } = new(new(0, 0, 0, 0, 0, 0), []);
}

public sealed record GovernanceDetailPresentation(
    GovernanceRunPresentation Run,
    int ApprovalCount,
    int ArtifactCount,
    int CheckpointCount,
    int ReceiptCount,
    ImmutableArray<GovernanceEntry> Approvals,
    ImmutableArray<GovernanceEntry> Artifacts,
    ImmutableArray<GovernanceEntry> Checkpoints,
    ImmutableArray<GovernanceEntry> Receipts,
    ImmutableArray<GovernanceEntry> Timeline,
    ExecutionMetricsPresentation Metrics);

public sealed record GovernancePresentation(
    ImmutableArray<GovernanceAgentOption> Agents,
    ImmutableArray<GovernanceRunPresentation> Runs,
    GovernanceDetailPresentation? Detail) {
    public static GovernancePresentation Empty { get; } = new([], [], null);
}

public abstract record GovernanceIntent {
    public sealed record SelectAgent(Guid? AgentId) : GovernanceIntent;
    public sealed record SelectRun(Guid RunId) : GovernanceIntent;
    public sealed record Refresh : GovernanceIntent;
    public sealed record RetryCatalog : GovernanceIntent;
    public sealed record RetryList : GovernanceIntent;
    public sealed record RetryDetail : GovernanceIntent;
}
