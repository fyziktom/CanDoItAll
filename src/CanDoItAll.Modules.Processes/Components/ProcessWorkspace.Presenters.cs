using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    private ProcessWorkspaceStepsTabPresenter StepsTabPresenter => new(this);

    private ProcessWorkspaceRunsTabPresenter RunsTabPresenter => new(this);
}
