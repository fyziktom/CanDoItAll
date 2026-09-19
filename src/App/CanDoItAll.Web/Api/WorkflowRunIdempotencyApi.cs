using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Web.Api;

internal static class WorkflowRunIdempotencyApi
{
    public static RouteGroupBuilder MapWorkflowRunIdempotencyApi(this RouteGroupBuilder workflows)
    {
        workflows.MapGet("/runs/by-idempotency-key/{key}", GetRunByIdempotencyKeyAsync)
            .WithName("GetWorkflowRunByIdempotencyKey")
            .Produces<WorkflowLaunchIdempotencyEvidence>()
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        return workflows;
    }

    /// <summary>
    /// Find the workflow run that a start request with a given <c>Idempotency-Key</c> created.
    /// </summary>
    /// <remarks>
    /// Use this after a start request whose response was lost to learn, without starting anything, whether the key
    /// was recorded and which run it belongs to. The evidence names the original run, the workflow and version
    /// selection, the resolved version and backend, whether the start completed, the run's current state and how often
    /// the key was replayed. It returns hashes of the key and of the request, never the key itself or the run input.
    ///
    /// Only keys sent to the HTTP start operations are found. Keys are global across all API callers of this host,
    /// so the lookup can find a start made by another caller with the same key. <c>claimState</c> Pending means the
    /// original start is still in progress: read again later, or resend the same start request with the same key,
    /// which waits for it and then returns the same run. When the original start failed before admitting a run, its
    /// key was released and this operation returns HTTP 404. Read the run itself with
    /// <c>GET /api/workflows/runs/{runId}</c> using <c>originalRunId</c>.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="key">
    /// The <c>Idempotency-Key</c> value sent to the start operation, for example <c>invoice-4711-review</c>. It is
    /// trimmed and then compared exactly; URL-encode reserved characters.
    /// </param>
    /// <response code="200">The key was recorded; the body describes the start request it belongs to.</response>
    /// <response code="400">
    /// The key is blank or longer than 256 characters after trimming (<c>workflows.idempotency-key-invalid</c>).
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// Reserved for a bearer token that does not authorize the operation (<c>api.authorization-forbidden</c>). The
    /// <c>/api</c> group currently accepts any valid token, so this route does not return it today.
    /// </response>
    /// <response code="404">
    /// No HTTP start request recorded this key (<c>workflows.idempotency-key-not-found</c>). Nothing was started with
    /// it, or its start failed before a run was admitted; it is safe to send the start request with this key.
    /// </response>
    internal static async Task<IResult> GetRunByIdempotencyKeyAsync(
        string key,
        IWorkflowLaunchIdempotencyQueryService queryService,
        CancellationToken cancellationToken)
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
    }
}
