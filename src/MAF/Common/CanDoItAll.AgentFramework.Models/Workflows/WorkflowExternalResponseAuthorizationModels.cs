namespace CanDoItAll.AgentFramework.Models;

public enum WorkflowExternalResponseAction
{
    SubmitInput,
    Approve,
    Deny
}

public sealed record WorkflowExternalResponseAuthorization(
    WorkflowExternalResponseOperationId OperationId,
    WorkflowExternalRequestId RequestId,
    WorkflowExternalRequestVersion RequestVersion,
    WorkflowRunId RunId,
    WorkflowId WorkflowId,
    WorkflowVersionId WorkflowVersionId,
    WorkflowExternalRequestKind RequestKind,
    WorkflowExternalResponseAction Action,
    WorkflowLaunchActor Actor,
    WorkspaceScopeDescriptor AuthorizationScope,
    WorkflowLaunchActor? OriginActor,
    string AuthorizationPolicyFingerprint,
    DateTimeOffset AuthorizedAtUtc,
    DateTimeOffset ExpiresAtUtc)
{
    public bool IsExpired(DateTimeOffset nowUtc) => nowUtc >= ExpiresAtUtc;
}
