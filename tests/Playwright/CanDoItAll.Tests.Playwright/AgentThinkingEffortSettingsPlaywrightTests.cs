using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
public sealed class AgentThinkingEffortSettingsPlaywrightTests
{
    private const string SupportedModel = "gpt-5.4";
    private const string UnsupportedModel = "gpt-4.1";
    private const string UnknownModel = "custom-deployment-west";
    private const string ProviderDefaultEffortLabel = "Provider default (medium)";
    private const string ProviderDefaultModelLabel = "Provider default (gpt-5.4)";

    private readonly PlaywrightAppFixture fixture;

    public AgentThinkingEffortSettingsPlaywrightTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Runtime_thinking_effort_supports_override_reset_and_blocks_incompatible_models()
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
        var modelSelector = dialog.GetByTestId("agents-catalog-model-choice");
        var saveButton = dialog.GetByTestId("agents-catalog-save");

        await Assertions.Expect(effortSelector).ToBeEnabledAsync();
        await AssertSelectedOptionAsync(effortSelector, ProviderDefaultEffortLabel);
        Assert.Equal(
            [
                ProviderDefaultEffortLabel,
                "None (disable thinking)",
                "Low",
                "Medium",
                "High",
                "Extra high"
            ],
            await effortSelector.Locator("option").AllTextContentsAsync());
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
        supportGuidance = dialog.GetByTestId("agents-catalog-thinking-effort-support");
        modelSelector = dialog.GetByTestId("agents-catalog-model-choice");
        saveButton = dialog.GetByTestId("agents-catalog-save");

        await AssertSelectedOptionAsync(effortSelector, "High");
        await CaptureEvidenceAsync(page, evidenceDirectory, "agent-thinking-override-reopened.png");
        await SelectOptionByLabelAsync(effortSelector, ProviderDefaultEffortLabel);
        await AssertSelectedOptionAsync(effortSelector, ProviderDefaultEffortLabel);

        await saveButton.ClickAsync();
        await ExpectTextContainsAsync(page.Locator("body"), "Technical agent saved.");

        dialog = await OpenAgentRuntimeAsync(page, seed);
        effortSelector = dialog.GetByTestId("agents-catalog-thinking-effort");
        supportGuidance = dialog.GetByTestId("agents-catalog-thinking-effort-support");
        modelSelector = dialog.GetByTestId("agents-catalog-model-choice");
        saveButton = dialog.GetByTestId("agents-catalog-save");

        await AssertSelectedOptionAsync(effortSelector, ProviderDefaultEffortLabel);
        await CaptureEvidenceAsync(page, evidenceDirectory, "agent-thinking-reset-reopened.png");

        await SelectOptionByLabelAsync(modelSelector, UnsupportedModel);
        await Assertions.Expect(effortSelector).ToBeDisabledAsync();
        await Assertions.Expect(supportGuidance).ToContainTextAsync("does not support configurable thinking effort");
        await Assertions.Expect(saveButton).ToBeEnabledAsync();
        await CaptureEvidenceAsync(page, evidenceDirectory, "agent-thinking-unsupported.png");

        await SetCustomModelAsync(dialog, UnknownModel);
        await Assertions.Expect(effortSelector).ToBeDisabledAsync();
        await Assertions.Expect(supportGuidance).ToContainTextAsync("not defined");
        await Assertions.Expect(supportGuidance).ToContainTextAsync("verified capability definition");
        await Assertions.Expect(saveButton).ToBeEnabledAsync();
        await CaptureEvidenceAsync(page, evidenceDirectory, "agent-thinking-unknown.png");

        await dialog.GetByTestId("agents-catalog-model-override").UncheckAsync();
        await AssertSelectedOptionAsync(modelSelector, ProviderDefaultModelLabel);
        await SelectOptionByLabelAsync(effortSelector, "High");
        await SelectOptionByLabelAsync(modelSelector, UnsupportedModel);

        await Assertions.Expect(effortSelector).ToBeEnabledAsync();
        await AssertSelectedOptionAsync(effortSelector, "High (currently configured; unavailable)");
        await Assertions.Expect(supportGuidance).ToContainTextAsync("cannot be applied");
        await Assertions.Expect(supportGuidance).ToContainTextAsync("Select Provider default to remove this override");
        await Assertions.Expect(saveButton).ToBeDisabledAsync();
        await CaptureEvidenceAsync(page, evidenceDirectory, "agent-thinking-incompatible-override.png");

        await SelectOptionByLabelAsync(effortSelector, "Provider default");
        await Assertions.Expect(effortSelector).ToBeDisabledAsync();
        await Assertions.Expect(saveButton).ToBeEnabledAsync();

        await SelectOptionByLabelAsync(modelSelector, ProviderDefaultModelLabel);
        await SelectOptionByLabelAsync(effortSelector, "High");
        await SetCustomModelAsync(dialog, UnknownModel);

        await Assertions.Expect(effortSelector).ToBeEnabledAsync();
        await AssertSelectedOptionAsync(effortSelector, "High (currently configured; unavailable)");
        await Assertions.Expect(supportGuidance).ToContainTextAsync("cannot be applied");
        await Assertions.Expect(supportGuidance).ToContainTextAsync("not defined");
        await Assertions.Expect(saveButton).ToBeDisabledAsync();

