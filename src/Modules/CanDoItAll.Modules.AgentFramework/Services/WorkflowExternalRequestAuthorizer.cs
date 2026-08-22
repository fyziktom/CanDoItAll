using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class WorkflowExternalRequestAuthorizer(
    IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor,
    IAgentExecutionProfileGenerationSource profileGenerationSource) :
    IWorkflowExternalRequestAuthorizer
{
    public Task<WorkflowExternalRequestAuthorizationDecision> AuthorizeAsync(
        WorkflowExternalRequestAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ActorContext is not WorkflowExternalResponseActorContext.Authenticated authenticated)
        {
            return Decision(
                WorkflowExternalRequestAuthorizationOutcome.Unauthenticated,
                "Authentication is required.");
        }

        var access = authenticated.Access;
        if (access.AuthenticationExpiresAtUtc is { } authenticationExpiry &&
            authenticationExpiry <= request.EvaluatedAtUtc)
        {
            return Decision(
                WorkflowExternalRequestAuthorizationOutcome.AuthenticationExpired,
                "The workflow response authentication has expired.");
        }

        var currentProfile = databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile;
        var currentGeneration = profileGenerationSource.GetGeneration();
        var currentProfileScope = WorkspaceScopeDescriptor.Organization(currentProfile.Id.ToString("N"));
        if (access.DatabaseProfileId != currentProfile.Id ||
            access.DatabaseProfileGeneration != currentGeneration ||
            access.CurrentProfileScope != currentProfileScope)
        {
            return Decision(
                WorkflowExternalRequestAuthorizationOutcome.ProfileMismatch,
                "The workflow response caller is not authorized for the current database profile.");
        }

        var origin = request.Run.Origin;
        var policy = request.Boundary.AuthorizationPolicy;
        if (origin?.AuthorizationScope is not { } originScope ||
            string.IsNullOrWhiteSpace(origin.AuthorizationPolicyFingerprint) ||
            policy?.AuthorizationScope is not { } requiredScope ||
            string.IsNullOrWhiteSpace(policy.AuthorizationPolicyFingerprint) ||
            policy.ResponseAuthorizationLifetimeSeconds <= 0)
        {
            return Decision(
                WorkflowExternalRequestAuthorizationOutcome.AuthorizationContextUnavailable,
                "The workflow request does not contain a complete authorization context.");
        }

        if (originScope != requiredScope ||
            !string.Equals(
                origin.AuthorizationPolicyFingerprint,
                policy.AuthorizationPolicyFingerprint,
                StringComparison.Ordinal) ||
            !OriginActorMatches(origin, policy.OriginActor))
        {
            return Decision(
                WorkflowExternalRequestAuthorizationOutcome.AuthorizationContextUnavailable,
                "The workflow request authorization context is inconsistent.");
        }

        if (!string.Equals(
                access.PolicyFingerprint,
                policy.AuthorizationPolicyFingerprint,
                StringComparison.Ordinal))
        {
            return Decision(
                WorkflowExternalRequestAuthorizationOutcome.ScopeMismatch,
                "The workflow response caller policy does not match the request policy.");
        }

        if (!IsScopeAuthorized(authenticated, access, requiredScope, currentProfileScope))
        {
            return Decision(
                WorkflowExternalRequestAuthorizationOutcome.ScopeMismatch,
                "The workflow response caller is not authorized for the request scope.");
        }

        var requiredCapability = request.Request.Kind == WorkflowExternalRequestKind.HumanInput
            ? WorkflowExternalResponseCallerCapabilities.SubmitHumanInput
            : WorkflowExternalResponseCallerCapabilities.SubmitApprovalDecision;
        if (!access.Capabilities.HasFlag(requiredCapability))
        {
            return Decision(
                WorkflowExternalRequestAuthorizationOutcome.CapabilityMissing,
                "The workflow response caller lacks the required capability.");
        }

        if (!string.IsNullOrWhiteSpace(policy.IntendedApproverSubjectId) &&
            !string.Equals(
                authenticated.Actor.SubjectId,
                policy.IntendedApproverSubjectId,
                StringComparison.Ordinal))
        {
            return Decision(
                WorkflowExternalRequestAuthorizationOutcome.AssignmentMismatch,
                "The workflow response caller is not the intended responder.");
        }

        if (request.Request.Kind is WorkflowExternalRequestKind.Approval or
            WorkflowExternalRequestKind.ToolApproval &&
            authenticated.Actor.Kind != WorkflowLaunchActorKind.User)
        {
            return Decision(
                WorkflowExternalRequestAuthorizationOutcome.SelfApprovalDenied,
                "Autonomous actors cannot approve workflow requests.");
        }

        if (request.Request.Kind == WorkflowExternalRequestKind.HumanInput &&
            authenticated.Actor.Kind == WorkflowLaunchActorKind.Agent &&
            authenticated.Channel != WorkflowExternalResponseTrustedChannel.AgentTool)
        {
            return Decision(
                WorkflowExternalRequestAuthorizationOutcome.CapabilityMissing,
                "Agent workflow input requires a governed agent-tool context.");
        }

        DateTimeOffset expiresAtUtc;
        try
        {
            expiresAtUtc = request.EvaluatedAtUtc.AddSeconds(
                policy.ResponseAuthorizationLifetimeSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Decision(
                WorkflowExternalRequestAuthorizationOutcome.AuthorizationContextUnavailable,
                "The workflow request authorization lifetime is invalid.");
        }

        return Task.FromResult(new WorkflowExternalRequestAuthorizationDecision(
            WorkflowExternalRequestAuthorizationOutcome.Authorized,
            new WorkflowAuthorizedExternalResponseActor(
                authenticated.Actor,
                requiredScope,
                policy.AuthorizationPolicyFingerprint,
                request.EvaluatedAtUtc,
                expiresAtUtc),
            "The workflow response caller is authorized."));
    }

    private static bool IsScopeAuthorized(
        WorkflowExternalResponseActorContext.Authenticated authenticated,
        WorkflowExternalResponseCallerAccess access,
        WorkspaceScopeDescriptor requiredScope,
        WorkspaceScopeDescriptor currentProfileScope)
    {
        if (authenticated.Actor.Kind == WorkflowLaunchActorKind.Agent)
        {
            return access.GrantedScopes.Length == 1 &&
                   access.GrantedScopes[0] == requiredScope;
        }

        return access.GrantedScopes.Any(scope =>
            scope == requiredScope ||
            scope == currentProfileScope &&
            requiredScope.Kind is WorkspaceScopeKind.Project or WorkspaceScopeKind.Process);
    }

    private static bool OriginActorMatches(
        WorkflowLaunchOrigin origin,
        WorkflowLaunchActor? persistedActor)
    {
        var actualActor = origin switch
        {
            WorkflowLaunchOrigin.Api api => api.Actor,
            WorkflowLaunchOrigin.Preview preview => preview.Actor,
            WorkflowLaunchOrigin.ProjectStructureNode project => project.RequestingActor,
            WorkflowLaunchOrigin.AgentRuntimeInvocation agent => agent.Agent,
            _ => null
        };

        return actualActor is null
            ? persistedActor is null
            : persistedActor is not null &&
              actualActor.Kind == persistedActor.Kind &&
              string.Equals(actualActor.SubjectId, persistedActor.SubjectId, StringComparison.Ordinal);
    }

    private static Task<WorkflowExternalRequestAuthorizationDecision> Decision(
        WorkflowExternalRequestAuthorizationOutcome outcome,
        string safeMessage)
        => Task.FromResult(new WorkflowExternalRequestAuthorizationDecision(
            outcome,
            Authorization: null,
            safeMessage));
}
