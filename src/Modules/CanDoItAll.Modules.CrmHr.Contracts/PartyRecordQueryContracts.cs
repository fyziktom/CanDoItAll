namespace CanDoItAll.Modules.CrmHr;

/// <summary>
/// Party types included by a party query, as a bit flags value: 1 People, 2 Organizations, 4 OrganizationUnits,
/// 8 AiAgents, 15 All. It filters records; it is not an authorization scope.
/// </summary>
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

/// <summary>
/// Summary of one CRM/HR party in a party list or lookup.
/// </summary>
/// <param name="Id">Party identifier, used by the other party operations.</param>
/// <param name="DisplayName">Name shown for the party. Always returned, also for sensitive parties.</param>
/// <param name="PartyType">Kind of party (person, organization, organization unit or AI agent).</param>
/// <param name="LifecycleStatus">Current lifecycle status of the party record.</param>
/// <param name="ExternalCode">
/// Business reference supplied by the organization, for example an ERP or customer number. It is not guaranteed to
/// be unique. Empty for sensitive parties.
/// </param>
/// <param name="Summary">Short description of the party. Empty for sensitive parties.</param>
/// <param name="Tags">Tags attached to the party. Empty for sensitive parties.</param>
/// <param name="IsSensitive">
/// True when the party is marked for restricted handling; its external code, summary and tags are then withheld.
/// </param>
public sealed record PartyRecordQueryItem(
    Guid Id,
    string DisplayName,
    PartyType PartyType,
    PartyLifecycleStatus LifecycleStatus,
    string ExternalCode,
    string Summary,
    IReadOnlyList<string> Tags,
    bool IsSensitive);

/// <summary>
/// One page of parties matching a party query.
/// </summary>
/// <param name="Items">Parties on this page, ordered by display name and then party identifier.</param>
/// <param name="PageIndex">Zero-based index of this page.</param>
/// <param name="PageSize">Maximum number of parties per page that was applied.</param>
/// <param name="TotalCount">Number of parties matching the filters across all pages.</param>
public sealed record PartyRecordPage(
    IReadOnlyList<PartyRecordQueryItem> Items,
    int PageIndex,
    int PageSize,
    int TotalCount)
{
    /// <summary>
    /// Number of pages at the applied page size; 0 when no party matches.
    /// </summary>
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
