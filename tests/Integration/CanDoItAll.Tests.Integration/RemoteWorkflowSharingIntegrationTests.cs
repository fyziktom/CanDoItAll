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
        var seed = WorkflowDefinitionValidator.CreateComponentValidationDefinition(Component());
        var graph = seed.Graph with {
            Nodes = [seed.Graph.Nodes[0], seed.Graph.Nodes[^1]],
            Edges = [seed.Graph.Edges[0] with { TargetNodeId = seed.Graph.Nodes[^1].Id }]
        };
        var saved = await catalog.SaveDefinitionAsync(new(null, null, "Budget review", "Saved workflow", WorkflowLifecycleStatus.Draft, graph, seed.RuntimePolicy));
        var hidden = await catalog.SaveDefinitionAsync(new(null, null, "Private budget", "Local only", WorkflowLifecycleStatus.Draft, graph, seed.RuntimePolicy));
        using var anonymous = await host.Client.GetAsync(RemoteWorkflowRoutes.Catalog);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var denied = await ReadAsync(RemoteWorkflowRoutes.Catalog, ApiAccessScopeNames.ReadSharedProviderCatalog);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var summary = (await catalog.ListDefinitionsAsync()).Single(item => item.Id == saved.Id);
        await sharing.SetPublishedAsync(summary, true, _ => ValueTask.CompletedTask, CancellationToken.None);
        using var listed = await ReadAsync(RemoteWorkflowRoutes.Catalog);
        listed.EnsureSuccessStatusCode();
        var row = Assert.Single((await listed.Content.ReadFromJsonAsync<RemoteTemplateRow[]>())!);
        Assert.Equal(saved.Id.Value, row.Id);
        Assert.Equal(saved.VersionId.Value, row.VersionId);
        using var details = await ReadAsync(RemoteWorkflowRoutes.Details(row.Id));
        var payload = await details.Content.ReadFromJsonAsync<RemoteWorkflowDocument>();
        Assert.NotNull(payload);
        Assert.Equal(row, payload.Row);
        Assert.Equal(graph.Edges, payload.Graph.Edges);
        Assert.Empty(payload.Models);
        Assert.Empty(payload.Components);
        using var privateItem = await ReadAsync(RemoteWorkflowRoutes.Details(hidden.Id.Value));
        Assert.Equal(HttpStatusCode.NotFound, privateItem.StatusCode);
        await sharing.SetPublishedAsync(summary, false, _ => ValueTask.CompletedTask, CancellationToken.None);
        using var withdrawn = await ReadAsync(RemoteWorkflowRoutes.Details(saved.Id.Value));
        Assert.Equal(HttpStatusCode.NotFound, withdrawn.StatusCode);
        await sharing.SetPublishedAsync(summary, true, _ => ValueTask.CompletedTask, CancellationToken.None);
        var active = await catalog.ChangeDefinitionStatusAsync(new(saved.Id, saved.VersionId, WorkflowLifecycleStatus.Active));
        using var activeResponse = await ReadAsync(RemoteWorkflowRoutes.Details(saved.Id.Value));
        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);
        var archivedVersion = await catalog.ChangeDefinitionStatusAsync(new(saved.Id, active.VersionId, WorkflowLifecycleStatus.Archived));
        using var archived = await ReadAsync(RemoteWorkflowRoutes.Details(saved.Id.Value));
        Assert.Equal(HttpStatusCode.NotFound, archived.StatusCode);
        await catalog.DeleteDefinitionAsync(new(saved.Id, archivedVersion.VersionId));
        var recreated = await catalog.SaveDefinitionAsync(new(saved.Id, null, "New private workflow", "", WorkflowLifecycleStatus.Draft, graph, seed.RuntimePolicy));
        using var recreatedResponse = await ReadAsync(RemoteWorkflowRoutes.Details(recreated.Id.Value));
        Assert.Equal(HttpStatusCode.NotFound, recreatedResponse.StatusCode);

        async Task<HttpResponseMessage> ReadAsync(string route, string permission = ApiAccessScopeNames.ReadSharedWorkflows) {
            var token = services.GetRequiredService<IApiTokenService>().IssueToken(new() { Subject = "workflow-reader", Scopes = [permission] });
            using var request = new HttpRequestMessage(HttpMethod.Get, route);
            request.Headers.Authorization = new AuthenticationHeaderValue(token.TokenType, token.Token);
            return await host.Client.SendAsync(request);
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
}
