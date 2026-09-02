using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.SharedProviders.Http;

public sealed class SharedProviderCatalogClient(
    IHttpClientFactory httpClientFactory,
    ISharedProviderSourceUriPolicy uriPolicy,
    ILogger<SharedProviderCatalogClient> logger,
    IAccessContextReferenceAccessor? accessContextAccessor = null) : ISharedProviderCatalogClient
{
    public const string PublicClientName = "SharedProviderCatalog.Public";
    public const string TrustedNetworkClientName = "SharedProviderCatalog.TrustedNetwork";
    public const string PrivateHttpClientName = "SharedProviderCatalog.PrivateHttp";
    public const int MaximumResponseBytes = 16 * 1024 * 1024;
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public async ValueTask<SharedProviderCatalogFetchResult> FetchAsync(
        SharedProviderCatalogFetchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Uri canonicalSourceBaseUri;
        try
        {
            canonicalSourceBaseUri = uriPolicy.Normalize(request.SourceBaseUri, request.NetworkPolicy);
        }
        catch (ArgumentException)
        {
            return Failed(
                SharedProviderFailureCategory.Validation,
                SharedProviderCatalogFailureCodes.SourceUriInvalid,
                "The shared-provider source address is invalid.");
        }

        using var message = CreateRequest(request, canonicalSourceBaseUri);
        var clientName = canonicalSourceBaseUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            ? PrivateHttpClientName
            : request.NetworkPolicy == SharedProviderSourceNetworkPolicy.AllowPrivateNetwork
                ? TrustedNetworkClientName
                : PublicClientName;
        var client = httpClientFactory.CreateClient(clientName);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(RequestTimeout);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Failed(
                SharedProviderFailureCategory.Timeout,
                SharedProviderCatalogFailureCodes.Timeout,
                "The shared-provider catalog did not respond before the timeout.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                "Shared-provider catalog transport failed with {FailureType}.",
                exception.GetType().Name);
            return Failed(
                SharedProviderFailureCategory.Unavailable,
                SharedProviderCatalogFailureCodes.Unavailable,
                "The shared-provider source is unavailable.");
        }

        using (response)
        {
            try
            {
                return await ReadResponseAsync(request, response, timeoutSource.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                return Failed(
                    SharedProviderFailureCategory.Timeout,
                    SharedProviderCatalogFailureCodes.Timeout,
                    "The shared-provider catalog response did not complete before the timeout.");
            }
        }
    }

    private HttpRequestMessage CreateRequest(
        SharedProviderCatalogFetchRequest request,
        Uri canonicalSourceBaseUri)
    {
        var message = new HttpRequestMessage(
            HttpMethod.Get,
            SharedProviderRoutes.ResolveCatalog(canonicalSourceBaseUri));
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Headers.Authorization = request.AccessToken.UseValue(
            value => new AuthenticationHeaderValue("Bearer", value));
        if (request.IfNoneMatch is { } entityTag)
        {
            message.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(entityTag.Value));
        }

        if (accessContextAccessor?.Current is { } accessContextReference)
        {
            message.Headers.Add(
                SharedProviderHeaders.AccessContextReference,
                accessContextReference.Value);
            if (accessContextAccessor.CurrentType is { } type)
            {
                message.Headers.Add(
                    SharedProviderHeaders.AccessContextReferenceType,
                    type.Value);
            }
        }

        return message;
    }

    private async ValueTask<SharedProviderCatalogFetchResult> ReadResponseAsync(
        SharedProviderCatalogFetchRequest request,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return ReadNotModified(request, response);
        }

        if (response.StatusCode != HttpStatusCode.OK)
        {
            return MapStatusFailure(response);
        }

        if (!TryReadEntityTag(response, out var entityTag) ||
            !HasSupportedContentType(response.Content.Headers.ContentType) ||
            response.Content.Headers.ContentLength is > MaximumResponseBytes)
        {
            return InvalidCatalogResponse();
        }

        byte[] payload;
        try
        {
            payload = await ReadBoundedAsync(response.Content, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            logger.LogWarning(
                "Shared-provider catalog response read failed with {FailureType}.",
                exception.GetType().Name);
            return Failed(
                SharedProviderFailureCategory.Unavailable,
                SharedProviderCatalogFailureCodes.Unavailable,
                "The shared-provider source response could not be read.");
        }

        SharedProviderCatalogDocument catalog;
        try
        {
            if (payload.Length == 0)
            {
                return InvalidCatalogResponse();
            }

            catalog = SharedProviderProtocolJson.DeserializeCatalog(StrictUtf8.GetString(payload));
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException or ArgumentException)
        {
            return InvalidCatalogResponse();
        }

        if (entityTag != SharedProviderCatalogEntityTag.FromRevision(catalog.CatalogRevision))
        {
            return InvalidCatalogResponse();
        }

        if (request.ExpectedSourceInstanceId is { } expectedSourceInstanceId &&
            catalog.SourceInstanceId != expectedSourceInstanceId)
        {
            return Failed(
                SharedProviderFailureCategory.Conflict,
                SharedProviderCatalogFailureCodes.SourceIdentityMismatch,
                "The shared-provider source identity differs from the trusted identity.");
        }

        return new SharedProviderCatalogFetchResult.Succeeded(catalog, entityTag);
    }

    private static SharedProviderCatalogFetchResult ReadNotModified(
        SharedProviderCatalogFetchRequest request,
        HttpResponseMessage response)
    {
        if (request.IfNoneMatch is not { } requestedEntityTag ||
            !TryReadEntityTag(response, out var responseEntityTag) ||
            responseEntityTag != requestedEntityTag ||
            response.Content.Headers.ContentLength is > 0)
        {
            return InvalidCatalogResponse();
        }

        return new SharedProviderCatalogFetchResult.NotModified(responseEntityTag);
    }

    private static SharedProviderCatalogFetchResult MapStatusFailure(HttpResponseMessage response)
    {
        var (category, code, message) = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => (
                SharedProviderFailureCategory.Unauthorized,
                SharedProviderCatalogFailureCodes.Unauthorized,
                "The shared-provider source rejected the catalog credential."),
            HttpStatusCode.Forbidden => (
                SharedProviderFailureCategory.InsufficientScope,
                SharedProviderCatalogFailureCodes.InsufficientScope,
                "The catalog credential does not grant the required source scope."),
            HttpStatusCode.NotFound => (
                SharedProviderFailureCategory.NotFound,
                SharedProviderCatalogFailureCodes.NotFound,
                "The shared-provider catalog endpoint was not found."),
            HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => (
                SharedProviderFailureCategory.Timeout,
                SharedProviderCatalogFailureCodes.Timeout,
                "The shared-provider catalog did not respond before the timeout."),
            HttpStatusCode.TooManyRequests => (
                SharedProviderFailureCategory.RateLimited,
                SharedProviderCatalogFailureCodes.RateLimited,
                "The shared-provider source rate limited the catalog request."),
            _ when (int)response.StatusCode >= 500 => (
                SharedProviderFailureCategory.Unavailable,
                SharedProviderCatalogFailureCodes.Unavailable,
                "The shared-provider source is unavailable."),
            _ => (
                SharedProviderFailureCategory.UpstreamFailure,
                SharedProviderCatalogFailureCodes.RequestRejected,
                "The shared-provider source rejected the catalog request.")
        };
        var retryAfter = category == SharedProviderFailureCategory.RateLimited
            ? SharedProviderRelayRetryAfterParser.Parse(
                response.Headers.RetryAfter?.ToString(),
                DateTimeOffset.UtcNow)
            : null;
        return Failed(category, code, message, retryAfter);
    }

    private static SharedProviderCatalogFetchResult InvalidCatalogResponse()
        => Failed(
            SharedProviderFailureCategory.VersionUnsupported,
            SharedProviderCatalogFailureCodes.ContractInvalid,
            "The shared-provider catalog response is incompatible or invalid.");

    private static SharedProviderCatalogFetchResult Failed(
        SharedProviderFailureCategory category,
        SharedProviderFailureCode code,
        string message,
        TimeSpan? retryAfter = null)
        => new SharedProviderCatalogFetchResult.Failed(
            new SharedProviderFailure(category, code, message, retryAfter: retryAfter));

    private static bool TryReadEntityTag(
        HttpResponseMessage response,
        out SharedProviderCatalogEntityTag entityTag)
    {
        entityTag = default;
        if (!response.Headers.TryGetValues("ETag", out var values))
        {
            return false;
        }

        var candidates = values.Take(2).ToArray();
        if (candidates.Length != 1)
        {
            return false;
        }

        try
        {
            entityTag = new SharedProviderCatalogEntityTag(candidates[0]);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool HasSupportedContentType(MediaTypeHeaderValue? contentType)
        => contentType is not null &&
            string.Equals(contentType.MediaType, "application/json", StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrEmpty(contentType.CharSet) ||
                string.Equals(contentType.CharSet.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase));

    private static async Task<byte[]> ReadBoundedAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        using var destination = new MemoryStream();
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return destination.ToArray();
            }

            if (destination.Length + read > MaximumResponseBytes)
            {
                return [];
            }

            destination.Write(buffer, 0, read);
        }
    }
}
