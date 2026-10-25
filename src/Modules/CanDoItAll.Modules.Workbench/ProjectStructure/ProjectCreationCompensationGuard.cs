using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectCreationCompensationGuard(DbContextOptions<WorkbenchDbContext> options,
    CoordinatedDatabaseTransaction transactions, IProjectPartyDeletionStateQuery parties,
    StorageCatalogService storageCatalog) : IProjectCreationCompensationGuard {
    public Task<bool> CanCompensateForMutationAsync(ProjectWriteAdmission project, CancellationToken cancellationToken = default)
        => CanCleanupAsync(project, () => parties.HasProjectDeletionStateForMutationAsync(project, cancellationToken), cancellationToken);

    public Task<bool> CanCancelReservationForMutationAsync(ProjectReservationCancellationPreparationContext original,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        return CanCleanupAsync(original.Target.Project,
            () => parties.HasReservationCancellationStateForMutationAsync(original, cancellationToken), cancellationToken);
    }

    private async Task<bool> CanCleanupAsync(ProjectWriteAdmission project, Func<Task<bool>> hasPartyState,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(project);
        var context = await transactions.CreateEnlistedAsync(options, static configured => new WorkbenchDbContext(configured), cancellationToken);
        Exception? primary = null;
        try {
            transactions.RequireOwnerProfile(context);
            var id = project.ProjectId;
            return !await hasPartyState()
                && !await storageCatalog.HasProjectRoutingForMutationAsync(id, cancellationToken)
                && !await context.Set<ProjectObjectRecord>().AnyAsync(row => row.ProjectId == id, cancellationToken)
                && !await context.Set<ProjectObjectLinkRecord>().AnyAsync(row => row.ProjectId == id, cancellationToken)
                && !await context.Set<ProjectWorkbenchViewStateRecord>().AnyAsync(row => row.ProjectId == id, cancellationToken)
                && !await context.Set<ProjectStructureProjectionLayoutRecord>().AnyAsync(row => row.ProjectId == id, cancellationToken)
                && !await context.Set<ProjectNodeLifecycleEventRecord>().AnyAsync(row => row.ProjectId == id, cancellationToken)
                && !await context.Set<ProjectCrossModuleMutationRecord>().AnyAsync(row => row.ProjectId == id, cancellationToken)
                && !await context.Set<ProjectWorkAssignmentRecord>().AnyAsync(row => row.ProjectId == id, cancellationToken)
                && !await context.Set<ProjectWorkflowAdmissionRecord>().AnyAsync(row => row.ProjectId == id, cancellationToken)
                && !await context.Set<ProjectWorkflowContributionRecord>().AnyAsync(row => row.ProjectId == id, cancellationToken)
                && !await context.Set<ProjectProcessAssetContributionRecord>().AnyAsync(row => row.ProjectId == id, cancellationToken);
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            try {
                await context.DisposeAsync();
            } catch (Exception cleanup) when (primary is not null) {
                throw new AggregateException("Creation cleanup observation and enlisted context release failed.", primary, cleanup);
            }
        }
    }
}
