using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessArtifactLedgerStore(ProcessPersistenceDbContext dbContext) : IProcessArtifactLedgerStore
{
    public async Task AppendAsync(
        IReadOnlyList<ProcessArtifactLedgerEvent> ledgerEvents,
        CancellationToken cancellationToken = default)
    {
        foreach (var ledgerEvent in ledgerEvents)
        {
            dbContext.ArtifactLedgerEvents.Add(ProcessPersistenceMappers.ToLedgerEntity(ledgerEvent));
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
