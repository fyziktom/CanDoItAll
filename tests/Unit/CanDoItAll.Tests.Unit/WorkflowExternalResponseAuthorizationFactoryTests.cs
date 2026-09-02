using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowExternalResponseAuthorizationFactoryTests
{
    private static readonly DateTimeOffset AcceptedAtUtc =
        DateTimeOffset.Parse("2026-08-21T12:00:00Z");

    [Fact]
    public void Create_ValidDurableEvidence_ReconstructsBoundedAuthorization()
    {
        var context = CreateContext();

        var result = Create(context, WorkflowExternalResponseAction.Approve, AcceptedAtUtc.AddMinutes(1));

        Assert.True(result.Succeeded, result.SafeMessage);
        var authorization = Assert.IsType<WorkflowExternalResponseAuthorization>(result.Authorization);
        Assert.Equal(context.Operation.Id, authorization.OperationId);
        Assert.Equal(context.Run.WorkflowId, authorization.WorkflowId);
        Assert.Equal(context.Run.VersionId, authorization.WorkflowVersionId);
        Assert.Equal(context.Scope, authorization.AuthorizationScope);
        Assert.Equal(AcceptedAtUtc, authorization.AuthorizedAtUtc);
        Assert.Equal(
            AcceptedAtUtc.AddSeconds(WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds),
            authorization.ExpiresAtUtc);
    }

    [Fact]
    public void Create_MissingOriginScope_FailsClosed()
    {
        var context = CreateContext();
        context = context with
        {
            Run = context.Run with
            {
                Origin = context.Run.Origin! with { AuthorizationScope = null }
            }
        };

        var result = Create(context, WorkflowExternalResponseAction.Approve, AcceptedAtUtc.AddMinutes(1));

        Assert.Equal(
            WorkflowExternalResponseAuthorizationOutcome.AuthorizationContextUnavailable,
            result.Outcome);
        Assert.Null(result.Authorization);
    }

    [Fact]
    public void Create_OriginScopeMismatch_FailsClosed()
    {
        var context = CreateContext();
        context = context with
        {
            Run = context.Run with
            {
                Origin = context.Run.Origin! with
                {
                    AuthorizationScope = WorkspaceScopeDescriptor.Project("other-project")
                }
            }
        };

        var result = Create(context, WorkflowExternalResponseAction.Approve, AcceptedAtUtc.AddMinutes(1));

        Assert.Equal(WorkflowExternalResponseAuthorizationOutcome.OriginScopeMismatch, result.Outcome);
    }

    [Fact]
    public void Create_PolicyMismatch_FailsClosed()
    {
        var context = CreateContext();
        var policy = context.Request.AuthorizationPolicy! with
        {
            AuthorizationPolicyFingerprint = "workflow-external-response.legacy"
        };
        context = WithPolicy(context, policy);

        var result = Create(context, WorkflowExternalResponseAction.Approve, AcceptedAtUtc.AddMinutes(1));

        Assert.Equal(
            WorkflowExternalResponseAuthorizationOutcome.AuthorizationPolicyMismatch,
            result.Outcome);
    }

    [Fact]
    public void Create_ActorScopeFingerprintMismatch_FailsClosed()
    {
        var context = CreateContext();
        context = context with
        {
            Operation = context.Operation with
            {
                ActorScopeFingerprint = new WorkflowExternalResponseActorScopeFingerprint(
                    new string('0', 64))
            }
        };

        var result = Create(context, WorkflowExternalResponseAction.Approve, AcceptedAtUtc.AddMinutes(1));

        Assert.Equal(WorkflowExternalResponseAuthorizationOutcome.FingerprintMismatch, result.Outcome);
    }

    [Fact]
    public void Create_ActionNotDerivedFromProtectedPayload_FailsClosed()
    {
        var context = CreateContext();

        var result = Create(context, WorkflowExternalResponseAction.Deny, AcceptedAtUtc.AddMinutes(1));

        Assert.Equal(WorkflowExternalResponseAuthorizationOutcome.FingerprintMismatch, result.Outcome);
    }

    [Fact]
    public void Create_InvalidPersistedLifetime_FailsClosed()
    {
        var context = CreateContext();
        var policy = context.Request.AuthorizationPolicy! with
        {
            ResponseAuthorizationLifetimeSeconds = 0
        };
        context = WithPolicy(context, policy);

        var result = Create(context, WorkflowExternalResponseAction.Approve, AcceptedAtUtc.AddMinutes(1));

        Assert.Equal(WorkflowExternalResponseAuthorizationOutcome.InvalidLifetime, result.Outcome);
    }

    [Fact]
    public void Create_ExpiredAuthorization_FailsClosedAtExactBoundary()
    {
        var context = CreateContext();

        var result = Create(
            context,
            WorkflowExternalResponseAction.Approve,
            AcceptedAtUtc.AddSeconds(WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds));

        Assert.Equal(WorkflowExternalResponseAuthorizationOutcome.Expired, result.Outcome);
    }

    [Fact]
    public void Create_OperationRequestLinkMismatch_FailsClosed()
    {
        var context = CreateContext();
        context = context with
        {
            Operation = context.Operation with { RequestId = WorkflowExternalRequestId.New() }
        };

        var result = Create(context, WorkflowExternalResponseAction.Approve, AcceptedAtUtc.AddMinutes(1));

        Assert.Equal(WorkflowExternalResponseAuthorizationOutcome.LinkageMismatch, result.Outcome);
    }

    private static WorkflowExternalResponseAuthorizationResult Create(
        AuthorizationContext context,
        WorkflowExternalResponseAction action,
        DateTimeOffset asOfUtc)
        => WorkflowExternalResponseAuthorizationFactory.Create(
            context.Operation,
            context.Run,
            context.Request,
            context.Boundary,
            action,
            asOfUtc);

    private static AuthorizationContext WithPolicy(
        AuthorizationContext context,
        WorkflowExternalRequestAuthorizationPolicySnapshot policy)
        => context with
        {
            Request = context.Request with { AuthorizationPolicy = policy },
            Boundary = context.Boundary with { AuthorizationPolicy = policy }
        };

    private static AuthorizationContext CreateContext()
    {
        var scope = WorkspaceScopeDescriptor.Project("project-1");
        var originActor = new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "launch-user");
        var origin = new WorkflowLaunchOrigin.Api(
            originActor,
            new WorkflowLaunchCorrelationId("launch-correlation"))
        {
            AuthorizationScope = scope,
            AuthorizationPolicyFingerprint = WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint
        };
        var runId = WorkflowRunId.New();
        var requestId = WorkflowExternalRequestId.New();
        var continuation = new WorkflowExternalRequestContinuation(
            new WorkflowBackendExternalRequestLink(
                requestId,
                new WorkflowBackendRequestId("backend-request"),
                new WorkflowBackendRequestPortId("approval")),
            new WorkflowBackendCheckpointLink(
                new WorkflowBackendSessionId("session"),
                new WorkflowBackendCheckpointId("checkpoint")),
            new WorkflowCompilerContractVersion(1),
            WorkflowTopologyFingerprint.Create("authorization-test-topology"),
            WorkflowBackendCheckpointPayloadHash.Compute("{}"));
        var contract = new WorkflowExternalResponseContract(
            WorkflowExternalRequestKind.Approval,
            "CanDoItAll.WorkflowApprovalResponse/v1",
            schemaVersion: 1,
            "{}",
            maximumPayloadBytes: 1_024);
        var policy = new WorkflowExternalRequestAuthorizationPolicySnapshot(
            originActor,
            ExecutorId: null,
            WorkflowExecutorCapabilityFlags.None,
            WorkflowExecutorApprovalRequirement.NotRequired,
            IntendedApproverSubjectId: string.Empty)
        {
            AuthorizationScope = scope,
            AuthorizationPolicyFingerprint = WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            ResponseAuthorizationLifetimeSeconds =
                WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds
        };
        var request = new WorkflowExternalRequestRecord(
            requestId,
            runId,
            WorkflowExternalRequestKind.Approval,
            new WorkflowNodeId("approval"),
            "ApprovalRequested",
            "{}",
            string.Empty,
            AcceptedAtUtc.AddMinutes(-1),
            RespondedAtUtc: null)
        {
            Version = WorkflowExternalRequestVersion.Initial,
            State = WorkflowExternalRequestState.Pending,
            ResponseContract = contract,
            Continuation = continuation,
            AuthorizationPolicy = policy
        };
        Assert.True(WorkflowExternalRequestBoundaryRecord.TryCreate(request, out var boundary));
        var run = new WorkflowRunSnapshot(
            runId,
            WorkflowId.New(),
            WorkflowVersionId.New(),
            WorkflowRunState.WaitingForInput,
            WorkflowRuntimeBackendKind.InProcess,
            "backend-run",
            "Waiting",
            AcceptedAtUtc.AddMinutes(-2),
            AcceptedAtUtc.AddMinutes(-1))
        {
            Origin = origin
        };
        var actor = new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "approver");
        var fingerprint = WorkflowExternalResponseFingerprintFactory.Create(
            requestId,
            request.Version,
            actor,
            scope,
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            new WorkflowExternalResponseIdempotencyKey("authorization-test-response"),
            "{\"approved\":true}");
        var operation = new WorkflowExternalResponseOperationRecord(
            WorkflowExternalResponseOperationId.New(),
            requestId,
            runId,
            request.Version,
            fingerprint.IdempotencyKeyHash,
            fingerprint.PayloadHash,
            fingerprint.ActorScopeFingerprint,
            fingerprint.CanonicalPayload,
            actor,
            new WorkflowLaunchCorrelationId("response-correlation"),
            WorkflowExternalResponseOperationState.Accepted,
            Attempt: 0,
            WorkflowExternalResponseOperationConcurrencyVersion.Initial,
            AcceptedAtUtc);
        return new AuthorizationContext(operation, run, request, boundary!, scope);
    }

    private sealed record AuthorizationContext(
        WorkflowExternalResponseOperationRecord Operation,
        WorkflowRunSnapshot Run,
        WorkflowExternalRequestRecord Request,
        WorkflowExternalRequestBoundaryRecord Boundary,
        WorkspaceScopeDescriptor Scope);
}
