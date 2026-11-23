using System.Net;
using System.Net.Http.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

[Collection(WorkspaceSettingsBrowserCollection.Name)]
[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed partial class RemoteInstanceBrowserTests(PlaywrightAppFixture consumer) {
    private const string Instructions = "Prepare the regional report and show the underlying figures.";

    [Fact]
    public async Task Saved_connection_is_visible_to_providers_and_imports_only_the_selected_remote_template() {
        var profile = consumer.OwnedDatabaseProfile;
        var runId = Guid.NewGuid().ToString("N");
        var connectionName = $"Regional template publisher {runId}";
        var disabledConnectionName = $"Disabled regional HTTPS connection {runId}";
        var templateName = $"Regional report author {runId}";
        var localName = $"Local report author {runId}";
        var privateName = $"Regional private agent {runId}";
        await using var publisher = new ApiAccessProductionHost();
        var password = ApiAccessProductionHost.Secret();
        await publisher.StartAsync(password);
        var admin = await ApiAccessSettingsBrowserTests.LoginAsync(publisher.Client, "admin", password);
        publisher.ProtectSecrets(admin.Token);
        publisher.Client.DefaultRequestHeaders.Authorization = new("Bearer", admin.Token);
        var author = await IssueAsync(publisher, "regional-template-author", ApiAccessScopeNames.ReadAgents, ApiAccessScopeNames.WriteAgents);
        var reader = await IssueAsync(publisher, "regional-template-reader", ApiAccessScopeNames.ReadSharedAgentTemplates);
        publisher.Client.DefaultRequestHeaders.Authorization = new("Bearer", author.Token);
        var templateId = await SaveAgentAsync(publisher.Client, templateName, isTemplate: true);
        await SaveAgentAsync(publisher.Client, privateName, isTemplate: true);
        await using var authorBrowser = await consumer.Browser.NewContextAsync(new() {
            IgnoreHTTPSErrors = true, ViewportSize = new() { Width = 1920, Height = 1080 }
        });
        var publisherAuthority = new Uri(publisher.HttpsUrl).Authority;
        await authorBrowser.RouteAsync("**/*", route => new Uri(route.Request.Url).Authority == publisherAuthority ? route.ContinueAsync() : route.AbortAsync());
        await authorBrowser.SetExtraHTTPHeadersAsync(new Dictionary<string, string> { ["Authorization"] = $"Bearer {author.Token}" });
        var authorPage = await authorBrowser.NewPageAsync();
        var catalogUrl = $"{publisher.HttpsUrl}/agents?tab=agents";
        var sharingUrl = $"{catalogUrl}&agentId={templateId:D}&agentSection=sharing";
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(authorPage, catalogUrl);
        var templateCard = authorPage.GetByRole(AriaRole.Button, new() { Name = $"Select {templateName}", Exact = true });
        await Assertions.Expect(templateCard).ToContainTextAsync("Local template", new() { Timeout = 60_000 });
        await Assertions.Expect(templateCard.GetByText("api", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(templateCard.GetByText("shared", new() { Exact = true })).ToHaveCountAsync(0);
        await templateCard.DblClickAsync();
        var authorEditor = authorPage.GetByTestId("agents-details-dialog");
        await authorEditor.GetByRole(AriaRole.Tab, new() { Name = "Sharing", Exact = true }).ClickAsync();
        await Assertions.Expect(authorPage).ToHaveURLAsync(sharingUrl);
        var publication = authorEditor.GetByTestId("definition-sharing-template");
        var apiAvailability = authorEditor.GetByTestId("definition-sharing-api");
        await Assertions.Expect(apiAvailability).ToBeCheckedAsync();
        await Assertions.Expect(publication).Not.ToBeCheckedAsync();
        await apiAvailability.UncheckAsync();
        await publication.CheckAsync();
        await authorEditor.GetByTestId("agents-catalog-save").ClickAsync();
        await authorPage.GetByText("Agent saved", new() { Exact = true }).First.WaitForAsync(new() { Timeout = 60_000 });
        Assert.Equal(ApiAccessScopeNames.ReadSharedAgentTemplates, Assert.Single(reader.Scopes));
        await ApiAccessSettingsBrowserTests.AssertStatusAsync(publisher.Client, reader.Token, "/api/shared-providers/v1/catalog", HttpStatusCode.Forbidden);

        using var client = new HttpClient { BaseAddress = new Uri(consumer.BaseUrl) };
        var localId = await SaveAgentAsync(client, localName, isTemplate: false);
        await using var seed = await TestApplicationBootstrap.BuildServiceProviderAsync(profile,
            "RemoteInstances.Browser.Seed", TestSchemaBootstrapModules.Full);
        await using var scope = seed.CreateAsyncScope();
        const string modelName = "regional-report-model";
        var substitute = await scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>().SaveProviderAsync(new() {
            Name = $"Local report model {runId}", Kind = ProviderKind.Ollama, Transport = ProviderTransportKind.ChatCompletions,
            BaseUrl = "http://127.0.0.1:11434", DefaultModel = modelName
        });
        var credential = await scope.ServiceProvider.GetRequiredService<SecretService>().SaveAsync(new SecretEditorModel {
            Name = $"Regional template reader {runId}",
            Kind = SecretKind.Token,
            SecretValue = reader.Token,
            Scope = "workspace"
        });
        Assert.True(credential.IsSuccess);
        await using var browser = await consumer.Browser.NewContextAsync(new() {
            ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1
        });
        var page = await browser.NewPageAsync();
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page, consumer.BaseUrl + "/settings");
        await page.GetByRole(AriaRole.Button, new() { Name = "Remote instances", Exact = true }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(consumer.BaseUrl + "/settings?tab=remote-instances");
        await AddConnectionAsync(page, connectionName, publisher.HttpUrl, credential.Value);
        await Assertions.Expect(page.GetByText(connectionName, new() { Exact = true })).ToBeVisibleAsync();

        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page, consumer.BaseUrl + "/agents?tab=providers");
        await page.GetByTestId("provider-editor-tabs").WaitForAsync();
        await page.GetByTestId("providers-connections").ClickAsync();
        var connections = page.GetByTestId("shared-provider-connections-dialog");
        await connections.GetByTestId("shared-provider-source-add").WaitForAsync();
        var source = connections.GetByTestId("shared-provider-source-card").Filter(new() { HasText = connectionName });
        await source.WaitForAsync();
        var connectionId = Guid.Parse((await source.GetAttributeAsync("data-source-id"))!);
        await source.GetByTestId("shared-provider-source-edit").ClickAsync();
        await Assertions.Expect(page.GetByTestId("shared-provider-source-name")).ToHaveValueAsync(connectionName);
        await page.GetByTestId("shared-provider-source-cancel").ClickAsync();
        await SharedProviderTwoInstanceUiAcceptanceTests.CloseSharedConnectionsAsync(page);

        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page, consumer.BaseUrl + "/agents?tab=agents");
        await page.GetByTestId("agents-catalog-import").ClickAsync();
        var dialog = page.GetByTestId("remote-template-import-dialog");
        await dialog.WaitForAsync();
        await AddConnectionAsync(page, disabledConnectionName, publisher.HttpsUrl, credential.Value, disabled: true);
        var picker = dialog.GetByTestId("remote-instance-select");
        await Assertions.Expect(picker.Locator("option").Filter(new() { HasText = disabledConnectionName })).ToBeDisabledAsync();
        await picker.SelectOptionAsync(connectionId.ToString("D"));
        var row = dialog.GetByTestId("remote-template-row").Filter(new() { HasText = templateName });
        await Assertions.Expect(row.GetByTestId("remote-template-avatar")).ToHaveAttributeAsync("src", AgentAvatarImageCatalog.BundledAvatarUrls[0]);
        await Assertions.Expect(dialog.GetByTestId("remote-template-row").Filter(new() { HasText = privateName })).ToHaveCountAsync(0);
        var checkbox = row.GetByTestId("remote-template-select");
        await checkbox.CheckAsync();
        await dialog.GetByTestId("remote-template-import").ClickAsync();
        var model = page.GetByTestId("remote-template-model-dialog");
        await Assertions.Expect(model).ToBeVisibleAsync();
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        await model.GetByTestId("remote-template-model-cancel").ClickAsync();
        await Assertions.Expect(checkbox).ToBeCheckedAsync();
        await dialog.GetByTestId("remote-template-import").ClickAsync();
        await model.GetByTestId("remote-template-provider").SelectOptionAsync(substitute.ToString("D"));
        await model.GetByTestId("remote-template-model").SelectOptionAsync(modelName);
        await model.GetByTestId("remote-template-model-confirm").ClickAsync();
        await row.GetByTestId("remote-template-accepted").WaitForAsync();
        await row.GetByTestId("remote-template-open").ClickAsync();
        await page.GetByTestId("agents-catalog-name").WaitForAsync();
        await Assertions.Expect(page.GetByTestId("agents-catalog-name")).ToHaveValueAsync(templateName);
        await Assertions.Expect(page.GetByTestId("agents-managed-tags")).ToHaveTextAsync("api");

        var localAgents = (await client.GetFromJsonAsync<AgentDefinition[]>("/api/agents?includeTemplates=true"))!;
        var imported = Assert.Single(localAgents, item => item.Name == templateName);
        Assert.NotEqual(templateId, imported.Id);
        Assert.False(imported.IsTemplate);
        Assert.False(imported.SharedTemplate);
        Assert.True(imported.ApiAvailable);
        Assert.Equal(["api"], imported.Tags);
        Assert.Equal(AgentLifecycleStatus.Draft, imported.Status);
        Assert.Equal(Instructions, imported.Instructions);
        Assert.Equal(AgentAvatarImageCatalog.BundledAvatarUrls[0], imported.AvatarImageUrl);
        Assert.Equal(substitute, imported.ProviderProfileId);
        Assert.Equal(modelName, imported.Model);
        Assert.Contains(localAgents, item => item.Id == localId && item.Name == localName);
        Assert.DoesNotContain(localAgents, item => item.Name == privateName);
        using var publisherOriginal = await publisher.Client.GetAsync($"/api/agents/{templateId:D}");
        Assert.Equal(HttpStatusCode.NotFound, publisherOriginal.StatusCode);
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(authorPage, sharingUrl);
        await Assertions.Expect(apiAvailability).Not.ToBeCheckedAsync();
        await Assertions.Expect(publication).ToBeCheckedAsync();
        await authorEditor.GetByRole(AriaRole.Tab, new() { Name = "Identity", Exact = true }).ClickAsync();
        await Assertions.Expect(authorEditor.GetByTestId("agents-managed-tags")).ToHaveTextAsync("shared");
        await authorEditor.GetByRole(AriaRole.Tab, new() { Name = "Sharing", Exact = true }).ClickAsync();
        await publication.UncheckAsync();
        await authorEditor.GetByTestId("agents-catalog-save").ClickAsync();
        await authorPage.GetByText("Agent saved", new() { Exact = true }).First.WaitForAsync(new() { Timeout = 60_000 });
        await ApiAccessSettingsBrowserTests.AssertStatusAsync(publisher.Client, reader.Token,
            RemoteAgentTemplateRoutes.Details(templateId), HttpStatusCode.NotFound);

        var directory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "remote-instances");
        Directory.CreateDirectory(directory);
        await page.ScreenshotAsync(new() { Path = Path.Combine(directory, "imported-local-agent.png") });
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page, consumer.BaseUrl + "/settings?tab=remote-instances");
        await Assertions.Expect(page.GetByText(disabledConnectionName, new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText(connectionName, new() { Exact = true })).ToBeVisibleAsync();
    }

    private static async Task<ApiTokenIssueResult> IssueAsync(ApiAccessProductionHost publisher, string subject, params string[] scopes) {
        using var response = await publisher.Client.PostAsJsonAsync("/api/access/tokens", new ApiTokenIssueRequest {
            Subject = subject, Scopes = [.. scopes]
        });
        response.EnsureSuccessStatusCode();
        var issued = Assert.IsType<ApiTokenIssueResult>(await response.Content.ReadFromJsonAsync<ApiTokenIssueResult>());
        publisher.ProtectSecrets(issued.Token);
        return issued;
    }

    private static async Task<Guid> SaveAgentAsync(HttpClient client, string name, bool isTemplate) {
        using var response = await client.PostAsJsonAsync("/api/agents", new AgentEditorModel {
            Name = name, Instructions = Instructions, Summary = "Prepare a report from input data.", IsTemplate = isTemplate,
            AvatarImageUrl = isTemplate ? AgentAvatarImageCatalog.BundledAvatarUrls[0] : string.Empty
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task AddConnectionAsync(IPage page, string name, string url, Guid credentialId, bool disabled = false) {
        await page.GetByTestId("remote-instance-add").ClickAsync();
        var editor = page.GetByTestId("remote-instance-editor");
        await editor.GetByTestId("remote-instance-name").FillAsync(name);
        await editor.GetByTestId("remote-instance-url").FillAsync(url);
        await editor.GetByTestId("remote-instance-credential").SelectOptionAsync(credentialId.ToString("D"));
        if (disabled) {
            await editor.GetByTestId("remote-instance-enabled").UncheckAsync();
            await editor.GetByTestId("remote-instance-private").CheckAsync();
        }
        await editor.GetByTestId("remote-instance-save").ClickAsync();
        await Assertions.Expect(editor).ToHaveCountAsync(0);
    }
}
