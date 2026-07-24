using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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

public sealed class PartyRecordQueryService(
    IDbContextFactory<AppDbContext> dbContextFactory) : IPartyRecordQueryService
{
    public async Task<PartyRecordQueryItem?> GetAsync(
        Guid partyId,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        if (partyId == Guid.Empty)
        {
            throw new ArgumentException("A party identifier is required.", nameof(partyId));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<Party> candidates = dbContext.Set<Party>().AsNoTracking();
        if (!includeArchived)
        {
            candidates = candidates.Where(party =>
                party.LifecycleStatus != PartyLifecycleStatus.Archived);
        }

        var party = await candidates
            .Where(candidate => candidate.Id == partyId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.DisplayName,
                candidate.PartyType,
                candidate.LifecycleStatus,
                candidate.ExternalCode,
                candidate.Summary,
                candidate.TagsJson,
                candidate.IsSensitive
            })
            .SingleOrDefaultAsync(cancellationToken);
        return party is null
            ? null
            : new PartyRecordQueryItem(
                party.Id,
                party.DisplayName,
                party.PartyType,
                party.LifecycleStatus,
                party.IsSensitive ? string.Empty : party.ExternalCode,
                party.IsSensitive ? string.Empty : party.Summary,
                party.IsSensitive ? [] : DeserializeTags(party.TagsJson, party.Id),
                party.IsSensitive);
    }

    public async Task<PartyRecordPage> SearchAsync(
        PartyRecordQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var normalized = Normalize(query);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<Party> candidates = dbContext.Set<Party>()
            .AsNoTracking();

        if (!normalized.IncludeArchived)
        {
            candidates = candidates.Where(party =>
                party.LifecycleStatus != PartyLifecycleStatus.Archived);
        }

        if (normalized.ExcludedPartyId.HasValue)
        {
            candidates = candidates.Where(party => party.Id != normalized.ExcludedPartyId.Value);
        }

        candidates = ApplyScope(candidates, normalized.Scope);
        if (normalized.Population == PartyRecordPopulation.Workforce)
        {
            candidates = candidates.Where(party =>
                party.PartyType == PartyType.Person ||
                party.PartyType == PartyType.OrganizationUnit ||
                dbContext.Set<WorkforceProfile>().Any(profile => profile.PartyId == party.Id) ||
                dbContext.Set<PartyRoleAssignment>().Any(role =>
                    role.PartyId == party.Id &&
                    role.RoleKind == PartyRoleKind.DeliveryUnit));
        }

        if (!string.IsNullOrEmpty(normalized.SearchText))
        {
            var search = normalized.SearchText.ToUpperInvariant();
            candidates = candidates.Where(party =>
                party.DisplayName.ToUpper().Contains(search) ||
                (!party.IsSensitive &&
                 (party.ExternalCode.ToUpper().Contains(search) ||
                  party.Summary.ToUpper().Contains(search))));
        }

        foreach (var tag in normalized.Tags ?? [])
        {
            var serializedTag = JsonSerializer.Serialize(tag).ToUpperInvariant();
            candidates = candidates.Where(party =>
                !party.IsSensitive &&
                party.TagsJson.ToUpper().Contains(serializedTag));
        }

        var totalCount = await candidates.CountAsync(cancellationToken);
        var pageRows = await candidates
            .OrderBy(party => party.DisplayName)
            .ThenBy(party => party.Id)
            .Skip(normalized.PageIndex * normalized.PageSize)
            .Take(normalized.PageSize)
            .Select(party => new
            {
                party.Id,
                party.DisplayName,
                party.PartyType,
                party.LifecycleStatus,
                party.ExternalCode,
                party.Summary,
                party.TagsJson,
                party.IsSensitive
            })
            .ToListAsync(cancellationToken);

        var items = pageRows
            .Select(party => new PartyRecordQueryItem(
                party.Id,
                party.DisplayName,
                party.PartyType,
                party.LifecycleStatus,
                party.IsSensitive ? string.Empty : party.ExternalCode,
                party.IsSensitive ? string.Empty : party.Summary,
                party.IsSensitive ? [] : DeserializeTags(party.TagsJson, party.Id),
                party.IsSensitive))
            .ToList();

        return new PartyRecordPage(
            items,
            normalized.PageIndex,
            normalized.PageSize,
            totalCount);
    }

    private static PartyRecordQuery Normalize(PartyRecordQuery query)
    {
        if (query.PageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageIndex,
                "Party record page index cannot be negative.");
        }

        if (query.PageSize is < 1 or > PartyRecordQueryLimits.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageSize,
                $"Party record page size must be between 1 and {PartyRecordQueryLimits.MaximumPageSize}.");
        }

        if (query.PageIndex > int.MaxValue / query.PageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageIndex,
                "Party record page offset is too large.");
        }

        var searchText = query.SearchText?.Trim() ?? string.Empty;
        if (searchText.Length > PartyRecordQueryLimits.MaximumSearchLength)
        {
            throw new ArgumentException(
                $"Party record search cannot exceed {PartyRecordQueryLimits.MaximumSearchLength} characters.",
                nameof(query));
        }

        var tags = (query.Tags ?? [])
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (tags.Count > PartyRecordQueryLimits.MaximumTagCount)
        {
            throw new ArgumentException(
                $"Party record tag filters cannot exceed {PartyRecordQueryLimits.MaximumTagCount} values.",
                nameof(query));
        }

        if (query.Scope == PartyRecordScope.None ||
            (query.Scope & ~PartyRecordScope.All) != PartyRecordScope.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.Scope,
                "Party record scope must contain at least one supported party type.");
        }

        if (!Enum.IsDefined(query.Population))
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.Population,
                "Party record population must be supported.");
        }

        return query with
        {
            SearchText = searchText,
            Tags = tags
        };
    }

    private static IQueryable<Party> ApplyScope(
        IQueryable<Party> candidates,
        PartyRecordScope scope)
    {
        var includePeople = scope.HasFlag(PartyRecordScope.People);
        var includeOrganizations = scope.HasFlag(PartyRecordScope.Organizations);
        var includeOrganizationUnits = scope.HasFlag(PartyRecordScope.OrganizationUnits);
        var includeAiAgents = scope.HasFlag(PartyRecordScope.AiAgents);

        return candidates.Where(party =>
            (includePeople && party.PartyType == PartyType.Person) ||
            (includeOrganizations && party.PartyType == PartyType.Organization) ||
            (includeOrganizationUnits && party.PartyType == PartyType.OrganizationUnit) ||
            (includeAiAgents && party.PartyType == PartyType.AiAgent));
    }

    private static IReadOnlyList<string> DeserializeTags(string tagsJson, Guid partyId)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(tagsJson) ?? [];
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Party '{partyId}' contains invalid tags JSON.",
                exception);
        }
    }
}
