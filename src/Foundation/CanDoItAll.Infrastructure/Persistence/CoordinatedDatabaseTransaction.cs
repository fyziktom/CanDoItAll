using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace CanDoItAll.Infrastructure.Persistence;

public sealed class CoordinatedDatabaseTransaction {
    private const string InMemoryProvider = "Microsoft.EntityFrameworkCore.InMemory";
    private readonly AsyncLocal<Frame?> current = new();
    private readonly DatabaseProviderKind provider;
    private readonly PostgreSqlBinding? postgres;
    private readonly string? inMemoryName;

    public CoordinatedDatabaseTransaction(ICanonicalRuntimeDatabase database) : this(database.Profile) {
    }

    private CoordinatedDatabaseTransaction(ResolvedDatabaseProfile profile) {
        ArgumentNullException.ThrowIfNull(profile);
        provider = profile.Profile.ProviderKind;
        if (provider == DatabaseProviderKind.PostgreSql) {
            postgres = PostgreSqlBinding.From(profile.ConnectionString);
        } else if (provider == DatabaseProviderKind.InMemory) {
            inMemoryName = profile.ConnectionString;
        } else {
            throw new InvalidOperationException("The database provider cannot participate in coordinated writes.");
        }
    }

    public static CoordinatedDatabaseTransaction ForProfile(ResolvedDatabaseProfile profile) => new(profile);

    public IDisposable Enter(DbContext owner) {
        ArgumentNullException.ThrowIfNull(owner);
        var previous = current.Value;
        previous?.RequireActive();
        Frame next;
        if (provider == DatabaseProviderKind.PostgreSql) {
            var ownerTransaction = owner.Database.CurrentTransaction
                ?? throw new InvalidOperationException("Coordinated writes require the owner's active transaction.");
            var transaction = ownerTransaction.GetDbTransaction();
            var connection = owner.Database.GetDbConnection();
            RequireBinding(connection.ConnectionString);
            if (!ReferenceEquals(transaction.Connection, connection)) {
                throw new InvalidOperationException("The owner transaction is not bound to its current connection.");
            }
            if (previous is not null && (!ReferenceEquals(previous.Connection, connection)
                    || !ReferenceEquals(previous.Transaction, transaction))) {
                throw new InvalidOperationException("A coordinated operation cannot enter a different transaction.");
            }
            next = new(previous, owner, ownerTransaction, connection, transaction, null);
        } else {
            if (owner.Database.ProviderName != InMemoryProvider) {
                throw new InvalidOperationException("The owner does not use the configured test database provider.");
            }
            var binding = ReadInMemoryBinding(owner);
            if (binding.Name != inMemoryName || previous?.InMemory is { } previousBinding && !previousBinding.Matches(binding)) {
                throw new InvalidOperationException("The owner does not use the configured test database.");
            }
            next = new(previous, owner, null, null, null, binding);
        }
        current.Value = next;
        return new Participation(this, next);
    }

