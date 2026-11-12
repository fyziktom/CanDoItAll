using System.Data.Common;
using System.Diagnostics;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessRootCancellationFixture;

namespace CanDoItAll.Tests.Integration.Processes;

internal static class ProcessCancellationRecoveryFixture {
    internal static readonly TimeSpan Lease = TimeSpan.FromMinutes(1);
    internal static EfProcessCancellationRecoveryQueue Queue(IServiceProvider services, params IInterceptor[] interceptors)
        => new(new Factory(Options(services, interceptors)));

    internal static async Task<ProcessRuntimeStateSnapshot> RootAsync(ProcessPersistenceDbContext context, bool legacy = false) {
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var state = (await owner.CommitAsync(ProcessProjectAdmissionFixture.Initial())).State;
        return legacy ? await LegacyCancelledAsync(context, state) : await StopAsync(owner, state);
    }

    internal static Task<string[]> RowsAsync(ProcessPersistenceDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT to_jsonb(q)::text AS "Value" FROM process_cancellation_recovery q ORDER BY "RootRunId"
        """).ToArrayAsync();

    internal static ProcessCancellationRecoveryIntent Intent(ProcessCancellationRecoveryClaim claim)
        => new ProcessCancellationCancelRun(claim.RootRunId,
            ProcessCancellationRecoveryIntent.CreateContext(claim.RootRunId, DateTimeOffset.UtcNow));

    internal static async Task WaitExpiredAsync(IServiceProvider services, DateTimeOffset expiry) {
        await using var context = Context(services);
        var timer = Stopwatch.StartNew();
        while (!await context.Database.SqlQuery<bool>($"""SELECT clock_timestamp() >= {expiry} AS "Value" """).SingleAsync()) {
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(5), "The test lease did not expire within the bounded database-time wait.");
            await Task.Delay(50);
        }
    }

    internal sealed class Factory(DbContextOptions<ProcessPersistenceDbContext> options) : IDbContextFactory<ProcessPersistenceDbContext> {
        public ProcessPersistenceDbContext CreateDbContext() => new(options);
    }

    internal sealed class ReaderGate(string selection, Func<Task> before) : DbCommandInterceptor {
        private int entered;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains(selection, StringComparison.Ordinal) && Interlocked.Exchange(ref entered, 1) == 0) {
                await before();
            }
            return result;
        }
    }

    internal sealed class LostAcknowledgement(Exception failure) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
            => throw failure;
    }

    internal sealed class FlushFault(bool after) : SaveChangesInterceptor {
        internal Exception Failure { get; } = new IOException("Injected recovery flush failure.");
        internal bool Flushed { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => after ? ValueTask.FromResult(result) : throw Failure;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            Flushed = result > 0 && eventData.Context!.Database.CurrentTransaction is not null;
            throw Failure;
        }
    }
}
