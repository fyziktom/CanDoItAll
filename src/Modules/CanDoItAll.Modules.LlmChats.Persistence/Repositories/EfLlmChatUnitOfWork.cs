using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Persistence.Entities;
using CanDoItAll.Modules.LlmChats.Ports;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.LlmChats.Persistence.Repositories;

public sealed class EfLlmChatUnitOfWork(AppDbContext dbContext) : ILlmChatUnitOfWork
{
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

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var result = await operation(cancellationToken).ConfigureAwait(false);
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
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

        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
