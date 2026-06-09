using CanDoItAll.Modules.Processes;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AppSmokeTests {
    [Fact]
    public async Task Project_scoped_process_workspace_SB010_INV_001_preserves_project_and_launch_plan_context() {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright", "project-scoped-process-launch");
        ResetDirectory(artifactsDir);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize {
                Width = 1900,
                Height = 1200
            }
        });

        var page = await context.NewPageAsync();
        using var apiClient = CreateProcessApiClient(fixture.BaseUrl);
        var projectName = $"SB010 Project {Guid.NewGuid():N}";
        var projectId = await CreateProjectAsync(page, projectName, "Execution");

        var response = await page.GotoAsync($"{fixture.BaseUrl}/projects/{projectId:D}/processes");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected project process route to return 2xx, got {(int)response.Status}.");

        await DismissStartupModalIfPresentAsync(page, timeoutMs: 15_000);
        await page.GetByTestId("processes-workspace-shell").WaitForAsync();
        await page.GetByTestId("processes-templates-button").WaitForAsync();
        await page.GetByTestId("processes-templates-button").ClickAsync();

        var templateDialog = page.GetByTestId("processes-template-library-dialog");
        await templateDialog.WaitForAsync();
        await templateDialog.GetByPlaceholder("Search templates, roles, artifacts, governance, or evidence")
            .FillAsync("Business plan development");
        await page.GetByTestId("processes-template-library-item-business-plan-development").WaitForAsync();
        await page.GetByTestId("processes-template-library-item-business-plan-development").ClickAsync();
        await templateDialog.GetByRole(AriaRole.Heading, new() {
            Name = "Business plan development",
            Exact = true
        }).WaitForAsync();
        await WaitForBodyTextAsync(page, projectName, 30_000);
        await templateDialog.ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "01-project-template-selected-large-desktop.png")
        });

        await page.GetByTestId("processes-template-library-add-button").ClickAsync();
        var definition = await WaitForProjectDefinitionAsync(
            apiClient,
            projectId,
            "Business plan development",
            candidate => candidate.StepCount > 0,
            30_000);
        Assert.Equal(projectId, definition.ProjectId);

        await templateDialog.GetByRole(AriaRole.Button, new() {
            Name = "Close",
            Exact = true
        }).ClickAsync();
        await templateDialog.WaitForAsync(new() {
            State = WaitForSelectorState.Detached
        });

        await PostApiAsync(apiClient, $"/api/processes/definitions/{definition.Id:D}/publish");
        definition = await WaitForProjectDefinitionAsync(
            apiClient,
            projectId,
            "Business plan development",
            candidate => candidate.Id == definition.Id && candidate.HasPublishedVersion,
            30_000);

        var launchName = $"SB010 project launch {Guid.NewGuid():N}";
        response = await page.GotoAsync($"{fixture.BaseUrl}/projects/{projectId:D}/processes?processId={definition.Id:D}");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected selected project process route to return 2xx, got {(int)response.Status}.");
        await page.GetByTestId("processes-detail-tabs").WaitForAsync();
        await WaitForBodyTextAsync(page, "Business plan development", 30_000);
        await page.GetByRole(AriaRole.Tab, new() {
            Name = "Runs",
            Exact = true
        }).ClickAsync();
        await page.GetByTestId("processes-runs-tab-shell").WaitForAsync();
        await page.GetByTestId("processes-runs-tabs").GetByRole(AriaRole.Tab, new() {
            Name = "Launch",
            Exact = true
        }).ClickAsync();
        await page.GetByTestId("processes-launch-name-input").FillAsync(launchName);
        await page.GetByTestId("processes-create-launch-plan-button").ClickAsync();
        await WaitForBodyTextAsync(page, "Launch plan created.", 30_000);
        await page.GetByTestId("processes-launch-plan-detail").WaitForAsync();
        await WaitForBodyTextAsync(page, launchName, 30_000);
        await page.ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "02-project-launch-plan-created-large-desktop.png"),
            FullPage = false
        });

        var launchPlan = await WaitForProjectLaunchPlanAsync(
            apiClient,
            projectId,
            definition.Id,
            launchName,
            plan => plan.Status == ProcessLaunchPlanStatus.Draft,
            30_000);
        Assert.Equal(projectId, launchPlan.ProjectId);

        response = await page.GotoAsync($"{fixture.BaseUrl}/projects/{projectId:D}/processes?processId={definition.Id:D}&launchPlanId={launchPlan.Id:D}");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected selected project launch plan route to return 2xx, got {(int)response.Status}.");
        await page.GetByTestId("processes-detail-tabs").WaitForAsync();
        await page.GetByRole(AriaRole.Tab, new() {
            Name = "Runs",
            Exact = true
        }).ClickAsync();
        await page.GetByTestId("processes-runs-tabs").GetByRole(AriaRole.Tab, new() {
            Name = "Launch",
            Exact = true
        }).ClickAsync();
        await page.GetByTestId("processes-launch-plan-detail").WaitForAsync();
        await WaitForBodyTextAsync(page, launchName, 30_000);
        await page.ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "03-project-launch-plan-query-large-desktop.png"),
            FullPage = false
        });

        Assert.Contains($"/projects/{projectId:D}/processes", page.Url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"processId={definition.Id:D}", page.Url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"launchPlanId={launchPlan.Id:D}", page.Url, StringComparison.OrdinalIgnoreCase);
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private static async Task<ProcessDefinitionListItem> WaitForProjectDefinitionAsync(
        HttpClient client,
        Guid projectId,
        string definitionName,
        Func<ProcessDefinitionListItem, bool> predicate,
        int timeoutMs) {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt) {
            var definitions = await ReadRequiredJsonAsync<IReadOnlyList<ProcessDefinitionListItem>>(
                client,
                $"/api/processes/definitions?projectId={projectId:D}");
            var match = definitions
                .Where(definition => definition.ProjectId == projectId)
                .Where(definition => string.Equals(definition.Name, definitionName, StringComparison.Ordinal))
                .Where(predicate)
                .OrderByDescending(definition => definition.UpdatedAtUtc)
                .FirstOrDefault();
            if (match is not null) {
                return match;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Timed out waiting for project process definition '{definitionName}'.");
    }

    private static async Task<ProcessLaunchPlanListItem> WaitForProjectLaunchPlanAsync(
        HttpClient client,
        Guid projectId,
        Guid definitionId,
        string launchName,
        Func<ProcessLaunchPlanListItem, bool> predicate,
        int timeoutMs) {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt) {
            var launchPlans = await ReadRequiredJsonAsync<IReadOnlyList<ProcessLaunchPlanListItem>>(
                client,
                $"/api/processes/launch-plans?definitionId={definitionId:D}&projectId={projectId:D}&take=50");
            var match = launchPlans
                .Where(plan => plan.ProjectId == projectId)
                .Where(plan => string.Equals(plan.Name, launchName, StringComparison.Ordinal))
                .Where(predicate)
                .OrderByDescending(plan => plan.UpdatedAtUtc)
                .FirstOrDefault();
            if (match is not null) {
                return match;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Timed out waiting for project launch plan '{launchName}'.");
    }
}
