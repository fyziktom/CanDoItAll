using System.Globalization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "HostPlatform")]
public sealed class SchedulerProjectionRestartTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Enabled_completed_finite_plan_survives_two_native_projection_restarts_without_replay(bool explicitStart) {
        await using var environment = CanDoItAllTestEnvironment.Create("ac1-scheduler-restart");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = environment.CreatePostgreSqlProfile("owner") };
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var fired = now.AddYears(-1);
        var next = now.AddDays(2);
        SchedulerPlan completed;
        SchedulerPlan future;
        SchedulerPlanRun history;
        SchedulerFireAdmissionRecord admission;
        await using (var application = await TestApplication.CreateAsync(harness)) {
            await using var scope = application.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var workflow = await SeedWorkflowAsync(services);
            var authority = await services.GetRequiredService<IWorkflowStructureAuthorityFactory>()
                .CaptureLocalOperatorAsync(WorkflowStructureOperatorSurface.UserInterface);
            completed = Plan(workflow, "Completed finite schedule", fired);
            future = Plan(workflow, "Future neighbor schedule", next);
            if (explicitStart) {
                completed.StartAtUtc = fired.AddMinutes(-5);
                completed.EndAtUtc = fired.AddMinutes(5);
            }
            completed.StructureAuthorityJson = SchedulerFireSnapshot.SerializeAuthority(authority);
            future.StructureAuthorityJson = completed.StructureAuthorityJson;
            await using (var database = await Factory(application).CreateDbContextAsync()) {
                database.AddRange(completed, future);
                await database.SaveChangesAsync();
            }
            await services.GetRequiredService<ISchedulerPlannerRunDispatcher>().DispatchAsync(
                new(completed.Id, Guid.NewGuid(), Guid.NewGuid(), fired, null));
            await using var readback = await Factory(application).CreateDbContextAsync();
            history = Assert.Single(await readback.Set<SchedulerPlanRun>().AsNoTracking().Where(row => row.PlanId == completed.Id).ToArrayAsync());
            admission = Assert.Single(await readback.Set<SchedulerFireAdmissionRecord>().AsNoTracking().Where(row => row.PlanId == completed.Id).ToArrayAsync());
            Assert.Equal(SchedulerPlanRunDispatchStatus.Dispatched, history.Status);
            Assert.Equal(SchedulerFireAdmissionState.Observed, admission.State);
            Assert.Equal(history.TargetRunId, admission.AcceptedWorkflowRunId);
            var run = await services.GetRequiredService<IWorkflowRuntimeManager>().GetRunAsync(new(history.TargetRunId!.Value));
            Assert.Equal(WorkflowRunState.Completed, run!.State);
            Assert.Equal(workflow.VersionId, run.VersionId);
        }
        for (var restart = 0; restart < 2; restart++) {
            await using var application = await TestApplication.CreateAsync(harness);
            var scheduler = await application.Services.GetRequiredService<ISchedulerFactory>().GetScheduler();
            try {
                var hosted = new SchedulerPlannerProjectionHostedService(application.Services.GetRequiredService<IServiceScopeFactory>(),
                    NullLogger<SchedulerPlannerProjectionHostedService>.Instance);
                await hosted.StartAsync(CancellationToken.None);
                await hosted.StartAsync(CancellationToken.None);
                Assert.False(await scheduler.CheckExists(Job(completed.Id)));
                var trigger = Assert.Single(await scheduler.GetTriggersOfJob(Job(future.Id)));
                Assert.Equal(next, trigger.GetNextFireTimeUtc());
                await using var database = await Factory(application).CreateDbContextAsync();
                var saved = await database.Set<SchedulerPlan>().AsNoTracking().SingleAsync(row => row.Id == completed.Id);
                Assert.True(saved.IsEnabled);
                Assert.Null(saved.NextPlannedFireAtUtc);
                Assert.Equal(fired, saved.LastFiredAtUtc);
                Assert.Equal(completed.TargetVersionId, saved.TargetVersionId);
                Assert.Equal(completed.StructureAuthorityJson, saved.StructureAuthorityJson);
                var retained = Assert.Single(await database.Set<SchedulerPlanRun>().AsNoTracking().Where(row => row.PlanId == completed.Id).ToArrayAsync());
                Assert.Equal(history.Id, retained.Id);
                Assert.Equal(history.TargetRunId, retained.TargetRunId);
                Assert.Equal(1, retained.AttemptCount);
                var retainedAdmission = Assert.Single(await database.Set<SchedulerFireAdmissionRecord>().AsNoTracking().Where(row => row.PlanId == completed.Id).ToArrayAsync());
                Assert.Equal(admission.Id, retainedAdmission.Id);
                Assert.Equal(admission.SnapshotJson, retainedAdmission.SnapshotJson);
                Assert.Equal(admission.Generation, retainedAdmission.Generation);
                Assert.Equal(admission.State, retainedAdmission.State);
            } finally {
                await scheduler.Shutdown(waitForJobsToComplete: true);
            }
        }
    }

    [Fact]
    public async Task Native_projection_preserves_unresolved_admission_and_valid_jobs_when_replacement_validation_fails() {
        await using var environment = CanDoItAllTestEnvironment.Create("ac1-scheduler-reconciliation");
        await using var application = await TestApplication.CreateAsync(new() {
            TestEnvironment = environment, ActiveProfile = environment.CreatePostgreSqlProfile("owner")
        });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workflow = await SeedWorkflowAsync(services);
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var future = Plan(workflow, "Future editable", now.AddDays(2));
        var neighbor = Plan(workflow, "Future neighbor", now.AddDays(3));
        var unresolved = Plan(workflow, "Unresolved admission", now.AddYears(-1));
        unresolved.StartAtUtc = now.AddYears(-1).AddMinutes(-5);
        var authority = await services.GetRequiredService<IWorkflowStructureAuthorityFactory>()
            .CaptureLocalOperatorAsync(WorkflowStructureOperatorSurface.UserInterface);
        unresolved.StructureAuthorityJson = SchedulerFireSnapshot.SerializeAuthority(authority);
        await using (var database = await Factory(application).CreateDbContextAsync()) {
            database.AddRange(future, neighbor, unresolved);
            await database.SaveChangesAsync();
        }
        var claim = await services.GetRequiredService<SchedulerFireAdmissionStore>().AcquireAsync(
            new(unresolved.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddYears(-1), null));
        Assert.NotNull(claim);
        var scheduler = await services.GetRequiredService<ISchedulerFactory>().GetScheduler();
        try {
            var projection = services.GetRequiredService<ISchedulerPlannerTriggerScheduler>();
            await projection.SynchronizeAsync();
            await projection.SynchronizeAsync();
            Assert.False(await scheduler.CheckExists(Job(unresolved.Id)));
            var original = Assert.Single(await scheduler.GetTriggersOfJob(Job(future.Id)));
            await using (var database = await Factory(application).CreateDbContextAsync()) {
                var plan = await database.Set<SchedulerPlan>().SingleAsync(row => row.Id == future.Id);
                plan.CronExpression = "invalid";
                await database.SaveChangesAsync();
            }
            await Assert.ThrowsAsync<FormatException>(() => projection.SynchronizePlanAsync(future.Id));
            Assert.Equal(original.GetNextFireTimeUtc(), Assert.Single(await scheduler.GetTriggersOfJob(Job(future.Id))).GetNextFireTimeUtc());
            Assert.True(await scheduler.CheckExists(Job(neighbor.Id)));
            using (var canceled = new CancellationTokenSource()) {
                canceled.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => projection.SynchronizePlanAsync(neighbor.Id, canceled.Token));
            }
            await using (var database = await Factory(application).CreateDbContextAsync()) {
                var plan = await database.Set<SchedulerPlan>().SingleAsync(row => row.Id == future.Id);
                plan.IsEnabled = false;
                database.Remove(await database.Set<SchedulerPlan>().SingleAsync(row => row.Id == neighbor.Id));
                await database.SaveChangesAsync();
            }
            await projection.SynchronizeAsync();
            Assert.False(await scheduler.CheckExists(Job(future.Id)));
            Assert.False(await scheduler.CheckExists(Job(neighbor.Id)));
            await using var readback = await Factory(application).CreateDbContextAsync();
            var admission = Assert.Single(await readback.Set<SchedulerFireAdmissionRecord>().AsNoTracking().ToArrayAsync());
            var history = Assert.Single(await readback.Set<SchedulerPlanRun>().AsNoTracking().ToArrayAsync());
            Assert.Equal(claim.Snapshot.PlanRunId, admission.Id);
            Assert.Equal(claim.Generation, admission.Generation);
            Assert.Equal(SchedulerFireAdmissionState.Dispatching, admission.State);
            Assert.Equal(SchedulerPlanRunDispatchStatus.Dispatching, history.Status);
            Assert.Null(history.TargetRunId);
            Assert.True((await readback.Set<SchedulerPlan>().SingleAsync(row => row.Id == unresolved.Id)).IsEnabled);
        } finally {
            await scheduler.Shutdown(waitForJobsToComplete: true);
        }
    }

    private static IDbContextFactory<SchedulerPlannerDbContext> Factory(TestApplication application)
        => application.Services.GetRequiredService<IDbContextFactory<SchedulerPlannerDbContext>>();

    private static JobKey Job(Guid id) => new($"plan-{id:N}", "candoitall-scheduler-planner");

    private static SchedulerPlan Plan(WorkflowDefinition workflow, string name, DateTimeOffset occurrence) => new() {
        Name = name,
        TargetKind = SchedulerPlanTargetKind.Workflow,
        TargetId = workflow.Id.Value,
        TargetVersionId = workflow.VersionId.Value,
        TargetNameSnapshot = workflow.Name,
        CronExpression = occurrence.ToString("s m H d M '?' yyyy", CultureInfo.InvariantCulture),
        TimeZoneId = "UTC",
        SchedulerTriggerId = Guid.NewGuid(),
        SchedulerTriggerKey = Guid.NewGuid().ToString("N"),
        InputJson = "{\"source\":\"owned-restart\"}",
        CreatedAtUtc = occurrence < DateTimeOffset.UtcNow ? occurrence.AddMinutes(-5) : DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    private static async Task<WorkflowDefinition> SeedWorkflowAsync(IServiceProvider services) {
        var start = new WorkflowNodeId("start");
        var end = new WorkflowNodeId("end");
        static WorkflowNode Node(WorkflowNodeId id, WorkflowNodeKind kind) => new(id, kind, id.Value, [],
            new(null, null, null, null, string.Empty, WorkflowValueShape.Text, WorkflowValueShape.Text));
        return await services.GetRequiredService<IWorkflowCatalogService>().SaveDefinitionAsync(new(
            WorkflowId.New(), null, "Owned restart Workflow", "Deterministic start/end", WorkflowLifecycleStatus.Active,
            new(start, [Node(start, WorkflowNodeKind.Start), Node(end, WorkflowNodeKind.End)],
                [new(new("start-end"), start, null, end, null, WorkflowEdgeKind.Direct, string.Empty) { Routing = WorkflowEdgeRouting.Always }]),
            new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false)));
    }
}
