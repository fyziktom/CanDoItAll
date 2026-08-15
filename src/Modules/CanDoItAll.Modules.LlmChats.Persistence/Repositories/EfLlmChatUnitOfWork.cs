using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Persistence.Entities;
using CanDoItAll.Modules.LlmChats.Ports;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.LlmChats.Persistence.Repositories;

public sealed class EfLlmChatUnitOfWork(
    AppDbContext dbContext,
    ILlmChatCommitFence commitFence) : ILlmChatUnitOfWork
{
    private readonly List<Action> _postCommitCallbacks = [];

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (dbContext.Database.CurrentTransaction is not null)
        {
            var nestedResult = await operation(cancellationToken).ConfigureAwait(false);
            await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return nestedResult;
        }

        _postCommitCallbacks.Clear();
        try
        {
            var result = await commitFence.ExecuteAsync(async fenceCancellationToken =>
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(fenceCancellationToken)
                    .ConfigureAwait(false);
                var transactionResult = await operation(fenceCancellationToken).ConfigureAwait(false);
                await SaveChangesAsync(fenceCancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(fenceCancellationToken).ConfigureAwait(false);
                return transactionResult;
            }, cancellationToken).ConfigureAwait(false);
            var callbacks = _postCommitCallbacks.ToArray();
            _postCommitCallbacks.Clear();
            foreach (var callback in callbacks)
            {
                callback();
            }

            return result;
        }
        catch
        {
            _postCommitCallbacks.Clear();
            throw;
        }
    }

    public void RegisterPostCommit(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A post-commit callback requires an active LLM Chat transaction.");
        }

        _postCommitCallbacks.Add(callback);
    }

    private Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        var invalidRevisionWrite = dbContext.ChangeTracker.Entries<LlmChatDefinitionRevisionRow>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (invalidRevisionWrite is not null)
        {
            throw new InvalidOperationException("LLM Chat definition revisions are append-only.");
        }

        var invalidAuditWrite = dbContext.ChangeTracker.Entries<LlmChatInvocationRecordRow>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (invalidAuditWrite is not null)
        {
            throw new InvalidOperationException("LLM Chat invocation records are append-only.");
        }

        var invalidEventWrite = dbContext.ChangeTracker.Entries<LlmChatOperationEventRow>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (invalidEventWrite is not null)
        {
            throw new InvalidOperationException("LLM Chat operation events are append-only.");
        }

        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
