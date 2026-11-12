using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

internal static class AccountPartyReferences {
    internal static async Task<bool> WouldInvalidateAsync(CrmHrDbContext context, Guid partyId, PartyType type, CancellationToken cancellationToken) {
        if (type == PartyType.Organization) {
            return false;
        }
        return await context.Set<CrmAccountProfile>().AsNoTracking().AnyAsync(row => row.AccountPartyId == partyId, cancellationToken) ||
            await context.Set<CrmAccountConnection>().AsNoTracking().AnyAsync(row => row.AccountPartyId == partyId, cancellationToken) ||
            await context.Set<InteractionPartyLink>().AsNoTracking().AnyAsync(row => row.PartyId == partyId && row.Role == InteractionPartyRole.Account, cancellationToken);
    }
}
