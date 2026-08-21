using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Http;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowApiIntegrationTests
{
    [Fact]
    public async Task Workflow_api_saves_validates_and_runs_workflow()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var component = await SaveComponentAsync(host);
        var saveResponse = await host.Client.PostAsJsonAsync(
            "/api/workflows/definitions",
            CreateDefinitionSaveRequest(component.Id, graph: CreatePassthroughGraph()));
        var saveBody = await saveResponse.Content.ReadAsStringAsync();
        Assert.True(saveResponse.IsSuccessStatusCode, saveBody);
        var definition = JsonSerializer.Deserialize<WorkflowDefinition>(saveBody, JsonOptions())!;

        var listResponse = await host.Client.GetAsync("/api/workflows/definitions");
        var listBody = await listResponse.Content.ReadAsStringAsync();
        Assert.True(listResponse.IsSuccessStatusCode, listBody);
        var definitions = JsonSerializer.Deserialize<IReadOnlyList<WorkflowCatalogItem>>(listBody, JsonOptions())!;

        var validationResponse = await host.Client.PostAsync($"/api/workflows/definitions/{definition.Id.Value:D}/validate", content: null);
        var validationBody = await validationResponse.Content.ReadAsStringAsync();
        Assert.True(validationResponse.IsSuccessStatusCode, validationBody);
        var validation = JsonSerializer.Deserialize<WorkflowValidationResult>(validationBody, JsonOptions())!;

        var testRunResponse = await host.Client.PostAsJsonAsync(
            "/api/workflows/test-runs",
            new WorkflowTestRunRequest(
                definition.Id,
                definition.VersionId,
                DraftDefinition: null,
                "{\"prompt\":\"hello\"}",
                WorkflowRuntimeBackendKind.InProcess,
                ValidateOnly: false));
        var testRunBody = await testRunResponse.Content.ReadAsStringAsync();
        Assert.True(testRunResponse.IsSuccessStatusCode, testRunBody);
        var testRun = JsonSerializer.Deserialize<WorkflowTestRunResult>(testRunBody, JsonOptions())!;

        Assert.Contains(definitions, item => item.Id == definition.Id);
        Assert.True(validation.Succeeded);
        Assert.True(testRun.Succeeded, testRun.ErrorMessage);
        Assert.NotNull(testRun.Run);
        Assert.Equal(WorkflowRunState.Completed, testRun.Run.State);
        Assert.NotEmpty(testRun.Events);
    }

    [Fact]
    public async Task Workflow_api_exports_imports_and_changes_definition_lifecycle()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var saveResponse = await host.Client.PostAsJsonAsync(
            "/api/workflows/definitions",
            CreateDefinitionSaveRequest(componentId: WorkflowComponentId.New(), graph: CreatePassthroughGraph()));
        var saveBody = await saveResponse.Content.ReadAsStringAsync();
        Assert.True(saveResponse.IsSuccessStatusCode, saveBody);
        var definition = JsonSerializer.Deserialize<WorkflowDefinition>(saveBody, JsonOptions())!;

        var publishResponse = await host.Client.PostAsync(
            $"/api/workflows/definitions/{definition.Id.Value:D}/publish?expectedVersionId={definition.VersionId.Value:D}",
            content: null);
        var publishBody = await publishResponse.Content.ReadAsStringAsync();
        Assert.True(publishResponse.IsSuccessStatusCode, publishBody);
        var published = JsonSerializer.Deserialize<WorkflowDefinition>(publishBody, JsonOptions())!;

        var suspendResponse = await host.Client.PostAsync(
            $"/api/workflows/definitions/{published.Id.Value:D}/suspend?expectedVersionId={published.VersionId.Value:D}",
            content: null);
        var suspendBody = await suspendResponse.Content.ReadAsStringAsync();
        Assert.True(suspendResponse.IsSuccessStatusCode, suspendBody);
        var suspended = JsonSerializer.Deserialize<WorkflowDefinition>(suspendBody, JsonOptions())!;

        var exportResponse = await host.Client.GetAsync($"/api/workflows/definitions/{suspended.Id.Value:D}/export");
        var exportBody = await exportResponse.Content.ReadAsStringAsync();
        Assert.True(exportResponse.IsSuccessStatusCode, exportBody);
        var envelope = JsonSerializer.Deserialize<WorkflowDefinitionExportEnvelope>(exportBody, JsonOptions())!;

        var importResponse = await host.Client.PostAsJsonAsync(
            "/api/workflows/definitions/import",
            new WorkflowDefinitionImportRequest(
                envelope,
                Name: "Imported API workflow",
                WorkflowLifecycleStatus.Draft,
                PreserveWorkflowId: false));
        var importBody = await importResponse.Content.ReadAsStringAsync();
        Assert.True(importResponse.IsSuccessStatusCode, importBody);
        var imported = JsonSerializer.Deserialize<WorkflowDefinition>(importBody, JsonOptions())!;

        Assert.Equal(WorkflowLifecycleStatus.Active, published.Status);
        Assert.Equal(definition.Id, published.Id);
        Assert.NotEqual(definition.VersionId, published.VersionId);
        Assert.Equal(WorkflowLifecycleStatus.Suspended, suspended.Status);
        Assert.Equal(WorkflowDefinitionExchangeFormats.Current, envelope.SourceFormat);
        Assert.True(envelope.Validation.Succeeded);
        Assert.NotEqual(suspended.Id, imported.Id);
        Assert.Equal("Imported API workflow", imported.Name);
        Assert.Equal(WorkflowLifecycleStatus.Draft, imported.Status);
        Assert.Equal(suspended.Graph.Nodes.Count, imported.Graph.Nodes.Count);
    }

    [Fact]
    public async Task Workflow_api_rejects_invalid_definition_on_save()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var saveResponse = await host.Client.PostAsJsonAsync(
            "/api/workflows/definitions",
            CreateDefinitionSaveRequest(WorkflowComponentId.New()));
        var saveBody = await saveResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, saveResponse.StatusCode);
        Assert.Contains("Workflow definition save failed validation", saveBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("prepared LLM Call Component", saveBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Workflow_api_round_trips_typed_route_metadata()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var saveResponse = await host.Client.PostAsJsonAsync(
            "/api/workflows/definitions",
            new WorkflowDefinitionSaveRequest(
                Id: null,
                ExpectedVersionId: null,
                Name: "Routing API workflow",
                Description: "Workflow route metadata API proof.",
                Status: WorkflowLifecycleStatus.Draft,
                Graph: CreateRoutingGraph(),
                RuntimePolicy: new WorkflowRuntimePolicy(
                    WorkflowRuntimeBackendKind.InProcess,
                    AllowInProcessPreviewRuns: true,
                    RequireDurableProductionRuns: false,
                    ExposeAzureFunctionsStatusEndpoint: false,
                    ExposeAzureFunctionsMcpTool: false)));
        var saveBody = await saveResponse.Content.ReadAsStringAsync();
        Assert.True(saveResponse.IsSuccessStatusCode, saveBody);
        var definition = JsonSerializer.Deserialize<WorkflowDefinition>(saveBody, JsonOptions())!;

        var detailResponse = await host.Client.GetAsync($"/api/workflows/definitions/{definition.Id.Value:D}");
        var detailBody = await detailResponse.Content.ReadAsStringAsync();
        Assert.True(detailResponse.IsSuccessStatusCode, detailBody);
        var detail = JsonSerializer.Deserialize<WorkflowDefinitionDetail>(detailBody, JsonOptions())!;

        Assert.True(detail.Validation.Succeeded);
        Assert.Collection(
            detail.Definition.Graph.Edges.Where(edge => edge.SourceNodeId.Value == "start").OrderBy(edge => edge.Id.Value),
            switchCase =>
            {
                Assert.Equal(WorkflowRouteKind.SwitchCase, switchCase.Routing.Kind);
                Assert.Equal("$.customer.tier", switchCase.Routing.JsonPath);
                Assert.Equal("\"enterprise\"", switchCase.Routing.ExpectedValueJson);
                Assert.Equal("Enterprise", switchCase.Routing.Label);
            },
            switchDefault =>
            {
                Assert.Equal(WorkflowRouteKind.SwitchDefault, switchDefault.Routing.Kind);
                Assert.Equal("Default", switchDefault.Routing.Label);
            });
    }

    [Fact]
    public async Task Workflow_api_test_run_pauses_human_input_only_when_route_reaches_node()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var draft = new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "HITL routing API workflow",
            "Workflow API proof for execution-position HITL.",
            WorkflowLifecycleStatus.Draft,
            CreateHumanInputRoutingGraph(),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var automaticResponse = await host.Client.PostAsJsonAsync(
            "/api/workflows/test-runs",
            new WorkflowTestRunRequest(
                WorkflowId: null,
                VersionId: null,
                draft,
                "{\"route\":\"automatic\"}",
                WorkflowRuntimeBackendKind.InProcess,
                ValidateOnly: false));
        var automaticBody = await automaticResponse.Content.ReadAsStringAsync();
        Assert.True(automaticResponse.IsSuccessStatusCode, automaticBody);
        var automatic = JsonSerializer.Deserialize<WorkflowTestRunResult>(automaticBody, JsonOptions())!;

        var manualResponse = await host.Client.PostAsJsonAsync(
            "/api/workflows/test-runs",
            new WorkflowTestRunRequest(
                WorkflowId: null,
                VersionId: null,
                draft,
                "{\"route\":\"manual\"}",
                WorkflowRuntimeBackendKind.InProcess,
                ValidateOnly: false));
        var manualBody = await manualResponse.Content.ReadAsStringAsync();
        Assert.True(manualResponse.IsSuccessStatusCode, manualBody);
        var manual = JsonSerializer.Deserialize<WorkflowTestRunResult>(manualBody, JsonOptions())!;

        Assert.True(automatic.Succeeded, automatic.ErrorMessage);
        Assert.Equal(WorkflowRunState.Completed, automatic.Run?.State);
        Assert.Empty(automatic.PendingExternalRequests);
        Assert.True(manual.Succeeded, manual.ErrorMessage);
        Assert.Equal(WorkflowRunState.WaitingForInput, manual.Run?.State);
        var request = Assert.Single(manual.PendingExternalRequests);
        Assert.Equal(WorkflowExternalRequestKind.HumanInput, request.Kind);
        Assert.Equal(new WorkflowNodeId("human"), request.NodeId);
        var waitingEvent = Assert.Single(manual.Events, workflowEvent =>
            workflowEvent.Kind == WorkflowEventKind.WaitingForInput &&
            workflowEvent.NodeId == new WorkflowNodeId("human"));
        var waitingPayload = JsonSerializer.Deserialize<WorkflowEventPayloadEnvelope>(waitingEvent.PayloadJson, JsonOptions())!;
        Assert.Equal(WorkflowEventPayloadSource.ExternalRequest, waitingPayload.Source);
        Assert.Equal(request.Id, waitingPayload.RequestId);
        Assert.Equal(WorkflowExternalRequestKind.HumanInput, waitingPayload.RequestKind);
        Assert.Equal(new WorkflowNodeId("human"), waitingPayload.NodeId);
        Assert.Contains("\"route\":\"manual\"", waitingPayload.InlineJson, StringComparison.Ordinal);
        var automaticCheckpoint = Assert.Single(automatic.Checkpoints);
        Assert.Equal(WorkflowCheckpointKind.Completed, automaticCheckpoint.Kind);
        Assert.Equal(WorkflowResumeAvailability.NotSupported, automaticCheckpoint.ResumeAvailability);
        var manualCheckpoint = Assert.Single(manual.Checkpoints);
        Assert.Equal(WorkflowCheckpointKind.WaitingForInput, manualCheckpoint.Kind);
        Assert.Equal(request.Id, manualCheckpoint.ExternalRequestId);
        Assert.Equal(WorkflowCheckpointTrustBoundary.TrustedRuntimeState, manualCheckpoint.TrustBoundary);
        Assert.Equal(WorkflowResumeAvailability.Available, manualCheckpoint.ResumeAvailability);
        Assert.Empty(manualCheckpoint.ResumeUnavailableReason);
    }

    [Fact]
    public async Task Workflow_api_returns_validation_failure_for_invalid_test_run()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var draft = CreateDefinition(WorkflowComponentId.New());

        var response = await host.Client.PostAsJsonAsync(
            "/api/workflows/test-runs",
            new WorkflowTestRunRequest(
                WorkflowId: null,
                VersionId: null,
                DraftDefinition: draft,
                InputJson: "{}",
                RequestedBackend: WorkflowRuntimeBackendKind.InProcess,
                ValidateOnly: false));
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<WorkflowTestRunResult>(body, JsonOptions())!;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Validation.Issues, issue => issue.Code == WorkflowValidationIssueCode.InvalidComponentReference);
    }

    [Fact]
    public async Task Workflow_api_test_run_rejects_unregistered_durable_backend_policy_before_runtime()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var component = await SaveComponentAsync(host);
        var definition = CreateDefinition(component.Id) with
        {
            RuntimePolicy = new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.DurableTask,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: true,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)
        };

        var response = await host.Client.PostAsJsonAsync(
            "/api/workflows/test-runs",
            new WorkflowTestRunRequest(
                WorkflowId: null,
                VersionId: null,
                DraftDefinition: definition,
                InputJson: "{}",
                RequestedBackend: WorkflowRuntimeBackendKind.DurableTask,
                ValidateOnly: false));
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<WorkflowTestRunResult>(body, JsonOptions())!;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Validation.Issues, issue =>
            issue.Code == WorkflowValidationIssueCode.UnsupportedRuntimeBackend &&
            issue.Message.Contains("not registered", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Workflow_api_runtime_backend_catalog_marks_unregistered_durable_backends_unavailable()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);

        var response = await host.Client.GetAsync("/api/workflows/runtime-backends");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        var backends = JsonSerializer.Deserialize<IReadOnlyList<WorkflowRuntimeBackendDescriptor>>(body, JsonOptions())!;

        var inProcess = Assert.Single(backends, backend => backend.Kind == WorkflowRuntimeBackendKind.InProcess);
        var durableTask = Assert.Single(backends, backend => backend.Kind == WorkflowRuntimeBackendKind.DurableTask);
        var azureFunctions = Assert.Single(backends, backend => backend.Kind == WorkflowRuntimeBackendKind.AzureFunctions);
        Assert.Equal(WorkflowRuntimeBackendAvailabilityKind.Registered, inProcess.Availability);
        Assert.True(inProcess.IsRegistered);
        Assert.True(inProcess.IsRunnable);
        Assert.Equal(WorkflowRuntimeBackendAvailabilityKind.Planned, durableTask.Availability);
        Assert.False(durableTask.IsRegistered);
        Assert.False(durableTask.IsRunnable);
        Assert.Contains("not registered", durableTask.AvailabilityReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(WorkflowRuntimeBackendAvailabilityKind.Planned, azureFunctions.Availability);
        Assert.False(azureFunctions.IsRunnable);
    }

    [Fact]
    public async Task Workflow_api_rejects_unregistered_durable_backend_policy_on_save()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var component = await SaveComponentAsync(host);

        var response = await host.Client.PostAsJsonAsync(
            "/api/workflows/definitions",
            CreateDefinitionSaveRequest(
                component.Id,
                runtimePolicy: new WorkflowRuntimePolicy(
                    WorkflowRuntimeBackendKind.DurableTask,
                    AllowInProcessPreviewRuns: true,
                    RequireDurableProductionRuns: true,
                    ExposeAzureFunctionsStatusEndpoint: false,
                    ExposeAzureFunctionsMcpTool: false)));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("not registered", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(nameof(WorkflowRuntimeBackendKind.DurableTask), body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Workflow_api_rejects_unregistered_durable_backend_start_request()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var component = await SaveComponentAsync(host);
        var saveResponse = await host.Client.PostAsJsonAsync(
            "/api/workflows/definitions",
            CreateDefinitionSaveRequest(
                component.Id,
                graph: CreatePassthroughGraph(),
                status: WorkflowLifecycleStatus.Active));
        var saveBody = await saveResponse.Content.ReadAsStringAsync();
        Assert.True(saveResponse.IsSuccessStatusCode, saveBody);
        var definition = JsonSerializer.Deserialize<WorkflowDefinition>(saveBody, JsonOptions())!;
        Assert.Equal(WorkflowLifecycleStatus.Active, definition.Status);

        var response = await host.Client.PostAsJsonAsync(
            "/api/workflows/runs/start",
            new
            {
                workflowId = definition.Id.Value,
                inputJson = "{}",
                requestedBackend = WorkflowRuntimeBackendKind.DurableTask
            });
        var body = await response.Content.ReadAsStringAsync();

        if (response.StatusCode != HttpStatusCode.BadRequest)
        {
            Assert.Fail($"Expected BadRequest but received {response.StatusCode}. Body: {body}");
        }
        Assert.Contains("not registered", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(nameof(WorkflowRuntimeBackendKind.DurableTask), body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Workflow_api_test_run_applies_payload_policy_to_large_runtime_payloads()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var draft = new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Large payload API workflow",
            "Workflow API proof for runtime payload artifact policy.",
            WorkflowLifecycleStatus.Draft,
            CreatePassthroughGraph(),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        var settings = WorkflowSettings.Default with
        {
            ArtifactPolicy = WorkflowSettings.Default.ArtifactPolicy with
            {
                MaxInlinePayloadCharacters = 512
            }
        };
        var settingsResponse = await host.Client.PostAsJsonAsync("/api/workflows/settings", settings);
        var settingsBody = await settingsResponse.Content.ReadAsStringAsync();
        Assert.True(settingsResponse.IsSuccessStatusCode, settingsBody);
        var inputJson = $$"""{"token":"raw-token-value","prompt":"{{new string('x', 2048)}}"}""";

        var response = await host.Client.PostAsJsonAsync(
            "/api/workflows/test-runs",
            new WorkflowTestRunRequest(
                WorkflowId: null,
                VersionId: null,
                DraftDefinition: draft,
                InputJson: inputJson,
                RequestedBackend: WorkflowRuntimeBackendKind.InProcess,
                ValidateOnly: false));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        var result = JsonSerializer.Deserialize<WorkflowTestRunResult>(body, JsonOptions())!;
        var started = Assert.Single(result.Events, workflowEvent => workflowEvent.Kind == WorkflowEventKind.Started);
        var startedPayload = JsonSerializer.Deserialize<WorkflowEventPayloadEnvelope>(started.PayloadJson, JsonOptions())!;

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.False(startedPayload.InlineTruncated);
        Assert.Empty(startedPayload.InlineJson);
        Assert.Empty(startedPayload.Reference);
        Assert.DoesNotContain("raw-token-value", startedPayload.InlineJson, StringComparison.Ordinal);
        Assert.Contains(result.Artifacts, artifact =>
            artifact.Kind == WorkflowArtifactKind.Json &&
            artifact.Name.Contains("input", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Artifacts, artifact =>
            artifact.Kind == WorkflowArtifactKind.Json &&
            artifact.NodeId == new WorkflowNodeId("logic"));

        var inputArtifact = Assert.Single(result.Artifacts, artifact =>
            artifact.Kind == WorkflowArtifactKind.Json &&
            artifact.Name.Contains("input", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(result.Run);
        var artifactContent = await host.Client.GetStringAsync(
            $"/api/workflows/runs/{result.Run!.RunId.Value:D}/artifacts/{inputArtifact.Id.Value:D}/content");
        Assert.Contains("[REDACTED]", artifactContent, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-token-value", artifactContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Workflow_contract_lists_control_and_validation_routes()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);

        using var contract = JsonDocument.Parse(await host.Client.GetStringAsync("/api/workflows/contract"));
        var endpoints = contract.RootElement
            .GetProperty("endpoints")
            .EnumerateArray()
            .Select(endpoint => endpoint.GetString())
            .ToArray();

        Assert.Contains("GET /api/workflows/contract", endpoints);
        Assert.Contains("POST /api/workflows/validate", endpoints);
        Assert.Contains("POST /api/workflows/test-runs", endpoints);
        Assert.Contains("GET /api/workflows/executor-catalog", endpoints);
        Assert.Contains("POST /api/workflows/runs/{runId}/cancel", endpoints);
        var boundarySummary = contract.RootElement.GetProperty("boundarySummary").GetString() ?? string.Empty;
        Assert.Contains(
            "agent skill, tool, and MCP capability setup",
            boundarySummary,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Openapi_exposes_workflow_routes()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);

        using var payload = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        var paths = payload.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/workflows/contract", out _));
        Assert.True(paths.TryGetProperty("/api/workflows/definitions", out _));
        Assert.True(paths.TryGetProperty("/api/workflows/provider-options", out _));
        Assert.True(paths.TryGetProperty("/api/workflows/components", out _));
        Assert.True(paths.TryGetProperty("/api/workflows/runtime-backends", out _));
        Assert.True(paths.TryGetProperty("/api/workflows/test-runs", out _));
        Assert.True(paths.TryGetProperty("/api/workflows/definitions/{workflowId}/publish", out _));
        Assert.True(paths.TryGetProperty("/api/workflows/definitions/{workflowId}/export", out _));
        Assert.True(paths.TryGetProperty("/api/workflows/definitions/import", out _));
        Assert.True(paths.TryGetProperty("/api/workflows/runs/{runId}/events", out _));
        Assert.True(paths.TryGetProperty("/api/workflows/runs/{runId}/checkpoints", out _));
        Assert.True(paths.TryGetProperty("/api/workflows/runs/{runId}/artifacts/{artifactId}/content", out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task Workflow_api_handler_rejects_invalid_explicit_analytics_recent_take(int take)
    {
        var service = new UnexpectedWorkflowAnalyticsQueryService();
        var result = await WorkflowsApi.GetWorkflowAnalyticsResultAsync(
            new WorkflowAnalyticsApiQuery { Take = take },
            service);
        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var body = JsonSerializer.Serialize(valueResult.Value, JsonOptions());

        Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
        Assert.False(service.WasCalled);
        Assert.Contains("workflows.request-invalid", body, StringComparison.Ordinal);
        Assert.Contains("between 1 and 500", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Workflow_api_exposes_agent_provider_options_for_llm_components()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);

        var response = await host.Client.GetAsync("/api/workflows/provider-options");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        var options = JsonSerializer.Deserialize<IReadOnlyList<WorkflowProviderOption>>(body, JsonOptions())!;

        Assert.NotEmpty(options);
        Assert.All(options, option => Assert.Equal(ProviderProfilePurpose.Chat, option.Purpose));
        Assert.Contains(options, option => option.IsEnabled && !string.IsNullOrWhiteSpace(option.DefaultModel));
    }

    private static async Task<LlmCallComponent> SaveComponentAsync(ApiTestHost host)
    {
        var response = await host.Client.PostAsJsonAsync("/api/workflows/components", CreateComponentRequest());
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<LlmCallComponent>(body, JsonOptions())!;
    }

    private static WorkflowDefinitionSaveRequest CreateDefinitionSaveRequest(
        WorkflowComponentId componentId,
        WorkflowGraph? graph = null,
        WorkflowRuntimePolicy? runtimePolicy = null,
        WorkflowLifecycleStatus status = WorkflowLifecycleStatus.Draft)
    {
        return new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: "API workflow",
            Description: "Workflow created by API integration tests.",
            Status: status,
            Graph: graph ?? CreateGraph(componentId),
            RuntimePolicy: runtimePolicy ?? new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false));
    }

    private static WorkflowDefinition CreateDefinition(WorkflowComponentId componentId)
    {
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Draft API workflow",
            "Invalid draft workflow for API validation.",
            WorkflowLifecycleStatus.Draft,
            CreateGraph(componentId),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static WorkflowGraph CreateGraph(WorkflowComponentId componentId)
    {
        return new WorkflowGraph(
            new WorkflowNodeId("start"),
            [
                CreateNode("start", WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                CreateNode("llm", WorkflowNodeKind.LlmCall, componentId),
                CreateNode("end", WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
            ],
            [
                CreateEdge("start-to-llm", "start", "llm"),
                CreateEdge("llm-to-end", "llm", "end")
            ]);
    }

    private static WorkflowGraph CreatePassthroughGraph()
    {
        return new WorkflowGraph(
            new WorkflowNodeId("start"),
            [
                CreateNode("start", WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                CreateNode("logic", WorkflowNodeKind.StrictLogic, inputShape: WorkflowValueShape.Text, resultShape: WorkflowValueShape.Text),
                CreateNode("end", WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
            ],
            [
                CreateEdge("start-to-logic", "start", "logic"),
                CreateEdge("logic-to-end", "logic", "end")
            ]);
    }

    private static WorkflowGraph CreateRoutingGraph()
    {
        return new WorkflowGraph(
            new WorkflowNodeId("start"),
            [
                CreateNode("start", WorkflowNodeKind.Start, resultShape: JsonShape()),
                CreateNode("enterprise", WorkflowNodeKind.StrictLogic, inputShape: JsonShape(), resultShape: JsonShape()),
                CreateNode("standard", WorkflowNodeKind.StrictLogic, inputShape: JsonShape(), resultShape: JsonShape()),
                CreateNode("end", WorkflowNodeKind.End, inputShape: JsonShape())
            ],
            [
                CreateEdge(
                    "start-to-enterprise",
                    "start",
                    "enterprise",
                    WorkflowEdgeKind.Conditional,
                    WorkflowEdgeRouting.SwitchCase(
                        "$.customer.tier",
                        "\"enterprise\"",
                        WorkflowRouteValueKind.String,
                        "Enterprise")),
                CreateEdge(
                    "start-to-standard",
                    "start",
                    "standard",
                    WorkflowEdgeKind.Conditional,
                    WorkflowEdgeRouting.SwitchDefault("Default")),
                CreateEdge("enterprise-to-end", "enterprise", "end"),
                CreateEdge("standard-to-end", "standard", "end")
            ]);
    }

    private static WorkflowGraph CreateHumanInputRoutingGraph()
    {
        return new WorkflowGraph(
            new WorkflowNodeId("start"),
            [
                CreateNode("start", WorkflowNodeKind.Start, resultShape: JsonShape()),
                CreateNode("human", WorkflowNodeKind.HumanInput, inputShape: JsonShape(), resultShape: JsonShape()),
                CreateNode("end", WorkflowNodeKind.End, inputShape: JsonShape())
            ],
            [
                CreateEdge(
                    "start-to-human",
                    "start",
                    "human",
                    WorkflowEdgeKind.Conditional,
                    WorkflowEdgeRouting.SwitchCase(
                        "$.route",
                        "\"manual\"",
                        WorkflowRouteValueKind.String,
                        "Manual")),
                CreateEdge(
                    "start-to-end",
                    "start",
                    "end",
                    WorkflowEdgeKind.Conditional,
                    WorkflowEdgeRouting.SwitchDefault("Automatic")),
                CreateEdge("human-to-end", "human", "end")
            ]);
    }

    private static WorkflowValueShape JsonShape()
        => new(WorkflowValueShapeKind.Json, "{}", "JSON payload");

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
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: inputShape ?? WorkflowValueShape.Text,
                ResultShape: resultShape ?? WorkflowValueShape.Text));
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

    private static LlmCallComponentSaveRequest CreateComponentRequest()
    {
        return new LlmCallComponentSaveRequest(
            Id: null,
            Name: "Summarize",
            ProviderProfileId: null,
            Model: "gpt-5.4",
            Modality: WorkflowModality.Text,
            ModelSettings: new WorkflowModelSettings(
                Temperature: 0.2,
                MaxOutputTokens: 800,
                RequireJsonOutput: false,
                ResponseFormatJsonSchema: string.Empty),
            Instructions: "Summarize the input.",
            InputShape: WorkflowValueShape.Text,
            ResultShape: WorkflowValueShape.Text,
            Permissions: AgentPermissionsPolicy.Default);
    }

    private static JsonSerializerOptions JsonOptions()
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    private sealed class UnexpectedWorkflowAnalyticsQueryService : IWorkflowAnalyticsQueryService
    {
        public bool WasCalled { get; private set; }

        public Task<WorkflowAnalyticsSnapshot> QueryAsync(
            WorkflowAnalyticsQuery query,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            throw new InvalidOperationException("Invalid explicit take must be rejected before querying analytics.");
        }
    }
}
