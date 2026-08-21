using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowExternalRequestAuthorizerTests
{
    private static readonly DatabaseProfileGeneration CurrentGeneration = new(7);

    [Fact]
    public async Task CurrentProfileUserWithExactScopeAndCapabilityIsAuthorized()
    {
        var scenario = CreateScenario(WorkflowExternalRequestKind.HumanInput);

        var decision = await scenario.Authorizer.AuthorizeAsync(scenario.Request);

        Assert.Equal(WorkflowExternalRequestAuthorizationOutcome.Authorized, decision.Outcome);
        Assert.NotNull(decision.Authorization);
        Assert.Equal(scenario.Scope, decision.Authorization.AuthorizationScope);
        Assert.Equal(
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            decision.Authorization.AuthorizationPolicyFingerprint);
        Assert.Equal(
            scenario.Request.EvaluatedAtUtc.AddSeconds(
                WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds),
            decision.Authorization.ExpiresAtUtc);
    }

    [Fact]
    public async Task OrganizationScopedUserCanAnswerNarrowerProjectRequestInCurrentProfile()
    {
        var projectScope = WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D"));
        var scenario = CreateScenario(
            WorkflowExternalRequestKind.HumanInput,
            requestScope: projectScope,
            grantedScopes: null);

        var decision = await scenario.Authorizer.AuthorizeAsync(scenario.Request);

        Assert.Equal(WorkflowExternalRequestAuthorizationOutcome.Authorized, decision.Outcome);
    }

    [Fact]
    public async Task AgentHumanInputRequiresExactGovernanceScope()
    {
        var projectScope = WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D"));
        var scenario = CreateScenario(
            WorkflowExternalRequestKind.HumanInput,
            actorKind: WorkflowLaunchActorKind.Agent,
            channel: WorkflowExternalResponseTrustedChannel.AgentTool,
            requestScope: projectScope,
            grantedScopes: [WorkspaceScopeDescriptor.Organization(Guid.NewGuid().ToString("N"))],
            capabilities: WorkflowExternalResponseCallerCapabilities.SubmitHumanInput);

        var decision = await scenario.Authorizer.AuthorizeAsync(scenario.Request);

        Assert.Equal(WorkflowExternalRequestAuthorizationOutcome.ScopeMismatch, decision.Outcome);
        Assert.Null(decision.Authorization);
    }

    [Theory]
    [InlineData(WorkflowLaunchActorKind.Agent)]
    [InlineData(WorkflowLaunchActorKind.Service)]
    public async Task AutonomousActorsCannotApprove(WorkflowLaunchActorKind actorKind)
    {
        var scenario = CreateScenario(
            WorkflowExternalRequestKind.Approval,
            actorKind,
            actorKind == WorkflowLaunchActorKind.Agent
                ? WorkflowExternalResponseTrustedChannel.AgentTool
                : WorkflowExternalResponseTrustedChannel.Api,
            capabilities: WorkflowExternalResponseCallerCapabilities.SubmitApprovalDecision);

        var decision = await scenario.Authorizer.AuthorizeAsync(scenario.Request);

        Assert.Equal(WorkflowExternalRequestAuthorizationOutcome.SelfApprovalDenied, decision.Outcome);
        Assert.Null(decision.Authorization);
    }

    [Fact]
    public async Task HumanOriginatorIsNotAutomaticallyTreatedAsSelfApproval()
    {
        var actor = new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "human-42");
        var scenario = CreateScenario(
            WorkflowExternalRequestKind.Approval,
            actor: actor,
            originActor: actor,
            intendedApproverSubjectId: actor.SubjectId,
            capabilities: WorkflowExternalResponseCallerCapabilities.SubmitApprovalDecision);

        var decision = await scenario.Authorizer.AuthorizeAsync(scenario.Request);

        Assert.Equal(WorkflowExternalRequestAuthorizationOutcome.Authorized, decision.Outcome);
    }

    [Fact]
    public async Task MissingPersistedScopePolicyOrLifetimeFailsClosed()
    {
        var scenario = CreateScenario(WorkflowExternalRequestKind.HumanInput);
        var incompletePolicy = scenario.Request.Boundary.AuthorizationPolicy! with
        {
            AuthorizationScope = null,
            AuthorizationPolicyFingerprint = string.Empty,
            ResponseAuthorizationLifetimeSeconds = 0
        };
        var request = scenario.Request with
        {
            Boundary = scenario.Request.Boundary with
            {
                AuthorizationPolicy = incompletePolicy
            }
        };

        var decision = await scenario.Authorizer.AuthorizeAsync(request);

        Assert.Equal(
            WorkflowExternalRequestAuthorizationOutcome.AuthorizationContextUnavailable,
            decision.Outcome);
        Assert.Null(decision.Authorization);
    }

    [Fact]
    public async Task StaleProfileGenerationFailsClosed()
    {
        var scenario = CreateScenario(
            WorkflowExternalRequestKind.HumanInput,
            callerGeneration: new DatabaseProfileGeneration(CurrentGeneration.Value - 1));

        var decision = await scenario.Authorizer.AuthorizeAsync(scenario.Request);

        Assert.Equal(WorkflowExternalRequestAuthorizationOutcome.ProfileMismatch, decision.Outcome);
    }

    [Fact]
    public async Task MissingCapabilityOrWrongIntendedApproverIsDenied()
    {
        var missingCapability = CreateScenario(
            WorkflowExternalRequestKind.HumanInput,
            capabilities: WorkflowExternalResponseCallerCapabilities.None);
        var wrongAssignment = CreateScenario(
            WorkflowExternalRequestKind.HumanInput,
            intendedApproverSubjectId: "another-user");

        var missingCapabilityDecision = await missingCapability.Authorizer.AuthorizeAsync(
            missingCapability.Request);
        var wrongAssignmentDecision = await wrongAssignment.Authorizer.AuthorizeAsync(
            wrongAssignment.Request);

        Assert.Equal(
            WorkflowExternalRequestAuthorizationOutcome.CapabilityMissing,
            missingCapabilityDecision.Outcome);
        Assert.Equal(
            WorkflowExternalRequestAuthorizationOutcome.AssignmentMismatch,
            wrongAssignmentDecision.Outcome);
    }

    [Fact]
    public void LaunchScopeResolverKeepsTrustedAgentScopeAndDerivesServerOwnedProjectScope()
    {
        var profileId = Guid.NewGuid();
        var resolver = new WorkflowLaunchAuthorizationScopeResolver(
            new FixedDatabaseProfileRuntimeAccessor(profileId));
        var agentScope = WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D"));
        var agentOrigin = new WorkflowLaunchOrigin.AgentRuntimeInvocation(
            new WorkflowLaunchActor(WorkflowLaunchActorKind.Agent, Guid.NewGuid().ToString("D")),
            new WorkflowLaunchSessionId("launch-scope-test"),
            "test",
            new WorkflowLaunchCorrelationId("launch-scope-agent"))
        {
            AuthorizationScope = agentScope,
            AuthorizationPolicyFingerprint = WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint
        };
        var projectId = Guid.NewGuid();
        var projectOrigin = new WorkflowLaunchOrigin.ProjectStructureNode(
            projectId,
            new WorkflowProjectStructureNodeId("node-1"),
            new WorkflowLaunchActor(WorkflowLaunchActorKind.Agent, Guid.NewGuid().ToString("D")),
            new WorkflowLaunchSessionId("launch-scope-project"),
            new WorkflowLaunchCorrelationId("launch-scope-project"));

        var agentResolution = resolver.Resolve(agentOrigin);
        var projectResolution = resolver.Resolve(projectOrigin);

        Assert.Equal(agentScope, agentResolution.Scope);
        Assert.Equal(
            WorkspaceScopeDescriptor.Project(projectId.ToString("D")),
            projectResolution.Scope);
        Assert.Equal(
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            projectResolution.PolicyFingerprint);
    }

    [Fact]
    public void LaunchScopeResolverRejectsMissingAgentGovernanceScopeAndCrossProfileOrganization()
    {
        var profileId = Guid.NewGuid();
        var resolver = new WorkflowLaunchAuthorizationScopeResolver(
            new FixedDatabaseProfileRuntimeAccessor(profileId));
        var agentOrigin = new WorkflowLaunchOrigin.AgentRuntimeInvocation(
            new WorkflowLaunchActor(WorkflowLaunchActorKind.Agent, Guid.NewGuid().ToString("D")),
            new WorkflowLaunchSessionId("launch-scope-test"),
            "test",
            new WorkflowLaunchCorrelationId("launch-scope-agent"));
        var apiOrigin = new WorkflowLaunchOrigin.Api(
            new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "user-1"),
            new WorkflowLaunchCorrelationId("launch-scope-api"))
        {
            AuthorizationScope = WorkspaceScopeDescriptor.Organization(Guid.NewGuid().ToString("N"))
        };

        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(agentOrigin));
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(apiOrigin));
    }

    private static AuthorizationScenario CreateScenario(
        WorkflowExternalRequestKind requestKind,
        WorkflowLaunchActorKind actorKind = WorkflowLaunchActorKind.User,
        WorkflowExternalResponseTrustedChannel channel = WorkflowExternalResponseTrustedChannel.LocalOperator,
        WorkflowLaunchActor? actor = null,
        WorkflowLaunchActor? originActor = null,
        WorkspaceScopeDescriptor? requestScope = null,
        IReadOnlyList<WorkspaceScopeDescriptor>? grantedScopes = null,
        WorkflowExternalResponseCallerCapabilities? capabilities = null,
        string intendedApproverSubjectId = "",
        DatabaseProfileGeneration? callerGeneration = null)
    {
        var profileId = Guid.NewGuid();
        var profileScope = WorkspaceScopeDescriptor.Organization(profileId.ToString("N"));
        var scope = requestScope ?? profileScope;
        var resolvedActor = actor ?? new WorkflowLaunchActor(actorKind, "actor-42");
        var resolvedOriginActor = originActor ?? resolvedActor;
        var access = new WorkflowExternalResponseCallerAccess(
            profileId,
            callerGeneration ?? CurrentGeneration,
            profileScope,
            grantedScopes ?? [profileScope],
            capabilities ?? (requestKind == WorkflowExternalRequestKind.HumanInput
                ? WorkflowExternalResponseCallerCapabilities.SubmitHumanInput
                : WorkflowExternalResponseCallerCapabilities.SubmitApprovalDecision),
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            DateTimeOffset.Parse("2026-08-21T12:00:00Z"));
        var actorContext = new WorkflowExternalResponseActorContext.Authenticated(
            resolvedActor,
            channel,
            access);
        var runId = WorkflowRunId.New();
        var requestId = WorkflowExternalRequestId.New();
        var createdAtUtc = DateTimeOffset.Parse("2026-08-21T12:01:00Z");
        var origin = CreateOrigin(resolvedOriginActor) with
        {
            AuthorizationScope = scope,
            AuthorizationPolicyFingerprint = WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint
        };
        var run = new WorkflowRunSnapshot(
            runId,
            WorkflowId.New(),
            WorkflowVersionId.New(),
            WorkflowRunState.WaitingForInput,
            WorkflowRuntimeBackendKind.InProcess,
            "authorizer-test",
            "Waiting for input.",
            createdAtUtc,
            createdAtUtc)
        {
            Origin = origin
        };
        var responseContract = new WorkflowExternalResponseContract(
            requestKind,
            "test.response",
            1,
            "{\"type\":\"object\"}",
            4096);
        var externalRequest = new WorkflowExternalRequestRecord(
            requestId,
            runId,
            requestKind,
            new WorkflowNodeId("wait"),
            "wait",
            "{}",
            string.Empty,
            createdAtUtc,
            RespondedAtUtc: null)
        {
            Version = WorkflowExternalRequestVersion.Initial,
            State = WorkflowExternalRequestState.Pending,
            ResponseContract = responseContract
        };
        var authorizationPolicy = new WorkflowExternalRequestAuthorizationPolicySnapshot(
            resolvedOriginActor,
            ExecutorId: null,
            WorkflowExecutorCapabilityFlags.None,
            WorkflowExecutorApprovalRequirement.NotRequired,
            intendedApproverSubjectId)
        {
            AuthorizationScope = scope,
            AuthorizationPolicyFingerprint = WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            ResponseAuthorizationLifetimeSeconds = WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds
        };
        var boundary = new WorkflowExternalRequestBoundaryRecord(
            requestId,
            WorkflowExternalRequestVersion.Initial,
            WorkflowExternalRequestState.Pending,
            responseContract,
            new WorkflowExternalRequestContinuation(
                new WorkflowBackendExternalRequestLink(
                    requestId,
                    new WorkflowBackendRequestId("authorizer-request"),
                    new WorkflowBackendRequestPortId("authorizer-port")),
                new WorkflowBackendCheckpointLink(
                    new WorkflowBackendSessionId("authorizer-session"),
                    new WorkflowBackendCheckpointId("authorizer-checkpoint")),
                new WorkflowCompilerContractVersion(1),
                WorkflowTopologyFingerprint.Create("authorizer-topology"),
                WorkflowBackendCheckpointPayloadHash.Compute("{}")),
            new WorkflowExternalRequestPayloadHash(new string('0', 64)),
            createdAtUtc)
        {
            AuthorizationPolicy = authorizationPolicy
        };
        var authorizationRequest = new WorkflowExternalRequestAuthorizationRequest(
            actorContext,
            run,
            externalRequest,
            boundary,
            createdAtUtc.AddMinutes(1));
        var authorizer = new WorkflowExternalRequestAuthorizer(
            new FixedDatabaseProfileRuntimeAccessor(profileId),
            new FixedAgentExecutionProfileGenerationSource(CurrentGeneration));
        return new AuthorizationScenario(authorizer, authorizationRequest, scope);
    }

    private static WorkflowLaunchOrigin CreateOrigin(WorkflowLaunchActor actor)
        => actor.Kind == WorkflowLaunchActorKind.Agent
            ? new WorkflowLaunchOrigin.AgentRuntimeInvocation(
                actor,
                new WorkflowLaunchSessionId("authorizer-test-session"),
                "test",
                new WorkflowLaunchCorrelationId("authorizer-test"))
            : new WorkflowLaunchOrigin.Api(
                actor,
                new WorkflowLaunchCorrelationId("authorizer-test"));

    private sealed record AuthorizationScenario(
        WorkflowExternalRequestAuthorizer Authorizer,
        WorkflowExternalRequestAuthorizationRequest Request,
        WorkspaceScopeDescriptor Scope);

    private sealed class FixedDatabaseProfileRuntimeAccessor(Guid profileId) :
        IDatabaseProfileRuntimeAccessor
    {
        private readonly ResolvedDatabaseProfile profile = new(
            new DatabaseProfileRecord
            {
                Id = profileId,
                DisplayName = "Workflow authorization test",
                ProviderKind = DatabaseProviderKind.InMemory,
                SourceKind = DatabaseProfileSourceKind.InMemory
            },
            DatabaseProfileResolutionSource.ExplicitOverride,
            "test");

        public ResolvedDatabaseProfile ResolveCurrentProfile() => profile;

        public ResolvedDatabaseProfile ResolveProfile(Guid requestedProfileId) => profile;
    }
}
