using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.RemoteInstances;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Sharing = CanDoItAll.Modules.AgentFramework.ProviderManagement;

namespace CanDoItAll.Tests.Playwright.Smoke;

public sealed partial class RemoteInstanceBrowserTests {
    private const string ChatDefinitions = "/agents?tab=simple-chats&simpleChatView=definitions";
    private const string WorkflowCatalog = "/agents/workflows?tab=workflows";

    [Fact]
    public async Task Opted_in_chats_and_workflows_use_the_originating_shared_provider_on_the_consumer() {
        var marker = Guid.NewGuid().ToString("N");
        var chatName = $"Shared reviewer {marker}";
        var workflowName = $"Review workflow {marker}";
        var providerName = $"Regional model {marker}";
        const string model = "regional-editor";
        var avatar = AgentAvatarImageCatalog.BundledAvatarUrls[1];
        await using var publisher = new ApiAccessProductionHost();
        var password = ApiAccessProductionHost.Secret();
        await publisher.StartAsync(password);
        var admin = await ApiAccessSettingsBrowserTests.LoginAsync(publisher.Client, "admin", password);
        publisher.ProtectSecrets(admin.Token);
        publisher.Client.DefaultRequestHeaders.Authorization = new("Bearer", admin.Token);
        var author = await IssueAsync(publisher, "definition-publisher", ApiAccessScopeNames.ReadLlmChats, ApiAccessScopeNames.ManageLlmChats,
            ApiAccessScopeNames.ReadWorkflows, ApiAccessScopeNames.WriteWorkflows, ApiAccessScopeNames.ReadAgents, ApiAccessScopeNames.WriteAgents);
        var reader = await IssueAsync(publisher, "shared-definitions-reader", ApiAccessScopeNames.ReadSharedSimpleChats,
            ApiAccessScopeNames.ReadSharedWorkflows, ApiAccessScopeNames.ReadSharedProviderCatalog);
        publisher.Client.DefaultRequestHeaders.Authorization = new("Bearer", author.Token);
        var provider = await CreateAsync<Guid>(publisher.Client, "/api/agents/providers", new ProviderProfileEditorModel {
            Name = providerName, Kind = ProviderKind.Ollama, Transport = ProviderTransportKind.ChatCompletions,
            BaseUrl = "http://127.0.0.1:11434", DefaultModel = model, SuggestedModels = [model],
            ModelPrices = [new() { Model = model, TariffKind = ProviderTariffKind.ExplicitFree }]
        });
        var chatId = await CreateChatAsync(publisher.Client, chatName, avatar, provider, model);
        var privateName = $"Private reviewer {marker}";
        await CreateChatAsync(publisher.Client, privateName, avatar, provider, model);
        var component = await CreateAsync<LlmCallComponent>(publisher.Client, "/api/workflows/components", new LlmCallComponentSaveRequest(null,
            $"Review instructions {marker}", provider, model, WorkflowModality.Text, new(null, 150, false, ""), Instructions,
            WorkflowValueShape.Text, WorkflowValueShape.Text, AgentPermissionsPolicy.Default));
        var graph = WorkflowDefinitionValidator.CreateComponentValidationDefinition(component);
        var definition = await CreateAsync<WorkflowDefinition>(publisher.Client, "/api/workflows/definitions", new WorkflowDefinitionSaveRequest(null, null, workflowName,
            "Review a regional report.", WorkflowLifecycleStatus.Draft, graph.Graph, graph.RuntimePolicy));

        await using var authorBrowser = await consumer.Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        var host = new Uri(publisher.HttpsUrl).Authority;
        await authorBrowser.RouteAsync("**/*", route => new Uri(route.Request.Url).Authority == host ? route.ContinueAsync() : route.AbortAsync());
        await authorBrowser.SetExtraHTTPHeadersAsync(new Dictionary<string, string> { ["Authorization"] = $"Bearer {author.Token}" });
        var authorPage = await authorBrowser.NewPageAsync();
        await SharedProviderMetadataUiChecks.OpenProviderAsync(authorPage, publisher.HttpsUrl, providerName);
        await authorPage.GetByTestId("provider-editor-tab-sharing").ClickAsync();
        await authorPage.GetByTestId("shared-provider-publish").ClickAsync();
        await Assertions.Expect(authorPage.GetByTestId("shared-provider-publication-status")).ToHaveTextAsync("Published");
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(authorPage, publisher.HttpsUrl + ChatDefinitions);
        await authorPage.GetByTestId($"llm-chat-definition-select-{chatId:D}").ClickAsync();
        var chatEditor = authorPage.GetByTestId("llm-chat-definition-editor-dialog");
        await chatEditor.GetByTestId("llm-chat-definition-tab-sharing").ClickAsync();
        await Assertions.Expect(chatEditor.GetByTestId("definition-sharing-api")).ToBeCheckedAsync();
        await Assertions.Expect(chatEditor.GetByTestId("definition-sharing-template")).Not.ToBeCheckedAsync();
        await chatEditor.GetByTestId("definition-sharing-api").UncheckAsync();
        await chatEditor.GetByTestId("definition-sharing-template").CheckAsync();
        await chatEditor.GetByTestId("llm-chat-definition-save-sharing").ClickAsync();
        await Assertions.Expect(chatEditor.GetByTestId("llm-chat-definition-sharing-saved")).ToBeVisibleAsync();
        using (var hidden = await publisher.Client.GetAsync($"/api/llm-chats/{chatId:D}")) {
            Assert.Equal(System.Net.HttpStatusCode.NotFound, hidden.StatusCode);
        }
        await chatEditor.GetByTestId("llm-chat-definition-editor-cancel").ClickAsync();
        await authorPage.GetByTestId($"llm-chat-definition-sharing-{chatId:D}").ClickAsync();
        await Assertions.Expect(chatEditor.GetByTestId("definition-sharing-api")).Not.ToBeCheckedAsync();
        await Assertions.Expect(chatEditor.GetByTestId("definition-sharing-template")).ToBeCheckedAsync();
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(authorPage, $"{publisher.HttpsUrl}/agents/workflows?tab=editor&workflowId={definition.Id.Value:D}");
        await authorPage.GetByTestId("workflow-canvas-tab-sharing").ClickAsync();
        await authorPage.GetByTestId("definition-sharing-api").UncheckAsync();
        await authorPage.GetByTestId("definition-sharing-template").CheckAsync();
        await authorPage.GetByTestId("workflow-sharing-save").ClickAsync();
        await Assertions.Expect(authorPage.GetByText("Sharing settings saved.", new() { Exact = true })).ToBeVisibleAsync();
        using var hiddenWorkflow = await publisher.Client.GetAsync($"/api/workflows/definitions/{definition.Id.Value:D}");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, hiddenWorkflow.StatusCode);

