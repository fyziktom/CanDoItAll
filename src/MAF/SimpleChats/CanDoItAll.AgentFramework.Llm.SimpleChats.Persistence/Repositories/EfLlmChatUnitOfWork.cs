using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Repositories;

public sealed class EfLlmChatUnitOfWork(
    SimpleChatsDbContext dbContext,
    ILlmChatCommitFence commitFence,
    CoordinatedDatabaseTransaction transactions) : ILlmChatUnitOfWork {
    private readonly List<Action> _postCommitCallbacks = [];
    private int _executionDepth;
    private Exception? _nestedFailure;

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(operation);
        if (_executionDepth == 0 && dbContext.Database.CurrentTransaction is not null) {
            throw new InvalidOperationException("LLM Chat writes require their owning unit of work; an unmanaged outer transaction cannot publish commit callbacks.");
        }

        if (_executionDepth > 0) {
            _executionDepth++;
            try {
                var nestedResult = await operation(cancellationToken).ConfigureAwait(false);
                await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return nestedResult;
            } catch (Exception exception) {
                _nestedFailure ??= exception;
                throw;
            } finally {
                _executionDepth--;
            }
        }

        _postCommitCallbacks.Clear();
        _nestedFailure = null;
        try {
            var result = await commitFence.ExecuteAsync(async fenceCancellationToken => {
                await using var transaction = dbContext.Database.IsRelational()
                    ? await dbContext.Database.BeginTransactionAsync(fenceCancellationToken).ConfigureAwait(false)
                    : null;
                using var enlistment = transactions.Enter(dbContext);
                _executionDepth++;
                try {
                    var transactionResult = await operation(fenceCancellationToken).ConfigureAwait(false);
                    if (_nestedFailure is { } nestedFailure) {
                        throw new InvalidOperationException("An LLM Chat transaction cannot commit after nested work failed.", nestedFailure);
                    }

                    await SaveChangesAsync(fenceCancellationToken).ConfigureAwait(false);
                    if (transaction is not null) {
                        await transaction.CommitAsync(fenceCancellationToken).ConfigureAwait(false);
                    }

                    return transactionResult;
                } finally {
                    _executionDepth--;
                }
            }, cancellationToken).ConfigureAwait(false);
            var callbacks = _postCommitCallbacks.ToArray();
            _postCommitCallbacks.Clear();
            foreach (var callback in callbacks) {
                callback();
            }

            return result;
        } catch {
            _postCommitCallbacks.Clear();
            throw;
        } finally {
            _nestedFailure = null;
        }
    }

    public void RegisterPostCommit(Action callback) {
        ArgumentNullException.ThrowIfNull(callback);
        if (_executionDepth == 0) {
            throw new InvalidOperationException("A post-commit callback requires an active LLM Chat transaction.");
        }

        _postCommitCallbacks.Add(callback);
    }

    private Task<int> SaveChangesAsync(CancellationToken cancellationToken) {
        var invalidReceiptWrite = dbContext.ChangeTracker.Entries<LlmChatDefinitionCreateReceiptRow>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (invalidReceiptWrite is not null) {
            throw new InvalidOperationException("LLM Chat definition create receipts are immutable.");
        }

        var invalidRevisionWrite = dbContext.ChangeTracker.Entries<LlmChatDefinitionRevisionRow>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (invalidRevisionWrite is not null) {
            throw new InvalidOperationException("LLM Chat definition revisions are append-only.");
        }

        var invalidAuditWrite = dbContext.ChangeTracker.Entries<LlmChatInvocationRecordRow>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (invalidAuditWrite is not null) {
            throw new InvalidOperationException("LLM Chat invocation records are append-only.");
        }

        var invalidEventWrite = dbContext.ChangeTracker.Entries<LlmChatOperationEventRow>()
            .FirstOrDefault(entry => entry.State == EntityState.Modified);
        if (invalidEventWrite is not null) {
            throw new InvalidOperationException("LLM Chat operation events cannot be modified after append.");
        }

        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
