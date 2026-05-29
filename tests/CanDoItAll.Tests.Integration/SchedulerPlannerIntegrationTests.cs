using System.Collections.Concurrent;
using System.Data.Common;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Automation;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Plugins;
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
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
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
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
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
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
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
    public async Task Trigger_fire_handler_records_no_messages_as_success_without_retry()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("scheduler-planner-no-messages");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        var noMessageResult = new SchedulerTargetLaunchResult(
            SchedulerPlanTargetKind.Workflow,
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            SchedulerPlanRunDispatchStatus.NoMessages.ToString(),
            "No unprocessed Office365 email matched the configured address.",
            SchedulerPlanRunDispatchStatus.NoMessages,
            SchedulerPlanRunRoutes.NoMessages,
            SchedulerPlanRunRetryCategory.NoAction);
        var launcher = new RecordingSchedulerTargetLauncher(noMessageResult);
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
        var catalogService = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
        var planner = scope.ServiceProvider.GetRequiredService<ISchedulerPlannerService>();
        var definition = await CreateWorkflowTargetAsync(catalogService);
        var planSummary = await planner.SavePlanAsync(new SchedulerPlanEditorModel
        {
            Name = "No-message workflow launch",
            TargetKind = SchedulerPlanTargetKind.Workflow,
            TargetId = definition.Id.Value,
            TargetVersionId = definition.VersionId.Value,
            CronExpression = "0 0/5 * * * ?",
            TimeZoneId = "UTC",
            MisfirePolicy = AutomationTriggerMisfirePolicy.FireOnceNow,
            InputJson = "{}",
            IsEnabled = true
        });

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var plan = await dbContext.Set<SchedulerPlan>().SingleAsync(item => item.Id == planSummary.Id);
        plan.LastError = "Previous Microsoft Graph outage.";
        await dbContext.SaveChangesAsync();
        var trigger = await dbContext.Set<AutomationTriggerRecord>().SingleAsync(item => item.Id == plan.AutomationTriggerId);
        var firedAtUtc = new DateTimeOffset(2026, 5, 12, 9, 5, 0, TimeSpan.Zero);
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
            "scheduler-planner-no-messages-key",
            firedAtUtc);

        var first = await handler.HandleAsync(JsonSerializer.Serialize(request, JsonOptions), context, CancellationToken.None);
        var second = await handler.HandleAsync(JsonSerializer.Serialize(request, JsonOptions), context, CancellationToken.None);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var run = await verificationContext.Set<SchedulerPlanRun>().SingleAsync(item => item.PlanId == plan.Id);
        var updatedPlan = await verificationContext.Set<SchedulerPlan>().SingleAsync(item => item.Id == plan.Id);

        Assert.Equal(AutomationDeliveryAttemptOutcome.Completed, first.Outcome);
        Assert.Equal(AutomationDeliveryAttemptOutcome.Completed, second.Outcome);
        Assert.Single(launcher.Calls);
        Assert.Equal(SchedulerPlanRunDispatchStatus.NoMessages, run.Status);
        Assert.Equal(1, run.AttemptCount);
        Assert.Equal(noMessageResult.TargetRunId, run.TargetRunId);
        Assert.Equal(noMessageResult.Summary, run.Summary);
        Assert.Equal(SchedulerPlanRunRoutes.NoMessages, run.Route);
        Assert.Equal(SchedulerPlanRunRetryCategory.NoAction, run.RetryCategory);
        Assert.Equal(string.Empty, run.ErrorMessage);
        Assert.Equal("Previous Microsoft Graph outage.", updatedPlan.LastError);
    }

    [Fact]
    public async Task Trigger_fire_handler_records_waiting_for_approval_without_retry()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("scheduler-planner-waiting-approval");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        var waitingResult = new SchedulerTargetLaunchResult(
            SchedulerPlanTargetKind.Workflow,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            WorkflowRunState.WaitingForInput.ToString(),
            "Workflow is waiting for approval at node 'mark-office365-message'.",
            SchedulerPlanRunDispatchStatus.WaitingForApproval,
            SchedulerPlanRunRoutes.WaitingForApproval,
            SchedulerPlanRunRetryCategory.WorkflowWaitingForApproval);
        var launcher = new RecordingSchedulerTargetLauncher(waitingResult);
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
        var catalogService = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
        var planner = scope.ServiceProvider.GetRequiredService<ISchedulerPlannerService>();
        var definition = await CreateWorkflowTargetAsync(catalogService);
        var planSummary = await planner.SavePlanAsync(new SchedulerPlanEditorModel
        {
            Name = "Approval waiting workflow launch",
            TargetKind = SchedulerPlanTargetKind.Workflow,
            TargetId = definition.Id.Value,
            TargetVersionId = definition.VersionId.Value,
            CronExpression = "0 0/5 * * * ?",
            TimeZoneId = "UTC",
            MisfirePolicy = AutomationTriggerMisfirePolicy.FireOnceNow,
            InputJson = "{}",
            IsEnabled = true
        });

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var plan = await dbContext.Set<SchedulerPlan>().SingleAsync(item => item.Id == planSummary.Id);
        plan.LastError = "Previous project write failure.";
        await dbContext.SaveChangesAsync();
        var trigger = await dbContext.Set<AutomationTriggerRecord>().SingleAsync(item => item.Id == plan.AutomationTriggerId);
        var firedAtUtc = new DateTimeOffset(2026, 5, 12, 9, 10, 0, TimeSpan.Zero);
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
            "scheduler-planner-waiting-approval-key",
            firedAtUtc);

        var first = await handler.HandleAsync(JsonSerializer.Serialize(request, JsonOptions), context, CancellationToken.None);
        var second = await handler.HandleAsync(JsonSerializer.Serialize(request, JsonOptions), context, CancellationToken.None);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var run = await verificationContext.Set<SchedulerPlanRun>().SingleAsync(item => item.PlanId == plan.Id);
        var updatedPlan = await verificationContext.Set<SchedulerPlan>().SingleAsync(item => item.Id == plan.Id);

        Assert.Equal(AutomationDeliveryAttemptOutcome.Completed, first.Outcome);
        Assert.Equal(AutomationDeliveryAttemptOutcome.Completed, second.Outcome);
        Assert.Single(launcher.Calls);
        Assert.Equal(SchedulerPlanRunDispatchStatus.WaitingForApproval, run.Status);
        Assert.Equal(1, run.AttemptCount);
        Assert.Equal(waitingResult.TargetRunId, run.TargetRunId);
        Assert.Equal(SchedulerPlanRunRoutes.WaitingForApproval, run.Route);
        Assert.Equal(SchedulerPlanRunRetryCategory.WorkflowWaitingForApproval, run.RetryCategory);
        Assert.Equal(string.Empty, run.ErrorMessage);
        Assert.Equal("Previous project write failure.", updatedPlan.LastError);
    }

    [Fact]
    public async Task Trigger_fire_handler_classifies_graph_failures_for_retry()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("scheduler-planner-graph-failure");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        var launcher = new RecordingSchedulerTargetLauncher(
            failure: new HttpRequestException("Microsoft Graph request failed while listing Office365 messages."));
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
        var targetId = await SeedProcessTargetAsync(dbContextFactory, "Graph failure scheduled process");
        var planSummary = await planner.SavePlanAsync(new SchedulerPlanEditorModel
        {
            Name = "Graph failure launch",
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
        var firedAtUtc = new DateTimeOffset(2026, 5, 12, 9, 20, 0, TimeSpan.Zero);
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
            "scheduler-planner-graph-failure-key",
            firedAtUtc);

        var result = await handler.HandleAsync(JsonSerializer.Serialize(request, JsonOptions), context, CancellationToken.None);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var run = await verificationContext.Set<SchedulerPlanRun>().SingleAsync(item => item.PlanId == plan.Id);
        var updatedPlan = await verificationContext.Set<SchedulerPlan>().SingleAsync(item => item.Id == plan.Id);

        Assert.Equal(AutomationDeliveryAttemptOutcome.RetryScheduled, result.Outcome);
        Assert.Single(launcher.Calls);
        Assert.Equal(SchedulerPlanRunDispatchStatus.Failed, run.Status);
        Assert.Equal(SchedulerPlanRunRoutes.Failed, run.Route);
        Assert.Equal(SchedulerPlanRunRetryCategory.TransientExternalFailure, run.RetryCategory);
        Assert.Contains("Microsoft Graph", run.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(run.ErrorMessage, updatedPlan.LastError);
    }

    [Fact]
    public async Task Trigger_fire_handler_classifies_project_write_failures_for_retry()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("scheduler-planner-project-write-failure");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        var launcher = new RecordingSchedulerTargetLauncher(
            failure: new InvalidOperationException("Project structure write failed while creating scheduler task nodes."));
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
        var targetId = await SeedProcessTargetAsync(dbContextFactory, "Project write failure scheduled process");
        var planSummary = await planner.SavePlanAsync(new SchedulerPlanEditorModel
        {
            Name = "Project write failure launch",
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
        var firedAtUtc = new DateTimeOffset(2026, 5, 12, 9, 25, 0, TimeSpan.Zero);
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
            "scheduler-planner-project-write-failure-key",
            firedAtUtc);

        var result = await handler.HandleAsync(JsonSerializer.Serialize(request, JsonOptions), context, CancellationToken.None);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var run = await verificationContext.Set<SchedulerPlanRun>().SingleAsync(item => item.PlanId == plan.Id);
        var updatedPlan = await verificationContext.Set<SchedulerPlan>().SingleAsync(item => item.Id == plan.Id);

        Assert.Equal(AutomationDeliveryAttemptOutcome.RetryScheduled, result.Outcome);
        Assert.Single(launcher.Calls);
        Assert.Equal(SchedulerPlanRunDispatchStatus.Failed, run.Status);
        Assert.Equal(SchedulerPlanRunRoutes.Failed, run.Route);
        Assert.Equal(SchedulerPlanRunRetryCategory.ProjectWriteFailure, run.RetryCategory);
        Assert.Contains("Project structure", run.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(run.ErrorMessage, updatedPlan.LastError);
    }

    [Fact]
    public async Task Concurrent_trigger_fire_handlers_do_not_double_launch_the_same_dedupe_key()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("scheduler-planner-fire-concurrent");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
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
        var targetId = await SeedProcessTargetAsync(dbContextFactory, "Concurrent deduped scheduled process");
        var planSummary = await planner.SavePlanAsync(new SchedulerPlanEditorModel
        {
            Name = "Concurrent deduped process launch",
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
        var firedAtUtc = new DateTimeOffset(2026, 5, 12, 9, 15, 0, TimeSpan.Zero);
        var requestJson = JsonSerializer.Serialize(
            new AutomationTriggerFireRequest(
                trigger.Id,
                trigger.TriggerKey,
                trigger.OwnerKey,
                trigger.OwnerKind,
                trigger.PayloadJson,
                firedAtUtc),
            JsonOptions);
        var handler = scope.ServiceProvider.GetServices<IAutomationMessageHandler>()
            .Single(item => item is SchedulerPlannerTriggerFireHandler);
        var context = new AutomationMessageContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AutomationEnvelopeTypeNames.For<AutomationTriggerFireRequest>(),
            Guid.NewGuid(),
            null,
            "scheduler-planner-concurrent-dedupe-key",
            firedAtUtc);

        var results = await Task.WhenAll(
            handler.HandleAsync(requestJson, context, CancellationToken.None),
            handler.HandleAsync(requestJson, context, CancellationToken.None));

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var run = await verificationContext.Set<SchedulerPlanRun>().SingleAsync(item => item.PlanId == plan.Id);

        Assert.Contains(results, result => result.Outcome == AutomationDeliveryAttemptOutcome.Completed);
        Assert.Single(launcher.Calls);
        Assert.Equal(SchedulerPlanRunDispatchStatus.Dispatched, run.Status);
        Assert.Equal(1, run.AttemptCount);
    }

    [Fact]
    public async Task Target_launcher_starts_real_process_run()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("scheduler-planner-process-launch");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
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
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
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

    [Fact]
    public async Task Target_launcher_records_no_messages_for_completed_workflow_output()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("scheduler-planner-workflow-no-messages");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await BuildProviderAsync(profile);

        await using var scope = provider.CreateAsyncScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
        var launcher = scope.ServiceProvider.GetRequiredService<ISchedulerTargetLauncher>();
        var definition = await CreateNoMessageWorkflowTargetAsync(catalogService);
        var plan = CreatePlan(
            SchedulerPlanTargetKind.Workflow,
            definition.Id.Value,
            definition.VersionId.Value,
            "No-message workflow launch proof");
        plan.InputJson = """
            {
              "route": "no_messages",
              "noMessages": true,
              "summary": "No unprocessed Office365 email matched the configured address."
            }
            """;

        var result = await launcher.LaunchAsync(
            plan,
            new DateTimeOffset(2026, 5, 12, 10, 45, 0, TimeSpan.Zero));

        Assert.Equal(SchedulerPlanTargetKind.Workflow, result.TargetKind);
        Assert.Equal(SchedulerPlanRunDispatchStatus.NoMessages, result.DispatchStatus);
        Assert.Equal(SchedulerPlanRunDispatchStatus.NoMessages.ToString(), result.State);
        Assert.Equal("No unprocessed Office365 email matched the configured address.", result.Summary);
        Assert.Equal(SchedulerPlanRunRoutes.NoMessages, result.Route);
        Assert.Equal(SchedulerPlanRunRetryCategory.NoAction, result.RetryCategory);
    }

    [Fact]
    public async Task Target_launcher_waits_for_approval_when_workflow_requests_human_approval()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("scheduler-planner-workflow-approval");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await BuildProviderAsync(profile);

        await using var scope = provider.CreateAsyncScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
        var runStore = scope.ServiceProvider.GetRequiredService<IWorkflowRunStore>();
        var launcher = scope.ServiceProvider.GetRequiredService<ISchedulerTargetLauncher>();
        var definition = await CreateApprovalWorkflowTargetAsync(catalogService);
        var plan = CreatePlan(
            SchedulerPlanTargetKind.Workflow,
            definition.Id.Value,
            definition.VersionId.Value,
            "Approval waiting proof");
        plan.InputJson = """
            {
              "messageId": "message-1",
              "processedCategory": "CanDoItAllProcessed"
            }
            """;

        var result = await launcher.LaunchAsync(
            plan,
            new DateTimeOffset(2026, 5, 12, 10, 55, 0, TimeSpan.Zero));

        var workflowRun = await runStore.GetRunAsync(new WorkflowRunId(result.TargetRunId));
        var pendingRequests = await runStore.ListPendingExternalRequestsAsync(new WorkflowRunId(result.TargetRunId));

        Assert.NotNull(workflowRun);
        Assert.Equal(WorkflowRunState.WaitingForInput, workflowRun.State);
        Assert.Equal(SchedulerPlanRunDispatchStatus.WaitingForApproval, result.DispatchStatus);
        Assert.Equal(SchedulerPlanRunRoutes.WaitingForApproval, result.Route);
        Assert.Equal(SchedulerPlanRunRetryCategory.WorkflowWaitingForApproval, result.RetryCategory);
        Assert.Single(pendingRequests);
        Assert.Equal(WorkflowExternalRequestKind.Approval, pendingRequests[0].Kind);
        Assert.Contains("Approve scheduled Office365 processed marker?", pendingRequests[0].RequestJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Office365_mark_processed_executor_requires_approval_before_external_effect()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("scheduler-planner-office365-policy");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await BuildProviderAsync(profile);

        await using var scope = provider.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<PluginCatalogService>();
        var plugins = await catalog.ListCatalogAsync();
        var plugin = Assert.Single(plugins, item => item.PluginId == Office365PluginConstants.PluginId);
        var executor = Assert.Single(
            plugin.Descriptor.WorkflowExecutors,
            item => item.ExecutorId == Office365PluginConstants.MarkProcessedExecutorId);

        Assert.True(executor.PermissionPolicy.RequiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.WritesExternalData));
        Assert.True(executor.PermissionPolicy.RequiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.UsesNetwork));
        Assert.True(executor.PermissionPolicy.RequiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.UsesSecrets));
        Assert.Equal(
            WorkflowExecutorApprovalRequirement.RequiredForExternalEffect,
            executor.PermissionPolicy.ApprovalRequirement);
    }

    [Fact]
    public async Task Workflow_input_schema_service_resolves_descriptors_defaults_and_raw_json_fallback()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("scheduler-workflow-schema");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await BuildProviderAsync(profile);

        await using var scope = provider.CreateAsyncScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
        var schemaService = scope.ServiceProvider.GetRequiredService<ISchedulerWorkflowInputSchemaService>();
        var definition = await CreateWorkflowTargetAsync(catalogService, CreateSchedulerWorkflowInputParameters());
        var rawDefinition = await CreateWorkflowTargetAsync(catalogService);
        var projectId = Guid.NewGuid();

        var schema = await schemaService.ResolveSchemaAsync(definition.Id, definition.VersionId);
        var validation = await schemaService.ValidateInputAsync(
            definition.Id,
            definition.VersionId,
            $$"""
            {
              "emailAddress": "sender@example.com",
              "projectId": "{{projectId:D}}",
              "nodeId": "node-1"
            }
            """);
        var rawValidation = await schemaService.ValidateInputAsync(rawDefinition.Id, rawDefinition.VersionId, "[1,2,3]");

        Assert.False(schema.UsesRawJsonFallback);
        Assert.Contains(schema.Parameters, parameter => parameter.Key == "emailAddress" && parameter.Kind == WorkflowInputParameterKind.EmailAddress);
        Assert.True(validation.Succeeded, string.Join(" ", validation.Issues.Select(issue => issue.Message)));
        using (var document = JsonDocument.Parse(validation.NormalizedInputJson))
        {
            Assert.Equal("CanDoItAllProcessed", document.RootElement.GetProperty("processedCategory").GetString());
            Assert.Equal(336, document.RootElement.GetProperty("lookbackHours").GetInt32());
        }

        Assert.True(rawValidation.Succeeded);
        Assert.Equal("[1,2,3]", rawValidation.NormalizedInputJson);
    }

    [Fact]
    public async Task SavePlanAsync_rejects_missing_required_workflow_input_parameters()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("scheduler-workflow-schema-invalid");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await BuildProviderAsync(profile);

        await using var scope = provider.CreateAsyncScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
        var planner = scope.ServiceProvider.GetRequiredService<ISchedulerPlannerService>();
        var definition = await CreateWorkflowTargetAsync(catalogService, CreateSchedulerWorkflowInputParameters());
        var projectId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => planner.SavePlanAsync(new SchedulerPlanEditorModel
        {
            Name = "Invalid workflow schedule",
            TargetKind = SchedulerPlanTargetKind.Workflow,
            TargetId = definition.Id.Value,
            TargetVersionId = definition.VersionId.Value,
            CronExpression = "0 0 9 ? * MON-FRI",
            TimeZoneId = "UTC",
            MisfirePolicy = AutomationTriggerMisfirePolicy.FireOnceNow,
            InputJson = $$"""
            {
              "projectId": "{{projectId:D}}",
              "nodeId": "node-1"
            }
            """,
            IsEnabled = true
        }));

        Assert.Contains("Scheduler workflow input is invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("emailAddress", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SavePlanAsync_persists_normalized_workflow_input_defaults()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("scheduler-workflow-schema-save");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await BuildProviderAsync(profile);

        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var catalogService = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
        var planner = scope.ServiceProvider.GetRequiredService<ISchedulerPlannerService>();
        var definition = await CreateWorkflowTargetAsync(catalogService, CreateSchedulerWorkflowInputParameters());
        var projectId = Guid.NewGuid();

        var summary = await planner.SavePlanAsync(new SchedulerPlanEditorModel
        {
            Name = "Valid workflow schedule",
            TargetKind = SchedulerPlanTargetKind.Workflow,
            TargetId = definition.Id.Value,
            TargetVersionId = definition.VersionId.Value,
            CronExpression = "0 0 9 ? * MON-FRI",
            TimeZoneId = "UTC",
            MisfirePolicy = AutomationTriggerMisfirePolicy.FireOnceNow,
            InputJson = $$"""
            {
              "emailAddress": "sender@example.com",
              "projectId": "{{projectId:D}}",
              "nodeId": "node-1"
            }
            """,
            IsEnabled = true
        });

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var plan = await dbContext.Set<SchedulerPlan>().SingleAsync(item => item.Id == summary.Id);
        using var input = JsonDocument.Parse(plan.InputJson);
        Assert.Equal("CanDoItAllProcessed", input.RootElement.GetProperty("processedCategory").GetString());
        Assert.Equal(336, input.RootElement.GetProperty("lookbackHours").GetInt32());
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

    private static Task<WorkflowDefinition> CreateWorkflowTargetAsync(
        IWorkflowCatalogService catalogService,
        IReadOnlyList<WorkflowInputParameterDescriptor>? inputParameters = null)
    {
        var start = new WorkflowNodeId("start");
        var end = new WorkflowNodeId("end");
        var request = new WorkflowDefinitionSaveRequest(
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
                ExposeAzureFunctionsMcpTool: false))
        {
            InputParameters = inputParameters ?? []
        };

        return catalogService.SaveDefinitionAsync(request);
    }

    private static Task<WorkflowDefinition> CreateNoMessageWorkflowTargetAsync(IWorkflowCatalogService catalogService)
    {
        var start = new WorkflowNodeId("start");
        var transform = new WorkflowNodeId("no-message-output");
        var end = new WorkflowNodeId("end");
        var jsonShape = CreateJsonShape();
        var request = new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: "No-message scheduled workflow",
            Description: "Workflow definition used to verify scheduler no-message detection.",
            WorkflowLifecycleStatus.Active,
            new WorkflowGraph(
                start,
                [
                    CreateWorkflowNode(start, WorkflowNodeKind.Start, resultShape: jsonShape),
                    CreateJsonTransformWorkflowNode(transform),
                    CreateWorkflowNode(end, WorkflowNodeKind.End, inputShape: jsonShape)
                ],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-to-no-message-output"),
                        start,
                        SourcePortId: null,
                        transform,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty),
                    new WorkflowEdge(
                        new WorkflowEdgeId("no-message-output-to-end"),
                        transform,
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
                ExposeAzureFunctionsMcpTool: false));

        return catalogService.SaveDefinitionAsync(request);
    }

    private static Task<WorkflowDefinition> CreateApprovalWorkflowTargetAsync(IWorkflowCatalogService catalogService)
    {
        var start = new WorkflowNodeId("start");
        var approval = new WorkflowNodeId("approve-office365-message");
        var end = new WorkflowNodeId("end");
        var jsonShape = CreateJsonShape();
        var request = new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: "Scheduler approval waiting workflow",
            Description: "Workflow definition used to verify scheduler waits for workflow approval.",
            WorkflowLifecycleStatus.Active,
            new WorkflowGraph(
                start,
                [
                    CreateWorkflowNode(start, WorkflowNodeKind.Start, resultShape: jsonShape),
                    CreateWorkflowExecutorNode(
                        approval,
                        WorkflowExecutorIds.ApprovalRequest,
                        JsonSerializer.Serialize(
                            new WorkflowApprovalExecutorSettings
                            {
                                Prompt = "Approve scheduled Office365 processed marker?",
                                IncludeInputPayload = true
                            },
                            JsonOptions),
                        jsonShape,
                        jsonShape),
                    CreateWorkflowNode(end, WorkflowNodeKind.End, inputShape: jsonShape)
                ],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-to-approval"),
                        start,
                        SourcePortId: null,
                        approval,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty),
                    new WorkflowEdge(
                        new WorkflowEdgeId("approval-to-end"),
                        approval,
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
                ExposeAzureFunctionsMcpTool: false));

        return catalogService.SaveDefinitionAsync(request);
    }

    private static IReadOnlyList<WorkflowInputParameterDescriptor> CreateSchedulerWorkflowInputParameters()
    {
        return
        [
            new WorkflowInputParameterDescriptor(
                "emailAddress",
                "Watched email address",
                WorkflowInputParameterKind.EmailAddress,
                IsRequired: true,
                "Sender address.",
                "$.emailAddress",
                DefaultValue: string.Empty,
                new WorkflowInputParameterOptionSource(
                    WorkflowInputParameterOptionSourceKind.CrmContacts,
                    DependsOnParameterKey: string.Empty,
                    StaticOptions: []),
                MinimumValue: null,
                MaximumValue: null,
                Placeholder: string.Empty),
            new WorkflowInputParameterDescriptor(
                "projectId",
                "Project",
                WorkflowInputParameterKind.ProjectId,
                IsRequired: true,
                "Target project.",
                "$.projectId",
                DefaultValue: string.Empty,
                new WorkflowInputParameterOptionSource(
                    WorkflowInputParameterOptionSourceKind.ProjectStructureProjects,
                    DependsOnParameterKey: string.Empty,
                    StaticOptions: []),
                MinimumValue: null,
                MaximumValue: null,
                Placeholder: string.Empty),
            new WorkflowInputParameterDescriptor(
                "nodeId",
                "Parent node",
                WorkflowInputParameterKind.ProjectNodeId,
                IsRequired: true,
                "Target node.",
                "$.nodeId",
                DefaultValue: string.Empty,
                new WorkflowInputParameterOptionSource(
                    WorkflowInputParameterOptionSourceKind.ProjectStructureNodes,
                    DependsOnParameterKey: "projectId",
                    StaticOptions: []),
                MinimumValue: null,
                MaximumValue: null,
                Placeholder: string.Empty),
            new WorkflowInputParameterDescriptor(
                "processedCategory",
                "Processed category",
                WorkflowInputParameterKind.Category,
                IsRequired: false,
                "Processed marker.",
                "$.processedCategory",
                DefaultValue: "CanDoItAllProcessed",
                WorkflowInputParameterOptionSource.None,
                MinimumValue: null,
                MaximumValue: null,
                Placeholder: string.Empty),
            new WorkflowInputParameterDescriptor(
                "lookbackHours",
                "Lookback hours",
                WorkflowInputParameterKind.Integer,
                IsRequired: false,
                "Polling lookback.",
                "$.lookbackHours",
                DefaultValue: "336",
                WorkflowInputParameterOptionSource.None,
                MinimumValue: 1,
                MaximumValue: 720,
                Placeholder: string.Empty)
        ];
    }

    private static WorkflowNode CreateJsonTransformWorkflowNode(WorkflowNodeId id)
    {
        var jsonShape = CreateJsonShape();
        return new WorkflowNode(
            id,
            WorkflowNodeKind.Executor,
            id.Value,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: jsonShape,
                ResultShape: jsonShape) with
            {
                ExecutorId = WorkflowExecutorIds.JsonTransform,
                ExecutorSettingsJson = JsonSerializer.Serialize(
                    new WorkflowJsonTransformExecutorSettings
                    {
                        Operations =
                        [
                            new WorkflowJsonTransformStep
                            {
                                Operation = WorkflowJsonTransformOperation.Select,
                                Path = "$"
                            }
                        ]
                    },
                    JsonOptions),
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
            });
    }

    private static WorkflowNode CreateWorkflowExecutorNode(
        WorkflowNodeId id,
        WorkflowExecutorId executorId,
        string settingsJson,
        WorkflowValueShape inputShape,
        WorkflowValueShape resultShape)
    {
        return new WorkflowNode(
            id,
            WorkflowNodeKind.Executor,
            id.Value,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: inputShape,
                ResultShape: resultShape)
            {
                ExecutorId = executorId,
                ExecutorSettingsJson = settingsJson,
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
            });
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

    private static WorkflowValueShape CreateJsonShape()
        => new(WorkflowValueShapeKind.Json, "{}", "JSON payload");

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

    private sealed class RecordingSchedulerTargetLauncher(
        SchedulerTargetLaunchResult? launchResult = null,
        Exception? failure = null) : ISchedulerTargetLauncher
    {
        public ConcurrentQueue<(Guid PlanId, DateTimeOffset FiredAtUtc)> Calls { get; } = [];

        public Task<SchedulerTargetLaunchResult> LaunchAsync(
            SchedulerPlan plan,
            DateTimeOffset firedAtUtc,
            CancellationToken cancellationToken = default)
        {
            Calls.Enqueue((plan.Id, firedAtUtc));
            if (failure is not null)
            {
                return Task.FromException<SchedulerTargetLaunchResult>(failure);
            }

            return Task.FromResult(launchResult ?? new SchedulerTargetLaunchResult(
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
