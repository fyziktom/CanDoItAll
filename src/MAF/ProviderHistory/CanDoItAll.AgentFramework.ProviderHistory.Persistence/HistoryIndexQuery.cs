using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public static class HistoryIndexQuery {
    public static IQueryable<HistoryEntryRow> Authorized(AppDbContext db, HistoryAccessContext context, DateTimeOffset now) {
        var rows = db.Set<HistoryEntryRow>().AsNoTracking().Where(row =>
            row.PartitionId == context.Partition.StorageLineageId && row.IsVisible &&
            (row.ExpiresAtUtc == null || row.ExpiresAtUtc > now));
        if (context.AllowedProviders is { } providers) {
            var ids = providers.Select(provider => provider.Value).ToArray();
            rows = rows.Where(row => row.ProviderId != null && ids.Contains(row.ProviderId.Value));
        }
        return rows;
    }

    public static IQueryable<HistoryEntryRow> Page(AppDbContext db, HistoryAccessContext context,
        ProviderRequestHistoryQuery query, HistoryPagePosition? position, DateTimeOffset now) {
        var fromUtc = query.FromUtc.ToUniversalTime();
        var toUtc = query.ToUtc.ToUniversalTime();
        var rows = Authorized(db, context, now).Where(row => row.SortAtUtc >= fromUtc && row.SortAtUtc < toUtc);
        if (query.Scope is HistoryProviderScope.SingleProvider single) {
            rows = rows.Where(row => row.ProviderId == single.Provider.Value);
        }
        if (query.Model is { } model) {
            rows = rows.Where(row => row.ResolvedModel == model.Value || row.RequestedModel == model.Value);
        }
        if (query.Workload is { } workload) {
            rows = rows.Where(row => row.Workload == workload);
        }
        if (query.Operation is { } operation) {
            rows = rows.Where(row => row.Operation == operation);
        }
        if (query.Outcome is { } outcome) {
            rows = rows.Where(row => row.Outcome == outcome);
        }
        if (query.PriceState is { } price) {
            rows = rows.Where(row => row.PriceState == price);
        }
        if (query.CredentialId is { } credential) {
            rows = rows.Where(row => row.CredentialId == credential.Value);
        }
        if (query.Issuer is { } issuer) {
            rows = rows.Where(row => row.Issuer == issuer);
        }
        if (query.Subject is { } subject) {
            rows = rows.Where(row => row.Subject == subject);
        }
        if (query.RequestId is { } request) {
            rows = rows.Where(row => row.RequestId == request.Value);
        }
        if (query.AttemptId is { } attempt) {
            rows = rows.Where(row => row.AttemptId == attempt.Value);
        }
        if (query.CorrelationId is { } correlation) {
            rows = rows.Where(row => row.CorrelationId == correlation);
        }
        if (query.ExternalReference is { } externalReference) {
            rows = rows.Where(row => row.ExternalReferenceValue == externalReference.Value);
            if (externalReference.Type is { } externalReferenceType) {
                rows = rows.Where(row => row.ExternalReferenceType == externalReferenceType);
            }
        }
        if (position is { } after) {
            var sortAtUtc = after.SortAtUtc.ToUniversalTime();
            rows = rows.Where(row => row.SortAtUtc < sortAtUtc ||
                row.SortAtUtc == sortAtUtc && row.Id.CompareTo(after.EntryId.Value) < 0);
        }
        return rows.OrderByDescending(row => row.SortAtUtc).ThenByDescending(row => row.Id).Take(query.PageSize + 1);
    }
}
