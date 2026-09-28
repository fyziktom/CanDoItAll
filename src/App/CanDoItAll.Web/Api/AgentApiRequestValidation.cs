using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Web.Api;

internal static class AgentApiRequestValidation
{
    internal const string InvalidRequestCode = "agents.request-invalid";

    private const string ProcessStepSourceKind = "process-step";

    public static IResult? ValidateCommand(
        HttpContext context,
        Guid agentId,
        Guid? chatSessionId,
        string? prompt,
        ExecutionInvocationContext? invocationContext = null)
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

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return Invalid(
                context,
                "Prompt cannot be empty.",
                agentId,
                chatSessionId: chatSessionId);
        }

        return ValidateInvocationContext(context, invocationContext, agentId, chatSessionId);
    }

    // Process automation starts governed process runs inside the host, and the server trusts the process labels and
    // the reserved metadata of such a run. A run started over HTTP must not claim that origin, and must not bind host
    // folders through the reserved external-target root binding metadata, which the server honors for any run.
    private static IResult? ValidateInvocationContext(
        HttpContext context,
        ExecutionInvocationContext? invocationContext,
        Guid agentId,
        Guid? chatSessionId)
    {
        if (invocationContext is null)
        {
            return null;
        }

        if (string.Equals(invocationContext.SourceKind?.Trim(), ProcessStepSourceKind, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(invocationContext.ProcessRunId) ||
            !string.IsNullOrWhiteSpace(invocationContext.ProcessStepId))
        {
            return Invalid(
                context,
                "The run context claims a process step (source kind 'process-step' or a process run or step identifier); only process automation starts such runs.",
                agentId,
                chatSessionId: chatSessionId);
        }

        return HasTopLevelMember(
                invocationContext.MetadataJson,
                ExecutionInvocationMetadata.ExternalTargetRootBindingsMetadataKey)
            ? Invalid(
                context,
                $"The run context metadata must not contain '{ExecutionInvocationMetadata.ExternalTargetRootBindingsMetadataKey}'; external folders are granted only in the agent's workspace tool settings.",
                agentId,
                chatSessionId: chatSessionId)
            : null;
    }

    private static bool HasTopLevelMember(string? json, string name)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.EnumerateObject().Any(member =>
                       string.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            // Metadata that is not a JSON object is replaced by an empty object when the run is created.
            return false;
        }
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
