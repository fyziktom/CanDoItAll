using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using IProviderRuntimeAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Api.Streaming;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

internal static class AgentProviderEventsApi
{
    public static RouteGroupBuilder MapAgentProviderEventsApi(this RouteGroupBuilder group)
    {
        group.MapGroup("/agents")
            .WithTags("Agents")
            .DisableAntiforgery()
            .MapPost(
                "/providers/{providerId:guid}/chat-completions/stream",
                StreamChatCompletionAsync)
            .WithName("StreamAgentProviderChatCompletion")
            .Accepts<ProviderChatCompletionApiRequest>("application/json")
            .Produces<string>(
                StatusCodes.Status200OK,
                contentType: ServerSentEventResponseWriter.ContentType)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound);

        return group;
    }

    /// <summary>
    /// Send one chat completion request to a provider profile and stream its status and answer as server-sent events.
    /// </summary>
    /// <remarks>
    /// Calls the provider profile directly, outside any agent, chat session or execution run, and reports the call as
    /// a <c>text/event-stream</c>. It is the streaming form of <c>POST /api/agents/providers/{providerId}/test-chat</c>
    /// and is meant for checking a provider profile: the answer arrives once, in the last event, not token by token.
    ///
    /// The request is validated and the provider profile is looked up before the stream starts; those rejections are
    /// returned as JSON with the error statuses below. After HTTP 200 the outcome arrives as an event.
    ///
    /// Events; each has an <c>id:</c> and one JSON <c>data:</c> line with camel-case members:
    ///
    /// - <c>provider.chat.accepted</c> (<c>id: 1</c>): <c>operationId</c>, a new identifier of this stream only, and
    /// <c>providerId</c>.
    /// - <c>provider.chat.running</c> (<c>id: 2</c>): the same members; the provider call has started.
    /// - <c>provider.chat.completed</c> (<c>id: 3</c>): <c>operationId</c>, <c>providerId</c> and <c>result</c> with
    /// <c>model</c> (the model that answered, as reported), <c>responseText</c>, <c>inputTokens</c> and
    /// <c>outputTokens</c>.
    /// - <c>provider.chat.failed</c> (<c>id: 3</c>): <c>operationId</c>, <c>providerId</c>, <c>code</c>
    /// (<c>providers.chat-completion-failed</c>) and <c>message</c>. No failure detail is disclosed: a disabled
    /// provider profile, a provider error and an empty answer all report this event.
    ///
    /// While the provider call runs, a comment line <c>: heartbeat</c> followed by a UTC timestamp is sent every
    /// heartbeat interval (15 seconds by default). The server closes the response after
    /// <c>provider.chat.completed</c> or <c>provider.chat.failed</c>. The stream cannot be resumed: the
    /// <c>Last-Event-ID</c> header is ignored and no other operation accepts its <c>operationId</c>. Closing the
    /// connection cancels the provider call. The call can be recorded in provider request history under the calling
    /// identity.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <param name="providerId">
    /// Identifier of the provider profile to call (not the empty GUID), as listed by <c>GET /api/agents/providers</c>.
    /// </param>
    /// <param name="request">The model to call, an optional system prompt, earlier messages and the new prompt.</param>
    /// <response code="200">
    /// The <c>text/event-stream</c> described above. Unless the client disconnects it ends with
    /// <c>provider.chat.completed</c> or <c>provider.chat.failed</c>.
    /// </response>
    /// <response code="400">
    /// Rejected before the stream started (<c>providers.request-invalid</c>): the empty GUID as provider identifier, a
    /// blank <c>model</c> or <c>prompt</c>, or <c>messages</c> missing or null. A body the framework cannot bind is
    /// rejected with HTTP 400 before the operation runs and has no error envelope.
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// An authorization policy rejected the authenticated caller (<c>api.authorization-forbidden</c>). This operation
    /// itself requires only a valid bearer token.
    /// </response>
    /// <response code="404">
    /// No provider profile with this identifier exists (<c>providers.not-found</c>). Nothing was sent to a provider.
    /// </response>
    internal static async Task<IResult> StreamChatCompletionAsync(
        Guid providerId,
        ProviderChatCompletionApiRequest request,
        HttpContext context,
        IProviderRuntimeAdministrationService providerAdministration,
        IProviderRuntimeProfileSource providerSource,
        IOptions<ApiAccessOptions> apiOptions,
        ILogger<ProviderChatCompletionApiRequest> logger)
    {
        var validation = ValidateRequest(providerId, request);
        if (validation is not null)
        {
            return validation;
        }

        if (await providerSource.GetProviderAsync(
                providerId,
                context.RequestAborted) is null)
        {
            return ApiEndpointResults.NotFound(
                "The provider profile was not found.",
                "providers.not-found");
        }

        var operationId = Guid.NewGuid();
        ServerSentEventResponseWriter.Prepare(context.Response);
        await ServerSentEventResponseWriter.WriteEventAsync(
            context.Response,
            1,
            AgentServerEventNames.ProviderAccepted,
            new ProviderChatCompletionAccepted(operationId, providerId),
            context.RequestAborted);
        using var commandLifetime =
            CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        Task<ProviderTestChatResult> completion;
        try
        {
            completion = providerAdministration.RunProviderTestChatAsync(
                providerId,
                ProviderHistoryRequestContext.WithCaller(request.ToProviderRequest(), context),
                commandLifetime.Token);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            return Results.Empty;
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Provider chat completion failed to start. CorrelationId={CorrelationId} ProviderOperationId={ProviderOperationId} ProviderId={ProviderId} FailureType={FailureType}.",
                context.TraceIdentifier,
                operationId,
                providerId,
                exception.GetType().Name);
            await WriteFailureAsync(
                context,
                operationId,
                providerId);
            return Results.Empty;
        }

        var completionObserved = false;
        try
        {
            await ServerSentEventResponseWriter.WriteEventAsync(
                context.Response,
                2,
                AgentServerEventNames.ProviderRunning,
                new ProviderChatCompletionRunning(operationId, providerId),
                context.RequestAborted);

            ProviderTestChatResult result;
            try
            {
                result = await AwaitWithHeartbeatsAsync(
                    context,
                    completion,
                    apiOptions.Value.ServerSentEvents.HeartbeatInterval);
                completionObserved = true;
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return Results.Empty;
            }
            catch (Exception) when (completion.IsCompleted)
            {
                var completionFailure = await ObserveFailureAsync(completion);
                completionObserved = true;
                if (completionFailure is null)
                {
                    throw;
                }

                logger.LogError(
                    "Provider chat completion failed. CorrelationId={CorrelationId} ProviderOperationId={ProviderOperationId} ProviderId={ProviderId} FailureType={FailureType}.",
                    context.TraceIdentifier,
                    operationId,
                    providerId,
                    completionFailure.GetType().Name);
                await WriteFailureAsync(
                    context,
                    operationId,
                    providerId);
                return Results.Empty;
            }

            await ServerSentEventResponseWriter.WriteEventAsync(
                context.Response,
                3,
                AgentServerEventNames.ProviderCompleted,
                new ProviderChatCompletionCompleted(operationId, providerId, result),
                context.RequestAborted);
            return Results.Empty;
        }
        finally
        {
            if (!completionObserved)
            {
                await ApiCommandTaskLifetime.CancelAndObserveAsync(
                    commandLifetime,
                    completion,
                    logger,
                    operationId);
            }
        }
    }

    private static async Task<Exception?> ObserveFailureAsync(
        Task<ProviderTestChatResult> completion)
    {
        try
        {
            await completion.ConfigureAwait(false);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static Task WriteFailureAsync(
        HttpContext context,
        Guid operationId,
        Guid providerId)
    {
        return ServerSentEventResponseWriter.WriteEventAsync(
            context.Response,
            3,
            AgentServerEventNames.ProviderFailed,
            new ProviderChatCompletionFailed(
                operationId,
                providerId,
                "providers.chat-completion-failed",
                "The provider chat completion failed."),
            context.RequestAborted);
    }

    private static IResult? ValidateRequest(
        Guid providerId,
        ProviderChatCompletionApiRequest request)
    {
        if (providerId == Guid.Empty)
        {
            return ApiEndpointResults.BadRequest(
                "Provider id cannot be empty.",
                "providers.request-invalid");
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return ApiEndpointResults.BadRequest(
                "Provider model cannot be empty.",
                "providers.request-invalid");
        }

        if (request.Messages is null)
        {
            return ApiEndpointResults.BadRequest(
                "Provider messages are required.",
                "providers.request-invalid");
        }

        return string.IsNullOrWhiteSpace(request.Prompt)
            ? ApiEndpointResults.BadRequest(
                "Provider prompt cannot be empty.",
                "providers.request-invalid")
            : null;
    }

    internal static async Task<TResult> AwaitWithHeartbeatsAsync<TResult>(
        HttpContext context,
        Task<TResult> completion,
        TimeSpan heartbeatInterval)
    {
        using var heartbeatTimer = new PeriodicTimer(heartbeatInterval);
        while (true)
        {
            var heartbeat = heartbeatTimer
                .WaitForNextTickAsync(context.RequestAborted)
                .AsTask();
            if (await Task.WhenAny(completion, heartbeat) == completion)
            {
                return await completion;
            }

            if (!await heartbeat)
            {
                return await completion;
            }

            if (completion.IsCompleted)
            {
                return await completion;
            }

            await ServerSentEventResponseWriter.WriteHeartbeatAsync(
                context.Response,
                context.RequestAborted);
        }
    }
}

/// <summary>
/// One chat completion request sent directly to a provider profile, outside any agent: the model to call, an
/// optional system prompt, earlier conversation messages and the new prompt. Send every member.
/// </summary>
/// <param name="Model">Identifier of the model to call through the provider profile; must not be blank.</param>
/// <param name="SystemPrompt">
/// System prompt for the call. A blank value uses a built-in instruction for provider checks.
/// </param>
/// <param name="Messages">
/// Earlier conversation messages sent to the provider before the prompt. Send an empty array for a single question;
/// a missing or null member is rejected with <c>providers.request-invalid</c>.
/// </param>
/// <param name="Prompt">The new user prompt; must not be blank.</param>
public sealed record ProviderChatCompletionApiRequest(
    string Model,
    string SystemPrompt,
    IReadOnlyList<ProviderTestChatMessage> Messages,
    string Prompt)
{
    public ProviderTestChatRequest ToProviderRequest()
    {
        return new ProviderTestChatRequest(Model, SystemPrompt, Messages, Prompt);
    }
}

/// <summary>
/// Data of the <c>provider.chat.accepted</c> server-sent event: the provider chat completion was accepted.
/// </summary>
/// <param name="OperationId">Identifier generated for this stream; no other operation accepts it.</param>
/// <param name="ProviderId">Identifier of the called provider profile, from the route.</param>
public sealed record ProviderChatCompletionAccepted(
    Guid OperationId,
    Guid ProviderId);

/// <summary>
/// Data of the <c>provider.chat.running</c> server-sent event: the provider call has started.
/// </summary>
/// <param name="OperationId">Identifier generated for this stream.</param>
/// <param name="ProviderId">Identifier of the called provider profile.</param>
public sealed record ProviderChatCompletionRunning(
    Guid OperationId,
    Guid ProviderId);

/// <summary>
/// Data of the <c>provider.chat.completed</c> server-sent event: the provider answered.
/// </summary>
/// <param name="OperationId">Identifier generated for this stream.</param>
/// <param name="ProviderId">Identifier of the called provider profile.</param>
/// <param name="Result">The answer: model, response text and reported token counts.</param>
public sealed record ProviderChatCompletionCompleted(
    Guid OperationId,
    Guid ProviderId,
    ProviderTestChatResult Result);

/// <summary>
/// Data of the <c>provider.chat.failed</c> server-sent event: the provider call failed or could not start. The
/// failure detail is logged on the server only.
/// </summary>
/// <param name="OperationId">Identifier generated for this stream.</param>
/// <param name="ProviderId">Identifier of the called provider profile.</param>
/// <param name="Code">Always <c>providers.chat-completion-failed</c>.</param>
/// <param name="Message">Generic human-readable failure text; its wording can change.</param>
public sealed record ProviderChatCompletionFailed(
    Guid OperationId,
    Guid ProviderId,
    string Code,
    string Message);
