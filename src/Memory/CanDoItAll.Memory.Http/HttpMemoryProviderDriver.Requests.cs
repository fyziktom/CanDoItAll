using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Http;

public sealed partial class HttpMemoryProviderDriver
{
    private static HttpRequestMessage CreatePostRequest(
        Uri uri,
        HttpMemoryContextQueryRequest requestBody,
        HttpMemoryProviderConfiguration configuration)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(requestBody, options: JsonOptions)
        };
        ApplyAuthentication(request, configuration);
        return request;
    }

    private static void ApplyAuthentication(
        HttpRequestMessage request,
        HttpMemoryProviderConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.ApiKey))
        {
            return;
        }

        if (string.Equals(configuration.AuthHeaderName, "Authorization", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                configuration.AuthScheme,
                configuration.ApiKey);
            return;
        }

        request.Headers.TryAddWithoutValidation(configuration.AuthHeaderName, configuration.ApiKey);
    }

    private static HttpMemoryContextQueryRequest CreateQueryRequest(
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
            MemoryWorkspaceContext.None,
            CreateExecutionContext(operation.Requester),
            MemoryPolicyContext.InternalDefault,
            MemoryBudget.Default,
            request,
            MemoryExtensionData.Empty);
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

    private static MemoryExecutionContext CreateExecutionContext(MemoryLedgerRequester requester)
    {
        return new MemoryExecutionContext(
            ProjectId: null,
            ProjectName: null,
            requester.ProcessId,
            requester.ProcessStepId,
            ProcessStepName: null,
            requester.WorkflowId,
            requester.WorkflowNodeId,
            ArtifactIds: []);
    }

    private static bool SupportsAnyRequestedCapability(
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

    private static bool ShouldRetry(
        HttpStatusCode statusCode,
        int attempt,
        int maxRetryAttempts)
    {
        return attempt < maxRetryAttempts &&
            ((int)statusCode >= 500 || statusCode == HttpStatusCode.RequestTimeout);
    }
}
