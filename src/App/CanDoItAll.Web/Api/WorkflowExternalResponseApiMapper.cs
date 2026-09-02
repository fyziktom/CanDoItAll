using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Web.Api;

internal static class WorkflowExternalResponseApiMapper
{
    public static IResult Map(WorkflowExternalResponseServiceResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var response = WorkflowApiSafeProjection.Map(result);
        return result.Outcome switch
        {
            WorkflowExternalResponseServiceOutcome.Completed or
            WorkflowExternalResponseServiceOutcome.WaitingAgain or
            WorkflowExternalResponseServiceOutcome.Denied => Results.Ok(response),
            WorkflowExternalResponseServiceOutcome.Resuming =>
                Results.Json(response, statusCode: StatusCodes.Status202Accepted),
            WorkflowExternalResponseServiceOutcome.InvalidResponse =>
                Results.Json(response, statusCode: StatusCodes.Status400BadRequest),
            WorkflowExternalResponseServiceOutcome.Unauthenticated =>
                Results.Json(response, statusCode: StatusCodes.Status401Unauthorized),
            WorkflowExternalResponseServiceOutcome.Forbidden or
            WorkflowExternalResponseServiceOutcome.AuthorizationContextUnavailable =>
                Results.Json(response, statusCode: StatusCodes.Status403Forbidden),
            WorkflowExternalResponseServiceOutcome.RequestNotFound or
            WorkflowExternalResponseServiceOutcome.RunNotFound or
            WorkflowExternalResponseServiceOutcome.OperationNotFound =>
                Results.Json(response, statusCode: StatusCodes.Status404NotFound),
            WorkflowExternalResponseServiceOutcome.RequestVersionMismatch or
            WorkflowExternalResponseServiceOutcome.RequestNotPending or
            WorkflowExternalResponseServiceOutcome.RunNotWaiting or
            WorkflowExternalResponseServiceOutcome.IdempotencyConflict or
            WorkflowExternalResponseServiceOutcome.ActiveOperationConflict =>
                Results.Json(response, statusCode: StatusCodes.Status409Conflict),
            WorkflowExternalResponseServiceOutcome.Cancelled or
            WorkflowExternalResponseServiceOutcome.Superseded =>
                Results.Json(response, statusCode: StatusCodes.Status410Gone),
            WorkflowExternalResponseServiceOutcome.LegacyNonResumable or
            WorkflowExternalResponseServiceOutcome.CheckpointMissing or
            WorkflowExternalResponseServiceOutcome.CheckpointCorrupt or
            WorkflowExternalResponseServiceOutcome.CheckpointIncompatible or
            WorkflowExternalResponseServiceOutcome.TopologyMismatch or
            WorkflowExternalResponseServiceOutcome.WorkflowVersionMismatch or
            WorkflowExternalResponseServiceOutcome.RequestMismatch =>
                Results.Json(response, statusCode: StatusCodes.Status422UnprocessableEntity),
            WorkflowExternalResponseServiceOutcome.BackendUnavailable or
            WorkflowExternalResponseServiceOutcome.RetryableFailure =>
                Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable),
            WorkflowExternalResponseServiceOutcome.TerminalFailure =>
                Results.Json(
                    response with { Message = "The workflow external response failed." },
                    statusCode: StatusCodes.Status500InternalServerError),
            _ =>
                Results.Json(
                    response with { Message = "The workflow external response failed." },
                    statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    public static IResult InvalidRequest(string message)
        => Results.Json(
            Empty(
                WorkflowExternalResponseServiceOutcome.InvalidResponse,
                message),
            statusCode: StatusCodes.Status400BadRequest);

    public static IResult UnexpectedFailure()
        => Results.Json(
            Empty(
                WorkflowExternalResponseServiceOutcome.TerminalFailure,
                "The workflow external response failed."),
            statusCode: StatusCodes.Status500InternalServerError);

    private static WorkflowExternalResponseApiResponse Empty(
        WorkflowExternalResponseServiceOutcome outcome,
        string message)
        => new(
            OperationId: null,
            RequestId: null,
            ExpectedRequestVersion: null,
            RunId: null,
            outcome,
            OperationState: null,
            OperationOutcome: null,
            RunState: null,
            AcceptedAtUtc: null,
            StartedAtUtc: null,
            CompletedAtUtc: null,
            Replayed: false,
            Bound(message),
            NextPendingRequest: null);

    private static string Bound(string? message)
    {
        const int maximumLength = 4_096;
        var safe = message?.Trim();
        if (string.IsNullOrWhiteSpace(safe))
        {
            return "The workflow external response could not be processed.";
        }

        return safe.Length <= maximumLength
            ? safe
            : safe[..maximumLength];
    }
}
