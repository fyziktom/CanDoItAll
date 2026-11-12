using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

internal static class CrmAccountConnectionsContent {
    internal static string ScopeKey(Guid accountPartyId) => $"crmhr:account-connections:{accountPartyId:D}";

    internal static CrmAccountConnectionsObservation Capture(Guid accountPartyId,
        IReadOnlyList<CrmAccountConnection> connections, IReadOnlyList<CrmAccountConnectionProjectLink> projects) => new(accountPartyId,
            connections.Select(row => new CrmAccountConnectionOriginal(row.Id, row.RelatedPartyId, row.Role,
                row.IsPrimary, row.Notes, row.CreatedAtUtc, row.UpdatedAtUtc)).ToArray(),
            projects.Select(row => new CrmAccountConnectionProjectOriginal(row.Id, row.AccountConnectionId, row.ProjectId, row.CreatedAtUtc)).ToArray());

    internal static bool Matches(CrmAccountConnectionsObservation original, CrmAccountConnectionsObservation current) =>
        original.AccountPartyId == current.AccountPartyId && original.Rows.Count == current.Rows.Count &&
        original.Projects.Count == current.Projects.Count && original.Rows.ToHashSet().SetEquals(current.Rows) &&
        original.Projects.ToHashSet().SetEquals(current.Projects);

    internal static async Task<CrmAccountConnectionsObservation> ReadAsync(CrmHrDbContext context, Guid accountPartyId,
        CancellationToken cancellationToken) {
        var rows = await context.Set<CrmAccountConnection>().AsNoTracking()
            .Where(row => row.AccountPartyId == accountPartyId).ToListAsync(cancellationToken);
        var ids = rows.Select(row => row.Id).ToArray();
        var projects = await context.Set<CrmAccountConnectionProjectLink>().AsNoTracking()
            .Where(row => ids.Contains(row.AccountConnectionId)).ToListAsync(cancellationToken);
        return Capture(accountPartyId, rows, projects);
    }

    internal static Error Conflict() => Error.Failure(
        "The saved account connections or project links changed. Review and reconcile their current values before saving.",
        CrmHrEditingErrorCodes.ConnectionConflict);
}
