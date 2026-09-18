using System.ComponentModel.DataAnnotations.Schema;

namespace CanDoItAll.Modules.CrmHr;

public sealed record PartyProjectAssignmentItemModel(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    [property: Column(TypeName = "character varying(48)")] ProjectPartyAssignmentKind AssignmentKind,
    string NodeKey,
    decimal? AllocationPercent,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    bool IsPrimary,
    string Notes);

public static class PartyProjectAssignmentQueryLimits
{
    public const int DefaultPageSize = 8;
    public const int MaximumPageSize = 50;
}

public sealed record PartyProjectAssignmentQuery(
    Guid PartyId,
    int PageIndex = 0,
    int PageSize = PartyProjectAssignmentQueryLimits.DefaultPageSize);

public sealed record PartyProjectAssignmentPage(
    IReadOnlyList<PartyProjectAssignmentItemModel> Items,
    int PageIndex,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static PartyProjectAssignmentPage Empty(
        int pageSize = PartyProjectAssignmentQueryLimits.DefaultPageSize)
        => new([], 0, pageSize, 0);
}
