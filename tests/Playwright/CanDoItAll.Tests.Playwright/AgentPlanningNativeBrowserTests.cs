using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Playwright.Smoke;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class AgentPlanningNativeBrowserTests {
    [Fact]
    public async Task Native_project_plan_pauses_on_another_actor_then_refreshes_and_publishes_the_reviewed_generation() {
        await using var provider = await PlanningResponseFixture.StartAsync();
        await using var host = await LiveUiHost.StartAsync();
        var seed = await SeedAsync(host, provider.BaseUrl);
        var page = await host.NewPageAsync();
        var oracle = CrmHrBrowserOracle.Attach(page);
        var passed = false;
        var observations = new List<object>();
        try {
            await oracle.NavigateAsync($"{host.BaseUrl}/projects/{seed.ProjectId:D}/structure");
            await ProjectFilesUiJourney.ReadyFileCanvasAsync(page);
            var chat = await AgentUiJourneySupport.OpenFloatingChatFromCatalogAsync(page,
                "project-structure-agents-toggle", seed.AgentId, host.Artifact("planning-catalog-failure.png"));
            var draft = chat.GetByTestId("chat-prompt-input");
            await draft.FillAsync("Plan two manual tasks using the current project.");
            await draft.PressAsync("Tab");
            var firstReviewTime = Stopwatch.StartNew();
            await chat.GetByTestId("agents-chat-plan-tasks").ClickAsync();
            await Assertions.Expect(chat.GetByTestId("agents-chat-planning-status")).ToContainTextAsync("Plan ready for review", new() { Timeout = 45_000 });
            observations.Add(new { Stage = "OriginalReview", firstReviewTime.ElapsedMilliseconds });
            var first = await ReadRunAsync();
            Assert.Equal(AgentChatTrustedSourceKinds.ProjectStructure, first.Run.SourceKind);
            Assert.Equal(seed.ProjectId.ToString("D"), first.Run.SourceId);
            Assert.Equal(1, first.PlanningReview!.Expected.Generation);
            Assert.Equal(1, provider.Requests);
            Assert.Empty(await ReadTasksAsync());
            await draft.FillAsync("Unsent draft before review.");
            await draft.PressAsync("Tab");
            await chat.GetByTestId("agents-chat-planning-open").ClickAsync();
            var dialog = page.GetByTestId("agents-chat-planning-dialog");
            await Assertions.Expect(dialog).ToBeVisibleAsync();
            await Assertions.Expect(dialog).ToContainTextAsync(seed.ProjectId.ToString("D"));
            await Assertions.Expect(dialog).ToContainTextAsync("Generation 1 task 1");
            await Assertions.Expect(dialog).ToContainTextAsync("<script>plain text</script>");
            Assert.Equal(0, await dialog.Locator("script").CountAsync());
            await ShotAsync("planning-original-review");

            await host.SeedAsync(services => services.GetRequiredService<ProjectWorkbenchService>().CreateObjectAsync(seed.ProjectId,
                new(ProjectObjectType.WorkItem, "Concurrent actor task", "Task", "A real material-basis change.", $"project:{seed.ProjectId:D}",
                    ObjectSubtype: ProjectObjectSubtypePolicy.Task,
                    MetadataJson: ProjectObjectMetadataSerializer.Serialize(new() {
                        WorkItem = new() { WorkItemKind = ProjectWorkItemKind.Task, ExecutionState = ProjectTaskExecutionState.NotStarted }
                    })) { ExpectedProjectAdmission = seed.Admission }));
            await dialog.GetByTestId("agents-chat-planning-approve").ClickAsync();
            await Assertions.Expect(chat.GetByTestId("agents-chat-planning-status")).ToContainTextAsync("Task data changed; review required", new() { Timeout = 45_000 });
            Assert.Equal(1, provider.Requests);
            Assert.Equal(["Concurrent actor task"], await ReadTasksAsync());
            await Assertions.Expect(draft).ToHaveValueAsync("Unsent draft before review.");
            await chat.GetByTestId("agents-chat-planning-open").ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("agents-chat-planning-approve")).ToHaveCountAsync(0);
            await Assertions.Expect(dialog.GetByTestId("agents-chat-planning-refresh")).ToBeEnabledAsync();
            await ShotAsync("planning-paused-after-actor-write");

            await dialog.GetByTestId("agents-chat-planning-refresh").ClickAsync();
            await provider.RefreshEntered.Task.WaitAsync(TimeSpan.FromSeconds(45));
            await Assertions.Expect(dialog.GetByTestId("agents-chat-planning-refresh")).ToBeDisabledAsync();
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).Last.ClickAsync();
            await Assertions.Expect(dialog).ToHaveCountAsync(0);
            await draft.FillAsync("Later unsent typing while the refreshed plan is being prepared.");
            await draft.PressAsync("Tab");
            await chat.GetByTestId("agents-chat-planning-open").ClickAsync();
            await Assertions.Expect(dialog).ToBeVisibleAsync();
            provider.ReleaseRefresh.TrySetResult();
            await Assertions.Expect(chat.GetByTestId("agents-chat-planning-status")).ToContainTextAsync("Plan ready for review", new() { Timeout = 45_000 });
            await Assertions.Expect(dialog).ToHaveCountAsync(0);
            await Assertions.Expect(draft).ToHaveValueAsync("Later unsent typing while the refreshed plan is being prepared.");
            var refreshed = await ReadRunAsync();
            Assert.Equal(first.Run.Id, refreshed.Run.Id);
            Assert.Equal(2, refreshed.PlanningReview!.Expected.Generation);
            Assert.Equal(2, provider.Requests);
            await chat.GetByTestId("agents-chat-planning-open").ClickAsync();
            await Assertions.Expect(dialog).ToContainTextAsync("Generation 2 task 1");
            await Assertions.Expect(dialog).Not.ToContainTextAsync("Generation 1 task 1");
            await ShotAsync("planning-refreshed-review");
            await dialog.GetByTestId("agents-chat-planning-approve").ClickAsync();
            await Assertions.Expect(chat.GetByTestId("agents-chat-planning-status")).ToContainTextAsync("Planning completed", new() { Timeout = 45_000 });
            var completed = await ReadRunAsync();
            Assert.Equal(AgentPlanningGenerationState.Completed, completed.PlanningReview!.State);
            Assert.Equal(["Concurrent actor task", "Generation 2 task 1", "Generation 2 task 2"], await ReadTasksAsync());
            Assert.Equal(2, provider.Requests);
            await Assertions.Expect(draft).ToHaveValueAsync("Later unsent typing while the refreshed plan is being prepared.");
            await Assertions.Expect(page.GetByText("Generation 2 task 1", new() { Exact = true }).First).ToBeVisibleAsync(new() { Timeout = 45_000 });
            await Assertions.Expect(page.GetByText("Generation 2 task 2", new() { Exact = true }).First).ToBeVisibleAsync();
            await chat.GetByTestId("agents-chat-planning-open").ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("agents-chat-planning-approve")).ToHaveCountAsync(0);
            await dialog.GetByTestId("agents-chat-planning-reload").ClickAsync();
            await Assertions.Expect(dialog).ToHaveCountAsync(0);
            await Assertions.Expect(chat.GetByTestId("agents-chat-planning-status")).ToContainTextAsync("Planning completed", new() { Timeout = 45_000 });
            await Assertions.Expect(chat.GetByTestId("agents-chat-plan-tasks")).ToBeEnabledAsync();
            Assert.Equal(3, (await ReadTasksAsync()).Length);
            Assert.Equal(2, provider.Requests);
            observations.Add(new { seed.ProjectId, seed.AgentId, RunId = first.Run.Id,
                First = first.PlanningReview.Expected, Refreshed = refreshed.PlanningReview.Expected,
                Completed = completed.PlanningReview.Expected, provider.Requests });
            await ShotAsync("planning-completed-original-draft");
            await oracle.AssertCleanAsync();
            passed = true;

            Task<ExecutionRunDetail> ReadRunAsync() => host.SeedAsync(async services => {
                var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
                var run = Assert.Single(await workspace.ListExecutionRunsAsync(new ExecutionRunQuery(AgentId: seed.AgentId)));
                return await workspace.GetExecutionRunDetailAsync(run.Id);
            });
            Task<string[]> ReadTasksAsync() => host.SeedAsync(async services => {
                await using var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
                return await database.Set<ProjectObjectRecord>().Where(row => row.ProjectId == seed.ProjectId)
                    .OrderBy(row => row.Title).Select(row => row.Title).ToArrayAsync();
            });
            async Task ShotAsync(string name) {
                Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth"));
                await page.ScreenshotAsync(new() { Path = host.Artifact(name + ".png") });
            }
        } finally {
            provider.ReleaseRefresh.TrySetResult();
            if (!passed) {
                using var diagnostics = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try {
                    observations.Add(await host.SeedAsync(async services => {
                        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
                        var runs = await workspace.ListExecutionRunsAsync(new ExecutionRunQuery(AgentId: seed.AgentId), diagnostics.Token);
                        var run = runs.SingleOrDefault();
                        var detail = run is null ? null : await workspace.GetExecutionRunDetailAsync(run.Id, diagnostics.Token);
                        return new { Stage = "FailureReadback", RunState = detail?.Run.State.ToString(),
                            PlanningState = detail?.PlanningReview?.State.ToString(), detail?.PlanningReview?.HasUnsettledWork,
                            PendingApprovals = detail?.Run.PendingApprovals.Count };
                    }));
                } catch (Exception failure) {
                    observations.Add(new { Stage = "FailureReadback", FailureType = failure.GetType().Name });
                }
            }
            await page.ScreenshotAsync(new() { Path = host.Artifact(passed ? "planning-final.png" : "planning-failure.png"), Timeout = 5_000 });
            await host.CaptureHostLogAsync();
            await File.WriteAllTextAsync(host.Artifact("planning-evidence.json"), JsonSerializer.Serialize(new {
                Passed = passed, Viewport = "1920x1080@1", ControlledBoundary = "external HTTP model response",
                RealNativeContextAndPlanningOwners = true, provider.Requests, Observations = observations
            }));
        }
    }

    private static Task<(Guid ProjectId, Guid AgentId, ProjectWriteAdmission Admission)> SeedAsync(LiveUiHost host, string endpoint) => host.SeedAsync(async services => {
        var project = await services.GetRequiredService<ProjectsService>().SaveAsync(new ProjectEditorModel {
            Name = "Native concurrent planning", Description = "Owned browser planning fixture."
        });
        Assert.True(project.IsSuccess);
        var secret = await services.GetRequiredService<SecretService>().SaveAsync(new SecretEditorModel {
            Name = "Local planning fixture credential", Kind = SecretKind.ApiKey, SecretValue = "local-fixture-placeholder", Scope = "workspace"
        });
        Assert.True(secret.IsSuccess);
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var provider = await workspace.SaveProviderAsync(new ProviderProfileEditorModel {
            Name = "Local planning HTTP fixture", Kind = ProviderKind.OpenAi, Transport = ProviderTransportKind.ChatCompletions,
            BaseUrl = endpoint, ApiKeyEnvironmentVariable = $"secret:{secret.Value:D}",
            DefaultModel = ManagedSeedProviderFallbacks.OpenAiDefaultModel, SupportsStreaming = false,
            ModelPrices = [new() { Model = ManagedSeedProviderFallbacks.OpenAiDefaultModel, TariffKind = ProviderTariffKind.ExplicitFree }]
        });
        var agent = await workspace.SaveAgentAsync(new AgentEditorModel {
            Name = "Native task planning agent", Status = AgentLifecycleStatus.Active, ProviderProfileId = provider,
            Model = ManagedSeedProviderFallbacks.OpenAiDefaultModel, Instructions = "Prepare tasks for explicit review.",
            ProjectStructureAccess = new() { CanRead = true, CanWriteTasks = true, AllowedProjectIds = [project.Value] }
        });
        var settings = services.GetRequiredService<WorkspaceSettingsWriter>();
        var current = await settings.ReadV2Async(default);
        Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Accepted>(await settings.ReplaceV2Async(new(current.Expectation,
            current.Values, DecisionDataPolicy.PlanningDefault(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10))), default));
        var admission = await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(project.Value);
        return (project.Value, agent, Assert.IsType<ProjectWriteAdmission>(admission));
    });
}
