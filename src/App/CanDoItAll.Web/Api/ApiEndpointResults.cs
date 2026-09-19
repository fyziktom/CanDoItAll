using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Web.Api;

/// <summary>
/// Acknowledgement returned by a command that has no other result. A success response always carries
/// <c>{ "ok": true }</c>; failures use an error response instead.
/// </summary>
/// <param name="Ok">True: the command completed. A failed command never returns this object.</param>
internal sealed record ApiAck(bool Ok);

/// <summary>
/// Error envelope of the general API families (for example agents, workflows, processes, projects and CRM/HR): an
/// <c>errors</c> array with at least one item, plus correlation fields when an agent run or command failed. Project
/// Structure, LLM Chat and shared-provider operations use their own error formats. The HTTP status alone does not
/// prove that nothing was written; check the operation's documentation for its effect guarantees.
/// </summary>
/// <param name="Errors">The reasons the request was rejected or failed, in the order they were detected.</param>
internal sealed record ApiErrorResponse(IReadOnlyList<ApiErrorItem> Errors)
{
    /// <summary>
    /// Server trace identifier of the failed request, for support and log correlation. Present on agent run and
    /// agent command failures; omitted otherwise. It is not an idempotency key and does not identify a committed
    /// effect.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Identifier of the agent whose run or command failed. Omitted when the failure is not tied to an agent.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? AgentId { get; init; }

    /// <summary>
    /// Identifier of the agent execution run that failed, usable with the agent run read operations. Omitted when no
    /// execution run was created.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ExecutionRunId { get; init; }

    /// <summary>
    /// Identifier of the agent chat session in which the run failed. Omitted when the run was not part of a chat
    /// session.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ChatSessionId { get; init; }

    /// <summary>
    /// Category of a model-provider failure when an agent run failed while calling its provider: requestCompatibility
    /// is returned with HTTP 400, providerConfiguration with HTTP 422, and quotaOrBilling, rateLimit and providerError
    /// with HTTP 503. Omitted for other failures.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AgentProviderFailureCategory? ProviderFailureCategory { get; init; }
}

/// <summary>
/// One reason in an error envelope.
/// </summary>
/// <param name="Code">
/// Stable machine-readable error code, for example <c>crmhr.party.not-found</c> or <c>agents.run-failed</c>. Branch on
/// this value, not on the message.
/// </param>
/// <param name="Message">Human-readable explanation for operators and logs. Its wording can change.</param>
/// <param name="Severity">
/// Severity of the reason: request validation problems are reported as Warning and failed commands as Error. Any
/// item in an error envelope means the request did not succeed.
/// </param>
internal sealed record ApiErrorItem(string Code, string Message, ErrorSeverity Severity);

internal static class ApiEndpointMetadata
{
    public static RouteHandlerBuilder ProducesApiErrors(
        this RouteHandlerBuilder builder,
        params int[] statusCodes)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(statusCodes);

        foreach (var statusCode in statusCodes.Distinct())
        {
            builder.Produces<ApiErrorResponse>(statusCode);
        }

        return builder;
    }
}

internal static class ApiEndpointResults
{
    public const string CommandFailedCode = "agents.command-failed";
    public const string RunFailedCode = "agents.run-failed";
    public const string ProviderFailedCode = "agents.provider-failed";
    public const string ProviderConfigurationInvalidCode = "agents.provider-configuration-invalid";
    public const string ProviderQuotaUnavailableCode = "agents.provider-quota-unavailable";
    public const string ProviderRateLimitedCode = "agents.provider-rate-limited";
    public const string ProviderRequestIncompatibleCode = "agents.provider-request-incompatible";
    public const string ProviderRequestInvalidCode = "agents.provider-request-invalid";

    public static IResult FromResult(Result result)
    {
        return result.IsSuccess
            ? Results.Ok(new ApiAck(true))
            : Results.BadRequest(MapErrors(result.Errors));
    }

    public static IResult FromResult<T>(Result<T> result)
    {
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(MapErrors(result.Errors));
    }

