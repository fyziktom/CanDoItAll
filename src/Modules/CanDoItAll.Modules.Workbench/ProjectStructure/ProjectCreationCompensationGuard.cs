using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectCreationCompensationGuard(DbContextOptions<WorkbenchDbContext> options,
    CoordinatedDatabaseTransaction transactions, IProjectPartyDeletionStateQuery parties,
    StorageCatalogService storageCatalog) : IProjectCreationCompensationGuard {
    public async Task<bool> CanCompensateForMutationAsync(ProjectWriteAdmission project, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(project);
        await using var context = await transactions.CreateEnlistedAsync(options, static configured => new WorkbenchDbContext(configured), cancellationToken);
        var id = project.ProjectId;
        return !await parties.HasProjectDeletionStateForMutationAsync(project, cancellationToken)
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
    }
}
