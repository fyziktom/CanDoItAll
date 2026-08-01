using System.Net;
using System.Text.Json;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Protocol.Http;

namespace CanDoItAll.Memory.Http;

internal static class HttpMemoryProviderResponseMapper
{
    public static async Task<MemoryProviderDriverResult> MapContextQueryResponseAsync(
        MemoryProviderProfile provider,
        HttpResponseMessage response,
        MemoryProviderResponseSizeLimit responseSizeLimit,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.RequestTimeout ||
            response.StatusCode == HttpStatusCode.GatewayTimeout)
        {
            return MemoryProviderDriverResult.Failed(
                MemoryProviderDriverResultKind.Timeout,
                $"HTTP memory provider '{provider.InstanceId}' reported a timeout.");
        }

        if (response.StatusCode == HttpStatusCode.NotImplemented)
        {
            return MemoryProviderDriverResult.Failed(
                MemoryProviderDriverResultKind.UnsupportedCapability,
                $"HTTP memory provider '{provider.InstanceId}' does not support the requested capability.");
        }

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            return MemoryProviderDriverResult.Failed(
                MemoryProviderDriverResultKind.Unavailable,
                $"HTTP memory provider '{provider.InstanceId}' is unavailable.");
        }

        if (!response.IsSuccessStatusCode)
        {
            return MemoryProviderDriverResult.Failed(
                MemoryProviderDriverResultKind.ProviderError,
                $"HTTP memory provider '{provider.InstanceId}' failed with status {(int)response.StatusCode} {response.StatusCode}.");
        }

        HttpMemoryProviderResponse? providerResponse;
        try
        {
            providerResponse = await HttpMemoryProviderResponseReader.ReadJsonAsync<HttpMemoryProviderResponse>(
                response.Content,
                responseSizeLimit,
                HttpMemoryProviderJson.Options,
                cancellationToken);
        }
        catch (HttpMemoryProviderResponseTooLargeException exception)
        {
            return MemoryProviderDriverResult.Failed(
                MemoryProviderDriverResultKind.ProviderError,
                $"HTTP memory provider '{provider.InstanceId}' response exceeded the configured limit of {exception.SizeLimit.MaximumBytes} bytes.");
        }
        catch (JsonException)
        {
            return MemoryProviderDriverResult.Failed(
                MemoryProviderDriverResultKind.ProviderError,
                "Malformed HTTP memory provider response: invalid JSON body.");
        }

        return MapProviderResponse(provider, providerResponse);
    }

    private static MemoryProviderDriverResult MapProviderResponse(
        MemoryProviderProfile provider,
        HttpMemoryProviderResponse? response)
    {
        if (response is null)
        {
            return MemoryProviderDriverResult.Failed(
                MemoryProviderDriverResultKind.ProviderError,
                "Malformed HTTP memory provider response: empty body.");
        }

        return response.Kind switch
        {
            HttpMemoryProviderResponseKind.ContextPack when response.ContextPack is not null =>
                MemoryProviderDriverResult.ContextPackResult(
                    response.ContextPack,
                    $"HTTP memory provider '{provider.InstanceId}' returned a context pack."),
            HttpMemoryProviderResponseKind.OperationAccepted when response.AcceptedOperation is not null =>
                MemoryProviderDriverResult.Failed(
                    MemoryProviderDriverResultKind.UnsupportedCapability,
                    $"HTTP memory provider '{provider.InstanceId}' returned an asynchronous operation, but HTTP operation-status polling is not implemented."),
            HttpMemoryProviderResponseKind.UnsupportedCapability =>
                MemoryProviderDriverResult.Failed(
                    MemoryProviderDriverResultKind.UnsupportedCapability,
                    response.Error?.Message ?? $"HTTP memory provider '{provider.InstanceId}' does not support the requested capability."),
            HttpMemoryProviderResponseKind.ProviderError =>
                MemoryProviderDriverResult.Failed(
                    MemoryProviderDriverResultKind.ProviderError,
                    response.Error?.Message ?? $"HTTP memory provider '{provider.InstanceId}' returned a provider error."),
            _ => MemoryProviderDriverResult.Failed(
                MemoryProviderDriverResultKind.ProviderError,
                "Malformed HTTP memory provider response: response kind does not match payload.")
        };
    }
}

internal static class HttpMemoryProviderJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
