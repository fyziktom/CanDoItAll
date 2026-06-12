namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    public sealed class ProcessWorkspaceAnalyticsTabPresenter
    {
        private readonly ProcessWorkspace workspace;

        internal ProcessWorkspaceAnalyticsTabPresenter(ProcessWorkspace workspace)
        {
            this.workspace = workspace;
        }

        public bool HasSelectedProcess => workspace.selectedProcessId.HasValue;

        public ProcessAnalyticsSummary Analytics => workspace.analytics;

        public IReadOnlyList<ProcessRunListItem> Runs => workspace.runs;

        public IReadOnlyList<ProcessRunListItem> FilteredRuns => workspace.FilterRuns(workspace.analyticsRunFilter);

        public IReadOnlyList<ProcessImprovementViewModel> Improvements => workspace.improvements;

        public IReadOnlyList<ProcessImprovementViewModel> FilteredImprovements => workspace.FilterImprovements(workspace.improvementFilter);

        public string RunSearch
        {
            get => workspace.analyticsRunFilter.Search;
            set => workspace.analyticsRunFilter.Search = value ?? string.Empty;
        }

        public ProcessRunStatus? RunStatusFilter
        {
            get => workspace.analyticsRunFilter.Status;
            set => workspace.analyticsRunFilter.Status = value;
        }

        public ProcessOperatingMode? RunOperatingModeFilter
        {
            get => workspace.analyticsRunFilter.OperatingMode;
            set => workspace.analyticsRunFilter.OperatingMode = value;
        }

        public ProcessRunUpdatedTimeFilter RunUpdatedTimeFilter
        {
            get => workspace.analyticsRunFilter.UpdatedTime;
            set => workspace.analyticsRunFilter.UpdatedTime = value;
        }

        public string RunTagFilter
        {
            get => workspace.analyticsRunFilter.Tag;
            set => workspace.analyticsRunFilter.Tag = value ?? string.Empty;
        }

        public string ImprovementSearch
        {
            get => workspace.improvementFilter.Search;
            set => workspace.improvementFilter.Search = value ?? string.Empty;
        }

        public ProcessImprovementStatus? ImprovementStatusFilter
        {
            get => workspace.improvementFilter.Status;
            set => workspace.improvementFilter.Status = value;
        }

        public ProcessImprovementSignalFilter ImprovementSignalFilter
        {
            get => workspace.improvementFilter.Signal;
            set => workspace.improvementFilter.Signal = value;
        }

        public IReadOnlyList<ProcessRunStatus> RunStatusFilterOptions => ProcessRunStatusFilterOptions;

        public IReadOnlyList<ProcessOperatingMode> RunOperatingModeFilterOptions => ProcessOperatingModeFilterOptions;

        public IReadOnlyList<ProcessRunUpdatedTimeFilter> RunUpdatedTimeFilterOptions => ProcessRunUpdatedTimeFilterOptions;

        public IReadOnlyList<ProcessImprovementStatus> ImprovementStatusFilterOptions => ProcessImprovementStatusFilterOptions;

        public IReadOnlyList<ProcessImprovementSignalFilter> ImprovementSignalFilterOptions => ProcessImprovementSignalFilterOptions;

        public string RunResultText => BuildRunFilterResultText(FilteredRuns.Count, Runs.Count);

        public string ImprovementResultText => BuildImprovementFilterResultText(FilteredImprovements.Count, Improvements.Count);

        public void ClearRunFilters()
        {
            workspace.analyticsRunFilter.Clear();
        }

        public void ClearImprovementFilters()
        {
            workspace.improvementFilter.Clear();
        }

        public Task OpenRunStepsDialogAsync(Guid runId)
        {
            return workspace.OpenRunStepsDialogAsync(runId);
        }

        public string BuildRunSummary(ProcessRunListItem run)
        {
            return ProcessWorkspace.BuildRunSummary(run);
        }

        public string BuildRunUpdatedText(ProcessRunListItem run)
        {
            return ProcessWorkspace.BuildRunUpdatedText(run);
        }

        public string BuildRunCostText(ProcessRunListItem run)
        {
            return workspace.BuildRunCostText(run);
        }

        public string FormatCost(decimal value)
        {
            return workspace.CurrencyFormatter.Format(value);
        }

        public IReadOnlyList<ProcessWorkspaceTagViewModel> BuildRunTags(ProcessRunListItem run)
        {
            return ProcessWorkspace.BuildRunTags(run);
        }

        public IReadOnlyList<ProcessWorkspaceTagViewModel> BuildImprovementTags(ProcessImprovementViewModel improvement)
        {
            return ProcessWorkspace.BuildImprovementTags(improvement);
        }

        public string ResolveRunTone(ProcessRunStatus status)
        {
            return ProcessWorkspace.ResolveRunTone(status);
        }

        public string ResolveRunUpdatedTimeFilterText(ProcessRunUpdatedTimeFilter filter)
        {
            return ProcessWorkspace.ResolveRunUpdatedTimeFilterText(filter);
        }

        public string ResolveImprovementSignalFilterText(ProcessImprovementSignalFilter filter)
        {
            return ProcessWorkspace.ResolveImprovementSignalFilterText(filter);
        }
    }
}
