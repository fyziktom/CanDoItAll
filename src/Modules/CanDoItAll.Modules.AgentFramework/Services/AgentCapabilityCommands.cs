using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

public enum AgentCapabilityOperationKind { Assignment, Verification }

public enum AgentCapabilityOperationStatus {
    Pending, Rejected, Conflict, Reconciled, Committed, CommittedWithWarning, Unconfirmed,
    DesiredStateSatisfied, DefinitelyNotCommitted, Superseded, CanceledBeforeDispatch
}

public sealed record AgentCapabilityOperationState(Guid AttemptId, Guid AgentId, Guid CapabilityId,
    AgentCapabilityOperationKind Kind, AgentCapabilityOperationStatus Status, bool IsActive, string Message) {
    public bool CanReconcile => !IsActive && Status is AgentCapabilityOperationStatus.Committed
        or AgentCapabilityOperationStatus.CommittedWithWarning or AgentCapabilityOperationStatus.DesiredStateSatisfied;
    public bool CanVerify => !IsActive && Status is AgentCapabilityOperationStatus.Unconfirmed or AgentCapabilityOperationStatus.Conflict;
    public bool CanRetry => !IsActive && Kind == AgentCapabilityOperationKind.Assignment && Status == AgentCapabilityOperationStatus.DefinitelyNotCommitted;
    public bool CanAdopt => !IsActive && Status is AgentCapabilityOperationStatus.Superseded or AgentCapabilityOperationStatus.DefinitelyNotCommitted;
}

public interface IAgentCapabilityCommands {
    Task<CapabilityVerificationOutcome> DiagnoseAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default);
    Task<AgentCapabilityOperationStatus> AssignAsync(AgentCapabilityAssignmentAttempt attempt, CancellationToken cancellationToken = default);
}

public sealed class AgentCapabilityCommands(IAgentFrameworkWorkspaceService workspace,
    ILogger<AgentCapabilityCommands> logger) : IAgentCapabilityCommands {
    public async Task<CapabilityVerificationOutcome> DiagnoseAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default) {
        if (cancellationToken.IsCancellationRequested) {
            return new(CapabilityVerificationDisposition.CanceledBeforeDiagnostic);
        }
        try {
            await workspace.VerifyCapabilityAsync(agentId, capabilityId, cancellationToken);
            return new(CapabilityVerificationDisposition.Committed);
        } catch (CapabilityVerificationException exception) {
            return exception.Outcome;
        } catch (Exception exception) {
            logger.LogWarning("Capability diagnostic for agent {AgentId} requires verification after {FailureType}.", agentId, exception.GetType().Name);
            return new(CapabilityVerificationDisposition.Unconfirmed);
        }
    }

    public async Task<AgentCapabilityOperationStatus> AssignAsync(AgentCapabilityAssignmentAttempt attempt, CancellationToken cancellationToken = default) {
        if (cancellationToken.IsCancellationRequested) {
            return AgentCapabilityOperationStatus.CanceledBeforeDispatch;
        }
        try {
            var id = await workspace.SaveAgentAsync(attempt.CreateRequest(), cancellationToken);
            return id == attempt.AgentId ? AgentCapabilityOperationStatus.Committed : AgentCapabilityOperationStatus.Unconfirmed;
        } catch (AgentDirectoryProjectionSynchronizationException exception) when (exception.AgentId == attempt.AgentId) {
            return AgentCapabilityOperationStatus.CommittedWithWarning;
        } catch (AgentCatalogConcurrencyException) {
            return AgentCapabilityOperationStatus.Conflict;
        } catch (AgentEditorValidationException) {
            return AgentCapabilityOperationStatus.Rejected;
        } catch (Exception exception) {
            logger.LogWarning("Capability assignment {AttemptId} for agent {AgentId} requires canonical verification after {FailureType}.",
                attempt.AttemptId, attempt.AgentId, exception.GetType().Name);
            return AgentCapabilityOperationStatus.Unconfirmed;
        }
    }
}
