using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedKernel.Streaming;
using CanDoItAll.Web.Api.Streaming;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

internal static class AgentEventsApi
{
    public static RouteGroupBuilder MapAgentEventsApi(this RouteGroupBuilder group)
    {
        var agents = group.MapGroup("/agents")
            .WithTags("Agents")
            .DisableAntiforgery();

        agents.MapGet(
                "/execution-operations/{operationId:guid}/events/stream",
                StreamExistingOperationAsync)
            .WithName("StreamAgentExecutionOperationEvents")
            .Produces<string>(
                StatusCodes.Status200OK,
                contentType: ServerSentEventResponseWriter.ContentType)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound);

        agents.MapPost("/{agentId:guid}/chat/stream", StreamChatAsync)
            .WithName("StreamAgentChatMessage")
            .Accepts<AgentChatApiRequest>("application/json")
            .Produces<string>(
                StatusCodes.Status200OK,
                contentType: ServerSentEventResponseWriter.ContentType)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict,
                StatusCodes.Status410Gone,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError,
                StatusCodes.Status503ServiceUnavailable);

        agents.MapPost("/execution-runs/stream", StreamExecutionRunAsync)
            .WithName("StreamAgentExecutionRun")
            .Accepts<AgentExecutionRunApiRequest>("application/json")
            .Produces<string>(
                StatusCodes.Status200OK,
                contentType: ServerSentEventResponseWriter.ContentType)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict,
                StatusCodes.Status410Gone,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError,
                StatusCodes.Status503ServiceUnavailable);

        agents.MapPost("/{agentId:guid}/execution-runs/stream", StreamScopedExecutionRunAsync)
            .WithName("StreamAgentScopedExecutionRun")
            .Accepts<AgentExecutionRunStartApiRequest>("application/json")
            .Produces<string>(
                StatusCodes.Status200OK,
                contentType: ServerSentEventResponseWriter.ContentType)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict,
                StatusCodes.Status410Gone,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError,
                StatusCodes.Status503ServiceUnavailable);

        agents.MapPost(
                "/execution-runs/{executionRunId:guid}/pending-approvals/stream",
                StreamApprovalContinuationAsync)
            .WithName("StreamAgentExecutionApprovalResponse")
            .Accepts<PendingApprovalApiRequest>("application/json")
            .Produces<string>(
                StatusCodes.Status200OK,
                contentType: ServerSentEventResponseWriter.ContentType)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status410Gone,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError,
                StatusCodes.Status503ServiceUnavailable);

        return group;
    }

    private static async Task<IResult> StreamExistingOperationAsync(
        Guid operationId,
        HttpContext context,
        ICurrentProfileAgentExecutionActivityReader activityReader,
        IOptions<ApiAccessOptions> apiOptions)
    {
        if (operationId == Guid.Empty)
        {
            return ApiEndpointResults.BadRequest(
                "The agent execution operation id cannot be empty.",
                "agents.execution-operation-invalid");
        }

        if (!ServerSentEventCursor.TryResolve(
                context.Request,
                out var afterExclusive,
                out var error))
        {
            return ApiEndpointResults.BadRequest(
                error ?? "The SSE cursor is invalid.",
                ServerSentEventResponseWriter.InvalidCursorCode);
        }

        if (afterExclusive == long.MaxValue)
        {
            return ApiEndpointResults.BadRequest(
                "The SSE cursor is exhausted.",
                "sse.cursor-exhausted");
        }

        var fromInclusive = afterExclusive == 0
            ? StreamSequence.Beginning
            : new StreamSequence(afterExclusive + 1);
        var typedOperationId = new AgentExecutionOperationId(operationId);
        await using (var validationReader = activityReader.OpenReader(
                         typedOperationId,
                         StreamSequence.Beginning))
        {
            var validationRead = await validationReader.ReadAsync(context.RequestAborted);
            if (validationRead is SequencedStreamUnknown<AgentExecutionActivity>)
            {
                return ApiEndpointResults.NotFound(
                    "The agent execution operation stream was not found.",
                    "agents.execution-operation-not-found");
            }
        }

        await using var reader = activityReader.OpenReader(
            typedOperationId,
            fromInclusive);
        ServerSentEventResponseWriter.Prepare(context.Response);
        var firstRead = await AgentActivityServerSentEventWriter.ReadWithHeartbeatAsync(
            context,
            reader,
            apiOptions.Value.ServerSentEvents.HeartbeatInterval);
        await AgentActivityServerSentEventWriter.PumpAsync(
            context,
            typedOperationId,
            reader,
            firstRead,
            apiOptions.Value.ServerSentEvents.HeartbeatInterval);
        return Results.Empty;
    }

    private static Task<IResult> StreamChatAsync(
        Guid agentId,
        AgentChatApiRequest request,
        HttpContext context,
        IAgentFrameworkWorkspaceService workspaceService,
        ICurrentProfileAgentExecutionActivityReader activityReader,
        IOptions<ApiAccessOptions> apiOptions,
        ILoggerFactory loggerFactory)
    {
        var validation = AgentApiRequestValidation.ValidateCommand(
            context,
            agentId,
            request.ChatSessionId,
            request.Prompt);
        if (validation is not null)
        {
            return Task.FromResult(validation);
        }

        var operationId = request.ActivityOperationId ?? AgentExecutionOperationId.New();
        return StreamCommandAsync(
            context,
            operationId,
            agentId,
            null,
            request.ChatSessionId,
            cancellationToken => workspaceService.SendMessageAsync(
                agentId,
                request.ChatSessionId,
                request.Prompt,
                new AgentChatRunOptions(operationId),
                cancellationToken,
                request.AttachmentPaths),
            static result => result.ExecutionRunId,
            workspaceService,
            activityReader,
            apiOptions.Value.ServerSentEvents.HeartbeatInterval,
            loggerFactory.CreateLogger("CanDoItAll.Web.Api.AgentChatStream"));
    }

    private static Task<IResult> StreamExecutionRunAsync(
        AgentExecutionRunApiRequest request,
        HttpContext context,
        IAgentFrameworkWorkspaceService workspaceService,
        ICurrentProfileAgentExecutionActivityReader activityReader,
        IOptions<ApiAccessOptions> apiOptions,
        ILoggerFactory loggerFactory)
    {
        var validation = AgentApiRequestValidation.ValidateCommand(
            context,
            request.AgentId,
            request.ChatSessionId,
            request.Prompt);
        if (validation is not null)
        {
            return Task.FromResult(validation);
        }

        var operationId = request.ActivityOperationId ?? AgentExecutionOperationId.New();
        var executionRequest = new ExecutionRunRequest(
            request.AgentId,
            request.Prompt,
            operationId,
            request.ChatSessionId,
            request.Context,
            request.AutoApprovePendingToolCalls,
            InputAttachmentPaths: request.InputAttachmentPaths,
            JsonSchemaOutput: request.StructuredOutput);
        return StreamCommandAsync(
            context,
            operationId,
            request.AgentId,
            null,
            request.ChatSessionId,
            cancellationToken => workspaceService.ExecuteRunAsync(
                executionRequest,
                cancellationToken),
            static result => result.ExecutionRunId,
            workspaceService,
            activityReader,
            apiOptions.Value.ServerSentEvents.HeartbeatInterval,
            loggerFactory.CreateLogger("CanDoItAll.Web.Api.AgentExecutionStream"));
    }

    private static Task<IResult> StreamScopedExecutionRunAsync(
        Guid agentId,
        AgentExecutionRunStartApiRequest request,
        HttpContext context,
        IAgentFrameworkWorkspaceService workspaceService,
        ICurrentProfileAgentExecutionActivityReader activityReader,
        IOptions<ApiAccessOptions> apiOptions,
        ILoggerFactory loggerFactory)
    {
        var validation = AgentApiRequestValidation.ValidateCommand(
            context,
            agentId,
            request.ChatSessionId,
            request.Prompt);
        if (validation is not null)
        {
            return Task.FromResult(validation);
        }

        var operationId = request.ActivityOperationId ?? AgentExecutionOperationId.New();
        var executionRequest = new ExecutionRunRequest(
            agentId,
            request.Prompt,
            operationId,
            request.ChatSessionId,
            request.Context,
            request.AutoApprovePendingToolCalls,
            InputAttachmentPaths: request.InputAttachmentPaths,
            JsonSchemaOutput: request.StructuredOutput);
        return StreamCommandAsync(
            context,
            operationId,
            agentId,
            null,
            request.ChatSessionId,
            cancellationToken => workspaceService.ExecuteRunAsync(
                executionRequest,
                cancellationToken),
            static result => result.ExecutionRunId,
            workspaceService,
            activityReader,
            apiOptions.Value.ServerSentEvents.HeartbeatInterval,
            loggerFactory.CreateLogger("CanDoItAll.Web.Api.AgentExecutionStream"));
    }

    private static Task<IResult> StreamApprovalContinuationAsync(
        Guid executionRunId,
        PendingApprovalApiRequest request,
        HttpContext context,
        IAgentFrameworkWorkspaceService workspaceService,
        ICurrentProfileAgentExecutionActivityReader activityReader,
        IOptions<ApiAccessOptions> apiOptions,
        ILoggerFactory loggerFactory)
    {
        var validation = AgentApiRequestValidation.ValidateExecutionRun(
            context,
            executionRunId);
        if (validation is not null)
        {
            return Task.FromResult(validation);
        }

        var operationId = request.ActivityOperationId ?? AgentExecutionOperationId.New();
        return StreamCommandAsync(
            context,
            operationId,
            null,
            executionRunId,
            null,
            cancellationToken => workspaceService.ContinueExecutionRunAsync(
                executionRunId,
                operationId,
                request.Approved,
                request.AutoApprovePendingToolCalls,
                cancellationToken),
            static result => result.ExecutionRunId,
            workspaceService,
            activityReader,
            apiOptions.Value.ServerSentEvents.HeartbeatInterval,
            loggerFactory.CreateLogger("CanDoItAll.Web.Api.AgentApprovalStream"));
    }

    private static async Task<IResult> StreamCommandAsync<TResult>(
        HttpContext context,
        AgentExecutionOperationId operationId,
        Guid? agentId,
        Guid? knownExecutionRunId,
        Guid? chatSessionId,
        Func<CancellationToken, Task<TResult>> startCommand,
        Func<TResult, Guid> executionRunId,
        IAgentFrameworkWorkspaceService workspaceService,
        ICurrentProfileAgentExecutionActivityReader activityReader,
        TimeSpan heartbeatInterval,
        ILogger logger)
    {
        AgentActivityApiResults.SetOperationIdHeader(
            context.Response,
            operationId);
        using var commandLifetime =
            CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        Task<TResult> completion;
        try
        {
            completion = startCommand(commandLifetime.Token);
        }
        catch (AgentExecutionActivityAdmissionException exception)
        {
            return AgentActivityApiResults.FromAdmissionException(
                context,
                exception,
                agentId,
                knownExecutionRunId,
                chatSessionId);
        }
        catch (AgentJsonSchemaOutputContractException exception)
        {
            return ApiEndpointResults.AgentValidationFailure(
                context,
                exception.Message,
                exception.Code,
                agentId,
                knownExecutionRunId,
                chatSessionId);
        }
        catch (ArgumentException)
        {
            return ApiEndpointResults.AgentValidationFailure(
                context,
                "The agent command request was invalid.",
                AgentApiRequestValidation.InvalidRequestCode,
                agentId,
                knownExecutionRunId,
                chatSessionId);
        }
        catch (AgentChatRunFailedException exception)
        {
            return ApiEndpointResults.AgentRunFailure(context, exception);
        }
        catch (AgentRunFailedException exception)
        {
            return ApiEndpointResults.AgentRunFailure(context, exception);
        }

        var completionObserved = false;
        try
        {
            await using var reader = activityReader.OpenReader(
                operationId,
                StreamSequence.Beginning);
            var firstRead = await reader.ReadAsync(context.RequestAborted);
            if (firstRead is SequencedStreamUnknown<AgentExecutionActivity>)
            {
                throw new InvalidOperationException(
                    "The admitted agent activity stream could not be resolved.");
            }

            ServerSentEventResponseWriter.Prepare(context.Response);
            await AgentActivityServerSentEventWriter.PumpAsync(
                context,
                operationId,
                reader,
                firstRead,
                heartbeatInterval);

            TResult result;
            try
            {
                result = await completion;
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return Results.Empty;
            }
            catch (AgentChatRunFailedException exception)
            {
                logger.LogWarning(
                    "Agent API operation failed after its activity stream started. CorrelationId={CorrelationId} AgentExecutionOperationId={AgentExecutionOperationId} AgentId={AgentId} ChatSessionId={ChatSessionId} ExecutionRunId={ExecutionRunId} FailureCategory={FailureCategory}.",
                    context.TraceIdentifier,
                    operationId,
                    exception.AgentId,
                    exception.ChatSessionId,
                    exception.ExecutionRunId,
                    exception.FailureCategory);
                await ServerSentEventResponseWriter.WriteEventAsync(
                    context.Response,
                    AgentServerEventNames.CommandFailed,
                    CreateCommandFailed(
                        operationId,
                        ApiEndpointResults.AgentRunFailureResponse(
                            context,
                            exception)),
                    context.RequestAborted);
                return Results.Empty;
            }
            catch (AgentRunFailedException exception)
            {
                logger.LogWarning(
                    "Agent API operation failed after its activity stream started. CorrelationId={CorrelationId} AgentExecutionOperationId={AgentExecutionOperationId} AgentId={AgentId} ChatSessionId={ChatSessionId} ExecutionRunId={ExecutionRunId} FailureCategory={FailureCategory}.",
                    context.TraceIdentifier,
                    operationId,
                    exception.AgentId,
                    exception.ChatSessionId,
                    exception.ExecutionRunId,
                    exception.FailureCategory);
                await ServerSentEventResponseWriter.WriteEventAsync(
                    context.Response,
                    AgentServerEventNames.CommandFailed,
                    CreateCommandFailed(
                        operationId,
                        ApiEndpointResults.AgentRunFailureResponse(
                            context,
                            exception)),
                    context.RequestAborted);
                return Results.Empty;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    "Agent API operation failed after its activity stream started. CorrelationId={CorrelationId} AgentExecutionOperationId={AgentExecutionOperationId} AgentId={AgentId} ChatSessionId={ChatSessionId} ExecutionRunId={ExecutionRunId} FailureType={FailureType}.",
                    context.TraceIdentifier,
                    operationId,
                    agentId,
                    chatSessionId,
                    knownExecutionRunId,
                    exception.GetType().Name);
                await ServerSentEventResponseWriter.WriteEventAsync(
                    context.Response,
                    AgentServerEventNames.CommandFailed,
                    CreateCommandFailed(
                        operationId,
                        ApiEndpointResults.AgentCommandFailureResponse(
                            context,
                            agentId,
                            knownExecutionRunId,
                            chatSessionId)),
                    context.RequestAborted);
                return Results.Empty;
            }
            finally
            {
                completionObserved = true;
            }

            var runId = executionRunId(result);
            var detail = await workspaceService.GetExecutionRunDetailAsync(
                runId,
                context.RequestAborted);
            var pendingApprovals =
                AgentActivityServerSentEventWriter.CreatePendingApprovals(
                    detail.Approvals);
            if (pendingApprovals.Count > 0)
            {
                await ServerSentEventResponseWriter.WriteEventAsync(
                    context.Response,
                    AgentServerEventNames.ApprovalRequired,
                    new AgentApprovalRequired(operationId, runId, pendingApprovals),
                    context.RequestAborted);
            }

            await ServerSentEventResponseWriter.WriteEventAsync(
                context.Response,
                AgentServerEventNames.CommandCompleted,
                new AgentCommandCompleted<TResult>(
                    operationId,
                    result),
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

    private static AgentCommandFailed CreateCommandFailed(
        AgentExecutionOperationId operationId,
        ApiErrorResponse response)
    {
        var error = response.Errors.Single();
        return new AgentCommandFailed(
            operationId,
            error.Code,
            error.Message,
            response.CorrelationId,
            response.AgentId,
            response.ExecutionRunId,
            response.ChatSessionId,
            response.ProviderFailureCategory);
    }
}
