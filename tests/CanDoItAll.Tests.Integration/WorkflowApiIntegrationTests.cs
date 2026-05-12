using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Integration;

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
    public async Task Workflow_api_rejects_publish_when_definition_is_invalid()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var saveResponse = await host.Client.PostAsJsonAsync(
            "/api/workflows/definitions",
            CreateDefinitionSaveRequest(WorkflowComponentId.New()));
        var saveBody = await saveResponse.Content.ReadAsStringAsync();
        Assert.True(saveResponse.IsSuccessStatusCode, saveBody);
        var definition = JsonSerializer.Deserialize<WorkflowDefinition>(saveBody, JsonOptions())!;

        var response = await host.Client.PostAsync($"/api/workflows/definitions/{definition.Id.Value:D}/publish", content: null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("cannot be published", body, StringComparison.OrdinalIgnoreCase);
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
    public async Task Workflow_api_returns_runtime_failure_for_unregistered_durable_backend()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var component = await SaveComponentAsync(host);
        var saveResponse = await host.Client.PostAsJsonAsync(
            "/api/workflows/definitions",
            CreateDefinitionSaveRequest(
                component.Id,
                runtimePolicy: new WorkflowRuntimePolicy(
                    WorkflowRuntimeBackendKind.DurableTask,
                    AllowInProcessPreviewRuns: true,
                    RequireDurableProductionRuns: true,
                    ExposeAzureFunctionsStatusEndpoint: false,
                    ExposeAzureFunctionsMcpTool: false)));
        var saveBody = await saveResponse.Content.ReadAsStringAsync();
        Assert.True(saveResponse.IsSuccessStatusCode, saveBody);
        var definition = JsonSerializer.Deserialize<WorkflowDefinition>(saveBody, JsonOptions())!;

        var response = await host.Client.PostAsJsonAsync(
            "/api/workflows/test-runs",
            new WorkflowTestRunRequest(
                definition.Id,
                definition.VersionId,
                DraftDefinition: null,
                InputJson: "{}",
                RequestedBackend: WorkflowRuntimeBackendKind.DurableTask,
                ValidateOnly: false));
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<WorkflowTestRunResult>(body, JsonOptions())!;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(result.Succeeded);
        Assert.Contains("not registered", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Openapi_exposes_workflow_routes()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);

        using var payload = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        var paths = payload.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/workflows/definitions", out _));
        Assert.True(paths.TryGetProperty("/api/workflows/provider-options", out _));
        Assert.True(paths.TryGetProperty("/api/workflows/components", out _));
        Assert.True(paths.TryGetProperty("/api/workflows/test-runs", out _));
        Assert.True(paths.TryGetProperty("/api/workflows/definitions/{workflowId}/publish", out _));
        Assert.True(paths.TryGetProperty("/api/workflows/definitions/{workflowId}/export", out _));
        Assert.True(paths.TryGetProperty("/api/workflows/definitions/import", out _));
        Assert.True(paths.TryGetProperty("/api/workflows/runs/{runId}/events", out _));
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
        WorkflowRuntimePolicy? runtimePolicy = null)
    {
        return new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: "API workflow",
            Description: "Workflow created by API integration tests.",
            Status: WorkflowLifecycleStatus.Draft,
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
}
