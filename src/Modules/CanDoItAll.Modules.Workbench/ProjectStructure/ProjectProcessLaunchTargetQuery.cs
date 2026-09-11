using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectProcessLaunchTargetQuery(
    IDbContextFactory<WorkbenchDbContext> factory,
    DbContextOptions<WorkbenchDbContext> contextOptions,
    CoordinatedDatabaseTransaction transactions,
    ProjectStructureAssemblyService assembly) {
    public async Task<ProcessLaunchLinkTarget> CaptureAsync(Guid projectId, string sourceNodeKey, CancellationToken cancellationToken = default) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var fingerprint = await ReadBindingAsync(context, assembly, projectId, sourceNodeKey, cancellationToken);
        return new(projectId, sourceNodeKey, fingerprint);
    }

    public async Task RequireForMutationAsync(ProcessLaunchLinkTarget target, CancellationToken cancellationToken = default) {
        await using var context = await transactions.CreateEnlistedAsync(contextOptions, options => new WorkbenchDbContext(options), cancellationToken);
        var current = await ReadBindingAsync(context, assembly, target.ProjectId, target.SourceNodeKey, cancellationToken);
        if (current != target.SourceBindingFingerprint) {
            throw new ProcessLaunchIntentConflictException(null, "The reviewed process source node or binding changed before admission or delivery.");
        }
    }
    internal static async Task<string> ReadBindingAsync(WorkbenchDbContext database, ProjectStructureAssemblyService projectStructureAssemblyService, Guid projectId,
        string parentNodeId, CancellationToken cancellationToken) {
        if (parentNodeId == ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId)) {
            return ProjectWorkflowContributionFingerprint.Hash(JsonSerializer.Serialize(new { projectId, parentNodeId }));
        }

        var native = await database.Set<ProjectObjectRecord>().AsNoTracking()
            .SingleOrDefaultAsync(node => node.ProjectId == projectId && node.NodeKey == parentNodeId, cancellationToken);
        if (native is null) {
            var assembly = await projectStructureAssemblyService.LoadAsync(database, projectId, cancellationToken);
            native = assembly.Nodes.SingleOrDefault(node => node.NodeKey == parentNodeId)
                ?? throw new ProjectStructureAgentException(404, "ParentNodeNotFound", "The prepared workflow output parent no longer exists.");
        }

        var binding = await database.Set<ProjectNodeBindingRecord>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.ProjectObjectId == native.Id, cancellationToken);
        if (binding is not null) {
            native.Binding = new(binding.Route, binding.ExternalArtifactKind, binding.ExternalArtifactId, binding.MediaRelativePath,
                binding.MediaContentType, binding.MediaOriginalFileName, binding.StorageObjectReferenceJson);
        }

        return ProjectWorkflowContributionFingerprint.Hash(JsonSerializer.Serialize(new {
            Target = ProjectWorkflowContributionFingerprint.Target(native),
            Binding = binding is null ? null : new {
                binding.Id, binding.Route, binding.ExternalArtifactKind, binding.ExternalArtifactId,
                binding.MediaRelativePath, binding.MediaContentType, binding.MediaOriginalFileName, binding.StorageObjectReferenceJson
            }
        }));
    }

}
