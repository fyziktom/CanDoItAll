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
        const string model = "regional-editor";
        var avatar = AgentAvatarImageCatalog.BundledAvatarUrls[1];
        await using var publisher = new ApiAccessProductionHost();
        var password = ApiAccessProductionHost.Secret();
        await publisher.StartAsync(password);
        var admin = await ApiAccessSettingsBrowserTests.LoginAsync(publisher.Client, "admin", password);
        publisher.ProtectSecrets(admin.Token);
        publisher.Client.DefaultRequestHeaders.Authorization = new("Bearer", admin.Token);
        var author = await IssueAsync(publisher, "definition-publisher", ApiAccessScopeNames.ReadLlmChats, ApiAccessScopeNames.ManageLlmChats,
            ApiAccessScopeNames.ReadWorkflows, ApiAccessScopeNames.WriteWorkflows);
        var reader = await IssueAsync(publisher, "shared-definitions-reader", ApiAccessScopeNames.ReadSharedSimpleChats,
            ApiAccessScopeNames.ReadSharedWorkflows, ApiAccessScopeNames.ReadSharedProviderCatalog);
        publisher.Client.DefaultRequestHeaders.Authorization = new("Bearer", author.Token);
        await using var publisherServices = await TestApplicationBootstrap.BuildServiceProviderAsync(publisher.OwnedDatabaseProfile,
            "RemoteDefinitions.Publisher", TestSchemaBootstrapModules.Full);
        await using var publisherScope = publisherServices.CreateAsyncScope();
        var origin = publisherScope.ServiceProvider;
        var provider = await origin.GetRequiredService<IProviderProfileRegistry>().SaveProviderAsync(new() {
            Name = $"Regional model {marker}", Kind = ProviderKind.Ollama, Transport = ProviderTransportKind.ChatCompletions,
            BaseUrl = "http://127.0.0.1:11434", DefaultModel = model, SuggestedModels = [model]
        });
        var sharing = origin.GetRequiredService<Sharing.ISharedProviderManagementService>();
        var snapshot = await sharing.GetProfileSharingAsync(provider);
        var publication = await sharing.SetPublicationAsync(provider, Sharing.SharedProviderPublicationAction.Publish, null, snapshot.ProviderConcurrencyToken!.Value);
        var chatId = await CreateChatAsync(publisher.Client, chatName, avatar, provider, model);
        var privateName = $"Private reviewer {marker}";
        await CreateChatAsync(publisher.Client, privateName, avatar, provider, model);
        var component = await origin.GetRequiredService<IWorkflowComponentLibraryService>().SaveComponentAsync(new(null,
            $"Review instructions {marker}", provider, model, WorkflowModality.Text, new(null, 150, false, ""), Instructions,
            WorkflowValueShape.Text, WorkflowValueShape.Text, AgentPermissionsPolicy.Default));
        var graph = WorkflowDefinitionValidator.CreateComponentValidationDefinition(component);
        var definition = await origin.GetRequiredService<IWorkflowCatalogService>().SaveDefinitionAsync(new(null, null, workflowName,
            "Review a regional report.", WorkflowLifecycleStatus.Draft, graph.Graph, graph.RuntimePolicy));

        await using var authorBrowser = await consumer.Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        var host = new Uri(publisher.HttpsUrl).Authority;
        await authorBrowser.RouteAsync("**/*", route => new Uri(route.Request.Url).Authority == host ? route.ContinueAsync() : route.AbortAsync());
        await authorBrowser.SetExtraHTTPHeadersAsync(new Dictionary<string, string> { ["Authorization"] = $"Bearer {author.Token}" });
        var authorPage = await authorBrowser.NewPageAsync();
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(authorPage, publisher.HttpsUrl + ChatDefinitions);
        await authorPage.GetByTestId($"llm-chat-definition-sharing-{chatId:D}").ClickAsync();
        var publicationDialog = authorPage.GetByTestId("llm-chat-definition-publication-dialog");
        await publicationDialog.GetByTestId("llm-chat-definition-set-publication").ClickAsync();
        await Assertions.Expect(publicationDialog).ToHaveCountAsync(0);
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(authorPage, $"{publisher.HttpsUrl}{WorkflowCatalog}&workflowId={definition.Id.Value:D}");
        await authorPage.GetByTestId("workflows-remote-sharing").ClickAsync();
        publicationDialog = authorPage.GetByTestId("workflows-remote-publication-dialog");
        await publicationDialog.GetByTestId("workflows-remote-set-publication").ClickAsync();
        await Assertions.Expect(publicationDialog).ToHaveCountAsync(0);

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
        var observed = (await remoteProviders.ListSourcesAsync()).Single(item => item.Source.Id == source.Id).Observation;
        var sync = await remoteProviders.SynchronizeSourceAsync(source.Id, observed, new HashSet<SharedProviderPublicationId> { publication.Publication!.PublicId });
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
        using var response = await client.PostAsJsonAsync("/api/llm-chats", new {
            name, summary = "Review the regional report.", avatarImageUrl = avatar, systemPrompt = Instructions,
            providerProfileId = provider, model, tags = new[] { "regional" }, revisionReason = "Browser import fixture"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}
