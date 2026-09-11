using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectTransferStore(DatabaseTransferOperationRunner operations, ProjectsProfileTransferStore projects) {
    public async Task<ProjectTransferRecordCounts> CountAsync(DatabaseTransferProfileSession session, CancellationToken cancellationToken) {
        var projectCounts = await projects.CountAsync(session, cancellationToken);
        await using var context = await operations.CreateOwnerAsync<WorkbenchDbContext>(session, static options => new WorkbenchDbContext(options), cancellationToken);
        return new(projectCounts.Projects, projectCounts.Phases, projectCounts.Options, projectCounts.HierarchyLinks,
            await context.Set<ProjectObjectRecord>().CountAsync(cancellationToken),
            await context.Set<ProjectObjectLinkRecord>().CountAsync(cancellationToken),
            await context.Set<ProjectStructureProjectionLayoutRecord>().CountAsync(cancellationToken),
            await context.Set<ProjectNodeBindingRecord>().CountAsync(cancellationToken),
            await context.Set<ProjectNodeReferenceRecord>().CountAsync(cancellationToken),
            await context.Set<ProjectNodeLifecycleEventRecord>().CountAsync(cancellationToken),
            await context.Set<ProjectCrossModuleMutationRecord>().CountAsync(cancellationToken),
            await context.Set<ProjectWorkbenchViewStateRecord>().CountAsync(cancellationToken),
            projectCounts.Retirements, projectCounts.CreationReservations,
            await context.Set<ProjectWorkflowContributionRecord>().CountAsync(cancellationToken),
            await context.Set<ProjectWorkflowAdmissionRecord>().CountAsync(cancellationToken),
            await NativeAssignments(context).CountAsync(cancellationToken) + await context.Set<ProjectWorkAssignmentHistoryRecord>().CountAsync(cancellationToken),
            await context.Set<ProjectProcessAssetContributionRecord>().CountAsync(cancellationToken));
    }

    public async Task<ProjectTransferDataSet> LoadAsync(DatabaseTransferProfileSession session, CancellationToken cancellationToken) {
        var projectData = await projects.LoadAsync(session, cancellationToken);
        await using var context = await operations.CreateOwnerAsync<WorkbenchDbContext>(session, static options => new WorkbenchDbContext(options), cancellationToken);
        return new ProjectTransferDataSet {
            Projects = projectData.Projects.ToList(),
            Phases = projectData.Phases.ToList(),
            Options = projectData.Options.ToList(),
            HierarchyLinks = projectData.HierarchyLinks.ToList(),
            Retirements = projectData.Retirements.ToList(),
            CreationReservations = projectData.CreationReservations.ToList(),
            Objects = await context.Set<ProjectObjectRecord>().AsNoTracking().ToListAsync(cancellationToken),
            ObjectLinks = await context.Set<ProjectObjectLinkRecord>().AsNoTracking().ToListAsync(cancellationToken),
            ProjectionLayouts = await context.Set<ProjectStructureProjectionLayoutRecord>().AsNoTracking().ToListAsync(cancellationToken),
            NodeBindings = await context.Set<ProjectNodeBindingRecord>().AsNoTracking().ToListAsync(cancellationToken),
            NodeReferences = await context.Set<ProjectNodeReferenceRecord>().AsNoTracking().ToListAsync(cancellationToken),
            NodeLifecycleEvents = await context.Set<ProjectNodeLifecycleEventRecord>().AsNoTracking().ToListAsync(cancellationToken),
            CrossModuleMutations = await context.Set<ProjectCrossModuleMutationRecord>().AsNoTracking().ToListAsync(cancellationToken),
            ViewStates = await context.Set<ProjectWorkbenchViewStateRecord>().AsNoTracking().ToListAsync(cancellationToken),
            WorkflowContributions = await context.Set<ProjectWorkflowContributionRecord>().AsNoTracking().ToListAsync(cancellationToken),
            WorkflowAdmissions = await context.Set<ProjectWorkflowAdmissionRecord>().AsNoTracking().ToListAsync(cancellationToken),
            WorkAssignmentHistory = await LoadWorkHistoryAsync(context, session.ProfileId, cancellationToken),
            ProcessAssetContributions = await context.Set<ProjectProcessAssetContributionRecord>().AsNoTracking().ToListAsync(cancellationToken)
        };
    }

    public async Task ClearAsync(DatabaseTransferProfileSession session, CancellationToken cancellationToken) {
        RequireMutation(session);
        await using var context = await operations.CreateOwnerAsync<WorkbenchDbContext>(session, static options => new WorkbenchDbContext(options), cancellationToken);
        if (await context.Set<ProjectWorkflowContributionRecord>().AnyAsync(cancellationToken) ||
            await context.Set<ProjectWorkflowAdmissionRecord>().AnyAsync(cancellationToken) ||
            await context.Set<ProjectWorkAssignmentHistoryRecord>().AnyAsync(cancellationToken) ||
            await context.Set<ProjectProcessAssetContributionRecord>().AnyAsync(cancellationToken)) {
            throw new InvalidOperationException("Project transfer cannot replace retained Workbench history.");
        }
        await RemoveAndSaveAsync<ProjectNodeReferenceRecord>(context, cancellationToken);
        await RemoveAndSaveAsync<ProjectNodeBindingRecord>(context, cancellationToken);
        await RemoveAndSaveAsync<ProjectNodeLifecycleEventRecord>(context, cancellationToken);
        await RemoveAndSaveAsync<ProjectObjectLinkRecord>(context, cancellationToken);
        await RemoveAndSaveAsync<ProjectStructureProjectionLayoutRecord>(context, cancellationToken);
        await RemoveAndSaveAsync<ProjectCrossModuleMutationRecord>(context, cancellationToken);
        await RemoveAndSaveAsync<ProjectWorkbenchViewStateRecord>(context, cancellationToken);
        await RemoveAndSaveAsync<ProjectObjectRecord>(context, cancellationToken);
        await projects.ClearAsync(session, cancellationToken);
    }

    public async Task SaveAsync(DatabaseTransferProfileSession session, ProjectTransferDataSet data, CancellationToken cancellationToken) {
        RequireMutation(session);
        if (data.ProcessAssetContributions.Any(row => row.ImportedHistory is null)) {
            throw new InvalidDataException("Imported Process asset evidence requires an explicit history disposition.");
        }
        await projects.SaveAsync(session, new(data.Projects, data.Phases, data.Options, data.HierarchyLinks, data.Retirements, data.CreationReservations), cancellationToken);
        await using var context = await operations.CreateOwnerAsync<WorkbenchDbContext>(session, static options => new WorkbenchDbContext(options), cancellationToken);
        await AddAndSaveAsync(context, data.Objects, cancellationToken);
        await AddAndSaveAsync(context, data.ObjectLinks, cancellationToken);
        await AddAndSaveAsync(context, data.ProjectionLayouts, cancellationToken);
        await AddAndSaveAsync(context, data.NodeBindings, cancellationToken);
        await AddAndSaveAsync(context, data.NodeReferences, cancellationToken);
        await AddAndSaveAsync(context, data.NodeLifecycleEvents, cancellationToken);
        await AddAndSaveAsync(context, data.CrossModuleMutations, cancellationToken);
        await AddAndSaveAsync(context, data.ViewStates, cancellationToken);
        await AddAndSaveAsync(context, data.WorkflowContributions, cancellationToken);
        await AddAndSaveAsync(context, data.WorkflowAdmissions, cancellationToken);
        context.AddRange(data.WorkAssignmentHistory.Select(item => new ProjectWorkAssignmentHistoryRecord(item)));
        await context.SaveChangesAsync(cancellationToken);
        await AddAndSaveAsync(context, data.ProcessAssetContributions, cancellationToken);

    }

    private static IQueryable<ProjectWorkAssignmentRecord> NativeAssignments(WorkbenchDbContext context) =>
        context.Set<ProjectWorkAssignmentRecord>().AsNoTracking().Where(assignment => context.Set<ProjectObjectRecord>().Any(node =>
            node.ProjectId == assignment.ProjectId && node.NodeKey == assignment.NodeKey && node.ObjectType == CanDoItAll.SharedKernel.ProjectObjectType.WorkItem));

    private static async Task<List<ProjectWorkAssignmentTransferItem>> LoadWorkHistoryAsync(WorkbenchDbContext context, Guid sourceProfileId,
        CancellationToken cancellationToken) {
        var current = await NativeAssignments(context).ToArrayAsync(cancellationToken);
        var history = await context.Set<ProjectWorkAssignmentHistoryRecord>().AsNoTracking().ToArrayAsync(cancellationToken);
        return [.. current.Select(row => ProjectWorkAssignmentHistoryRecord.Capture(row, sourceProfileId)), .. history.Select(row => row.ToTransfer())];
    }

    private static void RequireMutation(DatabaseTransferProfileSession session) {
        if (session.Mode != DatabaseTransferProfileMode.Serializable) {
            throw new InvalidOperationException("Workbench import requires the exact locked Serializable target session.");
        }
    }

    private static async Task AddAndSaveAsync<T>(
        WorkbenchDbContext dbContext,
        IReadOnlyCollection<T> entities,
        CancellationToken cancellationToken)
        where T : class {
        if (entities.Count == 0) {
            return;
        }

        await dbContext.Set<T>().AddRangeAsync(entities, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task RemoveAndSaveAsync<T>(
        WorkbenchDbContext dbContext,
        CancellationToken cancellationToken)
        where T : class {
        var entities = await dbContext.Set<T>().ToListAsync(cancellationToken);
        if (entities.Count == 0) {
            return;
        }

        dbContext.RemoveRange(entities);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

}
