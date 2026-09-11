using System.Data.Common;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Infrastructure.ControlPlane;

internal sealed class DatabaseTransferOwnerSession : IDisposable {
    private readonly DbContext maintenance;
    private readonly DbConnection? connection;
    private readonly IDbContextTransaction? transaction;
    private readonly IReadOnlyDictionary<Type, IDbContextOptionsExtension> options;
    private readonly CoordinatedDatabaseTransaction transactions;
    private readonly IDisposable? participation;
    private bool disposed;

    public DatabaseTransferOwnerSession(ResolvedDatabaseProfile profile, DbContext maintenance) {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(maintenance);
        if (profile.Profile.Id == Guid.Empty) {
            throw new ArgumentException("A transfer session requires an explicit database profile.", nameof(profile));
        }
        this.maintenance = maintenance;
        options = maintenance.GetService<IDbContextOptions>().Extensions.ToDictionary(extension => extension.GetType());
        if (options.Values.OfType<CoreOptionsExtension>().SingleOrDefault()?.Model is not null) {
            throw new InvalidOperationException("Transfer owner sessions cannot use a supplied global model.");
        }
        transactions = CoordinatedDatabaseTransaction.ForProfile(profile);
        transactions.RequireOwnerProfile(maintenance);
        transaction = maintenance.Database.CurrentTransaction;
        if (maintenance.Database.IsNpgsql()) {
            connection = maintenance.Database.GetDbConnection();
        }
        if (transaction is not null || maintenance.Database.IsInMemory()) {
            participation = transactions.Enter(maintenance);
        }
    }

    public async Task<TContext> CreateAsync<TContext>(Func<DbContextOptions<TContext>, TContext> create,
        CancellationToken cancellationToken = default) where TContext : DbContext {
        ArgumentNullException.ThrowIfNull(create);
        cancellationToken.ThrowIfCancellationRequested();
        RequireActive();
        if (typeof(TContext) == typeof(AppDbContext)) {
            throw new InvalidOperationException("Transfer data operations require a bounded owner context.");
        }
        var builder = new DbContextOptionsBuilder<TContext>(new DbContextOptions<TContext>(options))
            .AddInterceptors(new SessionGuard(this));
        if (connection is not null) {
            builder.UseNpgsql(connection, contextOwnsConnection: false);
        }
        var context = participation is not null
            ? await transactions.CreateEnlistedAsync(builder.Options, create, cancellationToken)
            : create(builder.Options);
        try {
            RequireActive();
            return context;
        } catch {
            await context.DisposeAsync();
            throw;
        }
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        participation?.Dispose();
        disposed = true;
    }

    internal void RequireActive() {
        ObjectDisposedException.ThrowIf(disposed, this);
        transactions.RequireOwnerProfile(maintenance);
        if (!ReferenceEquals(maintenance.Database.CurrentTransaction, transaction) ||
                connection is not null && (!ReferenceEquals(maintenance.Database.GetDbConnection(), connection) ||
                    transaction is not null && !ReferenceEquals(transaction.GetDbTransaction().Connection, connection))) {
            throw new InvalidOperationException("The transfer owner session no longer has its original connection and transaction.");
        }
    }

    private sealed class SessionGuard(DatabaseTransferOwnerSession session) : DbCommandInterceptor, ISaveChangesInterceptor {
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result) => RequireActive(result);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(RequireActive(result));

        public override InterceptionResult<int> NonQueryExecuting(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result) => RequireActive(result);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(RequireActive(result));

        public override InterceptionResult<object> ScalarExecuting(DbCommand command,
            CommandEventData eventData, InterceptionResult<object> result) => RequireActive(result);

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(RequireActive(result));

        public InterceptionResult<int> SavingChanges(DbContextEventData eventData,
            InterceptionResult<int> result) => RequireActive(result);

        public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(RequireActive(result));

        private InterceptionResult<T> RequireActive<T>(InterceptionResult<T> result) {
            session.RequireActive();
            return result;
        }
    }
}
