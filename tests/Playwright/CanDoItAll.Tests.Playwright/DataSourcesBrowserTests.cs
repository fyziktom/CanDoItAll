using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Npgsql;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class DataSourcesBrowserTests {
    [Fact]
    public async Task Unlocked_UI_creates_transfers_and_activates_only_after_restart_with_real_partial_group_progress() {
        await using var host = new DataSourcesBrowserHost();
        await host.StartAsync();
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        var evidence = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "workspace-data-sources-ui");
        Directory.CreateDirectory(evidence);
        await page.GotoAsync(host.BaseUrl + "/settings?tab=data-sources");
        await page.Locator("[data-testid=database-profile-controls][data-interactive=true]").WaitForAsync();
        await Assertions.Expect(page.GetByTestId("database-startup-modal")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByTestId("database-data-sources-summary")).ToContainTextAsync("Owned workspace A");
        await Assertions.Expect(page.GetByTestId("database-profile-new-postgres")).ToBeEnabledAsync();
        await page.GetByTestId("database-profile-new-postgres").ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "New data source", Exact = true })).ToBeVisibleAsync();
        var connection = new NpgsqlConnectionStringBuilder(host.B.ConnectionString);
        await page.GetByTestId("database-profile-name").FillAsync("Owned workspace B");
        await page.GetByTestId("database-profile-workspace-root").FillAsync(host.B.WorkspaceRootPath);
        await page.GetByTestId("database-profile-postgres-host").FillAsync(connection.Host!);
        await page.GetByTestId("database-profile-postgres-port").FillAsync(connection.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await page.GetByTestId("database-profile-postgres-database").FillAsync(connection.Database!);
        await page.GetByTestId("database-profile-postgres-admin-database").FillAsync("postgres");
        await page.GetByTestId("database-profile-postgres-user").FillAsync(connection.Username!);
        await page.GetByTestId("database-profile-postgres-password").FillAsync(connection.Password!);
        await page.GetByTestId("database-profile-save").ClickAsync();
        try {
            await Assertions.Expect(page.GetByTestId("database-profile-operation-message")).ToContainTextAsync("Data source saved");
        } catch {
            await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "save-failure.png"), FullPage = true,
                Mask = [page.GetByTestId("database-profile-postgres-password")] });
            await File.WriteAllLinesAsync(Path.Combine(evidence, "save-validation.txt"), await page.Locator(".validation-errors, .validation-message, [data-testid=database-operation-receipt]").AllTextContentsAsync());
            throw;
        }
        await Assertions.Expect(page.GetByTestId("database-profile-postgres-password")).ToHaveValueAsync(string.Empty);
        var profileB = await host.TargetProfileIdAsync();
        Assert.False(await ExistsAsync(host.B.ConnectionString));
        var profiles = host.Services.GetRequiredService<IDatabaseProfileService>();
        var saved = await profiles.GetEditorAsync(profileB);
        Assert.True(saved.PostgresPassword == connection.Password);
        saved.PostgresPassword = string.Empty;
        await page.GetByTestId("database-profile-refresh-schema").ClickAsync();
        await Assertions.Expect(page.GetByTestId("database-profile-apply-schema")).ToBeEnabledAsync();
        await page.GetByTestId("database-profile-apply-schema").ClickAsync();
        await Assertions.Expect(page.GetByTestId("database-profile-operation-message")).ToContainTextAsync("schema applied", new() { Timeout = 90_000 });
        Assert.True(await ExistsAsync(host.B.ConnectionString));
        await page.GetByTestId("database-profile-create-empty").ClickAsync();
        var dialog = page.GetByTestId("database-transfer-dialog");
        await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = 90_000 });
        await Assertions.Expect(dialog.GetByTestId("database-transfer-source-select")).ToHaveValueAsync(host.ProfileA.ToString());
        Assert.True(await ExistsAsync(host.B.ConnectionString));
        await CloseTransfer();
        await page.GetByTestId("database-profile-test-connection").ClickAsync();
        await Assertions.Expect(page.GetByTestId("database-profile-operation-message")).ToContainTextAsync("connection succeeded");
        await using var targetServices = host.BuildTargetServices();
        await using var targetScope = targetServices.CreateAsyncScope();
        var targetWorkspace = targetScope.ServiceProvider.GetRequiredService<WorkspaceService>();
        Assert.Null((await targetWorkspace.GetSettingsAsync()).DefaultProviderProfileId);
        await page.GetByTestId("database-profile-transfer-settings").ClickAsync();
        await Assertions.Expect(dialog.GetByTestId("database-transfer-item-workspace-default-provider")).ToBeVisibleAsync();
        await dialog.GetByTestId("database-transfer-item-workspace-default-provider").Locator("input").CheckAsync();
        await dialog.GetByTestId("database-transfer-apply").ClickAsync();
        await Assertions.Expect(dialog.GetByTestId("database-transfer-result")).ToContainTextAsync("Transferred 1");
        Assert.Equal(host.DefaultProviderId, (await targetWorkspace.GetSettingsAsync()).DefaultProviderProfileId);
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "production-transfer-confirmed.png") });
        await CloseTransfer();

        var structurePage = await context.NewPageAsync();
        await structurePage.GotoAsync($"{host.BaseUrl}/projects/{host.ProjectA:D}/structure");
        await structurePage.GetByTestId("project-structure-selection-window").WaitForAsync();
        await page.GetByTestId("database-profile-activate").ClickAsync();
        await Assertions.Expect(page.GetByTestId("database-data-sources-summary")).ToContainTextAsync("Pending restart: Owned workspace B");
        var beforeRestart = await host.ReadRuntimeAsync();
        Assert.Equal(host.ProfileA, beforeRestart.RuntimeProfileId);
        Assert.Equal(profileB, beforeRestart.PendingRestartProfileId);
        Assert.Equal(host.A.WorkspaceRootPath, beforeRestart.WorkspaceRoot, ignoreCase: OperatingSystem.IsWindows());
        Assert.Equal(profileB, (await profiles.GetCurrentSelectionAsync()).ActiveProfileId);
        Assert.Contains(host.ProjectA.ToString("D"), structurePage.Url, StringComparison.Ordinal);
        var workspaceKeys = await structurePage.EvaluateAsync<string[]>("() => Object.keys(localStorage).filter(key => key.startsWith('candoitall.workbench.session:'))");
        Assert.Contains(workspaceKeys, key => key.EndsWith(host.ProfileA.ToString("N"), StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(workspaceKeys, key => key.EndsWith(profileB.ToString("N"), StringComparison.OrdinalIgnoreCase));
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "production-pending-restart.png") });

        var targetProject = await targetScope.ServiceProvider.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Owned target project B" });
        Assert.True(targetProject.IsSuccess);
        await page.GetByTestId("database-profile-transfer-settings").ClickAsync();
        await Assertions.Expect(dialog.GetByTestId("database-transfer-item-projects")).ToBeVisibleAsync();
        await dialog.GetByTestId("database-transfer-item-workspace-default-provider").Locator("input").CheckAsync();
        await dialog.GetByTestId("database-transfer-item-projects").Locator("input").CheckAsync();
        await dialog.GetByTestId("database-transfer-apply").ClickAsync();
        await Assertions.Expect(dialog.GetByTestId("database-transfer-result")).ToContainTextAsync("One or more groups");
        await Assertions.Expect(dialog.GetByTestId("database-transfer-group-result-workspace-default-provider").Last).ToContainTextAsync("Copied 1");
        await Assertions.Expect(dialog.GetByTestId("database-transfer-group-result-projects")).ToContainTextAsync("did not report success");
        var targetProjects = await targetScope.ServiceProvider.GetRequiredService<ProjectsService>().ListAsync();
        Assert.Equal(targetProject.Value, Assert.Single(targetProjects).Id);
        await using var sourceScope = host.Services.CreateAsyncScope();
        var sourceWorkspace = await sourceScope.ServiceProvider.GetRequiredService<WorkspaceService>().GetSettingsAsync();
        Assert.Equal("Marker A", sourceWorkspace.WorkspaceName);
        Assert.Equal(host.DefaultProviderId, sourceWorkspace.DefaultProviderProfileId);
        Assert.Equal(host.ProjectA, Assert.Single(await sourceScope.ServiceProvider.GetRequiredService<ProjectsService>().ListAsync()).Id);
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "production-partial-groups.png") });
        await CloseTransfer();
        await Assertions.Expect(page.GetByTestId("database-profile-save")).ToBeDisabledAsync();
        await File.WriteAllTextAsync(Path.Combine(evidence, "post-transfer-controls.json"), await page.EvaluateAsync<string>("() => JSON.stringify(Array.from(document.querySelectorAll('button')).map(e => ({text:e.innerText, aria:e.getAttribute('aria-label'), hidden:!!e.closest('[aria-hidden=true],[inert]')})))"));
        await page.GetByRole(AriaRole.Button).Filter(new() { Has = page.GetByText("Secrets", new() { Exact = true }) }).ClickAsync();
        await Assertions.Expect(page.GetByText("A-only secret", new() { Exact = true })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Data Sources", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByTestId("database-operation-unknown")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("database-profile-save")).ToBeDisabledAsync();

        await page.GetByTestId("database-shell-action").ClickAsync();
        await Assertions.Expect(page.GetByTestId("database-switcher-dialog")).ToContainTextAsync("Owned workspace A");
        await page.Keyboard.PressAsync("Escape");
        await page.GotoAsync(host.BaseUrl + "/projects");
        await Assertions.Expect(page.GetByTestId("projects-tree-workspace").GetByText("Owned source project A", new() { Exact = true }).First).ToBeVisibleAsync();
        await page.GoBackAsync();
        await Assertions.Expect(page.GetByTestId("database-data-sources-summary")).ToContainTextAsync("Owned workspace A");
        await context.CloseAsync();
        await host.RestartAsync();
        var afterRestart = await host.ReadRuntimeAsync();
        Assert.Equal(profileB, afterRestart.RuntimeProfileId);
        Assert.Null(afterRestart.PendingRestartProfileId);
        Assert.Equal(host.B.WorkspaceRootPath, afterRestart.WorkspaceRoot, ignoreCase: OperatingSystem.IsWindows());
        await using var restartedContext = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
        var restartedPage = await restartedContext.NewPageAsync();
        await restartedPage.GotoAsync(host.BaseUrl + "/settings?tab=data-sources");
        await restartedPage.Locator("[data-testid=database-profile-controls][data-interactive=true]").WaitForAsync();
        await Assertions.Expect(restartedPage.GetByTestId("database-startup-modal")).ToHaveCountAsync(0);
        await Assertions.Expect(restartedPage.GetByTestId("database-data-sources-summary")).ToContainTextAsync("Owned workspace B");
        await restartedPage.GetByRole(AriaRole.Button).Filter(new() { Has = restartedPage.GetByText("Secrets", new() { Exact = true }) }).ClickAsync();
        await Assertions.Expect(restartedPage.GetByText("A-only secret", new() { Exact = true })).ToHaveCountAsync(0);
        Assert.Null(await targetScope.ServiceProvider.GetRequiredService<SecretService>().GetAsync(host.SecretA));
        Assert.Null((await targetScope.ServiceProvider.GetRequiredService<ResourcesService>().GetAsync(host.ResourceA)).Id);
        Assert.NotEqual("Marker A", (await targetWorkspace.GetSettingsAsync()).WorkspaceName);
        Assert.Equal(host.AccountId, Assert.Single(await targetScope.ServiceProvider.GetRequiredService<IApiUserStore>().ReadAsync()).Id);
        Assert.NotNull(await targetScope.ServiceProvider.GetRequiredService<IApiTokenRegistry>().FindAsync(host.TokenId));
        await restartedPage.GotoAsync(host.BaseUrl + "/projects");
        await Assertions.Expect(restartedPage.GetByTestId("projects-tree-workspace").GetByText("Owned target project B", new() { Exact = true }).First).ToBeVisibleAsync();
        await Assertions.Expect(restartedPage.GetByText("Owned source project A", new() { Exact = true })).ToHaveCountAsync(0);
        await restartedPage.GotoAsync($"{host.BaseUrl}/projects/{targetProject.Value:D}/structure");
        await restartedPage.GetByTestId("project-structure-selection-window").WaitForAsync();
        await restartedPage.ScreenshotAsync(new() { Path = Path.Combine(evidence, "production-restarted-b.png") });
        Assert.Empty(errors);
        await File.WriteAllTextAsync(Path.Combine(evidence, "production-proof.json"), JsonSerializer.Serialize(new {
            host.ProfileA, ProfileB = profileB, host.ProjectA, ProjectB = targetProject.Value, host.SecretA, host.ResourceA,
            host.DefaultProviderId, host.AccountId, host.TokenId, beforeRestart, afterRestart,
            SourceUnchanged = true, TargetPreferenceCopied = true, LaterProjectGroupRefused = true,
            ProfileSecretsResourcesAndPreferencesIsolated = true, InstanceAccountsAndTokensPreserved = true
        }, new JsonSerializerOptions { WriteIndented = true }));

        async Task CloseTransfer() {
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).Last.ClickAsync();
            await Assertions.Expect(dialog).ToHaveCountAsync(0);
        }
    }

    private static async Task<bool> ExistsAsync(string targetConnection) {
        var target = new NpgsqlConnectionStringBuilder(targetConnection);
        var database = target.Database;
        target.Database = "postgres";
        await using var connection = new NpgsqlConnection(target.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name)", connection);
        command.Parameters.AddWithValue("name", database!);
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
