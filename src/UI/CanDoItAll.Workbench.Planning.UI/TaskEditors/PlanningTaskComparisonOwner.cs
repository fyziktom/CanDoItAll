namespace CanDoItAll.Workbench.Planning.UI;

public enum PlanningTaskUnavailableReason { AccessChanged, OriginalDeletedOrReplaced, ProjectLifetimeChanged, OpeningRetired }

public sealed record PlanningTaskComparisonCurrent(Guid Witness, PlanningTaskEditorInput Input,
    bool CanChangeDirectAssignee, bool ScheduleReadOnly);

public sealed record PlanningTaskComparisonState(Guid? Witness, long Revision, bool IsOpen);

public sealed record PlanningTaskLocalPatch(PlanningTaskEditorInput Expected, PlanningTaskEditorInput Result,
    PlanningTaskComparisonCurrent Current);

public interface IPlanningTaskComparisonOwner {
    bool IsActive { get; }
    PlanningTaskUnavailableReason? UnavailableReason { get; }
    long ChangeRevision { get; }
    event Action? Changed;
    Task<PlanningTaskComparisonCurrent> ReadCurrentAsync(CancellationToken cancellationToken);
    Task<bool> VerifyCurrentAsync(Guid witness, CancellationToken cancellationToken);
    bool AcceptCurrent(Guid witness);
}
