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
        Exception innerException)
        : base($"The prompt was saved to the thread, but the run failed: {innerException.Message}", innerException)
    {
        AgentId = agentId;
        ExecutionRunId = executionRunId;
        ChatSessionId = chatSessionId;
        ProviderName = providerName;
        ModelName = modelName;
    }

    public Guid AgentId { get; }

    public Guid ExecutionRunId { get; }

    public Guid ChatSessionId { get; }

    public string ProviderName { get; }

    public string ModelName { get; }
}
