using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public static class OpportunityPipelineQueryLimits
{
    public const int DefaultPageSize = 24;
    public const int MaximumPageSize = 100;
    public const int MaximumSearchLength = 200;
}

public sealed record OpportunityPipelineQuery(
    Guid AccountPartyId,
    string SearchText = "",
    OpportunityStage? Stage = null,
    Guid? OwnerPartyId = null,
    Guid? DeliveryUnitPartyId = null,
    Guid? PartnerPartyId = null,
    OpportunitySource? Source = null,
    int PageIndex = 0,
    int PageSize = OpportunityPipelineQueryLimits.DefaultPageSize);

public sealed record OpportunityPipelineItem(
    Guid Id,
    string Title,
    OpportunityStage Stage,
    OpportunitySource Source,
    Guid AccountPartyId,
    string AccountDisplayName,
    Guid OwnerPartyId,
    string OwnerDisplayName,
    Guid? DeliveryUnitPartyId,
    string DeliveryUnitDisplayName,
    string CurrencyCode,
    decimal? Amount,
    int ProbabilityPercent,
    DateOnly? ExpectedCloseOn,
    DateTimeOffset UpdatedAtUtc);

public sealed record OpportunityPipelinePage(
    IReadOnlyList<OpportunityPipelineItem> Items,
    int PageIndex,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public interface IOpportunityPipelineQueryService
{
    Task<OpportunityPipelinePage> SearchAsync(
        OpportunityPipelineQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class OpportunityPipelineQueryService(
    IDbContextFactory<AppDbContext> dbContextFactory) : IOpportunityPipelineQueryService
{
    public async Task<OpportunityPipelinePage> SearchAsync(
        OpportunityPipelineQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var normalized = Normalize(query);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<Opportunity> candidates = dbContext.Set<Opportunity>()
            .AsNoTracking()
            .Where(opportunity => opportunity.AccountPartyId == normalized.AccountPartyId);

        if (!string.IsNullOrEmpty(normalized.SearchText))
        {
            var search = normalized.SearchText.ToUpperInvariant();
            candidates = candidates.Where(opportunity =>
                opportunity.Title.ToUpper().Contains(search) ||
                opportunity.Summary.ToUpper().Contains(search));
        }

        if (normalized.Stage.HasValue)
        {
            candidates = candidates.Where(opportunity => opportunity.Stage == normalized.Stage.Value);
        }

        if (normalized.OwnerPartyId.HasValue)
        {
            candidates = candidates.Where(opportunity => opportunity.OwnerPartyId == normalized.OwnerPartyId.Value);
        }

        if (normalized.DeliveryUnitPartyId.HasValue)
        {
            candidates = candidates.Where(opportunity =>
                opportunity.DeliveryUnitPartyId == normalized.DeliveryUnitPartyId.Value);
        }

        if (normalized.PartnerPartyId.HasValue)
        {
            candidates = candidates.Where(opportunity =>
                dbContext.Set<OpportunityPartyLink>().Any(link =>
                    link.OpportunityId == opportunity.Id &&
                    link.PartyId == normalized.PartnerPartyId.Value &&
                    link.Role == OpportunityPartyRole.Partner));
        }

        if (normalized.Source.HasValue)
        {
            candidates = candidates.Where(opportunity =>
                opportunity.OpportunitySource == normalized.Source.Value);
        }

        var totalCount = await candidates.CountAsync(cancellationToken);
        var rows = await candidates
            .OrderByDescending(opportunity => opportunity.UpdatedAtUtc)
            .ThenBy(opportunity => opportunity.Id)
            .Skip(normalized.PageIndex * normalized.PageSize)
            .Take(normalized.PageSize)
            .Select(opportunity => new
            {
                opportunity.Id,
                opportunity.Title,
                opportunity.Stage,
                opportunity.OpportunitySource,
                opportunity.AccountPartyId,
                opportunity.OwnerPartyId,
                opportunity.DeliveryUnitPartyId,
                opportunity.CurrencyCode,
                opportunity.Amount,
                opportunity.ProbabilityPercent,
                opportunity.ExpectedCloseDateUtc,
                opportunity.UpdatedAtUtc,
                AccountDisplayName = dbContext.Set<Party>()
                    .Where(party => party.Id == opportunity.AccountPartyId)
                    .Select(party => party.DisplayName)
                    .FirstOrDefault(),
                OwnerDisplayName = dbContext.Set<Party>()
                    .Where(party => party.Id == opportunity.OwnerPartyId)
                    .Select(party => party.DisplayName)
                    .FirstOrDefault(),
                DeliveryUnitDisplayName = opportunity.DeliveryUnitPartyId.HasValue
                    ? dbContext.Set<Party>()
                        .Where(party => party.Id == opportunity.DeliveryUnitPartyId.Value)
                        .Select(party => party.DisplayName)
                        .FirstOrDefault()
                    : null
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(opportunity => new OpportunityPipelineItem(
                opportunity.Id,
                opportunity.Title,
                opportunity.Stage,
                opportunity.OpportunitySource,
                opportunity.AccountPartyId,
                RequirePartyDisplayName(
                    opportunity.AccountDisplayName,
                    opportunity.AccountPartyId,
                    "account"),
                opportunity.OwnerPartyId,
                RequirePartyDisplayName(
                    opportunity.OwnerDisplayName,
                    opportunity.OwnerPartyId,
                    "owner"),
                opportunity.DeliveryUnitPartyId,
                opportunity.DeliveryUnitPartyId.HasValue
                    ? RequirePartyDisplayName(
                        opportunity.DeliveryUnitDisplayName,
                        opportunity.DeliveryUnitPartyId.Value,
                        "delivery unit")
                    : string.Empty,
                opportunity.CurrencyCode,
                opportunity.Amount,
                opportunity.ProbabilityPercent,
                opportunity.ExpectedCloseDateUtc.HasValue
                    ? DateOnly.FromDateTime(opportunity.ExpectedCloseDateUtc.Value.UtcDateTime)
                    : null,
                opportunity.UpdatedAtUtc))
            .ToList();

        return new OpportunityPipelinePage(
            items,
            normalized.PageIndex,
            normalized.PageSize,
            totalCount);
    }

    private static string RequirePartyDisplayName(
        string? displayName,
        Guid partyId,
        string role)
    {
        return string.IsNullOrWhiteSpace(displayName)
            ? throw new InvalidOperationException(
                $"Opportunity {role} party '{partyId}' does not resolve to a directory record.")
            : displayName;
    }

    private static OpportunityPipelineQuery Normalize(OpportunityPipelineQuery query)
    {
        if (query.AccountPartyId == Guid.Empty)
        {
            throw new ArgumentException("An account is required for an opportunity pipeline query.", nameof(query));
        }

        if (query.PageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageIndex,
                "Opportunity page index cannot be negative.");
        }

        if (query.PageSize is < 1 or > OpportunityPipelineQueryLimits.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageSize,
                $"Opportunity page size must be between 1 and {OpportunityPipelineQueryLimits.MaximumPageSize}.");
        }

        if (query.PageIndex > int.MaxValue / query.PageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageIndex,
                "Opportunity page offset is too large.");
        }

        var searchText = query.SearchText?.Trim() ?? string.Empty;
        if (searchText.Length > OpportunityPipelineQueryLimits.MaximumSearchLength)
        {
            throw new ArgumentException(
                $"Opportunity search cannot exceed {OpportunityPipelineQueryLimits.MaximumSearchLength} characters.",
                nameof(query));
        }

        return query with { SearchText = searchText };
    }
}
