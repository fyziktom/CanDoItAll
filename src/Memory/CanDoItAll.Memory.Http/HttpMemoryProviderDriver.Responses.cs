using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Http;

public sealed partial class HttpMemoryProviderDriver
{
    private static async Task<MemoryProviderDriverResult> MapContextQueryResponseAsync(
        MemoryProviderProfile provider,
        HttpResponseMessage response,
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
            providerResponse = await response.Content.ReadFromJsonAsync<HttpMemoryProviderResponse>(
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException ex)
        {
            return MemoryProviderDriverResult.Failed(
                MemoryProviderDriverResultKind.ProviderError,
                $"Malformed HTTP memory provider response: {ex.Message}");
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
                MemoryProviderDriverResult.Accepted(
                    response.AcceptedOperation,
                    $"HTTP memory provider '{provider.InstanceId}' accepted an async operation."),
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
