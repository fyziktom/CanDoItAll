using System.Data;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessExecutionMutationGuard(
    DbContextOptions<ProcessPersistenceDbContext> options,
    CoordinatedDatabaseTransaction transactions,
    TimeProvider clock) : IProcessExecutionMutationGuard {
    public async Task RequireForMutationAsync(ProcessExecutionDispatchAuthority expected, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(expected);
        if (expected.RootRunId.Value == Guid.Empty || expected.SourceAuthority?.ProjectAdmission is null || expected.ProjectReference is null) {
            throw new ProcessExecutionAuthorityMismatchException("Native Process effects require the exact saved project launch authority.");
        }
        await RequireDispatchAsync(expected, cancellationToken);
    }

    public async Task RequireDispatchAsync(ProcessExecutionDispatchAuthority expected, CancellationToken cancellationToken = default) {
        await using var context = await transactions.CreateEnlistedAsync(options, static configured => new ProcessPersistenceDbContext(configured), cancellationToken);
        await RequireDispatchAsync(context, expected, cancellationToken);
    }

    public async Task<IAsyncDisposable> AcquireDispatchAsync(ProcessExecutionDispatchAuthority expected, CancellationToken cancellationToken = default) {
        var context = new ProcessPersistenceDbContext(options);
        try {
            await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await RequireDispatchAsync(context, expected, cancellationToken);
            return context;
        } catch (Exception failure) {
            try {
                await context.DisposeAsync();
            } catch (Exception cleanup) {
                throw new AggregateException("Process dispatch acquisition and transaction release failed.", failure, cleanup);
            }
            throw;
        }
    }

    private async Task RequireDispatchAsync(ProcessPersistenceDbContext context, ProcessExecutionDispatchAuthority expected,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(expected);
        if (expected.RootRunId.Value == Guid.Empty) {
            throw new ProcessExecutionAuthorityMismatchException("Process dispatch requires its exact saved root identity.");
        }
        if (!context.Database.IsNpgsql()) {
            throw new NotSupportedException("Process native mutation fencing requires the PostgreSQL owner transaction.");
        }
        var key = ProcessRootAdvisoryKey.From(expected.RootRunId.Value);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
        var evidence = expected.Evidence;
        await ProcessExecutionSourceRowLocks.AcquireAsync(context, expected.RootRunId, evidence, cancellationToken);
        var result = await EfProcessExecutionAuthorityQuery.ReadCoreAsync(context, evidence, clock.GetUtcNow(), cancellationToken);
        if (result.Snapshot is not { ObservedCurrentDispatch: true } current || current.OwnerFingerprint != expected.OwnerFingerprint ||
                current.ProjectReference != expected.ProjectReference || current.RootRunId != expected.RootRunId ||
                current.SourceAuthority?.ProjectAdmission != expected.SourceAuthority?.ProjectAdmission) {
            throw new ProcessExecutionAuthorityMismatchException("The Process claim, readiness, source or lifetime no longer permits this native effect.");
        }
    }

}
