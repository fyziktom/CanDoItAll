using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed class ApiAccessSettingsBrowserTests {
    [Fact]
    public async Task Real_settings_mutations_invalidate_sessions_and_preserve_machine_access_across_restart() {
        await using var host = new ApiAccessProductionHost();
        var adminPassword = ApiAccessProductionHost.Secret();
        var userPassword = ApiAccessProductionHost.Secret();
        var replacementPassword = ApiAccessProductionHost.Secret();
        host.ProtectSecrets(userPassword, replacementPassword);
        await host.StartAsync(adminPassword);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = true, ViewportSize = new() { Width = 1680, Height = 1050 } });
        var page = await context.NewPageAsync();
        await OpenSettingsAsync(page, host);
        await page.GetByTestId("api-token-subject").FillAsync("browser-machine");
        await page.GetByTestId("api-token-name").FillAsync("Browser machine");
        await page.GetByTestId("api-token-scopes").FillAsync(string.Join(' ', ApiAccessScopeNames.ReadWorkflows,
            ApiAccessScopeNames.ReadWorkspaceSettings, ApiAccessScopeNames.WriteWorkspaceSettings));
        await page.GetByTestId("api-token-create").ClickAsync();
        await page.GetByTestId("api-issued-token").WaitForAsync();
        var machine = await page.GetByTestId("api-issued-token").InputValueAsync();
        await AssertStatusAsync(host.Client, machine, "/api/workflows/templates", HttpStatusCode.OK);
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", machine);
        using var workspaceWrite = await host.Client.PutAsJsonAsync("/api/settings/workspace", new {
            workspaceName = "Original database workspace", defaultProviderProfileId = (Guid?)null,
            defaultPromptOutputFormat = "Markdown", currencyCode = "USD", currencyCultureName = "en-US", notes = "Database switch proof"
        });
        Assert.Equal(HttpStatusCode.OK, workspaceWrite.StatusCode);
        Assert.Equal("Original database workspace", (await host.Client.GetFromJsonAsync<WorkspaceSettingsModel>("/api/settings/workspace"))!.WorkspaceName);
        host.Client.DefaultRequestHeaders.Authorization = null;
        await page.GetByTestId("api-users-panel").WaitForAsync();
        await page.GetByTestId("api-user-create").ClickAsync();
        await page.GetByTestId("api-user-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("api-user-editor-error")).ToBeVisibleAsync();
        await page.GetByTestId("api-user-name").FillAsync("browser-user");
        await page.GetByTestId("api-user-display-name").FillAsync("Browser user");
        await page.GetByTestId("api-user-password").FillAsync(userPassword);
        await ChooseScopesAsync(page, ApiAccessScopeNames.ReadLlmChats, ApiAccessScopeNames.ReadWorkflows);
        await SaveOnceWithContendedStoreAsync(page, host);
        await AssertRefreshFailurePreservesCommittedUserAsync(page, host);
        var first = await LoginAsync(host.Client, "browser-user", userPassword);
        await AssertStatusAsync(host.Client, first.Token, "/api/workflows/templates", HttpStatusCode.OK);
        await AssertStatusAsync(host.Client, first.Token, "/api/llm-chats", HttpStatusCode.OK);
        await AssertStatusAsync(host.Client, first.Token, "/api/access/users", HttpStatusCode.Forbidden);

        await page.GetByTestId("api-user-edit").ClickAsync();
        var editingAdmin = await LoginAsync(host.Client, "admin", adminPassword);
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", editingAdmin.Token);
        var beforeConcurrentEdit = (await host.Client.GetFromJsonAsync<ApiUserDetails>($"/api/access/users/{first.UserId}"))!;
        using var concurrentEdit = await host.Client.PutAsJsonAsync($"/api/access/users/{first.UserId}", new ApiUserUpdateRequest(
            "browser-user", "Concurrent server edit", true, beforeConcurrentEdit.Scopes, beforeConcurrentEdit.Version));
        Assert.Equal(HttpStatusCode.OK, concurrentEdit.StatusCode);
        await page.GetByTestId("api-user-display-name").FillAsync("Stale browser edit");
        await page.GetByTestId("api-user-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("api-user-editor-error")).ToBeVisibleAsync();
        var afterConcurrentEdit = (await host.Client.GetFromJsonAsync<ApiUserDetails>($"/api/access/users/{first.UserId}"))!;
        Assert.Equal("Concurrent server edit", afterConcurrentEdit.DisplayName);
        Assert.Equal(beforeConcurrentEdit.Version + 1, afterConcurrentEdit.Version);
        host.Client.DefaultRequestHeaders.Authorization = null;
        await page.GetByTestId("api-user-dialog").Locator("footer").GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await page.GetByTestId("api-users-refresh").ClickAsync();
        await Assertions.Expect(page.GetByTestId("api-users-table")).ToContainTextAsync("Concurrent server edit");
        first = await LoginAsync(host.Client, "browser-user", userPassword);
        await page.GetByTestId("api-user-edit").ClickAsync();
        await ChooseScopesAsync(page, ApiAccessScopeNames.ReadLlmChats);
        await SaveUserAsync(page);
        await AssertStatusAsync(host.Client, first.Token, "/api/access/me", HttpStatusCode.Unauthorized);
        var reduced = await LoginAsync(host.Client, "browser-user", userPassword);
        await AssertStatusAsync(host.Client, reduced.Token, "/api/workflows/templates", HttpStatusCode.Forbidden);

        await page.GetByTestId("api-user-reset").ClickAsync();
        await page.GetByTestId("api-user-password").FillAsync(replacementPassword);
        await SaveUserAsync(page);
        await AssertStatusAsync(host.Client, reduced.Token, "/api/access/me", HttpStatusCode.Unauthorized);
        using var oldPassword = await host.Client.PostAsJsonAsync("/api/access/login", new ApiLoginRequest("browser-user", userPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        var reset = await LoginAsync(host.Client, "browser-user", replacementPassword);
        await page.GetByTestId("api-user-edit").ClickAsync();
        await page.GetByTestId("api-user-enabled").UncheckAsync();
        await SaveUserAsync(page);
        await AssertStatusAsync(host.Client, reset.Token, "/api/access/me", HttpStatusCode.Unauthorized);
        using var disabled = await host.Client.PostAsJsonAsync("/api/access/login", new ApiLoginRequest("browser-user", replacementPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, disabled.StatusCode);
        await page.GetByTestId("api-user-edit").ClickAsync();
        await page.GetByTestId("api-user-enabled").CheckAsync();
        await SaveUserAsync(page);
        await AssertStatusAsync(host.Client, reset.Token, "/api/access/me", HttpStatusCode.Unauthorized);
        var enabled = await LoginAsync(host.Client, "browser-user", replacementPassword);
        await AssertStatusAsync(host.Client, enabled.Token, "/api/llm-chats", HttpStatusCode.OK);
        await AssertStatusAsync(host.Client, machine, "/api/workflows/templates", HttpStatusCode.OK);
        var administrator = await LoginAsync(host.Client, "admin", adminPassword);

        await context.CloseAsync();
        await host.StartAsync(adminPassword, management: false, otherDatabase: true);
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", machine);
        Assert.NotEqual("Original database workspace", (await host.Client.GetFromJsonAsync<WorkspaceSettingsModel>("/api/settings/workspace"))!.WorkspaceName);
        host.Client.DefaultRequestHeaders.Authorization = null;
        await AssertStatusAsync(host.Client, administrator.Token, "/api/access/me", HttpStatusCode.OK);
        await AssertStatusAsync(host.Client, enabled.Token, "/api/llm-chats", HttpStatusCode.OK);
        await AssertStatusAsync(host.Client, reset.Token, "/api/access/me", HttpStatusCode.Unauthorized);
        await AssertStatusAsync(host.Client, administrator.Token, "/api/access/users", HttpStatusCode.NotFound);
        await using var afterRestart = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = true, ViewportSize = new() { Width = 1680, Height = 1050 } });
        page = await afterRestart.NewPageAsync();
        await OpenSettingsAsync(page, host);
        await Assertions.Expect(page.GetByTestId("api-effective-configuration")).ToContainTextAsync("disabled");
        await page.GetByTestId("api-user-delete").ClickAsync();
        await SaveUserAsync(page);
        await AssertStatusAsync(host.Client, enabled.Token, "/api/access/me", HttpStatusCode.Unauthorized);
        await page.GetByTestId("api-user-create").ClickAsync();
        await page.GetByTestId("api-user-name").FillAsync("browser-user");
        await page.GetByTestId("api-user-display-name").FillAsync("Recreated user");
        await page.GetByTestId("api-user-password").FillAsync(replacementPassword);
        await SaveUserAsync(page);
        var recreated = await LoginAsync(host.Client, "browser-user", replacementPassword);
        Assert.NotEqual(enabled.UserId, recreated.UserId);
        Assert.Equal([ApiAccessScopeNames.Session], recreated.Scopes);

        await page.GetByTestId("api-tokens-open").ClickAsync();
        await page.GetByTestId("api-token-revoke").ClickAsync();
        await page.GetByTestId("api-token-confirm").ClickAsync();
        await page.GetByTestId("api-token-confirmation").WaitForAsync(new() { State = WaitForSelectorState.Detached });
        await AssertStatusAsync(host.Client, machine, "/api/workflows/templates", HttpStatusCode.Unauthorized);
        await page.GetByTestId("api-tokens-dialog").Locator("footer").GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        var evidenceRoot = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "api-access");
        Directory.CreateDirectory(evidenceRoot);
        await page.GetByTestId("api-users-panel").ScreenshotAsync(new() { Path = Path.Combine(evidenceRoot, "settings-users-after-restart.png") });
        var privateStore = await File.ReadAllTextAsync(Path.Combine(host.ControlPlaneRoot, "api-users", "accounts.json"));
        Assert.True(!privateStore.Contains(replacementPassword, StringComparison.Ordinal), "The private account store must not contain the plaintext password.");
    }

    private static async Task OpenSettingsAsync(IPage page, ApiAccessProductionHost host) {
        var response = await page.GotoAsync(host.HttpsUrl + "/settings?tab=api-access");
        Assert.Equal(200, response!.Status);
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.GetByTestId("api-users-panel").WaitForAsync(new() { Timeout = 60000 });
    }

    private static async Task ChooseScopesAsync(IPage page, params string[] scopes) {
        await page.GetByTestId("api-user-scopes").ClickAsync();
        var dialog = page.GetByTestId("api-scopes-dialog");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Clear", Exact = true }).ClickAsync();
        await Assertions.Expect(dialog.Locator("input:checked")).ToHaveCountAsync(0);
        Assert.Equal(0, await dialog.Locator($"input[value='{ApiAccessScopeNames.Api}']").CountAsync());
        foreach (var scope in scopes) {
            await dialog.Locator($"input[value='{scope}']").CheckAsync();
        }
        await Assertions.Expect(dialog.GetByText($"{scopes.Length} selected", new() { Exact = true })).ToBeVisibleAsync();
        await page.GetByTestId("api-scopes-confirm").ClickAsync();
    }

    private static async Task SaveUserAsync(IPage page) {
        await page.GetByTestId("api-user-save").ClickAsync();
        await page.GetByTestId("api-user-dialog").WaitForAsync(new() { State = WaitForSelectorState.Detached });
        await Assertions.Expect(page.GetByTestId("api-users-message")).ToContainTextAsync("saved");
    }

    private static async Task SaveOnceWithContendedStoreAsync(IPage page, ApiAccessProductionHost host) {
        var accountPath = Path.Combine(host.ControlPlaneRoot, "api-users", "accounts.json");
        await using (var lease = new FileStream(accountPath + ".mutation.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) {
            await page.GetByTestId("api-user-save").ClickAsync();
            await Assertions.Expect(page.GetByTestId("api-user-save")).ToBeDisabledAsync();
            await page.GetByTestId("api-user-save").DispatchEventAsync("click");
            Assert.False(File.Exists(accountPath));
        }
        await page.GetByTestId("api-user-dialog").WaitForAsync(new() { State = WaitForSelectorState.Detached });
        await Assertions.Expect(page.GetByTestId("api-users-message")).ToContainTextAsync("saved");
        using var document = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(accountPath));
        var user = Assert.Single(document.RootElement.GetProperty("users").EnumerateArray());
        Assert.Equal(1, user.GetProperty("version").GetInt64());
    }

    private static async Task AssertRefreshFailurePreservesCommittedUserAsync(IPage page, ApiAccessProductionHost host) {
        var accountPath = Path.Combine(host.ControlPlaneRoot, "api-users", "accounts.json");
        var saved = await File.ReadAllBytesAsync(accountPath);
        await using (var lease = new FileStream(accountPath, FileMode.Open, FileAccess.Read, FileShare.None)) {
            await page.GetByTestId("api-users-refresh").ClickAsync();
            await Assertions.Expect(page.GetByTestId("api-users-error")).ToContainTextAsync("could not be refreshed");
            await Assertions.Expect(page.GetByTestId("api-users-message")).ToContainTextAsync("saved");
            await Assertions.Expect(page.GetByTestId("api-user-dialog")).ToHaveCountAsync(0);
        }
        var afterRefresh = await File.ReadAllBytesAsync(accountPath);
        Assert.True(saved.AsSpan().SequenceEqual(afterRefresh), "A failed refresh must not change the committed account store.");
        await page.GetByTestId("api-users-refresh").ClickAsync();
        await Assertions.Expect(page.GetByTestId("api-users-error")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByTestId("api-users-table")).ToContainTextAsync("browser-user");
    }

    internal static async Task<ApiLoginResult> LoginAsync(HttpClient client, string userName, string password) {
        using var response = await client.PostAsJsonAsync("/api/access/login", new ApiLoginRequest(userName, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return (await response.Content.ReadFromJsonAsync<ApiLoginResult>())!;
    }

    internal static async Task AssertStatusAsync(HttpClient client, string token, string path, HttpStatusCode expected) {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == expected, $"GET {path}: expected {expected}, actual {response.StatusCode}");
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }
}
