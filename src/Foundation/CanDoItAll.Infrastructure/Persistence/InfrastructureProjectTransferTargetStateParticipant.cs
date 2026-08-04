using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Infrastructure.Persistence;

internal sealed class InfrastructureProjectTransferTargetStateParticipant
    : IProjectTransferTargetStateParticipant
{
    public ProjectTransferTargetStateArea Area =>
        ProjectTransferTargetStateArea.Infrastructure;

    public IReadOnlyCollection<Type> EntityTypesToLock { get; } =
    [
        typeof(SearchDocument),
        typeof(StorageCatalogRecord),
        typeof(StorageRoutingRule)
    ];

    public async Task<IReadOnlyList<ProjectTransferTargetStateResidue>>
        FindResiduesAsync(
            AppDbContext dbContext,
            CancellationToken cancellationToken)
    {
        var residues = new List<ProjectTransferTargetStateResidue>();
        if (await dbContext.Set<SearchDocument>()
                .AsNoTracking()
                .AnyAsync(
                    document =>
                        document.ProjectId.HasValue ||
                        document.SourceType == SearchDocument.ProjectSourceType,
                    cancellationToken))
        {
            residues.Add(new("project search documents"));
        }

        if (await dbContext.Set<StorageRoutingRule>()
                .AsNoTracking()
                .AnyAsync(
                    rule =>
                        rule.ProjectId.HasValue ||
                        rule.ScopeKind == StorageRoutingScopeKind.Project ||
                        rule.ScopeKind == StorageRoutingScopeKind.Node,
                    cancellationToken))
        {
            residues.Add(new("project storage routing rules"));
        }

        return residues;
    }
}
