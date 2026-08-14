using System.Collections.Concurrent;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Ports;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed class LlmChatOperationCancellationRegistry : ILlmChatOperationCancellationRegistry
{
    private readonly ConcurrentDictionary<LlmChatOperationId, LlmChatOperationCancellationRegistration> registrations = [];

    public ILlmChatOperationCancellationRegistration Register(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken)
    {
        var registration = new LlmChatOperationCancellationRegistration(
            cancellationToken,
            value => Unregister(operationId, value));
        if (registrations.TryAdd(operationId, registration))
        {
            return registration;
        }

        registration.Dispose();
        throw new InvalidOperationException("The LLM Chat operation is already executing in this process.");
    }

    public bool RequestCancellation(LlmChatOperationId operationId)
    {
        if (!registrations.TryGetValue(operationId, out var registration))
        {
            return false;
        }

        registration.Cancel();
        return true;
    }

    public bool IsRegistered(LlmChatOperationId operationId)
        => registrations.ContainsKey(operationId);

    private void Unregister(
        LlmChatOperationId operationId,
        LlmChatOperationCancellationRegistration registration)
        => ((ICollection<KeyValuePair<LlmChatOperationId, LlmChatOperationCancellationRegistration>>)registrations)
            .Remove(new KeyValuePair<LlmChatOperationId, LlmChatOperationCancellationRegistration>(operationId, registration));
}

internal sealed class LlmChatOperationCancellationRegistration : ILlmChatOperationCancellationRegistration
{
    private readonly Action<LlmChatOperationCancellationRegistration> unregister;
    private readonly CancellationTokenSource cancellationSource;
    private int disposed;

    public LlmChatOperationCancellationRegistration(
        CancellationToken cancellationToken,
        Action<LlmChatOperationCancellationRegistration> unregister)
    {
        this.unregister = unregister;
        cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    }

    public CancellationToken CancellationToken => cancellationSource.Token;

    public void Cancel()
        => cancellationSource.Cancel();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        unregister(this);
        cancellationSource.Dispose();
    }
}
