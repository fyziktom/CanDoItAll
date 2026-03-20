using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

public sealed class OriginSyncClient
{
    private readonly HttpClient _httpClient;

    public OriginSyncClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<OriginStatusBatchResponse?> GetOriginStatusAsync(
        OriginStatusBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        return _httpClient.PostAsJsonAsync("/api/v1/repos/status-batch", request, cancellationToken)
            .ContinueWith(async responseTask =>
            {
                using var response = await responseTask.ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<OriginStatusBatchResponse>(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }, cancellationToken).Unwrap();
    }
}

public sealed record OriginStatusBatchRequest(object[] Repositories);
public sealed record OriginStatusBatchResponse(object[] Repositories);
