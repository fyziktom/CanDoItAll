using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Workbench.Execution.UI.Processes;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureProcessPresentation {
    internal static ProcessLinkView Link(ProjectStructureProcessLinkDialogState state)
        => new(state.DialogId, state.Title, state.Copy, state.SubmitLabel, state.Options,
            state.SelectedDefinitionId, state.IsBusy, state.RequiresObservation, state.Error);

    internal static ProcessStartView Start(ProjectStructureProcessStartDialogState state)
        => new(state.DialogId, state.Title, state.Copy, state.SubmitLabel, state.ProjectId, state.NodeId,
            state.ParentNodeId, state.TargetNodeId, state.TargetNodeTitle, state.LaunchPlanId, state.IsBusy,
            state.ConfirmHrManagerMatch, state.StatusMessage, state.Roles, state.HrManagerName, state.Error,
            state.EstimateOnlyMode, state.Estimate, state.PreviousLaunchNotice,
            state.IsAccepted ? ProcessReviewPhase.Accepted : state.PreparedRequest?.PreparedAdmissionId is not null
                ? ProcessReviewPhase.Prepared : ProcessReviewPhase.Unprepared,
            state.PreparedRequest is not null || !string.IsNullOrWhiteSpace(state.Error));
}
