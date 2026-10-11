using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Npgsql;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class ApplicationDashboardNativeBrowserTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_readers_preserve_populated_identity_partial_usage_and_stale_recovery(bool published) {
        await using var host = new DataSourcesBrowserHost();
        await host.StartAsync(published: published);
        var workflowId = WorkflowId.New();
        var workflowRunId = WorkflowRunId.New();
        var processRunId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using (var scope = host.Services.CreateAsyncScope()) {
            await scope.ServiceProvider.GetRequiredService<IWorkflowRunStore>().SaveRunAsync(new(
                workflowRunId, workflowId, WorkflowVersionId.New(), WorkflowRunState.Running,
                WorkflowRuntimeBackendKind.InProcess, workflowRunId.ToString(), "Persisted dashboard fixture", now, now));
            var database = scope.ServiceProvider.GetRequiredService<ProcessPersistenceDbContext>();
            database.RuntimeStates.Add(new() {
                RunId = processRunId, RootRunId = processRunId, PlanId = Guid.NewGuid(), PlanHash = "dashboard-fixture",
                Status = ProcessRuntimeStatus.Active, UpdatedAtUtc = now, ConcurrencyToken = Guid.NewGuid()
            });
            await database.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceStore>().SaveExecutionAsync(SandboxWorkspaceExecutionState.Empty with {
                ProviderUsageObservations = [
                    new(Guid.NewGuid(), now, "Dashboard fixture", ProviderKind.OpenAi, "fixture-model", ProviderTransportKind.Responses,
                        ProviderUsageSourcePhases.AgentRuntime, ProviderUsageObservationStatus.Observed, 60, 0, 40, 0, 100, 0) { CalculatedCostUsd = 0.25m },
                    new(Guid.NewGuid(), now, "Dashboard fixture", ProviderKind.OpenAi, "fixture-model", ProviderTransportKind.Responses,
                        ProviderUsageSourcePhases.AgentRuntime, ProviderUsageObservationStatus.MissingAfterProviderActivity, 0, 0, 0, 0, 0, 0)
                ]
            });
        }
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        var failedAssets = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        page.Response += (_, response) => {
            if (response.Status >= 400 && (response.Url.Contains(".css", StringComparison.Ordinal) || response.Url.Contains(".js", StringComparison.Ordinal) || response.Url.Contains(".woff", StringComparison.Ordinal))) {
                failedAssets.Add(response.Url);
            }
        };
        var evidence = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "components-decoupling-closure");
        Directory.CreateDirectory(evidence);
        var mode = published ? "published-native" : "source-native";
        await page.GotoAsync(host.BaseUrl + "/");
        var project = page.GetByTestId($"dashboard-project-{host.ProjectA:N}");
        await Assertions.Expect(project).ToHaveAttributeAsync("href", $"/projects?projectId={host.ProjectA:D}");
        await page.GetByTestId("database-shell-action").HoverAsync();
        await Assertions.Expect(page.GetByTestId("database-shell-flyout-card")).ToContainTextAsync("Owned workspace A");
        foreach (var (name, route) in new[] { ("projects", "/projects"), ("agents", "/agents"), ("processes", "/processes/live"), ("scheduler", "/scheduler") }) {
            await Assertions.Expect(page.GetByTestId("dashboard-action-" + name)).ToHaveAttributeAsync("href", route);
        }
        await Assertions.Expect(page.GetByTestId($"dashboard-workflow-{workflowRunId.Value:N}")).ToHaveAttributeAsync("href", $"/agents/workflows?workflowId={workflowId.Value:D}&runId={workflowRunId.Value:D}");
        await page.GetByTestId("dashboard-processes-tab").ClickAsync();
        await Assertions.Expect(page.GetByTestId($"dashboard-process-{processRunId:N}")).ToHaveAttributeAsync("href", $"/processes/live?runId={processRunId:D}");
        await Assertions.Expect(page.GetByText("Projection behind", new() { Exact = true })).ToBeVisibleAsync();
        var knownCost = page.GetByText("$0.2500 Known cost (USD)", new() { Exact = true });
        await Assertions.Expect(knownCost).ToBeVisibleAsync();
        await knownCost.HoverAsync();
        await Assertions.Expect(page.GetByText("Known cost excludes 1 usage observations without complete token or pricing data.", new() { Exact = true })).ToBeVisibleAsync();
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, mode + "-dashboard-populated.png") });
        await using var connection = new NpgsqlConnection(host.A.ConnectionString);
        await connection.OpenAsync();
        await using (var hide = new NpgsqlCommand("ALTER TABLE \"Projects_Projects\" RENAME TO \"CD1_Projects_Projects_Held\"", connection)) {
            await hide.ExecuteNonQueryAsync();
        }
        try {
            await page.GetByTestId("dashboard-refresh").ClickAsync();
            await Assertions.Expect(page.GetByTestId("dashboard-stale-warning")).ToBeVisibleAsync();
            await Assertions.Expect(project).ToBeVisibleAsync();
            await Assertions.Expect(knownCost).ToBeVisibleAsync();
            await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, mode + "-dashboard-stale.png") });
        } finally {
            await using var restore = new NpgsqlCommand("ALTER TABLE \"CD1_Projects_Projects_Held\" RENAME TO \"Projects_Projects\"", connection);
            await restore.ExecuteNonQueryAsync();
        }
        await page.GetByTestId("dashboard-refresh").ClickAsync();
        await Assertions.Expect(page.GetByTestId("dashboard-stale-warning")).ToHaveCountAsync(0);
        await Assertions.Expect(project).ToBeVisibleAsync();
        await project.ClickAsync();
        await page.WaitForURLAsync(url => url.Contains($"projectId={host.ProjectA:D}", StringComparison.Ordinal));
        await page.GoBackAsync();
        await Assertions.Expect(project).ToBeVisibleAsync();
        await page.GetByTestId("shell-runtime-capabilities-action").ClickAsync();
        await Assertions.Expect(page.GetByTestId("runtime-readiness-summary")).ToBeVisibleAsync();
        await page.GetByTestId("runtime-capabilities-refresh").ClickAsync();
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, mode + "-runtime.png") });
        await page.GotoAsync(host.BaseUrl + "/dashboard");
        await Assertions.Expect(project).ToBeVisibleAsync();
        Assert.Empty(errors);
        Assert.Empty(failedAssets);
        await File.WriteAllTextAsync(Path.Combine(evidence, mode + "-dashboard.json"), JsonSerializer.Serialize(new {
            published, host.ProjectA, workflowId, workflowRunId, processRunId, errors, failedAssets,
            seededThroughNativePersistence = true, workflowOrProcessExecutionClaimed = false, controlledOwnerReadFailureRecovered = true
        }));
    }
}
