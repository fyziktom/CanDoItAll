using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.CrmHr;

public static class ProjectPartyAssignmentQueryLimits
{
    public const int DefaultPageSize = 12;
    public const int MaximumPageSize = 200;
    public const int SchedulePageSize = 200;
    public const int MaximumSearchLength = 200;
}

public sealed record ProjectPartyAssignmentQuery(
    Guid ProjectId,
    IReadOnlyCollection<ProjectPartyAssignmentRole> Roles,
    string SearchText = "",
    int PageIndex = 0,
    int PageSize = ProjectPartyAssignmentQueryLimits.DefaultPageSize,
    DateTimeOffset? WindowStartUtc = null,
    DateTimeOffset? WindowEndUtc = null,
    bool AllocationOnly = false);

public sealed record ProjectPartyAssignmentPage(
    IReadOnlyList<ProjectPartyAssignmentDetail> Items,
    int PageIndex,
    int PageSize,
    int TotalCount)
{
    public static ProjectPartyAssignmentPage Empty(int pageSize = ProjectPartyAssignmentQueryLimits.DefaultPageSize)
        => new([], 0, pageSize, 0);

    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record ProjectPartyAssignmentCounts(
    int TotalCount,
    int AllocationCount,
    int ScheduledCount)
{
    public static ProjectPartyAssignmentCounts Empty { get; } = new(0, 0, 0);
}
