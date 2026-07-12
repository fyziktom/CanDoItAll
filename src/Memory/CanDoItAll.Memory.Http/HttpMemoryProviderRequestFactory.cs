using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Protocol.Http;

namespace CanDoItAll.Memory.Http;

internal static class HttpMemoryProviderRequestFactory
{
    public static HttpRequestMessage CreatePostRequest(
        Uri uri,
        HttpMemoryContextQueryRequest requestBody,
        HttpMemoryProviderConfiguration configuration)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(requestBody, options: HttpMemoryProviderJson.Options)
        };
        ApplyAuthentication(request, configuration);
        return request;
    }

    public static void ApplyAuthentication(
        HttpRequestMessage request,
        HttpMemoryProviderConfiguration configuration)
    {
        var apiKey = configuration.ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        if (string.Equals(configuration.AuthHeaderName, "Authorization", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                configuration.AuthScheme,
                apiKey);
            return;
        }

        if (!request.Headers.TryAddWithoutValidation(configuration.AuthHeaderName, apiKey))
        {
            throw new InvalidOperationException(
                $"HTTP memory provider authentication header '{configuration.AuthHeaderName}' could not be applied.");
        }
    }

    public static HttpMemoryContextQueryRequest CreateQueryRequest(
        MemoryProviderProfile provider,
        MemoryOperationRecord operation,
        MemoryContextQueryRequest request)
    {
        var envelope = new MemoryOperationEnvelope<MemoryContextQueryRequest>(
            MemoryProtocolVersion.Current,
            operation.OperationId,
            operation.CorrelationId,
            operation.CausationId,
            provider.InstanceId,
            operation.OperationKind,
            CreateRequesterContext(operation.Requester),
            request.Context.Workspace,
            request.Context.Execution,
            request.Context.Policy,
            request.Context.Budget,
            request,
            request.Context.Extensions);
        return new HttpMemoryContextQueryRequest(
            operation.OperationId.Value.ToString("D"),
            operation.CorrelationId.Value.ToString("D"),
            operation.CausationId.Value.ToString("D"),
            provider.InstanceId.Value,
            operation.RequestedCapability.Value,
            MemoryProtocolVersion.Current.Value,
            request.Query,
            request.RequestedCapabilities.Select(capability => capability.Value).ToArray(),
            envelope);
    }

    private static MemoryRequesterContext CreateRequesterContext(MemoryLedgerRequester requester)
    {
        return new MemoryRequesterContext(
            requester.RequesterId,
            "memory context query",
            requester.AgentId,
            requester.AgentRole,
            requester.SessionId,
            UserVisibleTask: null);
    }

    public static bool SupportsAnyRequestedCapability(
        MemoryProviderProfile provider,
        MemoryContextQueryRequest request)
    {
        var supportedCapabilities = provider.Manifest.Capabilities
            .Where(capability => capability.Supported)
            .Select(capability => capability.Id)
            .ToHashSet();
        return request.RequestedCapabilities.Count == 0 ||
            request.RequestedCapabilities.Any(supportedCapabilities.Contains);
    }

    public static bool ShouldRetry(
        HttpStatusCode statusCode,
        int attempt,
        int maxRetryAttempts)
    {
        return attempt < maxRetryAttempts &&
            ((int)statusCode >= 500 || statusCode == HttpStatusCode.RequestTimeout);
    }
}
