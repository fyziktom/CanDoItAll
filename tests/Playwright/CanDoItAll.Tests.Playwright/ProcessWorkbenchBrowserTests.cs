using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net.Http.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class ProcessWorkbenchBrowserTests {
    [Fact]
    public async Task Workbench_reviewed_process_launch_retains_native_project_link_after_reload() {
        await using var wire = await AgentResponseFixture.StartAsync("No model request is needed for the workflow executor.");
        await using var host = await ProcessNativeBrowserHost.StartAsync(wire.BaseUrl);
        var definitionId = ProcessDefinitionCatalogProjectionService.CreateDefinitionId(new(ProcessNativeBrowserHost.CompleteDefinition));
        var processNode = $"process-definition:{definitionId.Value:D}";
        var target = await host.ReadAsync(async services => {
            var workbench = services.GetRequiredService<ProjectWorkbenchService>();
            var node = await workbench.CreateObjectAsync(host.ProjectId, new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock, "PC1 launch target", "Owned workflow target", "Keep the reviewed project lifetime.",
                $"project:{host.ProjectId:D}", 420, 260, ObjectSubtype: "delivery"));
            await workbench.LinkObjectsAsync(host.ProjectId, node.Id, processNode, ProjectObjectLinkKind.Uses);
            return node;
        });
        var published = await host.ReadAsync(async services => {
            var workspace = services.GetRequiredService<ProcessAuthoringWorkspace>();
            var baseline = await workspace.ReadAsync(ProcessWorkspaceShellScope.ForProject(host.ProjectId), new(ProcessNativeBrowserHost.CompleteDefinition), default);
            var content = ProcessAuthoringCodec.Read(ProcessAuthoringCodec.Write(baseline.Content));
            content.Definition.DisplayName = "PC4 project workflow publication";
            content.Definition.LaunchDriverActivations.Clear();
            content = await services.GetRequiredService<ProcessExecutableDefinitionResolver>().PinPublicationAsync(baseline, content, default);
            return (await workspace.CommitAsync(baseline, Guid.NewGuid(), ProcessAuthoringCodec.Hash("paired native and HTTP launch"), content,
                ProcessAuthoringLifecycle.Published, true, null, default)).Snapshot!;
        });
        using var http = new HttpClient { BaseAddress = new(host.BaseUrl) };
        HttpLaunchRequest httpRequest = new(DefinitionKey: ProcessNativeBrowserHost.CompleteDefinition, ProjectId: host.ProjectId,
            ProjectNodeId: target.Id, RunReadiness: false, Execute: false, CallerIntentId: Guid.NewGuid());
        using var checkedResponse = await http.PostAsJsonAsync("/api/processes/launch/check", httpRequest);
        checkedResponse.EnsureSuccessStatusCode();
        using var checkedBody = JsonDocument.Parse(await checkedResponse.Content.ReadAsStringAsync());
        var httpAdmission = new ProcessLaunchAdmissionId(checkedBody.RootElement.GetProperty("observation").GetProperty("admissionId").GetGuid());
        var httpPreparation = (await host.ReadAsync(services => services.GetRequiredService<IProcessPreparedLaunchStore>().GetAsync(httpAdmission)))!;
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        try {
            await page.GotoAsync($"{host.BaseUrl}/projects/{host.ProjectId:D}/structure");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await page.GetByTestId("project-structure-canvas-loaded").WaitForAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Hide window", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Fit canvas", Exact = true }).ClickAsync();
            var point = await page.EvaluateAsync<ProcessCanvasBrowser.NodePoint>("""
                async id => {
                    const host = document.querySelector('.cw-canvas-host');
                    const started = Date.now();
                    let signature = '';
                    let stableSince = started;
                    while (Date.now() - started < 10000) {
                        const point = window.CanDoItAll.canvasWorkbench.getHotZoneCenter(host, { zone: 'node-body', nodeId: id });
                        const viewport = host.__canvasWorkbenchState?.ui;
                        const rect = host.getBoundingClientRect();
                        const current = JSON.stringify([point, viewport?.panX, viewport?.panY, viewport?.zoom, rect.x, rect.y]);
                        if (current !== signature) {
                            signature = current;
                            stableSince = Date.now();
                        }
                        if (point && Date.now() - stableSince > 300) {
                            return { x: rect.left + point.x, y: rect.top + point.y };
                        }
                        await new Promise(resolve => requestAnimationFrame(resolve));
                    }
                    throw new Error('The process node hit target did not settle.');
                }
                """, processNode);
            await page.Mouse.ClickAsync(point.X, point.Y, new() { Button = MouseButton.Right });
            await page.Locator(".cw-context-menu__action[data-action-id='start-process']").ClickAsync();
            await page.GetByTestId("project-structure-process-start-continue").ClickAsync();
            var assignment = page.GetByTestId("project-structure-process-assignment-dialog");
            await Assertions.Expect(assignment).ToContainTextAsync("1 of 1 roles assigned");
            var review = assignment.GetByTestId("project-structure-process-assignment-review-start");
            await Assertions.Expect(review).ToHaveTextAsync("Prepare reviewed launch");
            await review.ClickAsync();
            await Assertions.Expect(review).ToHaveTextAsync("Start reviewed run", new() { Timeout = 60_000 });
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "workbench-reviewed-launch.png") });
            await review.ClickAsync();
            await page.WaitForURLAsync(new Regex("[?&]runId=", RegexOptions.IgnoreCase), new() { Timeout = 90_000 });
            var runId = new ProcessRunId(Guid.Parse(QueryHelpers.ParseQuery(new Uri(page.Url).Query)["runId"].ToString()));
            var preparation = await host.ReadAsync(services => services.GetRequiredService<IProcessPreparedLaunchStore>().FindByRunAsync(runId));
            Assert.NotNull(preparation);
            Assert.NotNull(preparation.Preparation.CallerIntentId);
            Assert.Equal(ProcessLaunchContinuationState.Started, preparation.State);
            Assert.Equal(ProcessLaunchLinkDeliveryState.Delivered, preparation.LinkDeliveryState);
            Assert.NotNull(preparation.DeliveredLinkId);
            Assert.Equal(host.ProjectId, preparation.Preparation.LinkTarget!.ProjectId);
            Assert.Equal(target.Id, preparation.Preparation.LinkTarget.SourceNodeKey);
            var uiDefinitions = preparation.Preparation.InitialCommit.InitialPlan!.ExecutableDefinitions!;
            var httpDefinitions = httpPreparation.Preparation.InitialCommit.InitialPlan!.ExecutableDefinitions!;
            var uiSource = uiDefinitions.Definitions[ProcessNativeBrowserHost.CompleteDefinition];
            var httpSource = httpDefinitions.Definitions[ProcessNativeBrowserHost.CompleteDefinition];
            Assert.Equal(published.PublishedId, uiSource.PublicationId);
            Assert.Equal(uiSource, httpSource);
            Assert.Empty(ProcessExecutableDefinitionResolver.Decode(httpDefinitions, ProcessNativeBrowserHost.CompleteDefinition).Definition.LaunchDriverActivations);
            Assert.Equal(target.Id, httpPreparation.Preparation.Request.Variables[ProcessRuntimeLaunchVariables.ProjectNodeId]);
            Assert.Equal(host.ProjectId.ToString("D"), httpPreparation.Preparation.Request.Variables[ProcessRuntimeLaunchVariables.ProjectId]);
            var structure = await host.ReadAsync(services => services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(host.ProjectId));
            var link = Assert.Single(structure.Links, item => item.RecordId == preparation.DeliveredLinkId);
            Assert.Equal(target.Id, link.SourceId);
            Assert.Equal($"process-run:{runId.Value:D}", link.TargetId);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            ProcessRuntimeStateSnapshot? state;
            do {
                state = await host.ReadAsync(services => services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(runId));
                if (state?.Status != ProcessRuntimeStatus.Completed) {
                    await Task.Delay(200, deadline.Token);
                }
            } while (state?.Status != ProcessRuntimeStatus.Completed);
            Assert.Equal("PC1 native workflow completed.", Assert.Single(state.AppliedResults).UserSafeSummary);
            await page.ReloadAsync();
            await page.GetByTestId("live-processes-run-open-details").WaitForAsync();
            var retained = await host.ReadAsync(services => services.GetRequiredService<IProcessPreparedLaunchStore>().FindByRunAsync(runId));
            Assert.Equal(preparation.Preparation.CallerIntentId, retained!.Preparation.CallerIntentId);
            Assert.Equal(preparation.DeliveredLinkId, retained.DeliveredLinkId);
            var child = Assert.Single(await host.ReadAsync(services => services.GetRequiredService<IWorkflowRuntimeManager>()
                .ListRunsAsync(host.Workflows[ProcessNativeBrowserHost.CompleteDefinition].Id)));
            Assert.Equal(WorkflowRunState.Completed, child.State);
            Assert.Equal(0, wire.Requests);
            await Assertions.Expect(page.GetByTestId("live-processes-summary")).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("live-processes-run-open-details").First).ToBeVisibleAsync();
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "workbench-native-result.png") });
            using var launchedResponse = await http.PostAsJsonAsync("/api/processes/launch", httpRequest with { PreparedAdmissionId = httpAdmission.Value });
            launchedResponse.EnsureSuccessStatusCode();
            using var launchedBody = JsonDocument.Parse(await launchedResponse.Content.ReadAsStringAsync());
            var httpRun = new ProcessRunId(launchedBody.RootElement.GetProperty("runId").GetGuid());
            using var replayResponse = await http.PostAsJsonAsync("/api/processes/launch", httpRequest with { PreparedAdmissionId = httpAdmission.Value });
            replayResponse.EnsureSuccessStatusCode();
            using var replayBody = JsonDocument.Parse(await replayResponse.Content.ReadAsStringAsync());
            Assert.Equal(httpRun.Value, replayBody.RootElement.GetProperty("runId").GetGuid());
            var httpAccepted = (await host.ReadAsync(services => services.GetRequiredService<IProcessPreparedLaunchStore>().FindByRunAsync(httpRun)))!;
            Assert.Equal(httpPreparation.Preparation.Review.PlanHash, httpAccepted.Preparation.Review.PlanHash);
            var httpAssignments = await host.ReadAsync(async services => await services.GetRequiredService<IProcessRuntimeStepAssignmentStore>().LoadByRunAsync(httpRun));
            Assert.All(httpAssignments, item => Assert.Equal(target.Id, item.LaunchVariables[ProcessRuntimeLaunchVariables.ProjectNodeId]));
            Assert.NotEmpty(httpAssignments);
            await File.WriteAllTextAsync(Path.Combine(host.Evidence, "workbench-oracle.json"), JsonSerializer.Serialize(new {
                Project = host.ProjectId, Target = target.Id, Run = runId.Value, preparation.Preparation.CallerIntentId,
                preparation.Preparation.AdmissionId, preparation.LinkDeliveryState, preparation.DeliveredLinkId,
                HttpRun = httpRun.Value, HttpAdmission = httpAdmission.Value, Publication = published.PublishedId, uiSource.ContentHash,
                WorkflowRun = child.RunId, Summary = state.AppliedResults.Single().UserSafeSummary
            }));
            Assert.Empty(errors);
        } catch {
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "workbench-failure.png") });
            await File.WriteAllTextAsync(Path.Combine(host.Evidence, "workbench-failure.txt"), await page.Locator("body").InnerTextAsync());
            throw;
        } finally {
            await context.Tracing.StopAsync(new() { Path = Path.Combine(host.Evidence, "workbench-trace.zip") });
        }
    }

    private sealed record HttpLaunchRequest(string DefinitionKey, Guid ProjectId, string ProjectNodeId, bool RunReadiness, bool Execute,
        Guid CallerIntentId, Guid? PreparedAdmissionId = null);
}
