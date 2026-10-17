using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class ProviderDefaultsBootstrapStage : IAsyncDisposable {
    private readonly ProvidersDbContext dbContext;
    private readonly IDbContextTransaction? transaction;
    private readonly bool providersChanged;
    private bool finished;
    private int disposed;
    private Exception? primaryFailure;

    internal ProviderDefaultsBootstrapStage(ProvidersDbContext dbContext, IDbContextTransaction? transaction,
        CoordinatedDatabaseTransaction transactions, Guid newWorkspaceDefaultProviderId,
        Guid matchedProviderId, bool providersChanged) {
        this.dbContext = dbContext;
        this.transaction = transaction;
        this.providersChanged = providersChanged;
        Transactions = transactions;
        NewWorkspaceDefaultProviderId = newWorkspaceDefaultProviderId;
        MatchedProviderId = matchedProviderId;
    }

    public Guid NewWorkspaceDefaultProviderId { get; }
    public Guid MatchedProviderId { get; }
    public CoordinatedDatabaseTransaction Transactions { get; }
    public bool IsCommitted { get; private set; }

    public IDisposable EnterTransaction() {
        RequireActive();
        return Transactions.Enter(dbContext);
    }

    public Task<bool> ProviderExistsAsync(Guid providerId, CancellationToken cancellationToken = default) {
        RequireActive();
        return dbContext.Set<ProviderProfile>().AnyAsync(item => item.Id == providerId, cancellationToken);
    }

    public async Task<bool> CommitAsync(bool workspaceChanged, CancellationToken cancellationToken = default) {
        RequireActive();
        try {
            if (providersChanged || workspaceChanged) {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            if (transaction is not null) {
                await transaction.CommitAsync(cancellationToken);
            }
            IsCommitted = true;
            finished = true;
            return providersChanged || workspaceChanged;
        } catch (Exception exception) {
            primaryFailure = exception;
            finished = true;
            throw;
        }
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref disposed, 1) != 0) {
            return;
        }
        finished = true;
        List<Exception> failures = [];
        if (transaction is not null) {
            try {
                await transaction.DisposeAsync();
            } catch (Exception exception) {
                failures.Add(exception);
            }
        }
        try {
            await dbContext.DisposeAsync();
        } catch (Exception exception) {
            failures.Add(exception);
        }
        if (failures.Count > 0) {
            if (primaryFailure is not null) {
                failures.Insert(0, primaryFailure);
            }
            throw new ProviderDefaultsBootstrapCleanupException(IsCommitted, new AggregateException(failures));
        }
    }

    private void RequireActive() {
        if (finished) {
            throw new InvalidOperationException("The provider bootstrap stage has ended.");
        }
    }
}

public sealed class ProviderDefaultsBootstrapCleanupException(bool committed, Exception innerException)
    : Exception("Provider bootstrap resource cleanup requires review.", innerException) {
    public bool IsCommitted { get; } = committed;
}
