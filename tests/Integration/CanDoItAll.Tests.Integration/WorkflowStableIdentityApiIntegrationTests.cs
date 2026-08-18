using System.Net;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowStableIdentityApiIntegrationTests
{
    private const string SourceHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public async Task Stable_identity_routes_and_external_filter_return_normalized_provenance()
    {
        await using var host = await CreateHostAsync(jwtEnabled: false);
        var active = await SeedStableWorkflowAsync(
            host,
            name: "A display label unrelated to either stable key",
            templateKey: "  Billing.Review  ",
            externalNamespace: "  Partner.System  ",
            externalKey: "  Invoice:Review  ");

        using var templateResponse = await host.Client.GetAsync(
            "/api/workflows/definitions/by-template-key/BILLING.REVIEW");
        var templateBody = await templateResponse.Content.ReadAsStringAsync();
        Assert.True(templateResponse.IsSuccessStatusCode, templateBody);
        var templateResolution = JsonSerializer.Deserialize<WorkflowStableIdentityResolution>(
            templateBody,
            JsonOptions())!;

        using var externalResponse = await host.Client.GetAsync(
            "/api/workflows/definitions/by-external-key/PARTNER.SYSTEM/INVOICE:REVIEW");
        var externalBody = await externalResponse.Content.ReadAsStringAsync();
        Assert.True(externalResponse.IsSuccessStatusCode, externalBody);
        var externalResolution = JsonSerializer.Deserialize<WorkflowStableIdentityResolution>(
            externalBody,
            JsonOptions())!;

        using var filterResponse = await host.Client.GetAsync(
            "/api/workflows/definitions?externalNamespace=PARTNER.SYSTEM&externalKey=INVOICE%3AREVIEW");
        var filterBody = await filterResponse.Content.ReadAsStringAsync();
        Assert.True(filterResponse.IsSuccessStatusCode, filterBody);
        var filtered = JsonSerializer.Deserialize<IReadOnlyList<WorkflowCatalogItem>>(
            filterBody,
            JsonOptions())!;

        Assert.Equal(WorkflowStableIdentityResolutionStatus.Resolved, templateResolution.Status);
        Assert.Equal("billing.review", templateResolution.Key);
        Assert.Equal(active.Id, templateResolution.WorkflowId);
        Assert.Equal(active.VersionId, templateResolution.RunnableVersionId);
        Assert.Equal("A display label unrelated to either stable key", Assert.Single(templateResolution.Materializations).Name);

        Assert.Equal(WorkflowStableIdentityResolutionStatus.Resolved, externalResolution.Status);
        Assert.Equal("partner.system", externalResolution.Namespace);
        Assert.Equal("invoice:review", externalResolution.Key);
        Assert.Equal(active.Id, externalResolution.WorkflowId);
        Assert.Equal(active.VersionId, externalResolution.RunnableVersionId);

        var item = Assert.Single(filtered);
        Assert.Equal(active.Id, item.Id);
        Assert.Equal("billing.review", item.TemplateKey);
        Assert.Equal("standard.pack", item.TemplatePackKey);
        Assert.Equal("2026.07", item.TemplatePackVersion);
        Assert.Equal(SourceHash, item.SourceHash);
        Assert.Equal("partner.system", item.ExternalNamespace);
        Assert.Equal("invoice:review", item.ExternalKey);
    }

    [Fact]
    public async Task Stable_identity_openapi_exposes_routes_parameters_and_provenance_fields()
    {
        await using var host = await CreateHostAsync(jwtEnabled: false);

        using var payload = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        var root = payload.RootElement;
        var paths = root.GetProperty("paths");
        var templateOperation = paths
            .GetProperty("/api/workflows/definitions/by-template-key/{templateKey}")
            .GetProperty("get");
        var externalOperation = paths
            .GetProperty("/api/workflows/definitions/by-external-key/{externalNamespace}/{externalKey}")
            .GetProperty("get");
        var listOperation = paths
            .GetProperty("/api/workflows/definitions")
            .GetProperty("get");

        AssertParameterNames(templateOperation, "templateKey");
        AssertParameterNames(externalOperation, "externalNamespace", "externalKey");
        AssertParameterNames(listOperation, "externalNamespace", "externalKey");
        Assert.DoesNotContain("displayName", templateOperation.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("displayName", externalOperation.GetRawText(), StringComparison.OrdinalIgnoreCase);

        var schemas = root.GetProperty("components").GetProperty("schemas");
        var resolutionProperties = FindSchemaProperties(schemas, "WorkflowStableIdentityResolution");
        AssertSchemaProperties(
            resolutionProperties,
            "identityKind",
            "namespace",
            "key",
            "status",
            "workflowId",
            "runnableVersionId",
            "materializations",
            "message");

        var catalogProperties = FindSchemaProperties(schemas, "WorkflowCatalogItem");
        AssertProvenanceProperties(catalogProperties);
        var definitionProperties = FindSchemaProperties(schemas, "WorkflowDefinition");
        AssertProvenanceProperties(definitionProperties);
    }

    [Fact]
    public async Task Stable_identity_routes_inherit_api_group_authorization()
    {
        await using var host = await CreateHostAsync(jwtEnabled: true);

        foreach (var route in new[]
                 {
                     "/api/workflows/definitions/by-template-key/billing.review",
                     "/api/workflows/definitions/by-external-key/partner.system/invoice:review",
                     "/api/workflows/definitions?externalNamespace=partner.system&externalKey=invoice%3Areview"
                 })
        {
            using var response = await host.Client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Stable_identity_materializations_do_not_leak_between_workspace_hosts()
    {
        await using var firstHost = await CreateHostAsync(jwtEnabled: false);
        await using var secondHost = await CreateHostAsync(jwtEnabled: false);
        await SeedStableWorkflowAsync(
            firstHost,
            name: "First workspace materialization",
            templateKey: "workspace.private",
            externalNamespace: "workspace",
            externalKey: "private");

        using var firstResponse = await firstHost.Client.GetAsync(
            "/api/workflows/definitions/by-template-key/workspace.private");
        using var secondResponse = await secondHost.Client.GetAsync(
            "/api/workflows/definitions/by-template-key/workspace.private");
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        var secondBody = await secondResponse.Content.ReadAsStringAsync();

        Assert.True(firstResponse.IsSuccessStatusCode, firstBody);
        Assert.True(secondResponse.IsSuccessStatusCode, secondBody);
        Assert.Equal(
            WorkflowStableIdentityResolutionStatus.Resolved,
            JsonSerializer.Deserialize<WorkflowStableIdentityResolution>(firstBody, JsonOptions())!.Status);
        Assert.Equal(
            WorkflowStableIdentityResolutionStatus.NotFound,
            JsonSerializer.Deserialize<WorkflowStableIdentityResolution>(secondBody, JsonOptions())!.Status);
    }

    private static async Task<ApiTestHost> CreateHostAsync(bool jwtEnabled)
        => await ApiTestHost.CreateAsync(
            jwtEnabled,
            services =>
            {
                services.RemoveAll<ILoggerProvider>();
                services.RemoveAll<ISecretVault>();
                services.AddSingleton<ISecretVault, InMemorySecretVault>();
            });

    private static async Task<WorkflowDefinition> SeedStableWorkflowAsync(
        ApiTestHost host,
        string name,
        string templateKey,
        string externalNamespace,
        string externalKey)
    {
        await using var scope = host.App.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
        var components = scope.ServiceProvider.GetRequiredService<IWorkflowComponentLibraryService>();
        var component = await components.SaveComponentAsync(new LlmCallComponentSaveRequest(
            Id: null,
            Name: "Stable identity API component",
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
            Permissions: AgentPermissionsPolicy.Default));
        var draft = await catalog.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: name,
            Description: "Stable identity API integration definition.",
            Status: WorkflowLifecycleStatus.Draft,
            Graph: CreateGraph(component.Id),
            RuntimePolicy: new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false))
        {
            TemplateProvenance = new WorkflowTemplateProvenance(
                templateKey,
                "Standard.Pack",
                "2026.07",
                SourceHash),
            ExternalNamespace = externalNamespace,
            ExternalKey = externalKey
        });

        return await catalog.ChangeDefinitionStatusAsync(new WorkflowDefinitionStatusChangeRequest(
            draft.Id,
            draft.VersionId,
            WorkflowLifecycleStatus.Active));
    }

    private static WorkflowGraph CreateGraph(WorkflowComponentId componentId)
        => new(
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

    private static WorkflowNode CreateNode(
        string id,
        WorkflowNodeKind kind,
        WorkflowComponentId? componentId = null,
        WorkflowValueShape? inputShape = null,
        WorkflowValueShape? resultShape = null)
        => new(
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

    private static WorkflowEdge CreateEdge(string id, string source, string target)
        => new(
            new WorkflowEdgeId(id),
            new WorkflowNodeId(source),
            SourcePortId: null,
            new WorkflowNodeId(target),
            TargetPortId: null,
            WorkflowEdgeKind.Direct,
            ConditionExpression: string.Empty);

    private static JsonSerializerOptions JsonOptions()
        => new(JsonSerializerDefaults.Web);

    private static void AssertParameterNames(JsonElement operation, params string[] expectedNames)
    {
        var names = operation
            .GetProperty("parameters")
            .EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString())
            .ToArray();

        Assert.Equal(expectedNames.Order(StringComparer.Ordinal), names.Order(StringComparer.Ordinal));
    }

    private static JsonElement FindSchemaProperties(JsonElement schemas, string typeName)
    {
        var schema = schemas
            .EnumerateObject()
            .Single(item =>
                item.Name.Equals(typeName, StringComparison.Ordinal) ||
                item.Name.EndsWith($".{typeName}", StringComparison.Ordinal));
        return schema.Value.GetProperty("properties");
    }

    private static void AssertSchemaProperties(JsonElement properties, params string[] names)
    {
        foreach (var name in names)
        {
            Assert.True(properties.TryGetProperty(name, out _), $"OpenAPI schema is missing property '{name}'.");
        }
    }

    private static void AssertProvenanceProperties(JsonElement properties)
        => AssertSchemaProperties(
            properties,
            "templateKey",
            "templatePackKey",
            "templatePackVersion",
            "sourceHash",
            "externalNamespace",
            "externalKey");
}
