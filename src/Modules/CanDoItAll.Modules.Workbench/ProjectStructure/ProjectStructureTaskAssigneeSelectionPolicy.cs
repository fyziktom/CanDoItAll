using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

public enum ProjectStructureTaskAssigneeSelectionStatus
{
    None,
    Single,
    MultipleWithPrimary,
    Ambiguous,
    UnsupportedPartyType
}

public sealed record ProjectStructureTaskAssigneeSelectionResult(
    ProjectStructureTaskAssigneeSelectionStatus Status,
    ProjectStructureTaskResourceSelection? Representative,
    IReadOnlyList<ProjectPartyAssignmentDetail> DirectAssignments)
{
    public bool CanChangeDirectAssignee =>
        Status is ProjectStructureTaskAssigneeSelectionStatus.None or
            ProjectStructureTaskAssigneeSelectionStatus.Single;
}

public static class ProjectStructureTaskAssigneeSelectionPolicy
{
    public static bool HasSameDirectAssignments(
        IReadOnlyCollection<ProjectPartyAssignmentDetail> expected,
        IReadOnlyCollection<ProjectPartyAssignmentDetail> current)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(current);
        return expected.Count == current.Count &&
            expected.Select(ProjectPartyAssignmentConcurrencySnapshot.From)
                .ToHashSet()
                .SetEquals(current.Select(
                    ProjectPartyAssignmentConcurrencySnapshot.From));
    }

    public static ProjectStructureTaskAssigneeSelectionResult Resolve(
        IEnumerable<ProjectPartyAssignmentDetail> assignments,
        string taskNodeId)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskNodeId);

        var directAssignments = assignments
            .Where(assignment =>
                assignment.Role == ProjectPartyAssignmentRole.WorkItemAssignee &&
                string.Equals(assignment.NodeKey, taskNodeId, StringComparison.Ordinal))
            .ToArray();
        if (directAssignments.Length == 0)
        {
            return new(
                ProjectStructureTaskAssigneeSelectionStatus.None,
                null,
                directAssignments);
        }

        if (directAssignments.Length == 1)
        {
            var representative = ToSelection(directAssignments[0]);
            return new(
                representative is null
                    ? ProjectStructureTaskAssigneeSelectionStatus.UnsupportedPartyType
                    : ProjectStructureTaskAssigneeSelectionStatus.Single,
                representative,
                directAssignments);
        }

        var primaryAssignments = directAssignments
            .Where(static assignment => assignment.IsPrimary)
            .ToArray();
        if (primaryAssignments.Length != 1)
        {
            return new(
                ProjectStructureTaskAssigneeSelectionStatus.Ambiguous,
                null,
                directAssignments);
        }

        var primaryRepresentative = ToSelection(primaryAssignments[0]);
        return new(
            primaryRepresentative is null
                ? ProjectStructureTaskAssigneeSelectionStatus.UnsupportedPartyType
                : ProjectStructureTaskAssigneeSelectionStatus.MultipleWithPrimary,
            primaryRepresentative,
            directAssignments);
    }

    private static ProjectStructureTaskResourceSelection? ToSelection(
        ProjectPartyAssignmentDetail assignment)
    {
        var kind = assignment.PartyType switch
        {
            ProjectPartyType.Person => ProjectStructureTaskResourceKind.Person,
            ProjectPartyType.AiAgent => ProjectStructureTaskResourceKind.Agent,
            _ => (ProjectStructureTaskResourceKind?)null
        };
        return kind.HasValue
            ? new ProjectStructureTaskResourceSelection(kind.Value, assignment.PartyId)
            : null;
    }
}
