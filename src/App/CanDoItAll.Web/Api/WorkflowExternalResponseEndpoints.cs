using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using System.Text.Json;

namespace CanDoItAll.Web.Api;

internal static class WorkflowExternalResponseEndpoints
{
    public static RouteGroupBuilder MapWorkflowExternalResponseApi(
        this RouteGroupBuilder workflows)
    {
        workflows.MapPost("/external-requests/{requestId:guid}/response", SubmitAsync)
            .WithName("RespondToWorkflowExternalRequest")
            .WithMetadata(WorkflowExternalResponseOpenApiContract.Instance)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status200OK)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status202Accepted)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status400BadRequest)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status401Unauthorized)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status403Forbidden)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status404NotFound)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status409Conflict)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status410Gone)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status422UnprocessableEntity)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status500InternalServerError)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status503ServiceUnavailable)
            .ApplyApiAuthorization(workflows, ApiAuthorizationPolicies.RespondWorkflows);

        workflows.MapGet("/external-response-operations/{operationId:guid}", GetStatusAsync)
            .WithName("GetWorkflowExternalResponseOperation")
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status200OK)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status202Accepted)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status400BadRequest)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status401Unauthorized)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status403Forbidden)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status404NotFound)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status409Conflict)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status410Gone)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status422UnprocessableEntity)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status500InternalServerError)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status503ServiceUnavailable)
            .ApplyApiAuthorization(workflows, ApiAuthorizationPolicies.RespondWorkflows);

        return workflows;
    }

    private static async Task<IResult> SubmitAsync(
        Guid requestId,
        HttpContext httpContext,
        IWorkflowExternalResponseService responseService,
        WorkflowExternalResponseApiActorResolver actorResolver,
        ILogger<WorkflowExternalResponseApiLog> logger,
        CancellationToken cancellationToken)
    {
        var idempotency = WorkflowExternalResponseIdempotencyKeyParser.Parse(httpContext.Request);
        if (!idempotency.Succeeded)
        {
            return WorkflowExternalResponseApiMapper.InvalidRequest(idempotency.SafeMessage);
        }

        var body = await WorkflowExternalResponseRequestReader
            .ReadAsync(httpContext.Request, cancellationToken)
            .ConfigureAwait(false);
        if (!body.Succeeded)
        {
            return WorkflowExternalResponseApiMapper.InvalidRequest(body.SafeMessage);
        }

        try
        {
            var request = body.Request!;
            var result = await responseService.SubmitAsync(
                new WorkflowExternalResponseCommand(
                    actorResolver.Resolve(httpContext.User),
                    new WorkflowExternalRequestId(requestId),
                    new WorkflowExternalRequestVersion(request.ExpectedRequestVersion),
                    request.Response,
                    idempotency.Key!.Value,
                    new WorkflowLaunchCorrelationId(httpContext.TraceIdentifier)),
                cancellationToken).ConfigureAwait(false);
            return WorkflowExternalResponseApiMapper.Map(result);
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException)
        {
            return WorkflowExternalResponseApiMapper.InvalidRequest(
                "The workflow external response request is invalid.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Workflow external response submission failed for request {RequestId} and trace {TraceIdentifier}.",
                requestId,
                httpContext.TraceIdentifier);
            return WorkflowExternalResponseApiMapper.UnexpectedFailure();
        }
    }

    private static async Task<IResult> GetStatusAsync(
        Guid operationId,
        HttpContext httpContext,
        IWorkflowExternalResponseService responseService,
        WorkflowExternalResponseApiActorResolver actorResolver,
        ILogger<WorkflowExternalResponseApiLog> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await responseService.GetStatusAsync(
                new WorkflowExternalResponseStatusQuery(
                    actorResolver.Resolve(httpContext.User),
                    new WorkflowExternalResponseOperationId(operationId),
                    new WorkflowLaunchCorrelationId(httpContext.TraceIdentifier)),
                cancellationToken).ConfigureAwait(false);
            return WorkflowExternalResponseApiMapper.Map(result with { Replayed = false });
        }
        catch (ArgumentException)
        {
            return WorkflowExternalResponseApiMapper.InvalidRequest(
                "The workflow external response operation id is invalid.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Workflow external response status failed for operation {OperationId} and trace {TraceIdentifier}.",
                operationId,
                httpContext.TraceIdentifier);
            return WorkflowExternalResponseApiMapper.UnexpectedFailure();
        }
    }

    private sealed class WorkflowExternalResponseApiLog;
}
