using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CanDoItAll.RemoteInstances;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.SharedProviders.Http;

public sealed class RemoteInstanceJsonReader(
    IHttpClientFactory clients, ISharedProviderSourceUriPolicy uriPolicy) : IRemoteInstanceJsonReader {
    internal const int MaximumBytes = 4 * 1024 * 1024;

    public async Task<T> ReadAsync<T>(Uri baseUri, SharedProviderSourceNetworkPolicy networkPolicy,
        SharedProviderCatalogAccessToken credential, string relativeApiPath, CancellationToken cancellationToken) {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeApiPath);
        if (!relativeApiPath.StartsWith("api/", StringComparison.Ordinal) ||
            relativeApiPath.Split('/').Any(segment => segment.Length == 0 ||
                segment.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_')))) {
            throw new ArgumentException("A relative API resource path is required.", nameof(relativeApiPath));
        }

        var origin = uriPolicy.Normalize(baseUri, networkPolicy);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(origin, relativeApiPath));
        request.Headers.Authorization = credential.UseValue(value => new AuthenticationHeaderValue("Bearer", value));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var client = clients.CreateClient(SelectClientName(origin, networkPolicy));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(SharedProviderCatalogClient.RequestTimeout);
        try {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode != HttpStatusCode.OK) {
                throw new RemoteInstanceReadException(response.StatusCode switch {
                    HttpStatusCode.Unauthorized => "The remote instance rejected the access token.",
                    HttpStatusCode.Forbidden => "The token lacks permission to read this remote catalog.",
                    HttpStatusCode.NotFound => "The catalog or selected item is unavailable on this remote instance.",
                    _ => $"The remote instance returned HTTP {(int)response.StatusCode}."
                });
            }
            if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase) ||
                response.Content.Headers.ContentLength > MaximumBytes) {
                throw new RemoteInstanceReadException("The remote response has an unsupported size or format.");
            }
            await response.Content.LoadIntoBufferAsync(MaximumBytes, deadline.Token);
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonSerializerOptions.Web, deadline.Token)
                ?? throw new RemoteInstanceReadException("The remote response is empty.");
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            throw new RemoteInstanceReadException("The remote instance did not finish responding before the timeout.");
        } catch (JsonException) {
            throw new RemoteInstanceReadException("The remote instance returned an incompatible document.");
        } catch (Exception exception) when (exception is HttpRequestException or IOException) {
            throw new RemoteInstanceReadException("The remote response could not be read within the connection and size limits.");
        }
    }

    internal static string SelectClientName(Uri origin, SharedProviderSourceNetworkPolicy networkPolicy) =>
        origin.Scheme == Uri.UriSchemeHttp ? SharedProviderCatalogClient.PrivateHttpClientName
            : networkPolicy == SharedProviderSourceNetworkPolicy.AllowPrivateNetwork ? SharedProviderCatalogClient.TrustedNetworkClientName
            : SharedProviderCatalogClient.PublicClientName;
}