    public async Task<TContext> CreateEnlistedAsync<TContext>(
        DbContextOptions<TContext> options,
        Func<DbContextOptions<TContext>, TContext> create,
        CancellationToken cancellationToken = default) where TContext : DbContext {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(create);
        cancellationToken.ThrowIfCancellationRequested();
        var frame = current.Value
            ?? throw new InvalidOperationException("An owner must enter its transaction before another owner can participate.");
        frame.RequireActive();
        var enlistedOptions = new DbContextOptionsBuilder<TContext>(options)
            .AddInterceptors(new ParticipationGuard(frame));
        if (frame.InMemory is null) {
            var relational = options.Extensions.OfType<RelationalOptionsExtension>().SingleOrDefault()
                ?? throw new InvalidOperationException("The participant must have explicit relational connection options.");
            RequireBinding(relational.ConnectionString ?? relational.Connection?.ConnectionString);
            enlistedOptions.UseNpgsql(frame.Connection!, contextOwnsConnection: false);
        }
        var context = create(enlistedOptions.Options);
        try {
            if (frame.InMemory is { } testBinding) {
                if (!testBinding.Matches(ReadInMemoryBinding(context))) {
                    throw new InvalidOperationException("The participant does not use the owner's test database.");
                }
                // InMemory profiles are test-only and cannot provide transactional atomicity.
            } else {
                await context.Database.UseTransactionAsync(frame.Transaction!, cancellationToken).ConfigureAwait(false);
            }
            frame.RequireActive();
            return context;
        } catch {
            await context.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private void RequireBinding(string? connectionString) {
        if (string.IsNullOrWhiteSpace(connectionString) || PostgreSqlBinding.From(connectionString) != postgres) {
            throw new InvalidOperationException("The participant connection does not match the coordinated database profile.");
        }
    }

    private void Exit(Frame frame) {
        if (frame.Retired) {
            return;
        }
        if (!ReferenceEquals(current.Value, frame)) {
            throw new InvalidOperationException("Coordinated transaction scopes must be disposed in reverse entry order.");
        }
        frame.Retired = true;
        current.Value = frame.Previous;
    }

    private sealed class Participation(CoordinatedDatabaseTransaction owner, Frame frame) : IDisposable {
        public void Dispose() => owner.Exit(frame);
    }

    private sealed class Frame(Frame? previous, DbContext owner, IDbContextTransaction? ownerTransaction,
        DbConnection? connection, DbTransaction? transaction, InMemoryBinding? inMemory) {
        public Frame? Previous { get; } = previous;
        public DbConnection? Connection { get; } = connection;
        public DbTransaction? Transaction { get; } = transaction;
        public InMemoryBinding? InMemory { get; } = inMemory;
        public volatile bool Retired;

        public void RequireActive() {
            Previous?.RequireActive();
            if (Retired || ownerTransaction is not null &&
                    (!ReferenceEquals(owner.Database.CurrentTransaction, ownerTransaction)
                        || !ReferenceEquals(Transaction!.Connection, Connection))) {
                throw new InvalidOperationException("The coordinated transaction scope has ended.");
            }
        }
    }

    private sealed class ParticipationGuard(Frame frame) : DbCommandInterceptor, ISaveChangesInterceptor {
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
            frame.RequireActive();
            return result;
        }
    }

    private sealed record PostgreSqlBinding(string Host, int Port, string Database, string? Username,
        string? SearchPath, string? Options) {
        public static PostgreSqlBinding From(string connectionString) {
            var settings = new NpgsqlConnectionStringBuilder(connectionString);
            var host = settings.Host;
            var database = settings.Database;
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(database)) {
                throw new InvalidOperationException("Coordinated writes require an explicit PostgreSQL host and database.");
            }
            var normalizedHost = string.Join(',', host.Split(',').Select(value => {
                var endpoint = value.Trim();
                return Path.IsPathRooted(endpoint) || endpoint.StartsWith('@') ? endpoint : endpoint.ToUpperInvariant();
            }));
            return new(normalizedHost, settings.Port, database,
                settings.Username, settings.SearchPath, settings.Options);
        }
    }

    // EF exposes InMemory store identity only through its provider services.
#pragma warning disable EF1001
    private sealed record InMemoryBinding(string Name, Microsoft.EntityFrameworkCore.InMemory.Storage.Internal.IInMemoryStore Store) {
        public bool Matches(InMemoryBinding other) => Name == other.Name && ReferenceEquals(Store, other.Store);
    }

    private static InMemoryBinding ReadInMemoryBinding(DbContext context) {
        var extension = context.GetService<IDbContextOptions>().Extensions
            .OfType<Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal.InMemoryOptionsExtension>()
            .SingleOrDefault()
            ?? throw new InvalidOperationException("The participant must have explicit InMemory test database options.");
        var store = context.GetService<Microsoft.EntityFrameworkCore.InMemory.Storage.Internal.IInMemoryStoreProvider>().Store;
        return new(extension.StoreName, store);
    }
#pragma warning restore EF1001
}
