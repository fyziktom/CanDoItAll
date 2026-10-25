using System.Data;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace CanDoItAll.Modules.Projects;

internal sealed class ProjectDeletionRecoveryTransaction : IAsyncDisposable {
    private readonly ProjectsDbContext context;
    private IDbContextTransaction? transaction;
    private NpgsqlConnection? connection;
    private readonly object cleanupSync = new();
    private Task? cleanup;

    private ProjectDeletionRecoveryTransaction(DbContextOptions<ProjectsDbContext> options) {
        context = new(options);
    }

    internal ProjectsDbContext Context => context;
    internal bool CommitAttempted { get; private set; }
    internal bool CommitAcknowledged { get; private set; }

    internal static async Task<ProjectDeletionRecoveryTransaction> OpenAsync(DbContextOptions<ProjectsDbContext> options,
        CoordinatedDatabaseTransaction transactions, IsolationLevel isolation, CancellationToken cancellationToken) {
        var owned = new ProjectDeletionRecoveryTransaction(options);
        try {
            var context = owned.context;
            if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction is not null ||
                System.Transactions.Transaction.Current is not null ||
                System.Transactions.TransactionsDatabaseFacadeExtensions.GetEnlistedTransaction(context.Database) is not null ||
                context.Database.GetDbConnection() is not NpgsqlConnection connection || connection.State != ConnectionState.Closed ||
                new NpgsqlConnectionStringBuilder(connection.ConnectionString).Pooling || context.ChangeTracker.Entries().Any()) {
                throw new InvalidOperationException("Offline recovery requires a fresh, unopened, unpooled PostgreSQL context.");
            }
            transactions.RequireOwnerProfile(context);
            owned.connection = connection;
            context.Database.SetCommandTimeout(5);
            owned.transaction = await context.Database.BeginTransactionAsync(isolation, cancellationToken);
            await context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'; SET LOCAL statement_timeout = '5s'", cancellationToken);
            return owned;
        } catch (Exception primary) {
            try {
                await owned.DisposeAsync();
            } catch (Exception release) {
                throw new ProjectDeletionRecoveryCleanupException(new AggregateException(primary, release));
            }
            throw;
        }
    }

    internal async Task CommitAsync(CancellationToken cancellationToken) {
        if (cleanup is not null || transaction is null || CommitAttempted) {
            throw new InvalidOperationException("The recovery release transaction cannot be committed again.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        CommitAttempted = true;
        await transaction.CommitAsync(cancellationToken);
        CommitAcknowledged = true;
    }

    public ValueTask DisposeAsync() {
        lock (cleanupSync) {
            return new(cleanup ??= CloseAsync());
        }
    }

    private async Task CloseAsync() {
        var failures = new List<Exception>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        if (transaction is not null) {
            if (!CommitAcknowledged) {
                try {
                    await transaction.RollbackAsync(deadline.Token);
                } catch (Exception failure) {
                    failures.Add(failure);
                }
            }
            try {
                await transaction.DisposeAsync();
            } catch (Exception failure) {
                failures.Add(failure);
            }
        }
        try {
            await context.DisposeAsync();
        } catch (Exception failure) {
            failures.Add(failure);
            try {
                if (connection is not null) {
                    await connection.DisposeAsync();
                }
            } catch (Exception lastRelease) {
                failures.Add(lastRelease);
            }
        }
        if (failures.Count > 0) {
            throw new ProjectDeletionRecoveryCleanupException(new AggregateException(failures));
        }
    }
}
