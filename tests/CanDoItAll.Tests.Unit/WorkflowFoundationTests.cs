using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI.Workflows;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowFoundationTests
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    [Fact]
    public void WorkflowIdRejectsEmptyValue()
    {
        Assert.Throws<ArgumentException>(() => new WorkflowId(Guid.Empty));
    }

    [Fact]
    public void ValidatorRequiresReferencedLlmComponent()
    {
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("llm", WorkflowNodeKind.LlmCall),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-to-llm", "start", "llm"),
            CreateEdge("llm-to-end", "llm", "end")
        ]);

        var result = new WorkflowDefinitionValidator().Validate(definition, []);

        Assert.Contains(result.Issues, issue => issue.Code == WorkflowValidationIssueCode.InvalidComponentReference);
    }

    [Fact]
    public void ValidatorAcceptsBasicLlmWorkflow()
    {
        var component = CreateComponent();
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("llm", WorkflowNodeKind.LlmCall, component.Id),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-to-llm", "start", "llm"),
            CreateEdge("llm-to-end", "llm", "end")
        ]);

        var result = new WorkflowDefinitionValidator().Validate(definition, [component]);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void MafCompilerBuildsWorkflowWithoutLeakingMafTypesThroughCoreContracts()
    {
        var component = CreateComponent();
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("llm", WorkflowNodeKind.LlmCall, component.Id),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-to-llm", "start", "llm"),
            CreateEdge("llm-to-end", "llm", "end")
        ]);

        var compiler = new MafWorkflowCompiler(new WorkflowDefinitionValidator());
        var result = compiler.Compile(definition, [component]);

        Assert.True(result.Compilation.Succeeded);
        Assert.NotNull(result.Workflow);
        Assert.Equal("Sample workflow", result.Workflow.Name);
    }

    [Fact]
    public void MafStatusMapperMapsPendingRequestsToWaitingForInput()
    {
        var state = MafWorkflowStatusMapper.MapRunStatus(RunStatus.PendingRequests);

        Assert.Equal(WorkflowRunState.WaitingForInput, state);
    }

    [Fact]
    public void RuntimeBackendCatalogMarksUnregisteredDurableBackendsAsPlanned()
    {
        var catalog = new WorkflowRuntimeBackendCatalog();

        var inProcess = catalog.GetRequiredBackend(WorkflowRuntimeBackendKind.InProcess);
        var durableTask = catalog.GetRequiredBackend(WorkflowRuntimeBackendKind.DurableTask);
        var azureFunctions = catalog.GetRequiredBackend(WorkflowRuntimeBackendKind.AzureFunctions);

        Assert.Equal(WorkflowRuntimeBackendAvailabilityKind.Registered, inProcess.Availability);
        Assert.True(inProcess.IsRegistered);
        Assert.True(inProcess.IsRunnable);
        Assert.Equal(WorkflowRuntimeBackendAvailabilityKind.Planned, durableTask.Availability);
        Assert.False(durableTask.IsRegistered);
        Assert.False(durableTask.IsRunnable);
        Assert.True(durableTask.IsDurable);
        Assert.True(durableTask.SupportsDashboardObservability);
        Assert.Contains("not registered", durableTask.AvailabilityReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(WorkflowRuntimeBackendAvailabilityKind.Planned, azureFunctions.Availability);
        Assert.False(azureFunctions.IsRunnable);
    }

    [Fact]
    public void WorkflowEdgeDefaultsMissingRoutingMetadataForLegacyJson()
    {
        const string legacyEdgeJson = """
            {
              "id": "legacy-edge",
              "sourceNodeId": "start",
              "sourcePortId": null,
              "targetNodeId": "end",
              "targetPortId": null,
              "kind": "Conditional",
              "conditionExpression": "$.approved == true"
            }
            """;

        var edge = JsonSerializer.Deserialize<WorkflowEdge>(legacyEdgeJson, SerializerOptions);

        Assert.NotNull(edge);
        Assert.Equal(WorkflowRouteKind.Always, edge.Routing.Kind);
        Assert.Equal("$.approved == true", edge.ConditionExpression);
    }

    [Fact]
    public void WorkflowEdgeRoutingRoundTripsTypedPredicateMetadata()
    {
        var edge = CreateEdge(
            "start-approved",
            "start",
            "approved",
            WorkflowEdgeKind.Conditional,
            WorkflowEdgeRouting.Predicate(
                "$.approval.status",
                WorkflowRouteOperator.Equals,
                "\"approved\"",
                WorkflowRouteValueKind.String,
                label: "Approved path"));

        var json = JsonSerializer.Serialize(edge, SerializerOptions);
        var roundTripped = JsonSerializer.Deserialize<WorkflowEdge>(json, SerializerOptions);

        Assert.NotNull(roundTripped);
        Assert.Equal(WorkflowRouteKind.Predicate, roundTripped.Routing.Kind);
        Assert.Equal("$.approval.status", roundTripped.Routing.JsonPath);
        Assert.Equal("\"approved\"", roundTripped.Routing.ExpectedValueJson);
        Assert.Contains("routing", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatorRejectsInvalidRouteExpectedJson()
    {
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("approved", WorkflowNodeKind.End)
        ], [
            CreateEdge(
                "start-approved",
                "start",
                "approved",
                WorkflowEdgeKind.Conditional,
                WorkflowEdgeRouting.Predicate(
                    "$.approval.status",
                    WorkflowRouteOperator.Equals,
                    "\"approved",
                    WorkflowRouteValueKind.String))
        ]);

        var result = new WorkflowDefinitionValidator().Validate(definition, []);

        Assert.Contains(result.Issues, issue =>
            issue.Code == WorkflowValidationIssueCode.InvalidRouteDefinition &&
            issue.EdgeId == new WorkflowEdgeId("start-approved"));
    }

    [Fact]
    public void ValidatorRejectsReservedArtlRouteLanguageUntilCompilerExists()
    {
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("approved", WorkflowNodeKind.End)
        ], [
            CreateEdge(
                "start-approved",
                "start",
                "approved",
                WorkflowEdgeKind.Conditional,
                WorkflowEdgeRouting.Predicate(
                    "$.approval.status",
                    WorkflowRouteOperator.Equals,
                    "\"approved\"",
                    WorkflowRouteValueKind.String) with
                {
                    RoutingLanguage = WorkflowRoutingLanguages.ArtlV1
                })
        ]);

        var result = new WorkflowDefinitionValidator().Validate(definition, []);

        Assert.Contains(result.Issues, issue =>
            issue.Code == WorkflowValidationIssueCode.InvalidRouteDefinition &&
            issue.Message.Contains("artl-v1", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidatorRejectsDuplicateSwitchDefaultRoutes()
    {
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("manual", WorkflowNodeKind.End),
            CreateNode("fallback", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-manual", "start", "manual", WorkflowEdgeKind.Conditional, WorkflowEdgeRouting.SwitchDefault("Manual")),
            CreateEdge("start-fallback", "start", "fallback", WorkflowEdgeKind.Conditional, WorkflowEdgeRouting.SwitchDefault("Fallback"))
        ]);

        var result = new WorkflowDefinitionValidator().Validate(definition, []);

        Assert.Contains(result.Issues, issue =>
            issue.Code == WorkflowValidationIssueCode.InvalidRouteDefinition &&
            issue.Message.Contains("more than one switch default", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidatorRejectsDuplicateFanOutTargetIndices()
    {
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("email", WorkflowNodeKind.End),
            CreateNode("slack", WorkflowNodeKind.End)
        ], [
            CreateEdge(
                "start-email",
                "start",
                "email",
                WorkflowEdgeKind.FanOut,
                WorkflowEdgeRouting.FanOutSelector(
                    "$.channels",
                    WorkflowRouteOperator.Contains,
                    "\"email\"",
                    WorkflowRouteValueKind.String,
                    targetIndex: 0)),
            CreateEdge(
                "start-slack",
                "start",
                "slack",
                WorkflowEdgeKind.FanOut,
                WorkflowEdgeRouting.FanOutSelector(
                    "$.channels",
                    WorkflowRouteOperator.Contains,
                    "\"slack\"",
                    WorkflowRouteValueKind.String,
                    targetIndex: 0))
        ]);

        var result = new WorkflowDefinitionValidator().Validate(definition, []);

        Assert.Contains(result.Issues, issue =>
            issue.Code == WorkflowValidationIssueCode.InvalidRouteDefinition &&
            issue.Message.Contains("duplicate fan-out target index", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RuntimeManagerCompletesInProcessWorkflow()
    {
        var component = CreateComponent();
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("llm", WorkflowNodeKind.LlmCall, component.Id),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-to-llm", "start", "llm"),
            CreateEdge("llm-to-end", "llm", "end")
        ]) with
        {
            RuntimePolicy = new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)
        };
        var store = new InMemoryWorkflowRunStore();
        var manager = new WorkflowRuntimeManager(
            [
                new MafInProcessWorkflowExecutionBackend(
                    new MafWorkflowCompiler(
                        new WorkflowDefinitionValidator(),
                        llmComponentInvoker: new PassthroughLlmComponentInvoker()),
                    [component])
            ],
            store);

        var run = await manager.StartAsync(
            definition,
            new WorkflowRunStartRequest(
                definition.Id,
                definition.VersionId,
                "{\"prompt\":\"hello\"}",
                WorkflowRuntimeBackendKind.InProcess,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null));

        Assert.Equal(WorkflowRunState.Completed, run.State);
        Assert.NotEmpty(await manager.ListEventsAsync(run.RunId));
    }

    [Fact]
    public async Task InMemoryWorkflowCheckpointStore_saves_and_lists_metadata()
    {
        var store = new InMemoryWorkflowRunStore();
        var now = DateTimeOffset.UtcNow;
        var runId = WorkflowRunId.New();
        var checkpoint = new WorkflowCheckpointRecord(
            WorkflowCheckpointId.New(),
            runId,
            WorkflowId.New(),
            WorkflowVersionId.New(),
            WorkflowRuntimeBackendKind.InProcess,
            WorkflowCheckpointKind.Completed,
            WorkflowCheckpointTrustBoundary.MetadataOnly,
            WorkflowResumeAvailability.NotSupported,
            NodeId: null,
            ExternalRequestId: null,
            BackendCheckpointId: string.Empty,
            PayloadReference: "runtime://metadata-only",
            PayloadHash: string.Empty,
            Summary: "Metadata checkpoint captured.",
            ResumeUnavailableReason: "Resume is not available for metadata-only workflow checkpoints.",
            CreatedAtUtc: now,
            ResumedAtUtc: null);

        await store.SaveCheckpointAsync(checkpoint);

        var saved = Assert.Single(await store.ListCheckpointsAsync(runId));
        Assert.Equal(checkpoint.Id, saved.Id);
        Assert.Equal(WorkflowCheckpointTrustBoundary.MetadataOnly, saved.TrustBoundary);
        Assert.Equal(WorkflowResumeAvailability.NotSupported, saved.ResumeAvailability);
        Assert.NotEmpty(saved.ResumeUnavailableReason);
    }

    [Fact]
    public async Task RuntimeManagerCapturesCompletedCheckpointMetadata()
    {
        var component = CreateComponent();
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("llm", WorkflowNodeKind.LlmCall, component.Id),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-to-llm", "start", "llm"),
            CreateEdge("llm-to-end", "llm", "end")
        ]) with
        {
            RuntimePolicy = new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)
        };
        var store = new InMemoryWorkflowRunStore();
        var manager = new WorkflowRuntimeManager(
            [
                new MafInProcessWorkflowExecutionBackend(
                    new MafWorkflowCompiler(
                        new WorkflowDefinitionValidator(),
                        llmComponentInvoker: new PassthroughLlmComponentInvoker()),
                    [component])
            ],
            store);

        var run = await manager.StartAsync(
            definition,
            new WorkflowRunStartRequest(
                definition.Id,
                definition.VersionId,
                "{\"prompt\":\"hello\"}",
                WorkflowRuntimeBackendKind.InProcess,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null));

        var checkpoint = Assert.Single(await store.ListCheckpointsAsync(run.RunId));
        Assert.Equal(WorkflowCheckpointKind.Completed, checkpoint.Kind);
        Assert.Equal(WorkflowCheckpointTrustBoundary.MetadataOnly, checkpoint.TrustBoundary);
        Assert.Equal(WorkflowResumeAvailability.NotSupported, checkpoint.ResumeAvailability);
        Assert.DoesNotContain("hello", checkpoint.PayloadReference, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("durable", checkpoint.ResumeUnavailableReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RuntimeManager_applies_payload_policy_to_started_input_and_node_output_artifacts()
    {
        var component = CreateComponent();
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("llm", WorkflowNodeKind.LlmCall, component.Id),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-to-llm", "start", "llm"),
            CreateEdge("llm-to-end", "llm", "end")
        ]) with
        {
            RuntimePolicy = new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)
        };
        var settings = WorkflowSettings.Default with
        {
            ArtifactPolicy = WorkflowSettings.Default.ArtifactPolicy with
            {
                MaxInlinePayloadCharacters = 160
            }
        };
        var payloadPolicy = new WorkflowPayloadPolicyService(new StaticWorkflowSettingsService(settings));
        var store = new InMemoryWorkflowRunStore();
        var manager = new WorkflowRuntimeManager(
            [
                new MafInProcessWorkflowExecutionBackend(
                    new MafWorkflowCompiler(
                        new WorkflowDefinitionValidator(),
                        llmComponentInvoker: new PassthroughLlmComponentInvoker()),
                    [component],
                    payloadPolicyService: payloadPolicy)
            ],
            store);
        var inputJson = $$"""{"token":"raw-token-value","prompt":"{{new string('x', 512)}}"}""";

        var run = await manager.StartAsync(
            definition,
            new WorkflowRunStartRequest(
                definition.Id,
                definition.VersionId,
                inputJson,
                WorkflowRuntimeBackendKind.InProcess,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null));
        var events = await manager.ListEventsAsync(run.RunId);
        var artifacts = await store.ListArtifactsAsync(run.RunId);
        var started = Assert.Single(events, workflowEvent => workflowEvent.Kind == WorkflowEventKind.Started);
        var startedPayload = JsonSerializer.Deserialize<WorkflowEventPayloadEnvelope>(started.PayloadJson, SerializerOptions)!;
        var completed = Assert.Single(events, workflowEvent =>
            workflowEvent.Kind == WorkflowEventKind.ExecutorCompleted &&
            workflowEvent.NodeId == new WorkflowNodeId("llm"));
        var completedPayload = JsonSerializer.Deserialize<WorkflowEventPayloadEnvelope>(completed.PayloadJson, SerializerOptions)!;

        Assert.Equal(WorkflowRunState.Completed, run.State);
        Assert.True(startedPayload.InlineTruncated);
        Assert.True(startedPayload.InlineJson.Length <= settings.ArtifactPolicy.MaxInlinePayloadCharacters);
        Assert.NotEmpty(startedPayload.Reference);
        Assert.DoesNotContain("raw-token-value", startedPayload.InlineJson, StringComparison.Ordinal);
        Assert.True(completedPayload.InlineTruncated);
        Assert.NotEmpty(completedPayload.Reference);
        Assert.DoesNotContain("raw-token-value", completedPayload.InlineJson, StringComparison.Ordinal);
        Assert.Contains(artifacts, artifact =>
            artifact.Kind == WorkflowArtifactKind.Json &&
            artifact.Name.Contains("input", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(artifacts, artifact =>
            artifact.Kind == WorkflowArtifactKind.Json &&
            artifact.NodeId == new WorkflowNodeId("llm"));
    }

    [Fact]
    public async Task WorkflowPayloadPolicyService_writes_retrievable_redacted_artifact_content()
    {
        var settings = WorkflowSettings.Default with
        {
            ArtifactPolicy = WorkflowSettings.Default.ArtifactPolicy with
            {
                MaxInlinePayloadCharacters = 32
            }
        };
        var contentStore = new InMemoryWorkflowArtifactContentStore();
        var payloadPolicy = new WorkflowPayloadPolicyService(
            new StaticWorkflowSettingsService(settings),
            contentStore);

        var result = await payloadPolicy.ApplyAsync(new WorkflowPayloadPolicyRequest(
            WorkflowRunId.New(),
            WorkflowPayloadPolicyScope.RunInput,
            "{\"token\":\"raw-token-value\",\"message\":\"" + new string('x', 256) + "\"}",
            WorkflowArtifactKind.Json,
            "workflow-input.json",
            "application/json",
            DateTimeOffset.UtcNow)
        {
            CaptureArtifact = true
        });

        Assert.NotNull(result.Artifact);
        var content = await contentStore.ReadContentAsync(result.Artifact);
        Assert.NotNull(content);
        Assert.Contains("[REDACTED]", content.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-token-value", content.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InMemoryWorkflowArtifactContentStore_returns_null_for_missing_content()
    {
        var artifact = new WorkflowArtifactRecord(
            WorkflowArtifactId.New(),
            WorkflowRunId.New(),
            WorkflowArtifactKind.Text,
            NodeId: null,
            "missing.txt",
            "text/plain",
            "workflow-runs/missing/payloads/missing.txt",
            "Missing content test.",
            DateTimeOffset.UtcNow);
        var contentStore = new InMemoryWorkflowArtifactContentStore();

        var content = await contentStore.ReadContentAsync(artifact);

        Assert.Null(content);
    }

    [Fact]
    public async Task RuntimeManager_does_not_wait_for_unreached_human_input_route()
    {
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("human", WorkflowNodeKind.HumanInput),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge(
                "start-to-human",
                "start",
                "human",
                WorkflowEdgeKind.Conditional,
                WorkflowEdgeRouting.SwitchCase(
                    "$.route",
                    "\"manual\"",
                    WorkflowRouteValueKind.String,
                    "Manual route")),
            CreateEdge(
                "start-to-end",
                "start",
                "end",
                WorkflowEdgeKind.Conditional,
                WorkflowEdgeRouting.SwitchDefault("Automatic route")),
            CreateEdge("human-to-end", "human", "end")
        ]) with
        {
            RuntimePolicy = new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)
        };
        var store = new InMemoryWorkflowRunStore();
        var manager = new WorkflowRuntimeManager(
            [
                new MafInProcessWorkflowExecutionBackend(
                    new MafWorkflowCompiler(new WorkflowDefinitionValidator()),
                    [])
            ],
            store);

        var run = await manager.StartAsync(
            definition,
            new WorkflowRunStartRequest(
                definition.Id,
                definition.VersionId,
                "{\"route\":\"automatic\"}",
                WorkflowRuntimeBackendKind.InProcess,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null));
        var pending = await store.ListPendingExternalRequestsAsync(run.RunId);

        Assert.Equal(WorkflowRunState.Completed, run.State);
        Assert.Empty(pending);
    }

    [Fact]
    public async Task RuntimeManager_creates_human_input_request_only_after_route_reaches_node()
    {
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("human", WorkflowNodeKind.HumanInput),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge(
                "start-to-human",
                "start",
                "human",
                WorkflowEdgeKind.Conditional,
                WorkflowEdgeRouting.SwitchCase(
                    "$.route",
                    "\"manual\"",
                    WorkflowRouteValueKind.String,
                    "Manual route")),
            CreateEdge(
                "start-to-end",
                "start",
                "end",
                WorkflowEdgeKind.Conditional,
                WorkflowEdgeRouting.SwitchDefault("Automatic route")),
            CreateEdge("human-to-end", "human", "end")
        ]) with
        {
            RuntimePolicy = new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)
        };
        var store = new InMemoryWorkflowRunStore();
        var manager = new WorkflowRuntimeManager(
            [
                new MafInProcessWorkflowExecutionBackend(
                    new MafWorkflowCompiler(new WorkflowDefinitionValidator()),
                    [])
            ],
            store);

        var run = await manager.StartAsync(
            definition,
            new WorkflowRunStartRequest(
                definition.Id,
                definition.VersionId,
                "{\"route\":\"manual\"}",
                WorkflowRuntimeBackendKind.InProcess,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null));
        var pending = await store.ListPendingExternalRequestsAsync(run.RunId);
        var events = await manager.ListEventsAsync(run.RunId);

        Assert.Equal(WorkflowRunState.WaitingForInput, run.State);
        var request = Assert.Single(pending);
        Assert.Equal(new WorkflowNodeId("human"), request.NodeId);
        var checkpoint = Assert.Single(await store.ListCheckpointsAsync(run.RunId));
        Assert.Equal(WorkflowCheckpointKind.WaitingForInput, checkpoint.Kind);
        Assert.Equal(request.Id, checkpoint.ExternalRequestId);
        Assert.Equal(WorkflowResumeAvailability.NotSupported, checkpoint.ResumeAvailability);
        Assert.Contains(events, workflowEvent =>
            workflowEvent.Kind == WorkflowEventKind.ExecutorInvoked &&
            workflowEvent.NodeId == new WorkflowNodeId("start"));
    }

    [Fact]
    public async Task RuntimeManagerCreatesAndRespondsToHumanInputRequest()
    {
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("human", WorkflowNodeKind.HumanInput),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-to-human", "start", "human"),
            CreateEdge("human-to-end", "human", "end")
        ]) with
        {
            RuntimePolicy = new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)
        };
        var store = new InMemoryWorkflowRunStore();
        var manager = new WorkflowRuntimeManager(
            [
                new MafInProcessWorkflowExecutionBackend(
                    new MafWorkflowCompiler(new WorkflowDefinitionValidator()),
                    [])
            ],
            store);

        var run = await manager.StartAsync(
            definition,
            new WorkflowRunStartRequest(
                definition.Id,
                definition.VersionId,
                "{\"question\":\"approve?\"}",
                WorkflowRuntimeBackendKind.InProcess,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null));
        var pending = await store.ListPendingExternalRequestsAsync(run.RunId);
        var completed = await manager.RespondToExternalRequestAsync(pending[0].Id, "{\"approved\":true}");

        Assert.Equal(WorkflowRunState.WaitingForInput, run.State);
        Assert.Single(pending);
        Assert.Equal(WorkflowRunState.Completed, completed.State);
    }

    [Fact]
    public async Task RuntimeManagerCompletesApprovalRequestWhenApproved()
    {
        var store = new InMemoryWorkflowRunStore();
        var manager = new WorkflowRuntimeManager([], store);
        var request = await SaveWaitingApprovalRequestAsync(store);

        var completed = await manager.RespondToExternalRequestAsync(
            request.Id,
            "{\"approved\":true,\"message\":\"Operator approved.\"}");
        var pending = await store.ListPendingExternalRequestsAsync(request.RunId);
        var events = await manager.ListEventsAsync(request.RunId);

        Assert.Equal(WorkflowRunState.Completed, completed.State);
        Assert.Empty(pending);
        Assert.Contains(events, workflowEvent =>
            workflowEvent.Kind == WorkflowEventKind.Completed &&
            workflowEvent.NodeId == request.NodeId);
    }

    [Fact]
    public async Task RuntimeManagerFailsApprovalRequestWhenDenied()
    {
        var store = new InMemoryWorkflowRunStore();
        var manager = new WorkflowRuntimeManager([], store);
        var request = await SaveWaitingApprovalRequestAsync(store);

        var failed = await manager.RespondToExternalRequestAsync(
            request.Id,
            "{\"approved\":false,\"message\":\"Denied token=raw-token-value.\"}");
        var events = await manager.ListEventsAsync(request.RunId);

        Assert.Equal(WorkflowRunState.Failed, failed.State);
        Assert.Contains("denied", failed.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw-token-value", failed.Summary, StringComparison.Ordinal);
        Assert.Contains(events, workflowEvent =>
            workflowEvent.Kind == WorkflowEventKind.Error &&
            workflowEvent.NodeId == request.NodeId);
    }

    [Fact]
    public async Task RuntimeManagerRejectsMalformedApprovalResponse()
    {
        var store = new InMemoryWorkflowRunStore();
        var manager = new WorkflowRuntimeManager([], store);
        var request = await SaveWaitingApprovalRequestAsync(store);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            manager.RespondToExternalRequestAsync(request.Id, "{\"message\":\"missing approval\"}"));
    }

    [Fact]
    public async Task RuntimeManagerRejectsUnregisteredBackendInsteadOfFallingBack()
    {
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-to-end", "start", "end")
        ]);
        var manager = new WorkflowRuntimeManager([], new InMemoryWorkflowRunStore());

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StartAsync(
            definition,
            new WorkflowRunStartRequest(
                definition.Id,
                definition.VersionId,
                "{}",
                WorkflowRuntimeBackendKind.DurableTask,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null)));
    }

    [Fact]
    public async Task RuntimeManagerCreatesDistinctParallelRuns()
    {
        var component = CreateComponent();
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("llm", WorkflowNodeKind.LlmCall, component.Id),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-to-llm", "start", "llm"),
            CreateEdge("llm-to-end", "llm", "end")
        ]) with
        {
            RuntimePolicy = new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)
        };
        var manager = new WorkflowRuntimeManager(
            [
                new MafInProcessWorkflowExecutionBackend(
                    new MafWorkflowCompiler(
                        new WorkflowDefinitionValidator(),
                        llmComponentInvoker: new PassthroughLlmComponentInvoker()),
                    [component])
            ],
            new InMemoryWorkflowRunStore());
        var request = new WorkflowRunStartRequest(
            definition.Id,
            definition.VersionId,
            "{}",
            WorkflowRuntimeBackendKind.InProcess,
            SourceProcessRunId: null,
            SourceProcessAssignmentId: null);

        var runs = await Task.WhenAll(
            manager.StartAsync(definition, request),
            manager.StartAsync(definition, request));

        Assert.NotEqual(runs[0].RunId, runs[1].RunId);
        Assert.All(runs, run => Assert.Equal(WorkflowRunState.Completed, run.State));
    }

    private static WorkflowDefinition CreateDefinition(
        IReadOnlyList<WorkflowNode> nodes,
        IReadOnlyList<WorkflowEdge> edges)
    {
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Sample workflow",
            "Sample workflow for tests.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(new WorkflowNodeId("start"), nodes, edges),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.DurableTask,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: true,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static async Task<WorkflowExternalRequestRecord> SaveWaitingApprovalRequestAsync(InMemoryWorkflowRunStore store)
    {
        var now = DateTimeOffset.UtcNow;
        var runId = WorkflowRunId.New();
        var run = new WorkflowRunSnapshot(
            runId,
            WorkflowId.New(),
            WorkflowVersionId.New(),
            WorkflowRunState.WaitingForInput,
            WorkflowRuntimeBackendKind.InProcess,
            BackendRunId: runId.ToString(),
            Summary: "Workflow is waiting for approval.",
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
        var request = new WorkflowExternalRequestRecord(
            WorkflowExternalRequestId.New(),
            runId,
            WorkflowExternalRequestKind.Approval,
            new WorkflowNodeId("approval-node"),
            EventName: "approval:sample.executor",
            RequestJson: "{}",
            ResponseJson: string.Empty,
            CreatedAtUtc: now,
            RespondedAtUtc: null);

        await store.SaveRunAsync(run);
        await store.SaveExternalRequestAsync(request);
        return request;
    }

    private static WorkflowNode CreateNode(
        string id,
        WorkflowNodeKind kind,
        WorkflowComponentId? componentId = null)
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
                ExternalRequestKind: kind == WorkflowNodeKind.HumanInput ? WorkflowExternalRequestKind.HumanInput : null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));
    }

    private static WorkflowEdge CreateEdge(
        string id,
        string source,
        string target,
        WorkflowEdgeKind kind = WorkflowEdgeKind.Direct,
        WorkflowEdgeRouting? routing = null)
    {
        return new WorkflowEdge(
            new WorkflowEdgeId(id),
            new WorkflowNodeId(source),
            SourcePortId: null,
            new WorkflowNodeId(target),
            TargetPortId: null,
            kind,
            ConditionExpression: string.Empty)
        {
            Routing = routing ?? WorkflowEdgeRouting.Always
        };
    }

    private static LlmCallComponent CreateComponent()
    {
        return new LlmCallComponent(
            WorkflowComponentId.New(),
            "Summarize",
            ProviderProfileId: null,
            "gpt-5.4",
            WorkflowModality.Text,
            new WorkflowModelSettings(
                Temperature: 0.2,
                MaxOutputTokens: 800,
                RequireJsonOutput: false,
                ResponseFormatJsonSchema: string.Empty),
            "Summarize the input.",
            WorkflowValueShape.Text,
            WorkflowValueShape.Text,
            AgentPermissionsPolicy.Default,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private sealed class PassthroughLlmComponentInvoker : IWorkflowLlmComponentInvoker
    {
        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowDefinition definition,
            WorkflowNode node,
            LlmCallComponent component,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                node.Id,
                input.PayloadJson,
                component.ResultShape));
        }
    }

    private sealed class StaticWorkflowSettingsService(WorkflowSettings settings) : IWorkflowSettingsService
    {
        public Task<WorkflowSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(settings);
        }

        public Task<WorkflowSettings> SaveSettingsAsync(
            WorkflowSettings updatedSettings,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(updatedSettings);
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
