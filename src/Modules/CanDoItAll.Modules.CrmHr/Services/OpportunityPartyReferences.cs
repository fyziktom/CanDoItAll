using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

internal static class OpportunityPartyReferences {
    internal static string ScopeKey(Guid partyId) => $"crmhr:opportunity-reference:{partyId:D}";

    internal static Task<bool> WouldInvalidateAsync(CrmHrDbContext context, Guid partyId, PartyType type, CancellationToken cancellationToken) =>
        context.Set<Opportunity>().AsNoTracking().AnyAsync(row =>
            type != PartyType.Organization && row.AccountPartyId == partyId ||
            type != PartyType.OrganizationUnit && row.DeliveryUnitPartyId == partyId ||
            type != PartyType.Person && type != PartyType.OrganizationUnit && type != PartyType.AiAgent && row.OwnerPartyId == partyId,
            cancellationToken);
}
