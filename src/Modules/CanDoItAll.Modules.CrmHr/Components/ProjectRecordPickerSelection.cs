namespace CanDoItAll.Modules.CrmHr.Components;

public readonly record struct ProjectRecordPickerSelection(Guid ProjectId, Guid? LifetimeId) {
    public bool Equals(ProjectRecordPickerSelection other) => ProjectId == other.ProjectId;

    public override int GetHashCode() => ProjectId.GetHashCode();
}
