using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class ProviderDefaultsBootstrapStage : IAsyncDisposable {
    private readonly ProvidersDbContext dbContext;
    private readonly IDbContextTransaction? transaction;
    private readonly bool providersChanged;
    private bool finished;

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
        if (providersChanged || workspaceChanged) {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        if (transaction is not null) {
            await transaction.CommitAsync(cancellationToken);
        }
        finished = true;
        return providersChanged || workspaceChanged;
    }

    public async ValueTask DisposeAsync() {
        finished = true;
        if (transaction is not null) {
            await transaction.DisposeAsync();
        }
        await dbContext.DisposeAsync();
    }

    private void RequireActive() {
        if (finished) {
            throw new InvalidOperationException("The provider bootstrap stage has ended.");
        }
    }
}
