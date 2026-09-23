using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

public sealed partial class AppSmokeTests
{
    private const string DisposedObjectMarker = "Cannot access a disposed object";

    // Probe for the recorded shared Dialog open/close race: on 2026-09-14 a task dialog submitted about 100 ms after it
    // appeared dropped the circuit with "Cannot access a disposed object" for DotNetObjectReference<Dialog> thrown from
    // Dialog.OnAfterRenderAsync (CanDoItAll.Components BaseLib, a read-only dependency of this repository). The task
    // itself was committed. Quarantined because the owner fix lives in Components; run it explicitly to reproduce or
    // disprove the observation, and remove the quarantine when the owner fix is pinned.
    [Fact]
    [Trait("Category", "Quarantined")]
    [Trait("Surface", "SharedCanvas")]
    public async Task Project_structure_task_dialog_survives_submits_issued_immediately_after_it_opens()
    {
        const int attempts = 8;
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
        await CreateProjectAsync(
            page,
            $"Playwright Fast Task Dialog {DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            "Validation");
        await page.WaitForSelectorAsync("[data-testid='project-structure-canvas-loaded']");
        var disposedMarkersBefore = CountDisposedObjectMarkers();

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            await EnsureStructureToolboxWindowExpandedAsync(page);
            await EnsureStructureToolboxGroupExpandedAsync(page, "work");
            await page.GetByTestId("project-structure-toolbox-add-work-task").ClickAsync();

            var dialog = page.GetByTestId("project-structure-task-create-dialog");
            var taskTitle = $"Fast submit task {attempt}";
            await dialog.GetByTestId("project-structure-task-create-title").FillAsync(taskTitle);
            await dialog.GetByTestId("project-structure-task-create-submit").ClickAsync();

            var circuitError = page.Locator("#blazor-error-ui");
            await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached, Timeout = 15_000 });
            Assert.False(
                await circuitError.IsVisibleAsync(),
                $"Attempt {attempt}: the circuit failed after an immediate task dialog submit. " +
                DescribeNewHostExceptions(disposedMarkersBefore));
            await page.WaitForSelectorAsync($"text={taskTitle}");
        }

        Assert.Equal(disposedMarkersBefore, CountDisposedObjectMarkers());
    }

    private int CountDisposedObjectMarkers()
        => Regex.Matches(fixture.GetLogSnapshot(100_000), Regex.Escape(DisposedObjectMarker)).Count;

    private string DescribeNewHostExceptions(int disposedMarkersBefore)
    {
        var log = fixture.GetLogSnapshot(100_000);
        var exceptionTypes = Regex
            .Matches(log, @"\b(?:CanDoItAll|System|Microsoft)\.(?:[A-Za-z_][A-Za-z0-9_]*\.)*[A-Za-z_][A-Za-z0-9_]*Exception\b")
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .Take(16);
        return $"Disposed-object markers: {disposedMarkersBefore} before, {CountDisposedObjectMarkers()} after. " +
               $"Host exception types: {string.Join(", ", exceptionTypes)}.";
    }
}
