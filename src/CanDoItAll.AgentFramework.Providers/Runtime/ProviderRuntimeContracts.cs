using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

public enum ProviderRuntimeInvalidationReason
{
    ProfileSaved,
    ProviderDeleted,
    ProviderDisabled,
    SecretChanged,
    ConfigSchemaChanged,
    Manual
}

public sealed record ProviderRuntimeKey(
    Guid ProviderProfileId,
    string ConfigFingerprint);

public sealed record ProviderRuntimeDescriptor(
    ProviderRuntimeKey Key,
    ProviderKind ProviderKind,
    string ProviderName,
    string ConnectorPluginKey,
    string BaseUrl,
    string DefaultModel,
    ProviderTransportKind Transport,
    ProviderProfilePurpose Purpose,
    bool IsEnabled,
    bool SupportsStreaming,
    bool SupportsTools,
    bool PreferFrameworkManagedChatHistory,
    bool SupportsBackgroundResponses,
    string SecretReferenceIdentity,
    int TimeoutSeconds,
    int ConfigSchemaVersion,
    string ConfigurationJson)
{
    public static ProviderRuntimeDescriptor FromProfile(
        ProviderProfile provider,
        string connectorPluginKey = "",
        string secretReferenceIdentity = "",
        int timeoutSeconds = 120,
        int configSchemaVersion = 1)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var normalizedConnector = NormalizeOptional(connectorPluginKey);
        var normalizedSecretReference = NormalizeOptional(secretReferenceIdentity);
        var normalizedConfiguration = NormalizeConfiguration(provider.ConfigurationJson);
        var fingerprint = CreateFingerprint(
            provider,
            normalizedConnector,
            normalizedSecretReference,
            timeoutSeconds,
            configSchemaVersion,
            normalizedConfiguration);

        return new ProviderRuntimeDescriptor(
            new ProviderRuntimeKey(provider.Id, fingerprint),
            provider.Kind,
            NormalizeOptional(provider.Name),
            normalizedConnector,
            NormalizeOptional(provider.BaseUrl),
            NormalizeOptional(provider.DefaultModel),
            provider.Transport,
            provider.Purpose,
            provider.IsEnabled,
            provider.SupportsStreaming,
            provider.SupportsTools,
            provider.PreferFrameworkManagedChatHistory,
            provider.SupportsBackgroundResponses,
            normalizedSecretReference,
            timeoutSeconds,
            configSchemaVersion,
            normalizedConfiguration);
    }

    private static string CreateFingerprint(
        ProviderProfile provider,
        string connectorPluginKey,
        string secretReferenceIdentity,
        int timeoutSeconds,
        int configSchemaVersion,
        string configurationJson)
    {
        var fingerprintSource = string.Join(
            "\n",
            provider.Id,
            provider.Kind,
            NormalizeOptional(provider.Name),
            connectorPluginKey,
            NormalizeOptional(provider.BaseUrl),
            NormalizeOptional(provider.DefaultModel),
            provider.Transport,
            provider.Purpose,
            provider.IsEnabled,
            provider.SupportsStreaming,
            provider.SupportsTools,
            provider.PreferFrameworkManagedChatHistory,
            provider.SupportsBackgroundResponses,
            secretReferenceIdentity,
            timeoutSeconds,
            configSchemaVersion,
            configurationJson);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintSource));
        return Convert.ToHexString(hashBytes);
    }

    private static string NormalizeConfiguration(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "{}"
            : value.Trim();
    }

    private static string NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }
}

public sealed record ProviderRuntimeDispatchRequest<TPayload>(
    ProviderDispatchQuery Query,
    TPayload Payload,
    Guid? CorrelationId = null);

public sealed record ProviderRuntimeDispatchContext<TPayload>(
    ProviderRuntimeDescriptor Descriptor,
    ProviderDispatchQuery Query,
    TPayload Payload,
    Guid CorrelationId);

public interface IProviderRuntimeDescriptorSource
{
    Task<ProviderRuntimeDescriptor> GetRequiredAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default);
}

