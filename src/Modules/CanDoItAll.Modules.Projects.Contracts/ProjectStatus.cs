namespace CanDoItAll.Modules.Projects;

/// <summary>
/// Lifecycle status of a project, as a JSON integer: 0 Draft, 1 Active, 2 OnHold, 3 Completed, 4 Archived. The status
/// is a label chosen when the project is saved: it filters and labels projects but does not lock a project against
/// changes or delete anything.
/// </summary>
public enum ProjectStatus
{
    Draft,
    Active,
    OnHold,
    Completed,
    Archived
}
