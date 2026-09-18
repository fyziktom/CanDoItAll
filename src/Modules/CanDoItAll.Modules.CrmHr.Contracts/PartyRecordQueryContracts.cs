namespace CanDoItAll.Modules.CrmHr;

[Flags]
public enum PartyRecordScope
{
    None = 0,
    People = 1,
    Organizations = 2,
    OrganizationUnits = 4,
    AiAgents = 8,
    All = People | Organizations | OrganizationUnits | AiAgents
}

public enum PartyRecordPopulation
{
    All,
    Workforce
}

public static class PartyRecordQueryLimits
{
    public const int DefaultPageSize = 24;
    public const int MaximumPageSize = 100;
    public const int MaximumSearchLength = 200;
    public const int MaximumTagCount = 20;
}

public sealed record PartyRecordQuery(
    string SearchText = "",
    IReadOnlyList<string>? Tags = null,
    PartyRecordScope Scope = PartyRecordScope.All,
    int PageIndex = 0,
    int PageSize = PartyRecordQueryLimits.DefaultPageSize,
    Guid? ExcludedPartyId = null,
    bool IncludeArchived = false,
    PartyRecordPopulation Population = PartyRecordPopulation.All);

public sealed record PartyRecordQueryItem(
    Guid Id,
    string DisplayName,
    PartyType PartyType,
    PartyLifecycleStatus LifecycleStatus,
    string ExternalCode,
    string Summary,
    IReadOnlyList<string> Tags,
    bool IsSensitive);

public sealed record PartyRecordPage(
    IReadOnlyList<PartyRecordQueryItem> Items,
    int PageIndex,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public interface IPartyRecordQueryService
{
    Task<PartyRecordQueryItem?> GetAsync(
        Guid partyId,
        bool includeArchived = false,
        CancellationToken cancellationToken = default);

    Task<PartyRecordPage> SearchAsync(
        PartyRecordQuery query,
        CancellationToken cancellationToken = default);
}
