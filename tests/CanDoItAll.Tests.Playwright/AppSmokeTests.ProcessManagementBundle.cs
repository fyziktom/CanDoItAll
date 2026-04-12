using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AppSmokeTests
{
    [Fact]
    public async Task Process_management_template_library_flows_are_validated_in_browser()
    {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright", "process-template-library");
        ResetDirectory(artifactsDir);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1900,
                Height = 1200
            }
        });

        var page = await context.NewPageAsync();
        var response = await page.GotoAsync($"{fixture.BaseUrl}/processes");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /processes to return 2xx, got {(int)response.Status}.");

        var startupDialog = page.GetByTestId("database-startup-modal");
        if (await WaitForLocatorAsync(startupDialog, 15_000))
        {
            await page.GetByTestId("database-startup-continue").ClickAsync();
            await startupDialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        }

        await page.GetByTestId("processes-new-definition-button").ClickAsync();
        var definitionStepsTab = page.GetByRole(AriaRole.Tab, new() { Name = "Steps", Exact = true });
        await definitionStepsTab.WaitForAsync();
        await definitionStepsTab.ClickAsync();
        await page.GetByTestId("processes-add-step-button").WaitForAsync();
        await page.GetByTestId("processes-add-step-button").ClickAsync();
        var firstStepCard = page.GetByTestId("processes-step-card").First;
        await firstStepCard.WaitForAsync();
        await firstStepCard.Locator("input").Nth(1).FillAsync("Template artifact target");

        await page.GetByTestId("processes-templates-button").WaitForAsync();
        await page.GetByTestId("processes-templates-button").ClickAsync();

        var templateDialog = page.GetByTestId("processes-template-library-dialog");
        await templateDialog.WaitForAsync();
        await templateDialog.GetByPlaceholder("Search templates, roles, artifacts, governance, or evidence").FillAsync("AI-assisted");
        await page.GetByTestId("processes-template-library-item-ai-assisted-change-delivery").WaitForAsync();
        await page.GetByTestId("processes-template-library-item-ai-assisted-change-delivery").ClickAsync();
        await templateDialog.GetByRole(AriaRole.Heading, new() { Name = "AI-assisted change delivery with guarded delegation", Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Tree, new() { Name = "Template structure", Exact = true }).WaitForAsync();

        await templateDialog.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifactsDir, "01-template-library-process-preview.png")
        });

        await templateDialog.GetByRole(AriaRole.Button, new() { Name = "Markdown", Exact = true }).ClickAsync();
        await page.Locator("[data-testid^='processes-template-library-markdown-']").First.WaitForAsync();

        await templateDialog.GetByRole(AriaRole.Button, new() { Name = "Diagrams", Exact = true }).ClickAsync();
        var flowchartDiagram = page.GetByTestId("processes-template-library-diagram-flowchart");
        await flowchartDiagram.WaitForAsync();
        var flowchartViewport = flowchartDiagram.Locator("div[style*='transform-origin: center center']").First;
        var styleBeforeZoom = await flowchartViewport.GetAttributeAsync("style");
        Assert.False(string.IsNullOrWhiteSpace(styleBeforeZoom));
        await flowchartDiagram.GetByRole(AriaRole.Button, new() { Name = "+", Exact = true }).ClickAsync();
        await page.WaitForTimeoutAsync(150);
        var styleAfterZoom = await flowchartViewport.GetAttributeAsync("style");
        Assert.False(string.IsNullOrWhiteSpace(styleAfterZoom));
        Assert.NotEqual(styleBeforeZoom, styleAfterZoom);
        await page.GetByTestId("processes-template-library-diagram-flowchart").ScreenshotAsync(new()
        {
            Path = Path.Combine(artifactsDir, "02-template-library-mermaid-preview.png")
        });

        await templateDialog.GetByRole(AriaRole.Button, new() { Name = "JSON", Exact = true }).ClickAsync();
        await page.GetByTestId("processes-template-library-json-definition-json").WaitForAsync();
        await page.GetByTestId("processes-template-library-json-definition-json").ScreenshotAsync(new()
        {
            Path = Path.Combine(artifactsDir, "03-template-library-json-preview.png")
        });

        var rolesTab = page.GetByRole(AriaRole.Tab, new() { Name = "Roles", Exact = true });
        await rolesTab.WaitForAsync();

        await templateDialog.GetByRole(AriaRole.Button, new() { Name = "Roles", Exact = true }).ClickAsync();
        await page.GetByTestId("processes-template-library-add-button").ClickAsync();
        await WaitForBodyTextAsync(page, "Role added", 15_000);
        var roleNotificationText = await page.Locator(".rz-notification").InnerTextAsync();
        Assert.Contains("Role added", roleNotificationText, StringComparison.OrdinalIgnoreCase);
        await templateDialog.WaitForAsync();

        var overlayProof = await page.EvaluateAsync<TemplateOverlayProof>(
            @"() => {
                const notification = document.querySelector('.rz-notification');
                const dialog = document.querySelector('[data-testid=""processes-template-library-dialog""]');
                return {
                    notificationZIndex: notification ? parseInt(getComputedStyle(notification).zIndex || '0', 10) : 0,
                    dialogZIndex: dialog ? parseInt(getComputedStyle(dialog).zIndex || '0', 10) : 0
                };
            }");
        Assert.NotNull(overlayProof);
        Assert.True(overlayProof!.NotificationZIndex > overlayProof.DialogZIndex, "Expected notifications to render above the templates modal.");

        await page.Locator(".rz-notification").First.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifactsDir, "04-template-library-notification-over-modal.png")
        });

        await templateDialog.GetByRole(AriaRole.Button, new() { Name = "Processes", Exact = true }).ClickAsync();
        await page.GetByTestId("processes-template-library-item-ai-assisted-change-delivery").WaitForAsync();
        await page.GetByTestId("processes-template-library-item-ai-assisted-change-delivery").ClickAsync();
        var artifactTarget = page.GetByTestId("processes-template-library-artifact-target");
        await artifactTarget.WaitForAsync();
        var selectedTarget = await artifactTarget.EvaluateAsync<string>(
            @"element => {
                const select = element;
                if (select.value) {
                    return select.value;
                }

                const option = Array.from(select.options).find(candidate => candidate.value);
                if (!option) {
                    return '';
                }

                select.value = option.value;
                select.dispatchEvent(new Event('change', { bubbles: true }));
                return select.value;
            }");
        Assert.False(string.IsNullOrWhiteSpace(selectedTarget), "Expected an artifact target step to be available for artifact imports.");

        var addArtifactButton = page.Locator("[data-testid^='processes-template-library-add-artifact-']").First;
        await addArtifactButton.WaitForAsync();
        await addArtifactButton.ClickAsync();
        await WaitForBodyTextAsync(page, "Artifact added", 15_000);
        var artifactNotificationText = await page.Locator(".rz-notification").InnerTextAsync();
        Assert.Contains("Artifact added", artifactNotificationText, StringComparison.OrdinalIgnoreCase);
        await templateDialog.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifactsDir, "05-template-library-role-and-artifact-imports.png")
        });

        await templateDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await templateDialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });

        await rolesTab.ClickAsync();
        await WaitForBodyTextAsync(page, "AI evaluation lead", 15_000);

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private sealed class TemplateOverlayProof
    {
        public int NotificationZIndex { get; set; }

        public int DialogZIndex { get; set; }
    }
}
