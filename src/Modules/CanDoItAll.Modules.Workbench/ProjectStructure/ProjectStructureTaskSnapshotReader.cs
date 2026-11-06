using System.Data;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectStructureTaskSnapshot(ProjectStructureNode Task, ProjectStructureTaskEditState State,
    ProjectStructureTaskAssigneeSelectionResult Assignees, ProjectStructureGanttProjectionResult? Schedule);

public sealed class ProjectStructureTaskSnapshotReader(IDbContextFactory<WorkbenchDbContext> contexts,
    CoordinatedDatabaseTransaction transactions, ProjectWriteAdmissionService admissions,
    IProjectWorkAssignmentPartyFacts partyFacts, ProjectStructureGanttProjectionAdapter projection) {
    private const int MaximumGraphNodes = ProjectPlanAnalyticsPayloadPolicy.MaximumNodeCount;
    private const int MaximumGraphLinks = ProjectPlanAnalyticsPayloadPolicy.MaximumLinkCount;
    private const int MaximumAssignments = 10000;

    internal async Task<ProjectStructureTaskSnapshot> ReadAsync(ProjectWriteAdmission project, string nodeId,
        Guid occurrence, ProjectStructureGanttProjectionOptions? schedule, CancellationToken cancellationToken) {
        await using var database = await contexts.CreateDbContextAsync(cancellationToken);
        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken) : null;
        using var participation = transactions.Enter(database);
        await admissions.RequireManyForMutationAsync([project], cancellationToken);
        var query = database.Set<ProjectObjectRecord>().AsNoTracking().Where(node => node.ProjectId == project.ProjectId && !node.IsSystemManaged);
        var records = schedule is null
            ? await query.Where(node => node.NodeKey == nodeId).ToListAsync(cancellationToken)
            : await query.OrderBy(node => node.Id).Take(MaximumGraphNodes + 1).ToListAsync(cancellationToken);
        if (records.Count > MaximumGraphNodes) {
            throw new InvalidOperationException("This Task schedule exceeds the supported comparison size. The draft is retained.");
        }
        var record = records.SingleOrDefault(node => node.NodeKey == nodeId && node.Id == occurrence)
            ?? throw new ProjectStructureEditConflictException("The original Task occurrence was deleted or replaced. Your draft is retained.");
        await ProjectNodeBindingStorage.LoadAsync(database, records, cancellationToken);
        var task = ProjectWorkbenchNodeMapper.MapStructureNode(record);
        if (!ProjectStructureCanonicalTaskMutationPolicy.IsTask(task.ObjectType, task.ObjectSubtype)) {
            throw new ProjectStructureEditConflictException("The original Task kind changed. Your draft is retained.");
        }
        var state = ProjectStructureTaskEditStatePolicy.Read(task);
        var assignmentQuery = database.Set<ProjectWorkAssignmentRecord>().AsNoTracking().Where(row =>
            row.ProjectId == project.ProjectId && row.ProjectLifetimeId == project.LifetimeId && row.NodeKey == nodeId);
        var rows = await assignmentQuery.OrderBy(row => row.Id).Take(MaximumAssignments + 1).ToArrayAsync(cancellationToken);
        if (rows.Length > MaximumAssignments) {
            throw new InvalidOperationException("This Task assignment context exceeds the supported comparison size. No partial assignment set was accepted.");
        }
        var parties = await partyFacts.ReadForMutationAsync(rows.Select(row => row.PartyId).Distinct().ToArray(), cancellationToken);
        if (rows.Any(row => !parties.ContainsKey(row.PartyId))) {
            throw new InvalidOperationException("A Task assignee could not be resolved. No incomplete assignment set was accepted.");
        }
        var assignments = rows.Select(row => Detail(row.ToFact(), parties[row.PartyId])).ToArray();
        var assignees = ProjectStructureTaskAssigneeSelectionPolicy.Resolve(assignments, nodeId);
        ProjectStructureGanttProjectionResult? graph = null;
        if (schedule is not null) {
            var links = await database.Set<ProjectObjectLinkRecord>().AsNoTracking().Where(link => link.ProjectId == project.ProjectId &&
                !link.IsSystemManaged && link.LinkKind == ProjectObjectLinkKind.DependsOn).OrderBy(link => link.Id)
                .Take(MaximumGraphLinks + 1).ToArrayAsync(cancellationToken);
            if (links.Length > MaximumGraphLinks) {
                throw new InvalidOperationException("This Task dependency graph exceeds the supported comparison size. No partial graph was accepted.");
            }
            var surface = new ProjectStructureSurface(project.ProjectId, string.Empty,
                records.Select(node => ProjectWorkbenchNodeMapper.MapStructureNode(node)).ToArray(),
                links.Select(link => new ProjectStructureLink(link.SourceNodeKey, link.TargetNodeKey, link.LinkKind, true, link.Id)).ToArray(), null) {
                ExpectedProjectAdmission = project
            };
            graph = projection.Build(surface, assignments, schedule);
            if (!graph.IsValid || graph.Tasks.All(item => item.Id != new GanttTaskId(nodeId))) {
                throw new InvalidOperationException("The current Task schedule is invalid. The draft is retained; review the project dependencies.");
            }
        }
        return new(task, state, assignees, graph);
    }

    private static ProjectPartyAssignmentDetail Detail(ProjectWorkAssignmentFact row, ProjectWorkAssignmentPartyFact party) =>
        new(row.Id, row.ProjectId, row.PartyId, row.Role, party.DisplayName, party.PartyType.ToString(), party.PartyType,
            row.NodeKey, row.IsPrimary, row.AllocationPercent, row.StartsAtUtc, row.EndsAtUtc, row.Source, row.Notes,
            PartyAffiliationId: row.PartyOrganizationAffiliationId, ProjectLifetimeId: row.ProjectLifetimeId) { Original = row.Original };
}
