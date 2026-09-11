using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workspace;

internal sealed class WorkspaceProjectTransferTargetStateParticipant(ProjectTransferTargetInspectionRunner inspections)
    : IProjectTransferTargetStateParticipant {
    public ProjectTransferTargetStateArea Area =>
        ProjectTransferTargetStateArea.Workspace;

    public IReadOnlyCollection<Type> EntityTypesToLock { get; } =
    [
        typeof(ConnectorCommandAuditRecord),
        typeof(ConnectorCommandRecord)
    ];

    public Task<IReadOnlyList<ProjectTransferTargetStateResidue>> FindResiduesAsync(
        ProjectTransferTargetInspection request, CancellationToken cancellationToken)
        => inspections.ReadOwnerAsync<WorkspaceConnectorCommandDbContext, IReadOnlyList<ProjectTransferTargetStateResidue>>(
            request, static options => new(options), ReadResiduesAsync, cancellationToken);

    private static async Task<IReadOnlyList<ProjectTransferTargetStateResidue>> ReadResiduesAsync(
        WorkspaceConnectorCommandDbContext dbContext, CancellationToken cancellationToken) {
        var residues = new List<ProjectTransferTargetStateResidue>();
        if (await dbContext.Set<ConnectorCommandRecord>()
                .AsNoTracking()
                .AnyAsync(cancellationToken)) {
            residues.Add(new("workspace connector commands linked to projects"));
        }

        if (await dbContext.Set<ConnectorCommandAuditRecord>()
                .AsNoTracking()
                .AnyAsync(cancellationToken)) {
            residues.Add(new("workspace connector command audits linked to projects"));
        }

        return residues;
    }
}
