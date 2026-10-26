using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Security;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

public sealed class WorkflowShellSmokeTests : IAsyncLifetime {
    private readonly PlaywrightAppFixture fixture = new();

    public Task InitializeAsync() => fixture.InitializeAsync();

    public Task DisposeAsync() => fixture.DisposeAsync();

    [Fact]
    public async Task Workflow_shell_creates_and_runs_starter_preview_on_large_screen()
    {
        const string output = "The starter workflow completed through its owned model fixture.";
        await using var upstream = await WorkflowResponseFixture.StartAsync(output, maximumOutputTokens: 800, temperature: 0.2);
        var providerId = await SeedPreviewProviderAsync(upstream.BaseUrl);
        var artifactDirectory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, ".artifacts", "agent-independent", "browser-captures");
        Directory.CreateDirectory(artifactDirectory);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });
        var page = await context.NewPageAsync();

        var response = await page.GotoAsync($"{fixture.BaseUrl}/agents/workflows");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /agents/workflows to return 2xx, got {(int)response.Status}.");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.GetByTestId("workflows-tabs").WaitForAsync();
        await page.GetByTestId("workflows-create-starter").WaitForAsync();
        await page.GetByTestId("workflows-create-starter").ClickAsync();
        await ExpectTextContainsAsync(page.Locator("body"), "Starter workflow and LLM component were created.", timeoutMs: 30_000);

        await page.GetByTestId("workflows-tab-workflows").ClickAsync();
        await page.GetByTestId("workflows-catalog").WaitForAsync();
        await page.GetByTestId("workflows-catalog-item").First.WaitForAsync();

        await using (var services = await TestApplicationBootstrap.BuildServiceProviderAsync(fixture.OwnedDatabaseProfile,
            "Workflow.Smoke.Readback", TestSchemaBootstrapModules.Full)) {
            await using var scope = services.CreateAsyncScope();
            var library = scope.ServiceProvider.GetRequiredService<IWorkflowComponentLibraryService>();
            var created = Assert.Single((await library.ListComponentsAsync()).Where(component => component.Name.StartsWith("Starter LLM call ", StringComparison.Ordinal)));
            Assert.Equal(providerId, created.ProviderProfileId);
        }
        await page.GetByTestId("workflows-tab-history").ClickAsync();
        await page.GetByTestId("workflows-run-test").WaitForAsync();
        await page.GetByTestId("workflows-run-test").ClickAsync();
        try {
            await upstream.ResponseWritten.WaitAsync(TimeSpan.FromSeconds(30));
            await Assertions.Expect(page.GetByTestId("workflows-test-result"))
                .ToContainTextAsync("Succeeded", new() { Timeout = 30_000 });
        } catch (Exception exception) {
            throw new InvalidOperationException($"The starter workflow did not complete. Model requests: {upstream.Requests}. " +
                $"Browser: {await page.Locator("body").InnerTextAsync()}. Host: {fixture.GetLogSnapshot(30)}", exception);
        }
        await page.GetByTestId("workflows-run-event").First.WaitForAsync();
        Assert.Equal(1, upstream.Requests);
        if (Environment.GetEnvironmentVariable("CANDOITALL_PLAYWRIGHT_CAPTURE_EVIDENCE") == "true") {
            await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(artifactDirectory, "workflow-shell-runtime-large.png"),
            FullPage = true
        });
        }

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private async Task<Guid> SeedPreviewProviderAsync(string baseUrl) {
        await using var services = await TestApplicationBootstrap.BuildServiceProviderAsync(fixture.OwnedDatabaseProfile,
            "Workflow.Smoke.Seed", TestSchemaBootstrapModules.Full);
        await using var scope = services.CreateAsyncScope();
        var secret = await scope.ServiceProvider.GetRequiredService<SecretService>().SaveAsync(new SecretEditorModel {
            Name = "Workflow smoke fixture credential", Kind = SecretKind.ApiKey,
            SecretValue = "local-fixture-credential", Scope = "workspace"
        });
        Assert.True(secret.IsSuccess);
        var model = ManagedSeedProviderFallbacks.OpenAiDefaultModel;
        var providerId = await scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>().SaveProviderAsync(new ProviderProfileEditorModel {
            Name = "000 Workflow smoke fixture", Kind = ProviderKind.OpenAi, Transport = ProviderTransportKind.Responses,
            BaseUrl = baseUrl, ApiKeyEnvironmentVariable = $"secret:{secret.Value:D}", DefaultModel = model,
            SuggestedModels = [model], SupportsStreaming = false, SupportsTools = false,
            ModelPrices = [new() { Model = model, TariffKind = ProviderTariffKind.ExplicitFree }]
        });
        Assert.Equal(providerId, (await scope.ServiceProvider.GetRequiredService<IWorkflowComponentLibraryService>()
            .ListProviderOptionsAsync()).First(option => option.IsEnabled).ProviderProfileId);
        return providerId;
    }

    private static async Task ExpectTextContainsAsync(ILocator locator, string expectedValue, int timeoutMs)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if ((await locator.InnerTextAsync()).Contains(expectedValue, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for text '{expectedValue}'.");
    }
}
