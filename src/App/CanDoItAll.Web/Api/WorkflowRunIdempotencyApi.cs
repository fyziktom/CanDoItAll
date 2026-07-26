using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Web.Api;

internal static class WorkflowRunIdempotencyApi
{
    public static RouteGroupBuilder MapWorkflowRunIdempotencyApi(this RouteGroupBuilder workflows)
    {
        workflows.MapGet("/runs/by-idempotency-key/{key}", async (
                string key,
                IWorkflowLaunchIdempotencyQueryService queryService,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var evidence = await queryService.FindApiKeyAsync(
                        new WorkflowLaunchIdempotencyKey(key),
                        cancellationToken);
                    return evidence is null
                        ? ApiEndpointResults.NotFound(
                            "No workflow run was found for the supplied idempotency key.",
                            "workflows.idempotency-key-not-found")
                        : Results.Ok(evidence);
                }
                catch (ArgumentException exception)
                {
                    return ApiEndpointResults.BadRequest(
                        exception.Message,
                        "workflows.idempotency-key-invalid");
                }
            })
            .WithName("GetWorkflowRunByIdempotencyKey")
            .Produces<WorkflowLaunchIdempotencyEvidence>()
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        return workflows;
    }
}
