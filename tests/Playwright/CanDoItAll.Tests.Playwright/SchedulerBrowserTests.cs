using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

[Collection(PlaywrightCollection.Name)]
[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class SchedulerBrowserTests(PlaywrightAppFixture fixture) {
    private static readonly string Artifacts = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "scheduler-ui");

    [Fact]
    public async Task Production_route_uses_real_owner_and_exact_persisted_workflow_identity() {
        Directory.CreateDirectory(Artifacts);
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(fixture.OwnedDatabaseProfile, "Scheduler.Browser.Seed",
            TestSchemaBootstrapModules.Full, new Dictionary<string, string?> {
                [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind
            });
        await using var scope = provider.CreateAsyncScope();
        var start = new WorkflowNodeId("start");
        var end = new WorkflowNodeId("end");
        static WorkflowNode Node(WorkflowNodeId id, WorkflowNodeKind kind) => new(id, kind, id.Value, [],
            new(null, null, null, null, string.Empty, WorkflowValueShape.Text, WorkflowValueShape.Text));
        var target = await scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>().SaveDefinitionAsync(new(
            WorkflowId.New(), null, "000 Scheduler browser harmless fixture", "Start/end only", WorkflowLifecycleStatus.Draft,
            new(start, [Node(start, WorkflowNodeKind.Start), Node(end, WorkflowNodeKind.End)],
                [new(new("start-end"), start, null, end, null, WorkflowEdgeKind.Direct, string.Empty) { Routing = WorkflowEdgeRouting.Always }]),
            new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false)));
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
        var page = await context.NewPageAsync();
        var errors = Observe(page);
        await page.GotoAsync(fixture.BaseUrl + "/scheduler");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await Assertions.Expect(page.GetByTestId("scheduler-agent-open")).ToBeEnabledAsync();
        await page.GetByTestId("scheduler-tab-new").ClickAsync();
        await page.GetByTestId("scheduler-name").FillAsync("Browser schedule");
        await page.GetByTestId("scheduler-input-json").FillAsync("{broken");
        await page.GetByTestId("scheduler-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("scheduler-draft-error")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("scheduler-input-json")).ToHaveValueAsync("{broken");
        await ScreenshotAsync(page, "production-invalid-json");
        await page.GetByTestId("scheduler-input-json").FillAsync("{\"unknown\":{\"keep\":true}}");
        await page.GetByTestId("scheduler-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("scheduler-draft-receipt")).ToContainTextAsync("Committed");
        await using var database = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<SchedulerPlannerDbContext>>().CreateDbContextAsync();
        var plan = Assert.Single(await database.Set<SchedulerPlan>().AsNoTracking().Where(item => item.TargetId == target.Id.Value).ToListAsync());
        Assert.Equal(target.VersionId.Value, plan.TargetVersionId);
        Assert.Equal("Browser schedule", plan.Name);
        Assert.Contains("unknown", plan.InputJson, StringComparison.Ordinal);
        await ScreenshotAsync(page, "production-new-saved");
        await page.GetByTestId("scheduler-tab-schedules").ClickAsync();
        var card = page.GetByTestId("scheduler-plan-card").Filter(new() { HasText = "Browser schedule" });
        await card.GetByRole(AriaRole.Button, new() { Name = "Pause", Exact = true }).ClickAsync();
        await Assertions.Expect(card.GetByRole(AriaRole.Button, new() { Name = "Resume", Exact = true })).ToBeVisibleAsync();
        Assert.False((await database.Set<SchedulerPlan>().AsNoTracking().SingleAsync(item => item.Id == plan.Id)).IsEnabled);
        await card.GetByTestId("scheduler-plan-edit").ClickAsync();
        await page.GetByTestId("scheduler-edit-name").FillAsync("Browser updated");
        await page.GetByTestId("scheduler-edit-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("scheduler-edit-dialog").GetByTestId("scheduler-draft-receipt")).ToContainTextAsync("Committed");
        await ScreenshotAsync(page, "production-edit");
        Assert.Equal("Browser updated", (await database.Set<SchedulerPlan>().AsNoTracking().SingleAsync(item => item.Id == plan.Id)).Name);
        await page.GetByTestId("scheduler-edit-cancel").ClickAsync();
        await page.GetByTestId("scheduler-plan-card").Filter(new() { HasText = "Browser updated" }).GetByTestId("scheduler-plan-delete").ClickAsync();
        await Assertions.Expect(page.GetByTestId("scheduler-delete-dialog")).ToContainTextAsync("Display run history is deleted");
        await ScreenshotAsync(page, "production-delete");
        await page.GetByTestId("scheduler-delete-confirm").ClickAsync();
        await Assertions.Expect(page.GetByTestId("scheduler-delete-dialog")).ToHaveCountAsync(0);
        Assert.False(await database.Set<SchedulerPlan>().AnyAsync(item => item.Id == plan.Id));
        foreach (var tab in new[] { "history", "calendar", "schedules" }) {
            await page.GetByTestId("scheduler-tab-" + tab).ClickAsync();
            await ScreenshotAsync(page, "production-" + tab);
        }
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Sandbox_real_canvas_controls_retained_successor_and_assets() {
        Directory.CreateDirectory(Artifacts);
        await using var host = new SchedulerSandboxHost();
        await host.ReadyAsync();
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
        await context.AddInitScriptAsync("""
            window.schedulerPointerEvents = [];
            for (const kind of ["pointerdown", "mousedown", "mouseup", "dblclick", "focusin", "scroll"]) {
                document.addEventListener(kind, e => window.schedulerPointerEvents.push({kind, phase:"capture", y:e.clientY, scrollY, target:e.target?.tagName}), true);
                document.addEventListener(kind, e => window.schedulerPointerEvents.push({kind, phase:"bubble", y:e.clientY, scrollY, target:e.target?.tagName}));
            }
            window.schedulerCanvasText = [];
            const fillText = CanvasRenderingContext2D.prototype.fillText;
            CanvasRenderingContext2D.prototype.fillText = function(text, x, y, ...rest) {
                const point = new DOMPoint(x, y - 3).matrixTransform(this.getTransform());
                window.schedulerCanvasText.push({canvas:this.canvas, width:this.canvas.width, height:this.canvas.height, text:String(text), x:point.x, y:point.y});
                if (window.schedulerCanvasText.length > 4000) window.schedulerCanvasText.splice(0, 2000);
                return fillText.call(this, text, x, y, ...rest);
            };
            """);
        var page = await context.NewPageAsync();
        var errors = Observe(page);
        try {
            await page.GotoAsync(host.BaseUrl);
            await page.GetByTestId("scheduler-calendar").WaitForAsync();
            await Assertions.Expect(page.GetByTestId("scheduler-calendar")).ToHaveAttributeAsync("data-scheduler-registration", new System.Text.RegularExpressions.Regex("^[a-f0-9]{32}$"));
            await ScreenshotAsync(page, "sandbox-calendar");
            await DoubleClickPlannedAsync(page, "Morning");
            await ScreenshotAsync(page, "sandbox-native-after-click");
            await File.WriteAllTextAsync(Path.Combine(Artifacts, "native-observed.txt"), string.Join("\n", errors) + "\n" + await page.Locator("[data-testid=scheduler-store-counts]").GetAttributeAsync("data-editor-reads"));
            await Assertions.Expect(page.GetByTestId("scheduler-edit-name")).ToHaveValueAsync("Morning review");
            await page.GetByTestId("scheduler-edit-cancel").ClickAsync();
            await Assertions.Expect(page.GetByTestId("scheduler-store-counts")).ToHaveAttributeAsync("data-editor-reads", "1");
            await page.Locator("canvas.zy-calendar-canvas").DblClickAsync(new() { Position = new() { X = 600, Y = 180 } });
            await Assertions.Expect(page.GetByTestId("scheduler-edit-dialog")).ToHaveCountAsync(0);
            await DoubleClickPlannedAsync(page, "History");
            await Assertions.Expect(page.GetByTestId("scheduler-edit-dialog")).ToHaveCountAsync(0);
            await page.GetByTestId("scheduler-calendar").Locator("[data-view=month]").ClickAsync();
            await page.GetByTestId("scheduler-tab-schedules").ClickAsync();
            await page.GetByTestId("scheduler-tab-calendar").ClickAsync();
            await Assertions.Expect(page.GetByTestId("scheduler-calendar").Locator("[data-view=month]")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-active"));
            await page.GetByTestId("scheduler-calendar").Locator("[data-view=week]").ClickAsync();
            await page.GetByTestId("scheduler-second-calendar").ClickAsync();
            var secondary = page.GetByTestId("scheduler-secondary-surface");
            await secondary.GetByTestId("scheduler-calendar").ScrollIntoViewIfNeededAsync();
            await DoubleClickPlannedAsync(page, "Second");
            await Assertions.Expect(secondary.GetByTestId("scheduler-edit-name")).ToHaveValueAsync("Second");
            await Assertions.Expect(page.GetByTestId("scheduler-primary-surface").GetByTestId("scheduler-edit-dialog")).ToHaveCountAsync(0);
            await secondary.GetByTestId("scheduler-edit-cancel").ClickAsync();
            await Assertions.Expect(page.GetByTestId("scheduler-store-counts")).ToHaveAttributeAsync("data-secondary-editor-reads", "1");
            await page.GetByTestId("scheduler-second-calendar").ClickAsync();
            await Assertions.Expect(secondary).ToHaveCountAsync(0);
            await page.GetByTestId("scheduler-calendar").ScrollIntoViewIfNeededAsync();
            await DoubleClickPlannedAsync(page, "Morning");
            await Assertions.Expect(page.GetByTestId("scheduler-edit-name")).ToHaveValueAsync("Morning review");
            await page.GetByTestId("scheduler-edit-cancel").ClickAsync();
            await Assertions.Expect(page.GetByTestId("scheduler-store-counts")).ToHaveAttributeAsync("data-editor-reads", "2");

            await page.GetByTestId("scheduler-tab-new").ClickAsync();
            await page.GetByTestId("scheduler-hold").SelectOptionAsync("Validation");
            await page.GetByTestId("scheduler-name").FillAsync("Submitted browser draft");
            await page.GetByTestId("scheduler-save").ClickAsync();
            await Assertions.Expect(page.GetByTestId("scheduler-draft-receipt")).ToContainTextAsync("Pending");
            await page.GetByTestId("scheduler-name").FillAsync("Newer unblurred input");
            await page.GetByTestId("scheduler-input-json").FillAsync("{\"later\":true}");
            await ScreenshotAsync(page, "sandbox-held-newer-input");
            await page.GetByTestId("scheduler-reset").ClickAsync();
            await Assertions.Expect(page.GetByTestId("scheduler-name")).ToHaveValueAsync("New review");
            await page.GetByTestId("scheduler-name").FillAsync("Successor draft");
            await page.GetByTestId("scheduler-release").ClickAsync();
            await Assertions.Expect(page.GetByTestId("scheduler-store-counts")).ToHaveAttributeAsync("data-writes", "1");
            await Assertions.Expect(page.GetByTestId("scheduler-name")).ToHaveValueAsync("Successor draft");
            await Assertions.Expect(page.GetByTestId("scheduler-stored-plan").Filter(new() { HasText = "Submitted browser draft" })).ToHaveCountAsync(1);
            await page.GetByTestId("scheduler-retained-drafts").GetByRole(AriaRole.Button).ClickAsync();
            await Assertions.Expect(page.GetByTestId("scheduler-edit-name")).ToHaveValueAsync("Newer unblurred input");
            await Assertions.Expect(page.GetByTestId("scheduler-edit-input-json")).ToHaveValueAsync("{\"later\":true}");
            await ScreenshotAsync(page, "sandbox-retained-origin");
            await page.GetByTestId("scheduler-edit-cancel").ClickAsync();
            await page.GetByTestId("scheduler-target-open").ClickAsync();
            await ScreenshotAsync(page, "sandbox-target-picker");
            await page.GetByTestId("scheduler-target-card").Filter(new() { HasText = "Report workflow" }).ClickAsync();
            foreach (var tab in new[] { "schedules", "history", "calendar" }) {
                await page.GetByTestId("scheduler-tab-" + tab).ClickAsync();
                await ScreenshotAsync(page, "sandbox-" + tab);
            }
            await DoubleClickPlannedAsync(page, "Morning");
            await page.GetByTestId("scheduler-edit-cancel").ClickAsync();
            await page.GetByTestId("scheduler-scenario").SelectOptionAsync("UnknownSave");
            await page.GetByTestId("scheduler-tab-new").ClickAsync();
            await page.GetByTestId("scheduler-save").ClickAsync();
            await Assertions.Expect(page.GetByTestId("scheduler-draft-receipt")).ToContainTextAsync("Unknown");
            await Assertions.Expect(page.GetByTestId("scheduler-save")).ToBeDisabledAsync();
            await ScreenshotAsync(page, "sandbox-unknown");
            await page.GetByTestId("scheduler-scenario").SelectOptionAsync("Large");
            await page.GetByTestId("scheduler-tab-schedules").ClickAsync();
            await Assertions.Expect(page.GetByTestId("scheduler-plan-card")).ToHaveCountAsync(82);
            await ScreenshotAsync(page, "sandbox-large");
            Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth"));
            var importing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseImport = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await page.RouteAsync("**/_content/CanDoItAll.SchedulerPlanner.UI/schedulerPlannerCalendarInterop*.js", async route => {
                importing.TrySetResult();
                await releaseImport.Task;
                await route.ContinueAsync();
            });
            try {
                await page.ReloadAsync();
                await importing.Task.WaitAsync(TimeSpan.FromSeconds(20));
                await page.GetByTestId("scheduler-tab-schedules").ClickAsync();
            } finally {
                releaseImport.TrySetResult();
            }
            await Assertions.Expect(page.GetByTestId("scheduler-calendar")).ToHaveCountAsync(0);
            await page.GetByTestId("scheduler-tab-calendar").ClickAsync();
            await Assertions.Expect(page.GetByTestId("scheduler-calendar")).ToHaveAttributeAsync("data-scheduler-registration", new System.Text.RegularExpressions.Regex("^[a-f0-9]{32}$"));
            await DoubleClickPlannedAsync(page, "Morning");
            await Assertions.Expect(page.GetByTestId("scheduler-edit-name")).ToHaveValueAsync("Morning review");
            await Assertions.Expect(page.GetByTestId("scheduler-store-counts")).ToHaveAttributeAsync("data-editor-reads", "1");
            Assert.Empty(errors);
        } finally {
            await File.WriteAllTextAsync(Path.Combine(Artifacts, "sandbox-server.log"), host.Logs);
        }
    }

    private static async Task DoubleClickPlannedAsync(IPage page, string text) {
        await page.EvaluateAsync("() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
        await page.WaitForFunctionAsync("prefix => window.schedulerCanvasText.some(item => item.canvas.isConnected && item.canvas.width === item.width && item.canvas.height === item.height && item.text.startsWith(prefix))", text);
        var point = await page.EvaluateAsync<double[]>("""
            prefix => {
                const item = window.schedulerCanvasText.findLast(item => item.canvas.isConnected && item.canvas.width === item.width && item.canvas.height === item.height && item.text.startsWith(prefix));
                const rect = item.canvas.getBoundingClientRect();
                return [rect.left + item.x * rect.width / item.canvas.width + 3, rect.top + item.y * rect.height / item.canvas.height];
            }
            """, text);
        await File.WriteAllTextAsync(Path.Combine(Artifacts, "native-point.json"), await page.EvaluateAsync<string>("point => JSON.stringify({point, element: document.elementFromPoint(point[0],point[1])?.outerHTML})", point));
        await page.Mouse.DblClickAsync((float)point[0], (float)point[1]);
        await File.WriteAllTextAsync(Path.Combine(Artifacts, "native-events.json"), await page.EvaluateAsync<string>("() => JSON.stringify(window.schedulerPointerEvents)"));
    }
    private static List<string> Observe(IPage page) {
        var errors = new List<string>();
        page.PageError += (_, message) => errors.Add(message);
        page.Console += (_, message) => {
            if (message.Type == "error") {
                errors.Add(message.Text);
            }
        };
        page.Response += (_, response) => {
            if (response.Status >= 400) {
                errors.Add($"{response.Status}: {response.Url}");
            }
        };
        return errors;
    }
    private static Task ScreenshotAsync(IPage page, string name) => page.ScreenshotAsync(new() { Path = Path.Combine(Artifacts, name + ".png"), FullPage = false });
}
