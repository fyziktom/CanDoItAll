using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

public sealed partial class AppSmokeTests
{
    [Fact]
    [Trait("Category", "Quarantined")]
    [Trait("Surface", "StorageDriver")]
    [Trait("Artifacts", "Required")]
    public async Task StorageDriver_settings_and_workbench_artifacts_capture_required_browser_evidence()
    {
        var repoRoot = GetRepoRoot();
        var screenshotsRoot = Path.Combine(repoRoot, "artifacts", "screenshots", "storage-driver");
        Directory.CreateDirectory(screenshotsRoot);
        DeleteFileIfExists(Path.Combine(screenshotsRoot, "settings-storage-desktop.png"));
        DeleteFileIfExists(Path.Combine(screenshotsRoot, "settings-storage-narrow.png"));
        DeleteFileIfExists(Path.Combine(screenshotsRoot, "workbench-upload-desktop.png"));
        DeleteFileIfExists(Path.Combine(screenshotsRoot, "workbench-upload-narrow.png"));
        DeleteFileIfExists(Path.Combine(screenshotsRoot, "workbench-preview-desktop.png"));
        DeleteFileIfExists(Path.Combine(screenshotsRoot, "workbench-preview-narrow.png"));
        DeleteFileIfExists(Path.Combine(screenshotsRoot, "workbench-storage-node-desktop.png"));
        DeleteFileIfExists(Path.Combine(screenshotsRoot, "workbench-storage-node-narrow.png"));

        Assert.False(string.IsNullOrWhiteSpace(fixture.StorageWorkspaceRoot), "Expected the Playwright fixture to expose the workspace storage root.");

        var storageName = "Playwright assets lane";
        var storageRoot = Path.Combine(fixture.StorageWorkspaceRoot!, "storage-driver-proof", "assets-lane");
        var uploadedNodeTitle = "Architecture evidence PDF";
        var storageNodeTitle = "Project assets lane";
        var uploadedFile = BuildValidPreviewPdf("architecture-evidence.pdf");

        Directory.CreateDirectory(storageRoot);

        Guid projectId;
        string projectRootId;
        string storageCatalogId;

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
            storageCatalogId = await CreateStorageCatalogEntryAsync(page, storageName, storageRoot);
            await page.ScreenshotAsync(new()
            {
                Path = Path.Combine(screenshotsRoot, "settings-storage-desktop.png"),
                FullPage = false
            });

            projectId = await CreateProjectAsync(page, "Playwright Storage Driver Proof", "Validation");
            projectRootId = await ReadNodeIdAsync(page, ".cw-node[data-node-id^='project:']");

            await OpenPdfComposerAndPopulateAsync(
                page,
                ".cw-node[data-node-id^='project:']",
                uploadedNodeTitle,
                "docs/storage-driver",
                "Browser proof for the storage-backed typed upload flow.",
                uploadedFile);
            await CaptureWorkbenchShellAsync(page, Path.Combine(screenshotsRoot, "workbench-upload-desktop.png"));
            await SubmitCreateComposerAsync(page);
            await WaitForSceneNodeTitleAsync(page, uploadedNodeTitle, selectedOnly: true);

            await SelectStructureOutlineNodeAsync(page, uploadedNodeTitle);
            await page.GetByTestId("project-structure-selection-window").WaitForAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Expand preview", Exact = true }).ClickAsync();
            await page.Locator(".project-structure-preview-dialog").WaitForAsync();
            await CaptureWorkbenchShellAsync(page, Path.Combine(screenshotsRoot, "workbench-preview-desktop.png"));
            await page.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();

            await InvokeStructureCreateActionAsync(
                page,
                "add-infrastructure-storage",
                projectRootId,
                projectRootId,
                storageNodeTitle,
                "Storage ownership",
                "Storage-backed project asset lane for the workbench proof.",
                [
                    new CanvasInputValueSeed("storageCatalogId", storageCatalogId),
                    new CanvasInputValueSeed("storagePurpose", nameof(CanDoItAll.Infrastructure.Storage.StorageUsagePurpose.ProjectAsset)),
                    new CanvasInputValueSeed("storagePathPrefix", "projects/storage-driver-proof/assets"),
                    new CanvasInputValueSeed("connectionReference", "/storage/projects/storage-driver-proof/assets")
                ]);
            await SelectStructureOutlineNodeAsync(page, storageNodeTitle);
            await page.GetByTestId("project-structure-storage-summary").WaitForAsync();
            await CaptureWorkbenchShellAsync(page, Path.Combine(screenshotsRoot, "workbench-storage-node-desktop.png"));
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
            await CaptureSettingsStorageEvidenceAsync(page, storageName, Path.Combine(screenshotsRoot, "settings-storage-narrow.png"));

            var response = await page.GotoAsync($"{fixture.BaseUrl}/projects/{projectId:D}/structure");
            Assert.NotNull(response);
            Assert.True(response!.Ok, $"Expected the storage-driver proof workbench route to return 2xx, got {(int)response.Status}.");
            await page.WaitForSelectorAsync("[data-testid='project-structure-canvas-loaded']");
            await page.Locator(".cw-workbench-shell").WaitForAsync();

            await OpenPdfComposerAndPopulateAsync(
                page,
                ".cw-node[data-node-id^='project:']",
                "Responsive upload proof",
                "docs/storage-driver/narrow",
                "Responsive screenshot proof for the typed upload dialog.",
                uploadedFile);
            await CaptureWorkbenchShellAsync(page, Path.Combine(screenshotsRoot, "workbench-upload-narrow.png"));
            await page.Keyboard.PressAsync("Escape");

            await SelectStructureOutlineNodeAsync(page, uploadedNodeTitle);
            await page.GetByTestId("project-structure-selection-window").WaitForAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Expand preview", Exact = true }).ClickAsync();
            await page.Locator(".project-structure-preview-dialog").WaitForAsync();
            await CaptureWorkbenchShellAsync(page, Path.Combine(screenshotsRoot, "workbench-preview-narrow.png"));
            await page.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();

            await SelectStructureOutlineNodeAsync(page, storageNodeTitle);
            await page.GetByTestId("project-structure-storage-summary").WaitForAsync();
            await CaptureWorkbenchShellAsync(page, Path.Combine(screenshotsRoot, "workbench-storage-node-narrow.png"));
        }
    }

    private async Task<string> CreateStorageCatalogEntryAsync(IPage page, string storageName, string storageRoot)
    {
        var response = await page.GotoAsync($"{fixture.BaseUrl}/settings?tab=storage");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /settings?tab=storage to return 2xx, got {(int)response.Status}.");
        await DismissStartupModalIfPresentAsync(page);
        await page.WaitForSelectorAsync("text=Storage catalog");

        await page.GetByTestId("storage-settings-new-filesystem").ClickAsync();
        await page.GetByTestId("storage-settings-name").FillAsync(storageName);
        await page.GetByTestId("storage-settings-display-order").FillAsync("-100");

        await page.GetByRole(AriaRole.Button, new() { Name = "Next step", Exact = true }).ClickAsync();
        await page.GetByTestId("storage-settings-endpoint").FillAsync(storageRoot);
        await page.GetByTestId("storage-settings-test").ClickAsync();
        await page.WaitForFunctionAsync("() => (document.body.textContent || '').includes('Accessible local root')");

        await page.GetByRole(AriaRole.Button, new() { Name = "Next step", Exact = true }).ClickAsync();
        await page.GetByTestId("storage-settings-purpose-grid").WaitForAsync();
        await SetCheckboxAsync(page.GetByTestId("storage-settings-purpose-projectasset"), true);
        await SetCheckboxAsync(page.GetByTestId("storage-settings-purpose-promptattachment"), true);

        await page.GetByTestId("storage-settings-save").ClickAsync();
        await page.WaitForFunctionAsync(
            @"expectedName => Array.from(document.querySelectorAll('[data-testid^=""storage-catalog-row-""]'))
                .some(candidate => (candidate.textContent || '').includes(expectedName))",
            storageName);

        var storageCatalogId = await FindStorageCatalogIdByNameAsync(page, storageName);
        await page.Locator($"[data-testid='storage-catalog-row-{storageCatalogId}']").ClickAsync();
        await page.WaitForFunctionAsync("expectedName => document.querySelector('[data-testid=\"storage-settings-name\"]')?.value === expectedName", storageName);
        return storageCatalogId;
    }

    private async Task CaptureSettingsStorageEvidenceAsync(IPage page, string storageName, string screenshotPath)
    {
        var response = await page.GotoAsync($"{fixture.BaseUrl}/settings?tab=storage");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /settings?tab=storage to return 2xx, got {(int)response.Status}.");
        await DismissStartupModalIfPresentAsync(page);
        await page.WaitForSelectorAsync("text=Storage catalog");

        var storageCatalogId = await FindStorageCatalogIdByNameAsync(page, storageName);
        await page.Locator($"[data-testid='storage-catalog-row-{storageCatalogId}']").ClickAsync();
        await page.WaitForFunctionAsync("expectedName => document.querySelector('[data-testid=\"storage-settings-name\"]')?.value === expectedName", storageName);
        await page.ScreenshotAsync(new()
        {
            Path = screenshotPath,
            FullPage = false
        });
    }

    private static async Task<string> FindStorageCatalogIdByNameAsync(IPage page, string storageName)
    {
        var storageCatalogId = await page.EvaluateAsync<string>(
            @"expectedName => {
                const rows = Array.from(document.querySelectorAll('[data-testid^=""storage-catalog-row-""]'));
                const row = rows.filter(candidate => (candidate.textContent || '').includes(expectedName)).at(-1);
                const testId = row?.getAttribute('data-testid') || '';
                return testId.startsWith('storage-catalog-row-')
                    ? testId.substring('storage-catalog-row-'.length)
                    : '';
            }",
            storageName);
        Assert.False(string.IsNullOrWhiteSpace(storageCatalogId), $"Expected to find a storage catalog row for '{storageName}'.");
        return storageCatalogId;
    }

    private static async Task SetCheckboxAsync(ILocator locator, bool isChecked)
    {
        var current = await locator.IsCheckedAsync();
        if (current == isChecked)
        {
            return;
        }

        if (isChecked)
        {
            await locator.CheckAsync();
            return;
        }

        await locator.UncheckAsync();
    }

    private static async Task OpenPdfComposerAndPopulateAsync(
        IPage page,
        string selector,
        string title,
        string folder,
        string notes,
        FilePayload uploadedFile)
    {
        await OpenCanvasCreateComposerAsync(page, selector, "PDF", "add-file-pdf");
        await page.Locator(".cw-canvas-composer.is-dialog").WaitForAsync();
        await page.Locator(".cw-canvas-composer__file-input").SetInputFilesAsync([uploadedFile]);
        await page.WaitForFunctionAsync("() => !!document.querySelector('.cw-canvas-composer__upload-summary')?.textContent?.trim()");
        await page.Locator(".cw-canvas-composer__input").Nth(0).FillAsync(title);
        await page.Locator(".cw-canvas-composer__input").Nth(1).FillAsync(folder);
        await page.Locator(".cw-canvas-composer__textarea").FillAsync(notes);
        await page.WaitForFunctionAsync(
            @"() => {
                const button = document.querySelector('.cw-canvas-composer__actions .cw-button[data-tone=""accent""]');
                return button instanceof HTMLButtonElement && button.disabled !== true;
            }");
    }

    private static async Task SubmitCreateComposerAsync(IPage page)
    {
        await page.Locator(".cw-canvas-composer__actions .cw-button[data-tone='accent']").ClickAsync();
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-canvas-composer.is-dialog')");
    }

    private static FilePayload BuildValidPreviewPdf(string fileName)
    {
        const string previewPdfBase64 =
            "JVBERi0xLjQKMSAwIG9iajw8L1R5cGUvQ2F0YWxvZy9QYWdlcyAyIDAgUj4+ZW5kb2JqCjIgMCBvYmo8PC9UeXBlL1BhZ2VzL0NvdW50IDEvS2lkc1szIDAgUl0+PmVuZG9iagozIDAgb2JqPDwvVHlwZS9QYWdlL1BhcmVudCAyIDAgUi9NZWRpYUJveFswIDAgMzAwIDE0NF0vQ29udGVudHMgNCAwIFIvUmVzb3VyY2VzPDwvRm9udDw8L0YxIDUgMCBSPj4+Pj4+ZW5kb2JqCjQgMCBvYmo8PC9MZW5ndGggNzQ+PnN0cmVhbQpCVCAvRjEgMTggVGYgMzYgOTIgVGQgKEZlZWRiYWNrIDggUERGIHZhbGlkYXRpb24gYXNzZXQpIFRqIEVUCmVuZHN0cmVhbQplbmRvYmoKNSAwIG9iajw8L1R5cGUvRm9udC9TdWJ0eXBlL1R5cGUxL0Jhc2VGb250L0hlbHZldGljYT4+ZW5kb2JqCnhyZWYKMCA2CjAwMDAwMDAwMDAgNjU1MzUgZiAKMDAwMDAwMDAwOSAwMDAwMCBuIAowMDAwMDAwMDU4IDAwMDAwIG4gCjAwMDAwMDAxMTUgMDAwMDAgbiAKMDAwMDAwMDI0MSAwMDAwMCBuIAowMDAwMDAwMzY1IDAwMDAwIG4gCnRyYWlsZXI8PC9Scenario290IDEgMCBSL1NpemUgNj4+CnN0YXJ0eHJlZgo0MzUKJSVFT0YK";

        return new FilePayload
        {
            Name = fileName,
            MimeType = "application/pdf",
            Buffer = Convert.FromBase64String(previewPdfBase64)
        };
    }

    private static void DeleteFileIfExists(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        File.Delete(path);
    }

    private static async Task DismissStartupModalIfPresentAsync(IPage page)
    {
        var startupDialog = page.GetByTestId("database-startup-modal");
        if (!await WaitForLocatorAsync(startupDialog, 1_500))
        {
            return;
        }

        await page.GetByTestId("database-startup-continue").ClickAsync();
        await startupDialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }
}
