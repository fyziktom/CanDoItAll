using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.Options;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace CanDoItAll.Web.Api;

internal static class ProcessPlanningReviewApi {
    internal const string InvalidRequestCode = "processes.planning-review-invalid";
    internal const string ChangedCode = "processes.planning-review-changed";
    internal const string UnconfirmedCode = "processes.planning-review-unconfirmed";
    private const string Route = "/runs/{runId:guid}/steps/{stepInstanceId:guid}/planning/{executionRunId:guid}/review";

    internal static void MapProcessPlanningReviewApi(this RouteGroupBuilder processes) {
        processes.MapGet(Route, ReadAsync)
            .WithName("GetProcessPlanningReview")
            .WithApiPermission(ApiAccessScopeNames.ExecuteProcesses)
            .WithSummary("Read the original Process child's planning review and available choices.")
            .Produces<ProcessPlanningReviewApiView>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict, StatusCodes.Status503ServiceUnavailable);
        processes.MapPost(Route, ApplyAsync)
            .WithName("ApplyProcessPlanningReview")
            .WithApiPermission(ApiAccessScopeNames.ExecuteProcesses)
            .WithSummary("Apply or recover one exact manager choice for the original Process child.")
            .WithDescription("Retain the entire request and its action ID before sending. An uncertain response permits only exact recovery of that request. Requires Process and Agent read/write/execute and Project read/write grants. The body is limited to 24576 UTF-8 bytes.")
            .Accepts<ProcessPlanningReviewApiSubmission>("application/json")
            .Produces<ProcessPlanningReviewApiResult>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict, StatusCodes.Status413PayloadTooLarge, StatusCodes.Status415UnsupportedMediaType,
                StatusCodes.Status503ServiceUnavailable);
    }

    internal static async Task<IResult> ReadAsync(Guid runId, Guid stepInstanceId, Guid executionRunId, HttpContext context,
        ProcessPlanningReviewHttpAccess access, ProjectTaskPlanProcessReviewReader reader, ILoggerFactory logs,
        CancellationToken cancellationToken) {
        context.Response.Headers.CacheControl = "no-store";
        if (runId == Guid.Empty || stepInstanceId == Guid.Empty || executionRunId == Guid.Empty) {
            return Invalid(StatusCodes.Status400BadRequest);
        }
        try {
            var manager = await access.CaptureAsync(context, cancellationToken);
            return Results.Ok(ProcessPlanningReviewApiView.From(
                await reader.ReadAsync(new(runId), new(stepInstanceId), executionRunId, manager, cancellationToken)));
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) when (exception is not OutOfMemoryException) {
            return Failure(context, logs, exception, executionRunId, actionId: null);
        }
    }

    internal static async Task<IResult> ApplyAsync(Guid runId, Guid stepInstanceId, Guid executionRunId, HttpContext context,
        IOptions<JsonOptions> options, ProcessPlanningReviewHttpAccess access, ProjectTaskPlanProcessReviewService reviews,
        ILoggerFactory logs, CancellationToken cancellationToken) {
        context.Response.Headers.CacheControl = "no-store";
        ProcessPlanningReviewApiSubmission submission;
        ProjectTaskPlanProcessReviewRequest request;
        try {
            if (!context.Request.HasJsonContentType()) {
                return Invalid(StatusCodes.Status415UnsupportedMediaType);
            }
            if (context.Request.ContentLength > ProcessPlanningReviewApiSubmission.MaximumUtf8Bytes) {
                return Invalid(StatusCodes.Status413PayloadTooLarge);
            }
            var bytes = new byte[ProcessPlanningReviewApiSubmission.MaximumUtf8Bytes + 1];
            var length = await context.Request.Body.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false, cancellationToken);
            if (length > ProcessPlanningReviewApiSubmission.MaximumUtf8Bytes) {
                return Invalid(StatusCodes.Status413PayloadTooLarge);
            }
            using var document = JsonDocument.Parse(bytes.AsMemory(0, length), new() { MaxDepth = 16 });
            AgentApiRequestValidation.RequireUniqueMembers(document.RootElement);
            var json = new JsonSerializerOptions(options.Value.SerializerOptions) {
                PropertyNameCaseInsensitive = false, MaxDepth = 16, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true
            };
            submission = document.Deserialize<ProcessPlanningReviewApiSubmission>(json) ?? throw new JsonException("A review request is required.");
            request = submission.ToDomain(runId, stepInstanceId, executionRunId);
        } catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidDataException) {
            return Invalid(StatusCodes.Status400BadRequest);
        }
        var activity = submission.ActivityOperationId ?? AgentExecutionOperationId.New();
        AgentActivityApiResults.SetOperationIdHeader(context.Response, activity);
        try {
            var manager = await access.CaptureAsync(context, cancellationToken);
            return Results.Ok(ProcessPlanningReviewApiResult.From(await reviews.ReviewAsync(request, manager, activity, cancellationToken)));
        } catch (AgentExecutionActivityAdmissionException exception) {
            return AgentActivityApiResults.FromAdmissionException(context, exception, executionRunId: executionRunId);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) when (exception is not OutOfMemoryException) {
            return Failure(context, logs, exception, executionRunId, request.Agent.ActionId);
        }
    }

    private static IResult Failure(HttpContext context, ILoggerFactory logs, Exception exception, Guid executionRunId, Guid? actionId) {
        if (AgentEffectRecoveryApiResults.Handles(exception)) {
            return AgentEffectRecoveryApiResults.ToHttp(context, exception, executionRunId);
        }
        if (exception is WebCurrentPrincipalDeniedException or UnauthorizedAccessException or ProcessLaunchAuthorityRejectedException) {
            return ApiEndpointResults.AgentFailure(context, StatusCodes.Status403Forbidden,
                "Current access does not permit this original Process planning review.", ChangedCode, executionRunId: executionRunId);
        }
        if (exception is ProcessDecisionReviewAdmissionException or AgentPlanningAdmissionException or AgentToolAdmissionException or
                ProcessRuntimeOptimisticConcurrencyException) {
            return ApiEndpointResults.AgentFailure(context, StatusCodes.Status409Conflict,
                "The original review changed or remains unresolved. Preserve the exact submitted request and reload its original Process and child before recovery.",
                ChangedCode, executionRunId: executionRunId);
        }
        logs.CreateLogger(typeof(ProcessPlanningReviewApi).FullName!).LogWarning(exception,
            "Process planning review {ActionId} for child {ExecutionRunId} has no acknowledged response.", actionId, executionRunId);
        return ApiEndpointResults.AgentFailure(context, StatusCodes.Status503ServiceUnavailable,
            "The review response is unavailable. Preserve the exact submitted request; an uncertain result cannot authorize another publication.",
            UnconfirmedCode, executionRunId: executionRunId);
    }

    private static IResult Invalid(int status)
        => ApiAccessEndpoints.Error(status, "The Process planning review request is invalid; no action was dispatched.", InvalidRequestCode);
}
