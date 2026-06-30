using System.Text;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class PromptLibraryVerificationTests
{
    [Fact]
    [Trait("Surface", "StorageDriver")]
    [Trait("Artifacts", "Required")]
    public async Task StorageDriver_prompt_factory_attachment_artifacts_capture_required_browser_evidence()
    {
        var repoRoot = GetRepoRoot();
        var screenshotsRoot = Path.Combine(repoRoot, "artifacts", "screenshots", "storage-driver");
        var proofRoot = Path.Combine(repoRoot, "output", "playwright", "storage-driver");
        var attachmentSourcePath = Path.Combine(proofRoot, "prompt-attachment.md");
        Directory.CreateDirectory(screenshotsRoot);
        Directory.CreateDirectory(proofRoot);
        await File.WriteAllTextAsync(
            attachmentSourcePath,
            "# Storage driver prompt attachment\n\nUse this markdown file to prove the prompt-factory storage summary and routing surface.",
            Encoding.UTF8);
        DeleteFileIfExists(Path.Combine(screenshotsRoot, "factory-attachments-desktop.png"));
        DeleteFileIfExists(Path.Combine(screenshotsRoot, "factory-attachments-narrow.png"));

        var input = new VerificationInputDefinition(
            "file",
            "File",
            "Storage routing brief",
            "docs/storage-driver/prompt-attachment.md",
            "Prompt-factory attachment proof routed through the storage layer.",
            attachmentSourcePath);

        await using (var desktopContext = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1900,
                Height = 1200
            }
        }))
        {
            var page = await desktopContext.NewPageAsync();
            await LoadPromptFactoryAsync(page);
            await ResetSessionAsync(page);
            await InvokeCanvasInputCreateActionAsync(page, input);
            await WaitForSingleInputNodeAsync(page);
            await page.Locator(".pf-page-tab").Filter(new() { HasText = "Assembly" }).First.ClickAsync();
            await page.WaitForSelectorAsync("text=Assembly workspace");
            await page.GetByRole(AriaRole.Tab, new() { Name = "Inputs", Exact = true }).ClickAsync();
            await page.WaitForSelectorAsync("text=Files, images, notes, and links attached to the session");
            await page.WaitForSelectorAsync("text=Storage routing brief");
            await page.WaitForSelectorAsync("text=Route");
            await page.ScreenshotAsync(new()
            {
                Path = Path.Combine(screenshotsRoot, "factory-attachments-desktop.png"),
                FullPage = false
            });
        }

        await using (var narrowContext = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1366,
                Height = 900
            }
        }))
        {
            var page = await narrowContext.NewPageAsync();
            await LoadPromptFactoryAsync(page);
            await ResetSessionAsync(page);
            await InvokeCanvasInputCreateActionAsync(page, input);
            await WaitForSingleInputNodeAsync(page);
            await page.Locator(".pf-page-tab").Filter(new() { HasText = "Assembly" }).First.ClickAsync();
            await page.WaitForSelectorAsync("text=Assembly workspace");
            await page.GetByRole(AriaRole.Tab, new() { Name = "Inputs", Exact = true }).ClickAsync();
            await page.WaitForSelectorAsync("text=Storage routing brief");
            await page.ScreenshotAsync(new()
            {
                Path = Path.Combine(screenshotsRoot, "factory-attachments-narrow.png"),
                FullPage = false
            });
        }
    }

    private static void DeleteFileIfExists(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        File.Delete(path);
    }
}
