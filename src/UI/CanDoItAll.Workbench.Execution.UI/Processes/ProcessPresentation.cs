using CanDoItAll.Modules.Workbench.Pages;

namespace CanDoItAll.Workbench.Execution.UI.Processes;

public sealed record ProcessLinkView(Guid OpeningId, string Title, string Copy, string SubmitLabel,
    IReadOnlyList<ProjectStructureProcessLinkOption> Options, Guid? SelectedDefinitionId, bool IsBusy,
    bool RequiresObservation, string Error);

public sealed record ProcessLinkActions(Func<Guid, Task> Select, Func<Task> Submit, Func<Task> Close);

public enum ProcessReviewPhase { Unprepared, Prepared, Accepted }

public sealed record ProcessStartView(Guid OpeningId, string Title, string Copy, string SubmitLabel,
    Guid ProjectId, string NodeId, string? ParentNodeId, string TargetNodeId, string TargetNodeTitle,
    Guid? LaunchPlanId, bool IsBusy, bool ConfirmHrManagerMatch, string StatusMessage,
    IReadOnlyList<ProjectStructureProcessStartRoleState> Roles, string HrManagerName, string Error,
    bool EstimateOnlyMode, ProjectStructureProcessEstimateSummary? Estimate, string PreviousLaunchNotice,
    ProcessReviewPhase ReviewPhase, bool CanPrepareAnother) {
    public int ResolvedRoleCount => Roles.Count(role => role.IsResolved);
    public int RequiredGapCount => Roles.Count(role => role.HasBlockingGap);
    public bool IsAccepted => ReviewPhase == ProcessReviewPhase.Accepted;
    public bool HasPreparedAdmission => ReviewPhase != ProcessReviewPhase.Unprepared;
    public string ReviewLabel => ReviewPhase switch {
        ProcessReviewPhase.Accepted => "Open accepted run",
        ProcessReviewPhase.Prepared => "Start reviewed run",
        _ => "Prepare reviewed launch"
    };
}

public sealed record ProcessStartActions(Func<Task> Submit, Func<Task> PrepareAnother, Func<Task> Close);

public sealed record ProcessCandidateChoice(Guid OpeningId, Guid LaunchPlanRoleId, Guid CandidateId,
    Guid? PreviousCandidateId);
