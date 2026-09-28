namespace CanDoItAll.AgentFramework.Core;

public enum AgentApprovalCheckpointFailure { Missing, Corrupt, Incompatible, Unavailable }

public sealed class AgentApprovalCheckpointUnavailableException(
    AgentApprovalCheckpointFailure failure, Exception? innerException = null) : Exception(
        "The saved approval checkpoint " + (failure switch {
            AgentApprovalCheckpointFailure.Missing => "is missing.",
            AgentApprovalCheckpointFailure.Corrupt => "is corrupt.",
            AgentApprovalCheckpointFailure.Incompatible => "does not match the pending run.",
            _ => "could not be read."
        }) + " No approval was applied. Restore the original checkpoint, or cancel the pending run and review fresh proposals in a new thread. Preserve the existing history and effects for reconciliation.", innerException) {
    public AgentApprovalCheckpointFailure Failure { get; } = failure;
}
