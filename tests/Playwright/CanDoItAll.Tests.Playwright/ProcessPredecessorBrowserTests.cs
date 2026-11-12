using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
[Trait("Category", "LiveProcess")]
public sealed class ProcessPredecessorBrowserTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retained_predecessor_survives_reentry_and_OS_restart_without_rebinding_to_a_recreated_project(bool knownOrigin) {
        await using var host = new ProcessAuthoringRestartHost();
        var startup = Stopwatch.StartNew();
        await host.StartAsync();
        startup.Stop();
        var firstProcessId = host.ProcessId;
        var projectId = Guid.NewGuid();
        var retained = await host.ReadAsync(async services => {
            Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(projectId, new() { Name = "Predecessor recovery" })).IsSuccess);
            var workbench = services.GetRequiredService<ProjectWorkbenchService>();
            var node = await workbench.CreateObjectAsync(projectId, new(ProjectObjectType.ProjectBlock, "Legacy source", "Retained predecessor",
                "Keep the original runtime identity.", $"project:{projectId:D}", 300, 200));
            var current = (await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId))!;
            ProcessProjectAdmission admission = new(current.DatabaseProfileId, current.ProjectId, current.LifetimeId);
            await using var database = await services.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync();
            var variables = new Dictionary<string, string> {
                [ProcessRuntimeLaunchVariables.ProjectId] = projectId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ProjectNodeId] = node.Id
            };
            var parent = await ProcessPredecessorFixture.SeedRetainedAsync(database, true, admission, ProcessRuntimeStatus.Blocked, variables);
            var assignments = await services.GetRequiredService<IProcessRuntimeStepAssignmentStore>().LoadByRunAsync(parent.RunId);
            var step = Assert.Single(assignments, item => item.AllowedOperations.Contains(ProcessOperationContractNames.ExecuteExternalAction)).StepInstanceId;
            variables[ProcessRuntimeLaunchVariables.ParentProcessRunId] = parent.RunId.ToString();
            variables[ProcessRuntimeLaunchVariables.ParentProcessStepId] = step.ToString();
            variables[ProcessRuntimeLaunchVariables.SubprocessDefinitionKey] = ProcessPredecessorFixture.DefinitionKey;
            var child = await ProcessPredecessorFixture.SeedRetainedAsync(database, false, knownOrigin ? admission : null, ProcessRuntimeStatus.Blocked, variables, parent.RootRunId);
            var created = ProcessPredecessorFixture.ReadInitialCommit(true).Mutation.Events.Concat(
                ProcessPredecessorFixture.ReadInitialCommit().Mutation.Events.Select(item => item with { RootRunId = parent.RootRunId })).ToArray();
            await services.GetRequiredService<IProcessRuntimeEventStore>().AppendAsync(created.Concat(created.Select(item => item with {
                EventId = new(Guid.NewGuid()), EventType = new(ProcessRuntimeProjectionEventTypeNames.ProcessRunBlocked)
            })).ToArray());
            await services.GetRequiredService<ProcessRuntimeProjectionCatchupService>().CatchUpAsync();
            var childNode = $"process-run:{child.RunId.Value:D}";
            await workbench.LinkObjectsAsync(projectId, node.Id, childNode, ProjectObjectLinkKind.Uses);
            await workbench.LinkObjectsAsync(projectId, $"process-run:{parent.RunId.Value:D}", childNode, ProjectObjectLinkKind.Uses);
            return (Parent: parent, Child: child, Step: step);
        });
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        await using var restartedContext = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        await restartedContext.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        var before = await ReadRowsAsync();
        string? refusal = null;
        var recoveryMilliseconds = new List<double>();
        var restart = new Stopwatch();
        try {
            for (var attempt = 0; attempt < 2; attempt++) {
                if (attempt == 1) {
                    restart.Start();
                    await host.RestartAsync();
                    restart.Stop();
                    Assert.NotEqual(firstProcessId, host.ProcessId);
                    page = await restartedContext.NewPageAsync();
                    page.PageError += (_, error) => errors.Add(error);
                }
                var recovery = Stopwatch.StartNew();
                if (knownOrigin) {
                    var recovered = await ReenterAsync();
                    Assert.Equal(retained.Child.RunId.Value, recovered.RunId);
                    Assert.Equal(retained.Child.PlanId.Value, recovered.LaunchPlanId);
                } else {
                    var error = await Assert.ThrowsAsync<ProcessExistingLaunchReconciliationRequiredException>(ReenterAsync);
                    Assert.Equal(retained.Child.RunId, error.RunId);
                    Assert.Contains("automatic replacement is refused", error.Message, StringComparison.Ordinal);
                    refusal = error.Message;
                }
                recoveryMilliseconds.Add(recovery.Elapsed.TotalMilliseconds);
                await page.GotoAsync($"{host.BaseUrl}/processes/live?runId={retained.Child.RunId.Value:D}");
                await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
                var filter = page.GetByTestId("live-processes-run-filter").First;
                await Assertions.Expect(filter).ToBeVisibleAsync();
                await filter.SelectOptionAsync(retained.Child.RunId.Value.ToString("D"));
                var card = page.GetByTestId("live-processes-run-card").Filter(new() { HasTextString = retained.Child.RunId.Value.ToString("N")[..8] });
                await Assertions.Expect(card).ToBeVisibleAsync();
                await card.GetByTestId("live-processes-run-open-details").ClickAsync();
                await Assertions.Expect(page.GetByTestId("live-processes-run-detail-dialog")).ToContainTextAsync(retained.Child.RunId.Value.ToString("N")[..8]);
                await AssertRowsAsync();
            }
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, $"pc4-legacy-{knownOrigin}-retained.png"), FullPage = true });
            await host.ReadAsync(async services => {
                var projects = services.GetRequiredService<ProjectsService>();
                await ProjectLifecycleTestCommands.DeleteAsync(services, projectId);
                var created = await projects.CreateAsync(projectId, new() { Name = "Successor legacy project" });
                Assert.True(created.IsSuccess);
                return created;
            });
            var mismatch = await Assert.ThrowsAsync<ProjectStructureAgentException>(ReenterAsync);
            Assert.Equal("ProcessSubprocessParentOriginMismatch", mismatch.ErrorCode);
            await page.GotoAsync($"{host.BaseUrl}/projects/{projectId:D}/processes/live?runId={retained.Child.RunId.Value:D}");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await Assertions.Expect(page.GetByTestId("live-processes-refresh-button")).ToBeEnabledAsync();
            await Assertions.Expect(page.GetByTestId("live-processes-run-card")).ToHaveCountAsync(0);
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, $"pc4-legacy-{knownOrigin}-successor.png"), FullPage = true });
            await AssertRowsAsync();
            Assert.Empty(errors);
            await File.WriteAllTextAsync(Path.Combine(host.Evidence, "pc4-legacy-oracle.json"), JsonSerializer.Serialize(new {
                knownOrigin, Parent = retained.Parent.RunId, Child = retained.Child.RunId, retained.Child.PlanId, retained.Child.PlanHash,
                Refusal = refusal, RecreatedProjectOutcome = mismatch.ErrorCode, OriginalRowsPreserved = true,
                FirstProcessId = firstProcessId, RestartedProcessId = host.ProcessId, HostConfiguration = PlaywrightTestHostPaths.BuildConfiguration,
                StartupMilliseconds = startup.Elapsed.TotalMilliseconds, RestartMilliseconds = restart.Elapsed.TotalMilliseconds, RecoveryMilliseconds = recoveryMilliseconds
            }));
        } catch {
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "pc4-legacy-failure.png"), FullPage = true });
            throw;
        } finally {
            await context.Tracing.StopAsync(new() { Path = Path.Combine(host.Evidence, "pc4-legacy-trace.zip") });
            await restartedContext.Tracing.StopAsync(new() { Path = Path.Combine(host.Evidence, "pc4-legacy-restarted-trace.zip") });
        }

        Task<ProjectStructureProcessSubprocessLaunchResult> ReenterAsync() => host.ReadAsync(services =>
            services.GetRequiredService<ProjectStructureProcessNodeService>().StartSubprocessAsync(projectId, retained.Parent.RunId.ToString(), retained.Step.ToString(),
                new(ProcessPredecessorFixture.DefinitionKey, RunHrMatch: false, Execute: false, IncludeLaunchPlan: true),
                new("pc4-legacy-browser", "Recovery operator", "fixture-host", string.Empty, string.Empty, "retained-reentry")));

        async Task AssertRowsAsync() {
            var after = await ReadRowsAsync();
            if (!before.SequenceEqual(after)) {
                await File.WriteAllTextAsync(Path.Combine(host.Evidence, "pc4-legacy-row-diff.json"), JsonSerializer.Serialize(new {
                    Before = before.Except(after).ToArray(), After = after.Except(before).ToArray()
                }));
            }
            Assert.Equal(before, after);
        }

        Task<string[]> ReadRowsAsync() => host.ReadAsync(async services => {
            await using var database = await services.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync();
            Assert.Equal(2, await database.RuntimeStates.CountAsync());
            Assert.Equal(ProcessPredecessorFixture.PlanJson, (await database.InstancePlans.SingleAsync(item => item.PlanId == retained.Child.PlanId.Value)).PayloadJson);
            return await database.Database.SqlQueryRaw<string>("""
                SELECT to_jsonb(p)::text AS "Value" FROM process_instance_plans p
                UNION ALL SELECT to_jsonb(p)::text AS "Value" FROM process_runtime_states p
                UNION ALL SELECT to_jsonb(p)::text AS "Value" FROM process_runtime_step_assignments p
                ORDER BY "Value"
                """).ToArrayAsync();
        });
    }
}
