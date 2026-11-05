using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

internal static class PartyAffiliationEndpoints {
    internal static string ScopeKey(Guid partyId) => $"crmhr:affiliation-endpoint:{partyId:D}";

    internal static Guid[] PartyIds(Guid personId, IEnumerable<PartyOrganizationAffiliationEditorModel> affiliations) => affiliations
        .SelectMany(item => new Guid?[] { item.PersonPartyId, item.OrganizationPartyId, item.OrganizationUnitPartyId, item.ManagerPartyId })
        .Where(id => id.HasValue)
        .Select(id => id!.Value)
        .Append(personId)
        .Distinct()
        .ToArray();

    internal static Task<bool> WouldInvalidateAsync(CrmHrDbContext context, Guid partyId, PartyType type, CancellationToken cancellationToken) => context
        .Set<PartyOrganizationAffiliation>().AsNoTracking().AnyAsync(row =>
            type != PartyType.Person && (row.PersonPartyId == partyId || row.ManagerPartyId == partyId) ||
            type != PartyType.Organization && row.OrganizationPartyId == partyId ||
            type != PartyType.OrganizationUnit && row.OrganizationUnitPartyId == partyId, cancellationToken);
}
