using System.Text.Json;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tools.Documents;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AppSmokeTests
{
    private const string SpreadsheetAssetFileName = "agent-hardening-acceptance.xlsx";
    private const string SpreadsheetAssetTitle = "Agent hardening acceptance workbook";
    private const string SpreadsheetContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [Fact]
    [Trait("Surface", "ProjectStructure")]
    public async Task Project_structure_xlsx_asset_opens_bounded_governed_preview_without_managed_file_url()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1700,
                Height = 1100
            }
        });
        var page = await context.NewPageAsync();
        var projectId = await CreateProjectAsync(page, "Playwright XLSX governed preview", "Validation");
        await page.GetByTestId("project-structure-canvas-loaded").WaitForAsync();
        var workbookBytes = CreateAcceptanceWorkbook();
        var assetNodeId = await CreateSpreadsheetAssetAsync(context, projectId, workbookBytes);

        var consoleErrors = new List<string>();
        var pageErrors = new List<string>();
        var failedRequests = new List<string>();
        var requestedUrls = new List<string>();
        page.Console += (_, message) =>
        {
            if (string.Equals(message.Type, "error", StringComparison.OrdinalIgnoreCase))
            {
                consoleErrors.Add(message.Text);
            }
        };
        page.PageError += (_, message) => pageErrors.Add(message);

        var reloadResponse = await page.ReloadAsync();
        Assert.NotNull(reloadResponse);
        Assert.True(
            reloadResponse!.Ok,
            $"Expected the Project Structure page reload to return 2xx, got {(int)reloadResponse.Status}.");
        await page.GetByTestId("project-structure-canvas-loaded").WaitForAsync();
        page.RequestFailed += (_, request) =>
            failedRequests.Add($"{request.Method} {request.Url}: {request.Failure}");
        page.Request += (_, request) => requestedUrls.Add(request.Url);

        await EnsureStructureObjectIndexWindowExpandedAsync(page);
        var outlineNode = page.GetByTestId(BuildProjectStructureOutlineNodeTestId(assetNodeId));
        await outlineNode.WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        await outlineNode.ClickAsync();

        var selectionWindow = page.GetByTestId("project-structure-selection-window");
        await EnsureFloatingWindowExpandedAsync(page, "project-structure-selection-window");
        await Assertions.Expect(selectionWindow).ToContainTextAsync(SpreadsheetAssetTitle);
        var expandPreview = selectionWindow.GetByRole(
            AriaRole.Button,
            new() { Name = "Expand preview", Exact = true });
        await expandPreview.ClickAsync();

        var dialog = page.GetByRole(
            AriaRole.Dialog,
            new() { Name = $"{SpreadsheetAssetFileName} file interaction", Exact = true });
        await dialog.WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        await AssertSpreadsheetPreviewAsync(dialog);

        var sizeToggle = dialog.GetByTestId("project-structure-dialog-size-toggle");
        await Assertions.Expect(dialog).ToHaveAttributeAsync("data-maximized", "false");
        await sizeToggle.ClickAsync();
        await Assertions.Expect(dialog).ToHaveAttributeAsync("data-maximized", "true");
        await Assertions.Expect(sizeToggle).ToHaveAttributeAsync("aria-label", "Restore preview size");
        await AssertSpreadsheetPreviewAsync(dialog);

        await sizeToggle.ClickAsync();
        await Assertions.Expect(dialog).ToHaveAttributeAsync("data-maximized", "false");
        await Assertions.Expect(sizeToggle).ToHaveAttributeAsync("aria-label", "Maximize preview");

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await dialog.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Detached,
            Timeout = 10_000
        });

        await expandPreview.ClickAsync();
        await dialog.WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        await AssertSpreadsheetPreviewAsync(dialog);

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        Assert.DoesNotContain(
            requestedUrls,
            url => url.Contains("managed-files", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            consoleErrors.Count == 0,
            $"XLSX preview emitted console errors:{Environment.NewLine}{string.Join(Environment.NewLine, consoleErrors)}");
        Assert.True(
            pageErrors.Count == 0,
            $"XLSX preview emitted page errors:{Environment.NewLine}{string.Join(Environment.NewLine, pageErrors)}");
        Assert.True(
            failedRequests.Count == 0,
            $"XLSX preview emitted failed requests:{Environment.NewLine}{string.Join(Environment.NewLine, failedRequests)}");
    }

    private static async Task AssertSpreadsheetPreviewAsync(ILocator dialog)
    {
        await dialog.GetByTestId("project-structure-direct-file-interaction").WaitForAsync();
        await Assertions.Expect(dialog.GetByTestId("project-structure-file-interaction-policy"))
            .ToContainTextAsync("bounded, read-only preview");
        var preview = dialog.GetByTestId("workbench-spreadsheet-preview");
        await preview.WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        Assert.Equal(0, await dialog.GetByTestId("workbench-spreadsheet-preview-unavailable").CountAsync());
        Assert.Equal(0, await dialog.Locator("iframe").CountAsync());
        Assert.DoesNotContain("managed-files", await dialog.InnerHTMLAsync(), StringComparison.OrdinalIgnoreCase);

        var worksheet = preview.GetByTestId("workbench-spreadsheet-worksheet");
        await Assertions.Expect(worksheet).ToContainTextAsync("Acceptance");
        var grid = worksheet.GetByTestId("workbench-spreadsheet-grid");
        await grid.WaitForAsync();
        await Assertions.Expect(grid).ToContainTextAsync("Acceptance case");
        await Assertions.Expect(grid).ToContainTextAsync("Project Structure agent hardening");
        await Assertions.Expect(grid).ToContainTextAsync("Ready");
        await Assertions.Expect(grid).ToContainTextAsync("Verified");
        await Assertions.Expect(grid).ToContainTextAsync("=COUNTA(B2:B3)");
    }

    private async Task<string> CreateSpreadsheetAssetAsync(
        IBrowserContext context,
        Guid projectId,
        byte[] workbookBytes)
    {
        var response = await context.APIRequest.PostAsync(
            $"{fixture.BaseUrl}/api/project-structure/projects/{projectId:D}/assets",
            new APIRequestContextOptions
            {
                DataObject = new
                {
                    objectType = ProjectObjectType.File,
                    title = SpreadsheetAssetTitle,
                    subtitle = "Real XLSX browser acceptance",
                    notes = "Known cells and a formula must render through governed FileInteraction.",
                    media = new
                    {
                        fileName = SpreadsheetAssetFileName,
                        contentType = SpreadsheetContentType,
                        base64Data = Convert.ToBase64String(workbookBytes)
                    },
                    parentNodeKey = $"project:{projectId:D}",
                    objectSubtype = "xlsx"
                }
            });
        var responseBody = await response.TextAsync();
        Assert.True(
            response.Ok,
            $"Expected XLSX asset creation to return 2xx, got {response.Status}: {responseBody}");

        using var responseDocument = JsonDocument.Parse(responseBody);
        return responseDocument.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Project Structure asset creation returned no node id.");
    }

    private static byte[] CreateAcceptanceWorkbook()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "candoitall-playwright-xlsx",
            Guid.NewGuid().ToString("N"));
        var workbookPath = Path.Combine(temporaryDirectory, SpreadsheetAssetFileName);
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var spreadsheets = new ClosedXmlSpreadsheetDocumentService();
            spreadsheets.Write(new SpreadsheetWriteRequest(
                workbookPath,
                workbookPath,
                "Acceptance",
                [
                    new SpreadsheetCellWrite("A1", "Acceptance case"),
                    new SpreadsheetCellWrite("B1", "Status"),
                    new SpreadsheetCellWrite("A2", "Project Structure agent hardening"),
                    new SpreadsheetCellWrite("B2", "Ready"),
                    new SpreadsheetCellWrite("A3", "Governed XLSX preview"),
                    new SpreadsheetCellWrite("B3", "Verified"),
                    new SpreadsheetCellWrite("A4", "Verified status count"),
                    new SpreadsheetCellWrite("B4", "=COUNTA(B2:B3)")
                ],
                [],
                CreateWorkbookIfMissing: true,
                Overwrite: true));
            return File.ReadAllBytes(workbookPath);
        }
        finally
        {
            if (File.Exists(workbookPath))
            {
                File.Delete(workbookPath);
            }

            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory);
            }
        }
    }

    private static string BuildProjectStructureOutlineNodeTestId(string nodeId)
    {
        var buffer = new char[nodeId.Length];
        for (var index = 0; index < nodeId.Length; index++)
        {
            var character = nodeId[index];
            buffer[index] = char.IsLetterOrDigit(character)
                ? char.ToLowerInvariant(character)
                : '-';
        }

        return $"project-structure-outline-node-{new string(buffer)}";
    }
}
