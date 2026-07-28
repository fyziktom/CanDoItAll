namespace CanDoItAll.Web.Api;

internal static class AgentApiRequestValidation
{
    private const string InvalidRequestCode = "agents.request-invalid";

    public static IResult? ValidateCommand(
        Guid agentId,
        Guid? chatSessionId,
        string? prompt)
    {
        if (agentId == Guid.Empty)
        {
            return Invalid("Agent id cannot be empty.");
        }

        if (chatSessionId == Guid.Empty)
        {
            return Invalid("Chat session id cannot be empty.");
        }

        return string.IsNullOrWhiteSpace(prompt)
            ? Invalid("Prompt cannot be empty.")
            : null;
    }

    public static IResult? ValidateExecutionRun(Guid executionRunId)
    {
        return executionRunId == Guid.Empty
            ? Invalid("Agent execution run id cannot be empty.")
            : null;
    }

    private static IResult Invalid(string message)
    {
        return ApiEndpointResults.BadRequest(message, InvalidRequestCode);
    }
}
