using System.Net.Http.Json;
using CanDoItAll.Modules.Processes;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AppSmokeTests {
    [Fact]
    public async Task Process_start_SB015_INV_001_large_screen_imports_template_and_executes_ready_launch_from_ui() {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright", "process-start-smoke");
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
        var response = await page.GotoAsync($"{fixture.BaseUrl}/processes");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /processes to return 2xx, got {(int)response.Status}.");

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
        await templateDialog.ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "01-template-selected-large-desktop.png")
        });

        await page.GetByTestId("processes-template-library-add-button").ClickAsync();
        var definition = await WaitForDefinitionAsync(
            apiClient,
            "Business plan development",
            candidate => candidate.StepCount > 0,
            30_000);
        await templateDialog.GetByRole(AriaRole.Button, new() {
            Name = "Close",
            Exact = true
        }).ClickAsync();
        await templateDialog.WaitForAsync(new() {
            State = WaitForSelectorState.Detached
        });

        await PostApiAsync(apiClient, $"/api/processes/definitions/{definition.Id:D}/publish");
        definition = await WaitForDefinitionAsync(
            apiClient,
            "Business plan development",
            candidate => candidate.Id == definition.Id && candidate.HasPublishedVersion,
            30_000);

        var launchName = $"SB015 process start smoke {Guid.NewGuid():N}";
        response = await page.GotoAsync($"{fixture.BaseUrl}/processes?processId={definition.Id:D}");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected selected process route to return 2xx, got {(int)response.Status}.");
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
        await page.GetByTestId("processes-runs-tab-shell").ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "02-runs-tab-before-launch-large-desktop.png")
        });
        await page.GetByTestId("processes-launch-name-input").FillAsync(launchName);
        await page.GetByTestId("processes-create-launch-plan-button").ClickAsync();
        await WaitForBodyTextAsync(page, "Launch plan created.", 30_000);
        await page.GetByTestId("processes-launch-plan-detail").WaitForAsync();
        await page.GetByTestId("processes-launch-plan-detail").ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "02-launch-plan-created-large-desktop.png")
        });

        var launchPlan = await WaitForLaunchPlanAsync(
            apiClient,
            definition.Id,
            launchName,
            plan => plan.Status == ProcessLaunchPlanStatus.Draft,
            30_000);
        await PostApiAsync(apiClient, $"/api/processes/launch-plans/{launchPlan.Id:D}/hr-match?requestedBy=sb015-playwright");
        await PostApiAsync(apiClient, $"/api/processes/launch-plans/{launchPlan.Id:D}/submit-approval?requestedBy=sb015-playwright");
        await PostJsonApiAsync(
            apiClient,
            "/api/processes/launch-plans/approval-decisions",
            new ProcessLaunchApprovalDecisionRequest {
                LaunchPlanId = launchPlan.Id,
                Status = ProcessLaunchApprovalStatus.Approved,
                ResolutionSummary = "SB015 large-screen process-start smoke approved the UI-created launch plan.",
                DecidedBy = "sb015-playwright"
            });
        await PostApiAsync(apiClient, $"/api/processes/launch-plans/{launchPlan.Id:D}/provision?requestedBy=sb015-playwright");
        launchPlan = await WaitForLaunchPlanAsync(
            apiClient,
            definition.Id,
            launchName,
            plan => plan.Id == launchPlan.Id && plan.Status == ProcessLaunchPlanStatus.Ready,
            30_000);

        response = await page.GotoAsync($"{fixture.BaseUrl}/processes?processId={definition.Id:D}&launchPlanId={launchPlan.Id:D}");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected ready launch route to return 2xx, got {(int)response.Status}.");
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
        await WaitForEnabledTestIdAsync(page, "processes-launch-execute-button", 30_000);
        await page.GetByTestId("processes-launch-execute-button").ClickAsync();
        await WaitForBodyTextAsync(page, "Launch plan executed into a process run.", 30_000);

        var run = await WaitForRunAsync(
            apiClient,
            definition.Id,
            launchName,
            30_000);
        await page.GetByTestId("processes-runs-tabs").GetByRole(AriaRole.Tab, new() {
            Name = "Activity",
            Exact = true
        }).ClickAsync();
        var runHistoryItem = page.GetByTestId($"processes-run-history-item-{run.Id:D}");
        await runHistoryItem.WaitForAsync();
        await runHistoryItem.ClickAsync();
        var runStepsDialog = page.GetByTestId("processes-run-steps-dialog");
        await runStepsDialog.GetByTestId("processes-run-steps-dialog-step-list").WaitForAsync();
        await runStepsDialog.GetByRole(AriaRole.Button, new() {
            Name = "Close",
            Exact = true
        }).ClickAsync();
        await runStepsDialog.WaitForAsync(new() {
            State = WaitForSelectorState.Detached
        });
        await page.GetByTestId("processes-selected-run-summary").WaitForAsync();
        var selectedRunSummary = await page.GetByTestId("processes-selected-run-summary").InnerTextAsync();
        Assert.Contains(launchName, selectedRunSummary, StringComparison.Ordinal);
        Assert.Contains(run.TotalStepCount.ToString(), selectedRunSummary, StringComparison.Ordinal);
        await page.GetByTestId("processes-selected-run-summary").ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "03-run-selected-large-desktop.png")
        });

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private static HttpClient CreateProcessApiClient(string baseUrl) {
        return new HttpClient {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(45)
        };
    }

    private static async Task<ProcessDefinitionListItem> WaitForDefinitionAsync(
        HttpClient client,
        string definitionName,
        Func<ProcessDefinitionListItem, bool> predicate,
        int timeoutMs) {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt) {
            var definitions = await ReadRequiredJsonAsync<IReadOnlyList<ProcessDefinitionListItem>>(
                client,
                "/api/processes/definitions");
            var match = definitions
                .Where(definition => string.Equals(definition.Name, definitionName, StringComparison.Ordinal))
                .Where(predicate)
                .OrderByDescending(definition => definition.UpdatedAtUtc)
                .FirstOrDefault();
            if (match is not null) {
                return match;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Timed out waiting for process definition '{definitionName}'.");
    }

    private static async Task<ProcessLaunchPlanListItem> WaitForLaunchPlanAsync(
        HttpClient client,
        Guid definitionId,
        string launchName,
        Func<ProcessLaunchPlanListItem, bool> predicate,
        int timeoutMs) {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt) {
            var plans = await ReadRequiredJsonAsync<IReadOnlyList<ProcessLaunchPlanListItem>>(
                client,
                $"/api/processes/launch-plans?definitionId={definitionId:D}&take=50");
            var match = plans
                .Where(plan => string.Equals(plan.Name, launchName, StringComparison.Ordinal))
                .Where(predicate)
                .OrderByDescending(plan => plan.UpdatedAtUtc)
                .FirstOrDefault();
            if (match is not null) {
                return match;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Timed out waiting for launch plan '{launchName}'.");
    }

    private static async Task<ProcessRunListItem> WaitForRunAsync(
        HttpClient client,
        Guid definitionId,
        string runName,
        int timeoutMs) {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt) {
            var runs = await ReadRequiredJsonAsync<IReadOnlyList<ProcessRunListItem>>(
                client,
                $"/api/processes/runs?definitionId={definitionId:D}&take=50");
            var match = runs
                .Where(run => string.Equals(run.Name, runName, StringComparison.Ordinal))
                .OrderByDescending(run => run.UpdatedAtUtc)
                .FirstOrDefault();
            if (match is not null) {
                return match;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Timed out waiting for process run '{runName}'.");
    }

    private static async Task WaitForEnabledTestIdAsync(IPage page, string testId, int timeoutMs) {
        var locator = page.GetByTestId(testId);
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt) {
            if (await locator.IsEnabledAsync()) {
                return;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Timed out waiting for '{testId}' to become enabled.");
    }

    private static async Task<T> ReadRequiredJsonAsync<T>(HttpClient client, string requestUri) {
        using var response = await client.GetAsync(requestUri);
        await AssertApiSuccessAsync(response);
        var value = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(value);
        return value;
    }

    private static async Task PostApiAsync(HttpClient client, string requestUri) {
        using var response = await client.PostAsync(requestUri, content: null);
        await AssertApiSuccessAsync(response);
    }

    private static async Task PostJsonApiAsync<T>(HttpClient client, string requestUri, T payload) {
        using var response = await client.PostAsJsonAsync(requestUri, payload);
        await AssertApiSuccessAsync(response);
    }

    private static async Task AssertApiSuccessAsync(HttpResponseMessage response) {
        if (response.IsSuccessStatusCode) {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {body}");
    }
}
