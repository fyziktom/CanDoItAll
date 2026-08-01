using System.Net;
using System.Text.Json;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Http;

public sealed class HttpMemoryProviderDriver(
    IHttpClientFactory httpClientFactory,
    HttpMemoryProviderOptions options) : IMemoryProviderDriver, IMemoryProviderHealthDriver
{
    public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.Http;

    public async Task<MemoryProviderDriverResult> ExecuteContextQueryAsync(
        MemoryProviderProfile provider,
        MemoryOperationRecord operation,
        MemoryContextQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(request);

        if (!HttpMemoryProviderRequestFactory.SupportsAnyRequestedCapability(provider, request))
        {
            return MemoryProviderDriverResult.Failed(
                MemoryProviderDriverResultKind.UnsupportedCapability,
                $"HTTP memory provider '{provider.InstanceId}' does not support any requested query capability.");
        }

        var configuration = HttpMemoryProviderConfiguration.FromProfile(provider, options);
        var client = httpClientFactory.CreateClient(options.ClientName);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(configuration.Timeout);
        var requestBody = HttpMemoryProviderRequestFactory.CreateQueryRequest(provider, operation, request);
        var responseSizeLimit = options.ResponseSizeLimit.ConstrainToJsonEnvelope(request.Context.Budget);

        for (var attempt = 0; attempt <= configuration.MaxRetryAttempts; attempt++)
        {
            using var httpRequest = HttpMemoryProviderRequestFactory.CreatePostRequest(
                configuration.BuildUri(configuration.QueryPath),
                requestBody,
                configuration);
            try
            {
                using var response = await client.SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeout.Token);
                if (HttpMemoryProviderRequestFactory.ShouldRetry(response.StatusCode, attempt, configuration.MaxRetryAttempts))
                {
                    continue;
                }

                return await HttpMemoryProviderResponseMapper.MapContextQueryResponseAsync(
                    provider,
                    response,
                    responseSizeLimit,
                    timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return MemoryProviderDriverResult.Failed(
                    MemoryProviderDriverResultKind.Timeout,
                    $"HTTP memory provider '{provider.InstanceId}' timed out after {configuration.Timeout}.");
            }
            catch (HttpRequestException ex)
            {
                return MemoryProviderDriverResult.Failed(
                    MemoryProviderDriverResultKind.Unavailable,
                    $"HTTP memory provider '{provider.InstanceId}' is unavailable: {ex.Message}");
            }
        }

        return MemoryProviderDriverResult.Failed(
            MemoryProviderDriverResultKind.Unavailable,
            $"HTTP memory provider '{provider.InstanceId}' did not return a usable response.");
    }

    public async Task<MemoryProviderHealth> GetHealthAsync(
        MemoryProviderProfile provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var configuration = HttpMemoryProviderConfiguration.FromProfile(provider, options);
        var client = httpClientFactory.CreateClient(options.ClientName);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(configuration.Timeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, configuration.BuildUri(configuration.HealthPath));
        HttpMemoryProviderRequestFactory.ApplyAuthentication(request, configuration);

        try
        {
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return new MemoryProviderHealth(
                    response.StatusCode == HttpStatusCode.ServiceUnavailable
                        ? MemoryProviderHealthStatus.Unreachable
                        : MemoryProviderHealthStatus.Degraded,
                    response.StatusCode.ToString(),
                    provider.Manifest);
            }

            return await HttpMemoryProviderResponseReader.ReadJsonAsync<MemoryProviderHealth>(
                    response.Content,
                    options.ResponseSizeLimit,
                    HttpMemoryProviderJson.Options,
                    timeout.Token)
                ?? new MemoryProviderHealth(
                    MemoryProviderHealthStatus.Degraded,
                    "empty-health-response",
                    provider.Manifest);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new MemoryProviderHealth(
                MemoryProviderHealthStatus.Unreachable,
                "timeout",
                provider.Manifest);
        }
        catch (HttpRequestException)
        {
            return new MemoryProviderHealth(
                MemoryProviderHealthStatus.Unreachable,
                "transport",
                provider.Manifest);
        }
        catch (JsonException)
        {
            return new MemoryProviderHealth(
                MemoryProviderHealthStatus.Degraded,
                "malformed-health-response",
                provider.Manifest);
        }
        catch (HttpMemoryProviderResponseTooLargeException)
        {
            return new MemoryProviderHealth(
                MemoryProviderHealthStatus.Degraded,
                "oversized-health-response",
                provider.Manifest);
        }
    }
}
