using System.IO;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AppSmokeTests
{
    [Fact]
    [Trait("Surface", "ProjectStructure")]
    public async Task Project_structure_markdown_right_click_opens_one_authoring_dialog_and_persists_content()
    {
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
        await CreateProjectAsync(page, "Markdown right-click authoring", "Validation");
        const string rootSelector = ".cw-node[data-node-id^='project:']";

        string[] menuLabels = await OpenCanvasContextMenuAsync(page, rootSelector);
        Assert.Contains(menuLabels, label => label.Contains("Assets", StringComparison.OrdinalIgnoreCase));
        await OpenContextSubmenuAsync(page, "group-assets");
        await ClickContextMenuActionAsync(page, "add-file-markdown");

        ILocator dialog = page.GetByTestId("project-structure-text-asset-create-dialog");
        await dialog.WaitForAsync();
        Assert.Equal(1, await dialog.CountAsync());
        Assert.Equal(0, await page.Locator(".cw-canvas-composer:visible").CountAsync());
        await page.GetByTestId("project-structure-text-asset-title").FillAsync("Architecture notes");
        await page.GetByTestId("project-structure-text-asset-file-name").FillAsync("architecture-notes.markdown");
        await page.GetByTestId("project-structure-text-asset-content").FillAsync(
            "# Architecture\n\nTyped storage boundary.");
        await page.GetByTestId("project-structure-text-asset-notes").FillAsync(
            "Describes the architecture decision.");
        await page.GetByTestId("project-structure-text-asset-submit").ClickAsync();

        await dialog.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Detached,
            Timeout = 20_000
        });
        await WaitForSceneNodeTitleAsync(page, "Architecture notes", timeoutMs: 20_000);
        string? mediaFileName = await page.EvaluateAsync<string?>(
            @"title => {
                const host = document.querySelector('.cw-canvas-host');
                const nodes = host?.__canvasWorkbenchState?.surface?.nodes || [];
                const node = nodes.find(candidate => candidate?.title === title);
                return node?.mediaFileName || null;
            }",
            "Architecture notes");
        Assert.Equal("architecture-notes.md", mediaFileName);
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    [Trait("Category", "Quarantined")]
    [Trait("Surface", "ProjectStructure")]
    [Trait("Artifacts", "Required")]
    public async Task Project_structure_artifacts_capture_required_canvas_evidence()
    {
        var repoRoot = GetRepoRoot();
        var screenshotsRoot = Path.Combine(repoRoot, "artifacts", "screenshots");
        var i04Root = Path.Combine(screenshotsRoot, "i04");
        var i08Root = Path.Combine(screenshotsRoot, "i08");
        var i17Root = Path.Combine(screenshotsRoot, "i17");
        var i19Root = Path.Combine(screenshotsRoot, "i19");
        var i23Root = Path.Combine(screenshotsRoot, "i23");

        ResetDirectory(i04Root);
        ResetDirectory(i08Root);
        ResetDirectory(i17Root);
        ResetDirectory(i19Root);
        ResetDirectory(i23Root);

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
        await SeedProviderProfilesAsync(page);

        var projectId = await CreateProjectAsync(page, "Playwright Artifact Validation", "Validation");
        var projectRootId = await ReadNodeIdAsync(page, ".cw-node[data-node-id^='project:']");

        var recordingId = await InvokeStructureCreateActionAsync(
            page,
            "add-recording",
            projectRootId,
            projectRootId,
            "Kickoff recording",
            "Discovery sync",
            "Recording captured for transcript and LLM validation.",
            [
                new CanvasInputValueSeed("recordingSource", "Teams recording"),
                new CanvasInputValueSeed("storageReference", "workspace://meetings/kickoff.mp4"),
                new CanvasInputValueSeed("durationMinutes", "52")
            ]);

        var transcriptId = await InvokeStructureCreateActionAsync(
            page,
            "add-transcript",
            recordingId,
            recordingId,
            "Kickoff transcript",
            "Discovery sync transcript",
            string.Empty,
            [
                new CanvasInputValueSeed("recordingRef", recordingId),
                new CanvasInputValueSeed("transcriptText", "Alice: We need the toolbox redesign validated in the browser.\nBob: Export progress workbook and Gantt evidence from the same summary source.\nChris: Keep provider confirmation explicit before any transcript action.")
            ]);

        var featureId = await InvokeStructureCreateActionAsync(
            page,
            "add-block-feature",
            projectRootId,
            projectRootId,
            "Canvas editor rollout",
            "Validation track",
            "Use this branch for reconnect, summary, export, and delete confirmation evidence.");

        var summaryTaskId = await InvokeStructureCreateActionAsync(
            page,
            "add-work-task",
            featureId,
            featureId,
            "Capture screenshot evidence",
            "QA stream",
            "Capture the required evidence for the bundle.",
            [
                new CanvasInputValueSeed("dueUtc", "2026-04-10T15:00:00+00:00")
            ]);

        var exportTaskId = await InvokeStructureCreateActionAsync(
            page,
            "add-work-task",
            featureId,
            featureId,
            "Export workbook and Gantt",
            "Reporting",
            "Use the progress summary modal exports for proof.",
            [
                new CanvasInputValueSeed("dueUtc", "2026-04-11T16:30:00+00:00")
            ]);

        var reconnectTaskId = await InvokeStructureCreateActionAsync(
            page,
            "add-work-task",
            projectRootId,
            projectRootId,
            "Reconnect detached follow-up",
            "Backlog",
            "This task will be reparented into the feature branch.",
            [
                new CanvasInputValueSeed("dueUtc", "2026-04-12T12:00:00+00:00")
            ]);

        await InvokeStructureCreateActionAsync(
            page,
            "add-file-pdf",
            projectRootId,
            projectRootId,
            "Architecture evidence PDF",
            "docs/architecture",
            "Typed PDF validation node.",
            uploadedFile: BuildUploadedFile(
                "architecture-evidence.pdf",
                "application/pdf",
                "%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF"));

        await InvokeStructureCreateActionAsync(
            page,
            "add-file-excel",
            projectRootId,
            projectRootId,
            "Validation workbook",
            "reports",
            "Typed spreadsheet validation node.",
            uploadedFile: BuildUploadedFile(
                "validation-workbook.csv",
                "text/csv",
                "name,status\nexports,ready\nsummary,ready"));

        await InvokeStructureCreateActionAsync(
            page,
            "add-file-docx",
            projectRootId,
            projectRootId,
            "Project brief docx",
            "docs/briefs",
            "Typed docx validation node.",
            uploadedFile: BuildUploadedFile(
                "project-brief.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "Fake docx payload for UI evidence only."));

        await InvokeStructureCreateActionAsync(
            page,
            "add-file-text",
            projectRootId,
            projectRootId,
            "Runbook text",
            "docs/runbooks",
            "Operator checklist and rollout notes.",
            uploadedFile: BuildUploadedFile(
                "runbook.txt",
                "text/plain",
                "Validate the release, capture evidence, and record the rollout result."));

        await InvokeStructureCreateActionAsync(
            page,
            "add-file-json",
            projectRootId,
            projectRootId,
            "Settings JSON",
            "config",
            "Runtime settings for strict toolbox validation.",
            uploadedFile: BuildUploadedFile(
                "settings.json",
                "application/json",
                "{\n  \"toolbox\": true,\n  \"validation\": \"strict\"\n}"));

        await InvokeStructureCreateActionAsync(
            page,
            "add-file-markdown",
            projectRootId,
            projectRootId,
            "Evidence README",
            "docs",
            "Validation evidence index.",
            uploadedFile: BuildUploadedFile(
                "README.md",
                "text/markdown",
                "# Validation evidence\n\nCapture screenshots and exports."));

        await InvokeStructureCreateActionAsync(
            page,
            "add-file-mermaid",
            projectRootId,
            projectRootId,
            "Validation flow diagram",
            "docs/diagrams",
            "Visualizes the bundle validation timeline.",
            uploadedFile: BuildUploadedFile(
                "validation-flow.mmd",
                "text/vnd.mermaid",
                "gantt\n    title Bundle validation timeline\n    dateFormat YYYY-MM-DD\n    section Evidence\n    Capture screenshots :done, a1, 2026-04-08, 2d\n    Export workbook :active, a2, 2026-04-10, 2d"));

        await FocusCanvasRootAsync(page);
        await SetCanvasZoomPercentAsync(page, 58);

        await EnsureStructureToolboxWindowExpandedAsync(page);
        await CaptureWorkbenchShellAsync(page, Path.Combine(i23Root, "01-primary-state.png"));
        var structureToolboxSearch = page.Locator("[data-testid='project-structure-standard-blocks-toolbox'] .project-structure-toolbox__search");
        await structureToolboxSearch.FillAsync("task");
        await page.WaitForFunctionAsync(
            @"() => {
                const items = Array.from(document.querySelectorAll('[data-testid^=""project-structure-toolbox-""]'));
                return items.length > 0 &&
                    items.some(item => (item.textContent || '').toLowerCase().includes('task'));
            }");
        await CaptureWorkbenchShellAsync(page, Path.Combine(i23Root, "02-secondary-state.png"));
        await page.EvaluateAsync(
            @"() => {
                const sections = document.querySelector('[data-testid=""project-structure-standard-blocks-toolbox""] .project-structure-toolbox__sections');
                if (sections instanceof HTMLElement) {
                    sections.scrollTop = Math.max(260, sections.scrollHeight * 0.35);
                }
            }");
        await page.WaitForTimeoutAsync(180);
        await CaptureWorkbenchShellAsync(page, Path.Combine(i23Root, "03-interaction-result.png"));
        await structureToolboxSearch.FillAsync(string.Empty);

        await EnsureCanvasSelectionAsync(page, SelectorForNodeId(recordingId));
        await OpenNodeQuickActionsAsync(page, SelectorForNodeId(recordingId));
        var quickActionsDialog = page.GetByTestId("project-structure-node-quick-actions");
        await quickActionsDialog.WaitForAsync();
        await quickActionsDialog.GetByTestId("project-structure-quick-action-primary").WaitForAsync();
        await CaptureWorkbenchShellAsync(page, Path.Combine(i04Root, "01-primary-state.png"));
        await quickActionsDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await page.WaitForFunctionAsync("() => !document.querySelector('[data-testid=\"project-structure-node-quick-actions\"]')");

        await EnsureCanvasSelectionAsync(page, SelectorForNodeId(transcriptId));
        await page.GetByTestId("project-structure-selection-window").WaitForAsync();
        await CaptureLocatorAsync(page.GetByTestId("project-structure-selection-window"), Path.Combine(i04Root, "02-secondary-state.png"));

        await page.GetByRole(AriaRole.Button, new() { Name = "Summarize", Exact = true }).ClickAsync();
        var transcriptDialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Summarize confirmation" });
        await transcriptDialog.WaitForAsync();
        await page.EvaluateAsync(
            @"() => {
                const select = document.querySelector('[aria-label=""Summarize confirmation""] select');
                if (select instanceof HTMLSelectElement) {
                    select.size = Math.min(select.options.length, 3);
                    select.style.height = 'auto';
                }
            }");
        await page.WaitForFunctionAsync(
            @"() => {
                const select = document.querySelector('[aria-label=""Summarize confirmation""] select');
                return select instanceof HTMLSelectElement &&
                    Array.from(select.options).some(option => (option.textContent || '').includes('OpenAI API')) &&
                    Array.from(select.options).some(option => (option.textContent || '').includes('Local Ollama'));
            }");
        await CaptureLocatorAsync(transcriptDialog, Path.Combine(i04Root, "03-interaction-result.png"));
        await page.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();

        await FocusCanvasRootAsync(page);
        await SetCanvasZoomPercentAsync(page, 34);
        await page.WaitForTimeoutAsync(250);
        await CaptureCanvasSurfaceAsync(page, Path.Combine(i08Root, "01-primary-state.png"));

        await SelectStructureOutlineNodeAsync(page, "Validation flow diagram");
        await page.GetByRole(AriaRole.Button, new() { Name = "View Mermaid", Exact = true }).ClickAsync();
        var mermaidDialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Validation flow diagram Mermaid viewer" });
        await mermaidDialog.WaitForAsync();
        await CaptureLocatorAsync(mermaidDialog, Path.Combine(i08Root, "02-secondary-state.png"));
        await page.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await CaptureWorkbenchShellAsync(page, Path.Combine(i08Root, "03-interaction-result.png"));

        await SelectStructureOutlineNodeAsync(page, "Reconnect detached follow-up");
        await page.GetByRole(AriaRole.Button, new() { Name = "Reconnect", Exact = true }).ClickAsync();
        await page.WaitForSelectorAsync("text=Reconnect mode");
        await CaptureWorkbenchShellAsync(page, Path.Combine(i17Root, "01-primary-state.png"));
        await SelectStructureOutlineNodeAsync(page, "Canvas editor rollout");
        await page.WaitForTimeoutAsync(220);
        await CaptureWorkbenchShellAsync(page, Path.Combine(i17Root, "04-reconnect-result.png"));

        await SelectStructureOutlineNodeAsync(page, "Canvas editor rollout");
        await page.GetByTestId("project-structure-node-actions")
            .GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true })
            .ClickAsync();
        var deleteDialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Delete Canvas editor rollout" });
        await deleteDialog.WaitForAsync();
        await CaptureLocatorAsync(deleteDialog, Path.Combine(i17Root, "02-secondary-state.png"));
        await deleteDialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();

        await SelectCanvasNodesAsync(page, [summaryTaskId, exportTaskId], summaryTaskId);
        await page.GetByTestId("project-structure-selection-window").WaitForAsync();
        await page.Locator(".cw-floating-window[data-testid='project-structure-selection-window'] input[placeholder='Name this border']").FillAsync("Delivery swimlane");
        await page.GetByRole(AriaRole.Button, new() { Name = "Border", Exact = true }).ClickAsync();
        await WaitForSceneFrameLabelAsync(page, "Delivery swimlane");
        await CaptureCanvasSurfaceAsync(page, Path.Combine(i17Root, "03-interaction-result.png"));

        await EnsureCanvasSelectionAsync(page, SelectorForNodeId(featureId));
        await page.GetByRole(AriaRole.Button, new() { Name = "Summary", Exact = true }).ClickAsync();
        var summaryDialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Canvas editor rollout progress summary" });
        await summaryDialog.WaitForAsync();
        await CaptureLocatorAsync(summaryDialog, Path.Combine(i19Root, "01-primary-state.png"));

        var summaryStatusSelect = summaryDialog.Locator(".project-structure-summary-row").Filter(new() { HasText = "Capture screenshot evidence" }).Locator("select");
        await summaryStatusSelect.SelectOptionAsync(new[]
        {
            new SelectOptionValue
            {
                Label = "Blocked"
            }
        });
        await page.WaitForFunctionAsync(
            @"() => {
                const row = Array.from(document.querySelectorAll('.project-structure-summary-row'))
                    .find(candidate => (candidate.textContent || '').includes('Capture screenshot evidence'));
                const select = row?.querySelector('select');
                if (!(select instanceof HTMLSelectElement)) {
                    return false;
                }

                const selectedOption = select.options[select.selectedIndex];
                return (selectedOption?.textContent || '').trim() === 'Blocked';
            }");
        await CaptureLocatorAsync(summaryDialog, Path.Combine(i19Root, "02-secondary-state.png"));

        await summaryDialog.GetByRole(AriaRole.Button, new() { Name = "Export XLSX", Exact = true }).ClickAsync();
        Assert.True(
            await WaitForNodeTitleInStateAsync(page, "Canvas editor rollout progress workbook", timeoutMs: 15_000),
            "Expected the workbook export node to exist in canvas state.");
        await page.GetByText("was exported as an Excel attachment.", new() { Exact = false }).WaitForAsync();
        await summaryDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await SelectStructureOutlineNodeAsync(page, "Canvas editor rollout");
        await page.GetByRole(AriaRole.Button, new() { Name = "Summary", Exact = true }).ClickAsync();
        await summaryDialog.WaitForAsync();
        await summaryDialog.GetByRole(AriaRole.Button, new() { Name = "Export Gantt", Exact = true }).ClickAsync();
        Assert.True(
            await WaitForNodeTitleInStateAsync(page, "Canvas editor rollout gantt", timeoutMs: 15_000),
            "Expected the Gantt export node to exist in canvas state.");
        await page.GetByText("was exported as a Mermaid Gantt node.", new() { Exact = false }).WaitForAsync();
        await summaryDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await SelectStructureOutlineNodeAsync(page, "Canvas editor rollout gantt");
        await SetCanvasZoomPercentAsync(page, 52);
        await page.WaitForTimeoutAsync(250);
        await CaptureCanvasSurfaceAsync(page, Path.Combine(i19Root, "03-interaction-result.png"));

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }
}
