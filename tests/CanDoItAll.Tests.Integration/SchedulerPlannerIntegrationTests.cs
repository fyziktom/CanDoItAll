using System.Data.Common;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Automation;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration;

public sealed class SchedulerPlannerIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task SavePlanAsync_persists_plan_projects_calendar_events_and_writes_quartz_store()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("scheduler-planner-save");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        await using var provider = await BuildProviderAsync(profile);

        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var planner = scope.ServiceProvider.GetRequiredService<ISchedulerPlannerService>();
        var targetId = await SeedProcessTargetAsync(dbContextFactory, "Scheduled onboarding process");

        var summary = await planner.SavePlanAsync(new SchedulerPlanEditorModel
        {
            Name = "Weekday onboarding check",
            Description = "Starts the onboarding process every weekday morning.",
            TargetKind = SchedulerPlanTargetKind.Process,
            TargetId = targetId,
            CronExpression = "0 0 9 ? * MON-FRI",
            TimeZoneId = "UTC",
            MisfirePolicy = AutomationTriggerMisfirePolicy.FireOnceNow,
            InputJson = "{}",
            IsEnabled = true
        });

        var workspace = await planner.GetWorkspaceAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        Assert.Equal("Weekday onboarding check", summary.Name);
        Assert.NotNull(summary.NextPlannedFireAtUtc);
        Assert.Contains(workspace.Plans, item => item.Id == summary.Id);
        Assert.Contains(workspace.CalendarSurface.Events, item =>
            item.EventType == nameof(SchedulerPlanTargetKind.Process) &&
            item.Title == "Weekday onboarding check" &&
            item.Status == "Scheduled");
        Assert.False(workspace.CalendarSurface.AllowCreate);
        Assert.False(workspace.CalendarSurface.AllowEdit);
        Assert.False(workspace.CalendarSurface.AllowDelete);
        Assert.False(workspace.CalendarSurface.AllowDragDrop);
        Assert.False(workspace.CalendarSurface.AllowResize);
        Assert.Equal(1, await CountRowsAsync(dbContext.Database.GetDbConnection(), "QRTZ_JOB_DETAILS"));
        Assert.Equal(1, await CountRowsAsync(dbContext.Database.GetDbConnection(), "QRTZ_CRON_TRIGGERS"));
    }

    [Fact]
    public async Task Saved_plan_and_quartz_store_survive_provider_restart()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("scheduler-planner-restart");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var planId = Guid.Empty;

        await using (var provider = await BuildProviderAsync(profile))
        {
            await using var scope = provider.CreateAsyncScope();
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            var planner = scope.ServiceProvider.GetRequiredService<ISchedulerPlannerService>();
            var targetId = await SeedProcessTargetAsync(dbContextFactory, "Restart durable process");

            var summary = await planner.SavePlanAsync(new SchedulerPlanEditorModel
            {
                Name = "Restart durable schedule",
                TargetKind = SchedulerPlanTargetKind.Process,
                TargetId = targetId,
                CronExpression = "0 0/15 * * * ?",
                TimeZoneId = "UTC",
                MisfirePolicy = AutomationTriggerMisfirePolicy.FireOnceNow,
                InputJson = "{}",
                IsEnabled = true
            });
            planId = summary.Id;
        }

        await using (var restartedProvider = await BuildProviderAsync(profile))
        {
            await using var scope = restartedProvider.CreateAsyncScope();
            var planner = scope.ServiceProvider.GetRequiredService<ISchedulerPlannerService>();
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            var workspace = await planner.GetWorkspaceAsync();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            Assert.Contains(workspace.Plans, item => item.Id == planId && item.IsEnabled);
            Assert.Equal(1, await CountRowsAsync(dbContext.Database.GetDbConnection(), "QRTZ_JOB_DETAILS"));
            Assert.Equal(1, await CountRowsAsync(dbContext.Database.GetDbConnection(), "QRTZ_CRON_TRIGGERS"));
        }
    }

    [Fact]
    public async Task Trigger_fire_handler_dispatches_once_for_the_same_dedupe_key()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("scheduler-planner-fire");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var launcher = new RecordingSchedulerTargetLauncher();
        await using var provider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.RemoveAll<ISchedulerTargetLauncher>();
                services.AddSingleton(launcher);
                services.AddScoped<ISchedulerTargetLauncher>(serviceProvider =>
                    serviceProvider.GetRequiredService<RecordingSchedulerTargetLauncher>());
            });

        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var planner = scope.ServiceProvider.GetRequiredService<ISchedulerPlannerService>();
        var targetId = await SeedProcessTargetAsync(dbContextFactory, "Deduped scheduled process");
        var planSummary = await planner.SavePlanAsync(new SchedulerPlanEditorModel
        {
            Name = "Deduped process launch",
            TargetKind = SchedulerPlanTargetKind.Process,
            TargetId = targetId,
            CronExpression = "0 0/5 * * * ?",
            TimeZoneId = "UTC",
            MisfirePolicy = AutomationTriggerMisfirePolicy.FireOnceNow,
            InputJson = "{}",
            IsEnabled = true
        });

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var plan = await dbContext.Set<SchedulerPlan>().SingleAsync(item => item.Id == planSummary.Id);
        var trigger = await dbContext.Set<AutomationTriggerRecord>().SingleAsync(item => item.Id == plan.AutomationTriggerId);
        var firedAtUtc = new DateTimeOffset(2026, 5, 12, 9, 0, 0, TimeSpan.Zero);
        var request = new AutomationTriggerFireRequest(
            trigger.Id,
            trigger.TriggerKey,
            trigger.OwnerKey,
            trigger.OwnerKind,
            trigger.PayloadJson,
            firedAtUtc);
        var handler = scope.ServiceProvider.GetServices<IAutomationMessageHandler>()
            .Single(item => item is SchedulerPlannerTriggerFireHandler);
        var context = new AutomationMessageContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AutomationEnvelopeTypeNames.For<AutomationTriggerFireRequest>(),
            Guid.NewGuid(),
            null,
            "scheduler-planner-dedupe-key",
            firedAtUtc);

        var first = await handler.HandleAsync(JsonSerializer.Serialize(request, JsonOptions), context, CancellationToken.None);
        var second = await handler.HandleAsync(JsonSerializer.Serialize(request, JsonOptions), context, CancellationToken.None);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var run = await verificationContext.Set<SchedulerPlanRun>().SingleAsync(item => item.PlanId == plan.Id);
        var updatedPlan = await verificationContext.Set<SchedulerPlan>().SingleAsync(item => item.Id == plan.Id);

        Assert.Equal(AutomationDeliveryAttemptOutcome.Completed, first.Outcome);
        Assert.Equal(AutomationDeliveryAttemptOutcome.Completed, second.Outcome);
        Assert.Single(launcher.Calls);
        Assert.Equal(SchedulerPlanRunDispatchStatus.Dispatched, run.Status);
        Assert.Equal(1, run.AttemptCount);
        Assert.Equal(firedAtUtc, run.FiredAtUtc);
        Assert.Equal(firedAtUtc, updatedPlan.LastFiredAtUtc);
    }

    [Fact]
    public async Task Target_launcher_starts_real_process_run()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("scheduler-planner-process-launch");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        await using var provider = await BuildProviderAsync(profile);

        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var launcher = scope.ServiceProvider.GetRequiredService<ISchedulerTargetLauncher>();
        var targetId = await SeedProcessTargetAsync(dbContextFactory, "Launchable scheduled process");
        var firedAtUtc = new DateTimeOffset(2026, 5, 12, 10, 0, 0, TimeSpan.Zero);

        var result = await launcher.LaunchAsync(
            CreatePlan(SchedulerPlanTargetKind.Process, targetId, null, "Real process launch proof"),
            firedAtUtc);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var processRun = await dbContext.Set<ProcessRun>().SingleAsync(item => item.Id == result.TargetRunId);
        Assert.Equal(SchedulerPlanTargetKind.Process, result.TargetKind);
        Assert.Contains("Real process launch proof", processRun.Name, StringComparison.Ordinal);
        Assert.Contains("scheduler plan", processRun.TriggerReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Target_launcher_starts_real_workflow_run()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("scheduler-planner-workflow-launch");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        await using var provider = await BuildProviderAsync(profile);

        await using var scope = provider.CreateAsyncScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
        var runStore = scope.ServiceProvider.GetRequiredService<IWorkflowRunStore>();
        var launcher = scope.ServiceProvider.GetRequiredService<ISchedulerTargetLauncher>();
        var definition = await CreateWorkflowTargetAsync(catalogService);

        var result = await launcher.LaunchAsync(
            CreatePlan(
                SchedulerPlanTargetKind.Workflow,
                definition.Id.Value,
                definition.VersionId.Value,
                "Real workflow launch proof"),
            new DateTimeOffset(2026, 5, 12, 10, 30, 0, TimeSpan.Zero));
        var workflowRun = await runStore.GetRunAsync(new WorkflowRunId(result.TargetRunId));

        Assert.NotNull(workflowRun);
        Assert.Equal(SchedulerPlanTargetKind.Workflow, result.TargetKind);
        Assert.Equal(definition.Id, workflowRun.WorkflowId);
        Assert.Equal(definition.VersionId, workflowRun.VersionId);
    }

    private static Task<ServiceProvider> BuildProviderAsync(
        TestDatabaseProfile profile,
        Action<IServiceCollection>? configureServices = null,
        CancellationToken cancellationToken = default)
    {
        Quartz.Logging.LogProvider.IsDisabled = false;
        Quartz.Logging.LogProvider.SetCurrentLogProvider(new NoOpQuartzLogProvider());

        return TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Automation:Runtime:Mqtt:Enabled"] = "false",
                ["Automation:Runtime:Mqtt:Host"] = string.Empty
            },
            configureServices,
            cancellationToken);
    }

    private static async Task<Guid> SeedProcessTargetAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        string name)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var now = DateTimeOffset.UtcNow;
        var definitionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var definition = new ProcessDefinition
        {
            Id = definitionId,
            Name = name,
            Slug = $"scheduler-{definitionId:N}",
            Summary = $"{name} summary.",
            ValueStatement = $"{name} value.",
            CustomerName = "Scheduler tests",
            OwnerName = "Scheduler tests",
            InterfaceContractSummary = "Test process target.",
            GovernanceNotes = "Seeded by scheduler planner tests.",
            Status = ProcessDefinitionStatus.Published,
            NextVersionNumber = 2,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Set<ProcessDefinition>().Add(definition);
        await dbContext.SaveChangesAsync();

        dbContext.Set<ProcessDefinitionVersion>().Add(new ProcessDefinitionVersion
        {
            Id = versionId,
            ProcessDefinitionId = definitionId,
            VersionNumber = 1,
            Status = ProcessVersionStatus.Published,
            ChangeSummary = "Initial published test version.",
            GovernancePolicySummary = "Scheduler test policy.",
            ConstitutionRuleSummary = "Scheduler test rule.",
            OperatingModeSummary = "Assisted execution.",
            SimulationReadinessSummary = "Ready.",
            PublishedAtUtc = now,
            PublishedBy = "scheduler-planner-tests",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await dbContext.SaveChangesAsync();

        definition.ActivePublishedVersionId = versionId;
        definition.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync();

        return definitionId;
    }

    private static SchedulerPlan CreatePlan(
        SchedulerPlanTargetKind targetKind,
        Guid targetId,
        Guid? targetVersionId,
        string name)
    {
        var now = DateTimeOffset.UtcNow;
        return new SchedulerPlan
        {
            Id = Guid.NewGuid(),
            Name = name,
            TargetKind = targetKind,
            TargetId = targetId,
            TargetVersionId = targetVersionId,
            TargetNameSnapshot = name,
            CronExpression = "0 0 9 ? * MON-FRI",
            CronDescription = "Every weekday at 09:00 UTC.",
            TimeZoneId = "UTC",
            MisfirePolicy = AutomationTriggerMisfirePolicy.FireOnceNow,
            InputJson = "{}",
            IsEnabled = true,
            AutomationTriggerId = Guid.NewGuid(),
            AutomationTriggerKey = $"scheduler-planner:{Guid.NewGuid():N}",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private static Task<WorkflowDefinition> CreateWorkflowTargetAsync(IWorkflowCatalogService catalogService)
    {
        var start = new WorkflowNodeId("start");
        var end = new WorkflowNodeId("end");
        return catalogService.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: "Launchable scheduled workflow",
            Description: "Workflow definition used to verify scheduler target launch.",
            WorkflowLifecycleStatus.Active,
            new WorkflowGraph(
                start,
                [
                    CreateWorkflowNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                    CreateWorkflowNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
                ],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-to-end"),
                        start,
                        SourcePortId: null,
                        end,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty)
                ]),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)));
    }

    private static WorkflowNode CreateWorkflowNode(
        WorkflowNodeId id,
        WorkflowNodeKind kind,
        WorkflowValueShape? inputShape = null,
        WorkflowValueShape? resultShape = null)
    {
        return new WorkflowNode(
            id,
            kind,
            id.Value,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: inputShape ?? WorkflowValueShape.Text,
                ResultShape: resultShape ?? WorkflowValueShape.Text));
    }

    private static async Task<int> CountRowsAsync(DbConnection connection, string tableName)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName};";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private sealed class RecordingSchedulerTargetLauncher : ISchedulerTargetLauncher
    {
        public List<(Guid PlanId, DateTimeOffset FiredAtUtc)> Calls { get; } = [];

        public Task<SchedulerTargetLaunchResult> LaunchAsync(
            SchedulerPlan plan,
            DateTimeOffset firedAtUtc,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((plan.Id, firedAtUtc));
            return Task.FromResult(new SchedulerTargetLaunchResult(
                plan.TargetKind,
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "Started",
                "Fake scheduler target launch."));
        }
    }

    private sealed class NoOpQuartzLogProvider : Quartz.Logging.ILogProvider
    {
        public Quartz.Logging.Logger GetLogger(string name)
        {
            return static (_, _, _, _) => false;
        }

        public IDisposable OpenNestedContext(string message)
        {
            return NoOpDisposable.Instance;
        }

        public IDisposable OpenMappedContext(string key, object value, bool destructure = false)
        {
            return NoOpDisposable.Instance;
        }
    }

    private sealed class NoOpDisposable : IDisposable
    {
        public static readonly IDisposable Instance = new NoOpDisposable();

        public void Dispose()
        {
        }
    }
}
