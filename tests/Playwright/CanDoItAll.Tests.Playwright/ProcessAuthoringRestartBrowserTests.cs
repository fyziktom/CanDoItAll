using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "LiveProcess")]
[Trait("Category", "HostPlatform")]
public sealed class ProcessAuthoringRestartBrowserTests {
    [Fact]
    public async Task Real_canvas_geometry_references_and_all_imports_survive_independent_session_and_process_restart() {
        await using var host = new ProcessAuthoringRestartHost();
        await host.StartAsync();
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1 });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        try {
            await OpenAsync(page, host);
            await page.GetByTestId("processes-detail-tab-steps").ClickAsync();
            await page.GetByTitle("Maximize canvas", new() { Exact = true }).ClickAsync();
            await Assertions.Expect(page.Locator(".cw-workbench-shell.is-maximized")).ToBeVisibleAsync();
            await page.GetByTestId("processes-canvas-toggle-toolbox").ClickAsync();
            var point = await ProcessCanvasBrowser.ReadNodePointAsync(page, "step:outcome");
            await page.Mouse.ClickAsync(point.X, point.Y);
            await Assertions.Expect(page.GetByTestId("processes-canvas-selection")).ToContainTextAsync("Published workflow outcome");
            await page.WaitForFunctionAsync("() => { const last = document.querySelector('.cw-canvas-host').__canvasWorkbenchState.lastPointerTarget; return !last || Date.now() - last.timestamp > 340; }");
            point = await ProcessCanvasBrowser.ReadNodePointAsync(page, "step:outcome");
            await page.Mouse.MoveAsync(point.X, point.Y);
            await page.Mouse.DownAsync();
            await page.Mouse.MoveAsync(point.X + 48, point.Y + 32, new() { Steps = 8 });
            await page.Mouse.UpAsync();
            await Assertions.Expect(page.GetByTestId("processes-canvas-command-receipt")).ToContainTextAsync("Accepted");
            var moved = await ReadAsync();
            Assert.NotEqual(0, moved.Content.Definition.Steps[0].CanvasX);
            await page.GetByTestId("processes-canvas-recompose").ClickAsync();
            await WaitForAuthoringAsync(host, session => session.Revision > moved.Revision);
            await Assertions.Expect(page.GetByTestId("processes-canvas-recompose")).ToBeEnabledAsync();
            await page.GetByTestId("processes-canvas-toggle-selection").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-canvas-selection")).ToBeHiddenAsync();
            point = await ProcessCanvasBrowser.ReadNodePointAsync(page, "role:workflow-owner");
            await page.Mouse.ClickAsync(point.X, point.Y);
            await page.GetByTestId("processes-canvas-toggle-selection").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-canvas-selection")).ToContainTextAsync("Workflow owner");
            await page.GetByTestId("processes-canvas-toggle-selection").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-canvas-selection")).ToBeHiddenAsync();
            point = await ProcessCanvasBrowser.ReadNodePointAsync(page, "role:workflow-owner");
            await page.Mouse.ClickAsync(point.X, point.Y, new() { Button = MouseButton.Right });
            await page.Locator(".cw-context-menu__action[data-action-id='process-canvas:role-reference:clone:role:workflow-owner']").ClickAsync();
            var cloned = await WaitForAuthoringAsync(host, session => session.Content.References.Any(item => item.IsClone));
            await Assertions.Expect(page.GetByTestId("processes-canvas-command-receipt")).ToContainTextAsync("Accepted");
            await page.GetByTestId("processes-canvas-toggle-selection").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-canvas-selection")).ToContainTextAsync("Workflow owner");
            Assert.Single(cloned.Content.Definition.RoleUsages);
            Assert.Single(cloned.Content.References, item => item.IsClone);
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "native-canvas-overlay.png") });
            await page.GetByTitle("Dock canvas", new() { Exact = true }).ClickAsync();
            await page.GetByTestId("processes-detail-tab-exchange").ClickAsync();
            var importCount = 0;
            foreach (var (category, kind) in new[] { ("roles", "role"), ("artifacts", "artifact"), ("processes", "process") }) {
                await page.GetByTestId("processes-template-library-category-" + category).ClickAsync();
                if (kind == "artifact") {
                    await page.GetByTestId("processes-template-library-artifact-target").SelectOptionAsync("outcome");
                }
                await page.GetByTestId("processes-template-library-import-" + kind).ClickAsync();
                importCount++;
                await WaitForAuthoringAsync(host, session => session.Content.Imports.Count == importCount);
                await Assertions.Expect(page.GetByTestId("processes-template-library-import-receipt")).ToContainTextAsync("Accepted");
                await Assertions.Expect(page.GetByTestId("processes-template-library-import-" + kind)).ToBeEnabledAsync();
            }
            var imported = await ReadAsync();
            Assert.Equal(3, imported.Content.Imports.Count);
            Assert.True(imported.Content.Definition.RoleUsages.Count > 1);
            Assert.True(imported.Content.Definition.Steps.Count > 1);
            Assert.Single(imported.Content.Definition.Steps.Single(step => step.Key == "outcome").ArtifactExpectations);
            Assert.All(imported.Content.Definition.Steps.SelectMany(step => step.RoleAssignments), assignment =>
                Assert.Contains(imported.Content.Definition.RoleUsages, role => role.Key == assignment.RoleKey));
            var canonical = ProcessAuthoringCodec.Write(imported.Content);
            await host.RestartAsync();
            await using var independent = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
            var reopened = await independent.NewPageAsync();
            await OpenAsync(reopened, host);
            await reopened.GetByTestId("processes-detail-tab-exchange").ClickAsync();
            Assert.Equal(canonical, ProcessAuthoringCodec.Write((await ReadAsync()).Content));
            await reopened.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "restarted-materialized-imports.png"), FullPage = true });
            Assert.Empty(errors);
        } catch {
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "canvas-import-failure.png"), FullPage = true });
            throw;
        } finally {
            await context.Tracing.StopAsync(new() { Path = Path.Combine(host.Evidence, "canvas-import.zip") });
        }

        Task<ProcessAuthoringSession> ReadAsync() => host.ReadAsync(provider => provider.GetRequiredService<ProcessAuthoringWorkspace>()
            .ReadAsync(ProcessWorkspaceShellScope.Global, new(host.DefinitionKey), default));
    }

    [Fact]
    public async Task Published_content_and_old_preparation_survive_a_new_OS_process_and_launch_the_exact_workflow() {
        await using var host = new ProcessAuthoringRestartHost();
        await host.StartAsync();
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1 });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        using var http = new HttpClient { BaseAddress = new(host.BaseUrl), Timeout = TimeSpan.FromSeconds(90) };
        try {
            await OpenAsync(page, host);
            await PublishAsync(page, host, "Owned publication v1");
            LaunchRequest request = new(host.DefinitionKey, true, true, Guid.NewGuid());
            using var first = await PostAsync("/api/processes/launch/check", request);
            var admission = first.RootElement.GetProperty("observation").GetProperty("admissionId").GetGuid();
            var planHash = first.RootElement.GetProperty("launchPlan").GetProperty("planHash").GetString();
            Assert.Equal("Owned publication v1", first.RootElement.GetProperty("launchPlan").GetProperty("definitionName").GetString());
            await page.GetByTestId("processes-detail-tab-steps").ClickAsync();
            await page.GetByTestId("processes-step-add-artifact-expectation").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-step-command-receipt")).ToContainTextAsync("Accepted");
            await page.GetByTestId("processes-tab-definitions").ClickAsync();
            await PublishAsync(page, host, "Owned publication v2");
            var firstPid = host.ProcessId;
            await host.RestartAsync();
            Assert.NotEqual(firstPid, host.ProcessId);
            await using var independent = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1 });
            var reopened = await independent.NewPageAsync();
            await OpenAsync(reopened, host, "Owned publication v2");
            await Assertions.Expect(reopened.GetByTestId("processes-definition-editor-name")).ToHaveValueAsync("Owned publication v2");
            await reopened.GetByTestId("processes-detail-tab-steps").ClickAsync();
            await Assertions.Expect(reopened.GetByTestId("processes-step-editor-form")).ToBeVisibleAsync();
            await reopened.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "restarted-editor.png"), FullPage = true });
            using var retried = await PostAsync("/api/processes/launch/check", request);
            Assert.Equal(admission, retried.RootElement.GetProperty("observation").GetProperty("admissionId").GetGuid());
            Assert.Equal(planHash, retried.RootElement.GetProperty("launchPlan").GetProperty("planHash").GetString());
            using var second = await PostAsync("/api/processes/launch/check", request with { CallerIntentId = Guid.NewGuid() });
            Assert.Equal("Owned publication v2", second.RootElement.GetProperty("launchPlan").GetProperty("definitionName").GetString());
            Assert.NotEqual(planHash, second.RootElement.GetProperty("launchPlan").GetProperty("planHash").GetString());
            using var accepted = await PostAsync("/api/processes/launch", request with { PreparedAdmissionId = admission });
            var run = new ProcessRunId(accepted.RootElement.GetProperty("runId").GetGuid());
            Assert.Equal(planHash, accepted.RootElement.GetProperty("launchPlan").GetProperty("planHash").GetString());
            var planId = new ProcessInstancePlanId(accepted.RootElement.GetProperty("launchPlanId").GetGuid());
            var plan = await host.ReadAsync(async provider => await provider.GetRequiredService<IProcessInstancePlanStore>().LoadAsync(planId));
            Assert.Equal("Owned publication v1", ProcessExecutableDefinitionResolver.Decode(plan!.ExecutableDefinitions!, host.DefinitionKey).Definition.DisplayName);
            var assignments = await host.ReadAsync(async provider => await provider.GetRequiredService<IProcessRuntimeStepAssignmentStore>().LoadByRunAsync(run));
            Assert.Empty(Assert.Single(assignments).ProducedArtifactSlotIds);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            ProcessRuntimeStateSnapshot state;
            do {
                state = (await host.ReadAsync(provider => provider.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(run)))!;
                if (state.Status != ProcessRuntimeStatus.Active) {
                    break;
                }
                await Task.Delay(150, deadline.Token);
            } while (true);
            Assert.Equal(ProcessRuntimeStatus.Completed, state.Status);
            Assert.Equal("PC1 native workflow completed.", Assert.Single(state.AppliedResults).UserSafeSummary);
            var workflowRun = Assert.Single(await host.ReadAsync(provider => provider.GetRequiredService<IWorkflowRuntimeManager>().ListRunsAsync(host.Workflow.Id)));
            Assert.Equal(WorkflowRunState.Completed, workflowRun.State);
            var origin = Assert.IsType<WorkflowLaunchOrigin.ProcessDispatchAssignment>(workflowRun.Origin);
            Assert.Equal(run.Value, origin.Dispatch.ProcessRun.Value);
            await reopened.GotoAsync($"{host.BaseUrl}/processes?runId={run.Value:D}&definitionKey={host.DefinitionKey}");
            await Assertions.Expect(reopened.GetByTestId("processes-page-scaffold")).ToHaveAttributeAsync("data-interactive", "true");
            await reopened.GetByTestId("processes-detail-tab-runs").ClickAsync();
            await Assertions.Expect(reopened.GetByTestId("processes-runtime-run-card").First).ToContainTextAsync("Completed");
            await reopened.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "restarted-exact-workflow.png") });
            using var recovered = await PostAsync("/api/processes/launch", request with { PreparedAdmissionId = admission });
            Assert.Equal(run.Value, recovered.RootElement.GetProperty("runId").GetGuid());
            Assert.Empty(errors);
            await File.WriteAllTextAsync(Path.Combine(host.Evidence, "restart-proof.json"), JsonSerializer.Serialize(new {
                FirstPid = firstPid, SecondPid = host.ProcessId, Admission = admission, Run = run.Value, PlanHash = planHash,
                WorkflowRun = workflowRun.RunId, Definition = host.DefinitionKey, Configuration = PlaywrightTestHostPaths.BuildConfiguration
            }));
        } catch {
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "restart-failure.png"), FullPage = true });
            throw;
        } finally {
            await context.Tracing.StopAsync(new() { Path = Path.Combine(host.Evidence, "restart.zip") });
        }

        async Task<JsonDocument> PostAsync(string path, LaunchRequest body) {
            using var response = await http.PostAsJsonAsync(path, body);
            Assert.True(response.IsSuccessStatusCode, $"Owned API operation failed: {response.StatusCode}; {await response.Content.ReadAsStringAsync()}");
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        }
    }

    private sealed record LaunchRequest(string DefinitionKey, bool RunReadiness, bool Execute, Guid CallerIntentId, Guid? PreparedAdmissionId = null);

    private static async Task OpenAsync(IPage page, ProcessAuthoringRestartHost host, string expectedName = "Owned restart draft") {
        await page.GotoAsync(host.BaseUrl + "/processes");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await Assertions.Expect(page.GetByTestId("processes-page-scaffold")).ToHaveAttributeAsync("data-interactive", "true");
        var definition = page.GetByTestId("processes-definition-" + host.DefinitionKey);
        if (await definition.GetAttributeAsync("aria-selected") != "true") {
            await definition.ClickAsync();
        }
        await Assertions.Expect(page.GetByTestId("processes-definition-editor-name")).ToHaveValueAsync(expectedName);
    }

    private static async Task PublishAsync(IPage page, ProcessAuthoringRestartHost host, string name) {
        await page.GetByTestId("processes-definition-editor-name").FillAsync(name);
        await page.GetByTestId("processes-definition-publish").ClickAsync();
        await Assertions.Expect(page.GetByTestId("processes-definition-editor-receipt")).ToContainTextAsync("Accepted");
        await Assertions.Expect(page.GetByTestId("processes-definition-editor-name")).ToHaveValueAsync(name);
        await WaitForAuthoringAsync(host, session => session.Lifecycle == ProcessAuthoringLifecycle.Published && session.Content.Definition.DisplayName == name);
        await Assertions.Expect(page.GetByTestId("processes-definition-publish")).ToBeEnabledAsync();
    }

    private static async Task<ProcessAuthoringSession> WaitForAuthoringAsync(ProcessAuthoringRestartHost host, Func<ProcessAuthoringSession, bool> condition) {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true) {
            var observed = await host.ReadAsync(provider => provider.GetRequiredService<ProcessAuthoringWorkspace>()
                .ReadAsync(ProcessWorkspaceShellScope.Global, new(host.DefinitionKey), deadline.Token));
            if (condition(observed)) {
                return observed;
            }
            await Task.Delay(100, deadline.Token);
        }
    }
}
