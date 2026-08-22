using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum WorkflowExternalResponseRecoveryPoint
{
    AcceptedBeforeClaim,
    ClaimedBeforeResponseDelivery,
    ResponseDeliveredBeforeCommit
}

public delegate ValueTask WorkflowExternalResponseRecoveryHook(
    WorkflowExternalResponseRecoveryPoint point,
    WorkflowExternalResponseOperationId operationId,
    CancellationToken cancellationToken);
