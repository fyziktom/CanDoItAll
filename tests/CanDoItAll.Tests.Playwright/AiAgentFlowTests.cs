using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
public sealed class AiAgentFlowTests
{
    private readonly PlaywrightAppFixture fixture;

    public AiAgentFlowTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Agentframework_catalog_projects_agents_into_crm_hr_directory()
    {
        var evidenceDirectory = @"C:\repositories\CanDoItAll\evidence\crm-hr\b09";
        Directory.CreateDirectory(evidenceDirectory);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });
        var page = await context.NewPageAsync();
        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var providerName = $"B09 Provider {suffix}";
        var agentName = $"B09 Agent {suffix}";
        var seededDependencies = await SeedAgentDependenciesAsync(providerName);

        await page.GotoAsync($"{fixture.BaseUrl}/agents?tab=agents");
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("agents-catalog-name").WaitForAsync();

        await page.GetByTestId("agents-catalog-name").FillAsync(agentName);
        await page.GetByTestId("agents-catalog-role").FillAsync("Delivery analyst");
        await page.GetByTestId("agents-catalog-summary").FillAsync("Coordinates structured analysis and guarded delivery support.");
        await page.GetByTestId("agents-catalog-instructions").FillAsync("Review the brief, keep the runtime explicit, and create durable delivery evidence.");
        await page.GetByTestId("agents-catalog-provider").SelectOptionAsync(new[] { seededDependencies.ProviderId.ToString() });
        await page.GetByTestId("agents-catalog-model").FillAsync("llama3.2");
        await page.GetByTestId("agents-catalog-save").ClickAsync();

        await ExpectTextContainsAsync(page.Locator("body"), "Technical agent saved.");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "agentframework-agents-b09-desktop.png"),
            FullPage = true
        });

        await page.SetViewportSizeAsync(1100, 900);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "agentframework-agents-b09-tablet.png"),
            FullPage = true
        });

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/agents");
        await page.GetByTestId("crmhr-agent-search").WaitForAsync();
        await page.GetByTestId("crmhr-agent-search").FillAsync(agentName);
        await page.GetByText(agentName, new PageGetByTextOptions { Exact = true }).First.ClickAsync();
        await page.GetByTestId("crmhr-agent-open-technical-record").WaitForAsync();
        await ExpectTextContainsAsync(page.GetByTestId("crmhr-agent-summary-provider"), providerName);
        await page.GetByTestId("crmhr-agent-open-technical-record").ClickAsync();
        await WaitForUrlContainsAsync(page, "/agents?tab=agents&agentId=");
        await ExpectInputValueContainsAsync(page.GetByTestId("agents-catalog-name"), agentName);
        await ExpectInputValueContainsAsync(page.GetByTestId("agents-catalog-model"), "llama3.2");
        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/agents");
        await page.GetByTestId("crmhr-agent-search").FillAsync(agentName);
        await page.GetByText(agentName, new PageGetByTextOptions { Exact = true }).First.ClickAsync();
        await page.GetByTestId("crmhr-agent-open-directory-record").ClickAsync();
        await WaitForUrlContainsAsync(page, "/crm-hr/directory?partyId=");
        await page.GetByTestId("crmhr-party-display-name").WaitForAsync();
        await ExpectInputValueContainsAsync(page.GetByTestId("crmhr-party-display-name"), agentName);
        await ExpectInputValueContainsAsync(page.GetByTestId("crmhr-party-type"), PartyType.AiAgent.ToString());
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private async Task<SeededAiAgentDependencies> SeedAgentDependenciesAsync(string providerName)
    {
        var activeProfile = CreateActiveProfile();
        await using var serviceProvider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            activeProfile,
            "CanDoItAll.Tests.Playwright.Seed",
            TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?>
            {
                ["DevelopmentManager:TuningModeEnabled"] = "false"
            });
        await using var scope = serviceProvider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var providerSave = await workspaceService.SaveProviderAsync(new ProviderProfileEditorModel
        {
            Name = providerName,
            ConnectorPluginKey = OllamaRemoteProviderAdapter.PluginKey,
            ConfigSchemaVersion = "1.0",
            Configuration = new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["baseUrl"] = "http://ollama.internal",
                ["defaultModel"] = "llama3.1",
                ["timeoutSeconds"] = "45"
            }),
            IsEnabled = true
        });
        Assert.True(providerSave.IsSuccess);

        return new SeededAiAgentDependencies(providerSave.Value);
    }

    private TestDatabaseProfile CreateActiveProfile()
    {
        if (string.IsNullOrWhiteSpace(fixture.DatabaseConnectionString))
        {
            throw new InvalidOperationException("Playwright fixture did not expose a database connection string.");
        }

        if (string.IsNullOrWhiteSpace(fixture.StorageWorkspaceRoot))
        {
            throw new InvalidOperationException("Playwright fixture did not expose the storage workspace root.");
        }

        var workspaceRoot = fixture.StorageWorkspaceRoot;
        var profileRoot = Directory.GetParent(workspaceRoot)?.FullName
            ?? throw new InvalidOperationException($"Could not resolve profile root from '{workspaceRoot}'.");
        var environmentRoot = Path.GetFullPath(Path.Combine(profileRoot, "..", ".."));

        return new TestDatabaseProfile(
            "playwright-seed",
            environmentRoot,
            profileRoot,
            TestDatabaseProviderKind.Sqlite,
            fixture.DatabaseConnectionString,
            workspaceRoot,
            Path.Combine(profileRoot, "manager-artifacts"));
    }

    private static async Task ExpectInputValueContainsAsync(ILocator locator, string expectedValue, int timeoutMs = 10_000)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (string.Equals(await locator.InputValueAsync(), expectedValue, StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for input value '{expectedValue}'.");
    }

    private static async Task ExpectTextContainsAsync(ILocator locator, string expectedValue, int timeoutMs = 10_000)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if ((await locator.InnerTextAsync()).Contains(expectedValue, StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for text '{expectedValue}'.");
    }

    private static async Task DismissStartupModalIfPresentAsync(IPage page, float timeoutMs = 1_500)
    {
        var startupDialog = page.GetByTestId("database-startup-modal");
        try
        {
            await startupDialog.WaitForAsync(new LocatorWaitForOptions
            {
                Timeout = timeoutMs
            });
        }
        catch (TimeoutException)
        {
            return;
        }

        await page.GetByTestId("database-startup-continue").ClickAsync();
        await startupDialog.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Detached
        });
    }

    private static async Task WaitForUrlContainsAsync(IPage page, string fragment, int timeoutMs = 10_000)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (page.Url.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for URL to contain '{fragment}'. Current URL: {page.Url}");
    }

    private sealed record SeededAiAgentDependencies(Guid ProviderId);
}
