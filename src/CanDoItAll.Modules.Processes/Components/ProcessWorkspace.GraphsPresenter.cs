namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    public sealed class ProcessWorkspaceGraphsTabPresenter
    {
        private readonly ProcessWorkspace workspace;

        internal ProcessWorkspaceGraphsTabPresenter(ProcessWorkspace workspace)
        {
            this.workspace = workspace;
        }

        public bool HasSelectedProcess => workspace.selectedProcessId.HasValue;

        public IReadOnlyList<ProcessLiveHistoryWindow> RangeOptions => ProcessGraphWindows;

        public ProcessLiveHistoryWindow SelectedWindow
        {
            get => workspace.processGraphsWindow;
            set => workspace.SetProcessGraphsWindow(value);
        }

        public bool HasRequestedLoad => workspace.processGraphsLoadRequested;

        public bool IsLoading => workspace.processGraphsLoading;

        public string Error => workspace.processGraphsError;

        public ProcessLiveObservationSnapshot? Snapshot => workspace.processGraphsSnapshot;

        public Task LoadAllRunsGraphsAsync()
        {
            return workspace.LoadProcessGraphsAsync(forceRefresh: true);
        }

        public string ResolveRangeLabel(ProcessLiveHistoryWindow window)
        {
            return ResolveProcessGraphWindowLabel(window);
        }
    }
}
