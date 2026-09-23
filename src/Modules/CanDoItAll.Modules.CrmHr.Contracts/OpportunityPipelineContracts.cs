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