public interface IProviderRuntimeDescriptorStore : IProviderRuntimeDescriptorSource
{
    void Upsert(
        ProviderProfile provider,
        string connectorPluginKey = "",
        string secretReferenceIdentity = "",
        int? timeoutSeconds = null,
        int configSchemaVersion = 1);

    bool Remove(Guid providerProfileId);
}

public sealed class ProviderProfileRuntimeDescriptorStore : IProviderRuntimeDescriptorStore
{
    private readonly ConcurrentDictionary<Guid, ProviderRuntimeDescriptor> descriptorsByProviderId = [];

    public void Upsert(
        ProviderProfile provider,
        string connectorPluginKey = "",
        string secretReferenceIdentity = "",
        int? timeoutSeconds = null,
        int configSchemaVersion = 1)
    {
        ArgumentNullException.ThrowIfNull(provider);

        descriptorsByProviderId[provider.Id] = ProviderRuntimeDescriptor.FromProfile(
            provider,
            connectorPluginKey,
            secretReferenceIdentity,
            timeoutSeconds ?? ResolveTimeoutSeconds(provider),
            configSchemaVersion);
    }

    public bool Remove(Guid providerProfileId)
    {
        return descriptorsByProviderId.TryRemove(providerProfileId, out _);
    }

    public Task<ProviderRuntimeDescriptor> GetRequiredAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default)
    {
        if (descriptorsByProviderId.TryGetValue(providerProfileId, out var descriptor))
        {
            return Task.FromResult(descriptor);
        }

        throw new InvalidOperationException($"Provider runtime descriptor '{providerProfileId:D}' has not been registered.");
    }

    private static int ResolveTimeoutSeconds(ProviderProfile provider)
    {
        if (string.IsNullOrWhiteSpace(provider.ConfigurationJson))
        {
            return 120;
        }

        try
        {
            using var document = JsonDocument.Parse(provider.ConfigurationJson);
            return document.RootElement.TryGetProperty("timeoutSeconds", out var timeout) &&
                   timeout.TryGetInt32(out var seconds)
                ? Math.Clamp(seconds, 5, 3600)
                : 120;
        }
        catch (JsonException)
        {
            return 120;
        }
    }
}

public interface IProviderRuntimeHandle : IAsyncDisposable
{
    ProviderRuntimeDescriptor Descriptor { get; }

    ProviderRuntimeKey Key { get; }

    IAgentProviderFactory ProviderFactory { get; }

    bool IsDisposed { get; }

    int InFlightRequestCount { get; }

    Task<TResult> DispatchAsync<TPayload, TResult>(
        ProviderRuntimeDispatchRequest<TPayload> request,
        Func<ProviderRuntimeDispatchContext<TPayload>, CancellationToken, Task<TResult>> sendAsync,
        CancellationToken cancellationToken = default);
}

public interface IProviderRuntimeHandleFactory
{
    IProviderRuntimeHandle Create(ProviderRuntimeDescriptor descriptor);
}

public interface IProviderRuntimePool : IAsyncDisposable
{
    ValueTask<IProviderRuntimeHandle> GetRequiredAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default);

    ValueTask InvalidateAsync(
        Guid providerProfileId,
        ProviderRuntimeInvalidationReason reason,
        CancellationToken cancellationToken = default);
}

public sealed class ProviderRuntimeHandleFactory(IAgentProviderFactory providerFactory) : IProviderRuntimeHandleFactory
{
    public IProviderRuntimeHandle Create(ProviderRuntimeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return new ProviderRuntimeHandle(descriptor, providerFactory);
    }
}

public sealed class ProviderRuntimeHandle : IProviderRuntimeHandle
{
    private readonly ConcurrentDictionary<Guid, IProviderRuntimePendingRequest> pendingRequests = new();
    private readonly CancellationTokenSource disposeCancellation = new();

