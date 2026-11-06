using CanDoItAll.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public sealed class RecruitmentSearchProjection(IDbContextFactory<CrmHrDbContext> dbContextFactory, ISearchIndexService searchIndexService) {
    public async Task UpsertAsync(Guid applicationId, CancellationToken cancellationToken) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var application = await dbContext.Set<RecruitmentApplication>()
            .SingleOrDefaultAsync(item => item.Id == applicationId, cancellationToken);
        if (application is null) {
            await searchIndexService.DeleteAsync(CrmHrSearchSourceTypes.RecruitmentApplication, applicationId.ToString("N"), cancellationToken);
            return;
        }

        var party = await dbContext.Set<Party>()
            .SingleOrDefaultAsync(item => item.Id == application.PartyId, cancellationToken);
        if (party is null || party.IsSensitive) {
            await searchIndexService.DeleteAsync(CrmHrSearchSourceTypes.RecruitmentApplication, applicationId.ToString("N"), cancellationToken);
            return;
        }

        var peopleNames = await dbContext.Set<Party>()
            .Where(item =>
                item.Id == application.RecruiterPartyId ||
                item.Id == application.HiringManagerPartyId ||
                item.Id == application.TargetUnitPartyId)
            .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);
        var contacts = await dbContext.Set<PartyContactPoint>()
            .Where(item => item.PartyId == application.PartyId && item.IsPublic)
            .OrderByDescending(item => item.IsPrimary)
            .ToListAsync(cancellationToken);
        var primaryEmail = contacts
            .Where(item => item.ContactType == PartyContactType.Email)
            .Select(item => item.Value)
            .FirstOrDefault();
        var primaryPhone = contacts
            .Where(item => item.ContactType == PartyContactType.Phone)
            .Select(item => item.Value)
            .FirstOrDefault();

        await searchIndexService.UpsertAsync(
            new SearchDocumentInput(
                CrmHrSearchSourceTypes.RecruitmentApplication,
                applicationId.ToString("N"),
                "CRM / HR recruiting",
                party.DisplayName,
                $"{application.Stage} / {application.Decision} / {application.DesiredRole}".Trim(' ', '/'),
                string.Join(
                    Environment.NewLine,
                    new[] {
                        party.DisplayName,
                        primaryEmail,
                        primaryPhone,
                        application.DesiredRole,
                        application.Source,
                        application.Notes,
                        application.RecruiterPartyId.HasValue ? peopleNames.GetValueOrDefault(application.RecruiterPartyId.Value) : string.Empty,
                        application.HiringManagerPartyId.HasValue ? peopleNames.GetValueOrDefault(application.HiringManagerPartyId.Value) : string.Empty,
                        application.TargetUnitPartyId.HasValue ? peopleNames.GetValueOrDefault(application.TargetUnitPartyId.Value) : string.Empty
                    }.Where(item => !string.IsNullOrWhiteSpace(item))),
                $"/crm-hr/recruiting?applicationId={applicationId}"),
            cancellationToken);
    }
}

