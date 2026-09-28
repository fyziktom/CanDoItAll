using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Api.ApiUserSessionIntegrationTests;

namespace CanDoItAll.Tests.Integration.Api;

public sealed class ApiSectionPermissionsTests {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Each_section_read_grant_allows_its_real_read_and_denies_unrelated_reads_and_writes() {
        var password = Password();
        await using var host = await CreateHostAsync(password);
        var admin = await LoginAsync(host.Client, "admin", password);
        SetToken(host.Client, admin.Token);
        var user = await CreateUserAsync(host.Client, "section-reader", password, []);
        var sections = new[] {
            (ApiAccessScopeNames.ReadProjects, "/api/projects"),
            (ApiAccessScopeNames.ReadAgents, "/api/agents/"),
            (ApiAccessScopeNames.ReadWorkflows, "/api/workflows/templates"),
            (ApiAccessScopeNames.ReadProcesses, "/api/processes/definitions"),
            (ApiAccessScopeNames.ReadCrmHr, "/api/crm-hr/parties"),
            (ApiAccessScopeNames.ReadPlugins, "/api/plugins/catalog"),
            (ApiAccessScopeNames.ReadPrompts, "/api/prompt-gallery/items"),
            (ApiAccessScopeNames.ReadWorkspaceSettings, "/api/settings/workspace"),
            (ApiAccessScopeNames.ReadMemoryProviders, "/api/memory-providers"),
            (ApiAccessScopeNames.ReadLlmChats, "/api/llm-chats")
        };
        var proposed = new { workspaceName = "Forbidden mutation", defaultProviderProfileId = (Guid?)null,
            defaultPromptOutputFormat = "Markdown", currencyCode = "USD", currencyCultureName = "en-US", notes = "No change allowed" };
        foreach (var (grant, path) in sections) {
            SetToken(host.Client, admin.Token);
            user = await UpdateAsync(host.Client, user, true, [grant]);
            var session = await LoginAsync(host.Client, user.UserName, password);
            SetToken(host.Client, session.Token);
            foreach (var (otherGrant, otherPath) in sections) {
                using var response = await host.Client.GetAsync(otherPath);
                var expected = grant == otherGrant ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
                Assert.True(response.StatusCode == expected, $"{grant} -> {otherPath}: expected {expected}, actual {response.StatusCode}");
                Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            }
            using var write = await host.Client.PutAsJsonAsync("/api/settings/workspace", proposed);
            Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
            using var management = await host.Client.PostAsJsonAsync("/api/access/users", new ApiUserCreateRequest("forbidden", "Forbidden", password, true, []));
            Assert.Equal(HttpStatusCode.Forbidden, management.StatusCode);
        }
        await using var scope = host.App.Services.CreateAsyncScope();
        Assert.NotEqual(proposed.workspaceName, (await scope.ServiceProvider.GetRequiredService<WorkspaceService>().GetSettingsAsync()).WorkspaceName);
    }

