using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public static class CrmHrHomeQueryLimits
{
    public const int DirectoryPreviewSize = 5;
    public const int SensitivePreviewSize = 3;
    public const int OpenPipelinePreviewSize = 6;
}

public sealed record CrmHrHomePartyPreviewModel(
    Guid Id,
    string DisplayName,
    PartyType PartyType,
    PartyLifecycleStatus LifecycleStatus,
    bool IsSensitive,
    string Summary,
    DateTimeOffset UpdatedAtUtc);

public sealed record CrmHrHomeSnapshotModel(
    int PartyCount,
    int OrganizationCount,
    int OpportunityCount,
    int WorkforceProfileCount,
    int AgentProjectionCount,
    int SensitiveCount,
    IReadOnlyList<CrmHrHomePartyPreviewModel> DirectoryPreview,
    IReadOnlyList<CrmHrHomePartyPreviewModel> SensitivePreview,
    IReadOnlyList<OpportunitySummaryModel> OpenPipelinePreview);

internal sealed record CrmHrHomePartyCounts(
    int TotalCount,
    int OrganizationCount,
    int SensitiveCount);

public interface ICrmHrHomeQueryService
{
    Task<CrmHrHomeSnapshotModel> GetAsync(
        CancellationToken cancellationToken = default);
}

public sealed class CrmHrHomeQueryService(
    IDbContextFactory<AppDbContext> dbContextFactory) : ICrmHrHomeQueryService
{
    public async Task<CrmHrHomeSnapshotModel> GetAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var parties = dbContext.Set<Party>().AsNoTracking();
        var partyCounts = await parties
            .GroupBy(_ => 1)
            .Select(group => new CrmHrHomePartyCounts(
                group.Count(),
                group.Count(item =>
                    item.PartyType == PartyType.Organization ||
                    item.PartyType == PartyType.OrganizationUnit),
                group.Count(item => item.IsSensitive)))
            .SingleOrDefaultAsync(cancellationToken)
            ?? new CrmHrHomePartyCounts(0, 0, 0);
        var directoryPreview = await parties
            .OrderBy(item => item.DisplayName)
            .ThenBy(item => item.Id)
            .Take(CrmHrHomeQueryLimits.DirectoryPreviewSize)
            .Select(item => new CrmHrHomePartyPreviewModel(
                item.Id,
                item.DisplayName,
                item.PartyType,
                item.LifecycleStatus,
                item.IsSensitive,
                item.Summary,
                item.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
        var sensitivePreview = await parties
            .Where(item => item.IsSensitive)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenBy(item => item.DisplayName)
            .ThenBy(item => item.Id)
            .Take(CrmHrHomeQueryLimits.SensitivePreviewSize)
            .Select(item => new CrmHrHomePartyPreviewModel(
                item.Id,
                item.DisplayName,
                item.PartyType,
                item.LifecycleStatus,
                item.IsSensitive,
                item.Summary,
                item.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        var opportunityCount = await dbContext.Set<Opportunity>()
            .AsNoTracking()
            .CountAsync(cancellationToken);
        var openPipelineRows = await (
                from opportunity in dbContext.Set<Opportunity>().AsNoTracking()
                join accountParty in parties
                    on opportunity.AccountPartyId equals accountParty.Id
                    into accountParties
                from accountParty in accountParties.DefaultIfEmpty()
                join ownerParty in parties
                    on opportunity.OwnerPartyId equals ownerParty.Id
                    into ownerParties
                from ownerParty in ownerParties.DefaultIfEmpty()
                where opportunity.Stage != OpportunityStage.Won &&
                      opportunity.Stage != OpportunityStage.Lost
                orderby opportunity.ExpectedCloseDateUtc == null,
                    opportunity.ExpectedCloseDateUtc,
                    opportunity.UpdatedAtUtc descending,
                    opportunity.Id
                select new
                {
                    opportunity.Id,
                    opportunity.Title,
                    opportunity.Stage,
                    opportunity.AccountPartyId,
                    opportunity.OwnerPartyId,
                    AccountDisplayName = accountParty == null
                        ? "Unknown account"
                        : accountParty.DisplayName,
                    OwnerDisplayName = ownerParty == null
                        ? "Unknown owner"
                        : ownerParty.DisplayName,
                    opportunity.OpportunitySource,
                    opportunity.Amount,
                    opportunity.ProbabilityPercent,
                    opportunity.ExpectedCloseDateUtc,
                    opportunity.UpdatedAtUtc
                })
            .Take(CrmHrHomeQueryLimits.OpenPipelinePreviewSize)
            .ToListAsync(cancellationToken);
        var openPipelinePreview = openPipelineRows
            .Select(item => new OpportunitySummaryModel(
                item.Id,
                item.Title,
                item.Stage,
                item.AccountPartyId,
                item.OwnerPartyId,
                item.AccountDisplayName,
                item.OwnerDisplayName,
                item.OpportunitySource,
                item.Amount,
                item.ProbabilityPercent,
                ToDateOnly(item.ExpectedCloseDateUtc),
                item.UpdatedAtUtc))
            .ToList();

        var workforceProfileCount = await dbContext.Set<WorkforceProfile>()
            .AsNoTracking()
            .CountAsync(cancellationToken);
        var agentProfileCount = await (
                from party in parties
                join binding in dbContext.Set<AiResourceBinding>().AsNoTracking()
                    on party.Id equals binding.PartyId
                where party.PartyType == PartyType.AiAgent &&
                      binding.TechnicalAgentId.HasValue &&
                      binding.BindingStatus == AiResourceBindingStatus.Bound
                select party.Id)
            .CountAsync(cancellationToken);
        return new CrmHrHomeSnapshotModel(
            partyCounts.TotalCount,
            partyCounts.OrganizationCount,
            opportunityCount,
            workforceProfileCount,
            agentProfileCount,
            partyCounts.SensitiveCount,
            directoryPreview,
            sensitivePreview,
            openPipelinePreview);
    }

    private static DateOnly? ToDateOnly(DateTimeOffset? value)
    {
        return value.HasValue
            ? DateOnly.FromDateTime(value.Value.UtcDateTime)
            : null;
    }
}
