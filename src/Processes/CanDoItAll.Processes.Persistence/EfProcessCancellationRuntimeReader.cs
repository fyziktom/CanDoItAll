using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessCancellationRuntimeReader(IDbContextFactory<ProcessPersistenceDbContext> factory) : IProcessCancellationRuntimeReader {
    public async Task<ProcessRunId?> NextAsync(ProcessRunId rootRunId, ProcessRunId? after, CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfEqual(rootRunId.Value, Guid.Empty);
        if (after?.Value == Guid.Empty) {
            throw new ArgumentOutOfRangeException(nameof(after));
        }
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.Database.SetCommandTimeout(TimeSpan.FromSeconds(5));
        var query = context.RuntimeStates.AsNoTracking().Where(run => run.RootRunId == rootRunId.Value);
        if (after is { } cursor) {
            query = query.Where(run => run.RunId.CompareTo(cursor.Value) > 0);
        }
        var id = await query.OrderBy(run => run.RunId).Select(run => (Guid?)run.RunId)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return id is { } value ? new(value) : null;
    }

    public async Task<ProcessCancellationRuntimeRun?> ReadAsync(ProcessRunId rootRunId, ProcessRunId runId, CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfEqual(rootRunId.Value, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(runId.Value, Guid.Empty);
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.Database.SetCommandTimeout(TimeSpan.FromSeconds(5));
        return await context.RuntimeStates.AsNoTracking().Where(run => run.RootRunId == rootRunId.Value && run.RunId == runId.Value)
            .Select(run => new ProcessCancellationRuntimeRun(new(run.RunId), run.Status))
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
}
