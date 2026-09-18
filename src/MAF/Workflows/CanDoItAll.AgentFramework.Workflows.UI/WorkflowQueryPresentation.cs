using System.Collections.Immutable;

namespace CanDoItAll.AgentFramework.Workflows.UI;

public enum WorkflowQueryState { Inactive, Loading, Failed, Ready }
public enum WorkflowAnalyticsScopeKind { All, SelectedWorkflow }
public enum WorkflowQueryAction { Refresh, Scope, Workflow }
public sealed record WorkflowQueryIntent(long Revision, WorkflowQueryAction Action,
    WorkflowAnalyticsScopeKind Scope = WorkflowAnalyticsScopeKind.All, Guid? WorkflowId = null);
public sealed record WorkflowChoice(Guid Id, string Name);
public sealed record WorkflowChartPointView(string Label, int Count);
public sealed record WorkflowRankedView(string Name, string Status, string Tone, string LastRun, string RunCount, string FailureCount);
public sealed record WorkflowActivityView(string Name, string Summary, string UpdatedAt, string Status, string Tone, string Backend = "");
public sealed record WorkflowDistributionView(string Dimension, string Label, string Count, string Tone);
public sealed record WorkflowProviderUsageView(string ProviderName, string Model, string? ProviderKind,
    string Observations, string Tokens, string Cost, string UnknownPricing);
public sealed record WorkflowOverviewPresentation {
    public long Revision { get; init; }
    public WorkflowQueryState State { get; init; }
    public string Error { get; init; } = "";
    public string AsOf { get; init; } = "";
    public ImmutableArray<WorkflowMetric> Metrics { get; init; } = [];
    public ImmutableArray<WorkflowRankedView> TopWorkflows { get; init; } = [];
    public ImmutableArray<WorkflowActivityView> RecentRuns { get; init; } = [];
    public ImmutableArray<WorkflowActivityView> RecentlyUpdatedDefinitions { get; init; } = [];
    public ImmutableArray<WorkflowBadge> Lifecycle { get; init; } = [];
    public ImmutableArray<WorkflowChartPointView> RunStates { get; init; } = [];
    public ImmutableArray<WorkflowChartPointView> Backends { get; init; } = [];
}
public sealed record WorkflowAnalyticsPresentation {
    public long Revision { get; init; }
    public WorkflowQueryState State { get; init; }
    public string Error { get; init; } = "";
    public string AsOf { get; init; } = "";
    public WorkflowAnalyticsScopeKind Scope { get; init; }
    public Guid? SelectedWorkflow { get; init; }
    public string ScopeDescription { get; init; } = "";
    public ImmutableArray<WorkflowChoice> Workflows { get; init; } = [];
    public ImmutableArray<WorkflowMetric> RuntimeMetrics { get; init; } = [];
    public ImmutableArray<WorkflowMetric> UsageMetrics { get; init; } = [];
    public ImmutableArray<WorkflowMetric> DurationMetrics { get; init; } = [];
    public ImmutableArray<WorkflowDistributionView> Distribution { get; init; } = [];
    public ImmutableArray<WorkflowProviderUsageView> Providers { get; init; } = [];
    public ImmutableArray<WorkflowRunView> RecentRuns { get; init; } = [];
}
