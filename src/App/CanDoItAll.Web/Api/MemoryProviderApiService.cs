using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Modules.Memory.Services;

namespace CanDoItAll.Web.Api;

internal sealed class MemoryProviderApiService(
    IMemoryProviderProfileConfigurationService profileConfigurationService,
    IMemoryOperationHandler operationHandler,
    TimeProvider timeProvider)
{
    private const int MaximumQueryLength = 32_768;
    private static readonly TimeSpan OperationRetention = TimeSpan.FromDays(7);
    private static readonly TimeSpan OperationForgetAfter = TimeSpan.FromDays(30);

    public async Task<IReadOnlyList<MemoryProviderProfileApiResponse>> ListProfilesAsync(
        CancellationToken cancellationToken)
    {
        var profiles = await profileConfigurationService.ListAsync(cancellationToken).ConfigureAwait(false);
        return profiles
            .OrderBy(snapshot => snapshot.Profile.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(snapshot => snapshot.Profile.InstanceId.Value, StringComparer.Ordinal)
            .Select(MemoryProviderApiResponseMapper.MapProfile)
            .ToArray();
    }

    public async Task<MemoryProviderProfileApiResponse?> GetProfileAsync(
        string providerId,
        CancellationToken cancellationToken)
    {
        var parsedProviderId = ParseProviderId(providerId);
        var snapshot = await profileConfigurationService
            .GetAsync(parsedProviderId, cancellationToken)
            .ConfigureAwait(false);
        return snapshot is null
            ? null
            : MemoryProviderApiResponseMapper.MapProfile(snapshot);
    }

    public async Task<MemoryProviderProfileApiResponse> SaveProfileAsync(
        string providerId,
        MemoryProviderProfileApiRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Capabilities is null)
        {
            throw new MemoryProviderApiRequestException("Memory provider capabilities are required.");
        }

        var parsedProviderId = ParseProviderId(providerId);
        var driverKind = MapDriverKind(request.DriverKind);
        var configuration = new MemoryProviderProfileConfiguration(
            request.DisplayName,
            driverKind,
            request.IsEnabled,
            MapFallbackBehavior(request.FallbackBehavior),
            request.ProviderKind,
            request.SelectionTags?.ToArray() ?? [],
            new MemoryProviderProfileCapabilityConfiguration(
                request.Capabilities.SupportsSynchronousQueries,
                request.Capabilities.SupportsAsynchronousQueries,
                request.Capabilities.SupportsOperationStatus),
            request.Http is null
                ? null
                : new MemoryProviderHttpTransportConfiguration(
                    request.Http.BaseUrl,
                    request.Http.QueryPath,
                    request.Http.HealthPath,
                    request.Http.ApiKeyEnvironmentVariable,
                    request.Http.AuthHeaderName,
                    request.Http.AuthScheme,
                    request.Http.TimeoutMilliseconds,
                    request.Http.MaxRetryAttempts),
            request.Mcp is null
                ? null
                : new MemoryProviderMcpTransportConfiguration(
                    request.Mcp.DescriptorKind,
                    request.Mcp.ServerKey,
                    request.Mcp.DisplayName,
                    request.Mcp.Description,
                    request.Mcp.RemoteEndpoint,
                    request.Mcp.AuthHeaderName,
                    request.Mcp.AuthHeaderEnvironmentVariable,
                    request.Mcp.ContextQueryTool,
                    request.Mcp.OperationStatusTool));
        var snapshot = await profileConfigurationService
            .SaveAsync(parsedProviderId, configuration, cancellationToken)
            .ConfigureAwait(false);
        return MemoryProviderApiResponseMapper.MapProfile(snapshot);
    }

    public async Task<MemoryProviderApiExecutionResult<MemoryProviderQueryApiResponse>> ExecuteQueryAsync(
        string providerId,
        MemoryProviderQueryApiRequest request,
        string requesterId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = NormalizeQuery(request.Query);
        var parsedProviderId = ParseProviderId(providerId);
        var requiredCapability = request.Mode switch
        {
            MemoryProviderQueryMode.Synchronous => MemoryCapabilityIds.ContextQuerySync,
            MemoryProviderQueryMode.Asynchronous => MemoryCapabilityIds.ContextQueryAsync,
            _ => throw new MemoryProviderApiRequestException("Unknown memory provider query mode.")
        };
        var selectionPolicy = MemoryProviderSelectionPolicy.RequireCapability(requiredCapability) with
        {
            ExplicitProviderId = parsedProviderId,
            AllowedProviderIds = [parsedProviderId]
        };
        var operationRequest = MemoryOperationRequestBuilder.Query(
            CreateCaller("memory-providers.query", requesterId),
            selectionPolicy,
            new MemoryContextQueryRequest(
                query,
                [requiredCapability],
                MemorySourceProvenance.None),
            CreateRetentionPolicy());
        var result = await operationHandler
            .ExecuteQueryAsync(operationRequest, cancellationToken)
            .ConfigureAwait(false);
        var response = new MemoryProviderQueryApiResponse(
            result.Status,
            result.Diagnostic,
            MemoryProviderApiResponseMapper.MapSelection(result.Selection),
            MemoryProviderApiResponseMapper.MapOperation(result.OperationRecord),
            MemoryProviderApiResponseMapper.MapContextPack(result.Output),
            MemoryProviderApiResponseMapper.MapAcceptedOperation(result.AcceptedOperation),
            result.FeedbackHandle?.Value,
            result.DriverDispatchAttempted);
        return new MemoryProviderApiExecutionResult<MemoryProviderQueryApiResponse>(
            GetStatusCode(result.Status),
            response);
    }

    public async Task<MemoryProviderApiExecutionResult<MemoryProviderOperationStatusApiResponse>> GetOperationStatusAsync(
        Guid operationId,
        string requesterId,
        CancellationToken cancellationToken)
    {
        if (operationId == Guid.Empty)
        {
            throw new MemoryProviderApiRequestException("Memory operation id must not be empty.");
        }

        var parsedOperationId = new MemoryOperationId(operationId);
        var operationRequest = MemoryOperationRequestBuilder.Status(
            CreateCaller("memory-providers.operation.status", requesterId),
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.OperationStatus),
            new MemoryOperationStatusRequest(parsedOperationId),
            CreateRetentionPolicy());
        var result = await operationHandler
            .GetStatusAsync(operationRequest, cancellationToken)
            .ConfigureAwait(false);
        var response = new MemoryProviderOperationStatusApiResponse(
            result.Status,
            result.Diagnostic,
            MemoryProviderApiResponseMapper.MapSelection(result.Selection),
            MemoryProviderApiResponseMapper.MapOperation(result.Output ?? result.OperationRecord));
        return new MemoryProviderApiExecutionResult<MemoryProviderOperationStatusApiResponse>(
            GetStatusCode(result.Status),
            response);
    }

    private static MemoryOperationCaller CreateCaller(string route, string requesterId) =>
        MemoryOperationCaller.ApiEndpoint(
            route,
            new MemoryLedgerRequester(
                requesterId,
                AgentId: null,
                AgentRole: null,
                SessionId: null,
                WorkflowId: null,
                WorkflowNodeId: null,
                ProcessId: null,
                ProcessStepId: null));

    private MemoryLedgerRetentionPolicy CreateRetentionPolicy()
    {
        var now = timeProvider.GetUtcNow();
        return MemoryLedgerRetentionPolicy.Expiring(
            now.Add(OperationRetention),
            now.Add(OperationForgetAfter));
    }

    private static string NormalizeQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new MemoryProviderApiRequestException("Memory provider query must not be empty.");
        }

        var normalized = query.Trim();
        if (normalized.Length > MaximumQueryLength)
        {
            throw new MemoryProviderApiRequestException(
                $"Memory provider query cannot exceed {MaximumQueryLength} characters.");
        }

        return normalized;
    }

    private static MemoryProviderInstanceId ParseProviderId(string providerId)
    {
        try
        {
            return MemoryProviderInstanceId.Parse(providerId);
        }
        catch (ArgumentException exception)
        {
            throw new MemoryProviderApiRequestException(exception.Message, exception);
        }
    }

    private static MemoryProviderDriverKind MapDriverKind(
        MemoryProviderDriverKindApiRequest driverKind) =>
        driverKind switch
        {
            MemoryProviderDriverKindApiRequest.Http => MemoryProviderDriverKind.Http,
            MemoryProviderDriverKindApiRequest.Mcp => MemoryProviderDriverKind.Mcp,
            MemoryProviderDriverKindApiRequest.NativeRemote => MemoryProviderDriverKind.NativeRemote,
            MemoryProviderDriverKindApiRequest.Mock => MemoryProviderDriverKind.Mock,
            _ => throw new MemoryProviderApiRequestException(
                $"Memory provider driver '{driverKind}' is not supported by the provider API.")
        };

    private static MemoryProviderFallbackBehavior MapFallbackBehavior(
        MemoryProviderFallbackBehaviorApiRequest fallbackBehavior) =>
        fallbackBehavior switch
        {
            MemoryProviderFallbackBehaviorApiRequest.DenyImplicitFallback =>
                MemoryProviderFallbackBehavior.DenyImplicitFallback,
            MemoryProviderFallbackBehaviorApiRequest.AllowDefaultProviderWhenNoAssignment =>
                MemoryProviderFallbackBehavior.AllowDefaultProviderWhenNoAssignment,
            _ => throw new MemoryProviderApiRequestException(
                $"Memory provider fallback behavior '{fallbackBehavior}' is not supported by the provider API.")
        };

    private static int GetStatusCode(MemoryOperationHandlerStatus status) =>
        status switch
        {
            MemoryOperationHandlerStatus.Completed => StatusCodes.Status200OK,
            MemoryOperationHandlerStatus.Accepted => StatusCodes.Status202Accepted,
            MemoryOperationHandlerStatus.ProviderNotFound or
                MemoryOperationHandlerStatus.NotFound => StatusCodes.Status404NotFound,
            MemoryOperationHandlerStatus.CapabilityDenied or
                MemoryOperationHandlerStatus.ProviderDenied or
                MemoryOperationHandlerStatus.AccessDenied => StatusCodes.Status403Forbidden,
            MemoryOperationHandlerStatus.DriverUnavailable or
                MemoryOperationHandlerStatus.DriverFailed or
                MemoryOperationHandlerStatus.SourceCaptureFailed or
                MemoryOperationHandlerStatus.Failed => StatusCodes.Status502BadGateway,
            MemoryOperationHandlerStatus.TimedOut => StatusCodes.Status504GatewayTimeout,
            _ => StatusCodes.Status409Conflict
        };

}

internal sealed record MemoryProviderApiExecutionResult<TResponse>(
    int StatusCode,
    TResponse Response);

internal sealed class MemoryProviderApiRequestException : Exception
{
    public MemoryProviderApiRequestException(string message)
        : base(message)
    {
    }

    public MemoryProviderApiRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
