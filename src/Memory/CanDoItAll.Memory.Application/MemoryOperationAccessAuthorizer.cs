using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public sealed record MemoryOperationAccessDecision(
    bool IsAllowed,
    string Diagnostic)
{
    public static readonly MemoryOperationAccessDecision Allowed = new(
        IsAllowed: true,
        "Memory operation ownership matched the caller.");

    public static readonly MemoryOperationAccessDecision Denied = new(
        IsAllowed: false,
        "Memory operation ownership did not match the caller.");
}

public interface IMemoryOperationAccessAuthorizer
{
    MemoryOperationAccessDecision Authorize(
        MemoryLedgerRequester owner,
        MemoryLedgerRequester caller);
}

public sealed class ExactMemoryOperationAccessAuthorizer : IMemoryOperationAccessAuthorizer
{
    public MemoryOperationAccessDecision Authorize(
        MemoryLedgerRequester owner,
        MemoryLedgerRequester caller)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(caller);

        return HasRequiredRequester(owner.RequesterId, caller.RequesterId) &&
            Matches(owner.AgentId, caller.AgentId) &&
            Matches(owner.AgentRole, caller.AgentRole) &&
            Matches(owner.SessionId, caller.SessionId) &&
            Matches(owner.WorkflowId, caller.WorkflowId) &&
            Matches(owner.WorkflowNodeId, caller.WorkflowNodeId) &&
            Matches(owner.ProcessId, caller.ProcessId) &&
            Matches(owner.ProcessStepId, caller.ProcessStepId)
            ? MemoryOperationAccessDecision.Allowed
            : MemoryOperationAccessDecision.Denied;
    }

    private static bool HasRequiredRequester(
        string ownerRequesterId,
        string callerRequesterId)
    {
        return !string.IsNullOrWhiteSpace(ownerRequesterId) &&
            !string.IsNullOrWhiteSpace(callerRequesterId) &&
            Matches(ownerRequesterId, callerRequesterId);
    }

    private static bool Matches(
        string? ownerValue,
        string? callerValue)
    {
        return string.Equals(ownerValue, callerValue, StringComparison.Ordinal);
    }
}
