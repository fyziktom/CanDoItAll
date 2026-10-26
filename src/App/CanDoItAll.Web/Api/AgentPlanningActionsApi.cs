using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.Options;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace CanDoItAll.Web.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record AgentPlanningActionApiSubmission(AgentPlanningActionRequest Request,
    AgentExecutionOperationId? ActivityOperationId = null);

internal static class AgentPlanningActionsApi {
    internal const string InvalidRequestCode = "agents.planning-request-invalid";
    internal const string RefusedCode = "agents.planning-action-refused";
    internal const string UnconfirmedCode = "agents.planning-action-unconfirmed";

    internal static void MapAgentPlanningActionsApi(this RouteGroupBuilder agents) {
        agents.MapPost("/execution-runs/{executionRunId:guid}/planning/actions", ApplyAsync)
            .WithName("ApplyAgentPlanningAction")
            .WithMetadata(new ApiEndpointDocumentation("Exact Agent planning action result and current review state.", "Original displayed expectation and explicit approval, refresh or cancellation; maximum 8192 UTF-8 bytes."))
            .WithApiPermission(ApiAccessScopeNames.WriteAgents)
            .WithSummary("Apply an exact displayed planning approval, refresh or cancellation.")
            .WithDescription("Requires the original chat, decision generation and lossless run/journal revisions. Approval names its exact saved publication; Refresh requires renewed review. An uncertain result never permits automatic replay. The JSON body is limited to 8192 UTF-8 bytes.")
            .Accepts<AgentPlanningActionApiSubmission>("application/json")
            .Produces<AgentExecutionRunDetailApiResponse>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict, StatusCodes.Status413PayloadTooLarge, StatusCodes.Status415UnsupportedMediaType,
                StatusCodes.Status503ServiceUnavailable);
    }

    internal static async Task<IResult> ApplyAsync([Description("Original selected Agent planning execution ID, matching the submitted expectation.")] Guid executionRunId, HttpContext context,
        IOptions<JsonOptions> options, ILoggerFactory logs, CancellationToken cancellationToken) {
        context.Response.Headers.CacheControl = "no-store";
        AgentPlanningActionApiSubmission submission;
        try {
            if (!context.Request.HasJsonContentType()) {
                return Invalid(StatusCodes.Status415UnsupportedMediaType);
            }
            if (context.Request.ContentLength > AgentPlanningActionRequest.MaximumUtf8Bytes) {
                return Invalid(StatusCodes.Status413PayloadTooLarge);
            }
            var bytes = new byte[AgentPlanningActionRequest.MaximumUtf8Bytes + 1];
            var length = await context.Request.Body.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false, cancellationToken);
            if (length > AgentPlanningActionRequest.MaximumUtf8Bytes) {
                return Invalid(StatusCodes.Status413PayloadTooLarge);
            }
            using var document = JsonDocument.Parse(bytes.AsMemory(0, length), new() { MaxDepth = 16 });
            AgentApiRequestValidation.RequireUniqueMembers(document.RootElement);
            var json = new JsonSerializerOptions(options.Value.SerializerOptions) {
                PropertyNameCaseInsensitive = false, MaxDepth = 16, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true
            };
            submission = document.Deserialize<AgentPlanningActionApiSubmission>(json) ?? throw new JsonException("A planning action is required.");
            if (executionRunId == Guid.Empty || submission.Request.Expected.ExecutionRunId != executionRunId ||
                submission.ActivityOperationId is { Value: var id } && id == Guid.Empty) {
                return Invalid(StatusCodes.Status400BadRequest);
            }
        } catch (Exception exception) when (exception is JsonException or ArgumentException) {
            return Invalid(StatusCodes.Status400BadRequest);
        }
        var operationId = submission.ActivityOperationId ?? AgentExecutionOperationId.New();
        AgentActivityApiResults.SetOperationIdHeader(context.Response, operationId);
        try {
            var workspace = context.RequestServices.GetRequiredService<IAgentFrameworkWorkspaceService>();
            var result = await workspace.ApplyPlanningActionAsync(submission.Request, operationId, cancellationToken);
            return Results.Ok(AgentApiResponseMapper.ToExecutionRunDetail(result));
        } catch (AgentExecutionActivityAdmissionException exception) {
            return AgentActivityApiResults.FromAdmissionException(context, exception, executionRunId: executionRunId,
                chatSessionId: submission.Request.ChatSessionId);
        } catch (Exception exception) when (AgentEffectRecoveryApiResults.Handles(exception)) {
            return AgentEffectRecoveryApiResults.ToHttp(context, exception, executionRunId);
        } catch (Exception exception) when (exception is AgentPlanningAdmissionException or AgentToolAdmissionException) {
            return ApiEndpointResults.AgentFailure(context, StatusCodes.Status409Conflict,
                "The planning action was refused. Reload the original review; an uncertain publication must not be replayed.",
                RefusedCode, executionRunId: executionRunId, chatSessionId: submission.Request.ChatSessionId);
        } catch (UnauthorizedAccessException) {
            return ApiEndpointResults.AgentFailure(context, StatusCodes.Status403Forbidden,
                "Current access does not permit this planning action.", RefusedCode,
                executionRunId: executionRunId, chatSessionId: submission.Request.ChatSessionId);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) when (exception is not OutOfMemoryException) {
            logs.CreateLogger(typeof(AgentPlanningActionsApi).FullName!).LogWarning(exception,
                "Planning action {Action} for run {RunId} and operation {OperationId} returned no acknowledged outcome.",
                submission.Request.Action, executionRunId, operationId.Value);
            return ApiEndpointResults.AgentFailure(context, StatusCodes.Status503ServiceUnavailable,
                "The planning action has no confirmed response. Preserve the original run and review its saved receipts before further work.",
                UnconfirmedCode, executionRunId: executionRunId, chatSessionId: submission.Request.ChatSessionId);
        }
    }

    private static IResult Invalid(int status) => ApiAccessEndpoints.Error(status,
        "The planning action request is invalid; no action was dispatched.", InvalidRequestCode);

}
