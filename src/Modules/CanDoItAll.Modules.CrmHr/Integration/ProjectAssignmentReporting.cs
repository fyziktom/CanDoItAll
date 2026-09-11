using System.ComponentModel.DataAnnotations.Schema;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace CanDoItAll.Modules.CrmHr;

internal sealed record ProjectAssignmentReportRow(
    Guid Id,
    Guid ProjectId,
    Guid PartyId,
    Guid? PartyOrganizationAffiliationId,
    [property: Column(TypeName = "character varying(48)")] ProjectPartyAssignmentKind AssignmentKind,
    string NodeKey,
    string PhaseName,
    Guid? OpportunityId,
    decimal? AllocationPercent,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    bool IsPrimary,
    string Source,
    string Notes,
    Guid? ProjectLifetimeId,
    Guid? CurrentProjectLifetimeId);

internal static class ProjectAssignmentReporting {
    internal const string AllAssignmentsSql = """
        SELECT assignment.*, project."LifetimeId" AS "CurrentProjectLifetimeId"
        FROM (
        SELECT "Id", "ProjectId", "PartyId", "PartyOrganizationAffiliationId", "AssignmentKind",
            "NodeKey", "PhaseName", "OpportunityId", "AllocationPercent", "StartsAtUtc", "EndsAtUtc", "IsPrimary", "Source", "Notes", "ProjectLifetimeId"
        FROM "CrmHr_ProjectPartyAssignments"
        UNION ALL
        SELECT "Id", "ProjectId", "PartyId", "PartyOrganizationAffiliationId", 'WorkItemAssignee'::varchar(48) AS "AssignmentKind",
            "NodeKey", "PhaseName", "OpportunityId", "AllocationPercent", "StartsAtUtc", "EndsAtUtc", "IsPrimary", "Source", "Notes", "ProjectLifetimeId"
        FROM "Workbench_WorkAssignments"
        ) AS assignment
        LEFT JOIN "Projects_Projects" AS project ON project."Id" = assignment."ProjectId"
            AND NOT EXISTS (SELECT 1 FROM "Projects_ProjectRetirements" AS retirement WHERE retirement."LifetimeId" = project."LifetimeId")
        """;

    internal static IQueryable<ProjectAssignmentReportRow> Relational(CrmHrDbContext context) =>
        context.Database.SqlQueryRaw<ProjectAssignmentReportRow>(AllAssignmentsSql);

    internal static async Task<IQueryable<ProjectAssignmentReportRow>> ForProjectsAsync(CrmHrDbContext context,
        IProjectWorkAssignmentQueries work, IProjectRecordQueryService projects, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) {
        if (context.Database.IsRelational()) {
            return Relational(context).Where(item => ids.Contains(item.ProjectId) &&
                (item.CurrentProjectLifetimeId == null || item.ProjectLifetimeId == item.CurrentProjectLifetimeId));
        }
        RequireInMemory(context);
        var participation = await context.Set<ProjectPartyAssignment>().AsNoTracking()
            .Where(item => ids.Contains(item.ProjectId)).ToListAsync(cancellationToken);
        var combined = await CombineAsync(participation, await work.ListForProjectsAsync(ids, cancellationToken), projects, cancellationToken);
        return combined.Where(item => item.CurrentProjectLifetimeId == null || item.ProjectLifetimeId == item.CurrentProjectLifetimeId);
    }

    internal static async Task<IQueryable<ProjectAssignmentReportRow>> ForPartiesAsync(CrmHrDbContext context,
        IProjectWorkAssignmentQueries work, IProjectRecordQueryService projects, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) {
        if (context.Database.IsRelational()) {
            return Relational(context).Where(item => ids.Contains(item.PartyId));
        }
        RequireInMemory(context);
        var participation = await context.Set<ProjectPartyAssignment>().AsNoTracking()
            .Where(item => ids.Contains(item.PartyId)).ToListAsync(cancellationToken);
        return await CombineAsync(participation, await work.ListForPartiesAsync(ids, cancellationToken), projects, cancellationToken);
    }

