using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafWorkflowHumanInLoopTests
{
    private static readonly WorkflowValueShape JsonObjectShape = new(
        WorkflowValueShapeKind.Json,
        """{"type":"object"}""",
        "JSON object");

    [Fact]
    public async Task Human_input_round_trip_rehydrates_disposed_run_without_replaying_marker()
    {
        var marker = new CountingLlmInvoker();
        var component = CreateComponent();
        var definition = CreateHumanInputDefinition(component);
        var runStore = new InMemoryWorkflowRunStore();
        var retainedCheckpointState = new InMemoryWorkflowBackendCheckpointPayloadStore(TimeProvider.System);

        var first = await StartWithFreshRuntimeAsync(
            definition,
            component,
            marker,
            runStore,
            retainedCheckpointState);
        var waitingCheckpoint = Assert.Single(await runStore.ListCheckpointsAsync(first.Run.RunId));
        var continuation = Assert.IsType<WorkflowExternalRequestContinuation>(first.Request.Continuation);
        var payload = await retainedCheckpointState.ReadAsync(continuation.Checkpoint);

        Assert.Equal(WorkflowRunState.WaitingForInput, first.Run.State);
        Assert.Equal(WorkflowExternalRequestState.Pending, first.Request.State);
        Assert.Equal(WorkflowCheckpointTrustBoundary.TrustedRuntimeState, waitingCheckpoint.TrustBoundary);
        Assert.Equal(WorkflowResumeAvailability.Available, waitingCheckpoint.ResumeAvailability);
        Assert.True(payload.Succeeded);
        Assert.Equal(continuation.CheckpointPayloadHash, payload.Checkpoint?.Payload.Sha256);
        Assert.Contains("prompt", first.Request.RequestJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("responseShape", first.Request.RequestJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("context", first.Request.RequestJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, marker.InvocationCount);

        var secondCatalog = new ExactWorkflowCatalog(definition);
        var secondCompiler = CreateCompiler(marker);
        var secondBackend = new MafInProcessWorkflowExecutionBackend(
            secondCompiler,
            [component],
            checkpointPayloadStore: new ForwardingCheckpointStore(retainedCheckpointState),
            catalog: secondCatalog);
        var secondRuntime = WorkflowExternalResponseTestCompositionFactory.Create(
            [secondBackend],
            runStore,
            retainedCheckpointState);
        var response = await secondRuntime.Responses.SubmitAsync(
            first.Request,
            """{"answer":"continue"}""",
            "rehydrated-human-input");

        Assert.True(secondBackend.Descriptor.SupportsExternalResponseResume);
        Assert.False(secondBackend.Descriptor.IsDurable);
        Assert.Equal(WorkflowExternalResponseServiceOutcome.Completed, response.Outcome);
        Assert.Equal(WorkflowRunState.Completed, response.Run?.State);
        Assert.Equal(1, marker.InvocationCount);
        Assert.Equal(definition.VersionId, secondCatalog.RequestedVersionId);
        Assert.Equal(1, secondCatalog.ExactReadCount);
    }

    [Fact]
    public async Task Resume_rejects_wrong_session_request_port_topology_and_missing_payload()
    {
        var marker = new CountingLlmInvoker();
        var component = CreateComponent();
        var definition = CreateHumanInputDefinition(component);
        var runStore = new InMemoryWorkflowRunStore();
        var retainedCheckpointState = new InMemoryWorkflowBackendCheckpointPayloadStore(TimeProvider.System);
        var first = await StartWithFreshRuntimeAsync(
            definition,
            component,
            marker,
            runStore,
            retainedCheckpointState);
        var backend = CreateNativeBackend(
            definition,
            component,
            marker,
            new ForwardingCheckpointStore(retainedCheckpointState));
        using var responseDocument = JsonDocument.Parse("""{"answer":"continue"}""");

        var wrongSession = first.Request with
        {
            Continuation = first.Request.Continuation! with
            {
                Checkpoint = new WorkflowBackendCheckpointLink(
                    new WorkflowBackendSessionId("wrong-session"),
                    first.Request.Continuation.Checkpoint.CheckpointId)
            }
        };
        var wrongPort = first.Request with
        {
            Continuation = first.Request.Continuation! with
            {
                Request = first.Request.Continuation.Request with
                {
                    BackendRequestPortId = new WorkflowBackendRequestPortId("wrong-port")
                }
            }
        };
        var wrongRequest = first.Request with
        {
            Continuation = first.Request.Continuation! with
            {
                Request = first.Request.Continuation.Request with
                {
                    BackendRequestId = new WorkflowBackendRequestId("wrong-request")
                }
            }
        };
        var wrongTopology = first.Request with
        {
            Continuation = first.Request.Continuation! with
            {
                TopologyFingerprint = WorkflowTopologyFingerprint.Create("wrong-topology")
            }
        };
        var missingPayload = first.Request with
        {
            Continuation = first.Request.Continuation! with
            {
                Checkpoint = new WorkflowBackendCheckpointLink(
                    first.Request.Continuation.Checkpoint.SessionId,
                    WorkflowBackendCheckpointId.New())
            }
        };

        var wrongSessionFailure = await Assert.ThrowsAsync<WorkflowBackendResumeException>(
            () => ResumeAsync(backend, first.Run, wrongSession, responseDocument.RootElement));
        var wrongPortFailure = await Assert.ThrowsAsync<WorkflowBackendResumeException>(() => ResumeAsync(
            CreateNativeBackend(definition, component, marker, new ForwardingCheckpointStore(retainedCheckpointState)),
            first.Run,
            wrongPort,
            responseDocument.RootElement));
        var wrongRequestFailure = await Assert.ThrowsAsync<WorkflowBackendResumeException>(() => ResumeAsync(
            CreateNativeBackend(definition, component, marker, new ForwardingCheckpointStore(retainedCheckpointState)),
            first.Run,
            wrongRequest,
            responseDocument.RootElement));
        var wrongTopologyFailure = await Assert.ThrowsAsync<WorkflowBackendResumeException>(
            () => ResumeAsync(backend, first.Run, wrongTopology, responseDocument.RootElement));
        var missingPayloadFailure = await Assert.ThrowsAsync<WorkflowBackendResumeException>(
            () => ResumeAsync(backend, first.Run, missingPayload, responseDocument.RootElement));

        Assert.Equal(WorkflowBackendResumeFailureKind.CheckpointIncompatible, wrongSessionFailure.Kind);
        Assert.Equal(WorkflowBackendResumeFailureKind.PortMismatch, wrongPortFailure.Kind);
        Assert.Equal(WorkflowBackendResumeFailureKind.RequestMismatch, wrongRequestFailure.Kind);
        Assert.Equal(WorkflowBackendResumeFailureKind.TopologyMismatch, wrongTopologyFailure.Kind);
        Assert.Equal(WorkflowBackendResumeFailureKind.CheckpointMissing, missingPayloadFailure.Kind);
        Assert.Equal(1, marker.InvocationCount);
    }

    [Fact]
    public async Task Approval_round_trip_invokes_once_denial_invokes_never_and_tampering_is_rejected()
    {
        var approvedExecutor = new RecordingApprovalExecutor();
        var approved = await StartApprovalWithFreshRuntimeAsync(approvedExecutor);

        Assert.Equal(WorkflowRunState.WaitingForInput, approved.Run.State);
        Assert.Equal(0, approvedExecutor.InvocationCount);
        Assert.DoesNotContain("protected-value", approved.Request.RequestJson, StringComparison.Ordinal);
        Assert.DoesNotContain("approvalToken", approved.Request.RequestJson, StringComparison.OrdinalIgnoreCase);

        var approveRuntime = CreateApprovalRuntime(
            approved.Definition,
            approvedExecutor,
            approved.RunStore,
            approved.CheckpointState);
        var approveResult = await approveRuntime.Responses.SubmitAsync(
            approved.Request,
            """{"approved":true,"message":"approved"}""",
            "approve");

        Assert.True(
            approveResult.Outcome == WorkflowExternalResponseServiceOutcome.Completed,
            $"{approveResult.SafeMessage} Run summary: {approveResult.Run?.Summary}");
        Assert.True(
            approveResult.Run?.State == WorkflowRunState.Completed,
            $"Approval resume did not complete. Run summary: {approveResult.Run?.Summary}");
        Assert.Equal(1, approvedExecutor.InvocationCount);
        Assert.Equal("""{"original":"protected-value"}""", approvedExecutor.LastInputJson);

        var deniedExecutor = new RecordingApprovalExecutor();
        var denied = await StartApprovalWithFreshRuntimeAsync(deniedExecutor);
        var denyRuntime = CreateApprovalRuntime(
            denied.Definition,
            deniedExecutor,
            denied.RunStore,
            denied.CheckpointState);
        var denyResult = await denyRuntime.Responses.SubmitAsync(
            denied.Request,
            """{"approved":false,"message":"not allowed"}""",
            "deny");

        Assert.Equal(WorkflowExternalResponseServiceOutcome.Denied, denyResult.Outcome);
        Assert.Equal(WorkflowRunState.Completed, denyResult.Run?.State);
        Assert.Equal(0, deniedExecutor.InvocationCount);

        var tamperedExecutor = new RecordingApprovalExecutor();
        var tampered = await StartApprovalWithFreshRuntimeAsync(tamperedExecutor);
        var tamperRuntime = CreateApprovalRuntime(
            tampered.Definition,
            tamperedExecutor,
            tampered.RunStore,
            tampered.CheckpointState);
        var tamperResult = await tamperRuntime.Responses.SubmitAsync(
            tampered.Request,
            """{"approved":true,"originalInput":{"original":"attacker-value"}}""",
            "tampered-approval");

        Assert.Equal(WorkflowExternalResponseServiceOutcome.InvalidResponse, tamperResult.Outcome);
        Assert.Equal(WorkflowRunState.WaitingForInput, tamperResult.Run?.State);
        Assert.Equal(0, tamperedExecutor.InvocationCount);
    }

    [Fact]
    public async Task LegacyThreeArgumentApprovalResumeFailsClosedWithoutExecutorEffect()
    {
        var executor = new RecordingApprovalExecutor();
        var started = await StartApprovalWithFreshRuntimeAsync(executor);
        var backend = new MafInProcessWorkflowExecutionBackend(
            CreateApprovalCompiler(executor),
            [],
            checkpointPayloadStore: new ForwardingCheckpointStore(started.CheckpointState),
            catalog: new ExactWorkflowCatalog(started.Definition));
        using var response = JsonDocument.Parse("""{"approved":true,"message":"approved"}""");

        var failure = await Assert.ThrowsAsync<WorkflowBackendResumeException>(() => backend.ResumeAsync(
            new WorkflowBackendResumeRequest(
                started.Run,
                started.Request,
                response.RootElement.Clone())));

        Assert.Equal(WorkflowBackendResumeFailureKind.RequestMismatch, failure.Kind);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task Consecutive_human_input_boundaries_each_resume_fresh_and_marker_runs_once()
    {
        var marker = new CountingLlmInvoker();
        var component = CreateComponent();
        var definition = CreateConsecutiveHumanInputDefinition(component);
        var runStore = new InMemoryWorkflowRunStore();
        var checkpointState = new InMemoryWorkflowBackendCheckpointPayloadStore(TimeProvider.System);
        var first = await StartWithFreshRuntimeAsync(
            definition,
            component,
            marker,
            runStore,
            checkpointState);

        var secondRuntime = WorkflowExternalResponseTestCompositionFactory.Create(
            [CreateNativeBackend(definition, component, marker, new ForwardingCheckpointStore(checkpointState))],
            runStore,
            checkpointState);
        var secondWait = await secondRuntime.Responses.SubmitAsync(
            first.Request,
            """{"answer":"first"}""",
            "first-human-input");
        var pendingAfterFirstResume = await runStore.ListPendingExternalRequestsAsync(first.Run.RunId);
        var secondRequest = Assert.Single(pendingAfterFirstResume);

        Assert.Equal(WorkflowExternalResponseServiceOutcome.WaitingAgain, secondWait.Outcome);
        Assert.Equal(WorkflowRunState.WaitingForInput, secondWait.Run?.State);
        Assert.NotEqual(first.Request.Id, secondRequest.Id);
        Assert.NotEqual(
            first.Request.Continuation?.Checkpoint,
            secondRequest.Continuation?.Checkpoint);
        Assert.Equal(1, marker.InvocationCount);

        var thirdRuntime = WorkflowExternalResponseTestCompositionFactory.Create(
            [CreateNativeBackend(definition, component, marker, new ForwardingCheckpointStore(checkpointState))],
            runStore,
            checkpointState);
        var completed = await thirdRuntime.Responses.SubmitAsync(
            secondRequest,
            """{"answer":"second"}""",
            "second-human-input");

        Assert.Equal(WorkflowExternalResponseServiceOutcome.Completed, completed.Outcome);
        Assert.Equal(WorkflowRunState.Completed, completed.Run?.State);
        Assert.Equal(1, marker.InvocationCount);
    }

    [Fact]
    public async Task Rehydration_verifier_and_external_driver_are_directly_testable_without_runtime_manager()
    {
        var marker = new CountingLlmInvoker();
        var component = CreateComponent();
        var definition = CreateHumanInputDefinition(component);
        var checkpointState = new InMemoryWorkflowBackendCheckpointPayloadStore(TimeProvider.System);
        var compiler = CreateCompiler(marker);
        var build = compiler.Compile(definition, [component], WorkflowPreviewSimulationPlan.Empty);
        Assert.True(build.Compilation.Succeeded, build.Compilation.ErrorMessage);
        var verifier = new MafWorkflowRehydrationVerifier();
        var streamingDriver = new MafWorkflowStreamingRunDriver();
        var requestMapper = new MafWorkflowExternalRequestMapper(TimeProvider.System);
        var turnResultMapper = new MafWorkflowTurnResultMapper(
            checkpointState,
            requestMapper,
            new MafWorkflowEventNormalizer(),
            new WorkflowCheckpointFactory(),
            new WorkflowPayloadPolicyService(),
            TimeProvider.System);
        var startDriver = new MafWorkflowNativeStartDriver(
            checkpointState,
            streamingDriver,
            turnResultMapper,
            TimeProvider.System);
        var driver = new MafWorkflowExternalResponseDriver(
            compiler,
            new ExactWorkflowCatalog(definition),
            checkpointState,
            streamingDriver,
            verifier,
            requestMapper,
            turnResultMapper);
        var started = await startDriver.StartAsync(
            definition,
            CreateStartRequest(definition),
            WorkflowRunId.New(),
            build,
            CancellationToken.None);
        var firstRequest = Assert.Single(started.ExternalRequests);
        var authorizationPolicy = Assert.IsType<WorkflowExternalRequestAuthorizationPolicySnapshot>(
            firstRequest.AuthorizationPolicy);
        Assert.Equal(CreateTestOrigin().AuthorizationScope, authorizationPolicy.AuthorizationScope);
        Assert.Equal(
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            authorizationPolicy.AuthorizationPolicyFingerprint);
        Assert.Equal(
            WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds,
            authorizationPolicy.ResponseAuthorizationLifetimeSeconds);
        Assert.True(WorkflowExternalRequestBoundaryRecord.TryCreate(firstRequest, out var firstBoundary));
        Assert.NotNull(firstBoundary);
        Assert.Equal(
            InMemoryWorkflowCheckpointRequestLinkOutcome.Linked,
            checkpointState.TryLinkExternalRequest(firstBoundary, started.Run));
        using var response = JsonDocument.Parse("""{"answer":"direct"}""");
        var resume = CreateAuthorizedResumeRequest(
            started.Run,
            firstRequest,
            response.RootElement);

        var verified = await verifier.VerifyAsync(
            resume,
            definition,
            build,
            checkpointState,
            CancellationToken.None);
        var wrongIdentity = resume with
        {
            Run = resume.Run with { VersionId = WorkflowVersionId.New() }
        };
        var wrongTopology = resume with
        {
            ExternalRequest = resume.ExternalRequest with
            {
                Continuation = resume.ExternalRequest.Continuation! with
                {
                    TopologyFingerprint = WorkflowTopologyFingerprint.Create("direct-wrong-topology")
                }
            }
        };
        var wrongHash = resume with
        {
            ExternalRequest = resume.ExternalRequest with
            {
                Continuation = resume.ExternalRequest.Continuation! with
                {
                    CheckpointPayloadHash = WorkflowBackendCheckpointPayloadHash.Compute("{}")
                }
            }
        };

        var wrongIdentityFailure = await Assert.ThrowsAsync<WorkflowBackendResumeException>(() => verifier.VerifyAsync(
            wrongIdentity,
            definition,
            build,
            checkpointState,
            CancellationToken.None));
        var wrongTopologyFailure = await Assert.ThrowsAsync<WorkflowBackendResumeException>(() => verifier.VerifyAsync(
            wrongTopology,
            definition,
            build,
            checkpointState,
            CancellationToken.None));
        var wrongHashFailure = await Assert.ThrowsAsync<WorkflowBackendResumeException>(() => verifier.VerifyAsync(
            wrongHash,
            definition,
            build,
            checkpointState,
            CancellationToken.None));
        var corruptPayloadFailure = await Assert.ThrowsAsync<WorkflowBackendResumeException>(() => verifier.VerifyAsync(
            resume,
            definition,
            build,
            new CorruptReadCheckpointStore(checkpointState),
            CancellationToken.None));
        var result = await driver.ResumeAsync(resume, [component], CancellationToken.None);

        Assert.Equal(WorkflowBackendResumeFailureKind.ExactWorkflowVersionMismatch, wrongIdentityFailure.Kind);
        Assert.Equal(WorkflowBackendResumeFailureKind.TopologyMismatch, wrongTopologyFailure.Kind);
        Assert.Equal(WorkflowBackendResumeFailureKind.CheckpointCorrupt, wrongHashFailure.Kind);
        Assert.Equal(WorkflowBackendResumeFailureKind.CheckpointCorrupt, corruptPayloadFailure.Kind);
        Assert.Equal(firstRequest.Continuation?.Checkpoint, verified.Index.Link);
        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
        Assert.Equal(1, marker.InvocationCount);
    }

    [Fact]
    public void Native_resume_capability_requires_both_store_and_exact_catalog()
    {
        var component = CreateComponent();
        var definition = CreateHumanInputDefinition(component);
        var compiler = CreateCompiler(new CountingLlmInvoker());
        var store = new InMemoryWorkflowBackendCheckpointPayloadStore(TimeProvider.System);

        var defaultBackend = new MafInProcessWorkflowExecutionBackend(compiler, [component]);
        var storeOnly = new MafInProcessWorkflowExecutionBackend(
            compiler,
            [component],
            checkpointPayloadStore: store);
        var catalogOnly = new MafInProcessWorkflowExecutionBackend(
            compiler,
            [component],
            catalog: new ExactWorkflowCatalog(definition));
        var complete = new MafInProcessWorkflowExecutionBackend(
            compiler,
            [component],
            checkpointPayloadStore: store,
            catalog: new ExactWorkflowCatalog(definition));

        Assert.False(defaultBackend.Descriptor.SupportsExternalResponseResume);
        Assert.False(storeOnly.Descriptor.SupportsExternalResponseResume);
        Assert.False(catalogOnly.Descriptor.SupportsExternalResponseResume);
        Assert.True(complete.Descriptor.SupportsExternalResponseResume);
        Assert.All(
            new[] { defaultBackend, storeOnly, catalogOnly, complete },
            backend => Assert.False(backend.Descriptor.IsDurable));
    }

    [Fact]
    public async Task Complete_composition_keeps_non_hitl_workflow_on_legacy_execution_path()
    {
        var marker = new CountingLlmInvoker();
        var component = CreateComponent();
        var definition = CreateCompletedDefinition(component);
        var checkpointStore = new InMemoryWorkflowBackendCheckpointPayloadStore(TimeProvider.System);
        var backend = CreateNativeBackend(
            definition,
            component,
            marker,
            checkpointStore);

        var result = await backend.StartAsync(
            definition,
            CreateStartRequest(definition),
            WorkflowRunId.New());
        var index = await checkpointStore.ListIndexAsync(
            new WorkflowBackendSessionId(result.Run.RunId.ToString()));

        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
        Assert.Equal(1, marker.InvocationCount);
        Assert.Equal(WorkflowBackendCheckpointListOutcome.SessionNotFound, index.Outcome);
    }

    [Fact]
    public void Production_registration_advertises_resume_only_when_native_dependencies_are_registered()
    {
        var component = CreateComponent();
        var definition = CreateHumanInputDefinition(component);

        using var completeProvider = CreateProductionProvider(
            definition,
            component,
            includeCheckpointStore: true);
        using var incompleteProvider = CreateProductionProvider(
            definition,
            component,
            includeCheckpointStore: false);
        using var completeScope = completeProvider.CreateScope();
        using var incompleteScope = incompleteProvider.CreateScope();
        var complete = Assert.IsType<MafInProcessWorkflowExecutionBackend>(
            Assert.Single(completeScope.ServiceProvider.GetServices<IWorkflowExecutionBackend>()));
        var incomplete = Assert.IsType<MafInProcessWorkflowExecutionBackend>(
            Assert.Single(incompleteScope.ServiceProvider.GetServices<IWorkflowExecutionBackend>()));

        Assert.True(complete.Descriptor.SupportsExternalResponseResume);
        Assert.False(complete.Descriptor.IsDurable);
        Assert.False(incomplete.Descriptor.SupportsExternalResponseResume);
        Assert.False(incomplete.Descriptor.IsDurable);
    }

    private static async Task<(WorkflowRunSnapshot Run, WorkflowExternalRequestRecord Request)> StartWithFreshRuntimeAsync(
        WorkflowDefinition definition,
        LlmCallComponent component,
        CountingLlmInvoker marker,
        InMemoryWorkflowRunStore runStore,
        InMemoryWorkflowBackendCheckpointPayloadStore checkpointStore)
    {
        var backend = CreateNativeBackend(
            definition,
            component,
            marker,
            new ForwardingCheckpointStore(checkpointStore));
        var runtime = WorkflowExternalResponseTestCompositionFactory.Create(
            [backend],
            runStore,
            checkpointStore);
        var run = await runtime.Manager.StartAsync(definition, CreateStartRequest(definition));
        var request = Assert.Single(await runStore.ListPendingExternalRequestsAsync(run.RunId));
        return (run, request);
    }

    private static MafInProcessWorkflowExecutionBackend CreateNativeBackend(
        WorkflowDefinition definition,
        LlmCallComponent component,
        CountingLlmInvoker marker,
        IWorkflowBackendCheckpointPayloadStore checkpointStore)
    {
        var compiler = CreateCompiler(marker);
        var build = compiler.Compile(definition, [component], WorkflowPreviewSimulationPlan.Empty);
        Assert.True(build.Compilation.Succeeded, build.Compilation.ErrorMessage);

        return new MafInProcessWorkflowExecutionBackend(
            compiler,
            [component],
            checkpointPayloadStore: checkpointStore,
            catalog: new ExactWorkflowCatalog(definition));
    }

    private static ServiceProvider CreateProductionProvider(
        WorkflowDefinition definition,
        LlmCallComponent component,
        bool includeCheckpointStore)
    {
        var services = new ServiceCollection();
        var catalog = new ExactWorkflowCatalog(definition, [component]);
        services.AddSingleton<IWorkflowCatalogService>(catalog);
        services.AddSingleton<IWorkflowComponentLibraryService>(catalog);
        if (includeCheckpointStore)
        {
            services.AddSingleton<IWorkflowBackendCheckpointPayloadStore>(
                new InMemoryWorkflowBackendCheckpointPayloadStore(TimeProvider.System));
        }

        services.AddMafWorkflowAdapterServices(ServiceLifetime.Scoped);
        services.AddSingleton<IWorkflowMafCompiler>(CreateCompiler(new CountingLlmInvoker()));
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = false,
            ValidateScopes = true
        });
    }

    private static MafWorkflowCompiler CreateCompiler(CountingLlmInvoker marker)
        => new(
            new WorkflowDefinitionValidator(),
            llmComponentInvoker: marker);

    private static Task<WorkflowBackendStartResult> ResumeAsync(
        MafInProcessWorkflowExecutionBackend backend,
        WorkflowRunSnapshot run,
        WorkflowExternalRequestRecord request,
        JsonElement response)
    {
        return backend.ResumeAsync(CreateAuthorizedResumeRequest(run, request, response));
    }

    private static WorkflowRunStartRequest CreateStartRequest(WorkflowDefinition definition)
    {
        return new WorkflowRunStartRequest(
            definition.Id,
            definition.VersionId,
            """{"marker":"before-human"}""",
            WorkflowRuntimeBackendKind.InProcess,
            SourceProcessRunId: null,
            SourceProcessAssignmentId: null)
        {
            Origin = CreateTestOrigin()
        };
    }

    private static WorkflowLaunchOrigin CreateTestOrigin()
        => new WorkflowLaunchOrigin.Api(
            new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "native-hitl-test-user"),
            new WorkflowLaunchCorrelationId("native-hitl-test"))
        {
            AuthorizationScope = WorkspaceScopeDescriptor.Organization("native-hitl-test-profile"),
            AuthorizationPolicyFingerprint = WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint
        };

    private static WorkflowBackendResumeRequest CreateAuthorizedResumeRequest(
        WorkflowRunSnapshot run,
        WorkflowExternalRequestRecord request,
        JsonElement response)
    {
        var policy = Assert.IsType<WorkflowExternalRequestAuthorizationPolicySnapshot>(
            request.AuthorizationPolicy);
        var scope = Assert.IsType<WorkspaceScopeDescriptor>(policy.AuthorizationScope);
        var actor = Assert.IsType<WorkflowLaunchOrigin.Api>(run.Origin).Actor;
        var action = request.Kind switch
        {
            WorkflowExternalRequestKind.HumanInput => WorkflowExternalResponseAction.SubmitInput,
            WorkflowExternalRequestKind.Approval or WorkflowExternalRequestKind.ToolApproval =>
                response.GetProperty("approved").GetBoolean()
                    ? WorkflowExternalResponseAction.Approve
                    : WorkflowExternalResponseAction.Deny,
            _ => throw new InvalidOperationException($"Unsupported test request kind '{request.Kind}'.")
        };
        var authorizedAtUtc = DateTimeOffset.UtcNow;
        var operationId = WorkflowExternalResponseOperationId.New();
        var authorization = new WorkflowExternalResponseAuthorization(
            operationId,
            request.Id,
            request.Version,
            run.RunId,
            run.WorkflowId,
            run.VersionId,
            request.Kind,
            action,
            actor,
            scope,
            policy.OriginActor,
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            authorizedAtUtc,
            authorizedAtUtc.AddSeconds(
                WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds));
        return new WorkflowBackendResumeRequest(
            run,
            request,
            response.Clone(),
            operationId,
            request.Version.Value,
            authorization);
    }

    private static WorkflowDefinition CreateHumanInputDefinition(LlmCallComponent component)
    {
        return CreateDefinition(
            component,
            [
                CreateNode("start", WorkflowNodeKind.Start),
                CreateNode("marker", WorkflowNodeKind.LlmCall, component.Id),
                CreateNode("human", WorkflowNodeKind.HumanInput, resultShape: JsonObjectShape),
                CreateNode("end", WorkflowNodeKind.End, inputShape: JsonObjectShape)
            ],
            [
                CreateEdge("start-marker", "start", "marker"),
                CreateEdge("marker-human", "marker", "human"),
                CreateEdge("human-end", "human", "end")
            ]);
    }

    private static WorkflowDefinition CreateConsecutiveHumanInputDefinition(LlmCallComponent component)
    {
        return CreateDefinition(
            component,
            [
                CreateNode("start", WorkflowNodeKind.Start),
                CreateNode("marker", WorkflowNodeKind.LlmCall, component.Id),
                CreateNode("human-one", WorkflowNodeKind.HumanInput, resultShape: JsonObjectShape),
                CreateNode(
                    "human-two",
                    WorkflowNodeKind.HumanInput,
                    inputShape: JsonObjectShape,
                    resultShape: JsonObjectShape),
                CreateNode("end", WorkflowNodeKind.End, inputShape: JsonObjectShape)
            ],
            [
                CreateEdge("start-marker", "start", "marker"),
                CreateEdge("marker-human-one", "marker", "human-one"),
                CreateEdge("human-one-human-two", "human-one", "human-two"),
                CreateEdge("human-two-end", "human-two", "end")
            ]);
    }

    private static async Task<ApprovalFixture> StartApprovalWithFreshRuntimeAsync(
        RecordingApprovalExecutor executor)
    {
        var definition = CreateApprovalDefinition();
        var runStore = new InMemoryWorkflowRunStore();
        var checkpointState = new InMemoryWorkflowBackendCheckpointPayloadStore(TimeProvider.System);
        var runtime = CreateApprovalRuntime(
            definition,
            executor,
            runStore,
            checkpointState);
        var run = await runtime.Manager.StartAsync(
            definition,
            new WorkflowRunStartRequest(
                definition.Id,
                definition.VersionId,
                """{"original":"protected-value"}""",
                WorkflowRuntimeBackendKind.InProcess,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null)
            {
                Origin = CreateTestOrigin()
            });
        var request = Assert.Single(await runStore.ListPendingExternalRequestsAsync(run.RunId));
        return new ApprovalFixture(definition, run, request, runStore, checkpointState);
    }

    private static WorkflowExternalResponseTestComposition CreateApprovalRuntime(
        WorkflowDefinition definition,
        RecordingApprovalExecutor executor,
        InMemoryWorkflowRunStore runStore,
        InMemoryWorkflowBackendCheckpointPayloadStore checkpointStore)
    {
        return WorkflowExternalResponseTestCompositionFactory.Create(
            [
                new MafInProcessWorkflowExecutionBackend(
                    CreateApprovalCompiler(executor),
                    [],
                    checkpointPayloadStore: new ForwardingCheckpointStore(checkpointStore),
                    catalog: new ExactWorkflowCatalog(definition))
            ],
            runStore,
            checkpointStore);
    }

    private static MafWorkflowCompiler CreateApprovalCompiler(RecordingApprovalExecutor executor)
    {
        var executorCatalog = new WorkflowExecutorCatalog([executor]);
        return new MafWorkflowCompiler(
            new WorkflowDefinitionValidator(executorCatalog),
            new WorkflowExecutorInvoker(executorCatalog, [executor]),
            executorCatalog: executorCatalog);
    }

    private static WorkflowDefinition CreateApprovalDefinition()
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Native approval workflow",
            "Exercises immutable approval continuation state.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(
                new WorkflowNodeId("start"),
                [
                    CreateNode("start", WorkflowNodeKind.Start),
                    CreateApprovalNode("governed-effect"),
                    CreateNode(
                        "end",
                        WorkflowNodeKind.End,
                        inputShape: RecordingApprovalExecutor.TestDescriptor.ResultShape)
                ],
                [
                    CreateEdge("start-effect", "start", "governed-effect"),
                    CreateEdge("effect-end", "governed-effect", "end")
                ]),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            now,
            now);
    }

    private static WorkflowNode CreateApprovalNode(string id)
    {
        return new WorkflowNode(
            new WorkflowNodeId(id),
            WorkflowNodeKind.Executor,
            id,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: RecordingApprovalExecutor.TestDescriptor.InputShape,
                ResultShape: RecordingApprovalExecutor.TestDescriptor.ResultShape)
            {
                ExecutorId = RecordingApprovalExecutor.ExecutorId,
                ExecutorSettingsJson = string.Empty,
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
            });
    }

    private static WorkflowDefinition CreateCompletedDefinition(LlmCallComponent component)
    {
        return CreateDefinition(
            component,
            [
                CreateNode("start", WorkflowNodeKind.Start),
                CreateNode("marker", WorkflowNodeKind.LlmCall, component.Id),
                CreateNode("end", WorkflowNodeKind.End)
            ],
            [
                CreateEdge("start-marker", "start", "marker"),
                CreateEdge("marker-end", "marker", "end")
            ]);
    }

    private static WorkflowDefinition CreateDefinition(
        LlmCallComponent component,
        IReadOnlyList<WorkflowNode> nodes,
        IReadOnlyList<WorkflowEdge> edges)
    {
        _ = component;
        var now = DateTimeOffset.UtcNow;
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Native HITL workflow",
            "Exercises MAF checkpoint rehydration.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(new WorkflowNodeId("start"), nodes, edges),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            now,
            now);
    }

    private static WorkflowNode CreateNode(
        string id,
        WorkflowNodeKind kind,
        WorkflowComponentId? componentId = null,
        WorkflowValueShape? inputShape = null,
        WorkflowValueShape? resultShape = null)
    {
        return new WorkflowNode(
            new WorkflowNodeId(id),
            kind,
            id,
            [],
            new WorkflowNodeSettings(
                componentId,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: kind == WorkflowNodeKind.HumanInput
                    ? WorkflowExternalRequestKind.HumanInput
                    : null,
                Instructions: kind switch
                {
                    WorkflowNodeKind.HumanInput => "Provide the reviewed payload.",
                    WorkflowNodeKind.LlmCall => "Record one marker invocation.",
                    _ => string.Empty
                },
                InputShape: inputShape ?? WorkflowValueShape.Text,
                ResultShape: resultShape ?? WorkflowValueShape.Text));
    }

    private static WorkflowEdge CreateEdge(string id, string source, string target)
    {
        return new WorkflowEdge(
            new WorkflowEdgeId(id),
            new WorkflowNodeId(source),
            SourcePortId: null,
            new WorkflowNodeId(target),
            TargetPortId: null,
            WorkflowEdgeKind.Direct,
            ConditionExpression: string.Empty);
    }

    private static LlmCallComponent CreateComponent()
    {
        var now = DateTimeOffset.UtcNow;
        return new LlmCallComponent(
            WorkflowComponentId.New(),
            "Marker",
            ProviderProfileId: null,
            "test-model",
            WorkflowModality.Text,
            new WorkflowModelSettings(
                Temperature: null,
                MaxOutputTokens: null,
                RequireJsonOutput: false,
                ResponseFormatJsonSchema: string.Empty),
            "Record one marker invocation.",
            WorkflowValueShape.Text,
            WorkflowValueShape.Text,
            AgentPermissionsPolicy.Default,
            now,
            now);
    }

    private sealed class CountingLlmInvoker : IWorkflowLlmComponentInvoker
    {
        public int InvocationCount { get; private set; }

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowDefinition definition,
            WorkflowNode node,
            LlmCallComponent component,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InvocationCount++;
            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                node.Id,
                input.PayloadJson,
                component.ResultShape));
        }
    }

    private sealed class RecordingApprovalExecutor : IWorkflowExecutor
    {
        public static WorkflowExecutorId ExecutorId { get; } = new("test.native-approval");

        public static WorkflowExecutorDescriptor TestDescriptor { get; } = BuiltInWorkflowExecutorDescriptors.StorageFile with
        {
            Id = ExecutorId,
            Name = "Native approval test executor",
            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                WorkflowExecutorCapabilityFlags.WritesWorkspace,
                WorkflowExecutorApprovalRequirement.AlwaysRequired)
        };

        public WorkflowExecutorDescriptor Descriptor => TestDescriptor;

        public int InvocationCount { get; private set; }

        public string LastInputJson { get; private set; } = string.Empty;

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InvocationCount++;
            LastInputJson = input.PayloadJson;
            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                context.Node.Id,
                """{"executed":true}""",
                context.Descriptor.ResultShape));
        }
    }

    private sealed class ForwardingCheckpointStore(IWorkflowBackendCheckpointPayloadStore inner) :
        IWorkflowBackendCheckpointPayloadStore
    {
        public Task<WorkflowBackendCheckpointCreateResult> CreateAsync(
            WorkflowBackendCheckpointCreateRequest request,
            CancellationToken cancellationToken = default)
            => inner.CreateAsync(request, cancellationToken);

        public Task<WorkflowBackendCheckpointListResult> ListIndexAsync(
            WorkflowBackendSessionId sessionId,
            CancellationToken cancellationToken = default)
            => inner.ListIndexAsync(sessionId, cancellationToken);

        public Task<WorkflowBackendCheckpointReadResult> ReadAsync(
            WorkflowBackendCheckpointLink link,
            CancellationToken cancellationToken = default)
            => inner.ReadAsync(link, cancellationToken);
    }

    private sealed class CorruptReadCheckpointStore(IWorkflowBackendCheckpointPayloadStore inner) :
        IWorkflowBackendCheckpointPayloadStore
    {
        public Task<WorkflowBackendCheckpointCreateResult> CreateAsync(
            WorkflowBackendCheckpointCreateRequest request,
            CancellationToken cancellationToken = default)
            => inner.CreateAsync(request, cancellationToken);

        public Task<WorkflowBackendCheckpointListResult> ListIndexAsync(
            WorkflowBackendSessionId sessionId,
            CancellationToken cancellationToken = default)
            => inner.ListIndexAsync(sessionId, cancellationToken);

        public Task<WorkflowBackendCheckpointReadResult> ReadAsync(
            WorkflowBackendCheckpointLink link,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new WorkflowBackendCheckpointReadResult(
                WorkflowBackendCheckpointReadOutcome.PayloadCorrupt,
                Checkpoint: null));
        }
    }

    private sealed class ExactWorkflowCatalog(
        WorkflowDefinition definition,
        IReadOnlyList<LlmCallComponent>? components = null) :
        IWorkflowCatalogService,
        IWorkflowComponentLibraryService
    {
        public int ExactReadCount { get; private set; }

        public WorkflowVersionId? RequestedVersionId { get; private set; }

        public Task<IReadOnlyList<WorkflowCatalogItem>> ListDefinitionsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkflowCatalogItem>>([]);

        public Task<WorkflowDefinitionDetail?> GetDefinitionAsync(
            WorkflowId workflowId,
            WorkflowVersionId? versionId = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestedVersionId = versionId;
            ExactReadCount++;
            return Task.FromResult<WorkflowDefinitionDetail?>(
                workflowId == definition.Id && versionId == definition.VersionId
                    ? new WorkflowDefinitionDetail(definition, WorkflowValidationResult.Success)
                    : null);
        }

        public Task<WorkflowDefinitionDetail?> GetLatestDefinitionByStatusAsync(
            WorkflowId workflowId,
            WorkflowLifecycleStatus status,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinition> SaveDefinitionAsync(
            WorkflowDefinitionSaveRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinition> ChangeDefinitionStatusAsync(
            WorkflowDefinitionStatusChangeRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinitionExportEnvelope?> ExportDefinitionAsync(
            WorkflowId workflowId,
            WorkflowVersionId? versionId = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinition> ImportDefinitionAsync(
            WorkflowDefinitionImportRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteDefinitionAsync(
            WorkflowId workflowId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowValidationResult> ValidateDefinitionAsync(
            WorkflowDefinition workflowDefinition,
            CancellationToken cancellationToken = default)
            => Task.FromResult(WorkflowValidationResult.Success);

        public Task<IReadOnlyList<WorkflowProviderOption>> ListProviderOptionsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkflowProviderOption>>([]);

        public Task<IReadOnlyList<LlmCallComponent>> ListComponentsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(components ?? []);

        public Task<LlmCallComponent?> GetComponentAsync(
            WorkflowComponentId componentId,
            CancellationToken cancellationToken = default)
            => Task.FromResult((components ?? []).SingleOrDefault(component => component.Id == componentId));

        public Task<LlmCallComponent> SaveComponentAsync(
            LlmCallComponentSaveRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteComponentAsync(
            WorkflowComponentId componentId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed record ApprovalFixture(
        WorkflowDefinition Definition,
        WorkflowRunSnapshot Run,
        WorkflowExternalRequestRecord Request,
        InMemoryWorkflowRunStore RunStore,
        InMemoryWorkflowBackendCheckpointPayloadStore CheckpointState);

}
