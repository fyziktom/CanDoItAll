using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.UI.Catalog;

public enum AgentDecisionPolicyPendingStage { Prepared, Dispatching, Accepted, Rejected }

public sealed record AgentDecisionPolicyPendingRecord {
    public AgentDecisionPolicyPendingRecord(ReplaceAgentDecisionDataPolicyCommand command, AgentDecisionPolicyPendingStage stage,
        AgentDecisionDataPolicySaveOutcome? outcome = null) {
        ArgumentNullException.ThrowIfNull(command);
        var valid = stage switch {
            AgentDecisionPolicyPendingStage.Prepared or AgentDecisionPolicyPendingStage.Dispatching => outcome is null,
            AgentDecisionPolicyPendingStage.Accepted => outcome is AgentDecisionDataPolicySaveOutcome.Accepted accepted && accepted.Command == command,
            AgentDecisionPolicyPendingStage.Rejected => outcome is AgentDecisionDataPolicySaveOutcome.Rejected rejected && Enum.IsDefined(rejected.Reason),
            _ => false
        };
        if (!valid) {
            throw new ArgumentException("The retained policy state must match its original command and outcome.");
        }
        Command = command;
        Stage = stage;
        Outcome = outcome;
    }

    public ReplaceAgentDecisionDataPolicyCommand Command { get; }
    public AgentDecisionPolicyPendingStage Stage { get; }
    public AgentDecisionDataPolicySaveOutcome? Outcome { get; }
}

public sealed record AgentDecisionPolicyPendingChange(bool Changed, AgentDecisionPolicyPendingRecord? Record);

public sealed record AgentDecisionPolicyPendingAddress(AgentExecutionActivityWorkspaceIdentity Workspace, Guid AgentId) {
    public static AgentDecisionPolicyPendingAddress From(AgentDecisionDataPolicyExpectation expectation)
        => new(expectation.Workspace, expectation.AgentId);
}

public sealed record AgentDecisionPolicyPendingSummary(Guid AgentId, Guid OperationId, AgentDecisionPolicyPendingStage Stage);

public interface IAgentDecisionPolicyPendingStore {
    public const int MaximumRecords = 128;
    ValueTask InitializeAsync(CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<AgentDecisionPolicyPendingSummary>> ListAsync(AgentExecutionActivityWorkspaceIdentity context, CancellationToken cancellationToken = default);
    ValueTask<AgentDecisionPolicyPendingRecord?> ReadAsync(AgentDecisionPolicyPendingAddress context, CancellationToken cancellationToken = default);
    ValueTask<AgentDecisionPolicyPendingChange> PrepareAsync(ReplaceAgentDecisionDataPolicyCommand command, CancellationToken cancellationToken = default);
    ValueTask<AgentDecisionPolicyPendingChange> BeginDispatchAsync(ReplaceAgentDecisionDataPolicyCommand command, CancellationToken cancellationToken = default);
    ValueTask<AgentDecisionPolicyPendingChange> RecordOutcomeAsync(ReplaceAgentDecisionDataPolicyCommand command,
        AgentDecisionDataPolicySaveOutcome outcome, CancellationToken cancellationToken = default);
    ValueTask<AgentDecisionPolicyPendingChange> ClearResolvedAsync(ReplaceAgentDecisionDataPolicyCommand command, CancellationToken cancellationToken = default);
    ValueTask<AgentDecisionPolicyPendingChange> CancelPreparedAsync(ReplaceAgentDecisionDataPolicyCommand command, CancellationToken cancellationToken = default);
}
