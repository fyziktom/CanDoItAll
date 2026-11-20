using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

[Collection(WorkspaceSettingsBrowserCollection.Name)]
[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class RemoteInstanceBrowserTests(PlaywrightAppFixture consumer) {
    private const string ConnectionName = "Regional template publisher";
    private const string DisabledConnectionName = "Disabled regional HTTPS connection";
    private const string CredentialName = "Regional template reader";
    private const string TemplateName = "Regional report author";
    private const string Instructions = "Prepare the regional report and show the underlying figures.";

    [Fact]
    public async Task Saved_connection_is_visible_to_providers_and_imports_only_the_selected_remote_template() {
        _ = consumer.OwnedDatabaseProfile;
        await using var publisher = new ApiAccessProductionHost();
        var password = ApiAccessProductionHost.Secret();
        await publisher.StartAsync(password);
        var admin = await ApiAccessSettingsBrowserTests.LoginAsync(publisher.Client, "admin", password);
        publisher.ProtectSecrets(admin.Token);
        publisher.Client.DefaultRequestHeaders.Authorization = new("Bearer", admin.Token);
        var author = await IssueAsync(publisher, "regional-template-author", ApiAccessScopeNames.ReadAgents, ApiAccessScopeNames.WriteAgents);
        var reader = await IssueAsync(publisher, "regional-template-reader", ApiAccessScopeNames.ReadSharedAgentTemplates);
        publisher.Client.DefaultRequestHeaders.Authorization = new("Bearer", author.Token);
        var templateId = await SaveAgentAsync(publisher.Client, TemplateName, isTemplate: true);
        const string privateName = "Regional private agent";
        await SaveAgentAsync(publisher.Client, privateName, isTemplate: false);
        Assert.Equal(ApiAccessScopeNames.ReadSharedAgentTemplates, Assert.Single(reader.Scopes));
        await ApiAccessSettingsBrowserTests.AssertStatusAsync(publisher.Client, reader.Token, "/api/shared-providers/v1/catalog", HttpStatusCode.Forbidden);

        using var client = new HttpClient { BaseAddress = new Uri(consumer.BaseUrl) };
        var localId = await SaveAgentAsync(client, "Local report author", isTemplate: false);
        await using var browser = await consumer.Browser.NewContextAsync(new() {
            ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1
        });
        var page = await browser.NewPageAsync();
        await page.GotoAsync(consumer.BaseUrl + "/settings?tab=secrets");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.GetByRole(AriaRole.Button, new() { Name = "New secret", Exact = true }).ClickAsync();
        await page.GetByTestId("secret-name").FillAsync(CredentialName);
        await page.GetByTestId("secret-kind").SelectOptionAsync(nameof(SecretKind.Token));
        await page.GetByTestId("settings-secret-value").FillAsync(reader.Token);
        await page.GetByTestId("secret-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("settings-operation").First).ToContainTextAsync("Committed");
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Remote instances") }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/settings\\?tab=remote-instances$"));
        await AddConnectionAsync(page, ConnectionName, publisher.HttpUrl);
        await Assertions.Expect(page.GetByText(ConnectionName, new() { Exact = true })).ToBeVisibleAsync();

        await page.GotoAsync(consumer.BaseUrl + "/agents?tab=providers");
        await page.GetByTestId("providers-connections").ClickAsync();
        var source = page.GetByTestId("shared-provider-source-card").Filter(new() { HasText = ConnectionName });
        await Assertions.Expect(source).ToBeVisibleAsync();
        var connectionId = Guid.Parse((await source.GetAttributeAsync("data-source-id"))!);
        await source.GetByTestId("shared-provider-source-edit").ClickAsync();
        await Assertions.Expect(page.GetByTestId("shared-provider-source-name")).ToHaveValueAsync(ConnectionName);
        await page.GetByTestId("shared-provider-source-cancel").ClickAsync();
        await SharedProviderTwoInstanceUiAcceptanceTests.CloseSharedConnectionsAsync(page);

        await page.GotoAsync(consumer.BaseUrl + "/agents?tab=agents");
        await page.GetByTestId("agents-catalog-import").ClickAsync();
        var dialog = page.GetByTestId("remote-agent-template-dialog");
        await AddConnectionAsync(page, DisabledConnectionName, publisher.HttpsUrl, disabled: true);
        var picker = dialog.GetByTestId("remote-instance-select");
        await Assertions.Expect(picker.Locator("option").Filter(new() { HasText = DisabledConnectionName })).ToBeDisabledAsync();
        await picker.SelectOptionAsync(connectionId.ToString("D"));
        var templates = dialog.GetByTestId("remote-agent-template-select");
        await templates.SelectOptionAsync(templateId.ToString("D"));
        await Assertions.Expect(templates.Locator("option").Filter(new() { HasText = privateName })).ToHaveCountAsync(0);
        await dialog.GetByTestId("remote-agent-template-import").ClickAsync();
        await Assertions.Expect(dialog.GetByTestId("remote-agent-template-accepted")).ToBeVisibleAsync();
        await dialog.GetByTestId("remote-agent-template-open").ClickAsync();
        await Assertions.Expect(page.GetByTestId("agents-catalog-name")).ToHaveValueAsync(TemplateName);

        var localAgents = (await client.GetFromJsonAsync<AgentDefinition[]>("/api/agents?includeTemplates=true"))!;
        var imported = Assert.Single(localAgents, item => item.Name == TemplateName);
        Assert.NotEqual(templateId, imported.Id);
        Assert.False(imported.IsTemplate);
        Assert.Equal(AgentLifecycleStatus.Draft, imported.Status);
        Assert.Equal(Instructions, imported.Instructions);
        Assert.Contains(localAgents, item => item.Id == localId && item.Name == "Local report author");
        Assert.DoesNotContain(localAgents, item => item.Name == privateName);
        var publisherOriginal = (await publisher.Client.GetFromJsonAsync<AgentEditorModel>($"/api/agents/{templateId:D}"))!;
        Assert.True(publisherOriginal.IsTemplate);
        Assert.Equal(Instructions, publisherOriginal.Instructions);

        var directory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "remote-instances");
        Directory.CreateDirectory(directory);
        await page.ScreenshotAsync(new() { Path = Path.Combine(directory, "imported-local-agent.png") });
        await page.GotoAsync(consumer.BaseUrl + "/settings?tab=remote-instances");
        await Assertions.Expect(page.GetByText(DisabledConnectionName, new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText(ConnectionName, new() { Exact = true })).ToBeVisibleAsync();
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
            Name = name, Instructions = Instructions, Summary = "Prepare a report from input data.", IsTemplate = isTemplate
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task AddConnectionAsync(IPage page, string name, string url, bool disabled = false) {
        await page.GetByTestId("remote-instance-add").ClickAsync();
        var editor = page.GetByTestId("remote-instance-editor");
        await editor.GetByTestId("remote-instance-name").FillAsync(name);
        await editor.GetByTestId("remote-instance-url").FillAsync(url);
        await editor.GetByTestId("remote-instance-credential").SelectOptionAsync(new SelectOptionValue { Label = CredentialName });
        if (disabled) {
            await editor.GetByTestId("remote-instance-enabled").UncheckAsync();
            await editor.GetByTestId("remote-instance-private").CheckAsync();
        }
        await editor.GetByTestId("remote-instance-save").ClickAsync();
        await Assertions.Expect(editor).ToHaveCountAsync(0);
    }
}
