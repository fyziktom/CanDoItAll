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
    public async Task Agents_workspace_supports_creation_and_governance_profile()
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
        var ownerName = $"B09 Steward {suffix}";
        var providerName = $"B09 Provider {suffix}";
        var agentName = $"B09 Agent {suffix}";
        var seededDependencies = await SeedAgentDependenciesAsync(ownerName, providerName);

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/agents");
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("crmhr-agent-name").WaitForAsync();

        await page.GetByTestId("crmhr-agent-name").FillAsync(agentName);
        await page.GetByTestId("crmhr-agent-code").FillAsync("B09-AI");
        await page.GetByTestId("crmhr-agent-summary").FillAsync("Coordinates structured analysis and guarded delivery support.");
        await page.GetByTestId("crmhr-agent-save-button").ClickAsync();

        await page.GetByTestId("crmhr-agent-profile-save-button").WaitForAsync();
        await page.GetByTestId("crmhr-agent-provider").SelectOptionAsync(new[] { seededDependencies.ProviderId.ToString() });
        await page.GetByTestId("crmhr-agent-default-model").FillAsync("llama3.2");
        await page.GetByTestId("crmhr-agent-execution-mode").SelectOptionAsync(AiExecutionMode.ThirdParty.ToString());
        await page.GetByTestId("crmhr-agent-owner").SelectOptionAsync(new[] { seededDependencies.OwnerId.ToString() });
        await page.GetByTestId("crmhr-agent-validation-status").SelectOptionAsync(AiValidationStatus.ReviewRequired.ToString());
        await page.GetByTestId("crmhr-agent-last-reviewed-on").FillAsync("2026-04-03");
        await page.GetByTestId("crmhr-agent-notes").FillAsync("Requires human approval before any customer-facing recommendation.");
        await page.GetByTestId("crmhr-agent-capability-add").ClickAsync();
        await page.GetByTestId("crmhr-agent-capability-name-0").WaitForAsync();
        await page.GetByTestId("crmhr-agent-capability-name-0").FillAsync("Architecture review");
        await page.GetByTestId("crmhr-agent-capability-scope-0").FillAsync("Solution design and impact analysis");
        await page.GetByTestId("crmhr-agent-capability-tool-access-0").FillAsync("Read-only repository and project metadata");
        await page.GetByTestId("crmhr-agent-capability-limitations-0").FillAsync("No production execution or customer commitments");
        await page.GetByTestId("crmhr-agent-capability-notes-0").FillAsync("Escalate unresolved ambiguity to the steward.");
        await page.GetByTestId("crmhr-agent-profile-save-button").ClickAsync();

        await page.GetByTestId("crmhr-agent-message").WaitForAsync();
        await ExpectTextContainsAsync(page.GetByTestId("crmhr-agent-message"), "AI agent profile saved.");
        await ExpectTextContainsAsync(page.GetByTestId("crmhr-agent-summary-provider"), providerName);
        await ExpectTextContainsAsync(page.GetByTestId("crmhr-agent-summary-owner"), ownerName);
        await ExpectTextContainsAsync(page.GetByTestId("crmhr-agent-summary-capabilities"), "1");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-agents-b09-desktop.png"),
            FullPage = true
        });

        await page.SetViewportSizeAsync(1100, 900);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-agents-b09-tablet.png"),
            FullPage = true
        });

        await page.GetByRole(AriaRole.Button, new() { Name = "Open directory record", Exact = true }).First.ClickAsync();
        await WaitForUrlContainsAsync(page, "/crm-hr/directory?partyId=");
        await page.GetByTestId("crmhr-party-display-name").WaitForAsync();
        await ExpectInputValueContainsAsync(page.GetByTestId("crmhr-party-display-name"), agentName);
        await ExpectInputValueContainsAsync(page.GetByTestId("crmhr-party-type"), PartyType.AiAgent.ToString());
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private async Task<SeededAiAgentDependencies> SeedAgentDependenciesAsync(string ownerName, string providerName)
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
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<WorkspaceService>();

        var ownerId = await CreatePersonAsync(
            partyDirectoryService,
            ownerName,
            $"owner.{ownerName[^6..].ToLowerInvariant()}@example.test");
        var providerSave = await workspaceService.SaveProviderAsync(new ProviderProfileEditorModel
        {
            Name = providerName,
            ProviderKind = ProviderKind.OllamaRemote,
            BaseUrl = "http://ollama.internal",
            DefaultModel = "llama3.1",
            TimeoutSeconds = 45,
            IsEnabled = true
        });
        Assert.True(providerSave.IsSuccess);

        return new SeededAiAgentDependencies(ownerId, providerSave.Value);
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

    private static async Task<Guid> CreatePersonAsync(PartyDirectoryService partyDirectoryService, string displayName, string email)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "playwright-tests",
            Roles =
            [
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = PartyRoleKind.Employee,
                    Title = "Employee",
                    IsPrimary = true
                }
            ],
            ContactPoints =
            [
                new PartyContactPointEditorModel
                {
                    ContactType = PartyContactType.Email,
                    Label = "Primary email",
                    Value = email,
                    NormalizedValue = email.ToLowerInvariant(),
                    IsPrimary = true,
                    IsPublic = true
                }
            ]
        });

        Assert.True(result.IsSuccess);
        return result.Value;
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

    private sealed record SeededAiAgentDependencies(Guid OwnerId, Guid ProviderId);
}
