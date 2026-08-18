using System.Collections.Concurrent;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

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
    private readonly object gate = new();
    private bool cancellationRequested;
    private bool disposed;
    private bool sourceDisposed;
    private int activeNotifications;

    public LlmChatOperationCancellationRegistration(
        CancellationToken cancellationToken,
        Action<LlmChatOperationCancellationRegistration> unregister)
    {
        this.unregister = unregister;
        cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    }

    public CancellationToken CancellationToken => cancellationSource.Token;

    public void Cancel()
    {
        lock (gate)
        {
            if (disposed || cancellationRequested)
            {
                return;
            }

            cancellationRequested = true;
            activeNotifications++;
        }

        try
        {
            cancellationSource.Cancel(throwOnFirstException: false);
        }
        catch (AggregateException)
        {
            // Cancellation observers cannot override the already-committed durable request.
        }
        catch (ObjectDisposedException)
        {
            // Disposal won the lifecycle race after this notification was selected.
        }
        finally
        {
            lock (gate)
            {
                activeNotifications--;
                DisposeSourceIfReady();
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
        }

        unregister(this);
        lock (gate)
        {
            DisposeSourceIfReady();
        }
    }

    private void DisposeSourceIfReady()
    {
        if (!disposed || activeNotifications != 0 || sourceDisposed)
        {
            return;
        }

        cancellationSource.Dispose();
        sourceDisposed = true;
    }
}
