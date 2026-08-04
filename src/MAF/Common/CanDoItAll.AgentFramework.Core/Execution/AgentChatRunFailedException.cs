using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentChatRunFailedException : InvalidOperationException
{
    public AgentChatRunFailedException(
        Guid agentId,
        Guid executionRunId,
        Guid chatSessionId,
        string providerName,
        string modelName,
        Exception innerException,
        string displayMessage,
        AgentProviderFailureCategory? failureCategory = null)
        : base($"The prompt was saved to the thread, but the run failed: {AgentProviderFailureDisplayFormatter.NormalizeDisplayMessage(displayMessage)}", innerException)
    {
        AgentId = agentId;
        ExecutionRunId = executionRunId;
        ChatSessionId = chatSessionId;
        ProviderName = providerName;
        ModelName = modelName;
        SanitizedDisplayMessage = AgentProviderFailureDisplayFormatter.NormalizeDisplayMessage(displayMessage);
        FailureCategory = failureCategory;
    }

    public Guid AgentId { get; }

    public Guid ExecutionRunId { get; }

    public Guid ChatSessionId { get; }

    public string ProviderName { get; }

    public string ModelName { get; }

    public string SanitizedDisplayMessage { get; }

    public AgentProviderFailureCategory? FailureCategory { get; }
}
