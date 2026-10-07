using CanDoItAll.Modules.Workbench;
using CanDoItAll.Workbench.Insights.UI;

namespace CanDoItAll.Workbench.Insights.UiSandbox;

public enum ReportScenarioKind { Populated, Empty, Failed, Confirmation, LimitRejected, Delayed }

public sealed class ReportScenario(string name) : IManagerSummarySource {
    private readonly Guid projectId = Guid.NewGuid();
    private readonly List<TaskCompletionSource> held = [];
    public ReportScenarioKind Kind { get; set; }
    public int ReportReads { get; private set; }
    public int ActivityReads { get; private set; }
    public int ScopeReads { get; private set; }

    public Task<ManagerScopePresentation> ResolveScopeAsync(ProjectManagerSummaryOptions options, CancellationToken cancellationToken) {
        ScopeReads++;
        if (Kind == ReportScenarioKind.LimitRejected) {
            throw new InvalidOperationException("The native scope limit was exceeded. Narrow the project scope before retrying.");
        }
        return Task.FromResult(new ManagerScopePresentation(new(Guid.NewGuid()), name,
            Kind == ReportScenarioKind.Confirmation ? 26 : 1, Kind == ReportScenarioKind.Confirmation ? 25 : 0,
            Kind == ReportScenarioKind.Confirmation, 31, 20, ["Confirm the submitted reporting scope."]));
    }

    public async Task<ManagerReportPresentation> LoadAsync(ManagerScopeId scope, ProjectManagerSummaryOptions options,
        Func<ProjectManagerSummaryLoadProgress, ValueTask> progress, CancellationToken cancellationToken) {
        ReportReads++;
        var scenario = Kind;
        await progress(new("History", "Reading the accepted scope", 1, 3));
        if (scenario == ReportScenarioKind.Delayed) {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            held.Add(gate);
            await gate.Task;
            await progress(new("Delayed response", "Original read completed", 3, 3));
        }
        if (scenario == ReportScenarioKind.Failed) {
            throw new InvalidOperationException("The report read failed. The accepted snapshot remains available.");
        }
        var cutoff = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var empty = scenario == ReportScenarioKind.Empty;
        var future = options.ContentMode == ProjectManagerSummaryContentMode.HistoryAndFuture;
        return new(new(Guid.NewGuid()), projectId, name, options,
            options.TimeRange == ProjectManagerSummaryTimeRange.All ? null : cutoff.AddMonths(-1), cutoff, cutoff.AddSeconds(1),
            new(empty ? 0 : 2, empty ? null : cutoff, empty ? null : cutoff.AddHours(6.5), empty ? null : 6.5m, empty ? 0 : 3.1256789m),
            new(empty ? 0 : 1.25m, empty ? 0 : 0.125m, !empty && future ? 12.25m : 0, empty ? 0 : 1),
            empty ? [] : [new(ProjectManagerCostCategory.ChatsAndAgents, 1.25m, 0.125m, 0, 1), new(ProjectManagerCostCategory.Workforce, 0, 0, future ? 12.25m : 0, 0)],
            !empty && future ? [new("EUR", 125.123456789m, 1)] : [],
            empty ? [] : Enumerable.Range(0, 5).Select(index => new ProjectManagerExpensePoint(new DateOnly(2026, 10, index + 1), index == 0 ? 0 : 0.25m, 0.025m, future ? 2.45m : 0)).ToArray(),
            empty ? [] : Enum.GetValues<ProjectManagerActivityKind>().Select(kind => Activity(kind, 0, cutoff)).ToArray(),
            empty ? [] : ["One historical price is unknown; known zero remains zero."]);
    }

    public void Release() {
        foreach (var gate in held) {
            gate.TrySetResult();
        }
        held.Clear();
    }

    public IManagerActivitySource OpenActivity(ManagerReportId report) => new ActivitySource(this);
    public void Retain(ProjectManagerSummaryOptions options, ManagerReportPresentation? report) { }
    public string DescribeFailure(Exception exception) => exception.Message;

    private static ProjectManagerActivity Activity(ProjectManagerActivityKind kind, int index, DateTimeOffset cutoff) => new(
        new(Guid.NewGuid()), kind, $"{kind} run {index + 1}", "Accepted native reporting projection", index % 4 == 0 ? ProjectManagerActivityStatus.Failed : ProjectManagerActivityStatus.Succeeded,
        cutoff.AddMinutes(-index - 1), (index + 1) * 1000, index == 0 ? 0 : 0.0125m, 0.0025m, index == 2, ["WB2", kind.ToString()]);

    private sealed class ActivitySource(ReportScenario owner) : IManagerActivitySource {
        private bool disposed;
        public Task<ManagerActivityPresentation> ReadAsync(ManagerActivityQuery query, CancellationToken cancellationToken) {
            ObjectDisposedException.ThrowIf(disposed, this);
            owner.ActivityReads++;
            var cutoff = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
            var items = Enumerable.Range(0, owner.Kind == ReportScenarioKind.Empty ? 0 : 43).Select(index => Activity(query.Kind, index, cutoff))
                .Where(item => query.Status switch {
                    ProjectManagerActivityStatusFilter.All => true,
                    ProjectManagerActivityStatusFilter.Failed => item.Status == ProjectManagerActivityStatus.Failed,
                    ProjectManagerActivityStatusFilter.Succeeded => item.Status == ProjectManagerActivityStatus.Succeeded,
                    _ => false
                }).ToArray();
            return Task.FromResult(new ManagerActivityPresentation(items.Skip(query.PageIndex * ManagerActivitySession.PageSize).Take(ManagerActivitySession.PageSize).ToArray(),
                query.PageIndex, ManagerActivitySession.PageSize, items.Length,
                new(items.Sum(item => item.KnownCostUsd), items.Sum(item => item.EstimatedCostUsd), 0, items.Count(item => item.HasUnknownCost)),
                items.Sum(item => item.DurationMilliseconds), (query.PageIndex + 1) * ManagerActivitySession.PageSize < items.Length));
        }
        public string DescribeFailure(Exception exception) => exception.Message;
        public void Dispose() => disposed = true;
    }
}