    [Fact]
    public async Task New_process_reads_and_workspace_settings_use_current_catalog_and_persist_authorized_changes() {
        var password = Password();
        await using var host = await CreateHostAsync(password);
        var admin = await LoginAsync(host.Client, "admin", password);
        SetToken(host.Client, admin.Token);
        await CreateUserAsync(host.Client, "business", password,
            [ApiAccessScopeNames.ReadProcesses, ApiAccessScopeNames.ReadWorkspaceSettings, ApiAccessScopeNames.WriteWorkspaceSettings]);
        var session = await LoginAsync(host.Client, "business", password);
        SetToken(host.Client, session.Token);
        var catalog = (await host.Client.GetFromJsonAsync<JsonObject>("/api/processes/definitions"))!;
        var items = catalog["items"]!.AsArray();
        Assert.NotEmpty(items);
        var keyNode = items[0]!["key"]!;
        var key = keyNode is JsonObject keyObject ? keyObject["value"]!.GetValue<string>() : keyNode.GetValue<string>();
        foreach (var suffix in new[] { "", "/roles", "/steps" }) {
            using var response = await host.Client.GetAsync("/api/processes/definitions/" + Uri.EscapeDataString(key) + suffix);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotEmpty((await response.Content.ReadFromJsonAsync<JsonObject>())!);
        }
        using var missing = await host.Client.GetAsync("/api/processes/definitions/missing-definition");
        using var invalid = await host.Client.GetAsync("/api/processes/definitions?scopeFilter=999");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var proposed = new { workspaceName = "API saved workspace", defaultProviderProfileId = (Guid?)null,
            defaultPromptOutputFormat = "Markdown", currencyCode = "USD", currencyCultureName = "en-US", notes = "Persisted through the application owner" };
        using var saved = await host.Client.PutAsJsonAsync("/api/settings/workspace", proposed);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(proposed.workspaceName, (await saved.Content.ReadFromJsonAsync<WorkspaceSettingsModel>())!.WorkspaceName);
        Assert.Equal(proposed.notes, (await host.Client.GetFromJsonAsync<WorkspaceSettingsModel>("/api/settings/workspace"))!.Notes);
        using var rejected = await host.Client.PutAsJsonAsync("/api/settings/workspace", proposed with { currencyCode = "invalid" });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("USD", (await host.Client.GetFromJsonAsync<WorkspaceSettingsModel>("/api/settings/workspace"))!.CurrencyCode);
    }

    [Fact]
    public async Task Template_drafts_are_separate_persisted_graphs_with_approval_and_read_only_users_cannot_create_them() {
        var password = Password();
        await using var host = await CreateHostAsync(password);
        var admin = await LoginAsync(host.Client, "admin", password);
        SetToken(host.Client, admin.Token);
        var user = await CreateUserAsync(host.Client, "workflow-writer", password, [ApiAccessScopeNames.ReadWorkflows]);
        var session = await LoginAsync(host.Client, user.UserName, password);
        SetToken(host.Client, session.Token);
        var templates = (await host.Client.GetFromJsonAsync<WorkflowTemplateCatalogItem[]>("/api/workflows/templates"))!;
        Assert.NotEmpty(templates);
        var path = "/api/workflows/templates/" + Uri.EscapeDataString(templates[0].Key) + "/drafts";
        using var denied = await host.Client.PostAsync(path, null);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        SetToken(host.Client, admin.Token);
        await UpdateAsync(host.Client, user, true, [ApiAccessScopeNames.ReadWorkflows, ApiAccessScopeNames.WriteWorkflows]);
        session = await LoginAsync(host.Client, user.UserName, password);
        SetToken(host.Client, session.Token);
        await using var scope = host.App.Services.CreateAsyncScope();
        var providers = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var provider = (await providers.ListProvidersAsync()).First(item => item.Purpose == ProviderProfilePurpose.Chat);
        await providers.UpdateProviderAsync(provider.Id, value => value with { IsEnabled = true });
        var components = scope.ServiceProvider.GetRequiredService<IWorkflowComponentLibraryService>();
        var before = (await components.ListComponentsAsync()).Select(item => item.Id).ToHashSet();
        using var first = await host.Client.PostAsync(path, null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var draft = (await first.Content.ReadFromJsonAsync<WorkflowDefinition>(JsonOptions))!;
        Assert.Equal(WorkflowLifecycleStatus.Draft, draft.Status);
        Assert.NotNull(await scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>().GetDefinitionAsync(draft.Id));
        var created = Assert.Single(await components.ListComponentsAsync(), item => !before.Contains(item.Id));
        Assert.True(created.Permissions.RequiresApprovalForExternalCalls);
        Assert.False(created.Permissions.CanUseTools);
        using var second = await host.Client.PostAsync(path, null);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.NotEqual(draft.Id, (await second.Content.ReadFromJsonAsync<WorkflowDefinition>(JsonOptions))!.Id);
        using var missing = await host.Client.PostAsync("/api/workflows/templates/missing-template/drafts", null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
