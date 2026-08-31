using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class PersistentWorkflowResumeBoundaryStoreInMemoryTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 21, 23, 0, 0, TimeSpan.Zero);
    private static readonly WorkflowLaunchActor Actor = new(
        WorkflowLaunchActorKind.User,
        "persistent-inmemory-user");
    private static readonly WorkspaceScopeDescriptor AuthorizationScope =
        WorkspaceScopeDescriptor.Organization("persistent-inmemory-tests");

    [Fact]
    public async Task SubmitInput_AfterPersistentStart_CommitsNextApprovalWait()
    {
        var fixture = CreateFixture();
        var definition = CreateDefinition();
        var startRequest = new WorkflowRunStartRequest(
            definition.Id,
            definition.VersionId,
            "{}",
            WorkflowRuntimeBackendKind.InProcess,
            SourceProcessRunId: null,
            SourceProcessAssignmentId: null)
        {
            Origin = new WorkflowLaunchOrigin.Api(
                Actor,
                new WorkflowLaunchCorrelationId("persistent-inmemory-start"))
            {
                AuthorizationScope = AuthorizationScope,
                AuthorizationPolicyFingerprint =
                    WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint
            }
        };

        var started = await fixture.Manager.StartAsync(definition, startRequest);
        var initialRequest = Assert.Single(
            await fixture.RunStore.ListPendingExternalRequestsAsync(started.RunId));
        using var response = JsonDocument.Parse("{\"answer\":\"Ada\"}");

        var result = await fixture.ResponseService.SubmitAsync(
            new WorkflowExternalResponseCommand(
                fixture.ActorContext,
                initialRequest.Id,
                initialRequest.Version,
                response.RootElement,
                new WorkflowExternalResponseIdempotencyKey("persistent-inmemory-input"),
                new WorkflowLaunchCorrelationId("persistent-inmemory-input")));

        Assert.Equal(WorkflowExternalResponseServiceOutcome.WaitingAgain, result.Outcome);
        Assert.Equal(WorkflowExternalResponseOperationState.WaitingAgain, result.Operation?.State);
        Assert.Equal(WorkflowRunState.WaitingForInput, result.Run?.State);
        Assert.Equal(1, fixture.Backend.ResumeCount);
        var nextRequest = Assert.IsType<WorkflowExternalRequestRecord>(result.NextRequest);
        Assert.Equal(WorkflowExternalRequestKind.ToolApproval, nextRequest.Kind);
        Assert.Equal(WorkflowExternalRequestState.Pending, nextRequest.EffectiveState);
        var nextContinuation = Assert.IsType<WorkflowExternalRequestContinuation>(
            nextRequest.Continuation);

        var persistedPending = Assert.Single(
            await fixture.RunStore.ListPendingExternalRequestsAsync(started.RunId));
        Assert.Equal(nextRequest.Id, persistedPending.Id);
        Assert.Equal(nextRequest.Continuation, persistedPending.Continuation);
        Assert.Equal(nextRequest.AuthorizationPolicy, persistedPending.AuthorizationPolicy);
        var nextBoundary = await fixture.BoundaryStore.ReadAsync(nextRequest.Id);
        Assert.Equal(WorkflowExternalRequestBoundaryReadOutcome.Found, nextBoundary.Outcome);
        Assert.Equal(WorkflowExternalRequestState.Pending, nextBoundary.Boundary?.State);

        await using var dbContext = await fixture.Factory.CreateDbContextAsync();
        var sourceBoundary = await dbContext.Set<WorkflowExternalRequestBoundaryEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.RequestId == initialRequest.Id.Value);
        var operation = await dbContext.Set<WorkflowExternalResponseOperationEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == result.Operation!.Id.Value);
        var linkedCheckpoint = await dbContext.Set<WorkflowBackendCheckpointPayloadEntity>()
            .AsNoTracking()
            .SingleAsync(item =>
                item.Id == nextContinuation.Checkpoint.CheckpointId.Value);
        Assert.Equal((int)WorkflowExternalRequestState.Responded, sourceBoundary.State);
        Assert.Equal((int)WorkflowExternalResponseOperationState.WaitingAgain, operation.State);
        Assert.Equal(nextRequest.Id.Value, linkedCheckpoint.ExternalRequestId);
        Assert.Equal(
            nextContinuation.Request.BackendRequestId.Value,
            linkedCheckpoint.BackendRequestId);
        Assert.Equal(
            nextContinuation.Request.BackendRequestPortId.Value,
            linkedCheckpoint.BackendRequestPortId);
    }

    private static TestFixture CreateFixture()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([
            typeof(AgentFrameworkModuleAssemblyMarker).Assembly
        ]);
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"persistent-resume-{Guid.NewGuid():N}")
            .Options;
        var factory = new TestDbContextFactory(options);
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var timeProvider = new FixedTimeProvider(Now);
        var runStore = new PersistentWorkflowRunStore(factory);
        var boundaryStore = new PersistentWorkflowExternalRequestBoundaryStore(factory);
        var operationStore = new PersistentWorkflowExternalResponseOperationStore(
            factory,
            dataProtectionProvider);
        var resumeStore = new PersistentWorkflowResumeBoundaryStore(
            factory,
            dataProtectionProvider,
            new WorkflowHistoryProjection(new CanDoItAll.AgentFramework.ProviderHistory.Persistence.HistoryOutboxWriter(timeProvider)));
        var checkpointStore = new PersistentWorkflowBackendCheckpointPayloadStore(
            factory,
            dataProtectionProvider,
            timeProvider);
        var backend = new ConsecutiveWaitBackend(checkpointStore, timeProvider);
        var activeRuns = new WorkflowActiveRunRegistry();
        var validator = new WorkflowExternalResponseValidator();
        var manager = new WorkflowRuntimeManager(
            [backend],
            runStore,
            activeRuns,
            timeProvider,
            boundaryStore,
            resumeStore);
        var continuation = new WorkflowExternalResponseContinuation(
            [backend],
            operationStore,
            resumeStore,
            activeRuns,
            validator,
            new NullWorkflowEventSink(),
            NullLogger<WorkflowExternalResponseContinuation>.Instance,
            timeProvider);
        var authorizer = new TestAuthorizer(timeProvider);
        var responseService = new WorkflowExternalResponseService(
            runStore,
            boundaryStore,
            operationStore,
            continuation,
            authorizer,
            validator,
            timeProvider,
            NullLogger<WorkflowExternalResponseService>.Instance);
        return new TestFixture(
            factory,
            runStore,
            boundaryStore,
            manager,
            responseService,
            backend,
            authorizer.ActorContext);
    }

    private static WorkflowDefinition CreateDefinition()
    {
        var start = CreateNode("start", WorkflowNodeKind.Start);
        var end = CreateNode("end", WorkflowNodeKind.End);
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Persistent consecutive wait",
            "Exercises persistent human-input to approval continuation.",
            WorkflowLifecycleStatus.Active,
            new WorkflowGraph(
                start.Id,
                [start, end],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-end"),
                        start.Id,
                        SourcePortId: null,
                        end.Id,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty)
                ]),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            Now,
            Now);
    }

    private static WorkflowNode CreateNode(string id, WorkflowNodeKind kind)
        => new(
            new WorkflowNodeId(id),
            kind,
            id,
            Ports: [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));

    private sealed record TestFixture(
        TestDbContextFactory Factory,
        PersistentWorkflowRunStore RunStore,
        PersistentWorkflowExternalRequestBoundaryStore BoundaryStore,
        WorkflowRuntimeManager Manager,
        WorkflowExternalResponseService ResponseService,
        ConsecutiveWaitBackend Backend,
        WorkflowExternalResponseActorContext.Authenticated ActorContext);

    private sealed class ConsecutiveWaitBackend(
        PersistentWorkflowBackendCheckpointPayloadStore checkpointStore,
        TimeProvider timeProvider) :
        IWorkflowExecutionBackend,
        IWorkflowExternalResponseBackend
    {
        private readonly WorkflowBackendSessionId sessionId = new(
            $"persistent-inmemory-{Guid.NewGuid():N}");
        private WorkflowBackendCheckpointLink? initialCheckpoint;

        public WorkflowRuntimeBackendDescriptor Descriptor { get; } = new(
            WorkflowRuntimeBackendKind.InProcess,
            "Persistent consecutive-wait backend",
            IsDurable: false,
            SupportsStreaming: false,
            SupportsExternalRequests: true,
            SupportsDashboardObservability: false,
            OperationalNotes: "Persists native checkpoints for the EF InMemory regression.")
        {
            SupportsExternalResponseResume = true,
            SupportsActiveCancellation = true
        };

        public int ResumeCount { get; private set; }

        public async Task<WorkflowBackendStartResult> StartAsync(
            WorkflowDefinition definition,
            WorkflowRunStartRequest request,
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
        {
            var run = CreateWaitingRun(definition, request, runId, "Waiting for human input.");
            var checkpoint = await CreateCheckpointAsync(
                run,
                Parent: null,
                "{\"step\":\"human-input\"}",
                cancellationToken);
            initialCheckpoint = checkpoint.Index.Link;
            var externalRequest = CreateExternalRequest(
                run,
                checkpoint,
                WorkflowExternalRequestKind.HumanInput,
                "human-input");
            return CreateWaitingResult(run, externalRequest, checkpoint);
        }

        public Task<WorkflowBackendStartResult> ResumeAsync(
            WorkflowRunSnapshot run,
            WorkflowExternalRequestRecord request,
            string responseJson,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The typed response path is required.");

        public async Task<WorkflowBackendStartResult> ResumeAsync(
            WorkflowBackendResumeRequest request,
            CancellationToken cancellationToken = default)
        {
            ResumeCount++;
            Assert.Equal(WorkflowExternalRequestKind.HumanInput, request.ExternalRequest.Kind);
            Assert.Equal("Ada", request.Response.GetProperty("answer").GetString());
            var checkpoint = await CreateCheckpointAsync(
                request.Run,
                initialCheckpoint,
                "{\"step\":\"http-fetch-approval\"}",
                cancellationToken);
            var externalRequest = CreateExternalRequest(
                request.Run,
                checkpoint,
                WorkflowExternalRequestKind.ToolApproval,
                "http-fetch-approval");
            var waitingRun = request.Run with
            {
                State = WorkflowRunState.WaitingForInput,
                Summary = "Waiting for HTTP fetch approval.",
                UpdatedAtUtc = timeProvider.GetUtcNow(),
                TerminalAtUtc = null
            };
            return CreateWaitingResult(waitingRun, externalRequest, checkpoint);
        }

        private WorkflowRunSnapshot CreateWaitingRun(
            WorkflowDefinition definition,
            WorkflowRunStartRequest request,
            WorkflowRunId runId,
            string summary)
            => new(
                runId,
                definition.Id,
                definition.VersionId,
                WorkflowRunState.WaitingForInput,
                WorkflowRuntimeBackendKind.InProcess,
                sessionId.Value,
                summary,
                timeProvider.GetUtcNow(),
                timeProvider.GetUtcNow())
            {
                Origin = request.Origin
            };

        private async Task<WorkflowBackendCheckpointPayloadRecord> CreateCheckpointAsync(
            WorkflowRunSnapshot run,
            WorkflowBackendCheckpointLink? Parent,
            string payloadJson,
            CancellationToken cancellationToken)
        {
            var created = await checkpointStore.CreateAsync(
                new WorkflowBackendCheckpointCreateRequest(
                    CreateSession(run),
                    Parent,
                    WorkflowBackendCheckpointPayload.Create(payloadJson)),
                cancellationToken);
            Assert.Equal(WorkflowBackendCheckpointCreateOutcome.Created, created.Outcome);
            return Assert.IsType<WorkflowBackendCheckpointPayloadRecord>(created.Checkpoint);
        }

        private WorkflowBackendCheckpointSession CreateSession(WorkflowRunSnapshot run)
            => new(
                sessionId,
                run.RunId,
                run.WorkflowId,
                run.VersionId,
                run.Backend,
                new WorkflowBackendCheckpointFormat("persistent-test-json"),
                new WorkflowBackendCheckpointFormatVersion(1),
                new WorkflowCompilerContractVersion(1),
                WorkflowTopologyFingerprint.Create("persistent-consecutive-wait"));

        private static WorkflowExternalRequestRecord CreateExternalRequest(
            WorkflowRunSnapshot run,
            WorkflowBackendCheckpointPayloadRecord checkpoint,
            WorkflowExternalRequestKind kind,
            string suffix)
        {
            var requestId = WorkflowExternalRequestId.New();
            var isHumanInput = kind == WorkflowExternalRequestKind.HumanInput;
            return new WorkflowExternalRequestRecord(
                requestId,
                run.RunId,
                kind,
                new WorkflowNodeId(suffix),
                suffix,
                isHumanInput
                    ? "{\"question\":\"What is your name?\"}"
                    : "{\"executorId\":\"http.fetch\",\"reason\":\"Fetch SimWiki article\"}",
                ResponseJson: string.Empty,
                run.UpdatedAtUtc,
                RespondedAtUtc: null)
            {
                Version = WorkflowExternalRequestVersion.Initial,
                State = WorkflowExternalRequestState.Pending,
                ResponseContract = new WorkflowExternalResponseContract(
                    kind,
                    isHumanInput ? "persistent.human-input" : "persistent.tool-approval",
                    schemaVersion: 1,
                    isHumanInput
                        ? "{\"type\":\"object\",\"properties\":{\"answer\":{\"type\":\"string\"}},\"required\":[\"answer\"],\"additionalProperties\":false}"
                        : "{\"type\":\"object\",\"properties\":{\"approved\":{\"type\":\"boolean\"},\"message\":{\"type\":\"string\"}},\"required\":[\"approved\"],\"additionalProperties\":false}",
                    maximumPayloadBytes: 4_096),
                Continuation = new WorkflowExternalRequestContinuation(
                    new WorkflowBackendExternalRequestLink(
                        requestId,
                        new WorkflowBackendRequestId($"request-{suffix}"),
                        new WorkflowBackendRequestPortId($"port-{suffix}")),
                    checkpoint.Index.Link,
                    checkpoint.Session.CompilerContractVersion,
                    checkpoint.Session.TopologyFingerprint,
                    checkpoint.Payload.Sha256),
                AuthorizationPolicy = new WorkflowExternalRequestAuthorizationPolicySnapshot(
                    Actor,
                    isHumanInput ? null : WorkflowExecutorIds.HttpFetch,
                    isHumanInput
                        ? WorkflowExecutorCapabilityFlags.None
                        : WorkflowExecutorCapabilityFlags.ReadsExternalData |
                          WorkflowExecutorCapabilityFlags.UsesNetwork,
                    isHumanInput
                        ? WorkflowExecutorApprovalRequirement.NotRequired
                        : WorkflowExecutorApprovalRequirement.RequiredForExternalEffect,
                    Actor.SubjectId)
                {
                    AuthorizationScope = AuthorizationScope,
                    AuthorizationPolicyFingerprint =
                        WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                    ResponseAuthorizationLifetimeSeconds =
                        WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds
                }
            };
        }

        private static WorkflowBackendStartResult CreateWaitingResult(
            WorkflowRunSnapshot run,
            WorkflowExternalRequestRecord request,
            WorkflowBackendCheckpointPayloadRecord checkpoint)
            => new(
                run,
                [
                    new WorkflowEventRecord(
                        Guid.NewGuid(),
                        run.RunId,
                        WorkflowEventKind.WaitingForInput,
                        request.NodeId,
                        run.Summary,
                        "{}",
                        run.UpdatedAtUtc)
                ],
                [request],
                Artifacts: [])
            {
                Checkpoints =
                [
                    new WorkflowCheckpointRecord(
                        WorkflowCheckpointId.New(),
                        run.RunId,
                        run.WorkflowId,
                        run.VersionId,
                        run.Backend,
                        WorkflowCheckpointKind.WaitingForInput,
                        WorkflowCheckpointTrustBoundary.TrustedRuntimeState,
                        WorkflowResumeAvailability.Available,
                        request.NodeId,
                        request.Id,
                        checkpoint.Index.Link.CheckpointId.Value,
                        $"persistent-checkpoint://{checkpoint.Index.Link.SessionId.Value}/{checkpoint.Index.Link.CheckpointId.Value}",
                        checkpoint.Payload.Sha256.Value,
                        run.Summary,
                        ResumeUnavailableReason: string.Empty,
                        run.UpdatedAtUtc,
                        ResumedAtUtc: null)
                ]
            };
    }

    private sealed class TestAuthorizer(TimeProvider timeProvider) :
        IWorkflowExternalRequestAuthorizer
    {
        public WorkflowExternalResponseActorContext.Authenticated ActorContext { get; } = new(
            Actor,
            WorkflowExternalResponseTrustedChannel.LocalOperator,
            new WorkflowExternalResponseCallerAccess(
                Guid.Parse("3c17f327-0a99-4a74-8338-875ead439f72"),
                new DatabaseProfileGeneration(1),
                AuthorizationScope,
                [AuthorizationScope],
                WorkflowExternalResponseCallerCapabilities.SubmitHumanInput |
                WorkflowExternalResponseCallerCapabilities.SubmitApprovalDecision,
                WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                timeProvider.GetUtcNow().AddMinutes(-1),
                timeProvider.GetUtcNow().AddMinutes(30)));

        public Task<WorkflowExternalRequestAuthorizationDecision> AuthorizeAsync(
            WorkflowExternalRequestAuthorizationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var policy = Assert.IsType<WorkflowExternalRequestAuthorizationPolicySnapshot>(
                request.Boundary.AuthorizationPolicy);
            return Task.FromResult(new WorkflowExternalRequestAuthorizationDecision(
                WorkflowExternalRequestAuthorizationOutcome.Authorized,
                new WorkflowAuthorizedExternalResponseActor(
                    Actor,
                    policy.AuthorizationScope!,
                    policy.AuthorizationPolicyFingerprint,
                    timeProvider.GetUtcNow(),
                    timeProvider.GetUtcNow().AddMinutes(15)),
                "Authorized by the persistent InMemory test boundary."));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) :
        IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }
}
