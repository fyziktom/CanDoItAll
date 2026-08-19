using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Flows;

[Collection(PlaywrightCollection.Name)]
public sealed class AgentThinkingEffortSettingsPlaywrightTests
{
    private const string SupportedModel = "gpt-5.4";
    private const string ProviderDefaultEffortLabel = "Provider default (medium)";

    private readonly PlaywrightAppFixture fixture;

    public AgentThinkingEffortSettingsPlaywrightTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Runtime_thinking_effort_override_and_reset_survive_browser_reload()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var seed = await SeedThinkingEffortAgentAsync(suffix);
        var evidenceDirectory = Path.Combine(
            PlaywrightTestHostPaths.RepositoryRoot,
            "artifacts",
            "bundles",
            "agent-thinking-effort-configuration",
            "evidence",
            "Thinking effort acceptance");
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

        var dialog = await OpenAgentRuntimeAsync(page, seed);

        var effortSelector = dialog.GetByTestId("agents-catalog-thinking-effort");
        var supportGuidance = dialog.GetByTestId("agents-catalog-thinking-effort-support");
        var saveButton = dialog.GetByTestId("agents-catalog-save");

        await Assertions.Expect(effortSelector).ToBeEnabledAsync();
        await AssertSelectedOptionAsync(effortSelector, ProviderDefaultEffortLabel);
        await Assertions.Expect(effortSelector).ToBeInViewportAsync();
        await Assertions.Expect(supportGuidance).ToBeInViewportAsync();
        await Assertions.Expect(saveButton).ToBeInViewportAsync();
        Assert.False(await dialog.EvaluateAsync<bool>(
            "element => element.scrollWidth > element.clientWidth + 1"));
        await CaptureEvidenceAsync(page, evidenceDirectory, "agent-thinking-supported.png");

        await SelectOptionByLabelAsync(effortSelector, "High");
        await AssertSelectedOptionAsync(effortSelector, "High");

        await saveButton.ClickAsync();
        await ExpectTextContainsAsync(page.Locator("body"), "Technical agent saved.");

        dialog = await OpenAgentRuntimeAsync(page, seed);
        effortSelector = dialog.GetByTestId("agents-catalog-thinking-effort");
        saveButton = dialog.GetByTestId("agents-catalog-save");

        await AssertSelectedOptionAsync(effortSelector, "High");
        await CaptureEvidenceAsync(page, evidenceDirectory, "agent-thinking-override-reopened.png");
        await SelectOptionByLabelAsync(effortSelector, ProviderDefaultEffortLabel);
        await AssertSelectedOptionAsync(effortSelector, ProviderDefaultEffortLabel);

        await saveButton.ClickAsync();
        await ExpectTextContainsAsync(page.Locator("body"), "Technical agent saved.");

        dialog = await OpenAgentRuntimeAsync(page, seed);
        effortSelector = dialog.GetByTestId("agents-catalog-thinking-effort");

        await AssertSelectedOptionAsync(effortSelector, ProviderDefaultEffortLabel);
        await CaptureEvidenceAsync(page, evidenceDirectory, "agent-thinking-reset-reopened.png");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private async Task<SeededThinkingEffortAgent> SeedThinkingEffortAgentAsync(string suffix)
    {
        var activeProfile = CreateActiveProfile();
        await using var serviceProvider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            activeProfile,
            "CanDoItAll.Tests.Playwright.AgentThinkingEffort",
            TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?>
            {
                ["DevelopmentManager:TuningModeEnabled"] = "false"
            });
        await using var scope = serviceProvider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var providerId = await workspaceService.SaveProviderAsync(new ProviderProfileEditorModel
        {
            Name = $"Thinking Effort Browser {suffix}",
            Kind = ProviderKind.OpenAi,
            BaseUrl = $"https://api.openai.com/v1/playwright-thinking-effort/{suffix}",
            ApiKeyEnvironmentVariable = "OPENAI_API_KEY",
            DefaultModel = SupportedModel,
            Transport = ProviderTransportKind.Responses,
            Purpose = ProviderProfilePurpose.Chat,
            IsEnabled = true,
            SupportsStreaming = true,
            SupportsTools = true,
            ConfigurationJson = AgentThinkingEffortPolicy.WriteProviderDefault(
                "{}",
                AgentReasoningEffortLevel.Medium),
            SuggestedModels = [SupportedModel]
        });
        var agentName = $"Thinking Effort Browser {suffix}";
        _ = await workspaceService.SaveAgentAsync(new AgentEditorModel
        {
            Name = agentName,
            RoleTitle = "Runtime settings tester",
            Summary = "Verifies thinking-effort runtime settings in the agent details dialog.",
            Instructions = "Keep provider defaults and explicit thinking-effort overrides observable.",
            ProviderProfileId = providerId
        });

        return new SeededThinkingEffortAgent(agentName);
    }

    private async Task<ILocator> OpenAgentRuntimeAsync(
        IPage page,
        SeededThinkingEffortAgent seed)
    {
        var response = await page.GotoAsync($"{fixture.BaseUrl}/agents?tab=agents");
        Assert.True(response?.Ok);
        await DismissStartupModalIfPresentAsync(page);
        var search = page.GetByTestId("agents-catalog-search");
        await search.WaitForAsync();
        await search.FillAsync(seed.AgentName);
        var agentCard = page.GetByTestId("agents-catalog-card")
            .Filter(new LocatorFilterOptions { HasTextString = seed.AgentName });
        await Assertions.Expect(agentCard).ToHaveCountAsync(1);
        await agentCard.ClickAsync();
        var selectedAgentShell = page.Locator(
                "[data-testid='agents-catalog-card-shell'].agent-selection-card--selected")
            .Filter(new LocatorFilterOptions { HasTextString = seed.AgentName });
        await Assertions.Expect(selectedAgentShell).ToHaveCountAsync(1);
        await agentCard.DispatchEventAsync("dblclick");

        var dialog = page.Locator("dialog[open][data-testid='agents-details-dialog']");
        await Assertions.Expect(dialog).ToHaveCountAsync(1);
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        await dialog.GetByTestId("agents-catalog-name").WaitForAsync();
        var runtimeTab = dialog.GetByRole(
            AriaRole.Tab,
            new LocatorGetByRoleOptions
            {
                Name = "Runtime",
                Exact = true
            });
        await runtimeTab.ClickAsync();
        await Assertions.Expect(runtimeTab).ToHaveAttributeAsync("aria-selected", "true");
        await dialog.GetByTestId("agents-catalog-thinking-effort").WaitForAsync();
        return dialog;
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
            "playwright-thinking-effort",
            environmentRoot,
            profileRoot,
            TestDatabaseProviderKind.PostgreSql,
            fixture.DatabaseConnectionString,
            workspaceRoot,
            Path.Combine(profileRoot, "manager-artifacts"));
    }

    private static async Task SelectOptionByLabelAsync(ILocator selector, string label)
    {
        await selector.SelectOptionAsync(new SelectOptionValue
        {
            Label = label
        });
    }

    private static Task AssertSelectedOptionAsync(ILocator selector, string expectedLabel)
        => Assertions.Expect(selector.Locator("option:checked")).ToHaveTextAsync(expectedLabel);

    private static Task CaptureEvidenceAsync(IPage page, string evidenceDirectory, string fileName)
        => page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, fileName)
        });

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

    private sealed record SeededThinkingEffortAgent(string AgentName);
}