        await using var consumerServices = await TestApplicationBootstrap.BuildServiceProviderAsync(consumer.OwnedDatabaseProfile,
            "RemoteDefinitions.Consumer", TestSchemaBootstrapModules.Full);
        await using var consumerScope = consumerServices.CreateAsyncScope();
        var local = consumerScope.ServiceProvider;
        var credential = await local.GetRequiredService<SecretService>().SaveAsync(new SecretEditorModel {
            Name = $"Regional definitions {marker}", Kind = SecretKind.Token, SecretValue = reader.Token, Scope = "workspace"
        });
        Assert.True(credential.IsSuccess);
        var source = await local.GetRequiredService<IRemoteInstanceRegistry>().CreateAsync(Guid.NewGuid(),
            new($"Regional definitions {marker}", new(publisher.HttpUrl), credential.Value, true, true));
        var remoteProviders = local.GetRequiredService<Sharing.ISharedProviderManagementService>();
        var tested = await remoteProviders.TestSourceAsync(source.Id);
        Assert.True(tested.IsSuccessful, $"Shared provider discovery returned {tested.Outcome}.");
        var publication = Assert.Single(tested.Catalog!.Providers, item => item.DisplayName == providerName);
        var observed = (await remoteProviders.ListSourcesAsync()).Single(item => item.Source.Id == source.Id).Observation;
        var sync = await remoteProviders.SynchronizeSourceAsync(source.Id, observed, new HashSet<SharedProviderPublicationId> { publication.PublicationId });
        Assert.True(sync.IsSuccessful, $"Shared provider synchronization returned {sync.Outcome}.");
        var mirror = Assert.Single((await remoteProviders.ListSourcesAsync()).Single(item => item.Source.Id == source.Id).Imports);
        await using var browser = await consumer.Browser.NewContextAsync();
        var page = await browser.NewPageAsync();
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page, consumer.BaseUrl + ChatDefinitions);
        await page.GetByTestId("llm-chat-definition-import").ClickAsync();
        var dialog = page.GetByTestId("remote-template-import-dialog");
        await dialog.GetByTestId("remote-instance-select").SelectOptionAsync(source.Id.ToString("D"));
        var chat = dialog.GetByTestId("remote-template-row").Filter(new() { HasText = chatName });
        await Assertions.Expect(chat.GetByTestId("remote-template-avatar")).ToHaveAttributeAsync("src", avatar);
        await Assertions.Expect(dialog.GetByText(privateName, new() { Exact = true })).ToHaveCountAsync(0);
        await chat.GetByTestId("remote-template-select").CheckAsync();
        await dialog.GetByTestId("remote-template-import").ClickAsync();
        await chat.GetByTestId("remote-template-accepted").WaitForAsync();
        await chat.GetByTestId("remote-template-open").ClickAsync();
        await Assertions.Expect(page.GetByTestId("llm-chat-definition-name")).ToHaveValueAsync(chatName);
        using var client = new HttpClient { BaseAddress = new(consumer.BaseUrl) };
        var chats = await client.GetFromJsonAsync<JsonElement>($"/api/llm-chats?search={Uri.EscapeDataString(chatName)}");
        var imported = Assert.Single(chats.GetProperty("items").EnumerateArray(), row => row.GetProperty("name").GetString() == chatName);
        Assert.NotEqual(chatId, imported.GetProperty("id").GetGuid());
        Assert.Equal(mirror.ProviderProfileId, imported.GetProperty("providerProfileId").GetGuid());
        Assert.Equal(mirror.DefaultModelId.Value, imported.GetProperty("model").GetString());
        Assert.Equal(avatar, imported.GetProperty("avatarImageUrl").GetString());

        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page, consumer.BaseUrl + WorkflowCatalog);
        await page.GetByTestId("workflows-remote-import").ClickAsync();
        await dialog.GetByTestId("remote-instance-select").SelectOptionAsync(source.Id.ToString("D"));
        var workflow = dialog.GetByTestId("remote-template-row").Filter(new() { HasText = workflowName });
        await workflow.GetByTestId("remote-template-select").CheckAsync();
        await dialog.GetByTestId("remote-template-import").ClickAsync();
        await workflow.GetByTestId("remote-template-accepted").WaitForAsync();
        await dialog.GetByTestId("remote-template-close").ClickAsync();
        var localCatalog = local.GetRequiredService<IWorkflowCatalogService>();
        var copied = Assert.Single(await localCatalog.ListDefinitionsAsync(), entry => entry.Name == workflowName);
        Assert.NotEqual(definition.Id, copied.Id);
        Assert.Equal(WorkflowLifecycleStatus.Draft, copied.Status);
        var workflowDetails = await localCatalog.GetDefinitionAsync(copied.Id);
        var localNode = Assert.Single(workflowDetails!.Definition.Graph.Nodes, node => node.Kind == WorkflowNodeKind.LlmCall);
        Assert.NotEqual(component.Id, localNode.Settings.ComponentId);
        Assert.Equal(mirror.ProviderProfileId, localNode.Settings.ProviderProfileId);
        Assert.Equal(mirror.DefaultModelId.Value, localNode.Settings.Model);
    }

    private static async Task<Guid> CreateChatAsync(HttpClient client, string name, string avatar, Guid provider, string model) {
        var chat = await CreateAsync<JsonElement>(client, "/api/llm-chats", new {
            name, summary = "Review the regional report.", avatarImageUrl = avatar, systemPrompt = Instructions,
            providerProfileId = provider, model, tags = new[] { "regional" }, revisionReason = "Browser import fixture"
        });
        return chat.GetProperty("id").GetGuid();
    }

    private static async Task<T> CreateAsync<T>(HttpClient client, string route, object request) {
        using var response = await client.PostAsJsonAsync(route, request);
        if (!response.IsSuccessStatusCode) {
            var detail = response.ReasonPhrase;
            if (response.Content.Headers.ContentType?.MediaType == "application/json") {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (json.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array) {
                    detail = string.Join(" ", errors.EnumerateArray().Select(error =>
                        $"{error.GetProperty("code").GetString()} {error.GetProperty("message").GetString()}"));
                }
            }
            throw new HttpRequestException($"POST {route} returned {(int)response.StatusCode}. {detail}", null, response.StatusCode);
        }
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
