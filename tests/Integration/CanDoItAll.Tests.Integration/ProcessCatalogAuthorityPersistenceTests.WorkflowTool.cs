using System.Data.Common;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Fact]
    public async Task Workflow_tool_uses_real_background_proposals_and_keeps_source_executor_and_two_child_identities_distinct() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await WorkflowCase.CreateAsync(scope.ServiceProvider, clock);
        await using var lease = await test.Journal.AcquireRunAsync(test.Context.AdmittedToolSession!, default);
        using var bound = lease.Bind();
        var metadata = Assert.Single(test.Provider.GetToolMetadata(test.Context), item => item.ToolName == WorkflowToolPolicy.WorkflowsRunStart);
        Assert.Equal(test.Payload, metadata.PrepareAdmission!(JsonSerializer.SerializeToElement(new WorkflowProcessToolProposal(test.Input), WorkflowProcessToolProposalCodec.SerializerOptions)));
        var batch = await test.AdmitAsync(lease, ["first", "second"]);
        var tool = Assert.Single((await test.Provider.CreateToolsAsync(test.Context, default)).OfType<AIFunction>(), item => item.Name == WorkflowToolPolicy.WorkflowsRunStart);
        var childIds = new List<Guid>();
        foreach (var call in new[] { "first", "second" }) {
            var claim = await test.Journal.ClaimInvocationAsync(lease, batch.Id, call, test.Payload, default);
            using var dispatch = claim.Bind();
            using var effects = AgentToolInvocationEffectScope.Begin();
            var raw = await tool.InvokeAsync(new AIFunctionArguments { ["request"] = test.Input });
            var result = raw is JsonElement json
                ? json.Deserialize<WorkflowAgentStartResult>(WorkflowProcessToolProposalCodec.SerializerOptions)!
                : Assert.IsType<WorkflowAgentStartResult>(raw);
            Assert.NotNull(result);
            childIds.Add(result.Run.RunId);
            Assert.Equal(claim.Proposal.IntentId.Value, result.Run.RunId);
            Assert.Equal(new AgentToolCommittedEffect(WorkflowProcessToolAdmission.EffectSourceKind, result.Run.RunId.ToString("D")), effects.CommittedEffect);
            var saved = (await test.Owner().GetRunAsync(new(result.Run.RunId)))!;
            var origin = Assert.IsType<WorkflowLaunchOrigin.ProcessToolInvocation>(saved.Origin);
            Assert.Equal(test.Context.Agent.Id, origin.Invocation.ExecutorAgentId);
            Assert.NotEqual(test.Fixture.Agent.Id, origin.Invocation.ExecutorAgentId);
            Assert.Equal(test.Fixture.Agent.Id.ToString("D"), origin.StructureAuthority!.Principal.SubjectId);
            Assert.Equal(test.Fixture.Project.LifetimeId, Assert.Single(origin.StructureAuthority.ProjectScope!.Projects).LifetimeId);
            Assert.Equal(test.Execution.Id, origin.Invocation.Session.ExecutionRunId);
            Assert.Null(test.Context.Governance);
            await test.Journal.CompleteInvocationAsync(claim, WorkflowEnvelope(JsonSerializer.Serialize(result,
                WorkflowProcessToolProposalCodec.SerializerOptions)), AgentToolEffectState.Committed, default);
        }
        Assert.Equal(2, childIds.Distinct().Count());
        Assert.Equal(2, test.Launcher.Calls);
        await using var owner = new WorkflowDbContext(NativeOptions<WorkflowDbContext>(scope.ServiceProvider));
        Assert.Equal(2, await owner.Set<WorkflowRunRecordEntity>().CountAsync(row => childIds.Contains(row.RunId)));
        Assert.Equal(2, await owner.Set<WorkflowEventRecordEntity>().CountAsync(row => childIds.Contains(row.RunId)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Workflow_tool_claim_expiry_before_or_after_real_flush_leaves_no_run_or_started_event(bool afterFlush) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await WorkflowCase.CreateAsync(scope.ServiceProvider, clock);
        await using var lease = await test.Journal.AcquireRunAsync(test.Context.AdmittedToolSession!, default);
        using var bound = lease.Bind();
        var batch = await test.AdmitAsync(lease, ["start"]);
        var claim = await test.Journal.ClaimInvocationAsync(lease, batch.Id, "start", test.Payload, default);
        using var dispatch = claim.Bind();
        var run = await test.RunAsync();
        var probe = new WorkflowToolProbe(run.RunId) { AfterFlush = afterFlush ? () => clock.Now = clock.Now.AddHours(2) : null };
        if (!afterFlush) {
            clock.Now = clock.Now.AddHours(2);
        }
        var error = await Record.ExceptionAsync(() => test.Owner(probe).CreateRunWithStartedEventAsync(run, WorkflowStarted(run)));
        Assert.NotNull(error);
        Assert.Equal(afterFlush ? 1 : 0, probe.Flushes);
        Assert.Equal(0, probe.Commits);
        Assert.Null(await test.Owner().GetRunAsync(run.RunId));
        Assert.Empty(await test.Owner().ListEventsAsync(run.RunId));
        await test.Fixture.Writer.UpdateCatalogAsync(catalog => catalog).WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Workflow_tool_source_and_executor_catalog_revocations_block_new_admission(bool executor) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await WorkflowCase.CreateAsync(scope.ServiceProvider, clock);
        await using var lease = await test.Journal.AcquireRunAsync(test.Context.AdmittedToolSession!, default);
        using var bound = lease.Bind();
        var batch = await test.AdmitAsync(lease, ["start"]);
        var claim = await test.Journal.ClaimInvocationAsync(lease, batch.Id, "start", test.Payload, default);
        using var dispatch = claim.Bind();
        var run = await test.RunAsync();
        if (executor) {
            await test.Fixture.Writer.UpdateCatalogAsync(catalog => catalog with {
                Agents = catalog.Agents.Select(agent => agent.Id == test.Context.Agent.Id ? agent with { Capabilities = [] } : agent).ToArray()
            });
        } else {
            await test.Fixture.RevokeAsync(Revocation.Write);
        }
        Assert.NotNull(await Record.ExceptionAsync(() => test.Owner().CreateRunWithStartedEventAsync(run, WorkflowStarted(run))));
        Assert.Null(await test.Owner().GetRunAsync(run.RunId));
        Assert.Empty(await test.Owner().ListEventsAsync(run.RunId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Workflow_tool_lost_commit_ack_retains_original_receipt_for_cancelled_reconciliation_after_claim_expiry(bool argumentException) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await WorkflowCase.CreateAsync(scope.ServiceProvider, clock);
        Exception failure = argumentException ? new ArgumentException("Workflow owner acknowledgement lost.") : new IOException("Workflow owner acknowledgement lost.");
        WorkflowRunSnapshot run;
        AgentToolBusinessIntentId intent;
        await using (var lease = await test.Journal.AcquireRunAsync(test.Context.AdmittedToolSession!, default)) {
            using var bound = lease.Bind();
            var batch = await test.AdmitAsync(lease, ["start"]);
            var claim = await test.Journal.ClaimInvocationAsync(lease, batch.Id, "start", test.Payload, default);
            using var dispatch = claim.Bind();
            run = await test.RunAsync();
            intent = claim.Proposal.IntentId;
            var probe = new WorkflowToolProbe(run.RunId) { AfterCommitFailure = failure };
            Assert.Same(failure, await Record.ExceptionAsync(() => test.Owner(probe).CreateRunWithStartedEventAsync(run, WorkflowStarted(run))));
            Assert.Equal(1, probe.Flushes);
            Assert.Equal(1, probe.Commits);
            await test.Journal.PreserveUncertainInvocationAsync(claim, default);
        }
        clock.Now = clock.Now.AddHours(2);
        await test.Fixture.RevokeAsync(Revocation.Write);
        await test.ExecutionStore.UpdateExecutionRunDetailAsync(test.Execution.Id, detail => detail with {
            Run = detail.Run with { State = ExecutionState.Failed, Outcome = RunOutcome.Cancelled, CompletedAtUtc = clock.Now,
                Revision = detail.Run.Revision + 1 }
        });
        var restarted = new AgentToolAdmissionJournal(test.ExecutionStore, NativeProfile(scope.ServiceProvider), clock,
            [NativeBackgroundPolicy(scope.ServiceProvider, test.Fixture, clock)]);
        await using (var reconciliation = await restarted.AcquireCancelledReconciliationAsync(test.Context.AdmittedToolSession!, default)) {
            using var bound = reconciliation.Bind();
            await restarted.ReconcileCancelledAsync(reconciliation, [test.Adapter], true, default);
        }
        var journal = (await test.ExecutionStore.GetExecutionRunAsync(test.Execution.Id))!.ToolAdmission!;
        var proposal = Assert.Single(Assert.Single(journal.Batches).Proposals);
        Assert.Equal(intent, proposal.IntentId);
        Assert.Equal(AgentToolEffectState.Committed, proposal.EffectState);
        Assert.Equal(new AgentToolCommittedEffect(WorkflowProcessToolAdmission.EffectSourceKind, run.RunId.Value.ToString("D")), proposal.Cancellation!.CommittedEffect);
        Assert.Equal(run.RunId, (await test.Owner().GetRunAsync(run.RunId))!.RunId);
        Assert.Single(await test.Owner().ListEventsAsync(run.RunId));
        Assert.Equal(0, test.Launcher.Calls);
    }

    [Fact]
    public async Task Workflow_tool_catalog_is_held_through_owner_commit_and_retargeted_proposal_fails_before_writes() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await WorkflowCase.CreateAsync(scope.ServiceProvider, clock);
        await using var lease = await test.Journal.AcquireRunAsync(test.Context.AdmittedToolSession!, default);
        using var bound = lease.Bind();
        var batch = await test.AdmitAsync(lease, ["start"]);
        var claim = await test.Journal.ClaimInvocationAsync(lease, batch.Id, "start", test.Payload, default);
        using var dispatch = claim.Bind();
        var run = await test.RunAsync();
        var retarget = run with { WorkflowId = WorkflowId.New() };
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => test.Owner().CreateRunWithStartedEventAsync(retarget, WorkflowStarted(retarget)));
        Assert.Null(await test.Owner().GetRunAsync(run.RunId));
        var probe = new WorkflowToolProbe(run.RunId) { BeforeCommit = async token => {
            using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(token);
            cancelled.CancelAfter(TimeSpan.FromMilliseconds(150));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => test.Fixture.Writer.UpdateCatalogAsync(catalog => catalog, cancelled.Token));
        } };
        await test.Owner(probe).CreateRunWithStartedEventAsync(run, WorkflowStarted(run));
        Assert.True(probe.BeforeCommitObserved);
        await test.Fixture.Writer.UpdateCatalogAsync(catalog => catalog).WaitAsync(TimeSpan.FromSeconds(10));
        await using var independent = new ProcessPersistenceDbContext(NativeOptions<ProcessPersistenceDbContext>(scope.ServiceProvider));
        Assert.Null(independent.Database.CurrentTransaction);
    }

    [Fact]
    public async Task Workflow_tool_same_intent_concurrent_owner_admission_has_one_run_and_started_event_and_frozen_source() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await WorkflowCase.CreateAsync(scope.ServiceProvider, clock);
        await using var lease = await test.Journal.AcquireRunAsync(test.Context.AdmittedToolSession!, default);
        using var bound = lease.Bind();
        var batch = await test.AdmitAsync(lease, ["start"]);
        var claim = await test.Journal.ClaimInvocationAsync(lease, batch.Id, "start", test.Payload, default);
        using var dispatch = claim.Bind();
        var run = await test.RunAsync();
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Record.ExceptionAsync(() =>
            test.Owner().CreateRunWithStartedEventAsync(run, WorkflowStarted(run)))));
        Assert.Single(outcomes, error => error is null);
        Assert.IsType<WorkflowRunAlreadyExistsException>(Assert.Single(outcomes, error => error is not null));
        var restarted = test.Owner();
        Assert.Single(await restarted.ListEventsAsync(run.RunId));
        Assert.Equal(JsonSerializer.Serialize<WorkflowLaunchOrigin>(run.Origin!), JsonSerializer.Serialize<WorkflowLaunchOrigin>((await restarted.GetRunAsync(run.RunId))!.Origin!));
        var stripped = run with { Origin = new WorkflowLaunchOrigin.ProcessAssignment(Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid())) };
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.SaveRunAsync(stripped));
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.TryTransitionRunAsync(run.RunId, [run.State], stripped));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Workflow_tool_missing_owner_policy_fails_before_writes_with_or_without_saved_project_authority(bool legacySource) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await WorkflowCase.CreateAsync(scope.ServiceProvider, clock, legacySource);
        await using var lease = await test.Journal.AcquireRunAsync(test.Context.AdmittedToolSession!, default);
        using var bound = lease.Bind();
        var batch = await test.AdmitAsync(lease, ["start"]);
        var claim = await test.Journal.ClaimInvocationAsync(lease, batch.Id, "start", test.Payload, default);
        using var dispatch = claim.Bind();
        var run = await test.RunAsync();
        Assert.Equal(legacySource, run.Origin!.StructureAuthority is null);
        var missing = new PersistentWorkflowRunStore(new Factory<WorkflowDbContext>(NativeOptions<WorkflowDbContext>(scope.ServiceProvider),
            static options => new(options)), coordinatedTransactions: scope.ServiceProvider.GetRequiredService<CoordinatedDatabaseTransaction>());
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => missing.CreateRunWithStartedEventAsync(run, WorkflowStarted(run)));
        Assert.Contains("source policy", failure.Message, StringComparison.Ordinal);
        Assert.Null(await missing.GetRunAsync(run.RunId));
        Assert.Empty(await missing.ListEventsAsync(run.RunId));
        await test.Owner().CreateRunWithStartedEventAsync(run, WorkflowStarted(run));
        var retained = (await missing.GetRunAsync(run.RunId))!;
        Assert.Equal(legacySource, retained.Origin!.StructureAuthority is null);
        Assert.Equal(test.Context.Agent.Id, Assert.IsType<WorkflowLaunchOrigin.ProcessToolInvocation>(retained.Origin).Invocation.ExecutorAgentId);
    }

    [Fact]
    public async Task Workflow_tool_rechecks_original_Process_policy_after_waiting_for_the_combined_catalog_snapshot() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await WorkflowCase.CreateAsync(scope.ServiceProvider, clock, revokeBeforeHeldRead: true);
        await using var lease = await test.Journal.AcquireRunAsync(test.Context.AdmittedToolSession!, default);
        using var bound = lease.Bind();
        var batch = await test.AdmitAsync(lease, ["start"]);
        var claim = await test.Journal.ClaimInvocationAsync(lease, batch.Id, "start", test.Payload, default);
        using var dispatch = claim.Bind();
        var run = await test.RunAsync();
        await Assert.ThrowsAsync<ProcessLaunchAuthorityRejectedException>(() => test.Owner().CreateRunWithStartedEventAsync(run, WorkflowStarted(run)));
        Assert.Null(await test.Owner().GetRunAsync(run.RunId));
        Assert.Empty(await test.Owner().ListEventsAsync(run.RunId));
        await using var current = await test.Fixture.Source.AcquireAgentReadLeaseAsync(test.Fixture.Agent.Id);
        Assert.False(ProjectStructureNonTaskWritePolicy.CanUseStructureMutationTools(AgentProjectStructureAccessMetadata.Read(current.Agent!.ConfigurationJson)));
    }

    private sealed class WorkflowReadRace(IAgentCatalogReadLeaseStore inner, Func<Task> before) : IAgentCatalogReadLeaseStore {
        public Task<IAgentCatalogReadLease> AcquireAgentReadLeaseAsync(Guid agentId, CancellationToken cancellationToken = default)
            => inner.AcquireAgentReadLeaseAsync(agentId, cancellationToken);

        public async Task<IAgentCatalogReadLease> AcquireAgentsReadLeaseAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default) {
            await before();
            return await inner.AcquireAgentsReadLeaseAsync(ids, cancellationToken);
        }
    }

    private static WorkflowEventRecord WorkflowStarted(WorkflowRunSnapshot run)
        => new(Guid.NewGuid(), run.RunId, WorkflowEventKind.Started, null, "Started by exact Process proposal", "{}", run.CreatedAtUtc);

    private static AgentToolProtocolEnvelope WorkflowEnvelope(string json = "{}")
        => CanDoItAll.Tests.Integration.Runtime.AgentToolAdmissionJournalFixture.Envelope(json);

    private sealed record WorkflowCase(IServiceProvider Services, NativeClock Clock, Fixture Fixture, ExecutionRunRecord Execution,
        FileSandboxWorkspaceStore ExecutionStore, AgentToolAdmissionJournal Journal, AgentRuntimeToolProviderContext Context,
        WorkflowAgentStartInput Input, AgentToolPreparedPayload Payload, ProjectStructureWorkflowAuthorityService Authority,
        WorkflowProcessToolAdmission Adapter, WorkflowAgentRuntimeToolProvider Provider, WorkflowOwnerLauncher Launcher) {
        public PersistentWorkflowRunStore Owner(IInterceptor? probe = null)
            => new(new Factory<WorkflowDbContext>(NativeOptions<WorkflowDbContext>(Services, probe), static options => new(options)),
                Services.GetRequiredService<IWorkflowScheduledSourceAuthorityPolicy>(), Services.GetRequiredService<CoordinatedDatabaseTransaction>(), Authority, Adapter);

        public async Task<WorkflowRunSnapshot> RunAsync() {
            var origin = await Adapter.CaptureAsync(Context, Input, default);
            return new(origin.Invocation.PreparedRunId, new(Input.WorkflowId), new(Input.VersionId!.Value), WorkflowRunState.Running,
                WorkflowRuntimeBackendKind.InProcess, origin.Invocation.PreparedRunId.ToString(), "Started", Clock.Now, Clock.Now) { Origin = origin };
        }

        public async Task<AgentToolBatchRecord> AdmitAsync(AgentToolRunLease lease, string[] calls) {
            await Journal.BeginSegmentAsync(lease, WorkflowEnvelope(), null, default);
            return Assert.Single((await Journal.AdmitBatchAsync(lease, Payload.Digest, WorkflowEnvelope(),
                calls.Select(call => new AgentToolPreparedCall(call, Payload, false)).ToArray(), default)).Batches);
        }

        public static async Task<WorkflowCase> CreateAsync(IServiceProvider services, NativeClock clock, bool legacySource = false, bool revokeBeforeHeldRead = false) {
            var fixture = await Fixture.CreateAsync(services);
            var execution = await CreateClaimedWorkflowExecutionAsync(services, fixture, clock);
            if (legacySource) {
                await using var legacyContext = fixture.Context();
                var state = await legacyContext.RuntimeStates.SingleAsync(item => item.RunId == Guid.Parse(execution.ProcessRunId!));
                state.LaunchAdmissionId = null;
                await legacyContext.SaveChangesAsync();
            }
            var store = Assert.IsType<FileSandboxWorkspaceStore>(services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>());
            var catalog = await store.LoadCatalogAsync();
            var capabilityKeys = new[] { WorkflowRuntimeCapabilityKeys.RunStart, WorkflowRuntimeCapabilityKeys.RunStatusGet };
            var capabilities = capabilityKeys.Select(key => Assert.Single(catalog.Capabilities, item => item.Key == key && item.Kind == CapabilityKind.Tool)).ToArray();
            catalog = await store.UpdateCatalogAsync(current => current with {
                Agents = current.Agents.Select(agent => agent.Id == execution.AgentId ? agent with {
                    Capabilities = capabilities.Select(item => new AgentCapabilityAssignment(item.Id, item.Key, item.Kind, item.ProofStatus, item.LastVerifiedAtUtc, item.ProofNotes)).ToArray()
                } : agent).ToArray()
            });
            var journal = new AgentToolAdmissionJournal(store, NativeProfile(services), clock, [NativeBackgroundPolicy(services, fixture, clock)]);
            execution = execution with { ToolAdmission = await journal.CreateForNewBackgroundRunAsync(execution, "Launch the approved Workflow.") };
            await store.SaveExecutionRunDetailAsync(new(execution, null, [], []));
            var context = (await ToolProducerContextAsync(fixture, execution)) with {
                Agent = catalog.Agents.Single(agent => agent.Id == execution.AgentId), Capabilities = capabilities
            };
            var source = new ProjectStructureWorkflowAuthorityService(services.GetRequiredService<ICanonicalRuntimeDatabase>(),
                services.GetRequiredService<IOptionsMonitor<ApiAccessOptions>>(), services.GetRequiredService<IAgentFrameworkWorkspaceService>(),
                services.GetRequiredService<IProcessRuntimeStateStore>(), services.GetRequiredService<IProcessRuntimeStepAssignmentStore>(), clock,
                services.GetRequiredService<IWorkflowScheduledAuthorityPolicy>(), revokeBeforeHeldRead
                    ? new WorkflowReadRace(fixture.Source, () => fixture.RevokeAsync(Revocation.Write)) : fixture.Source,
                services.GetRequiredService<IAgentExecutionProfileGenerationSource>(), services.GetRequiredService<ProjectWriteAdmissionService>(),
                NativeReader(services, clock), new EfProcessExecutionMutationGuard(NativeOptions<ProcessPersistenceDbContext>(services),
                    services.GetRequiredService<CoordinatedDatabaseTransaction>(), clock), NativeAuthority(services, clock), journal);
            var codec = new WorkflowProcessToolProposalCodec();
            var adapter = new WorkflowProcessToolAdmission(journal, source, codec,
                new Factory<WorkflowDbContext>(NativeOptions<WorkflowDbContext>(services), static options => new(options)));
            var input = new WorkflowAgentStartInput(Guid.NewGuid(), WorkflowAgentDefinitionSelectionMode.ExactSavedVersion, Guid.NewGuid());
            var owner = new PersistentWorkflowRunStore(new Factory<WorkflowDbContext>(NativeOptions<WorkflowDbContext>(services), static options => new(options)),
                services.GetRequiredService<IWorkflowScheduledSourceAuthorityPolicy>(), services.GetRequiredService<CoordinatedDatabaseTransaction>(), source, adapter);
            var launcher = new WorkflowOwnerLauncher(owner, clock);
            var provider = new WorkflowAgentRuntimeToolProvider(services.GetRequiredService<IWorkflowCatalogService>(), launcher,
                services.GetRequiredService<IWorkflowRuntimeManager>(), services.GetRequiredService<IWorkflowExternalResponseService>(),
                services.GetRequiredService<IWorkflowExternalResponseActorContextFactory>(), services.GetRequiredService<WorkflowAgentRuntimeAuthorizationService>(), source, adapter);
            return new(services, clock, fixture, execution, store, journal, context, input, codec.Prepare(input), source, adapter, provider, launcher);
        }
    }

    private sealed class WorkflowOwnerLauncher(PersistentWorkflowRunStore owner, NativeClock clock) : IWorkflowLaunchService {
        public int Calls { get; private set; }
        public async Task<WorkflowLaunchResult> LaunchAsync(WorkflowLaunchIntent intent, CancellationToken cancellationToken = default) {
            Calls++;
            var origin = Assert.IsType<WorkflowLaunchOrigin.ProcessToolInvocation>(intent.Origin);
            var selected = Assert.IsType<WorkflowDefinitionSelection.ExactSavedVersion>(intent.Selection);
            var run = new WorkflowRunSnapshot(origin.Invocation.PreparedRunId, selected.WorkflowId, selected.VersionId, WorkflowRunState.Running,
                WorkflowRuntimeBackendKind.InProcess, origin.Invocation.PreparedRunId.ToString(), "Admitted", clock.Now, clock.Now) { Origin = origin };
            await owner.CreateRunWithStartedEventAsync(run, WorkflowStarted(run), cancellationToken);
            var definition = new WorkflowDefinition(selected.WorkflowId, selected.VersionId, "Process tool fixture", "Owner admission fixture",
                WorkflowLifecycleStatus.Active, new(new("start"), [], []), new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false), clock.Now, clock.Now);
            var backend = new WorkflowRuntimeBackendCatalog([WorkflowRuntimeBackendKind.InProcess]).GetRequiredBackend(WorkflowRuntimeBackendKind.InProcess);
            return new(run, new(definition, intent.InputJson, backend, WorkflowPreviewSimulationPlan.Empty, intent.Mode, origin,
                intent.CompletionPolicy, intent.Idempotency, clock.Now), WorkflowLaunchIdempotencyDisposition.EnforcedNewRun);
        }
    }

    private sealed class WorkflowToolProbe(WorkflowRunId runId) : SaveChangesInterceptor, IDbTransactionInterceptor {
        public int Flushes { get; private set; }
        public int Commits { get; private set; }
        public Action? AfterFlush { get; init; }
        public Func<CancellationToken, Task>? BeforeCommit { get; init; }
        public bool BeforeCommitObserved { get; private set; }
        public Exception? AfterCommitFailure { get; init; }

        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            if (eventData.Context is WorkflowDbContext context && await context.Set<WorkflowRunRecordEntity>().AnyAsync(row => row.RunId == runId.Value, cancellationToken)) {
                Assert.NotNull(context.Database.CurrentTransaction);
                Assert.True(await context.Set<WorkflowEventRecordEntity>().AnyAsync(row => row.RunId == runId.Value, cancellationToken));
                Flushes++;
                AfterFlush?.Invoke();
            }
            return result;
        }

        public async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            if (BeforeCommit is not null) {
                await BeforeCommit(cancellationToken);
                BeforeCommitObserved = true;
            }
            return result;
        }

        public async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            await using var command = eventData.Context!.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT EXISTS (SELECT 1 FROM \"AgentFramework_WorkflowRuns\" WHERE \"RunId\" = @id)";
            command.Parameters.Add(new NpgsqlParameter<Guid>("id", runId.Value));
            if (await command.ExecuteScalarAsync(cancellationToken) is true) {
                Commits++;
                if (AfterCommitFailure is not null) {
                    throw AfterCommitFailure;
                }
            }
        }
    }

    private static async Task<ExecutionRunRecord> CreateClaimedWorkflowExecutionAsync(IServiceProvider services, Fixture fixture, NativeClock clock) {
        var preparation = ProcessPreparedLaunchFixture.Create(fixture.SavedAuthority, new(Guid.NewGuid()));
        var initial = preparation.InitialCommit;
        var assignment = Assert.Single(initial.InitialAssignments!) with {
            AllowedOperations = [ProcessOperationContractNames.ReadProjectStructure, ProcessOperationContractNames.ExecuteExternalAction, ProcessOperationContractNames.LaunchRuntime],
            LaunchVariables = new Dictionary<string, string> { [ProcessRuntimeLaunchVariables.ProjectId] = fixture.Project.ProjectId.ToString("D") }
        };
        var executionStore = Assert.IsType<FileSandboxWorkspaceStore>(services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>());
        var executor = fixture.Agent with {
            Id = Guid.Parse(assignment.ExecutorId), Name = "Assigned background executor", ConfigurationJson = "{}"
        };
        var catalog = await executionStore.UpdateCatalogAsync(current => current with { Agents = [.. current.Agents, executor] });
        Assert.Contains(catalog.Agents, agent => agent.Id == executor.Id);
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
            step.Status = ProcessRuntimeStepStatus.Running;
            step.AttemptNumber = 1;
            step.ActiveClaimToken = claim;
            context.DispatchClaims.Add(new() { RunId = assignment.RunId.Value, StepInstanceId = assignment.StepInstanceId.Value,
                ClaimToken = claim, OwnerId = "native-fixture", Status = DispatchClaimStatus.Claimed, AttemptNumber = 1,
                CreatedAtUtc = clock.Now.AddSeconds(-1), ExpiresAtUtc = clock.Now.AddHours(1) });
            await context.SaveChangesAsync();
        }
        var run = new ExecutionRunRecord(Guid.NewGuid(), Guid.Parse(assignment.ExecutorId), null, "Native Process executor", "process-step",
            assignment.StepKey, assignment.RunId.ToString(), assignment.StepInstanceId.ToString(), "process-runtime", "system",
            JsonSerializer.Serialize(new Dictionary<string, string> { ["agentProcessDispatchClaimIdentity"] = claim.ToString("D") }),
            "Execute.", "", "fixture", "fixture", ExecutionState.Running, null, clock.Now, clock.Now, clock.Now, null, "", null, [],
            ProcessRunId: assignment.RunId.ToString(), ProcessStepId: assignment.StepInstanceId.ToString());
        return run with { State = ExecutionState.Preparing };
    }

}
