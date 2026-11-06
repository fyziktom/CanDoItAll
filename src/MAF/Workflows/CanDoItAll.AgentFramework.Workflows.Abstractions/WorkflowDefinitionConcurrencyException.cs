namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public sealed class WorkflowDefinitionConcurrencyException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
