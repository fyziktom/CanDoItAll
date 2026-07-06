using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Http;

public sealed record HttpMemoryContextQueryRequest(
    string OperationId,
    string CorrelationId,
    string CausationId,
    string ProviderInstanceId,
    string CapabilityId,
    string MemoryProtocolVersion,
    string Query,
    IReadOnlyList<string> RequestedCapabilities,
    MemoryOperationEnvelope<MemoryContextQueryRequest> Envelope);

public enum HttpMemoryProviderResponseKind
{
    ContextPack = 0,
    OperationAccepted = 1,
    ProviderError = 2,
    UnsupportedCapability = 3
}

public sealed record HttpMemoryProviderError(
    string Code,
    string Message);

public sealed record HttpMemoryProviderResponse(
    HttpMemoryProviderResponseKind Kind,
    MemoryContextPack? ContextPack,
    MemoryOperationAccepted? AcceptedOperation,
    HttpMemoryProviderError? Error)
{
    public static HttpMemoryProviderResponse FromContextPack(MemoryContextPack contextPack)
    {
        ArgumentNullException.ThrowIfNull(contextPack);
        return new HttpMemoryProviderResponse(
            HttpMemoryProviderResponseKind.ContextPack,
            contextPack,
            AcceptedOperation: null,
            Error: null);
    }

    public static HttpMemoryProviderResponse FromAccepted(MemoryOperationAccepted acceptedOperation)
    {
        ArgumentNullException.ThrowIfNull(acceptedOperation);
        return new HttpMemoryProviderResponse(
            HttpMemoryProviderResponseKind.OperationAccepted,
            ContextPack: null,
            acceptedOperation,
            Error: null);
    }

    public static HttpMemoryProviderResponse ProviderError(
        string code,
        string message) =>
        new(
            HttpMemoryProviderResponseKind.ProviderError,
            ContextPack: null,
            AcceptedOperation: null,
            new HttpMemoryProviderError(
                EnsureText(code, nameof(code)),
                EnsureText(message, nameof(message))));

    public static HttpMemoryProviderResponse UnsupportedCapability(string message) =>
        new(
            HttpMemoryProviderResponseKind.UnsupportedCapability,
            ContextPack: null,
            AcceptedOperation: null,
            new HttpMemoryProviderError(
                "unsupported-capability",
                EnsureText(message, nameof(message))));

    private static string EnsureText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value.Trim();
    }
}
