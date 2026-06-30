using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowExecutorInvocationException : InvalidOperationException
{
    public WorkflowExecutorInvocationException(
        WorkflowNodeId nodeId,
        WorkflowExecutorId executorId,
        int attemptCount,
        int timeoutSeconds,
        string message,
        Exception? innerException)
        : base(message, innerException)
    {
        NodeId = nodeId;
        ExecutorId = executorId;
        AttemptCount = attemptCount;
        TimeoutSeconds = timeoutSeconds;
    }

    public WorkflowNodeId NodeId { get; }

    public WorkflowExecutorId ExecutorId { get; }

    public int AttemptCount { get; }

    public int TimeoutSeconds { get; }
}

public sealed class WorkflowExecutorUnavailableException : InvalidOperationException
{
    public WorkflowExecutorUnavailableException(
        WorkflowNodeId nodeId,
        WorkflowExecutorId executorId,
        WorkflowExecutorAvailabilityDescriptor availability,
        string message)
        : base(message)
    {
        NodeId = nodeId;
        ExecutorId = executorId;
        Availability = availability;
    }

    public WorkflowNodeId NodeId { get; }

    public WorkflowExecutorId ExecutorId { get; }

    public WorkflowExecutorAvailabilityDescriptor Availability { get; }
}

public sealed class WorkflowExecutorPayloadTooLargeException : InvalidOperationException
{
    public WorkflowExecutorPayloadTooLargeException(
        WorkflowNodeId nodeId,
        WorkflowExecutorId executorId,
        int payloadCharacters,
        int maxPayloadCharacters,
        string message)
        : base(message)
    {
        NodeId = nodeId;
        ExecutorId = executorId;
        PayloadCharacters = payloadCharacters;
        MaxPayloadCharacters = maxPayloadCharacters;
    }

    public WorkflowNodeId NodeId { get; }

    public WorkflowExecutorId ExecutorId { get; }

    public int PayloadCharacters { get; }

    public int MaxPayloadCharacters { get; }
}

public sealed class WorkflowExecutorSanitizedException : Exception
{
    public WorkflowExecutorSanitizedException(
        string originalExceptionType,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        OriginalExceptionType = originalExceptionType;
    }

    public string OriginalExceptionType { get; }
}
