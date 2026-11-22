using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Tests.Integration.Api;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class RemoteAgentTemplateIntegrationTests {
    [Fact]
    public async Task Ordinary_API_hides_unavailable_agents_from_discovery_writes_and_new_JSON_and_SSE_calls() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: true);
        await using var scope = host.App.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var available = await workspace.SaveAgentDefinitionAsync(new() { Name = "API visible agent" });
        var hidden = await workspace.SaveAgentDefinitionAsync(new() { Name = "API private agent", ApiAvailable = false });
        var team = await workspace.SaveAgentTeamAsync(new() { Name = "Mixed exposure team", AgentIds = [available.Id, hidden.Id] });
        var note = await workspace.SaveMemoryAsync(new() { AgentId = hidden.Id, Title = "Private note" });
        await workspace.GetOrCreateChatSessionAsync(hidden.Id);
        var path = $"/api/agents/{hidden.Id:D}";
        var teamPath = $"/api/agents/teams/{team:D}";

        foreach (var list in new[] { "/api/agents?includeTemplates=true", teamPath + "/agents", "/api/agents/teams", teamPath, teamPath + "/editor", "/api/agents/bootstrap" }) {
            var json = await host.Client.GetStringAsync(list);
            Assert.DoesNotContain(hidden.Id.ToString("D"), json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(available.Id.ToString("D"), json, StringComparison.OrdinalIgnoreCase);
        }
        var teamEditor = await host.Client.GetFromJsonAsync<AgentTeamEditorModel>(teamPath + "/editor");
        Assert.NotNull(teamEditor);
        teamEditor.Description = "Updated without losing private members";
        using var updateTeam = await host.Client.PutAsJsonAsync(teamPath, teamEditor);
        updateTeam.EnsureSuccessStatusCode();
        Assert.Contains(hidden.Id, (await workspace.GetAgentTeamEditorAsync(team)).AgentIds);

        foreach (var route in new[] { path, path + "/export", path + "/chat-workspace", path + "/chat-sessions", path + "/memory", path + "/decision-data-policy" }) {
            using var read = await host.Client.GetAsync(route);
            Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        }
        var edit = await workspace.GetAgentEditorAsync(hidden.Id);
        edit.ApiAvailable = true;
        await RefuseAsync("/api/agents", edit);
        await RefuseAsync(path + "/clone", new { cloneName = "Copy private agent" });
        await RefuseAsync(path + "/convert-to-template", new { templateKey = "copy-private-agent" });
        await RefuseAsync(path + "/chat", new { prompt = "Private" });
        await RefuseAsync(path + "/chat/stream", new { prompt = "Private" });
        await RefuseAsync(path + "/execution-runs", new { prompt = "Private" });
        await RefuseAsync(path + "/execution-runs/stream", new { prompt = "Private" });
        await RefuseAsync("/api/agents/execution-runs", new { agentId = hidden.Id, prompt = "Private" });
        await RefuseAsync("/api/agents/execution-runs/stream", new { agentId = hidden.Id, prompt = "Private" });
        await RefuseAsync("/api/agents/voice/speech", new { agentId = hidden.Id, text = "Private" });
        await RefuseAsync("/api/agents/memory", new MemoryEditorModel { AgentId = available.Id, Id = note, Title = "Reassigned" });
        using var deleteNote = await host.Client.DeleteAsync($"/api/agents/memory/{note:D}");
        Assert.Equal(HttpStatusCode.NotFound, deleteNote.StatusCode);
        using var deleteAgent = await host.Client.DeleteAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, deleteAgent.StatusCode);
        Assert.False((await workspace.GetAgentEditorAsync(hidden.Id)).ApiAvailable);
        Assert.Single(await workspace.ListMemoryAsync(hidden.Id));
        Assert.Empty(await workspace.ListExecutionRunsAsync(new(AgentId: hidden.Id)));

        await workspace.SaveAgentAsync(edit);
        using var visible = await host.Client.GetAsync(path);
        visible.EnsureSuccessStatusCode();

        async Task RefuseAsync(string route, object body) {
            using var response = await host.Client.PostAsJsonAsync(route, body);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        }
    }

    [Fact]
    public async Task Legacy_template_publication_survives_edits_until_withdrawn_and_new_copies_are_private() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: true);
        await using var scope = host.App.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var store = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceCatalogStore>();
        var agent = await workspace.SaveAgentDefinitionAsync(new() { Name = "Legacy sharing preference", IsTemplate = true });
        Assert.False(agent.SharesRemoteTemplate);
        var legacy = await store.UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents.Select(item => item.Id == agent.Id ? item with { SharedTemplate = null } : item).ToList()
        });
        var publisher = Guid.NewGuid();
        Assert.Contains(RemoteAgentTemplateCatalog.List(legacy, publisher), item => item.Id == agent.Id);
        var form = await workspace.GetAgentEditorAsync(agent.Id);
        Assert.True(form.SharedTemplate);
        form.SharedTemplate = null;
        form.IsTemplate = false;
        var saved = await workspace.SaveAgentDefinitionAsync(form);
        Assert.True(saved.SharedTemplate);
        Assert.False(saved.IsTemplate);
        var copied = await workspace.CloneAgentAsync(saved.Id, "Cloned private definition");
        var converted = await workspace.ConvertToTemplateAsync(saved.Id, "converted-private-definition");
        Assert.False((await workspace.GetAgentEditorAsync(copied)).SharedTemplate);
        Assert.False((await workspace.GetAgentEditorAsync(converted)).SharedTemplate);
        var withdrawn = await workspace.GetAgentEditorAsync(saved.Id);
        withdrawn.SharedTemplate = false;
        withdrawn.IsTemplate = true;
        await workspace.SaveAgentAsync(withdrawn);
        Assert.Null(RemoteAgentTemplateCatalog.Find(await store.LoadCatalogAsync(), publisher, agent.Id));
    }

    [Fact]
    public async Task Secured_OpenApi_describes_template_routes_and_their_read_permission() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: true, useInMemoryDatabase: true);
        var document = await host.Client.GetFromJsonAsync<JsonObject>("/openapi/v1.json");
        Assert.NotNull(document);
        var endpoints = host.App.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        const string catalog = "/api/shared-agent-templates/v1/templates";
        CheckOperation(catalog, "ListRemoteAgentTemplates", ["200", "401", "403", "409"]);
        CheckOperation(catalog + "/{id}", "GetRemoteAgentTemplate", ["200", "401", "403", "404", "409"]);
        CheckOperation(catalog + "/{id}/avatar", "GetRemoteAgentTemplateAvatar", ["200", "401", "403", "404", "409"]);
        var gaps = OpenApiDescriptionCoverage.FindGaps(document, catalog);
        Assert.Empty(gaps);

        void CheckOperation(string path, string name, string[] statusCodes) {
            var operation = document["paths"]?[path]?["get"];
            Assert.NotNull(operation);
            Assert.Equal(name, operation["operationId"]?.GetValue<string>());
            Assert.NotNull(operation["security"]?[0]?["Bearer"]);
            var responses = Assert.IsType<JsonObject>(operation["responses"]);
            Assert.All(statusCodes, code => Assert.True(responses.ContainsKey(code), $"{path} response {code}"));
            var endpoint = Assert.Single(endpoints, item => item.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == name);
            Assert.Equal(ApiAccessScopeNames.ReadSharedAgentTemplates, endpoint.Metadata.GetMetadata<ApiPermissionMetadata>()?.Scope);
            Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(), data => data.Policy == ApiAuthorizationPolicies.ReadSharedAgentTemplates);
        }
    }

    [Fact]
    public async Task Publisher_requires_template_scope_and_discloses_only_portable_template_content() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: true, useInMemoryDatabase: true);
        await using var scope = host.App.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var local = await workspace.SaveAgentAsync(new() { Name = "Private local template", IsTemplate = true });
        var withdrawn = await workspace.SaveAgentAsync(new() { Name = "Archived template", SharedTemplate = true, Status = AgentLifecycleStatus.Archived });
        var capability = await workspace.SaveCapabilityAsync(new() {
            Kind = CapabilityKind.Skill, Key = "publication-skill", Name = "Publication skill", Description = "Skill guidance",
            EndpointOrPath = "inline://publication-skill",
            ConfigurationJson = """{"secretBinding":"private-sentinel","inlineSkill":{"name":"publication-skill","description":"Help","instructions":"Portable instructions","resources":[{"name":"guide","content":"Read locally"}]}}"""
        });
        const string avatar = "data:image/png;base64,iVBORw0KGgo=";
        var agent = await workspace.SaveAgentDefinitionAsync(new() {
            Name = "Template publication", SharedTemplate = true, ApiAvailable = false, Instructions = "Template text", AvatarImageUrl = avatar,
            SelectedCapabilityIds = [capability], ConfigurationJson = """{"privateConfiguration":"private-sentinel"}"""
        });
        using var anonymous = await host.Client.GetAsync(RemoteAgentTemplateRoutes.Catalog);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var providerOnly = await ReadAsync(RemoteAgentTemplateRoutes.Catalog, ApiAccessScopeNames.ReadSharedProviderCatalog);
        Assert.Equal(HttpStatusCode.Forbidden, providerOnly.StatusCode);
        using var catalogResponse = await ReadAsync(RemoteAgentTemplateRoutes.Catalog, ApiAccessScopeNames.ReadSharedAgentTemplates);
        catalogResponse.EnsureSuccessStatusCode();
        var catalog = await catalogResponse.Content.ReadFromJsonAsync<RemoteAgentTemplateSummary[]>();
        Assert.NotNull(catalog);
        var offered = Assert.Single(catalog, item => item.Id == agent.Id);
        Assert.Equal(1, offered.CapabilityCount);
        Assert.NotEqual(Guid.Empty, offered.PublisherId);
        Assert.True(offered.CatalogRevision > 0);
        Assert.True(offered.HasEmbeddedAvatar);
        Assert.Null(offered.AvatarImageUrl);
        using var preview = await ReadAsync(RemoteAgentTemplateRoutes.Avatar(agent.Id), ApiAccessScopeNames.ReadSharedAgentTemplates);
        preview.EnsureSuccessStatusCode();
        Assert.Equal(new RemoteTemplateEmbeddedAvatar(offered.PublisherId, agent.Id, offered.CatalogRevision, avatar),
            await preview.Content.ReadFromJsonAsync<RemoteTemplateEmbeddedAvatar>());
        using var hiddenAvatar = await ReadAsync(RemoteAgentTemplateRoutes.Avatar(withdrawn), ApiAccessScopeNames.ReadSharedAgentTemplates);
        Assert.Equal(HttpStatusCode.NotFound, hiddenAvatar.StatusCode);
        Assert.DoesNotContain(catalog, item => item.Id == local);
        Assert.DoesNotContain(catalog, item => item.Id == withdrawn);
        using var archived = await ReadAsync(RemoteAgentTemplateRoutes.Details(withdrawn), ApiAccessScopeNames.ReadSharedAgentTemplates);
        Assert.Equal(HttpStatusCode.NotFound, archived.StatusCode);
        using var nativeAgent = await ReadAsync(RemoteAgentTemplateRoutes.Details(local), ApiAccessScopeNames.ReadSharedAgentTemplates);
        Assert.Equal(HttpStatusCode.NotFound, nativeAgent.StatusCode);
        using var details = await ReadAsync(RemoteAgentTemplateRoutes.Details(agent.Id), ApiAccessScopeNames.ReadSharedAgentTemplates);
        details.EnsureSuccessStatusCode();
        var json = await details.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private-sentinel", json, StringComparison.Ordinal);
        Assert.DoesNotContain("providerProfileId", json, StringComparison.OrdinalIgnoreCase);
        var payload = JsonSerializer.Deserialize<RemoteAgentTemplate>(json, JsonSerializerOptions.Web);
        Assert.NotNull(payload);
        Assert.Equal(agent.UpdatedAtUtc, payload.UpdatedAtUtc);
        Assert.Equal(offered.PublisherId, payload.PublisherId);
        Assert.Equal(offered.CatalogRevision, payload.CatalogRevision);
        Assert.Equal(avatar, payload.AvatarImageUrl);
        Assert.False(agent.IsTemplate);
        using var ordinary = await ReadAsync($"/api/agents/{agent.Id:D}", ApiAccessScopeNames.ReadAgents);
        Assert.Equal(HttpStatusCode.NotFound, ordinary.StatusCode);
        Assert.Equal(ProviderProfilePurpose.Chat, payload.PreferredModel?.Purpose);
        var skill = Assert.Single(payload.Capabilities).InlineSkill;
        Assert.NotNull(skill);
        Assert.Equal("Portable instructions", skill.Instructions);
        Assert.Equal("Read locally", Assert.Single(skill.Resources).Content);
        var skillEditor = await workspace.GetCapabilityEditorAsync(capability);
        skillEditor.ConfigurationJson = """{"inlineSkill":{"name":"publication-skill","description":"Help","instructions":"Changed skill content"}}""";
        await workspace.SaveCapabilityAsync(skillEditor);
        using var changed = await ReadAsync(RemoteAgentTemplateRoutes.Details(agent.Id), ApiAccessScopeNames.ReadSharedAgentTemplates);
        var changedTemplate = await changed.Content.ReadFromJsonAsync<RemoteAgentTemplate>();
        Assert.NotNull(changedTemplate);
        Assert.True(changedTemplate.CatalogRevision > offered.CatalogRevision);

        var editor = await workspace.GetAgentEditorAsync(agent.Id);
        editor.SharedTemplate = false;
        await workspace.SaveAgentAsync(editor);
        foreach (var route in new[] { RemoteAgentTemplateRoutes.Details(agent.Id), RemoteAgentTemplateRoutes.Avatar(agent.Id) }) {
            using var removed = await ReadAsync(route, ApiAccessScopeNames.ReadSharedAgentTemplates);
            Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
        }

        async Task<HttpResponseMessage> ReadAsync(string route, string permission) {
            var token = scope.ServiceProvider.GetRequiredService<IApiTokenService>().IssueToken(new() {
                Subject = "template-reader", Scopes = [permission]
            });
            using var request = new HttpRequestMessage(HttpMethod.Get, route);
            request.Headers.Authorization = new AuthenticationHeaderValue(token.TokenType, token.Token);
            return await host.Client.SendAsync(request);
        }
    }

    [Fact]
    public async Task Import_persists_the_chosen_local_model_and_avatar_and_rejects_a_missing_model() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        await using var scope = host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var local = await workspace.ReadAgentDecisionDataPolicyContextAsync();
        const string model = "local-review-model";
        var provider = await services.GetRequiredService<IProviderProfileRegistry>().SaveProviderAsync(new() {
            Name = "Replacement provider", Kind = ProviderKind.Ollama, Transport = ProviderTransportKind.ChatCompletions,
            BaseUrl = "http://127.0.0.1:11434", DefaultModel = model, SuggestedModels = [model], IsEnabled = true
        });
        var source = Guid.NewGuid();
        var template = new RemoteAgentTemplate(Guid.NewGuid(), "Writing assistant", "Writer", "Compose text", "Edit the request",
            AgentWorkloadKind.General, [], [], DateTimeOffset.UtcNow) {
            PublisherId = Guid.NewGuid(), CatalogRevision = 9, AvatarImageUrl = AgentAvatarImageCatalog.BundledAvatarUrls[0]
        };
        await Assert.ThrowsAsync<RemoteAgentTemplateImportException>(() => workspace.ImportRemoteAgentTemplateAsync(source, template, local,
            model: new(provider, "unavailable")));
        var id = await workspace.ImportRemoteAgentTemplateAsync(source, template, local, model: new(provider, model));
        var saved = await workspace.GetAgentEditorAsync(id);
        Assert.Equal(provider, saved.ProviderProfileId);
        Assert.Equal(model, saved.Model);
        Assert.Equal(template.AvatarImageUrl, saved.AvatarImageUrl);
        Assert.Equal(id, await workspace.ImportRemoteAgentTemplateAsync(source, template, local));
        Assert.Equal(provider, (await workspace.GetAgentEditorAsync(id)).ProviderProfileId);
    }

    [Fact]
    public async Task Publisher_does_not_expose_template_endpoints_without_JWT_authorization() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: true);
        using var result = await host.Client.GetAsync(RemoteAgentTemplateRoutes.Catalog);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
    }

    [Fact]
    public async Task Import_materializes_one_local_copy_per_source_preserves_edits_and_refuses_partial_or_unauthorized_writes() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: true);
        await using var scope = host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var tool = await workspace.SaveCapabilityAsync(new() { Kind = CapabilityKind.Tool, Name = "Local fixture tool", Key = "local-fixture-tool" });
        var before = await workspace.LoadCatalogSnapshotAsync();
        var source = Guid.NewGuid();
        var template = new RemoteAgentTemplate(Guid.NewGuid(), "Imported local draft", "Writer", "Summary", "Compose a note", AgentWorkloadKind.General, [],
            [new(CapabilityKind.Skill, "note-skill", new("note-skill", "Draft a note", "Review the note.", [new("format", "short", null)])),
             new(CapabilityKind.Tool, "local-fixture-tool", null)], DateTimeOffset.UtcNow) { PublisherId = Guid.NewGuid(), CatalogRevision = 1 };
        var missing = template with { Capabilities = [.. template.Capabilities, new(CapabilityKind.Tool, "uninstalled-tool", null)] };
        var failure = await Assert.ThrowsAsync<RemoteAgentTemplateImportException>(() => workspace.ImportRemoteAgentTemplateAsync(source, missing, before.Workspace));
        Assert.Contains("uninstalled-tool", failure.Message, StringComparison.Ordinal);
        var writes = services.GetRequiredService<AgentConfigurationWriteContext>();
        var editingScope = services.GetRequiredService<AgentConfigurationChanges>().Scope;
        var operation = new EditingOperationIdentity(Guid.NewGuid(), Guid.NewGuid());
        using (writes.Enter(editingScope, operation, _ => ValueTask.FromException(new UnauthorizedAccessException()))) {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => workspace.ImportRemoteAgentTemplateAsync(source, template, before.Workspace));
        }
        var wrongWorkspace = AgentExecutionActivityWorkspaceIdentity.CreateHostLifetime(before.Workspace.WorkspaceScope);
        await Assert.ThrowsAsync<RemoteAgentTemplateImportException>(() => workspace.ImportRemoteAgentTemplateAsync(source, template, wrongWorkspace));
        var refused = (await workspace.LoadCatalogSnapshotAsync()).Snapshot.Catalog;
        Assert.Equal(before.Snapshot.Catalog.Agents.Count, refused.Agents.Count);
        Assert.Equal(before.Snapshot.Catalog.Capabilities.Count, refused.Capabilities.Count);
        var external = await workspace.ProvisionAgentByExternalKeyAsync(new($"remote-instance-{source:N}", template.Id.ToString("N"),
            "pre-existing-external-agent", null, new() { Name = "Existing independently provisioned local agent" }));
        var feed = services.GetRequiredService<EditingChangeFeed>();
        using var subscriber = feed.Subscribe(new(editingScope, EditingOwner.AgentDefinition, null));
        Guid first;
        using (writes.Enter(editingScope, operation, _ => ValueTask.CompletedTask)) {
            first = await workspace.ImportRemoteAgentTemplateAsync(source, template, before.Workspace);
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var change = Assert.Single((await subscriber.ReadAsync(timeout.Token)).Changes);
        Assert.Equal(first, change.Resource.ResourceId);
        Assert.Equal(operation.OperationId, change.OperationId);
        Assert.NotEqual(external.AgentId, first);
        var editor = await workspace.GetAgentEditorAsync(first);
        Assert.False(editor.IsTemplate);
        Assert.False(editor.SharedTemplate);
        Assert.True(editor.ApiAvailable);
        Assert.Null(editor.ProviderProfileId);
        Assert.Empty(editor.AllowedSecretReferences);
        Assert.Equal(AgentLifecycleStatus.Draft, editor.Status);
        Assert.True(editor.Permissions.RequiresApprovalForExternalCalls);
        Assert.Contains(tool, editor.SelectedCapabilityIds);
        var skill = (await workspace.ListCapabilitiesAsync()).Single(item => item.Id != tool && editor.SelectedCapabilityIds.Contains(item.Id));
        Assert.False(skill.IsBuiltIn);
        var runtime = new SkillCapabilityBuilder(host.RootPath, new PhysicalFileSystemPathPolicyFactory(),
            new ServiceProviderRegisteredCapabilityServiceSource(services, new HashSet<Type>()), NullLoggerFactory.Instance);
        var loaded = Assert.Single(runtime.ResolveInlineSkills([skill]));
        Assert.Contains("Review the note.", await loaded.GetContentAsync(), StringComparison.Ordinal);
        var format = await loaded.GetResourceAsync("format");
        Assert.NotNull(format);
        Assert.Equal("short", await format.ReadAsync());
        Assert.Empty(runtime.ResolveScriptExecutionPolicies([skill]));
        editor.Instructions = "My local changes";
        await workspace.SaveAgentAsync(editor);
        Assert.Equal(first, await workspace.ImportRemoteAgentTemplateAsync(source, template, before.Workspace));
        Assert.Equal("My local changes", (await workspace.GetAgentEditorAsync(first)).Instructions);
        var second = await workspace.ImportRemoteAgentTemplateAsync(Guid.NewGuid(), template, before.Workspace);
        Assert.NotEqual(first, second);
        Assert.Equal(template.Instructions, (await workspace.GetAgentEditorAsync(second)).Instructions);
        var repointed = await workspace.ImportRemoteAgentTemplateAsync(source, template with { PublisherId = Guid.NewGuid() }, before.Workspace);
        Assert.NotEqual(first, repointed);
        var current = await workspace.GetAgentEditorAsync(first);
        await workspace.DeleteAgentAsync(first, Assert.IsType<DateTimeOffset>(current.ExpectedUpdatedAtUtc));
        var importedAgain = await workspace.ImportRemoteAgentTemplateAsync(source, template, before.Workspace);
        Assert.NotEqual(first, importedAgain);
        Assert.NotEqual(external.AgentId, importedAgain);
        Assert.Equal(external.AgentId, (await workspace.GetAgentByExternalKeyAsync(external.Namespace, external.Key)).AgentId);
    }
}
