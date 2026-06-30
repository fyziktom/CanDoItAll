using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
public sealed class AgentCapabilitySetupFlowPlaywrightTests
{
    private readonly PlaywrightAppFixture fixture;

    public AgentCapabilitySetupFlowPlaywrightTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Capabilities_tab_supports_tool_setup_test_and_access_preview_on_large_screen()
    {
        var evidenceDirectory = @"C:\repositories\CanDoItAll\codex\bundles\skill-tool-mcp-isolation-template-migration\proof\SB10";
        Directory.CreateDirectory(evidenceDirectory);

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var seed = await SeedCapabilityAgentAsync(suffix);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/agents?tab=capabilities&agentId={seed.AgentId:D}");
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("agents-capabilities-panel").WaitForAsync();
        await page.GetByText(seed.AgentName, new PageGetByTextOptions { Exact = true }).First.WaitForAsync();
        await page.GetByTestId("agents-capability-access-preview").WaitForAsync();

        await page.GetByTestId("agents-capability-access-preview").ClickAsync();
        await page.GetByTestId("agents-capability-access-diagnostics").WaitForAsync();
        await ExpectTextContainsAsync(page.GetByTestId("agents-capability-access-diagnostics"), seed.CapabilityKey);

        await page.GetByTestId("agents-capability-new-tool").ClickAsync();
        await page.GetByTestId("agents-capability-setup-wizard").WaitForAsync();

        var toolName = $"Browser Tool {suffix}";
        var toolKey = $"browser-tool-{suffix}";
        await page.GetByTestId("agents-capability-setup-name").FillAsync(toolName);
        await page.GetByTestId("agents-capability-setup-key").FillAsync(toolKey);
        await page.GetByTestId("agents-capability-setup-next").ClickAsync();
        await page.GetByTestId("agents-capability-setup-tool-kind").WaitForAsync();

        await page.GetByTestId("agents-capability-setup-tool-process-command").FillAsync("dotnet");
        await page.GetByTestId("agents-capability-setup-tool-process-allowed-executables").FillAsync("dotnet");
        await page.GetByTestId("agents-capability-setup-tool-test-input").FillAsync("{not-json");
        await page.GetByTestId("agents-capability-setup-test").ClickAsync();
        var setupDiagnostics = page.GetByTestId("agents-capability-setup-diagnostics");
        await setupDiagnostics.WaitForAsync();
        await setupDiagnostics.EvaluateAsync("element => element.scrollIntoView({ block: 'center', inline: 'nearest' })");
        await ExpectTextContainsAsync(setupDiagnostics, "JsonParse");

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "agent-capability-setup-flow-large.png"),
            FullPage = true
        });

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private async Task<SeededCapabilityAgent> SeedCapabilityAgentAsync(string suffix)
    {
        var activeProfile = CreateActiveProfile();
        await using var serviceProvider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            activeProfile,
            "CanDoItAll.Tests.Playwright.AgentCapabilitySetup",
            TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?>
            {
                ["DevelopmentManager:TuningModeEnabled"] = "false"
            });
        await using var scope = serviceProvider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var capabilityKey = $"external-audit-{suffix}";
        var capabilityId = await workspaceService.SaveCapabilityAsync(new CapabilityEditorModel
        {
            Kind = CapabilityKind.Tool,
            Key = capabilityKey,
            Name = $"External Audit {suffix}",
            Description = "Browser seeded setup-flow tool capability.",
            EndpointOrPath = "dotnet",
            Tags = ["external", "browser"],
            ConfigurationJson = """
            {
              "toolKind": "externalProcess",
              "runtimeToolName": "external_audit",
              "implementationKey": "external.audit",
              "operationClassifications": [ "externalAction" ],
              "externalProcess": {
                "command": "dotnet",
                "workingDirectory": ".",
                "allowedExecutableNames": [ "dotnet" ]
              }
            }
            """
        });
        var agentName = $"Capability Browser {suffix}";
        var agentId = await workspaceService.SaveAgentAsync(new AgentEditorModel
        {
            Name = agentName,
            RoleTitle = "Runtime tester",
            Summary = "Tests capability setup flows in the browser.",
            Instructions = "Inspect capability setup, access preview, and diagnostics.",
            SelectedCapabilityIds = [capabilityId]
        });

        return new SeededCapabilityAgent(agentId, agentName, capabilityKey);
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
            "playwright-capability-setup",
            environmentRoot,
            profileRoot,
            TestDatabaseProviderKind.PostgreSql,
            fixture.DatabaseConnectionString,
            workspaceRoot,
            Path.Combine(profileRoot, "manager-artifacts"));
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

    private sealed record SeededCapabilityAgent(
        Guid AgentId,
        string AgentName,
        string CapabilityKey);
}
