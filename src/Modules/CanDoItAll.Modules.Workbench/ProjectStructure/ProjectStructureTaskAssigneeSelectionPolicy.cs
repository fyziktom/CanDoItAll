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

    public static IReadOnlyList<ProjectStructureTaskResourceOption>
        IncludeRepresentativeOption(
            IReadOnlyList<ProjectStructureTaskResourceOption> options,
            ProjectStructureTaskAssigneeSelectionResult resolution)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(resolution);
        var representative = resolution.Representative;
        if (representative is null || options.Any(option =>
                option.Kind == representative.Kind &&
                option.ResourceId == representative.ResourceId))
        {
            return options;
        }

        var assignment = resolution.DirectAssignments.FirstOrDefault(item =>
            item.PartyId == representative.ResourceId &&
            IsCompatibleAssigneeType(item.PartyType, representative.Kind));
        if (assignment is null)
        {
            return options;
        }

        return options
            .Append(new ProjectStructureTaskResourceOption(
                representative.Kind,
                representative.ResourceId,
                VersionId: null,
                assignment.PartyDisplayName,
                assignment.PartyTypeLabel,
                string.Empty,
                IsFavorite: false,
                IsSensitive: false))
            .OrderBy(static option => option.Kind)
            .ThenBy(
                static option => option.DisplayName,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(static option => option.ResourceId)
            .ToArray();
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

    private static bool IsCompatibleAssigneeType(
        ProjectPartyType partyType,
        ProjectStructureTaskResourceKind resourceKind)
        => (partyType, resourceKind) is
            (ProjectPartyType.Person, ProjectStructureTaskResourceKind.Person) or
            (ProjectPartyType.AiAgent, ProjectStructureTaskResourceKind.Agent);
}
