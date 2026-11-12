using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessRootCancellationBarrierReader(IDbContextFactory<ProcessPersistenceDbContext> factory)
    : IProcessRootCancellationBarrierReader {
    public async Task<bool> IsStoppedAsync(ProcessRunId rootRunId, CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfEqual(rootRunId.Value, Guid.Empty);
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.RuntimeStates.AsNoTracking().AnyAsync(root => root.RunId == rootRunId.Value &&
            root.RootRunId == rootRunId.Value &&
            (root.Status == ProcessRuntimeStatus.CancelRequested || root.Status == ProcessRuntimeStatus.Cancelled),
            cancellationToken).ConfigureAwait(false);
    }
}
