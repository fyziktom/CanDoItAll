using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Tests.Integration.Api;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class RemoteWorkflowSharingIntegrationTests {
    [Fact]
    public async Task Read_scope_exposes_only_explicitly_shared_current_draft_or_active_workflows_and_withdrawal_hides_details() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: true, useInMemoryDatabase: true);
        await using var scope = host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var catalog = services.GetRequiredService<IWorkflowCatalogService>();
        var sharing = services.GetRequiredService<RemoteWorkflowCatalog>();
        var openApi = await host.Client.GetFromJsonAsync<JsonObject>("/openapi/v1.json");
        Assert.NotNull(openApi);
        Assert.Empty(OpenApiDescriptionCoverage.FindGaps(openApi, "/" + RemoteWorkflowRoutes.Catalog));
        var request = PortableWorkflow();
        var saved = await catalog.SaveDefinitionAsync(request);
        var hidden = await catalog.SaveDefinitionAsync(request with { Name = "Private budget" });
        using var anonymous = await host.Client.GetAsync(RemoteWorkflowRoutes.Catalog);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var denied = await ReadAsync(RemoteWorkflowRoutes.Catalog, ApiAccessScopeNames.ReadSharedProviderCatalog);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var published = DefinitionSharingSettings.Default with { SharedTemplate = true };
        await sharing.SetSharingAsync(saved.Id, saved.VersionId, DefinitionSharingSettings.Default, published, _ => ValueTask.CompletedTask, default);
        using var listed = await ReadAsync(RemoteWorkflowRoutes.Catalog);
        listed.EnsureSuccessStatusCode();
        var row = Assert.Single((await listed.Content.ReadFromJsonAsync<RemoteTemplateRow[]>())!);
        Assert.Equal(saved.Id.Value, row.Id);
        Assert.Equal(saved.VersionId.Value, row.VersionId);
        using var details = await ReadAsync(RemoteWorkflowRoutes.Details(row.Id));
        var payload = await details.Content.ReadFromJsonAsync<RemoteWorkflowDocument>();
        Assert.NotNull(payload);
        Assert.Equal(row, payload.Row);
        Assert.Equal(request.Graph.Edges, payload.Graph.Edges);
        Assert.Empty(payload.Models);
        Assert.Empty(payload.Components);
        using var privateItem = await ReadAsync(RemoteWorkflowRoutes.Details(hidden.Id.Value));
        Assert.Equal(HttpStatusCode.NotFound, privateItem.StatusCode);
        await sharing.SetSharingAsync(saved.Id, saved.VersionId, published, DefinitionSharingSettings.Default, _ => ValueTask.CompletedTask, default);
        using var withdrawn = await ReadAsync(RemoteWorkflowRoutes.Details(saved.Id.Value));
        Assert.Equal(HttpStatusCode.NotFound, withdrawn.StatusCode);
        await sharing.SetSharingAsync(saved.Id, saved.VersionId, DefinitionSharingSettings.Default, published, _ => ValueTask.CompletedTask, default);
        var active = await catalog.ChangeDefinitionStatusAsync(new(saved.Id, saved.VersionId, WorkflowLifecycleStatus.Active));
        using var activeResponse = await ReadAsync(RemoteWorkflowRoutes.Details(saved.Id.Value));
        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);
        var archivedVersion = await catalog.ChangeDefinitionStatusAsync(new(saved.Id, active.VersionId, WorkflowLifecycleStatus.Archived));
        using var archived = await ReadAsync(RemoteWorkflowRoutes.Details(saved.Id.Value));
        Assert.Equal(HttpStatusCode.NotFound, archived.StatusCode);
        await catalog.DeleteDefinitionAsync(new(saved.Id, archivedVersion.VersionId));
        var recreated = await catalog.SaveDefinitionAsync(request with { Id = saved.Id, Name = "New private workflow" });
        using var recreatedResponse = await ReadAsync(RemoteWorkflowRoutes.Details(recreated.Id.Value));
        Assert.Equal(HttpStatusCode.NotFound, recreatedResponse.StatusCode);

        async Task<HttpResponseMessage> ReadAsync(string route, string permission = ApiAccessScopeNames.ReadSharedWorkflows) {
            var token = services.GetRequiredService<IApiTokenService>().IssueToken(new() { Subject = "workflow-reader", Scopes = [permission] });
            using var message = new HttpRequestMessage(HttpMethod.Get, route);
            message.Headers.Authorization = new AuthenticationHeaderValue(token.TokenType, token.Token);
            return await host.Client.SendAsync(message);
        }
    }

    [Fact]
    public async Task Workflow_API_availability_controls_direct_discovery_reads_writes_and_launches_independently_of_template_sharing() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: true);
        await using var scope = host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var catalog = services.GetRequiredService<IWorkflowCatalogService>();
        var sharing = services.GetRequiredService<RemoteWorkflowCatalog>();
        var token = services.GetRequiredService<IApiTokenService>().IssueToken(new() {
            Subject = "workflow-api-operator",
            Scopes = [ApiAccessScopeNames.ReadWorkflows, ApiAccessScopeNames.WriteWorkflows, ApiAccessScopeNames.ExecuteWorkflows,
                ApiAccessScopeNames.ReadSharedWorkflows]
        });
        var client = host.Client;
        client.DefaultRequestHeaders.Authorization = new(token.TokenType, token.Token);
        const string template = "review-budget";
        var draft = PortableWorkflow() with {
            Status = WorkflowLifecycleStatus.Active, TemplateProvenance = new(template, "finance", "1", new string('a', 64))
        };
        var visible = await catalog.SaveDefinitionAsync(draft with { Name = "API review" });
        var saved = await catalog.SaveDefinitionAsync(draft with { Name = "Shared review", ExternalNamespace = "finance", ExternalKey = "review" });
        var definitionPath = $"/api/workflows/definitions/{saved.Id.Value:D}";
        var versionQuery = $"?expectedVersionId={saved.VersionId.Value:D}";
        const string externalPath = "/api/workflows/definitions/by-external-key/finance/review";
        var automatic = await sharing.GetSharingAsync(saved.Id, saved.VersionId, default);
        Assert.Equal(DefinitionSharingSettings.Default, automatic);
        var envelope = await client.GetFromJsonAsync<WorkflowDefinitionExportEnvelope>($"{definitionPath}/export");
        Assert.NotNull(envelope);
        var launch = new WorkflowRunStartApiRequest { WorkflowId = saved.Id.Value, VersionId = saved.VersionId.Value, InputJson = "{}" };
        using var admitted = await client.PostAsJsonAsync("/api/workflows/runs/start", launch);
        admitted.EnsureSuccessStatusCode();
        var run = await admitted.Content.ReadFromJsonAsync<WorkflowRunStartApiResponse>();
        Assert.NotNull(run);
        var templateOnly = new DefinitionSharingSettings(false, true);
        await sharing.SetSharingAsync(saved.Id, saved.VersionId, automatic, templateOnly, _ => ValueTask.CompletedTask, default);

        Assert.Contains(await catalog.ListDefinitionsAsync(), item => item.Id == saved.Id);
        var listed = await client.GetFromJsonAsync<WorkflowCatalogItem[]>("/api/workflows/definitions");
        Assert.NotNull(listed);
        Assert.Contains(listed, item => item.Id == visible.Id);
        Assert.DoesNotContain(listed, item => item.Id == saved.Id);
        Assert.Empty((await client.GetFromJsonAsync<WorkflowCatalogItem[]>("/api/workflows/definitions?externalNamespace=finance&externalKey=review"))!);
        var external = await client.GetFromJsonAsync<WorkflowStableIdentityResolution>(externalPath);
        Assert.Equal(WorkflowStableIdentityResolutionStatus.NotFound, external!.Status);
        Assert.Empty(external.Materializations);
        var resolution = await client.GetFromJsonAsync<WorkflowStableIdentityResolution>($"/api/workflows/definitions/by-template-key/{template}");
        Assert.Equal(WorkflowStableIdentityResolutionStatus.Resolved, resolution!.Status);
        Assert.Equal(visible.Id, resolution.WorkflowId);
        Assert.Equal(visible.Id, Assert.Single(resolution.Materializations).Id);
        await AssertHiddenAsync(client.GetAsync(definitionPath));
        await AssertHiddenAsync(client.GetAsync($"{definitionPath}/versions/{saved.VersionId.Value:D}"));
        await AssertHiddenAsync(client.GetAsync($"{definitionPath}/export"));
        await AssertHiddenAsync(client.PostAsync($"{definitionPath}/validate", null));
        await AssertHiddenAsync(client.PostAsJsonAsync("/api/workflows/definitions", draft with { Id = saved.Id, ExpectedVersionId = saved.VersionId }));
        await AssertHiddenAsync(client.PostAsJsonAsync("/api/workflows/definitions/import",
            new WorkflowDefinitionImportRequest(envelope, null, null, true) { ExpectedVersionId = saved.VersionId }));
        foreach (var change in new[] { "publish", "suspend", "archive" }) {
            await AssertHiddenAsync(client.PostAsync($"{definitionPath}/{change}{versionQuery}", null));
        }
        await AssertHiddenAsync(client.DeleteAsync(definitionPath + versionQuery));
        await AssertHiddenAsync(client.PostAsJsonAsync($"{definitionPath}/runs/start", launch));
        await AssertHiddenAsync(client.PostAsJsonAsync("/api/workflows/runs/start", launch));
        var preview = new WorkflowTestRunRequest(saved.Id, saved.VersionId, null, "{}", null, ValidateOnly: false);
        await AssertHiddenAsync(client.PostAsJsonAsync("/api/workflows/test-runs", preview));
        await AssertHiddenAsync(client.PostAsJsonAsync("/api/workflows/test-runs", preview with { WorkflowId = null, DraftDefinition = saved }));
        using var observed = await client.GetAsync($"/api/workflows/runs/{run.Run.RunId:D}");
        Assert.Equal(HttpStatusCode.OK, observed.StatusCode);
        var analytics = await client.GetFromJsonAsync<WorkflowAnalyticsSnapshot>($"/api/workflows/analytics?WorkflowId={saved.Id.Value:D}");
        Assert.NotNull(analytics);
        Assert.Equal(0, analytics.DefinitionCount);
        Assert.Equal(1, analytics.RunCount);
        using var shared = await client.GetAsync(RemoteWorkflowRoutes.Details(saved.Id.Value));
        Assert.Equal(HttpStatusCode.OK, shared.StatusCode);

        await sharing.SetSharingAsync(saved.Id, saved.VersionId, templateOnly, automatic, _ => ValueTask.CompletedTask, default);
        var restored = await client.GetFromJsonAsync<WorkflowDefinitionDetail>(definitionPath);
        Assert.Equal(saved.VersionId, restored!.Definition.VersionId);
        await AssertHiddenAsync(client.GetAsync(RemoteWorkflowRoutes.Details(saved.Id.Value)));

        static async Task AssertHiddenAsync(Task<HttpResponseMessage> pending) {
            using var response = await pending;
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task Workflow_component_and_prompt_commit_together_and_repeated_import_preserves_local_edits() {
        await using var database = PostgresTestDatabaseLease.Create("remoteworkflowdraft");
        var factory = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var gallery = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateGallery(factory);
        var catalog = WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateCatalog(factory, gallery, new WorkflowDefinitionValidator());
        var component = Component();
        var workflow = WorkflowDefinitionValidator.CreateComponentValidationDefinition(component);
        var llmNode = workflow.Graph.Nodes[1];
        var second = llmNode with { Id = new("review-again"), Name = "Second reviewer", Settings = llmNode.Settings with { Model = "second-publisher-model" } };
        workflow = workflow with { Graph = workflow.Graph with {
            Nodes = [workflow.Graph.Nodes[0], llmNode, second, workflow.Graph.Nodes[^1]],
            Edges = [workflow.Graph.Edges[0], workflow.Graph.Edges[1] with { TargetNodeId = second.Id },
                workflow.Graph.Edges[1] with { Id = new("second-end"), SourceNodeId = second.Id }]
        } };
        var row = new RemoteTemplateRow(workflow.Id.Value, Guid.NewGuid(), workflow.Name, workflow.Description, null, workflow.UpdatedAtUtc, VersionId: workflow.VersionId.Value);
        var document = await RemoteWorkflowProjection.ExportAsync(row, workflow, new Dictionary<WorkflowComponentId, LlmCallComponent> { [component.Id] = component },
            (requirement, _, _, _) => Task.FromResult(requirement), CancellationToken.None);
        var choice = new RemoteTemplateModelChoice(Guid.NewGuid(), "receiving-model");
        var alternative = new RemoteTemplateModelChoice(Guid.NewGuid(), "receiving-alternative");
        var source = Guid.NewGuid();
        var refused = RemoteWorkflowDraftWriter.Prepare(source, document, [choice, alternative], workflow.RuntimePolicy);
        var authorizations = 0;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => catalog.CreateRemoteDraftAsync(refused.Definition, refused.Components,
            _ => ++authorizations == 3 ? ValueTask.FromException(new UnauthorizedAccessException("Authority revoked before commit.")) : ValueTask.CompletedTask, CancellationToken.None));
        Assert.Equal(3, authorizations);
        await using (var before = factory.CreateDbContext()) {
            Assert.Empty(await before.Set<WorkflowDefinitionRecord>().ToArrayAsync());
            Assert.Empty(await before.Set<WorkflowComponentRecord>().ToArrayAsync());
            Assert.Empty(await before.Set<PromptArtifact>().ToArrayAsync());
            Assert.Empty(await before.Set<PromptVersion>().ToArrayAsync());
        }
        async Task<Guid> ImportAsync() {
            var draft = RemoteWorkflowDraftWriter.Prepare(source, document, [choice, alternative], workflow.RuntimePolicy);
            return await catalog.CreateRemoteDraftAsync(draft.Definition, draft.Components, _ => ValueTask.CompletedTask, CancellationToken.None);
        }
        var accepted = await Task.WhenAll(ImportAsync(), ImportAsync());
        Assert.Equal(accepted[0], accepted[1]);
        var local = (await catalog.GetDefinitionAsync(new(accepted[0])))!.Definition;
        var importedComponents = await catalog.ListComponentsAsync();
        var importedComponent = Assert.Single(importedComponents, item => item.ProviderProfileId == choice.ProviderId);
        var llm = Assert.Single(local.Graph.Nodes, node => node.Id == llmNode.Id);
        var localSecond = Assert.Single(local.Graph.Nodes, node => node.Id == second.Id);
        Assert.Equal(alternative.Model, localSecond.Settings.Model);
        Assert.Equal(Assert.Single(importedComponents, item => item.ProviderProfileId == alternative.ProviderId).Id, localSecond.Settings.ComponentId);
        Assert.NotEqual(workflow.Id, local.Id);
        Assert.Equal(importedComponent.Id, llm.Settings.ComponentId);
        Assert.Equal(choice.ProviderId, importedComponent.ProviderProfileId);
        Assert.Equal(choice.Model, llm.Settings.Model);
        Assert.NotEqual(component.Id, importedComponent.Id);
        Assert.Equal(component.Instructions, (await gallery.GetVersionSnapshotAsync(importedComponent.PromptVersionId!.Value)).Value!.Content);
        Assert.Equal(RemoteWorkflowDraftWriter.PackKey, local.TemplatePackKey);
        var edited = await catalog.SaveDefinitionAsync(new(local.Id, local.VersionId, "My private edits", local.Description, local.Status, local.Graph, local.RuntimePolicy));
        Assert.Equal(local.Id.Value, await ImportAsync());
        Assert.Equal(edited.VersionId, (await catalog.GetDefinitionAsync(local.Id))!.Definition.VersionId);
        await using var verify = factory.CreateDbContext();
        Assert.Equal(2, await verify.Set<WorkflowComponentRecord>().CountAsync());
        Assert.Equal(2, await verify.Set<PromptVersion>().CountAsync());
        Assert.Equal(1, await verify.Set<WorkflowDefinitionHeadRecord>().CountAsync());
        Assert.Empty(await verify.Set<WorkflowRunRecordEntity>().ToArrayAsync());
    }

    private static LlmCallComponent Component() => new(WorkflowComponentId.New(), "Review", Guid.NewGuid(), "publisher-model",
        WorkflowModality.Text, new(0.2, 120, false, string.Empty), "Review a budget", WorkflowValueShape.Text, WorkflowValueShape.Text,
        AgentPermissionsPolicy.Default, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static WorkflowDefinitionSaveRequest PortableWorkflow() {
        var seed = WorkflowDefinitionValidator.CreateComponentValidationDefinition(Component());
        var graph = seed.Graph with {
            Nodes = [seed.Graph.Nodes[0], seed.Graph.Nodes[^1]],
            Edges = [seed.Graph.Edges[0] with { TargetNodeId = seed.Graph.Nodes[^1].Id }]
        };
        return new(null, null, "Budget review", "Saved workflow", WorkflowLifecycleStatus.Draft, graph, seed.RuntimePolicy);
    }
}
