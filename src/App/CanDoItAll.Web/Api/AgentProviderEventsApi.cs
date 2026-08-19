using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
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

    internal static async Task<IResult> StreamChatCompletionAsync(
        Guid providerId,
        ProviderChatCompletionApiRequest request,
        HttpContext context,
        IAgentFrameworkWorkspaceService workspaceService,
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
            completion = workspaceService.RunProviderTestChatAsync(
                providerId,
                request.ToProviderRequest(),
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

public sealed record ProviderChatCompletionAccepted(
    Guid OperationId,
    Guid ProviderId);

public sealed record ProviderChatCompletionRunning(
    Guid OperationId,
    Guid ProviderId);

public sealed record ProviderChatCompletionCompleted(
    Guid OperationId,
    Guid ProviderId,
    ProviderTestChatResult Result);

public sealed record ProviderChatCompletionFailed(
    Guid OperationId,
    Guid ProviderId,
    string Code,
    string Message);
