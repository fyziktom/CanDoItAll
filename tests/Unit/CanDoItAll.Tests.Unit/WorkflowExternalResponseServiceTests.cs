using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowExternalResponseServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-21T12:00:00Z");

    [Fact]
    public async Task AuthorizedValidCommand_ReturnsTheSameTerminalRequestOnFirstResultAndReplay()
    {
        var fixture = await Fixture.CreateAsync();

        var firstResult = await fixture.Service.SubmitAsync(fixture.CreateCommand());

        Assert.Equal(WorkflowExternalResponseServiceOutcome.Completed, firstResult.Outcome);
        Assert.Equal(WorkflowExternalRequestState.Responded, firstResult.Request?.EffectiveState);
        Assert.Equal("{\"answer\":\"yes\"}", firstResult.Request?.ResponseJson);
        Assert.Equal(1, fixture.Authorizer.CallCount);
        Assert.Equal(1, fixture.Validator.CallCount);
        Assert.Equal(1, fixture.OperationStore.CreateCallCount);
        Assert.Equal(1, fixture.Continuation.CallCount);
        Assert.False(firstResult.Replayed);

        fixture.OperationStore.CreateOutcome = WorkflowExternalResponseOperationCreateOutcome.Replayed;
        fixture.OperationStore.StoredOperation = firstResult.Operation;

        var replay = await fixture.Service.SubmitAsync(fixture.CreateCommand());

        Assert.Equal(WorkflowExternalResponseServiceOutcome.Completed, replay.Outcome);
        Assert.Equal(firstResult.Request, replay.Request);
        Assert.True(replay.Replayed);
        Assert.Equal(2, fixture.Authorizer.CallCount);
        Assert.Equal(2, fixture.Validator.CallCount);
        Assert.Equal(2, fixture.OperationStore.CreateCallCount);
        Assert.Equal(1, fixture.Continuation.CallCount);
    }

    [Fact]
    public async Task AuthorizationDenied_PerformsNoValidationOperationOrContinuation()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.Authorizer.Result = new WorkflowExternalRequestAuthorizationDecision(
            WorkflowExternalRequestAuthorizationOutcome.ScopeMismatch,
            Authorization: null,
            "The caller does not have the required workflow scope.");

        var result = await fixture.Service.SubmitAsync(fixture.CreateCommand());

        Assert.Equal(WorkflowExternalResponseServiceOutcome.Forbidden, result.Outcome);
        Assert.Equal(1, fixture.Authorizer.CallCount);
        Assert.Equal(0, fixture.Validator.CallCount);
        Assert.Equal(0, fixture.OperationStore.CreateCallCount);
        Assert.Equal(0, fixture.Continuation.CallCount);
    }

    [Fact]
    public async Task ValidationRejected_PerformsNoOperationOrContinuation()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.Validator.Result = new WorkflowExternalResponseValidationResult(
            WorkflowExternalResponseValidationOutcome.SchemaMismatch,
            CanonicalPayload: null,
            Action: null,
            "The response does not satisfy its persisted schema.");

        var result = await fixture.Service.SubmitAsync(fixture.CreateCommand());

        Assert.Equal(WorkflowExternalResponseServiceOutcome.InvalidResponse, result.Outcome);
        Assert.Equal(1, fixture.Authorizer.CallCount);
        Assert.Equal(1, fixture.Validator.CallCount);
        Assert.Equal(0, fixture.OperationStore.CreateCallCount);
        Assert.Equal(0, fixture.Continuation.CallCount);
    }

    [Fact]
    public async Task RejectedSubmission_LogsOnlyStructuredRedactedContext()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.Authorizer.Result = new WorkflowExternalRequestAuthorizationDecision(
            WorkflowExternalRequestAuthorizationOutcome.ScopeMismatch,
            Authorization: null,
            "The caller does not have the required workflow scope.");

        await fixture.Service.SubmitAsync(fixture.CreateCommand());

        var entry = Assert.Single(fixture.Logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(nameof(WorkflowExternalResponseServiceOutcome.Forbidden), entry.Message);
        Assert.Contains(fixture.Request.Id.ToString(), entry.Message);
        Assert.DoesNotContain("service-test-key", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("answer", entry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("yes", entry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("approver-1", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SameOperationActiveReplay_ReauthorizesAndReturnsResumingWithoutSecondContinuation()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.OperationStore.CreateOutcome = WorkflowExternalResponseOperationCreateOutcome.Replayed;
        fixture.OperationStore.OperationState = WorkflowExternalResponseOperationState.Resuming;
        fixture.OperationStore.ActiveLease = new WorkflowExternalResponseLease(
            new WorkflowExternalResponseLeaseOwnerId("active-host"),
            new WorkflowExternalResponseLeaseEpoch(1),
            Now,
            Now.AddMinutes(1));

        var result = await fixture.Service.SubmitAsync(fixture.CreateCommand());

        Assert.Equal(WorkflowExternalResponseServiceOutcome.Resuming, result.Outcome);
        Assert.True(result.Replayed);
        Assert.Equal(1, fixture.Authorizer.CallCount);
        Assert.Equal(1, fixture.Validator.CallCount);
        Assert.Equal(1, fixture.OperationStore.CreateCallCount);
        Assert.Equal(0, fixture.Continuation.CallCount);
    }

    [Fact]
    public async Task UnauthenticatedCommand_PerformsNoLookupOrMutation()
    {
        var fixture = await Fixture.CreateAsync();
        using var response = JsonDocument.Parse("{\"answer\":\"yes\"}");
        var command = new WorkflowExternalResponseCommand(
            new WorkflowExternalResponseActorContext.Unauthenticated(),
            fixture.Request.Id,
            fixture.Request.Version,
            response.RootElement,
            new WorkflowExternalResponseIdempotencyKey("service-test-key"),
            new WorkflowLaunchCorrelationId("service-test-correlation"));

        var result = await fixture.Service.SubmitAsync(command);

        Assert.Equal(WorkflowExternalResponseServiceOutcome.Unauthenticated, result.Outcome);
        Assert.Equal(0, fixture.Authorizer.CallCount);
        Assert.Equal(0, fixture.Validator.CallCount);
        Assert.Equal(0, fixture.OperationStore.CreateCallCount);
        Assert.Equal(0, fixture.Continuation.CallCount);
    }

    [Fact]
    public async Task GetStatusAsync_AuthorizesLinkedRequestAndReturnsStableOutcome()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.OperationStore.OperationState = WorkflowExternalResponseOperationState.Denied;
        var operation = fixture.OperationStore.CreateOperation(fixture.CreateOperationRequest());
        fixture.OperationStore.StoredOperation = operation;

        var result = await fixture.Service.GetStatusAsync(
            new WorkflowExternalResponseStatusQuery(
                fixture.ActorContext,
                operation.Id,
                new WorkflowLaunchCorrelationId("status-test")));

        Assert.Equal(WorkflowExternalResponseServiceOutcome.Denied, result.Outcome);
        Assert.False(result.Replayed);
        Assert.Equal(operation.Id, result.Operation?.Id);
        Assert.Equal(1, fixture.Authorizer.CallCount);
        Assert.Equal(0, fixture.Validator.CallCount);
    }

    private sealed class Fixture
    {
        private Fixture(
            InMemoryWorkflowRunStore runStore,
            WorkflowRunSnapshot run,
            WorkflowExternalRequestRecord request,
            WorkflowExternalRequestBoundaryRecord boundary,
            FixedBoundaryStore boundaryStore,
            RecordingAuthorizer authorizer,
            RecordingValidator validator,
            RecordingOperationStore operationStore,
            RecordingContinuation continuation,
            WorkflowExternalResponseActorContext.Authenticated actorContext,
            RecordingLogger<WorkflowExternalResponseService> logger)
        {
            RunStore = runStore;
            Run = run;
            Request = request;
            Boundary = boundary;
            BoundaryStore = boundaryStore;
            Authorizer = authorizer;
            Validator = validator;
            OperationStore = operationStore;
            Continuation = continuation;
            ActorContext = actorContext;
            Logger = logger;
            Service = new WorkflowExternalResponseService(
                runStore,
                boundaryStore,
                operationStore,
                continuation,
                authorizer,
                validator,
                new FixedTimeProvider(Now),
                logger);
        }

        public InMemoryWorkflowRunStore RunStore { get; }

        public WorkflowRunSnapshot Run { get; }

        public WorkflowExternalRequestRecord Request { get; }

        public WorkflowExternalRequestBoundaryRecord Boundary { get; }

        public FixedBoundaryStore BoundaryStore { get; }

        public RecordingAuthorizer Authorizer { get; }

        public RecordingValidator Validator { get; }

        public RecordingOperationStore OperationStore { get; }

        public RecordingContinuation Continuation { get; }

        public WorkflowExternalResponseActorContext.Authenticated ActorContext { get; }

        public RecordingLogger<WorkflowExternalResponseService> Logger { get; }

        public WorkflowExternalResponseService Service { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var actor = new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "approver-1");
            var scope = WorkspaceScopeDescriptor.Organization("service-test");
            var run = new WorkflowRunSnapshot(
                WorkflowRunId.New(),
                WorkflowId.New(),
                WorkflowVersionId.New(),
                WorkflowRunState.WaitingForInput,
                WorkflowRuntimeBackendKind.InProcess,
                "service-test-run",
                "Waiting for input.",
                Now.AddMinutes(-1),
                Now.AddMinutes(-1))
            {
                Origin = new WorkflowLaunchOrigin.Api(actor, new WorkflowLaunchCorrelationId("launch-test"))
                {
                    AuthorizationScope = scope,
                    AuthorizationPolicyFingerprint = WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint
                }
            };
            var requestId = WorkflowExternalRequestId.New();
            var continuation = new WorkflowExternalRequestContinuation(
                new WorkflowBackendExternalRequestLink(
                    requestId,
                    new WorkflowBackendRequestId("native-request"),
                    new WorkflowBackendRequestPortId("human-input")),
                new WorkflowBackendCheckpointLink(
                    new WorkflowBackendSessionId(run.BackendRunId),
                    new WorkflowBackendCheckpointId("checkpoint-1")),
                new WorkflowCompilerContractVersion(1),
                WorkflowTopologyFingerprint.Create("service-test-topology"),
                WorkflowBackendCheckpointPayloadHash.Compute("{}"));
            var policy = new WorkflowExternalRequestAuthorizationPolicySnapshot(
                actor,
                ExecutorId: null,
                WorkflowExecutorCapabilityFlags.None,
                WorkflowExecutorApprovalRequirement.NotRequired,
                IntendedApproverSubjectId: string.Empty)
            {
                AuthorizationScope = scope,
                AuthorizationPolicyFingerprint = WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                ResponseAuthorizationLifetimeSeconds = WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds
            };
            var request = new WorkflowExternalRequestRecord(
                requestId,
                run.RunId,
                WorkflowExternalRequestKind.HumanInput,
                new WorkflowNodeId("human-input"),
                "HumanInputRequested",
                "{\"prompt\":\"Continue?\"}",
                string.Empty,
                Now.AddSeconds(-30),
                RespondedAtUtc: null)
            {
                Version = WorkflowExternalRequestVersion.Initial,
                State = WorkflowExternalRequestState.Pending,
                ResponseContract = new WorkflowExternalResponseContract(
                    WorkflowExternalRequestKind.HumanInput,
                    "service-test-human-input",
                    1,
                    "{}",
                    4_096),
                Continuation = continuation,
                AuthorizationPolicy = policy
            };
            Assert.True(WorkflowExternalRequestBoundaryRecord.TryCreate(request, out var boundary));
            Assert.NotNull(boundary);

            var runStore = new InMemoryWorkflowRunStore();
            await runStore.SaveRunAsync(run);
            await runStore.SaveExternalRequestAsync(request);
            var authorized = new WorkflowAuthorizedExternalResponseActor(
                actor,
                scope,
                WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                Now,
                Now.AddMinutes(15));
            var authorizer = new RecordingAuthorizer
            {
                Result = new WorkflowExternalRequestAuthorizationDecision(
                    WorkflowExternalRequestAuthorizationOutcome.Authorized,
                    authorized,
                    "Authorized.")
            };
            var validator = new RecordingValidator
            {
                Result = new WorkflowExternalResponseValidationResult(
                    WorkflowExternalResponseValidationOutcome.Valid,
                    new WorkflowExternalResponsePayload("{\"answer\":\"yes\"}"),
                    WorkflowExternalResponseAction.SubmitInput,
                    "Valid.")
            };
            var operationStore = new RecordingOperationStore();
            var boundaryStore = new FixedBoundaryStore(boundary!);
            var responseContinuation = new RecordingContinuation(
                run,
                request,
                async () =>
                {
                    await runStore.SaveExternalRequestAsync(request with
                    {
                        ResponseJson = "{\"answer\":\"yes\"}",
                        RespondedAtUtc = Now,
                        State = WorkflowExternalRequestState.Responded
                    });
                    boundaryStore.Boundary = boundary! with
                    {
                        State = WorkflowExternalRequestState.Responded
                    };
                });
            var actorContext = new WorkflowExternalResponseActorContext.Authenticated(
                actor,
                WorkflowExternalResponseTrustedChannel.Api,
                new WorkflowExternalResponseCallerAccess(
                    Guid.NewGuid(),
                    new DatabaseProfileGeneration(1),
                    scope,
                    [scope],
                    WorkflowExternalResponseCallerCapabilities.SubmitHumanInput,
                    WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                    Now.AddMinutes(-1),
                    Now.AddMinutes(30)));
            var logger = new RecordingLogger<WorkflowExternalResponseService>();
            return new Fixture(
                runStore,
                run,
                request,
                boundary!,
                boundaryStore,
                authorizer,
                validator,
                operationStore,
                responseContinuation,
                actorContext,
                logger);
        }

        public WorkflowExternalResponseCommand CreateCommand()
        {
            using var response = JsonDocument.Parse("{\"answer\":\"yes\"}");
            return new WorkflowExternalResponseCommand(
                ActorContext,
                Request.Id,
                Request.Version,
                response.RootElement,
                new WorkflowExternalResponseIdempotencyKey("service-test-key"),
                new WorkflowLaunchCorrelationId("service-test-correlation"));
        }

        public WorkflowExternalResponseOperationCreateRequest CreateOperationRequest()
        {
            var fingerprint = WorkflowExternalResponseFingerprintFactory.Create(
                Request.Id,
                Request.Version,
                ((WorkflowExternalResponseActorContext.Authenticated)ActorContext).Actor,
                Boundary.AuthorizationPolicy!.AuthorizationScope!,
                Boundary.AuthorizationPolicy.AuthorizationPolicyFingerprint,
                new WorkflowExternalResponseIdempotencyKey("service-test-key"),
                "{\"answer\":\"yes\"}");
            return new WorkflowExternalResponseOperationCreateRequest(
                WorkflowExternalResponseOperationId.New(),
                Request.Id,
                Run.RunId,
                Request.Version,
                fingerprint,
                ((WorkflowExternalResponseActorContext.Authenticated)ActorContext).Actor,
                new WorkflowLaunchCorrelationId("service-test-correlation"),
                Now);
        }
    }

    private sealed class RecordingAuthorizer : IWorkflowExternalRequestAuthorizer
    {
        public int CallCount { get; private set; }

        public required WorkflowExternalRequestAuthorizationDecision Result { get; set; }

        public Task<WorkflowExternalRequestAuthorizationDecision> AuthorizeAsync(
            WorkflowExternalRequestAuthorizationRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(Result);
        }
    }

    private sealed class RecordingValidator : IWorkflowExternalResponseValidator
    {
        public int CallCount { get; private set; }

        public required WorkflowExternalResponseValidationResult Result { get; set; }

        public WorkflowExternalResponseValidationResult Validate(
            WorkflowExternalResponseValidationRequest request)
        {
            CallCount++;
            return Result;
        }
    }

    private sealed class FixedBoundaryStore : IWorkflowExternalRequestBoundaryStore
    {
        public FixedBoundaryStore(WorkflowExternalRequestBoundaryRecord boundary)
        {
            Boundary = boundary;
        }

        public WorkflowExternalRequestBoundaryRecord Boundary { get; set; }

        public Task<WorkflowExternalRequestBoundarySaveResult> UpsertAsync(
            WorkflowExternalRequestBoundaryRecord value,
            CancellationToken cancellationToken = default)
        {
            Boundary = value;
            return Task.FromResult(new WorkflowExternalRequestBoundarySaveResult(
                WorkflowExternalRequestBoundarySaveOutcome.Updated,
                value));
        }

        public Task<WorkflowExternalRequestBoundaryReadResult> ReadAsync(
            WorkflowExternalRequestId requestId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(requestId == Boundary.RequestId
                ? new WorkflowExternalRequestBoundaryReadResult(
                    WorkflowExternalRequestBoundaryReadOutcome.Found,
                    Boundary)
                : new WorkflowExternalRequestBoundaryReadResult(
                    WorkflowExternalRequestBoundaryReadOutcome.RequestNotFound,
                    Boundary: null));
    }

    private sealed class RecordingOperationStore : IWorkflowExternalResponseOperationStore
    {
        public int CreateCallCount { get; private set; }

        public WorkflowExternalResponseOperationCreateOutcome CreateOutcome { get; set; } =
            WorkflowExternalResponseOperationCreateOutcome.Created;

        public WorkflowExternalResponseOperationState OperationState { get; set; } =
            WorkflowExternalResponseOperationState.Accepted;

        public WorkflowExternalResponseLease? ActiveLease { get; set; }

        public WorkflowExternalResponseOperationRecord? StoredOperation { get; set; }

        public Task<WorkflowExternalResponseOperationCreateResult> CreateOrReplayAsync(
            WorkflowExternalResponseOperationCreateRequest request,
            CancellationToken cancellationToken = default)
        {
            CreateCallCount++;
            if (CreateOutcome != WorkflowExternalResponseOperationCreateOutcome.Replayed ||
                StoredOperation is null)
            {
                StoredOperation = CreateOperation(request);
            }

            return Task.FromResult(new WorkflowExternalResponseOperationCreateResult(
                CreateOutcome,
                StoredOperation,
                CreateOutcome == WorkflowExternalResponseOperationCreateOutcome.Replayed
                    ? new WorkflowExternalResponseOperationReplay(
                        StoredOperation.Id,
                        StoredOperation.State,
                        StoredOperation.FinalResult,
                        Now)
                    : null));
        }

        public WorkflowExternalResponseOperationRecord CreateOperation(
            WorkflowExternalResponseOperationCreateRequest request)
            => new(
                request.OperationId,
                request.RequestId,
                request.RunId,
                request.ExpectedRequestVersion,
                request.Fingerprint.IdempotencyKeyHash,
                request.Fingerprint.PayloadHash,
                request.Fingerprint.ActorScopeFingerprint,
                request.Fingerprint.CanonicalPayload,
                request.Actor,
                request.CorrelationId,
                OperationState,
                Attempt: 0,
                WorkflowExternalResponseOperationConcurrencyVersion.Initial,
                request.AcceptedAtUtc)
            {
                Lease = ActiveLease
            };

        public Task<WorkflowExternalResponseOperationRecord?> GetAsync(
            WorkflowExternalResponseOperationId operationId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(StoredOperation?.Id == operationId ? StoredOperation : null);

        public Task<IReadOnlyList<WorkflowExternalResponseOperationRecord>> ListRecoverableAsync(
            DateTimeOffset asOfUtc,
            int maximumCount,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseOperationClaimResult> TryClaimAsync(
            WorkflowExternalResponseOperationClaimRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseOperationMutationResult> TryRenewLeaseAsync(
            WorkflowExternalResponseOperationLeaseRenewalRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseOperationMutationResult> TryMarkResumingAsync(
            WorkflowExternalResponseOperationMarkResumingRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseOperationMutationResult> TryCompleteAsync(
            WorkflowExternalResponseOperationCompletionRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseOperationMutationResult> TryFailAsync(
            WorkflowExternalResponseOperationFailureRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseOperationMutationResult> TryReleaseLeaseAsync(
            WorkflowExternalResponseOperationLeaseReleaseRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingContinuation(
        WorkflowRunSnapshot run,
        WorkflowExternalRequestRecord externalRequest,
        Func<Task>? beforeReturnAsync = null) : IWorkflowExternalResponseContinuation
    {
        public int CallCount { get; private set; }

        public async Task<WorkflowExternalResponseContinuationResult> ContinueAsync(
            WorkflowExternalResponseContinuationRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (beforeReturnAsync is not null)
            {
                await beforeReturnAsync();
            }

            var completedRun = run with
            {
                State = WorkflowRunState.Completed,
                UpdatedAtUtc = Now,
                TerminalAtUtc = Now
            };
            return new WorkflowExternalResponseContinuationResult(
                WorkflowExternalResponseContinuationOutcome.Completed,
                new WorkflowExternalResponseOperationRecord(
                    request.OperationId,
                    externalRequest.Id,
                    run.RunId,
                    externalRequest.Version,
                    new WorkflowExternalResponseIdempotencyKeyHash(new string('1', 64)),
                    new WorkflowExternalResponsePayloadHash(new string('2', 64)),
                    new WorkflowExternalResponseActorScopeFingerprint(new string('3', 64)),
                    new WorkflowExternalResponsePayload("{}"),
                    new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "approver-1"),
                    new WorkflowLaunchCorrelationId("continuation"),
                    WorkflowExternalResponseOperationState.Completed,
                    Attempt: 1,
                    WorkflowExternalResponseOperationConcurrencyVersion.Initial,
                    Now)
                {
                    CompletedAtUtc = Now,
                    OutcomeCode = WorkflowExternalResponseOperationOutcomeCode.Completed,
                    FinalResult = new WorkflowExternalResponseOperationFinalResult(
                        WorkflowExternalResponseOperationState.Completed,
                        WorkflowExternalResponseOperationOutcomeCode.Completed,
                        "Completed.",
                        WorkflowRunState.Completed)
                },
                completedRun,
                NextRequest: null,
                "Completed.");
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
