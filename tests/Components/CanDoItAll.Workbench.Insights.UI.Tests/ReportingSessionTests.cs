using CanDoItAll.Modules.Workbench;
using CanDoItAll.Workbench.Insights.UI;

namespace CanDoItAll.Workbench.Insights.UI.Tests;

public sealed class ReportingSessionTests {
    [Fact]
    public async Task Drafts_are_lazy_and_confirmation_cannot_cross_options_or_views() {
        var source = new ControlledReports { Confirm = true };
        using var first = new ManagerSummarySession(source);
        using var second = new ManagerSummarySession(new ControlledReports());
        var original = first.Options;
        first.UpdateOptions(original, original with { Scope = ProjectManagerSummaryScope.ProjectAndDescendants });
        Assert.Empty(source.Reads);
        Assert.Equal(ProjectManagerSummaryScope.CurrentProject, second.Options.Scope);
        await first.LoadAsync(first.Options);
        var confirmation = Assert.IsType<ManagerScopePresentation>(first.PendingScope);
        first.UpdateOptions(first.Options, first.Options with { TimeRange = ProjectManagerSummaryTimeRange.Year });
        await first.ContinueAsync(confirmation);
        await second.ContinueAsync(confirmation);
        Assert.Empty(source.Reads);
        Assert.Null(first.PendingScope);
        await first.LoadAsync(first.Options);
        var read = first.ContinueAsync(first.PendingScope!);
        Assert.Single(source.Reads);
        source.Reads[0].Succeed();
        await read;
        Assert.Equal(ProjectManagerSummaryTimeRange.Year, first.Snapshot!.Options.TimeRange);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Superseded_read_cannot_publish_progress_result_error_or_finally(bool fail) {
        var source = new ControlledReports();
        using var view = new ManagerSummarySession(source);
        var first = view.LoadAsync(view.Options);
        var old = source.Reads[0];
        var second = view.LoadAsync(view.Options);
        var current = source.Reads[1];
        Assert.True(old.Token.IsCancellationRequested);
        using var registration = old.Token.Register(() => { });
        var progress = view.Progress;
        await old.Progress(new("Retired progress", "Must stay invisible", 3, 3));
        Assert.Same(progress, view.Progress);
        if (fail) {
            old.Completion.SetException(new InvalidOperationException("Retired failure"));
        } else {
            old.Succeed();
        }
        await first;
        Assert.True(view.IsLoading);
        Assert.Null(view.Snapshot);
        Assert.Null(view.Error);
        current.Succeed();
        await second;
        Assert.False(view.IsLoading);
        Assert.Same(await current.Completion.Task, view.Snapshot);
        Assert.Equal(0, source.Failures);
    }

    [Fact]
    public async Task Accepted_report_survives_draft_changes_and_failed_reload() {
        var source = new ControlledReports();
        using var view = new ManagerSummarySession(source);
        var first = view.LoadAsync(view.Options);
        source.Reads[0].Succeed();
        await first;
        var accepted = view.Snapshot!;
        view.UpdateOptions(view.Options, view.Options with { TimeRange = ProjectManagerSummaryTimeRange.Day });
        Assert.Same(accepted, view.Snapshot);
        Assert.Equal(ProjectManagerSummaryTimeRange.Month, accepted.Options.TimeRange);
        var retry = view.LoadAsync(view.Options);
        source.Reads[1].Completion.SetException(new InvalidOperationException("Current failure"));
        await retry;
        Assert.Same(accepted, view.Snapshot);
        Assert.Equal("Current failure", view.Error);
        Assert.False(view.IsLoading);
    }

    [Fact]
    public async Task Closing_and_reopening_activity_ignores_old_completion_and_close_callback() {
        var source = new ControlledReports();
        using var view = new ManagerSummarySession(source);
        var load = view.LoadAsync(view.Options);
        source.Reads[0].Succeed();
        await load;
        var opening = view.OpenActivityAsync(view.Snapshot!);
        var old = view.Activity!;
        view.CloseActivity(old);
        var reopening = view.OpenActivityAsync(view.Snapshot!);
        var current = view.Activity!;
        view.CloseActivity(old);
        Assert.Same(current, view.Activity);
        source.ActivityReads[0].Completion.SetException(new InvalidOperationException("Closed opening"));
        await opening;
        Assert.True(current.IsLoading);
        source.ActivityReads[1].Succeed();
        await reopening;
        Assert.Null(current.Error);
        Assert.NotNull(current.Page);
    }

    [Theory]
    [InlineData(ProjectManagerActivityKind.Conversation)]
    [InlineData(ProjectManagerActivityKind.SimpleChat)]
    [InlineData(ProjectManagerActivityKind.Workflow)]
    [InlineData(ProjectManagerActivityKind.Process)]
    public async Task Activity_paging_uses_all_match_totals_and_old_callbacks_cannot_retarget(ProjectManagerActivityKind kind) {
        var source = new ControlledActivity();
        using var view = new ManagerActivitySession(source);
        var load = view.SetKindAsync(view.Query, kind);
        source.Reads[0].Succeed();
        await load;
        var firstQuery = view.Query;
        var next = view.NextAsync(firstQuery);
        Assert.Equal(1, source.Reads[1].Query.PageIndex);
        source.Reads[1].Succeed();
        await next;
        Assert.Equal(41, view.Page!.TotalCount);
        Assert.Equal(12.3456789m, view.Page.Totals.HistoricalKnownUsd);
        Assert.Equal(42000, view.Page.TotalDurationMilliseconds);
        var status = view.SetStatusAsync(view.Query, ProjectManagerActivityStatusFilter.Failed);
        await view.NextAsync(firstQuery);
        Assert.Equal(3, source.Reads.Count);
        Assert.Equal(0, source.Reads[2].Query.PageIndex);
        source.Reads[2].Succeed();
        await status;
        Assert.Equal(kind, view.Query.Kind);
        Assert.Equal(ProjectManagerActivityStatusFilter.Failed, view.Query.Status);
    }

    [Fact]
    public async Task Retired_A_B_A_view_cannot_change_new_view_with_reused_identity() {
        var source = new ControlledReports();
        var old = new ManagerSummarySession(source);
        var pending = old.LoadAsync(old.Options);
        old.Dispose();
        using var replacement = new ManagerSummarySession(new ControlledReports());
        source.Reads[0].Succeed();
        await pending;
        await old.LoadAsync(old.Options);
        old.UpdateOptions(old.Options, old.Options with { Scope = ProjectManagerSummaryScope.UncategorizedAgentActivity });
        Assert.Single(source.Reads);
        Assert.Null(old.Snapshot);
        Assert.Null(replacement.Snapshot);
        Assert.Equal(ProjectManagerSummaryScope.CurrentProject, replacement.Options.Scope);
    }

    internal sealed class ControlledReports : IManagerSummarySource {
        internal List<ReportRead> Reads { get; } = [];
        internal List<ActivityRead> ActivityReads { get; } = [];
        internal bool Confirm { get; init; }
        internal int Failures { get; private set; }
        public Task<ManagerScopePresentation> ResolveScopeAsync(ProjectManagerSummaryOptions options, CancellationToken cancellationToken)
            => Task.FromResult(new ManagerScopePresentation(new(Guid.NewGuid()), "Exact original project", 26, 25, Confirm, 31, 20, []));
        public Task<ManagerReportPresentation> LoadAsync(ManagerScopeId scope, ProjectManagerSummaryOptions options,
            Func<ProjectManagerSummaryLoadProgress, ValueTask> progress, CancellationToken cancellationToken) {
            var read = new ReportRead(options, progress, cancellationToken);
            Reads.Add(read);
            return read.Completion.Task;
        }
        public IManagerActivitySource OpenActivity(ManagerReportId report) => new ControlledActivity(ActivityReads);
        public void Retain(ProjectManagerSummaryOptions options, ManagerReportPresentation? report) { }
        public string DescribeFailure(Exception exception) {
            Failures++;
            return exception.Message;
        }
    }

    internal sealed class ReportRead(ProjectManagerSummaryOptions options, Func<ProjectManagerSummaryLoadProgress, ValueTask> progress, CancellationToken token) {
        internal TaskCompletionSource<ManagerReportPresentation> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Func<ProjectManagerSummaryLoadProgress, ValueTask> Progress { get; } = progress;
        internal CancellationToken Token { get; } = token;
        internal void Succeed() => Completion.SetResult(Report(options));
    }

    internal sealed class ControlledActivity(List<ActivityRead>? reads = null) : IManagerActivitySource {
        internal List<ActivityRead> Reads { get; } = reads ?? [];
        public Task<ManagerActivityPresentation> ReadAsync(ManagerActivityQuery query, CancellationToken cancellationToken) {
            var read = new ActivityRead(query);
            Reads.Add(read);
            return read.Completion.Task;
        }
        public string DescribeFailure(Exception exception) => exception.Message;
        public void Dispose() { }
    }

    internal sealed class ActivityRead(ManagerActivityQuery query) {
        internal ManagerActivityQuery Query { get; } = query;
        internal TaskCompletionSource<ManagerActivityPresentation> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Succeed() => Completion.SetResult(new([], Query.PageIndex, 20, 41, new(12.3456789m, 2, 0, 3), 42000, Query.PageIndex < 2));
    }

    internal static ManagerReportPresentation Report(ProjectManagerSummaryOptions options) {
        var cutoff = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        return new(new(Guid.NewGuid()), Guid.NewGuid(), "Accepted project", options, cutoff.AddMonths(-1), cutoff,
            cutoff.AddSeconds(1), new(1, cutoff, cutoff.AddHours(6.5), 6.5m, 3.1256789m), new(0, 0.25m, 0, 1),
            [new(ProjectManagerCostCategory.ChatsAndAgents, 0, 0.25m, 0, 1)], [new("EUR", 125.123456789m, 1)],
            [new(DateOnly.FromDateTime(cutoff.UtcDateTime), 0, 0.25m, 0)], [], ["One price is unknown."]);
    }
}
