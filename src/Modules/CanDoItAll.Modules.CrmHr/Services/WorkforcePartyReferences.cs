using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

internal static class WorkforcePartyReferences {
    internal static async Task<bool> WouldInvalidateAsync(CrmHrDbContext context, Guid partyId, PartyType type, CancellationToken cancellationToken) {
        if (type != PartyType.Person && await context.Set<WorkforceProfile>().AsNoTracking()
            .AnyAsync(row => row.ManagerPartyId == partyId, cancellationToken)) {
            return true;
        }
        if (type is not (PartyType.Organization or PartyType.OrganizationUnit) && await context.Set<WorkforceProfile>().AsNoTracking()
            .AnyAsync(row => row.HomeUnitPartyId == partyId, cancellationToken)) {
            return true;
        }
        return type switch {
            PartyType.Person => await context.Set<WorkforceProfile>().AsNoTracking()
                .AnyAsync(row => row.PartyId == partyId && row.WorkforceKind == WorkforceKind.DeliveryUnit, cancellationToken),
            PartyType.Organization or PartyType.OrganizationUnit => await context.Set<WorkforceProfile>().AsNoTracking()
                .AnyAsync(row => row.PartyId == partyId && row.WorkforceKind != WorkforceKind.DeliveryUnit, cancellationToken),
            _ => false
        };
    }
}
