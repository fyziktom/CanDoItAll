namespace CanDoItAll.Web.Api;

internal static class AgentApiRequestValidation
{
    internal const string InvalidRequestCode = "agents.request-invalid";

    public static IResult? ValidateCommand(
        HttpContext context,
        Guid agentId,
        Guid? chatSessionId,
        string? prompt)
    {
        if (agentId == Guid.Empty)
        {
            return Invalid(context, "Agent id cannot be empty.");
        }

        if (chatSessionId == Guid.Empty)
        {
            return Invalid(
                context,
                "Chat session id cannot be empty.",
                agentId,
                chatSessionId: null);
        }

        return string.IsNullOrWhiteSpace(prompt)
            ? Invalid(
                context,
                "Prompt cannot be empty.",
                agentId,
                chatSessionId: chatSessionId)
            : null;
    }

    public static IResult? ValidateExecutionRun(
        HttpContext context,
        Guid executionRunId)
    {
        return executionRunId == Guid.Empty
            ? Invalid(
                context,
                "Agent execution run id cannot be empty.",
                executionRunId: null)
            : null;
    }

    private static IResult Invalid(
        HttpContext context,
        string message,
        Guid? agentId = null,
        Guid? executionRunId = null,
        Guid? chatSessionId = null)
    {
        return ApiEndpointResults.AgentValidationFailure(
            context,
            message,
            InvalidRequestCode,
            agentId,
            executionRunId,
            chatSessionId);
    }
}
