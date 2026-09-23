using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.SharedProviders.Http;

internal sealed class SharedProviderInferenceRelayTransport(
    IHttpClientFactory httpClientFactory) : IProviderInferenceRelayTransport
{
    public async Task<ProviderInferenceRelayTransportResponse> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var client = httpClientFactory.CreateClient(
            SharedProviderHttpRelayClient.ClientName);
        try
        {
            var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            return new ProviderInferenceRelayTransportResponse(response, client);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }
}
