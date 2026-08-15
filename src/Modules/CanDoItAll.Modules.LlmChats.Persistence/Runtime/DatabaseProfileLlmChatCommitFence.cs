using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Ports;

namespace CanDoItAll.Modules.LlmChats.Persistence;

public sealed class DatabaseProfileLlmChatCommitFence(
    IDatabaseRuntimeWriteFence writeFence,
    ILlmChatOperationScopeAccessor operationScope) : ILlmChatCommitFence
{
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var identity = operationScope.Current?.RuntimeIdentity
            ?? throw new InvalidOperationException("An LLM Chat operation scope is required for a durable write.");
        try
        {
            return await writeFence.ExecuteAsync(
                new DatabaseRuntimeSnapshot(identity.ProfileId, identity.Fingerprint, identity.Generation),
                operation,
                cancellationToken).ConfigureAwait(false);
        }
        catch (DatabaseRuntimeProfileChangedException)
        {
            throw new LlmChatRuntimeProfileChangedException();
        }
    }
}
