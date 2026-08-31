using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Repositories;

public sealed class EfLlmChatInvocationRecordRepository(AppDbContext dbContext, LlmChatHistoryProjection history)
    : ILlmChatInvocationRecordRepository {
    public async Task AppendAsync(LlmChatInvocationRecord record, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(record);
        await history.StageAsync(dbContext, record, cancellationToken);
        dbContext.Add(LlmChatPersistenceMapper.ToRow(record));
    }

    public async Task<IReadOnlyList<LlmChatInvocationRecord>> ListAsync(
        LlmChatOperationId operationId, CancellationToken cancellationToken = default) {
        var rows = await dbContext.Set<LlmChatInvocationRecordRow>().AsNoTracking()
            .Where(row => row.OperationId == operationId.Value).OrderBy(row => row.Ordinal)
            .ToArrayAsync(cancellationToken);
        return [.. rows.Select(LlmChatPersistenceMapper.ToDomain)];
    }
}
