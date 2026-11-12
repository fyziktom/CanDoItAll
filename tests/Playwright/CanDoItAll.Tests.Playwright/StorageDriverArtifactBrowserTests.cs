using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using SkiaSharp;

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

        // The full Chromium channel supplies the native PDF viewer required by this proof.
        await using var browser = await fixture.Playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = "chromium" });
        Guid projectId;
        string projectRootId;
        Guid storageCatalogId;

        await using (var desktopContext = await browser.NewContextAsync(new BrowserNewContextOptions
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

            await Assertions.Expect(page.GetByTestId("project-structure-selection-window")).ToContainTextAsync(uploadedNodeTitle);
            await CapturePdfPreviewAsync(page, uploadedFile.Name, Path.Combine(screenshotsRoot, "workbench-preview-desktop.png"));

            await InvokeStructureCreateActionAsync(
                page,
                "add-infrastructure-storage",
                projectRootId,
                projectRootId,
                storageNodeTitle,
                "Storage ownership",
                "Storage-backed project asset lane for the workbench proof.",
                [
                    new CanvasInputValueSeed("storageCatalogId", storageCatalogId.ToString("D")),
                    new CanvasInputValueSeed("storagePurpose", nameof(CanDoItAll.Infrastructure.Storage.StorageUsagePurpose.ProjectAsset)),
                    new CanvasInputValueSeed("storagePathPrefix", "projects/storage-driver-proof/assets"),
                    new CanvasInputValueSeed("connectionReference", "/storage/projects/storage-driver-proof/assets")
                ]);
            await SelectStructureOutlineNodeAsync(page, storageNodeTitle);
            await page.GetByTestId("project-structure-storage-summary").WaitForAsync();
            await CaptureWorkbenchShellAsync(page, Path.Combine(screenshotsRoot, "workbench-storage-node-desktop.png"));
        }

        await using (var narrowContext = await browser.NewContextAsync(new BrowserNewContextOptions
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
            await CapturePdfPreviewAsync(page, uploadedFile.Name, Path.Combine(screenshotsRoot, "workbench-preview-narrow.png"));

            await SelectStructureOutlineNodeAsync(page, storageNodeTitle);
            await page.GetByTestId("project-structure-storage-summary").WaitForAsync();
            await CaptureWorkbenchShellAsync(page, Path.Combine(screenshotsRoot, "workbench-storage-node-narrow.png"));
        }
    }

    private async Task CapturePdfPreviewAsync(IPage page, string fileName, string screenshotPath) {
        try {
            await page.GetByRole(AriaRole.Button, new() { Name = "Expand preview", Exact = true }).ClickAsync();
            var preview = page.GetByRole(AriaRole.Dialog, new() { Name = $"{fileName} file interaction", Exact = true });
            await preview.WaitForAsync();
            await Assertions.Expect(preview.GetByTestId("file-interaction")).ToHaveAttributeAsync("data-state", "loaded");
            await Assertions.Expect(preview.GetByTestId("interaction-pdf-view")).ToBeVisibleAsync();
            await Assertions.Expect(preview.GetByTestId("interaction-object-fallback")).Not.ToBeVisibleAsync();
            await WaitForRenderedPdfAsync(page, preview.GetByTestId("interaction-pdf-view"));
            await page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = false });
            await preview.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        } catch (Exception exception) {
            await File.WriteAllTextAsync(Path.ChangeExtension(screenshotPath, ".failure.txt"),
                exception + Environment.NewLine + string.Join(Environment.NewLine, page.Frames.Select(frame => frame.Url)) +
                Environment.NewLine + fixture.GetLogSnapshot(60));
            if (!page.IsClosed) {
                await page.ScreenshotAsync(new() { Path = Path.ChangeExtension(screenshotPath, ".failure.png") });
            }
            throw;
        }
    }

    private static async Task WaitForRenderedPdfAsync(IPage page, ILocator pdfView) {
        var marker = new SKColor(17, 203, 61);
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(30)) {
            var bounds = await pdfView.BoundingBoxAsync();
            Assert.NotNull(bounds);
            using var rendered = SKBitmap.Decode(await page.ScreenshotAsync());
            Assert.NotNull(rendered);
            using var visiblePdf = new SKBitmap();
            Assert.True(rendered.ExtractSubset(visiblePdf, new SKRectI(
                Math.Clamp((int)Math.Floor(bounds.X), 0, rendered.Width),
                Math.Clamp((int)Math.Floor(bounds.Y), 0, rendered.Height),
                Math.Clamp((int)Math.Ceiling(bounds.X + bounds.Width), 0, rendered.Width),
                Math.Clamp((int)Math.Ceiling(bounds.Y + bounds.Height), 0, rendered.Height))));
            // The fixture's green bar proves that the native PDF plugin painted its content.
            if (visiblePdf.Pixels.Count(pixel => pixel == marker) >= 500) {
                return;
            }
            await Task.Delay(100);
        }
        throw new TimeoutException("The native PDF viewer did not paint the uploaded document's marker.");
    }

    private async Task<Guid> CreateStorageCatalogEntryAsync(IPage page, string storageName, string storageRoot)
    {
        var response = await page.GotoAsync($"{fixture.BaseUrl}/settings?tab=storage");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /settings?tab=storage to return 2xx, got {(int)response.Status}.");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.WaitForSelectorAsync("text=Storage catalog");

        var previousName = await page.GetByTestId("storage-settings-name").ElementHandleAsync();
        Assert.NotNull(previousName);
        await page.GetByTestId("storage-settings-new-filesystem").ClickAsync();
        await previousName.WaitForElementStateAsync(ElementState.Hidden);
        await previousName.DisposeAsync();
        await page.GetByTestId("storage-settings-name").FillAsync(storageName);
        await page.GetByTestId("storage-settings-display-order").FillAsync("-100");

        await page.GetByRole(AriaRole.Button, new() { Name = "Next step", Exact = true }).ClickAsync();
        await page.GetByTestId("storage-settings-endpoint").FillAsync(storageRoot);
        await page.GetByTestId("storage-settings-test").ClickAsync();
        // An unsaved filesystem draft has no host binding; testing must not persist it.
        await Assertions.Expect(page.GetByTestId("storage-operation-facts").First).ToContainTextAsync("Catalog: NotAttempted. Routing: NotAttempted. Driver: Completed");
        await Assertions.Expect(page.GetByTestId("storage-operation-receipts")).ToContainTextAsync("Tested snapshot: Unavailable.");
        await Assertions.Expect(page.GetByTestId("storage-settings-test")).ToBeEnabledAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Next step", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByTestId("storage-settings-wizard").Locator("[aria-current=step]")).ToContainTextAsync("Routing");
        await page.GetByTestId("storage-settings-purpose-grid").WaitForAsync();
        await SetCheckboxAsync(page.GetByTestId("storage-settings-purpose-projectasset"), true);
        await SetCheckboxAsync(page.GetByTestId("storage-settings-purpose-promptattachment"), true);

        await SubmitValidFormAsync(page.GetByTestId("storage-settings-save"),
            page.GetByTestId("storage-operation-receipts"), "Catalog: Committed. Routing: Complete");
        await page.WaitForFunctionAsync(
            @"expectedName => Array.from(document.querySelectorAll('[data-testid^=""storage-catalog-row-""]'))
                .some(candidate => (candidate.textContent || '').includes(expectedName))",
            storageName);

        var storageCatalogId = await FindStorageCatalogIdByNameAsync(page, storageName);
        await VerifySavedStorageRootAsync(storageCatalogId, storageRoot);
        await page.GetByTestId("storage-refresh-catalog").ClickAsync();
        await Assertions.Expect(page.GetByTestId($"storage-catalog-row-{storageCatalogId:N}")).ToContainTextAsync("Healthy");
        var previousWizard = await page.GetByTestId("storage-settings-wizard").ElementHandleAsync();
        Assert.NotNull(previousWizard);
        await page.GetByTestId("storage-settings-new-filesystem").ClickAsync();
        await previousWizard.WaitForElementStateAsync(ElementState.Hidden);
        await previousWizard.DisposeAsync();
        await page.GetByTestId($"storage-catalog-row-{storageCatalogId:N}").ClickAsync();
        await Assertions.Expect(page.GetByTestId("storage-settings-name")).ToHaveValueAsync(storageName);
        return storageCatalogId;
    }

    private async Task VerifySavedStorageRootAsync(Guid storageId, string root) {
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            fixture.OwnedDatabaseProfile, "Storage.Artifact.Browser", TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?> { [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind });
        await using var scope = provider.CreateAsyncScope();
        var storage = await scope.ServiceProvider.GetRequiredService<IStorageCatalogService>().GetAsync(storageId);
        Assert.NotNull(storage);
        Assert.Equal(root, storage.EndpointOrRoot);
        Assert.Equal(HostBoundPathState.Active, storage.RootPathState);
        var tested = await scope.ServiceProvider.GetRequiredService<IStorageConnectionTestService>().TestAsync(storageId);
        Assert.True(tested.IsSuccess, tested.Message);
        Assert.Equal(StorageHealthStatus.Healthy, tested.HealthStatus);
    }

    private async Task CaptureSettingsStorageEvidenceAsync(IPage page, string storageName, string screenshotPath)
    {
        var response = await page.GotoAsync($"{fixture.BaseUrl}/settings?tab=storage");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /settings?tab=storage to return 2xx, got {(int)response.Status}.");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.WaitForSelectorAsync("text=Storage catalog");

        var storageCatalogId = await FindStorageCatalogIdByNameAsync(page, storageName);
        await page.GetByTestId($"storage-catalog-row-{storageCatalogId:N}").ClickAsync();
        await Assertions.Expect(page.GetByTestId("storage-settings-name")).ToHaveValueAsync(storageName);
        await page.ScreenshotAsync(new()
        {
            Path = screenshotPath,
            FullPage = false
        });
    }

    private static async Task<Guid> FindStorageCatalogIdByNameAsync(IPage page, string storageName)
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
        Assert.True(Guid.TryParse(storageCatalogId, out var parsed), $"Expected to find a storage catalog row for '{storageName}'.");
        return parsed;
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

    private static FilePayload BuildValidPreviewPdf(string fileName) {
        const string previewResourceName = "CanDoItAll.Tests.Playwright.Fixtures.storage-preview.pdf";
        using var resource = typeof(AppSmokeTests).Assembly.GetManifestResourceStream(previewResourceName)
            ?? throw new InvalidOperationException($"Embedded PDF fixture '{previewResourceName}' is missing.");
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);

        return new FilePayload {
            Name = fileName,
            MimeType = "application/pdf",
            Buffer = buffer.ToArray()
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
