using System.Collections.Immutable;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectWorkPartyMergeObservationReader {
    internal static async Task<ProjectWorkPartyMergeObservation> ReadAsync(WorkbenchDbContext context,
        Guid retainedPartyId, Guid mergedPartyId, IReadOnlyCollection<Guid> affiliationIds, int maximumRows,
        CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumRows, 1);
        var parties = new[] { retainedPartyId, mergedPartyId };
        var affiliations = affiliationIds.ToArray();
        var rows = await context.Database.SqlQuery<ProjectWorkPartyMergeVersion>($"""
            WITH nodes AS (
                SELECT DISTINCT "ProjectId", "ProjectLifetimeId", "NodeKey"
                FROM "Workbench_WorkAssignments"
                WHERE "PartyId" = ANY ({parties}) OR "PartyOrganizationAffiliationId" = ANY ({affiliations})
            ), versions AS (
                SELECT {(int)ProjectWorkPartyMergeMember.Assignment} AS "Member", w."Id", w."ProjectId",
                    w."ProjectLifetimeId", w."NodeKey", w.xmin::text::bigint AS "Version"
                FROM "Workbench_WorkAssignments" w JOIN nodes n ON n."ProjectId" = w."ProjectId"
                    AND n."ProjectLifetimeId" IS NOT DISTINCT FROM w."ProjectLifetimeId" AND n."NodeKey" = w."NodeKey"
                UNION ALL
                SELECT {(int)ProjectWorkPartyMergeMember.WorkItem}, o."Id", o."ProjectId", n."ProjectLifetimeId",
                    o."NodeKey", o.xmin::text::bigint
                FROM "Workbench_ProjectObjects" o JOIN nodes n ON n."ProjectId" = o."ProjectId" AND n."NodeKey" = o."NodeKey"
            )
            SELECT * FROM versions ORDER BY "Member", "Id", "ProjectLifetimeId" LIMIT {maximumRows + 1}
            """).ToArrayAsync(cancellationToken);
        return new(rows.ToImmutableArray());
    }
}
