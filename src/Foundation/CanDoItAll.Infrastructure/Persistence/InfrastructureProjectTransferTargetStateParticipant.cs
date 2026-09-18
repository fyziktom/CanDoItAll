using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Infrastructure.Persistence;

internal sealed class InfrastructureProjectTransferTargetStateParticipant(ProjectTransferTargetInspectionRunner inspections)
    : IProjectTransferTargetStateParticipant {
    public ProjectTransferTargetStateArea Area => ProjectTransferTargetStateArea.Infrastructure;

    public IReadOnlyCollection<Type> EntityTypesToLock { get; } = [
        typeof(SearchDocument), typeof(StorageCatalogRecord), typeof(StorageRoutingRule), typeof(StoragePlacementIntentRecord)
    ];

    public async Task<IReadOnlyList<ProjectTransferTargetStateResidue>> FindResiduesAsync(
        ProjectTransferTargetInspection request, CancellationToken cancellationToken) {
        var residues = new List<ProjectTransferTargetStateResidue>();
        if (await inspections.ReadOwnerAsync<SearchDbContext, bool>(request, static options => new(options),
                static (db, token) => db.Set<SearchDocument>().AsNoTracking().AnyAsync(document =>
                    document.ProjectId.HasValue || document.SourceType == SearchDocument.ProjectSourceType, token), cancellationToken)) {
            residues.Add(new("project search documents"));
        }
        residues.AddRange(await inspections.ReadOwnerAsync<StorageDbContext, IReadOnlyList<ProjectTransferTargetStateResidue>>(
            request, static options => new(options), ReadStorageResiduesAsync, cancellationToken));
        return residues;
    }

    private static async Task<IReadOnlyList<ProjectTransferTargetStateResidue>> ReadStorageResiduesAsync(
        StorageDbContext db, CancellationToken cancellationToken) {
        var residues = new List<ProjectTransferTargetStateResidue>();
        if (await db.Set<StorageRoutingRule>().AsNoTracking().AnyAsync(rule =>
                rule.ProjectId.HasValue || rule.ScopeKind == StorageRoutingScopeKind.Project || rule.ScopeKind == StorageRoutingScopeKind.Node,
                cancellationToken)) {
            residues.Add(new("project storage routing rules"));
        }
        if (await db.Set<StoragePlacementIntentRecord>().AsNoTracking().AnyAsync(intent => intent.ProjectId.HasValue, cancellationToken)) {
            residues.Add(new("retained project storage placement intents"));
        }
        return residues;
    }
}
