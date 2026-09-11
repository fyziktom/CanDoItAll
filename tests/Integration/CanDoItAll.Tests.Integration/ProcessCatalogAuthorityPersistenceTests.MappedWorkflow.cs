using System.Data.Common;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Fact]
    public async Task Mapped_workflow_actual_executor_admits_one_outcome_only_child_and_new_claim_observes_it_after_restart() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await MappedCase.CreateAsync(scope.ServiceProvider, clock);
        var launcher = new MappedOwnerLauncher(test);
        var producer = test.Producer(launcher);
        var result = await producer.ExecuteAsync(test.Assignment, test.Contract, dispatchClaimIdentity: new(test.Claim));
        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        var child = Assert.Single(await test.Query().FindAsync(new(test.Assignment.RunId.Value), new(test.Assignment.StepInstanceId.Value)));
        var origin = Assert.IsType<WorkflowLaunchOrigin.ProcessDispatchAssignment>(child.Origin);
        Assert.Null(origin.StructureAuthority);
        Assert.Equal(test.Claim, origin.Dispatch.ClaimToken);
        Assert.Equal(test.Assignment.WorkflowBinding!.WorkflowId.Value, origin.Dispatch.WorkflowId.Value);
        Assert.NotEqual(test.Fixture.Agent.Id, origin.Dispatch.WorkflowId.Value);
        Assert.Equal(1, launcher.Calls);
        Assert.Empty(result.ProducedArtifacts);
        Assert.Empty(result.RequestedArtifacts);
        var replacement = await test.ReplaceClaimAsync();
        var restarted = test.Producer(launcher);
        var replay = await restarted.ExecuteAsync(test.Assignment, test.Contract, dispatchClaimIdentity: new(replacement));
        Assert.Equal(StrategyOutcome.Succeeded, replay.Outcome);
        Assert.Equal(result.ExecutionRunId, replay.ExecutionRunId);
        Assert.Equal(1, launcher.Calls);
        Assert.Equal(child, Assert.Single(await test.Query().FindAsync(origin.Dispatch.ProcessRun, origin.Dispatch.Assignment)));
        Assert.Equal(2, (await test.Owner().ListEventsAsync(child.RunId)).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mapped_workflow_claim_expiry_before_or_after_owner_flush_rolls_back_run_and_started_event(bool afterFlush) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await MappedCase.CreateAsync(scope.ServiceProvider, clock);
        var run = await test.RunAsync();
        var probe = new WorkflowToolProbe(run.RunId) { AfterFlush = afterFlush ? () => clock.Now = clock.Now.AddHours(2) : null };
        if (!afterFlush) {
            clock.Now = clock.Now.AddHours(2);
        }
        Assert.NotNull(await Record.ExceptionAsync(() => test.Owner(probe).CreateRunWithStartedEventAsync(run, WorkflowStarted(run))));
        Assert.Equal(afterFlush ? 1 : 0, probe.Flushes);
        Assert.Equal(0, probe.Commits);
        Assert.Null(await test.Owner().GetRunAsync(run.RunId));
        Assert.Empty(await test.Owner().ListEventsAsync(run.RunId));
        await test.Fixture.Writer.UpdateCatalogAsync(catalog => catalog).WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(Revocation.Write)]
    [InlineData(Revocation.ExactLifetime)]
    public async Task Mapped_workflow_original_source_revocation_blocks_new_owner_admission(Revocation revocation) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await MappedCase.CreateAsync(scope.ServiceProvider, clock);
        var run = await test.RunAsync();
        await test.Fixture.RevokeAsync(revocation);
        Assert.NotNull(await Record.ExceptionAsync(() => test.Owner().CreateRunWithStartedEventAsync(run, WorkflowStarted(run))));
        Assert.Null(await test.Owner().GetRunAsync(run.RunId));
        Assert.Empty(await test.Owner().ListEventsAsync(run.RunId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mapped_workflow_lost_owner_ack_preserves_exact_exception_and_durable_child_identity(bool argumentException) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await MappedCase.CreateAsync(scope.ServiceProvider, clock);
        var run = await test.RunAsync();
        Exception expected = argumentException ? new ArgumentException("Mapped owner acknowledgement lost") : new IOException("Mapped owner acknowledgement lost");
        var probe = new WorkflowToolProbe(run.RunId) { AfterCommitFailure = expected };
        Assert.Same(expected, await Record.ExceptionAsync(() => test.Owner(probe).CreateRunWithStartedEventAsync(run, WorkflowStarted(run))));
        Assert.Equal(1, probe.Flushes);
        Assert.Equal(1, probe.Commits);
        var retained = Assert.Single(await test.Query().FindAsync(new(test.Assignment.RunId.Value), new(test.Assignment.StepInstanceId.Value)));
        Assert.Equal(run, retained);
        var launcher = new MappedOwnerLauncher(test);
        var deferred = await Assert.ThrowsAsync<ProcessRuntimeDispatchDeferredException>(() => test.Producer(launcher).ExecuteAsync(
            test.Assignment, test.Contract, dispatchClaimIdentity: new(test.Claim)).AsTask());
        Assert.Contains(run.RunId.Value.ToString("D"), deferred.Message, StringComparison.Ordinal);
        Assert.Equal(0, launcher.Calls);
        Assert.Single(await test.Owner().ListEventsAsync(run.RunId));
    }

    [Fact]
    public async Task Mapped_workflow_stale_claim_and_changed_repair_content_cannot_admit_a_child() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await MappedCase.CreateAsync(scope.ServiceProvider, clock);
        var run = await test.RunAsync();
        var replacement = await test.ReplaceClaimAsync();
        Assert.NotNull(await Record.ExceptionAsync(() => test.Owner().CreateRunWithStartedEventAsync(run, WorkflowStarted(run))));
        var renewed = test with { Claim = replacement };
        var beforeRepair = await renewed.RunAsync();
        await using (var context = test.Fixture.Context()) {
            var row = await context.RuntimeStepAssignments.SingleAsync(item => item.RunId == test.Assignment.RunId.Value);
            row.Prompt = "Changed after preparation";
            await context.SaveChangesAsync();
        }
        Assert.NotNull(await Record.ExceptionAsync(() => test.Owner().CreateRunWithStartedEventAsync(beforeRepair, WorkflowStarted(beforeRepair))));
        Assert.Empty(await test.Query().FindAsync(new(test.Assignment.RunId.Value), new(test.Assignment.StepInstanceId.Value)));
    }

    [Fact]
    public async Task Mapped_workflow_concurrent_actual_owner_admission_keeps_one_child_and_started_event() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await MappedCase.CreateAsync(scope.ServiceProvider, clock);
        var first = await test.RunAsync();
        var second = first with { RunId = WorkflowRunId.New() };
        var outcomes = await Task.WhenAll(Record.ExceptionAsync(() => test.Owner().CreateRunWithStartedEventAsync(first, WorkflowStarted(first))),
            Record.ExceptionAsync(() => test.Owner().CreateRunWithStartedEventAsync(second, WorkflowStarted(second))));
        Assert.Single(outcomes, item => item is null);
        var retained = Assert.Single(await test.Query().FindAsync(new(test.Assignment.RunId.Value), new(test.Assignment.StepInstanceId.Value)));
        Assert.Contains(retained.RunId, new[] { first.RunId, second.RunId });
        Assert.Single(await test.Owner().ListEventsAsync(retained.RunId));
    }

    [Fact]
    public async Task Mapped_workflow_concurrent_SQL_waiter_keeps_one_child_without_a_catalog_source_lock() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await MappedCase.CreateAsync(scope.ServiceProvider, clock, legacyParent: true);
        var first = await test.RunAsync();
        var second = first with { RunId = WorkflowRunId.New() };
        var committing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstProbe = new WorkflowToolProbe(first.RunId) { BeforeCommit = async token => {
            committing.TrySetResult();
            var processId = await waiting.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
            await RequireMappedAdvisoryWaitAsync(scope.ServiceProvider, processId, token);
        } };
        var firstWrite = Record.ExceptionAsync(() => test.Owner(firstProbe).CreateRunWithStartedEventAsync(first, WorkflowStarted(first)));
        await committing.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var waiter = new MappedRootWaiterProbe(waiting);
        var secondWrite = Record.ExceptionAsync(() => test.Owner(processProbe: waiter).CreateRunWithStartedEventAsync(second, WorkflowStarted(second)));
        var outcomes = await Task.WhenAll(firstWrite, secondWrite);
        Assert.True(firstProbe.BeforeCommitObserved);
        Assert.True(waiter.Observed);
        Assert.Null(outcomes[0]);
        Assert.NotNull(outcomes[1]);
        var retained = Assert.Single(await test.Query().FindAsync(new(test.Assignment.RunId.Value), new(test.Assignment.StepInstanceId.Value)));
        Assert.Equal(first.RunId, retained.RunId);
        Assert.Single(await test.Owner().ListEventsAsync(first.RunId));
        Assert.Empty(await test.Owner().ListEventsAsync(second.RunId));
    }

    [Fact]
    public async Task Mapped_workflow_legacy_receipt_remains_visible_and_prevents_new_admission_without_authority_backfill() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await MappedCase.CreateAsync(scope.ServiceProvider, clock);
        var candidate = await test.RunAsync();
        var old = candidate with { RunId = WorkflowRunId.New(), Origin = new WorkflowLaunchOrigin.ProcessAssignment(
            new WorkflowProcessRunId(test.Assignment.RunId.Value), new WorkflowProcessAssignmentId(test.Assignment.StepInstanceId.Value),
            new WorkflowLaunchCorrelationId(test.Assignment.RunId.Value)) };
        await using (var database = new WorkflowDbContext(NativeOptions<WorkflowDbContext>(scope.ServiceProvider))) {
            database.Add(WorkflowRunRecordEntity.FromSnapshot(old));
            await database.SaveChangesAsync();
        }
        Assert.Equal(old, Assert.Single(await test.Query().FindAsync(new(test.Assignment.RunId.Value), new(test.Assignment.StepInstanceId.Value))));
        Assert.NotNull(await Record.ExceptionAsync(() => test.Owner().CreateRunWithStartedEventAsync(candidate, WorkflowStarted(candidate))));
        Assert.Null(await test.Owner().GetRunAsync(candidate.RunId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => test.Owner().SaveRunAsync(old with { Origin = null }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => test.Owner().TryTransitionRunAsync(old.RunId, [old.State], old with { Origin = null }));
        Assert.IsType<WorkflowLaunchOrigin.ProcessAssignment>((await test.Owner().GetRunAsync(old.RunId))!.Origin);
    }

    [Fact]
    public async Task Mapped_workflow_missing_owner_policy_fails_before_flush_and_legacy_parent_gets_no_structure_authority() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await MappedCase.CreateAsync(scope.ServiceProvider, clock, legacyParent: true);
        var run = await test.RunAsync();
        Assert.Null(run.Origin!.StructureAuthority);
        var probe = new WorkflowToolProbe(run.RunId);
        var owner = new PersistentWorkflowRunStore(new Factory<WorkflowDbContext>(NativeOptions<WorkflowDbContext>(scope.ServiceProvider, probe), static options => new(options)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CreateRunWithStartedEventAsync(run, WorkflowStarted(run)));
        Assert.Equal(0, probe.Flushes);
        await test.Owner().CreateRunWithStartedEventAsync(run, WorkflowStarted(run));
        Assert.Equal(run.RunId, Assert.Single(await test.Query().FindAsync(new(test.Assignment.RunId.Value), new(test.Assignment.StepInstanceId.Value))).RunId);
    }

    [Fact]
    public async Task Mapped_workflow_owner_updates_cannot_strip_or_replace_admitted_dispatch() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await MappedCase.CreateAsync(scope.ServiceProvider, clock);
        var run = await test.RunAsync();
        await test.Owner().CreateRunWithStartedEventAsync(run, WorkflowStarted(run));
        await Assert.ThrowsAsync<InvalidOperationException>(() => test.Owner().SaveRunAsync(run with { Origin = null }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => test.Owner().TryTransitionRunAsync(run.RunId, [run.State], run with { Origin = null }));
        Assert.Equal(run, await test.Owner().GetRunAsync(run.RunId));
        Assert.Single(await test.Owner().ListEventsAsync(run.RunId));
    }

    private sealed record MappedCase(IServiceProvider Services, NativeClock Clock, Fixture Fixture,
        ProcessRuntimeStepAssignment Assignment, ProcessStepExecutionContract Contract, Guid Claim,
        ProjectStructureWorkflowAuthorityService Source) {
        public PersistentWorkflowRunStore Owner(IInterceptor? probe = null, IInterceptor? processProbe = null) => new(
            new Factory<WorkflowDbContext>(NativeOptions<WorkflowDbContext>(Services, probe), static options => new(options)),
            coordinatedTransactions: Services.GetRequiredService<CoordinatedDatabaseTransaction>(),
            mappedProcessSource: processProbe is null ? Source : CreateSource(Services, Clock, Fixture, processProbe));
        public PersistentWorkflowProcessAssignmentRunQuery Query() => new(
            new Factory<WorkflowDbContext>(NativeOptions<WorkflowDbContext>(Services), static options => new(options)),
            Services.GetRequiredService<ICanonicalRuntimeDatabase>());
        public WorkflowProcessStepExecutor Producer(IWorkflowLaunchService launcher) => new(launcher,
            Services.GetRequiredService<IWorkflowRuntimeManager>(), Services.GetRequiredService<ProcessExecutionResultConverter>(), Source, Query());
        public async Task<WorkflowRunSnapshot> RunAsync() {
            var origin = await Source.CaptureAsync(new WorkflowProcessRunId(Assignment.RunId.Value), new WorkflowProcessAssignmentId(Assignment.StepInstanceId.Value), Claim, Contract.ContractHash);
            return new(WorkflowRunId.New(), origin.Dispatch.WorkflowId, origin.Dispatch.RequestedVersionId!.Value, WorkflowRunState.Running,
                WorkflowRuntimeBackendKind.InProcess, "mapped-fixture", "Mapped admission", Clock.Now, Clock.Now) { Origin = origin };
        }

        public async Task<Guid> ReplaceClaimAsync() {
            var next = Guid.NewGuid();
            await using var context = Fixture.Context();
            var old = await context.DispatchClaims.SingleAsync(item => item.RunId == Assignment.RunId.Value && item.ClaimToken == Claim);
            old.ExpiresAtUtc = Clock.Now.AddSeconds(-1);
            var step = await context.RuntimeSteps.SingleAsync(item => item.RunId == Assignment.RunId.Value);
            step.ActiveClaimToken = next;
            step.AttemptNumber++;
            context.DispatchClaims.Add(new() { RunId = Assignment.RunId.Value, StepInstanceId = Assignment.StepInstanceId.Value,
                ClaimToken = next, OwnerId = "mapped-recovery", Status = DispatchClaimStatus.Reclaimed, AttemptNumber = step.AttemptNumber,
                CreatedAtUtc = Clock.Now, ExpiresAtUtc = Clock.Now.AddHours(1) });
            await context.SaveChangesAsync();
            return next;
        }

        public static async Task<MappedCase> CreateAsync(IServiceProvider services, NativeClock clock, bool legacyParent = false) {
            clock.Now = new(clock.Now.Ticks - clock.Now.Ticks % TimeSpan.TicksPerMicrosecond, clock.Now.Offset);
            var fixture = await Fixture.CreateAsync(services);
            var preparation = ProcessPreparedLaunchFixture.Create(fixture.SavedAuthority, new(Guid.NewGuid()));
            var initial = preparation.InitialCommit;
            var assignment = Assert.Single(initial.InitialAssignments!) with {
                ExecutorKind = ProcessLaunchExecutorKinds.Workflow, ExecutorId = "legacy-display-only",
                WorkflowBinding = new(new(Guid.NewGuid()), new(Guid.NewGuid())),
                OperationTargetScope = string.Empty,
                LaunchVariables = new Dictionary<string, string> { [ProcessRuntimeLaunchVariables.ProjectId] = fixture.Project.ProjectId.ToString("D") }
            };
            preparation = preparation with { InitialCommit = initial with { InitialAssignments = [assignment] } };
            var saved = await fixture.Store.PrepareAsync(preparation);
            await using (var context = fixture.Context()) {
                Assert.True((await fixture.UnitOfWork(context).CommitAsync(ProcessPreparedLaunchFixture.Commit(saved))).Succeeded);
            }
            var claim = Guid.NewGuid();
            await using (var context = fixture.Context()) {
                var state = await context.RuntimeStates.SingleAsync(item => item.RunId == assignment.RunId.Value);
                var step = await context.RuntimeSteps.SingleAsync(item => item.RunId == assignment.RunId.Value);
                state.Status = ProcessRuntimeStatus.Active;
                if (legacyParent) {
                    state.LaunchAdmissionId = null;
                }
                step.Status = ProcessRuntimeStepStatus.Running;
                step.AttemptNumber = 1;
                step.ActiveClaimToken = claim;
                context.DispatchClaims.Add(new() { RunId = assignment.RunId.Value, StepInstanceId = assignment.StepInstanceId.Value,
                    ClaimToken = claim, OwnerId = "mapped-fixture", Status = DispatchClaimStatus.Claimed, AttemptNumber = 1,
                    CreatedAtUtc = clock.Now.AddSeconds(-1), ExpiresAtUtc = clock.Now.AddHours(1) });
                await context.SaveChangesAsync();
            }
            var initialState = initial.Mutation.State;
            var contract = ProcessRuntimeArtifactContracts.BuildStepContract(initialState, Assert.Single(initialState.Steps), initial.InitialPlan!.Branches);
            return new(services, clock, fixture, assignment, contract, claim, CreateSource(services, clock, fixture));
        }

        private static ProjectStructureWorkflowAuthorityService CreateSource(IServiceProvider services, NativeClock clock, Fixture fixture,
            IInterceptor? processProbe = null) {
            var options = NativeOptions<ProcessPersistenceDbContext>(services, processProbe);
            var dispatch = new EfProcessWorkflowDispatchAuthority(new Factory<ProcessPersistenceDbContext>(options, static configured => new(configured)),
                options, services.GetRequiredService<CoordinatedDatabaseTransaction>(), clock);
            return new(services.GetRequiredService<ICanonicalRuntimeDatabase>(),
                services.GetRequiredService<IOptionsMonitor<ApiAccessOptions>>(), services.GetRequiredService<IAgentFrameworkWorkspaceService>(),
                services.GetRequiredService<IProcessRuntimeStateStore>(), services.GetRequiredService<IProcessRuntimeStepAssignmentStore>(), clock,
                services.GetRequiredService<IWorkflowScheduledAuthorityPolicy>(), fixture.Source,
                services.GetRequiredService<IAgentExecutionProfileGenerationSource>(), services.GetRequiredService<ProjectWriteAdmissionService>(),
                processObservation: NativeAuthority(services, clock), mappedProcessReader: dispatch, mappedProcessGuard: dispatch);
        }
    }

    private static async Task RequireMappedAdvisoryWaitAsync(IServiceProvider services, int processId, CancellationToken cancellationToken) {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await using var monitor = new ProcessPersistenceDbContext(NativeOptions<ProcessPersistenceDbContext>(services));
        await monitor.Database.OpenConnectionAsync(timeout.Token);
        await using var command = monitor.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE pid = @pid AND wait_event_type = 'Lock' AND wait_event = 'advisory')";
        command.Parameters.Add(new NpgsqlParameter<int>("pid", processId));
        while (await command.ExecuteScalarAsync(timeout.Token) is not true) {
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
        }
    }

    private sealed class MappedRootWaiterProbe(TaskCompletionSource<int> waiting) : DbCommandInterceptor {
        public bool Observed { get; private set; }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal)) {
                Assert.IsType<ProcessPersistenceDbContext>(eventData.Context);
                Assert.NotNull(eventData.Context.Database.CurrentTransaction);
                Observed = true;
                waiting.TrySetResult(Assert.IsType<NpgsqlConnection>(command.Connection).ProcessID);
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class MappedOwnerLauncher(MappedCase test) : IWorkflowLaunchService {
        public int Calls { get; private set; }
        public async Task<WorkflowLaunchResult> LaunchAsync(WorkflowLaunchIntent intent, CancellationToken cancellationToken = default) {
            Calls++;
            var origin = Assert.IsType<WorkflowLaunchOrigin.ProcessDispatchAssignment>(intent.Origin);
            Assert.Equal(WorkflowMappedProcessInputFingerprint.Compute(intent.InputJson), origin.Dispatch.InputFingerprint);
            var running = new WorkflowRunSnapshot(WorkflowRunId.New(), origin.Dispatch.WorkflowId, origin.Dispatch.RequestedVersionId!.Value,
                WorkflowRunState.Running, WorkflowRuntimeBackendKind.InProcess, "mapped-fixture", "Mapped running", test.Clock.Now, test.Clock.Now) { Origin = origin };
            await test.Owner().CreateRunWithStartedEventAsync(running, WorkflowStarted(running), cancellationToken);
            var completed = running with { State = WorkflowRunState.Completed, TerminalAtUtc = test.Clock.Now };
            var output = new ProcessStepOutcomeResult { Status = ProcessStepOutcomeStatus.Completed, Reason = "Mapped output", EvidenceRefs = ["workflow://mapped/complete"] };
            var completedEvent = new WorkflowEventRecord(Guid.NewGuid(), running.RunId, WorkflowEventKind.ExecutorCompleted, new("result"), "Mapped output",
                WorkflowEventPayloads.Serialize(WorkflowEventPayloadSource.CanDoItAllProgress, "WorkflowNodeCompleted",
                    inlineJson: JsonSerializer.Serialize(output, AgentOutputJson.SerializerOptions)), test.Clock.Now);
            Assert.True((await test.Owner().TryTransitionRunAsync(running.RunId, [WorkflowRunState.Running], completed, completedEvent, cancellationToken)).Transitioned);
            var definition = new WorkflowDefinition(completed.WorkflowId, completed.VersionId, "Mapped fixture", "Outcome-only owner proof",
                WorkflowLifecycleStatus.Active, new(new("start"), [], []), new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false), test.Clock.Now, test.Clock.Now);
            var backend = new WorkflowRuntimeBackendCatalog([WorkflowRuntimeBackendKind.InProcess]).GetRequiredBackend(WorkflowRuntimeBackendKind.InProcess);
            return new(completed, new(definition, intent.InputJson, backend, WorkflowPreviewSimulationPlan.Empty, intent.Mode, origin,
                intent.CompletionPolicy, intent.Idempotency, test.Clock.Now), WorkflowLaunchIdempotencyDisposition.EnforcedNewRun);
        }
    }
}
