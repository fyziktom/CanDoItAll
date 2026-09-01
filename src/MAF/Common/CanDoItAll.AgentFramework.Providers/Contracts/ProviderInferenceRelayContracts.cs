using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

public enum ProviderInferenceRelayOperation
{
    ChatCompletions,
    Responses,
    ImageGenerations
}

public sealed class ProviderInferenceRelayCredential
{
    private readonly string value;

    public ProviderInferenceRelayCredential(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
        {
            throw new ArgumentException("The provider inference relay credential is invalid.", nameof(value));
        }

        this.value = value;
    }

    public TResult UseValue<TResult>(Func<string, TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action(value);
    }

    public override string ToString()
        => "[REDACTED]";
}

public sealed class ProviderInferenceRelayRequest
{
    private readonly byte[] payloadUtf8;

    public ProviderInferenceRelayRequest(
        ProviderProfile provider,
        string model,
        ProviderInferenceRelayOperation operation,
        ReadOnlySpan<byte> payloadUtf8,
        bool stream,
        ProviderInferenceRelayCredential? credential)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (!provider.IsEnabled)
        {
            throw new ArgumentException("The provider inference relay profile is disabled.", nameof(provider));
        }

        if (string.IsNullOrWhiteSpace(model) || model != model.Trim() || model.Any(char.IsControl))
        {
            throw new ArgumentException("The provider inference relay model is invalid.", nameof(model));
        }

        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (payloadUtf8.IsEmpty)
        {
            throw new ArgumentException("The provider inference relay payload is empty.", nameof(payloadUtf8));
        }

        Provider = provider;
        Model = model;
        Operation = operation;
        this.payloadUtf8 = payloadUtf8.ToArray();
        Stream = stream;
        Credential = credential;
    }

    public ProviderProfile Provider { get; }

    public string Model { get; }

    public ProviderInferenceRelayOperation Operation { get; }

    public ReadOnlyMemory<byte> PayloadUtf8 => payloadUtf8;

    public bool Stream { get; }

    public ProviderInferenceRelayCredential? Credential { get; }
}

public sealed class ProviderInferenceRelayTransportResponse(
    HttpResponseMessage response,
    IDisposable transportLifetime) : IDisposable
{
    private IDisposable? transportLifetime = transportLifetime ??
        throw new ArgumentNullException(nameof(transportLifetime));

    public HttpResponseMessage Response { get; } = response ??
        throw new ArgumentNullException(nameof(response));

    public void Dispose()
    {
        Response.Dispose();
        Interlocked.Exchange(ref transportLifetime, null)?.Dispose();
    }
}

public interface IProviderInferenceRelayTransport
{
    Task<ProviderInferenceRelayTransportResponse> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default);
}

public interface IProviderInferenceRelayRuntime
{
    Task<ProviderInferenceRelayTransportResponse> SendAsync(
        ProviderInferenceRelayRequest request,
        CancellationToken cancellationToken = default);
}
