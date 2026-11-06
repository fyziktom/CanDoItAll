using CanDoItAll.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public sealed class WorkforceSearchProjection(IDbContextFactory<CrmHrDbContext> dbContextFactory, ISearchIndexService searchIndexService) {
    public async Task UpsertAsync(Guid partyId, CancellationToken cancellationToken) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var party = await dbContext.Set<Party>()
            .SingleOrDefaultAsync(item => item.Id == partyId, cancellationToken);
        var profile = await dbContext.Set<WorkforceProfile>()
            .SingleOrDefaultAsync(item => item.PartyId == partyId, cancellationToken);
        if (party is null || profile is null || party.IsSensitive) {
            await searchIndexService.DeleteAsync(CrmHrSearchSourceTypes.Workforce, partyId.ToString("N"), cancellationToken);
            return;
        }

        var skillSummary = string.Join(
            ", ",
            ((await WorkforceProfileQueries.ReadSkillsAsync(dbContext, [partyId], cancellationToken)).GetValueOrDefault(partyId) ?? [])
                .Select(item => $"{item.SkillName} ({item.Proficiency})"));
        var managerName = profile.ManagerPartyId.HasValue
            ? await dbContext.Set<Party>()
                .Where(item => item.Id == profile.ManagerPartyId.Value)
                .Select(item => item.DisplayName)
                .FirstOrDefaultAsync(cancellationToken)
            : string.Empty;
        var homeUnitName = profile.HomeUnitPartyId.HasValue
            ? await dbContext.Set<Party>()
                .Where(item => item.Id == profile.HomeUnitPartyId.Value)
                .Select(item => item.DisplayName)
                .FirstOrDefaultAsync(cancellationToken)
            : string.Empty;

        await searchIndexService.UpsertAsync(
            new SearchDocumentInput(
                CrmHrSearchSourceTypes.Workforce,
                partyId.ToString("N"),
                "CRM / HR workforce",
                party.DisplayName,
                $"{profile.JobTitle} / {profile.Discipline} / {profile.Status}".Trim(' ', '/'),
                string.Join(
                    Environment.NewLine,
                    new[] {
                        party.DisplayName,
                        profile.JobTitle,
                        profile.Discipline,
                        profile.Seniority,
                        profile.Location,
                        profile.TimeZone,
                        homeUnitName,
                        managerName,
                        skillSummary,
                        profile.Notes
                    }.Where(item => !string.IsNullOrWhiteSpace(item))),
                $"/crm-hr/workforce?partyId={partyId}"),
            cancellationToken);
    }
}
