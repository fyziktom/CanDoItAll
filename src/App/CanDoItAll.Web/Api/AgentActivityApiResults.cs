using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel.Streaming;

namespace CanDoItAll.Web.Api;

internal static class AgentActivityApiResults
{
    public const string DuplicateOperationCode =
        "agents.execution-operation-duplicate";
    public const string EvictedOperationCode =
        "agents.execution-operation-evicted";
    public const string CapacityExhaustedCode =
        "agents.execution-operation-capacity-exhausted";

    public static void SetOperationIdHeader(
        HttpResponse response,
        AgentExecutionOperationId operationId)
    {
        response.Headers[AgentApiHeaderNames.ActivityOperationId] =
            operationId.Value.ToString("D");
    }

    public static IResult FromAdmissionException(
        HttpContext context,
        AgentExecutionActivityAdmissionException exception,
        Guid? agentId = null,
        Guid? executionRunId = null,
        Guid? chatSessionId = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(exception);

        var (statusCode, code, message) = exception.Reason switch
        {
            AgentExecutionActivityAdmissionRejectionReason.DuplicateOperation => (
                StatusCodes.Status409Conflict,
                DuplicateOperationCode,
                "The agent execution operation id is already in use."),
            AgentExecutionActivityAdmissionRejectionReason.PreviouslyEvicted => (
                StatusCodes.Status410Gone,
                EvictedOperationCode,
                "The agent execution operation id was previously used and its stream was evicted."),
            AgentExecutionActivityAdmissionRejectionReason.CapacityExhausted => (
                StatusCodes.Status503ServiceUnavailable,
                CapacityExhaustedCode,
                "Agent execution activity capacity is temporarily exhausted."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(exception),
                exception.Reason,
                "Unknown agent execution activity admission rejection reason.")
        };

        return ApiEndpointResults.AgentFailure(
            context,
            statusCode,
            message,
            code,
            agentId,
            executionRunId,
            chatSessionId);
    }
}
