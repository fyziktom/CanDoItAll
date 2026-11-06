using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

internal static class PartyMergeAccountRecords {
    internal static async Task<Error?> ApplyAsync(CrmHrDbContext context, Guid retainedId, Guid sourceId,
        string actor, DateTimeOffset now, CancellationToken cancellationToken) {
        var connections = await context.Set<CrmAccountConnection>().Where(row =>
            row.AccountPartyId == retainedId || row.AccountPartyId == sourceId ||
            row.RelatedPartyId == retainedId || row.RelatedPartyId == sourceId).ToListAsync(cancellationToken);
        if (connections.Any(row => Replace(row.AccountPartyId) == Replace(row.RelatedPartyId))) {
            return Error.Validation("These accounts are connected to each other. Resolve that connection before merging them.",
                CrmHrEditingErrorCodes.PartyMergeAccountConnection);
        }
        var profiles = await context.Set<CrmAccountProfile>()
            .Where(row => row.AccountPartyId == retainedId || row.AccountPartyId == sourceId).ToListAsync(cancellationToken);
        var source = profiles.SingleOrDefault(row => row.AccountPartyId == sourceId);
        if (source is not null) {
            var retained = profiles.SingleOrDefault(row => row.AccountPartyId == retainedId);
            if (retained is null) {
                source.AccountPartyId = retainedId;
                retained = source;
            } else {
                retained.CommercialNotes = Combine(retained.CommercialNotes, source.CommercialNotes);
                retained.ConstraintNotes = Combine(retained.ConstraintNotes, source.ConstraintNotes);
                retained.TimingRiskNotes = Combine(retained.TimingRiskNotes, source.TimingRiskNotes);
                context.Remove(source);
            }
            retained.LastChangedBy = actor;
            retained.UpdatedAtUtc = CrmHrEditVersions.Next(retained.UpdatedAtUtc, now);
        }
        var ids = connections.Select(row => row.Id).ToArray();
        var links = await context.Set<CrmAccountConnectionProjectLink>()
            .Where(row => ids.Contains(row.AccountConnectionId)).ToListAsync(cancellationToken);
        foreach (var group in connections.GroupBy(row => (Account: Replace(row.AccountPartyId), Related: Replace(row.RelatedPartyId), row.Role))) {
            var ordered = group.OrderBy(row => row.AccountPartyId == sourceId || row.RelatedPartyId == sourceId).ThenBy(row => row.Id).ToArray();
            var retained = ordered[0];
            var changed = retained.AccountPartyId == sourceId || retained.RelatedPartyId == sourceId || ordered.Length > 1;
            var projectIds = links.Where(row => row.AccountConnectionId == retained.Id).Select(row => row.ProjectId).ToHashSet();
            foreach (var duplicate in ordered.Skip(1)) {
                retained.Notes = Combine(retained.Notes, duplicate.Notes);
                retained.IsPrimary |= duplicate.IsPrimary;
                foreach (var link in links.Where(row => row.AccountConnectionId == duplicate.Id)) {
                    if (projectIds.Add(link.ProjectId)) {
                        link.AccountConnectionId = retained.Id;
                    } else {
                        context.Remove(link);
                    }
                }
                context.ChangeTracker.DetectChanges();
                context.Remove(duplicate);
            }
            if (changed) {
                retained.AccountPartyId = group.Key.Account;
                retained.RelatedPartyId = group.Key.Related;
                retained.UpdatedAtUtc = CrmHrEditVersions.Next(retained.UpdatedAtUtc, now);
            }
        }
        return null;

        Guid Replace(Guid id) => id == sourceId ? retainedId : id;
    }

    private static string Combine(string retained, string source) => string.IsNullOrWhiteSpace(source) || retained == source
        ? retained : string.IsNullOrWhiteSpace(retained) ? source : retained + Environment.NewLine + source;
}
