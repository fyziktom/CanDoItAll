using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed partial class SchedulerWorkspaceOwnerPersistenceTests {
    [Fact]
    public async Task Owner_models_match_all_canonical_mappings_and_cascades_exclude_foreign_entities() {
        await using var application = await TestApplication.CreateAsync();
        await using var canonical = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await using var scheduler = await application.Services.GetRequiredService<IDbContextFactory<SchedulerPlannerDbContext>>().CreateDbContextAsync();
        await using var connector = await application.Services.GetRequiredService<IDbContextFactory<WorkspaceConnectorCommandDbContext>>().CreateDbContextAsync();
        AssertModel(canonical, scheduler);
        AssertModel(canonical, connector);
        Assert.Throws<InvalidOperationException>(() => scheduler.Set<ConnectorCommandRecord>().ToQueryString());
        Assert.Throws<InvalidOperationException>(() => connector.Set<SchedulerPlan>().ToQueryString());
        Assert.Throws<InvalidOperationException>(() => connector.Set<WorkspaceSettings>().ToQueryString());
        var plan = CreatePlan();
        var run = new SchedulerPlanRun { PlanId = plan.Id, DedupeKey = $"cascade-{Guid.NewGuid():N}" };
        var command = new ConnectorCommandRecord { ProjectId = Guid.NewGuid(), ConnectorPluginKey = "legacy", CommandKey = "send", IdempotencyKey = "cascade" };
        var audit = new ConnectorCommandAuditRecord { ConnectorCommandId = command.Id, ProjectId = command.ProjectId, Actor = "legacy", Message = "cascade" };
        canonical.AddRange(plan, run, command, audit);
        await canonical.SaveChangesAsync();
        scheduler.Remove(await scheduler.Set<SchedulerPlan>().SingleAsync(item => item.Id == plan.Id));
        connector.Remove(await connector.Set<ConnectorCommandRecord>().SingleAsync(item => item.Id == command.Id));
        await scheduler.SaveChangesAsync();
        await connector.SaveChangesAsync();
        Assert.False(await canonical.Set<SchedulerPlanRun>().AsNoTracking().AnyAsync(item => item.Id == run.Id));
        Assert.False(await canonical.Set<ConnectorCommandAuditRecord>().AsNoTracking().AnyAsync(item => item.Id == audit.Id));
    }

    [Fact]
    public async Task Legacy_owner_records_keep_payloads_claim_fields_and_history_after_restart_with_profile_isolation() {
        await using var environment = CanDoItAllTestEnvironment.Create("scheduler-workspace-owner-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        var otherProfile = environment.CreatePostgreSqlProfile("other");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        var now = new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);
        var plan = CreatePlan();
        plan.Description = "Legacy DST and schedule policy";
        plan.IsEnabled = false;
        plan.StartAtUtc = now;
        plan.EndAtUtc = now.AddDays(2);
        plan.InputJson = """{"projectId":"legacy-context","nested":{"keep":"unchanged"}}""";
        plan.MisfirePolicy = SchedulerPlanMisfirePolicy.DoNothing;
        plan.LastFiredAtUtc = now;
        plan.LastError = "Retained warning";
        plan.CreatedAtUtc = now.AddDays(-1);
        plan.UpdatedAtUtc = now;
        var run = new SchedulerPlanRun {
            PlanId = plan.Id, DedupeKey = "legacy-exact-fire-key", SchedulerFireId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(),
            FiredAtUtc = now, Status = SchedulerPlanRunDispatchStatus.WaitingForApproval, AttemptCount = 3,
            TargetRunId = Guid.NewGuid(), TargetRunKind = "Workflow", Summary = "Approval required", ErrorMessage = "Retained diagnostic",
            Route = SchedulerPlanRunRoutes.WaitingForApproval, RetryCategory = SchedulerPlanRunRetryCategory.WorkflowWaitingForApproval,
            DispatchedAtUtc = now, CreatedAtUtc = now.AddMinutes(-1), UpdatedAtUtc = now
        };
        var command = new ConnectorCommandRecord {
            ProjectId = Guid.NewGuid(), ConnectorPluginKey = "legacy-webhook", CommandKey = "deliver", IdempotencyKey = "legacy-idempotency",
            PayloadJson = """{"endpoint":"legacy","unknown":{"preserved":true}}""", Status = ConnectorCommandStatus.Completed,
            ApprovalState = ConnectorCommandApprovalState.Approved, AttemptCount = 4, LastAttemptAtUtc = now.AddMinutes(-1),
            NextAttemptAtUtc = now.AddMinutes(1), CompletedAtUtc = now, LastError = "Historical diagnostic", ResultJson = """{"receipt":"original"}""",
            LeaseToken = "legacy-terminal-lease", LeaseExpiresAtUtc = now.AddMinutes(2), RequestedBy = "legacy-operator",
            CreatedAtUtc = now.AddHours(-2), UpdatedAtUtc = now
        };
        var audit = new ConnectorCommandAuditRecord {
            ConnectorCommandId = command.Id, ProjectId = command.ProjectId, EventKind = ConnectorCommandAuditEventKind.Completed,
            Actor = "legacy-operator", Message = "Original delivery completed", DetailsJson = """{"attempt":4,"original":true}""", CreatedAtUtc = now
        };
        await using (var original = await TestApplication.CreateAsync(harness)) {
            await using var canonical = await original.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            canonical.AddRange(plan, run, command, audit);
            await canonical.SaveChangesAsync();
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var scheduler = await restarted.Services.GetRequiredService<IDbContextFactory<SchedulerPlannerDbContext>>().CreateDbContextAsync();
        await using var connector = await restarted.Services.GetRequiredService<IDbContextFactory<WorkspaceConnectorCommandDbContext>>().CreateDbContextAsync();
        Assert.Equal(JsonSerializer.Serialize(plan), JsonSerializer.Serialize(await scheduler.Set<SchedulerPlan>().SingleAsync(item => item.Id == plan.Id)));
        Assert.Equal(JsonSerializer.Serialize(run), JsonSerializer.Serialize(await scheduler.Set<SchedulerPlanRun>().SingleAsync(item => item.Id == run.Id)));
        Assert.Equal(JsonSerializer.Serialize(command), JsonSerializer.Serialize(await connector.Set<ConnectorCommandRecord>().SingleAsync(item => item.Id == command.Id)));
        Assert.Equal(JsonSerializer.Serialize(audit), JsonSerializer.Serialize(await connector.Set<ConnectorCommandAuditRecord>().SingleAsync(item => item.Id == audit.Id)));
        await using var scope = restarted.Services.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<ConnectorOutboxService>();
        var snapshot = Assert.IsType<ConnectorCommandSnapshot>(await outbox.GetAsync(command.Id));
        Assert.Equal(command.IdempotencyKey, snapshot.IdempotencyKey);
        Assert.Equal(command.ResultJson, snapshot.ResultJson);
        Assert.Equal(audit.DetailsJson, Assert.Single(await outbox.ListAuditAsync(command.Id)).DetailsJson);
        await using var other = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = otherProfile });
        await using var otherScheduler = await other.Services.GetRequiredService<IDbContextFactory<SchedulerPlannerDbContext>>().CreateDbContextAsync();
        await using var otherConnector = await other.Services.GetRequiredService<IDbContextFactory<WorkspaceConnectorCommandDbContext>>().CreateDbContextAsync();
        Assert.False(await otherScheduler.Set<SchedulerPlan>().AnyAsync(item => item.Id == plan.Id));
        Assert.False(await otherConnector.Set<ConnectorCommandRecord>().AnyAsync(item => item.Id == command.Id));
    }

    [Fact]
    public async Task Scheduler_dispatch_reuses_terminal_firing_after_restart_and_keeps_launch_correlation() {
        await using var environment = CanDoItAllTestEnvironment.Create("scheduler-owner-dispatch-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        var launcher = new RecordingLauncher();
        var harness = new TestHarnessOptions {
            TestEnvironment = environment, ActiveProfile = profile,
            ConfigureServices = services => services.AddSingleton<ISchedulerTargetLauncher>(launcher)
        };
        var plan = CreatePlan();
        var firedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var request = new SchedulerPlanFireRequest(plan.Id, Guid.NewGuid(), Guid.NewGuid(), firedAt, firedAt.AddMinutes(30));
        Guid runId;
        await using (var original = await TestApplication.CreateAsync(harness)) {
            await using (var canonical = await original.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync()) {
                canonical.Add(plan);
                await canonical.SaveChangesAsync();
            }
            await using var scope = original.Services.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<ISchedulerPlannerRunDispatcher>();
            await dispatcher.DispatchAsync(request);
            await dispatcher.DispatchAsync(request);
            var launch = Assert.Single(launcher.Requests);
            Assert.Equal(request.PlanId, launch.PlanId);
            Assert.Equal(request.SchedulerFireId, launch.SchedulerFireId.Value);
            Assert.Equal(request.FiredAtUtc, launch.FiredAtUtc);
            Assert.Equal(request.CorrelationId!.Value.ToString("D"), launch.CorrelationId.Value);
            runId = launch.PlanRunId;
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        await restartedScope.ServiceProvider.GetRequiredService<ISchedulerPlannerRunDispatcher>().DispatchAsync(request);
        Assert.Single(launcher.Requests);
        await using var readback = await restarted.Services.GetRequiredService<IDbContextFactory<SchedulerPlannerDbContext>>().CreateDbContextAsync();
        var savedRun = await readback.Set<SchedulerPlanRun>().SingleAsync(item => item.PlanId == plan.Id);
        Assert.Equal(runId, savedRun.Id);
        Assert.Equal(SchedulerPlanRunDispatchStatus.Dispatched, savedRun.Status);
        Assert.Equal(launcher.TargetRunId, savedRun.TargetRunId);
        Assert.Equal(1, savedRun.AttemptCount);
        Assert.Equal(SchedulerPlanRunRoutes.Processed, savedRun.Route);
        Assert.Equal(request.SchedulerFireId, savedRun.SchedulerFireId);
        Assert.Equal(request.CorrelationId, savedRun.CorrelationId);
        var savedPlan = await readback.Set<SchedulerPlan>().SingleAsync(item => item.Id == plan.Id);
        Assert.Equal(request.FiredAtUtc, savedPlan.LastFiredAtUtc);
        Assert.Equal(request.NextPlannedFireAtUtc, savedPlan.NextPlannedFireAtUtc);
    }

    private static SchedulerPlan CreatePlan() => new() {
        Name = "Owner persisted schedule", Description = "Retained description", TargetKind = SchedulerPlanTargetKind.Workflow,
        TargetId = Guid.NewGuid(), TargetVersionId = Guid.NewGuid(), TargetNameSnapshot = "Workflow snapshot", CronExpression = "0 0/30 * * * ?",
        CronDescription = "Every thirty minutes", TimeZoneId = "UTC", SchedulerTriggerId = Guid.NewGuid(), SchedulerTriggerKey = "legacy-trigger-key"
    };

    private static void AssertModel(AppDbContext canonical, DbContext owner) {
        var entities = owner.GetService<IDesignTimeModel>().Model.GetEntityTypes().ToArray();
        Assert.Equal(owner is SchedulerPlannerDbContext ? 3 : 2, entities.Length);
        foreach (var entity in entities) {
            var complete = Assert.IsAssignableFrom<IEntityType>(canonical.GetService<IDesignTimeModel>().Model.FindEntityType(entity.ClrType));
            Assert.Equal(complete.ToDebugString(MetadataDebugStringOptions.LongDefault), entity.ToDebugString(MetadataDebugStringOptions.LongDefault));
        }
    }

    private sealed class RecordingLauncher : ISchedulerTargetLauncher {
        public Guid TargetRunId { get; private set; }
        public List<SchedulerTargetLaunchContext> Requests { get; } = [];
        public Task<SchedulerTargetLaunchResult> LaunchAsync(SchedulerPlan plan, SchedulerTargetLaunchContext context,
            CancellationToken cancellationToken = default) {
            Requests.Add(context);
            TargetRunId = context.PreparedRunId!.Value.Value;
            return Task.FromResult(new SchedulerTargetLaunchResult(plan.TargetKind, TargetRunId, "Completed", "Persisted target receipt"));
        }
    }
}