        await SelectOptionByLabelAsync(effortSelector, "Provider default");
        await Assertions.Expect(effortSelector).ToBeDisabledAsync();
        await Assertions.Expect(saveButton).ToBeEnabledAsync();

        var providerSelector = dialog.GetByTestId("agents-catalog-provider");
        await providerSelector.SelectOptionAsync(seed.InvalidDefaultProviderId.ToString("D"));
        await Assertions.Expect(providerSelector).ToHaveValueAsync(seed.InvalidDefaultProviderId.ToString("D"));
        await CaptureEvidenceAsync(page, evidenceDirectory, "agent-thinking-invalid-provider-default.png");
        await Assertions.Expect(effortSelector).ToBeEnabledAsync();
        await AssertSelectedOptionAsync(effortSelector, "Provider default (unavailable)");
        await Assertions.Expect(supportGuidance).ToContainTextAsync("provider default cannot be applied");
        await Assertions.Expect(supportGuidance).ToContainTextAsync("Select a supported override");
        await Assertions.Expect(supportGuidance).ToContainTextAsync("max");
        await Assertions.Expect(saveButton).ToBeDisabledAsync();
        await SelectOptionByLabelAsync(effortSelector, "High");
        await Assertions.Expect(saveButton).ToBeEnabledAsync();
        await saveButton.ClickAsync();
        await ExpectTextContainsAsync(page.Locator("body"), "Technical agent saved.");

        dialog = await OpenAgentRuntimeAsync(page, seed);
        effortSelector = dialog.GetByTestId("agents-catalog-thinking-effort");
        supportGuidance = dialog.GetByTestId("agents-catalog-thinking-effort-support");
        saveButton = dialog.GetByTestId("agents-catalog-save");

        await AssertSelectedOptionAsync(effortSelector, "High");
        await Assertions.Expect(saveButton).ToBeEnabledAsync();
        await SelectOptionByLabelAsync(effortSelector, "Provider default (unavailable)");
        await AssertSelectedOptionAsync(effortSelector, "Provider default (unavailable)");
        await Assertions.Expect(saveButton).ToBeDisabledAsync();
        await CaptureEvidenceAsync(page, evidenceDirectory, "agent-thinking-invalid-provider-default-reset.png");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        var chatResponse = await page.GotoAsync(
            $"{fixture.BaseUrl}/agents?tab=chat&agentId={seed.AgentId:D}");
        Assert.True(chatResponse?.Ok);
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("agents-chat-panel").WaitForAsync();
        await Assertions.Expect(
            page.GetByTestId("agents-catalog-thinking-effort"))
            .ToHaveCountAsync(0);
        await CaptureEvidenceAsync(page, evidenceDirectory, "agent-thinking-chat-no-control.png");
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
            SuggestedModels =
            [
                SupportedModel,
                UnsupportedModel,
                UnknownModel
            ]
        });
        var invalidDefaultProviderId = await workspaceService.SaveProviderAsync(new ProviderProfileEditorModel
        {
            Name = $"Thinking Effort Invalid Default {suffix}",
            Kind = ProviderKind.OpenAi,
            BaseUrl = $"https://api.openai.com/v1/playwright-thinking-effort-invalid/{suffix}",
            ApiKeyEnvironmentVariable = "OPENAI_API_KEY",
            DefaultModel = SupportedModel,
            Transport = ProviderTransportKind.Responses,
            Purpose = ProviderProfilePurpose.Chat,
            IsEnabled = true,
            SupportsStreaming = true,
            SupportsTools = true,
            ConfigurationJson = AgentThinkingEffortPolicy.WriteProviderDefault(
                "{}",
                AgentReasoningEffortLevel.Max),
            SuggestedModels = [SupportedModel]
        });
        var invalidDefaultProvider = Assert.Single(
            await workspaceService.ListProvidersAsync(),
            provider => provider.Id == invalidDefaultProviderId);
        Assert.Equal(
            AgentReasoningEffortLevel.Max,
            AgentThinkingEffortPolicy.ReadConfiguredEffort(
                invalidDefaultProvider.ConfigurationJson,
                "provider"));
        Assert.Throws<InvalidOperationException>(() =>
            AgentThinkingEffortPolicy.ResolveProviderDefault(
                invalidDefaultProvider,
                SupportedModel));
        var agentName = $"Thinking Effort Browser {suffix}";
        var agentId = await workspaceService.SaveAgentAsync(new AgentEditorModel
        {
            Name = agentName,
            RoleTitle = "Runtime settings tester",
            Summary = "Verifies thinking-effort runtime settings in the agent details dialog.",
            Instructions = "Keep provider defaults and explicit thinking-effort overrides observable.",
            ProviderProfileId = providerId
        });

        return new SeededThinkingEffortAgent(agentId, agentName, invalidDefaultProviderId);
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

    private static async Task SetCustomModelAsync(ILocator dialog, string model)
    {
        var overrideToggle = dialog.GetByTestId("agents-catalog-model-override");
        if (!await overrideToggle.IsCheckedAsync())
        {
            await overrideToggle.CheckAsync();
        }

        var customModelInput = dialog.GetByTestId("agents-catalog-model");
        await customModelInput.FillAsync(model);
        await Assertions.Expect(customModelInput).ToHaveValueAsync(model);
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

    private sealed record SeededThinkingEffortAgent(
        Guid AgentId,
        string AgentName,
        Guid InvalidDefaultProviderId);
}
