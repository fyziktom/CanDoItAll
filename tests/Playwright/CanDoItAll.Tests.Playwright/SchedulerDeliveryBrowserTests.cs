using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class SchedulerDeliveryBrowserTests {
    [Fact]
    public async Task UI_created_schedule_fires_through_Quartz_and_replay_reuses_exact_Workflow_asset() {
        var host = new PlaywrightAppFixture { EnableBackgroundWorkers = true };
        var evidence = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "scheduler-delivery");
        Directory.CreateDirectory(evidence);
        try {
            await host.InitializeAsync();
            await using var owners = await TestApplicationBootstrap.BuildServiceProviderAsync(host.OwnedDatabaseProfile,
                "Scheduler.Delivery.Browser", TestSchemaBootstrapModules.Full, new Dictionary<string, string?> {
                    [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind
                });
            await using var scope = owners.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var project = await services.GetRequiredService<ProjectsService>().SaveAsync(new ProjectEditorModel { Name = "Scheduled private asset" });
            Assert.True(project.IsSuccess);
            var marker = "scheduler-" + Guid.NewGuid().ToString("D");
            var input = JsonSerializer.Serialize(new { marker });
            var shape = new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "Input");
            WorkflowNode Node(string id, WorkflowNodeKind kind) => new(new(id), kind, id, [], new(null, null, null, null, "", shape, shape));
            var start = Node("start", WorkflowNodeKind.Start);
            var asset = Node("asset", WorkflowNodeKind.Executor) with { Settings = new(null, null, null, null, "", shape, shape) {
                ExecutorId = WorkflowExecutorIds.ProjectStructure,
                ExecutorSettingsJson = WorkflowExecutorJson.Serialize(new WorkflowProjectStructureExecutorSettings {
                    Operation = WorkflowProjectStructureOperation.CreateAsset, ProjectId = project.Value, NodeId = $"project:{project.Value:D}",
                    Title = "Scheduled actual file", ContentFromInput = true, AssetKind = "json", ContentType = "application/json"
                })
            } };
            var end = Node("end", WorkflowNodeKind.End);
            var definition = await services.GetRequiredService<IWorkflowCatalogService>().SaveDefinitionAsync(new(null, null,
                "000 Actual scheduled file", "Private owner integration", WorkflowLifecycleStatus.Active,
                new(start.Id, [start, asset, end], [Edge(start, asset), Edge(asset, end)]),
                new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false)));
            await using var context = await host.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
            var page = await context.NewPageAsync();
            var errors = new List<string>();
            page.PageError += (_, message) => errors.Add(message);
            await page.GotoAsync(host.BaseUrl + "/scheduler");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await Assertions.Expect(page.GetByTestId("scheduler-agent-open")).ToBeEnabledAsync();
            await page.GetByTestId("scheduler-tab-new").ClickAsync();
            await page.GetByTestId("scheduler-name").FillAsync("Actual scheduled delivery");
            await page.GetByTestId("scheduler-input-json").FillAsync(input);
            await page.GetByTestId("scheduler-timezone").FillAsync("UTC");
            var fireAt = DateTimeOffset.UtcNow.AddSeconds(25);
            await page.GetByTestId("scheduler-cron").FillAsync($"{fireAt.Second} {fireAt.Minute} {fireAt.Hour} {fireAt.Day} {fireAt.Month} ? {fireAt.Year}");
            await page.GetByTestId("scheduler-save").ClickAsync();
            await Assertions.Expect(page.GetByTestId("scheduler-draft-receipt")).ToContainTextAsync("Committed");
            await using var database = await services.GetRequiredService<IDbContextFactory<SchedulerPlannerDbContext>>().CreateDbContextAsync();
            var plan = Assert.Single(await database.Set<SchedulerPlan>().AsNoTracking().Where(item => item.TargetId == definition.Id.Value).ToArrayAsync());
            Assert.Equal(definition.VersionId.Value, plan.TargetVersionId);
            Assert.Equal(input, plan.InputJson);
            SchedulerPlanRun? delivery = null;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(85));
            while (delivery?.DispatchedAtUtc is null) {
                await Task.Delay(200, timeout.Token);
                delivery = await database.Set<SchedulerPlanRun>().AsNoTracking().SingleOrDefaultAsync(item => item.PlanId == plan.Id, timeout.Token);
                if (delivery is not null) {
                    await File.WriteAllTextAsync(Path.Combine(evidence, "observed-delivery.json"), JsonSerializer.Serialize(delivery));
                }
                Assert.False(delivery?.Status == SchedulerPlanRunDispatchStatus.Failed, delivery?.ErrorMessage);
            }
            await page.GetByTestId("scheduler-tab-schedules").ClickAsync();
            var card = page.GetByTestId("scheduler-plan-card").Filter(new() { HasText = plan.Name });
            await card.GetByRole(AriaRole.Button, new() { Name = "Pause", Exact = true }).ClickAsync();
            await Assertions.Expect(card.GetByRole(AriaRole.Button, new() { Name = "Resume", Exact = true })).ToBeVisibleAsync();
            var runStore = services.GetRequiredService<IWorkflowRunStore>();
            var run = Assert.Single(await runStore.ListRunsAsync(definition.Id));
            Assert.Equal(WorkflowRunState.Completed, run.State);
            Assert.Equal(definition.VersionId, run.VersionId);
            Assert.Equal(delivery.TargetRunId, run.RunId.Value);
            var origin = Assert.IsType<WorkflowLaunchOrigin.SchedulerPlanRun>(run.Origin);
            Assert.Equal(plan.Id, origin.PlanId);
            Assert.Equal(delivery.Id, origin.PlanRunId);
            Assert.Equal(delivery.SchedulerFireId, origin.FireId.Value);
            var structure = services.GetRequiredService<ProjectStructureAgentService>();
            var tree = await structure.GetStructureAsync(project.Value, new(IncludeAssets: true));
            var file = Assert.Single(tree.Nodes, item => item.Title == "Scheduled actual file");
            var content = await structure.GetAssetContentAsync(project.Value, file.Id);
            var bytes = Convert.FromBase64String(content.Base64Data);
            using var parsed = JsonDocument.Parse(bytes);
            Assert.Equal(marker, parsed.RootElement.GetProperty("marker").GetString());
            await page.GetByTestId("scheduler-tab-history").ClickAsync();
            await Assertions.Expect(page.GetByTestId("scheduler-history")).ToContainTextAsync(plan.Name);
            await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "actual-scheduled-run.png") });
            await using var restarted = await TestApplicationBootstrap.BuildServiceProviderAsync(host.OwnedDatabaseProfile,
                "Scheduler.Delivery.Restart", TestSchemaBootstrapModules.Full, new Dictionary<string, string?> {
                    [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind
                });
            await using var replayScope = restarted.CreateAsyncScope();
            await replayScope.ServiceProvider.GetRequiredService<ISchedulerPlannerRunDispatcher>().DispatchAsync(new(plan.Id,
                delivery.SchedulerFireId, delivery.CorrelationId, delivery.FiredAtUtc, plan.NextPlannedFireAtUtc));
            Assert.Single(await replayScope.ServiceProvider.GetRequiredService<IWorkflowRunStore>().ListRunsAsync(definition.Id));
            var replayTree = await replayScope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>()
                .GetStructureAsync(project.Value, new(IncludeAssets: true));
            Assert.Single(replayTree.Nodes, item => item.Id == file.Id);
            await File.WriteAllTextAsync(Path.Combine(evidence, "proof.json"), JsonSerializer.Serialize(new {
                planId = plan.Id, deliveryId = delivery.Id, delivery.SchedulerFireId, run.RunId, run.VersionId, origin,
                input = plan.InputJson, fileId = file.Id, file.ParentId, file.ArtifactId,
                sha256 = Convert.ToHexString(SHA256.HashData(bytes)), quartzFire = true, restartedOwnerReplayRunCount = 1
            }, new JsonSerializerOptions { WriteIndented = true }));
            Assert.Empty(errors);
        } finally {
            await File.WriteAllTextAsync(Path.Combine(evidence, "owned-host.log"), host.GetLogSnapshot(300));
            await host.DisposeAsync();
        }
    }

    private static WorkflowEdge Edge(WorkflowNode left, WorkflowNode right) => new(new(left.Id.Value + "-" + right.Id.Value),
        left.Id, null, right.Id, null, WorkflowEdgeKind.Direct, "") { Routing = WorkflowEdgeRouting.Always };
}
