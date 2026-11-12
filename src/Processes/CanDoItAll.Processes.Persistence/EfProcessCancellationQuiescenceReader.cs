using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessCancellationQuiescenceReader(IDbContextFactory<ProcessPersistenceDbContext> factory) : IProcessCancellationQuiescenceReader {
    public async Task<ProcessCancellationRecoveryPending> ReadAsync(ProcessRunId rootRunId, CancellationToken cancellationToken) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.Database.SetCommandTimeout(TimeSpan.FromSeconds(5));
        var pending = await EfProcessRootCancellationGuard.ReadPendingAsync(context, rootRunId, cancellationToken, includeRootLocal: true).ConfigureAwait(false);
        return pending switch {
            ProcessRootCancellationPending.None => ProcessCancellationRecoveryPending.None,
            _ when pending.HasFlag(ProcessRootCancellationPending.MissingRoot) => ProcessCancellationRecoveryPending.InvalidEvidence,
            _ when pending.HasFlag(ProcessRootCancellationPending.RuntimeWork) => ProcessCancellationRecoveryPending.RuntimeWork,
            _ when pending.HasFlag(ProcessRootCancellationPending.Decisions) => ProcessCancellationRecoveryPending.DecisionWork,
            _ when pending.HasFlag(ProcessRootCancellationPending.Budget) => ProcessCancellationRecoveryPending.BudgetWork,
            _ => ProcessCancellationRecoveryPending.ScopeWork
        };
    }
}