    public ProviderRuntimeHandle(
        ProviderRuntimeDescriptor descriptor,
        IAgentProviderFactory providerFactory)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        ProviderFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        Key = descriptor.Key;
    }

    public ProviderRuntimeDescriptor Descriptor { get; }

    public ProviderRuntimeKey Key { get; }

    public IAgentProviderFactory ProviderFactory { get; }

    public bool IsDisposed { get; private set; }

    public int InFlightRequestCount => pendingRequests.Count;

    public Task<TResult> DispatchAsync<TPayload, TResult>(
        ProviderRuntimeDispatchRequest<TPayload> request,
        Func<ProviderRuntimeDispatchContext<TPayload>, CancellationToken, Task<TResult>> sendAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sendAsync);

        ThrowIfDisposed();
        if (request.Query.Provider.Id != Descriptor.Key.ProviderProfileId)
        {
            throw new InvalidOperationException("Dispatch request provider id does not match the runtime handle descriptor.");
        }

        var correlationId = request.CorrelationId is { } value && value != Guid.Empty
            ? value
            : Guid.NewGuid();
        var pending = new ProviderRuntimePendingRequest<TResult>();
        if (!pendingRequests.TryAdd(correlationId, pending))
        {
            throw new InvalidOperationException($"A provider runtime request with correlation id '{correlationId}' is already in flight.");
        }

        var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disposeCancellation.Token);
        linkedCancellation.Token.Register(
            static state =>
            {
                var tuple = (Tuple<ProviderRuntimeHandle, Guid>)state!;
                tuple.Item1.CancelPendingRequest(tuple.Item2);
            },
            Tuple.Create(this, correlationId));

        var context = new ProviderRuntimeDispatchContext<TPayload>(
            Descriptor,
            request.Query,
            request.Payload,
            correlationId);
        _ = ExecuteDispatchAsync(correlationId, context, sendAsync, linkedCancellation);

        return pending.Task;
    }

    public async ValueTask DisposeAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        await disposeCancellation.CancelAsync();
        foreach (var pending in pendingRequests.Values)
        {
            pending.TrySetCanceled();
        }

        pendingRequests.Clear();
        disposeCancellation.Dispose();
    }

    private async Task ExecuteDispatchAsync<TPayload, TResult>(
        Guid correlationId,
        ProviderRuntimeDispatchContext<TPayload> context,
        Func<ProviderRuntimeDispatchContext<TPayload>, CancellationToken, Task<TResult>> sendAsync,
        CancellationTokenSource cancellationSource)
    {
        try
        {
            var cancellationToken = cancellationSource.Token;
            var result = await sendAsync(context, cancellationToken).ConfigureAwait(false);
            if (pendingRequests.TryRemove(correlationId, out var pending) &&
                pending is ProviderRuntimePendingRequest<TResult> typedPending)
            {
                typedPending.TrySetResult(result);
            }
        }
        catch (OperationCanceledException) when (cancellationSource.Token.IsCancellationRequested)
        {
            CancelPendingRequest(correlationId);
        }
        catch (Exception exception)
        {
            if (pendingRequests.TryRemove(correlationId, out var pending))
            {
                pending.TrySetException(exception);
            }
        }
        finally
        {
            cancellationSource.Dispose();
        }
    }

    private void CancelPendingRequest(Guid correlationId)
    {
        if (pendingRequests.TryRemove(correlationId, out var pending))
        {
            pending.TrySetCanceled();
        }
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(ProviderRuntimeHandle));
        }
    }

    private interface IProviderRuntimePendingRequest
    {
        void TrySetCanceled();

        void TrySetException(Exception exception);
    }

    private sealed class ProviderRuntimePendingRequest<TResult> : IProviderRuntimePendingRequest
    {
        private readonly TaskCompletionSource<TResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<TResult> Task => completion.Task;

        public void TrySetResult(TResult result)
        {
            completion.TrySetResult(result);
        }

        public void TrySetCanceled()
        {
            completion.TrySetCanceled();
        }

        public void TrySetException(Exception exception)
        {
            completion.TrySetException(exception);
        }
    }
}
