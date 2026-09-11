using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectProcessLaunchTargetQuery(
    IDbContextFactory<WorkbenchDbContext> factory,
    DbContextOptions<WorkbenchDbContext> contextOptions,
    CoordinatedDatabaseTransaction transactions,
    ProjectWorkbenchService workbench) {
    public async Task<ProcessLaunchLinkTarget> CaptureAsync(Guid projectId, string sourceNodeKey, CancellationToken cancellationToken = default) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var fingerprint = await workbench.ReadWorkflowTargetBindingAsync(context, projectId, sourceNodeKey, cancellationToken);
        return new(projectId, sourceNodeKey, fingerprint);
    }

    public async Task RequireForMutationAsync(ProcessLaunchLinkTarget target, CancellationToken cancellationToken = default) {
        await using var context = await transactions.CreateEnlistedAsync(contextOptions, options => new WorkbenchDbContext(options), cancellationToken);
        var current = await workbench.ReadWorkflowTargetBindingAsync(context, target.ProjectId, target.SourceNodeKey, cancellationToken);
        if (current != target.SourceBindingFingerprint) {
            throw new ProcessLaunchIntentConflictException(null, "The reviewed process source node or binding changed before admission or delivery.");
        }
    }
}
