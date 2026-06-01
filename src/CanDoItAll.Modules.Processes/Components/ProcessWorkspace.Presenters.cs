using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    private ProcessWorkspaceStepsTabPresenter StepsTabPresenter => new(this);

    private ProcessWorkspaceRunsTabPresenter RunsTabPresenter => new(this);

    private ProcessWorkspaceGraphsTabPresenter GraphsTabPresenter => new(this);

    private ProcessWorkspaceAnalyticsTabPresenter AnalyticsTabPresenter => new(this);
}
