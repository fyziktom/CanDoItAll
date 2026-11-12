using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Workbench.Insights.UI;

public readonly record struct ManagerReportId(Guid Value);
public readonly record struct ManagerScopeId(Guid Value);

public sealed record ManagerScopePresentation(ManagerScopeId Id, string ProjectName, int ProjectCount,
    int DescendantCount, bool RequiresConfirmation, long? PlanNodeCount, long? PlanLinkCount, IReadOnlyList<string> Warnings);

public sealed record ManagerReportPresentation(ManagerReportId Id, Guid ProjectId, string ProjectName,
    ProjectManagerSummaryOptions Options, DateTimeOffset? HistoryFromUtc, DateTimeOffset AsOfUtc,
    DateTimeOffset GeneratedAtUtc, ProjectManagerTaskSchedule Schedule, ProjectManagerCostTotals Costs,
    IReadOnlyList<ProjectManagerCostBreakdown> CostBreakdown, IReadOnlyList<ProjectManagerCurrencyCostTotal> OtherCurrencyFutureCosts,
    IReadOnlyList<ProjectManagerExpensePoint> ExpenseTrend, IReadOnlyList<ProjectManagerActivity> LatestActivities,
    IReadOnlyList<string> Warnings);

public sealed record ManagerActivityQuery(ProjectManagerActivityKind Kind, ProjectManagerActivityStatusFilter Status, int PageIndex);

public sealed record ManagerActivityPresentation(IReadOnlyList<ProjectManagerActivity> Items, int PageIndex,
    int PageSize, int TotalCount, ProjectManagerCostTotals Totals, long TotalDurationMilliseconds, bool HasNext);

public interface IManagerSummarySource {
    Task<ManagerScopePresentation> ResolveScopeAsync(ProjectManagerSummaryOptions options, CancellationToken cancellationToken);
    Task<ManagerReportPresentation> LoadAsync(ManagerScopeId scope, ProjectManagerSummaryOptions options,
        Func<ProjectManagerSummaryLoadProgress, ValueTask> progress, CancellationToken cancellationToken);
    IManagerActivitySource OpenActivity(ManagerReportId report);
    void Retain(ProjectManagerSummaryOptions options, ManagerReportPresentation? report);
    string DescribeFailure(Exception exception);
}

public interface IManagerActivitySource : IDisposable {
    Task<ManagerActivityPresentation> ReadAsync(ManagerActivityQuery query, CancellationToken cancellationToken);
    string DescribeFailure(Exception exception);
}
