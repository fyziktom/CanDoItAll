using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Repositories;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.ReadModels;

public sealed class EfLlmChatOperationReadStore(AppDbContext dbContext) : ILlmChatOperationReadStore
{
    public async Task<LlmChatOperationReadModel?> TryGetAsync(
        LlmChatOperationId id,
        CancellationToken cancellationToken = default)
    {
        var operation = await dbContext.Set<LlmChatOperationRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        if (operation is null)
        {
            return null;
        }

        var invocationRows = await dbContext.Set<LlmChatInvocationRecordRow>()
            .AsNoTracking()
            .Where(row => row.OperationId == id.Value)
            .OrderBy(row => row.Ordinal)
            .Take(LlmChatOperationDetails.MaximumInvocationRecords + 1)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (invocationRows.Length > LlmChatOperationDetails.MaximumInvocationRecords)
        {
            throw new InvalidOperationException("The LLM Chat operation exceeds the supported invocation history bound.");
        }

        return new LlmChatOperationReadModel(
            LlmChatPersistenceMapper.ToDomain(operation),
            [.. invocationRows.Select(LlmChatPersistenceMapper.ToDomain)]);
    }

    public async Task<IReadOnlyList<LlmChatOperationId>> ListDispatchCandidatesAsync(
        DateTimeOffset observedAtUtc,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
        var ids = await dbContext.Set<LlmChatOperationRow>()
            .AsNoTracking()
            .Where(row =>
                (row.Status == LlmChatOperationStatus.Pending ||
                 row.Status == LlmChatOperationStatus.Running ||
                 row.Status == LlmChatOperationStatus.CancellationRequested) &&
                (row.ExecutionOwnerId == null ||
                 row.LeaseExpiresAtUtc == null ||
                 row.LeaseExpiresAtUtc <= observedAtUtc))
            .OrderBy(row => row.StartedAtUtc)
            .ThenBy(row => row.Id)
            .Select(row => row.Id)
            .Take(take)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. ids.Select(id => new LlmChatOperationId(id))];
    }
}
