using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentCapabilityOperations(IAgentCapabilityCommands commands, IAgentCapabilitiesReads reads) {
    private readonly Lock gate = new();
    private readonly Dictionary<Guid, OperationEntry> operations = [];
    public event Action? Changed;

    public AgentCapabilityOperationState? Find(Guid? agentId) {
        lock (gate) {
            return agentId is { } id ? operations.GetValueOrDefault(id)?.State : null;
        }
    }

    public Task<AgentCapabilityOperationState?> AssignAsync(AgentEditorModel draft, Guid capabilityId, CancellationToken cancellationToken = default) {
        OperationEntry entry;
        lock (gate) {
            if (draft.Id is not { } id || operations.ContainsKey(id)) {
                return Task.FromResult<AgentCapabilityOperationState?>(null);
            }
            var attempt = new AgentCapabilityAssignmentAttempt(draft, capabilityId);
            entry = new OperationEntry(State(attempt, AgentCapabilityOperationStatus.Pending, true), attempt);
            operations.Add(id, entry);
        }
        return DispatchAsync(entry, cancellationToken);
    }

    public Task<AgentCapabilityOperationState?> RetryAsync(Guid agentId, Guid attemptId, CancellationToken cancellationToken = default) {
        OperationEntry retry;
        lock (gate) {
            if (!operations.TryGetValue(agentId, out var entry) || entry.State.AttemptId != attemptId || !entry.State.CanRetry || entry.Assignment is null) {
                return Task.FromResult<AgentCapabilityOperationState?>(null);
            }
            retry = entry with { State = State(entry.Assignment!, AgentCapabilityOperationStatus.Pending, true) };
            operations[agentId] = retry;
        }
        return DispatchAsync(retry, cancellationToken);
    }

    private async Task<AgentCapabilityOperationState?> DispatchAsync(OperationEntry entry, CancellationToken token) {
        var status = await commands.AssignAsync(entry.Assignment!, token);
        return Finish(entry, status);
    }

    public async Task<AgentCapabilityOperationState?> VerifyAsync(Guid agentId, Guid attemptId, CancellationToken cancellationToken = default) {
        OperationEntry entry;
        lock (gate) {
            if (!operations.TryGetValue(agentId, out var current) || current.State.AttemptId != attemptId || !current.State.CanVerify) {
                return null;
            }
            entry = current with { State = current.State with { IsActive = true } };
            operations[agentId] = entry;
        }
        AgentCapabilityOperationStatus status;
        try {
            if (entry.Assignment is { } assignment) {
                var canonical = await reads.ReadEditorAsync(agentId, cancellationToken);
                status = assignment.Classify(canonical);
            } else if (entry.Receipt is { } receipt) {
                var canonical = await reads.LoadCatalogAsync(cancellationToken);
                status = receipt.Classify(canonical.Agents, canonical.Capabilities) switch {
                    CapabilityProofRecovery.Satisfied => AgentCapabilityOperationStatus.DesiredStateSatisfied,
                    CapabilityProofRecovery.NotPublished => AgentCapabilityOperationStatus.DefinitelyNotCommitted,
                    CapabilityProofRecovery.Superseded => AgentCapabilityOperationStatus.Superseded,
                    _ => AgentCapabilityOperationStatus.Unconfirmed
                };
            } else {
                status = AgentCapabilityOperationStatus.Unconfirmed;
            }
            if (cancellationToken.IsCancellationRequested) {
                status = AgentCapabilityOperationStatus.Unconfirmed;
            }
        } catch (Exception) {
            status = AgentCapabilityOperationStatus.Unconfirmed;
        }
        return Finish(entry, status);
    }

    public bool CompleteReconciliation(Guid agentId, Guid attemptId, bool adoptCurrent = false) {
        lock (gate) {
            if (!operations.TryGetValue(agentId, out var entry) || entry.State.AttemptId != attemptId ||
                !(entry.State.CanReconcile || adoptCurrent && entry.State.CanAdopt)) {
                return false;
            }
            operations.Remove(agentId);
        }
        Changed?.Invoke();
        return true;
    }

    private AgentCapabilityOperationState? Finish(OperationEntry owner, AgentCapabilityOperationStatus status, CapabilityProofReceipt? receipt = null) {
        AgentCapabilityOperationState state;
        lock (gate) {
            if (!operations.TryGetValue(owner.State.AgentId, out var current) || !ReferenceEquals(current, owner)) {
                return null;
            }
            state = owner.Assignment is { } assignment ? State(assignment, status, false)
                : ProofState(owner.State.AttemptId, owner.State.AgentId, owner.State.CapabilityId, status, false);
            if (status is AgentCapabilityOperationStatus.Rejected or AgentCapabilityOperationStatus.CanceledBeforeDispatch) {
                operations.Remove(state.AgentId);
            } else {
                operations[state.AgentId] = owner with { State = state, Receipt = receipt ?? owner.Receipt };
            }
        }
        Changed?.Invoke();
        return state;
    }

    private static AgentCapabilityOperationState State(AgentCapabilityAssignmentAttempt attempt, AgentCapabilityOperationStatus status, bool active)
        => new(attempt.AttemptId, attempt.AgentId, attempt.CapabilityId, AgentCapabilityOperationKind.Assignment, status, active, status switch {
            AgentCapabilityOperationStatus.Pending => "Assignment is pending. The displayed attachments are the last authoritative state.",
            AgentCapabilityOperationStatus.Rejected => "The assignment was rejected. Correct the agent settings before trying a new intent.",
            AgentCapabilityOperationStatus.Conflict => "The agent changed elsewhere. Verify its current state before continuing.",
            AgentCapabilityOperationStatus.Committed => "Assignment saved. Reconcile the current catalog without another write.",
            AgentCapabilityOperationStatus.CommittedWithWarning => "Assignment saved; directory projection needs attention. Reconcile without another write.",
            AgentCapabilityOperationStatus.DesiredStateSatisfied => "The requested attachment set is now authoritative. No write was replayed.",
            AgentCapabilityOperationStatus.DefinitelyNotCommitted => "The exact previous state and revision remain. Retry this same request deliberately, or adopt the current state.",
            AgentCapabilityOperationStatus.Superseded => "An intervening change is authoritative. Adopt it before making a new assignment intent.",
            AgentCapabilityOperationStatus.CanceledBeforeDispatch => "Assignment canceled before dispatch. No write was started.",
            _ => "Assignment outcome is unconfirmed. Verify canonical state before another write."
        });

    public Task<AgentCapabilityOperationState?> DiagnoseAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default) {
        OperationEntry entry;
        lock (gate) {
            if (operations.ContainsKey(agentId)) {
                return Task.FromResult<AgentCapabilityOperationState?>(null);
            }
            entry = new(ProofState(Guid.NewGuid(), agentId, capabilityId, AgentCapabilityOperationStatus.Pending, true));
            operations.Add(agentId, entry);
        }
        return DispatchDiagnosticAsync(entry, cancellationToken);
    }

    private async Task<AgentCapabilityOperationState?> DispatchDiagnosticAsync(OperationEntry owner, CancellationToken token) {
        var outcome = await commands.DiagnoseAsync(owner.State.AgentId, owner.State.CapabilityId, token);
        var status = outcome.Disposition switch {
            CapabilityVerificationDisposition.Rejected => AgentCapabilityOperationStatus.Rejected,
            CapabilityVerificationDisposition.CanceledBeforeDiagnostic => AgentCapabilityOperationStatus.CanceledBeforeDispatch,
            CapabilityVerificationDisposition.Committed => AgentCapabilityOperationStatus.Committed,
            CapabilityVerificationDisposition.Superseded or CapabilityVerificationDisposition.DiagnosticInterrupted => AgentCapabilityOperationStatus.Superseded,
            CapabilityVerificationDisposition.PublicationCanceled or CapabilityVerificationDisposition.PublicationNotStarted => AgentCapabilityOperationStatus.DefinitelyNotCommitted,
            _ => AgentCapabilityOperationStatus.Unconfirmed
        };
        return Finish(owner, status, outcome.Receipt);
    }

    private static AgentCapabilityOperationState ProofState(Guid attemptId, Guid agentId, Guid capabilityId, AgentCapabilityOperationStatus status, bool active)
        => new(attemptId, agentId, capabilityId, AgentCapabilityOperationKind.Verification, status, active, status switch {
            AgentCapabilityOperationStatus.Pending => "Diagnostic is pending. Its inputs will be checked again before proof is published.",
            AgentCapabilityOperationStatus.Committed => "Proof was published. Reconcile without running the diagnostic again.",
            AgentCapabilityOperationStatus.DesiredStateSatisfied => "The captured proof is authoritative. No diagnostic was repeated.",
            AgentCapabilityOperationStatus.DefinitelyNotCommitted => "Proof was not published. Adopt the current state before deliberately starting another diagnostic.",
            AgentCapabilityOperationStatus.Superseded => "The diagnostic was interrupted or its inputs changed. No stale proof was published; adopt the current state.",
            AgentCapabilityOperationStatus.Rejected => "The diagnostic could not start. Check the agent, attachment, and provider availability.",
            AgentCapabilityOperationStatus.CanceledBeforeDispatch => "Verification canceled before the diagnostic started.",
            _ => "Proof publication is unconfirmed. Verify canonical state without repeating the diagnostic."
        });

    private sealed record OperationEntry(AgentCapabilityOperationState State, AgentCapabilityAssignmentAttempt? Assignment = null, CapabilityProofReceipt? Receipt = null);
}
