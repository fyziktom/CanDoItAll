using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public static class AiAgentDirectoryQueryLimits
{
    public const int DefaultPageSize = 12;
    public const int MaximumPageSize = 100;
    public const int MaximumSearchLength = 200;
}

public sealed record AiAgentDirectoryQuery(
    string SearchText = "",
    AiValidationStatus? ValidationStatus = null,
    int PageIndex = 0,
    int PageSize = AiAgentDirectoryQueryLimits.DefaultPageSize);

public sealed record AiAgentDirectoryPage(
    IReadOnlyList<AiAgentListItemModel> Items,
    int PageIndex,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public interface IAiAgentDirectoryQueryService
{
    Task RefreshProjectionAsync(CancellationToken cancellationToken = default);

    Task<AiAgentDirectoryPage> SearchAsync(
        AiAgentDirectoryQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class AiAgentDirectoryQueryService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IAiTechnicalAgentBridge technicalAgentBridge) : IAiAgentDirectoryQueryService
{
    public Task RefreshProjectionAsync(CancellationToken cancellationToken = default)
    {
        return technicalAgentBridge.SynchronizeDirectoryProjectionAsync(cancellationToken);
    }

    public async Task<AiAgentDirectoryPage> SearchAsync(
        AiAgentDirectoryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var normalized = Normalize(query);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidates =
            from party in dbContext.Set<Party>().AsNoTracking()
            join profile in dbContext.Set<AiAgentProfile>().AsNoTracking()
                on party.Id equals profile.PartyId into profiles
            from profile in profiles.DefaultIfEmpty()
            join owner in dbContext.Set<Party>().AsNoTracking()
                on profile.OwnerPartyId equals (Guid?)owner.Id into owners
            from owner in owners.DefaultIfEmpty()
            where party.PartyType == PartyType.AiAgent
            where dbContext.Set<AiResourceBinding>().Any(binding =>
                binding.PartyId == party.Id &&
                binding.TechnicalAgentId.HasValue &&
                binding.BindingStatus == AiResourceBindingStatus.Bound)
            select new
            {
                party.Id,
                party.DisplayName,
                party.Summary,
                party.IsSensitive,
                party.LifecycleStatus,
                party.UpdatedAtUtc,
                ValidationStatus = profile == null
                    ? AiValidationStatus.Draft
                    : profile.ValidationStatus,
                OwnerName = owner == null || owner.IsSensitive
                    ? string.Empty
                    : owner.DisplayName
            };

        if (normalized.ValidationStatus is AiValidationStatus validationStatus)
        {
            candidates = candidates.Where(candidate =>
                candidate.ValidationStatus == validationStatus);
        }

        if (!string.IsNullOrEmpty(normalized.SearchText))
        {
            var search = normalized.SearchText.ToUpperInvariant();
            candidates = candidates.Where(candidate =>
                candidate.DisplayName.ToUpper().Contains(search) ||
                (!candidate.IsSensitive &&
                 candidate.Summary.ToUpper().Contains(search)) ||
                candidate.OwnerName.ToUpper().Contains(search));
        }

        var totalCount = await candidates.CountAsync(cancellationToken);
        var pageRows = await candidates
            .OrderBy(candidate => candidate.DisplayName)
            .ThenBy(candidate => candidate.Id)
            .Skip(normalized.PageIndex * normalized.PageSize)
            .Take(normalized.PageSize)
            .ToListAsync(cancellationToken);
        if (pageRows.Count == 0)
        {
            return new AiAgentDirectoryPage(
                [],
                normalized.PageIndex,
                normalized.PageSize,
                totalCount);
        }

        var partyIds = pageRows
            .Select(row => row.Id)
            .ToArray();
        var technicalSummaries = await technicalAgentBridge.GetDirectorySummariesAsync(
            partyIds,
            cancellationToken);
        var items = pageRows
            .Select(row =>
            {
                if (!technicalSummaries.TryGetValue(row.Id, out var technicalSummary) ||
                    !technicalSummary.HasTechnicalProfile)
                {
                    throw new InvalidOperationException(
                        $"AI agent party '{row.Id:D}' has a bound projection but no matching AgentFramework record.");
                }

                return new AiAgentListItemModel(
                    row.Id,
                    row.DisplayName,
                    row.IsSensitive ? string.Empty : row.Summary,
                    row.LifecycleStatus,
                    technicalSummary.TechnicalAgentId,
                    technicalSummary.BindingStatus,
                    technicalSummary.BindingSummary,
                    technicalSummary.ExecutionMode,
                    row.ValidationStatus,
                    technicalSummary.ProviderName,
                    technicalSummary.DefaultModel,
                    row.OwnerName,
                    technicalSummary.CapabilityCount,
                    true,
                    technicalSummary.AgentsRoute,
                    row.UpdatedAtUtc);
            })
            .ToArray();

        return new AiAgentDirectoryPage(
            items,
            normalized.PageIndex,
            normalized.PageSize,
            totalCount);
    }

    private static AiAgentDirectoryQuery Normalize(AiAgentDirectoryQuery query)
    {
        if (query.PageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageIndex,
                "AI agent directory page index cannot be negative.");
        }

        if (query.PageSize is < 1 or > AiAgentDirectoryQueryLimits.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageSize,
                $"AI agent directory page size must be between 1 and {AiAgentDirectoryQueryLimits.MaximumPageSize}.");
        }

        if (query.PageIndex > int.MaxValue / query.PageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageIndex,
                "AI agent directory page offset is too large.");
        }

        var searchText = query.SearchText?.Trim() ?? string.Empty;
        if (searchText.Length > AiAgentDirectoryQueryLimits.MaximumSearchLength)
        {
            throw new ArgumentException(
                $"AI agent directory search cannot exceed {AiAgentDirectoryQueryLimits.MaximumSearchLength} characters.",
                nameof(query));
        }

        if (query.ValidationStatus.HasValue &&
            !Enum.IsDefined(query.ValidationStatus.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.ValidationStatus,
                "AI agent validation status must be supported.");
        }

        return query with
        {
            SearchText = searchText
        };
    }
}