    internal static async Task<IQueryable<ProjectAssignmentReportRow>> ForWorkforceAsync(CrmHrDbContext context,
        IProjectWorkAssignmentQueries work, IProjectRecordQueryService projects, CancellationToken cancellationToken) {
        if (context.Database.IsRelational()) {
            return Relational(context).Where(item => item.ProjectLifetimeId != null && item.ProjectLifetimeId == item.CurrentProjectLifetimeId);
        }
        RequireInMemory(context);
        var ids = await context.Set<WorkforceProfile>().Select(item => item.PartyId).Distinct().ToArrayAsync(cancellationToken);
        return (await ForPartiesAsync(context, work, projects, ids, cancellationToken))
            .Where(item => item.ProjectLifetimeId != null && item.ProjectLifetimeId == item.CurrentProjectLifetimeId);
    }

    internal static IQueryable<T> ReadRoot<T>(CrmHrDbContext context, IQueryable<T> query) {
        if (context.Database.IsRelational()) {
            return query;
        }
        RequireInMemory(context);
        return query.AsEnumerable().Select(static item => item).AsQueryable();
    }

    internal static Task<List<T>> ToAssignmentReportListAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return query.Provider switch {
            IAsyncQueryProvider => query.ToListAsync(cancellationToken),
            EnumerableQuery<T> => Task.FromResult(query.ToList()),
            _ => throw new InvalidOperationException("Unsupported assignment reporting query provider.")
        };
    }

    internal static async Task<T[]> ToAssignmentReportArrayAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken) =>
        (await query.ToAssignmentReportListAsync(cancellationToken)).ToArray();

    internal static Task<int> CountAssignmentReportAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return query.Provider switch {
            IAsyncQueryProvider => query.CountAsync(cancellationToken),
            EnumerableQuery<T> => Task.FromResult(query.Count()),
            _ => throw new InvalidOperationException("Unsupported assignment reporting query provider.")
        };
    }

    internal static async Task<T?> SingleAssignmentReportOrDefaultAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken) =>
        (await query.Take(2).ToAssignmentReportListAsync(cancellationToken)).SingleOrDefault();

    private static async Task<IQueryable<ProjectAssignmentReportRow>> CombineAsync(IReadOnlyList<ProjectPartyAssignment> participation,
        IReadOnlyList<ProjectWorkAssignmentFact> work, IProjectRecordQueryService projects, CancellationToken cancellationToken) {
        var projectIds = participation.Select(item => item.ProjectId).Concat(work.Select(item => item.ProjectId))
            .Where(id => id != Guid.Empty).Distinct().ToArray();
        var current = (await projects.GetManyAsync(projectIds, cancellationToken)).ToDictionary(item => item.Id, item => item.LifetimeId);
        return participation.Select(item => new ProjectAssignmentReportRow(
            item.Id, item.ProjectId, item.PartyId, item.PartyOrganizationAffiliationId, item.AssignmentKind, item.NodeKey,
            item.PhaseName, item.OpportunityId, item.AllocationPercent, item.StartsAtUtc, item.EndsAtUtc, item.IsPrimary, item.Source, item.Notes,
            item.ProjectLifetimeId, current.GetValueOrDefault(item.ProjectId)))
            .Concat(work.Select(item => new ProjectAssignmentReportRow(item.Id, item.ProjectId, item.PartyId,
                item.PartyOrganizationAffiliationId, ProjectPartyAssignmentKind.WorkItemAssignee, item.NodeKey, item.PhaseName,
                item.OpportunityId, item.AllocationPercent, item.StartsAtUtc, item.EndsAtUtc, item.IsPrimary, item.Source, item.Notes,
                item.ProjectLifetimeId, current.GetValueOrDefault(item.ProjectId)))).AsQueryable();
    }

    private static void RequireInMemory(CrmHrDbContext context) {
        if (context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory") {
            throw new InvalidOperationException("Assignment reporting requires a relational database or the explicit InMemory test provider.");
        }
    }
}