    public static IResult FromResult(Result result, params string[] notFoundCodes)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(new ApiAck(true));
        }

        return ContainsCode(result.Errors, notFoundCodes)
            ? Results.NotFound(MapErrors(result.Errors))
            : Results.BadRequest(MapErrors(result.Errors));
    }

    public static IResult FromResult<T>(Result<T> result, params string[] notFoundCodes)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return ContainsCode(result.Errors, notFoundCodes)
            ? Results.NotFound(MapErrors(result.Errors))
            : Results.BadRequest(MapErrors(result.Errors));
    }

    public static IResult NotFound(string message, string code)
    {
        return Results.NotFound(MapErrors([Error.Validation(message, code)]));
    }

    public static IResult BadRequest(string message, string code)
    {
        return Results.BadRequest(MapErrors([Error.Validation(message, code)]));
    }

    public static IResult AgentValidationFailure(
        HttpContext context,
        string message,
        string code,
        Guid? agentId = null,
        Guid? executionRunId = null,
        Guid? chatSessionId = null)
        => AgentFailure(
            context,
            StatusCodes.Status400BadRequest,
            message,
            code,
            agentId,
            executionRunId,
            chatSessionId);

    public static IResult AgentFailure(
        HttpContext context,
        int statusCode,
        string message,
        string code,
        Guid? agentId = null,
        Guid? executionRunId = null,
        Guid? chatSessionId = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Results.Json(
            CreateAgentErrorResponse(
                context,
                message,
                code,
                agentId,
                executionRunId,
                chatSessionId,
                providerFailureCategory: null),
            statusCode: statusCode);
    }

    public static IResult AgentRunFailure(
        HttpContext context,
        AgentRunFailedException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var (statusCode, response) = CreateAgentRunFailureResponse(
            context,
            exception.AgentId,
            exception.ExecutionRunId,
            exception.ChatSessionId,
            exception.SanitizedDisplayMessage,
            exception.FailureCategory);
        return Results.Json(response, statusCode: statusCode);
    }

    public static IResult AgentRunFailure(
        HttpContext context,
        AgentChatRunFailedException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var (statusCode, response) = CreateAgentRunFailureResponse(
            context,
            exception.AgentId,
            exception.ExecutionRunId,
            exception.ChatSessionId,
            exception.SanitizedDisplayMessage,
            exception.FailureCategory);
        return Results.Json(response, statusCode: statusCode);
    }

    public static ApiErrorResponse AgentRunFailureResponse(
        HttpContext context,
        AgentRunFailedException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return CreateAgentRunFailureResponse(
            context,
            exception.AgentId,
            exception.ExecutionRunId,
            exception.ChatSessionId,
            exception.SanitizedDisplayMessage,
            exception.FailureCategory).Response;
    }

    public static ApiErrorResponse AgentRunFailureResponse(
        HttpContext context,
        AgentChatRunFailedException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return CreateAgentRunFailureResponse(
            context,
            exception.AgentId,
            exception.ExecutionRunId,
            exception.ChatSessionId,
            exception.SanitizedDisplayMessage,
            exception.FailureCategory).Response;
    }

    public static ApiErrorResponse AgentCommandFailureResponse(
        HttpContext context,
        Guid? agentId = null,
        Guid? executionRunId = null,
        Guid? chatSessionId = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        return CreateAgentErrorResponse(
            context,
            "The agent command failed.",
            CommandFailedCode,
            agentId,
            executionRunId,
            chatSessionId,
            providerFailureCategory: null);
    }

    public static IResult Conflict(string message, string code)
    {
        return Results.Conflict(MapErrors([Error.Failure(message, code)]));
    }

    public static IResult Unauthorized(string message, string code)
    {
        return Results.Json(
            MapErrors([Error.Validation(message, code)]),
            statusCode: StatusCodes.Status401Unauthorized);
    }

    public static IResult Forbidden(string message, string code)
    {
        return Results.Json(
            MapErrors([Error.Validation(message, code)]),
            statusCode: StatusCodes.Status403Forbidden);
    }

    public static IResult FromException(Exception exception)
    {
        return Results.BadRequest(MapErrors([Error.Failure(exception.Message, "api.request-failed")]));
    }

    private static ApiErrorResponse MapErrors(IReadOnlyList<Error> errors)
    {
        return new ApiErrorResponse(
            errors
                .Select(error => new ApiErrorItem(error.Code, error.Message, error.Severity))
                .ToList());
    }

    private static (int StatusCode, ApiErrorResponse Response) CreateAgentRunFailureResponse(
        HttpContext context,
        Guid agentId,
        Guid executionRunId,
        Guid? chatSessionId,
        string sanitizedDisplayMessage,
        AgentProviderFailureCategory? failureCategory)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (statusCode, code) = failureCategory switch
        {
            null => (
                StatusCodes.Status500InternalServerError,
                RunFailedCode),
            AgentProviderFailureCategory.RequestCompatibility => (
                StatusCodes.Status400BadRequest,
                ProviderRequestIncompatibleCode),
            AgentProviderFailureCategory.ProviderConfiguration => (
                StatusCodes.Status422UnprocessableEntity,
                ProviderConfigurationInvalidCode),
            AgentProviderFailureCategory.QuotaOrBilling => (
                StatusCodes.Status503ServiceUnavailable,
                ProviderQuotaUnavailableCode),
            AgentProviderFailureCategory.RateLimit => (
                StatusCodes.Status503ServiceUnavailable,
                ProviderRateLimitedCode),
            AgentProviderFailureCategory.ProviderError => (
                StatusCodes.Status503ServiceUnavailable,
                ProviderFailedCode),
            _ => throw new ArgumentOutOfRangeException(
                nameof(failureCategory),
                failureCategory,
                "Unknown agent provider failure category.")
        };

        return (
            statusCode,
            CreateAgentErrorResponse(
                context,
                sanitizedDisplayMessage,
                code,
                agentId,
                executionRunId,
                chatSessionId,
                failureCategory));
    }

    private static ApiErrorResponse CreateAgentErrorResponse(
        HttpContext context,
        string message,
        string code,
        Guid? agentId,
        Guid? executionRunId,
        Guid? chatSessionId,
        AgentProviderFailureCategory? providerFailureCategory)
    {
        return new ApiErrorResponse(
            [new ApiErrorItem(code, message, ErrorSeverity.Error)])
        {
            CorrelationId = context.TraceIdentifier,
            AgentId = NormalizeId(agentId),
            ExecutionRunId = NormalizeId(executionRunId),
            ChatSessionId = NormalizeId(chatSessionId),
            ProviderFailureCategory = providerFailureCategory
        };
    }

    private static Guid? NormalizeId(Guid? value)
        => value is { } id && id != Guid.Empty
            ? id
            : null;

    private static bool ContainsCode(IReadOnlyList<Error> errors, IReadOnlyCollection<string> codes)
        => codes.Count > 0 && errors.Any(error => codes.Contains(error.Code, StringComparer.Ordinal));
}
