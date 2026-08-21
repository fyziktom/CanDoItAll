using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

internal sealed record WorkflowExternalResponseTestComposition(
    WorkflowRuntimeManager Manager,
    AuthorizedWorkflowExternalResponseTestClient Responses);

internal sealed class AuthorizedWorkflowExternalResponseTestClient(
    IWorkflowExternalResponseService service,
    WorkflowExternalResponseActorContext.Authenticated actorContext)
{
    public async Task<WorkflowExternalResponseServiceResult> SubmitAsync(
        WorkflowExternalRequestRecord request,
        string responseJson,
        string idempotencyKey = "unit-test-response")
    {
        using var response = JsonDocument.Parse(responseJson);
        return await service.SubmitAsync(
            new WorkflowExternalResponseCommand(
                actorContext,
                request.Id,
                request.Version,
                response.RootElement,
                new WorkflowExternalResponseIdempotencyKey(idempotencyKey),
                new WorkflowLaunchCorrelationId($"test:{request.Id}:{idempotencyKey}")));
    }
}

internal static class WorkflowExternalResponseTestCompositionFactory
{
    public static WorkflowExternalResponseTestComposition Create(
        IEnumerable<IWorkflowExecutionBackend> backends,
        InMemoryWorkflowRunStore runStore,
        InMemoryWorkflowBackendCheckpointPayloadStore checkpointStore,
        TimeProvider? timeProvider = null,
        IWorkflowEventSink? eventSink = null,
        InMemoryWorkflowUsageObservationStore? usageStore = null)
        => CreateCore(
            backends,
            runStore,
            new InMemoryWorkflowExternalRequestBoundaryStore(runStore, checkpointStore),
            timeProvider ?? TimeProvider.System,
            eventSink,
            usageStore);

    public static WorkflowExternalResponseTestComposition CreateCompatibility(
        IEnumerable<IWorkflowExecutionBackend> backends,
        IWorkflowRunStore runStore,
        InMemoryWorkflowBackendCheckpointPayloadStore checkpointStore,
        TimeProvider timeProvider,
        IWorkflowEventSink? eventSink = null,
        IWorkflowUsageObservationStore? usageStore = null)
        => CreateCore(
            backends,
            runStore,
            new InMemoryWorkflowExternalRequestBoundaryStore(
                runStore,
                checkpointStore,
                allowCompatibilityComposition: true),
            timeProvider,
            eventSink,
            usageStore);

    public static WorkflowExternalResponseTestComposition CreateLegacyCompatibility(
        IEnumerable<IWorkflowExecutionBackend> backends,
        IWorkflowRunStore runStore,
        TimeProvider timeProvider,
        IWorkflowEventSink? eventSink = null,
        IWorkflowUsageObservationStore? usageStore = null)
        => CreateCore(
            backends,
            runStore,
            new InMemoryWorkflowExternalRequestBoundaryStore(runStore),
            timeProvider,
            eventSink,
            usageStore);

    private static WorkflowExternalResponseTestComposition CreateCore(
        IEnumerable<IWorkflowExecutionBackend> backends,
        IWorkflowRunStore runStore,
        InMemoryWorkflowExternalRequestBoundaryStore boundaryStore,
        TimeProvider timeProvider,
        IWorkflowEventSink? eventSink,
        IWorkflowUsageObservationStore? usageStore)
    {
        var backendList = backends.ToArray();
        var operationStore = new InMemoryWorkflowExternalResponseOperationStore(
            runStore,
            boundaryStore);
        var resumeStore = runStore is InMemoryWorkflowRunStore inMemoryRunStore &&
            usageStore is null or InMemoryWorkflowUsageObservationStore
                ? new InMemoryWorkflowResumeBoundaryStore(
                    inMemoryRunStore,
                    boundaryStore,
                    operationStore,
                    usageStore as InMemoryWorkflowUsageObservationStore)
                : new InMemoryWorkflowResumeBoundaryStore(
                    runStore,
                    boundaryStore,
                    operationStore,
                    usageStore);
        var activeRuns = new WorkflowActiveRunRegistry();
        var validator = new WorkflowExternalResponseValidator();
        var continuation = new WorkflowExternalResponseContinuation(
            backendList,
            operationStore,
            resumeStore,
            activeRuns,
            validator,
            timeProvider);
        var manager = new WorkflowRuntimeManager(
            backendList,
            runStore,
            activeRuns,
            timeProvider,
            boundaryStore,
            resumeStore,
            eventSink,
            usageStore);
        var authorizer = new TestWorkflowExternalRequestAuthorizer(timeProvider);
        var service = new WorkflowExternalResponseService(
            runStore,
            boundaryStore,
            operationStore,
            continuation,
            authorizer,
            validator,
            timeProvider,
            NullLogger<WorkflowExternalResponseService>.Instance);
        return new WorkflowExternalResponseTestComposition(
            manager,
            new AuthorizedWorkflowExternalResponseTestClient(service, authorizer.ActorContext));
    }

    private sealed class TestWorkflowExternalRequestAuthorizer(TimeProvider timeProvider) :
        IWorkflowExternalRequestAuthorizer
    {
        private static readonly WorkflowLaunchActor Actor = new(
            WorkflowLaunchActorKind.User,
            "unit-test-operator");
        private static readonly WorkspaceScopeDescriptor DefaultScope =
            WorkspaceScopeDescriptor.Organization("unit-tests");

        public WorkflowExternalResponseActorContext.Authenticated ActorContext { get; } = new(
            Actor,
            WorkflowExternalResponseTrustedChannel.LocalOperator,
            new WorkflowExternalResponseCallerAccess(
                Guid.Parse("c905c4e8-c28d-4513-8f7a-ac8be5d05fb4"),
                new DatabaseProfileGeneration(1),
                DefaultScope,
                [DefaultScope],
                WorkflowExternalResponseCallerCapabilities.SubmitHumanInput |
                WorkflowExternalResponseCallerCapabilities.SubmitApprovalDecision,
                WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                timeProvider.GetUtcNow().AddMinutes(-1),
                timeProvider.GetUtcNow().AddHours(1)));

        public Task<WorkflowExternalRequestAuthorizationDecision> AuthorizeAsync(
            WorkflowExternalRequestAuthorizationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var policy = request.Boundary.AuthorizationPolicy;
            if (policy?.AuthorizationScope is null ||
                !string.Equals(
                    policy.AuthorizationPolicyFingerprint,
                    WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                    StringComparison.Ordinal))
            {
                return Task.FromResult(new WorkflowExternalRequestAuthorizationDecision(
                    WorkflowExternalRequestAuthorizationOutcome.AuthorizationContextUnavailable,
                    Authorization: null,
                    "The test request has no authorization policy."));
            }

            var now = timeProvider.GetUtcNow();
            return Task.FromResult(new WorkflowExternalRequestAuthorizationDecision(
                WorkflowExternalRequestAuthorizationOutcome.Authorized,
                new WorkflowAuthorizedExternalResponseActor(
                    Actor,
                    policy.AuthorizationScope,
                    policy.AuthorizationPolicyFingerprint,
                    now,
                    now.AddMinutes(15)),
                "Authorized by the test boundary."));
        }
    }
}
