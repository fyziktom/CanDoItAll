using CanDoItAll.Modules.Processes;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AppSmokeTests
{
    [Fact]
    public async Task Process_step_operation_contract_editor_controls_work_in_browser()
    {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright", "process-step-operation-contract");
        ResetDirectory(artifactsDir);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1440,
                Height = 1000
            }
        });

        var page = await context.NewPageAsync();
        var response = await page.GotoAsync($"{fixture.BaseUrl}/processes");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /processes to return 2xx, got {(int)response.Status}.");
        await DismissStartupModalIfPresentAsync(page, timeoutMs: 15_000);

        await page.GetByTestId("processes-new-definition-button").ClickAsync();
        await page.GetByTestId("processes-detail-tabs").WaitForAsync();
        await page.GetByRole(AriaRole.Tab, new() { Name = "Steps", Exact = true }).ClickAsync();
        await page.GetByTestId("processes-add-step-button").ClickAsync();

        var firstStepCard = page.GetByTestId("processes-step-card").First;
        await firstStepCard.WaitForAsync();

        var operationTargetScope = firstStepCard.GetByTestId("processes-operation-target-scope-select");
        await operationTargetScope.SelectOptionAsync(ProcessStepTargetScope.ExternalArtifactDestination.ToString());
        Assert.Equal(
            ProcessStepTargetScope.ExternalArtifactDestination.ToString(),
            await operationTargetScope.InputValueAsync());

        var externalArtifactOperation = firstStepCard.GetByTestId($"processes-operation-{ProcessStepOperation.WriteExternalArtifactDestination}");
        await externalArtifactOperation.CheckAsync();
        Assert.True(await externalArtifactOperation.IsCheckedAsync());

        await firstStepCard.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifactsDir, "operation-contract-editor.png")
        });

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }
}
