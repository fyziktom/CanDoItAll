using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
public sealed class AiAgentFlowTests(PlaywrightAppFixture fixture, ITestOutputHelper output) {
    [Fact]
    public async Task Agentframework_catalog_projects_agents_into_crm_hr_directory()
    {
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
        await CreateTechnicalAgentThroughCatalogAsync(page, providerName, agentName);

        await page.SetViewportSizeAsync(1100, 900);

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/agents");
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("crmhr-agent-search").WaitForAsync();
        await page.GetByTestId("crmhr-agent-search").FillAsync(agentName);
        await ExpectSingleAgentCardAsync(page, agentName);
        await page.GetByTestId("crmhr-agent-item-shell").Filter(new() { HasText = agentName }).GetByTestId("crmhr-agent-item").ClickAsync();
        await page.GetByTestId("crmhr-agent-open-technical-record").WaitForAsync();
        await ExpectTextContainsAsync(page.GetByTestId("crmhr-agent-summary-provider"), providerName);
        await page.GetByTestId("crmhr-agent-open-technical-record").ClickAsync();
        await WaitForUrlContainsAsync(page, "/agents?tab=agents&agentId=");
        await ExpectInputValueContainsAsync(page.GetByTestId("agents-catalog-name"), agentName);
        await page.GetByRole(AriaRole.Tab, new() { Name = "Runtime", Exact = true }).ClickAsync();
        await ExpectInputValueContainsAsync(page.GetByTestId("agents-catalog-model"), "llama3.2");
        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/agents");
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("crmhr-agent-search").FillAsync(agentName);
        await ExpectSingleAgentCardAsync(page, agentName);
        await page.GetByTestId("crmhr-agent-item-shell").Filter(new() { HasText = agentName }).GetByTestId("crmhr-agent-item").ClickAsync();
        await page.GetByTestId("crmhr-agent-open-directory-record").ClickAsync();
        await WaitForUrlContainsAsync(page, "/crm-hr/directory?partyId=");
        await page.GetByTestId("crmhr-party-display-name").WaitForAsync();
        await ExpectInputValueContainsAsync(page.GetByTestId("crmhr-party-display-name"), agentName);
        await ExpectInputValueContainsAsync(page.GetByTestId("crmhr-party-type"), PartyType.AiAgent.ToString());
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Crm_agent_directory_accepts_the_first_search_and_card_selection_only_once_interactive()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });
        var page = await context.NewPageAsync();
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var agentName = $"B09 Readiness Agent {suffix}";

        // The directory must contain a card this test owns, independent of test order or an empty database. Create it
        // through the catalog like a user: the running host then refreshes its directory snapshot in-process, which rows
        // seeded from another process could not guarantee within the snapshot lifetime.
        await CreateTechnicalAgentThroughCatalogAsync(page, $"B09 Readiness Provider {suffix}", agentName);
        await page.SetViewportSizeAsync(1400, 1000);
        var directoryUrl = $"{fixture.BaseUrl}/crm-hr/agents";

        // An ordinary first load completes the startup database prompt, so the held reload below shows only the directory.
        await page.GotoAsync(directoryUrl);
        await DismissStartupModalIfPresentAsync(page);

        // Hold the reload's circuit negotiation: until it is released the page is exactly the prerendered markup, where a
        // browser has no Blazor event handlers. Released deterministically after the assertions, never on a timer.
        var releaseCircuit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var negotiations = 0;
        await page.RouteAsync(new Regex(@"/_blazor/negotiate"), async route =>
        {
            if (Interlocked.Increment(ref negotiations) == 1)
            {
                await releaseCircuit.Task.WaitAsync(TimeSpan.FromMinutes(2));
            }

            await route.ContinueAsync();
        });

        var search = page.GetByTestId("crmhr-agent-search");
        try
        {
            await page.GotoAsync(directoryUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Assertions.Expect(search).ToBeVisibleAsync();
            await Assertions.Expect(search).ToBeDisabledAsync();

            // Cards may still be loading in the prerendered markup; any card that is already rendered must not look
            // selectable before the circuit can handle the click.
            var prerenderedCards = page.GetByTestId("crmhr-agent-item");
            var prerenderedCardCount = await prerenderedCards.CountAsync();
            for (var index = 0; index < prerenderedCardCount; index++)
            {
                await Assertions.Expect(prerenderedCards.Nth(index)).ToBeDisabledAsync();
            }
        }
        finally
        {
            releaseCircuit.TrySetResult();
        }

        await DismissStartupModalIfPresentAsync(page);

        // First search after the circuit attaches: Fill waits for the enabled input, and the filter must actually apply.
        await search.FillAsync($"zz-no-agent-{Guid.NewGuid():N}");
        await Assertions.Expect(page.GetByTestId("crmhr-agent-empty")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("crmhr-agent")).ToContainTextAsync(new Regex(@"(?<![0-9])0 matching record\(s\)"));

        // First card click: the owned agent's card must open its record without a retry.
        await search.FillAsync(agentName);
        await ExpectSingleAgentCardAsync(page, agentName);
        var ownedCard = page.GetByTestId("crmhr-agent-item-shell").Filter(new() { HasText = agentName }).GetByTestId("crmhr-agent-item");
        await Assertions.Expect(ownedCard).ToBeEnabledAsync();
        await ownedCard.ClickAsync();
        await Assertions.Expect(page.GetByTestId("crmhr-agent-record-dialog")).ToBeVisibleAsync();
        await WaitForUrlContainsAsync(page, "/crm-hr/agents?partyId=");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private async Task CreateTechnicalAgentThroughCatalogAsync(IPage page, string providerName, string agentName)
    {
        await SeedAgentDependenciesAsync(providerName);

        var catalogUrl = $"{fixture.BaseUrl}/agents?tab=agents";
        try {
            var response = await page.GotoAsync(catalogUrl);
            Assert.NotNull(response);
            Assert.True(response.Ok, $"Expected the agent catalog to return 2xx, got {response.Status}.");
            await DismissStartupModalIfPresentAsync(page);
            Assert.Equal(catalogUrl, page.Url);
            await page.GetByTestId("agents-catalog-new").ClickAsync();
        } catch {
            try {
                var hostLog = fixture.GetLogSnapshot();
                output.WriteLine(JsonSerializer.Serialize(new {
                    Path = new Uri(page.Url).AbsolutePath,
                    ShellCount = await page.GetByTestId("agents-shell-tabs").CountAsync(),
                    CatalogCount = await page.GetByTestId("agents-catalog-workspace").CountAsync(),
                    NewAgentCount = await page.GetByTestId("agents-catalog-new").CountAsync(),
                    CircuitErrorVisible = await page.Locator("#blazor-error-ui").IsVisibleAsync(),
                    StartupPromptCount = await page.GetByTestId("database-startup-modal").CountAsync(),
                    ExceptionTypes = Regex.Matches(hostLog, @"\b(?:CanDoItAll|System|Microsoft|Npgsql)\.(?:[A-Za-z_][A-Za-z0-9_]*\.)*[A-Za-z_][A-Za-z0-9_]*Exception\b")
                        .Select(match => match.Value).Distinct(StringComparer.Ordinal).Take(32).ToArray(),
                    StackMethods = Regex.Matches(hostLog, @"(?m)^\s*at\s+(?<method>(?:CanDoItAll|System|Microsoft|Npgsql)\.[A-Za-z0-9_.+<>]+)\(")
                        .Select(match => match.Groups["method"].Value).Distinct(StringComparer.Ordinal).Take(32).ToArray()
                }));
            } catch (Exception diagnosticFailure) {
                output.WriteLine($"Browser diagnostic capture failed: {diagnosticFailure.GetType().Name}");
            }
            throw;
        }
        await page.GetByTestId("agents-catalog-name").WaitForAsync();

        await page.GetByTestId("agents-catalog-name").FillAsync(agentName);
        await page.GetByTestId("agents-catalog-role").FillAsync("Delivery analyst");
        await page.GetByTestId("agents-catalog-summary").FillAsync("Coordinates structured analysis and guarded delivery support.");
        await page.GetByTestId("agents-catalog-instructions").FillAsync("Review the brief, keep the runtime explicit, and create durable delivery evidence.");
        await page.GetByRole(AriaRole.Tab, new() { Name = "Runtime", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByTestId("agents-catalog-provider")).ToContainTextAsync(providerName);
        await page.GetByTestId("agents-catalog-provider").SelectOptionAsync(new SelectOptionValue { Label = providerName });
        await page.GetByTestId("agents-catalog-model-override").CheckAsync();
        await page.GetByTestId("agents-catalog-model").FillAsync("llama3.2");
        await page.GetByTestId("agents-catalog-save").ClickAsync();

        await ExpectTextContainsAsync(page.Locator("body"), "Technical agent saved.");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    // "1 matching record(s)" is also a substring of "11 matching record(s)", so a text wait alone can pass before the
    // search applies. Require exactly one rendered card, and that it is the searched agent.
    private static async Task ExpectSingleAgentCardAsync(IPage page, string agentName)
    {
        await Assertions.Expect(page.GetByTestId("crmhr-agent")).ToContainTextAsync(new Regex(@"(?<![0-9])1 matching record\(s\)"));
        await Assertions.Expect(page.GetByTestId("crmhr-agent-item-shell")).ToHaveCountAsync(1);
        await Assertions.Expect(page.GetByTestId("crmhr-agent-item-shell")).ToContainTextAsync(agentName);
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
        var providerAdministration = scope.ServiceProvider.GetRequiredService<IProviderAdministrationService>();
        var providerSave = await providerAdministration.SaveProviderAsync(new ProviderProfileEditorModel
        {
            Name = providerName,
            ConnectorPluginKey = ProviderConnectorKeys.OllamaRemote,
            ModelPrices = [
                new() { Model = "llama3.1", TariffKind = CanDoItAll.AgentFramework.Models.ProviderTariffKind.ExplicitFree },
                new() { Model = "llama3.2", TariffKind = CanDoItAll.AgentFramework.Models.ProviderTariffKind.ExplicitFree }
            ],
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
            TestDatabaseProviderKind.PostgreSql,
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

        var observed = await locator.InnerTextAsync();
        throw new TimeoutException($"Timed out waiting for text '{expectedValue}'. Observed: {observed[..Math.Min(observed.Length, 5000)]}");
    }

    // The startup database prompt is raised by the layout after its asynchronous profile load, which can complete after the
    // routed page has already rendered; a fixed short poll therefore races it. Use the fixture's startup contract instead.
    private static Task DismissStartupModalIfPresentAsync(IPage page)
        => PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);

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
