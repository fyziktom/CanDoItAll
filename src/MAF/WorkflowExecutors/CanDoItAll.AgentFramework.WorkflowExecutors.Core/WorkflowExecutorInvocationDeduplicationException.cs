using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowExecutorInvocationDeduplicationException : InvalidOperationException
{
    public WorkflowExecutorInvocationDeduplicationException(
        WorkflowExecutorInvocationKey invocationKey,
        string message,
        WorkflowExecutorInvocationClaimOutcome? claimOutcome = null,
        WorkflowExecutorInvocationMutationOutcome? mutationOutcome = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        InvocationKey = invocationKey;
        ClaimOutcome = claimOutcome;
        MutationOutcome = mutationOutcome;
    }

    public WorkflowExecutorInvocationKey InvocationKey { get; }

    public WorkflowExecutorInvocationClaimOutcome? ClaimOutcome { get; }

    public WorkflowExecutorInvocationMutationOutcome? MutationOutcome { get; }
}
