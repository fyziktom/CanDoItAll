using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class DenyAllWorkflowExternalRequestAuthorizer : IWorkflowExternalRequestAuthorizer
{
    public Task<WorkflowExternalRequestAuthorizationDecision> AuthorizeAsync(
        WorkflowExternalRequestAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var outcome = request.ActorContext is WorkflowExternalResponseActorContext.Unauthenticated
            ? WorkflowExternalRequestAuthorizationOutcome.Unauthenticated
            : WorkflowExternalRequestAuthorizationOutcome.AuthorizationContextUnavailable;
        return Task.FromResult(new WorkflowExternalRequestAuthorizationDecision(
            outcome,
            Authorization: null,
            outcome == WorkflowExternalRequestAuthorizationOutcome.Unauthenticated
                ? "Authentication is required."
                : "No workflow external-request authorization policy is configured."));
    }
}
