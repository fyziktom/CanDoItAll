using System.Data.Common;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.SchedulerPlanner.Pages;
using CanDoItAll.Modules.SchedulerPlanner.Presentation;
using CanDoItAll.SchedulerPlanner.UI;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "HostPlatform")]
public sealed class SchedulerUiOwnerReceiptTests {
    public enum Fault { Projection, Reload, Logging, LostCommitAcknowledgement }

    [Theory]
    [InlineData(Fault.Projection)]
    [InlineData(Fault.Reload)]
    [InlineData(Fault.Logging)]
    [InlineData(Fault.LostCommitAcknowledgement)]
    public async Task Real_persistence_survives_followup_faults_and_receipt_controls_replay(Fault fault) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var target = await SeedWorkflowAsync(services);
        var interceptor = new FaultInterceptor();
        var options = new DbContextOptionsBuilder<SchedulerPlannerDbContext>(services.GetRequiredService<DbContextOptions<SchedulerPlannerDbContext>>())
            .AddInterceptors(interceptor, new ReadFault(interceptor), new CommitFault(interceptor)).Options;
        var factory = new OwnedFactory(options);
        var projection = new ProjectionProbe(services.GetRequiredService<ISchedulerPlannerTriggerScheduler>(), interceptor) { Mode = fault };
        var logger = new FaultLogger { Armed = fault == Fault.Logging };
        var owner = ActivatorUtilities.CreateInstance<SchedulerPlannerService>(services, factory, projection, logger);
        var session = ActivatorUtilities.CreateInstance<SchedulerWorkspaceSession>(services, owner);
        using var workspace = new SchedulerWorkspace(session);
        await workspace.InitializeAsync();
        var draft = workspace.NewDraft;
        workspace.SetValues(draft, draft.Values with {
            Name = "Owned receipt fixture", TargetKind = SchedulerPlanTargetKind.Workflow,
            TargetId = target.Id.Value, TargetVersionId = target.VersionId.Value,
            CronExpression = "0 0 9 ? * MON-FRI", TimeZoneId = "UTC", InputJson = "{\"unknown\":true}",
            StartAtUtc = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), EndAtUtc = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero)
        });
        interceptor.LoseAcknowledgement = fault == Fault.LostCommitAcknowledgement;
        await workspace.SaveAsync(draft);
        await using var database = await services.GetRequiredService<IDbContextFactory<SchedulerPlannerDbContext>>().CreateDbContextAsync();
        var stored = Assert.Single(await database.Set<SchedulerPlan>().Where(plan => plan.TargetId == target.Id.Value).ToListAsync());
        Assert.Equal(target.VersionId.Value, stored.TargetVersionId);
        Assert.Equal(draft.Values.StartAtUtc, stored.StartAtUtc);
        Assert.Equal(draft.Values.EndAtUtc, stored.EndAtUtc);
        Assert.Contains("unknown", stored.InputJson, StringComparison.Ordinal);
        Assert.Equal(fault == Fault.LostCommitAcknowledgement ? SchedulerMutationStatus.Unknown : SchedulerMutationStatus.CommittedWithWarning, draft.Receipt!.Status);
        if (fault == Fault.LostCommitAcknowledgement) {
            Assert.Null(draft.Values.Id);
            await workspace.SaveAsync(draft);
        } else {
            Assert.Equal(stored.Id, draft.Values.Id);
            Assert.Equal(fault == Fault.Projection ? SchedulerMutationStage.Persisted : SchedulerMutationStage.ProjectionSynchronized, draft.Receipt.Stage);
        }
        var projectionCalls = projection.Calls;
        interceptor.FailRead = false;
        await workspace.RefreshAsync();
        Assert.Equal(projectionCalls, projection.Calls);
        Assert.Single(await database.Set<SchedulerPlan>().Where(plan => plan.TargetId == target.Id.Value).ToListAsync());
        Assert.Empty(await database.Set<SchedulerPlanRun>().Where(run => run.PlanId == stored.Id).ToListAsync());
    }

    [Theory]
    [InlineData(SchedulerMutationKind.Disable)]
    [InlineData(SchedulerMutationKind.Enable)]
    [InlineData(SchedulerMutationKind.Delete)]
    public async Task Flag_and_delete_commit_facts_survive_projection_failure_and_keep_admissions(SchedulerMutationKind kind) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var target = await SeedWorkflowAsync(services);
        var actual = services.GetRequiredService<ISchedulerPlannerService>();
        var editor = await actual.CreateDefaultEditorAsync();
        editor.TargetId = target.Id.Value;
        editor.TargetVersionId = target.VersionId.Value;
        editor.Name = "Owned delete and flag fixture";
        editor.IsEnabled = kind != SchedulerMutationKind.Enable;
        var saved = await actual.SavePlanAsync(editor);
        var runId = Guid.NewGuid();
        var admissionId = Guid.NewGuid();
        var factory = services.GetRequiredService<IDbContextFactory<SchedulerPlannerDbContext>>();
        await using (var seed = await factory.CreateDbContextAsync()) {
            seed.Add(new SchedulerPlanRun { Id = runId, PlanId = saved.Id, DedupeKey = "owned-display-" + runId.ToString("N") });
            seed.Add(new SchedulerFireAdmissionRecord {
                Id = admissionId, PlanId = saved.Id, DedupeKey = "owned-admission-" + admissionId.ToString("N"),
                SnapshotJson = "{}", SnapshotFingerprint = "owned-display-fixture", PreparedWorkflowRunId = Guid.NewGuid(),
                State = SchedulerFireAdmissionState.Observed
            });
            await seed.SaveChangesAsync();
        }
        var projection = new ProjectionProbe(services.GetRequiredService<ISchedulerPlannerTriggerScheduler>(), new()) { Mode = Fault.Projection };
        var owner = ActivatorUtilities.CreateInstance<SchedulerPlannerService>(services, projection);
        var session = ActivatorUtilities.CreateInstance<SchedulerWorkspaceSession>(services, owner);
        var receipt = kind == SchedulerMutationKind.Delete ? await session.DeleteAsync(saved.Id) : await session.ToggleAsync(saved.Id, kind == SchedulerMutationKind.Enable);
        Assert.Equal(SchedulerMutationStatus.CommittedWithWarning, receipt.Status);
        Assert.Equal(kind, receipt.Kind);
        Assert.Equal(saved.Id, receipt.PlanId);
        Assert.Equal(SchedulerMutationStage.Persisted, receipt.Stage);
        await using var database = await factory.CreateDbContextAsync();
        Assert.True(await database.Set<SchedulerFireAdmissionRecord>().AnyAsync(item => item.Id == admissionId));
        if (kind == SchedulerMutationKind.Delete) {
            Assert.False(await database.Set<SchedulerPlan>().AnyAsync(item => item.Id == saved.Id));
            Assert.False(await database.Set<SchedulerPlanRun>().AnyAsync(item => item.Id == runId));
        } else {
            Assert.Equal(kind == SchedulerMutationKind.Enable, (await database.Set<SchedulerPlan>().SingleAsync(item => item.Id == saved.Id)).IsEnabled);
            Assert.True(await database.Set<SchedulerPlanRun>().AnyAsync(item => item.Id == runId));
        }
    }

    [Fact]
    public async Task Real_validation_refuses_invalid_cron_without_a_commit_or_privileged_UI_payload() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var target = await SeedWorkflowAsync(services);
        var receipt = await services.GetRequiredService<SchedulerWorkspaceSession>().SaveAsync(new() {
            Name = "Invalid cron fixture", TargetId = target.Id.Value, TargetVersionId = target.VersionId.Value,
            CronExpression = "not a cron", TimeZoneId = "UTC"
        });
        Assert.Equal(SchedulerMutationStatus.Refused, receipt.Status);
        await using var database = await services.GetRequiredService<IDbContextFactory<SchedulerPlannerDbContext>>().CreateDbContextAsync();
        Assert.False(await database.Set<SchedulerPlan>().AnyAsync(plan => plan.TargetId == target.Id.Value));
        Assert.Null(typeof(SchedulerDraftValues).GetProperty(nameof(SchedulerPlanEditorModel.StructureAuthority)));
    }

    private static async Task<WorkflowDefinition> SeedWorkflowAsync(IServiceProvider services) {
        var start = new WorkflowNodeId("start");
        var end = new WorkflowNodeId("end");
        static WorkflowNode Node(WorkflowNodeId id, WorkflowNodeKind kind) => new(id, kind, id.Value, [],
            new(null, null, null, null, string.Empty, WorkflowValueShape.Text, WorkflowValueShape.Text));
        return await services.GetRequiredService<IWorkflowCatalogService>().SaveDefinitionAsync(new(
            WorkflowId.New(), null, "Owned Scheduler receipt fixture", "Start/end only", WorkflowLifecycleStatus.Draft,
            new(start, [Node(start, WorkflowNodeKind.Start), Node(end, WorkflowNodeKind.End)],
                [new(new("start-end"), start, null, end, null, WorkflowEdgeKind.Direct, string.Empty) { Routing = WorkflowEdgeRouting.Always }]),
            new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false)));
    }
    private sealed class OwnedFactory(DbContextOptions<SchedulerPlannerDbContext> options) : IDbContextFactory<SchedulerPlannerDbContext> {
        public SchedulerPlannerDbContext CreateDbContext() => new(options);
    }
    private sealed class ProjectionProbe(ISchedulerPlannerTriggerScheduler actual, FaultInterceptor interceptor) : ISchedulerPlannerTriggerScheduler {
        public Fault Mode { get; init; }
        public int Calls { get; private set; }
        public Task SynchronizeAsync(CancellationToken token = default) => actual.SynchronizeAsync(token);
        public async Task SynchronizePlanAsync(Guid id, CancellationToken token = default) {
            Calls++;
            if (Mode == Fault.Projection) {
                throw new IOException("Owned projection fault after persistence.");
            }
            await actual.SynchronizePlanAsync(id, token);
            interceptor.FailRead = Mode == Fault.Reload;
        }
    }
    private sealed class FaultInterceptor : SaveChangesInterceptor {
        public bool LoseAcknowledgement { get; set; }
        public bool FailRead { get; set; }
    }
    private sealed class FaultLogger : ILogger<SchedulerPlannerService> {
        public bool Armed { get; init; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (Armed) {
                throw new IOException("Owned diagnostic failure after persistence.");
            }
        }
    }
    private sealed class ReadFault(FaultInterceptor state) : DbCommandInterceptor {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken token = default) {
            if (state.FailRead) {
                throw new IOException("Owned readback failure after projection.");
            }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class CommitFault(FaultInterceptor state) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken token = default) {
            if (state.LoseAcknowledgement) {
                throw new IOException("Owned lost acknowledgement after PostgreSQL commit.");
            }
            return Task.CompletedTask;
        }
    }
}
