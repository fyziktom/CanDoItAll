using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessProjectionPipelineTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Replay_worker_projects_live_history_run_detail_and_offsets()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var replay = new RecordingRuntimeEventReplayStore(
            StoredEvent(1, runId, ProcessRuntimeEventTypes.ProcessRunActivated, Now.AddMinutes(-5)),
            StoredEvent(2, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-4)));
        var clock = new FixedProcessProjectionClock(Now);
        var projector = new ProcessRuntimeProjectionProjector(store, ProcessProjectionJsonCodec.Default, clock);
        var worker = new ProcessProjectionReplayWorker(replay, store, projector, clock);

        var result = await worker.ReplayAsync(new ProcessProjectionReplayRequest(
            ProcessRuntimeProjectionProjector.ProjectorName,
            new ProcessProjectionShardKey("root-alpha"),
            Take: 10,
            LatestKnownGlobalSequence: 2));
        var query = new ProcessRuntimeProjectionQueryService(store, ProcessProjectionJsonCodec.Default, clock);
        var live = await query.GetLiveProcessesAsync(new ProcessLiveProcessesQuery(Now, TimeSpan.FromHours(1), Take: 10));
        var history = await query.GetRunHistoryAsync(new ProcessRunHistoryQuery(runId, Now.AddHours(-1), Now, Take: 10));
        var detail = await query.GetRunDetailAsync(new ProcessRunDetailQuery(runId));
        var offset = await store.LoadOffsetAsync(ProcessRuntimeProjectionProjector.ProjectorName, new ProcessProjectionShardKey("root-alpha"));

        Assert.Equal(ProcessProjectionReplayStatus.Completed, result.Status);
        Assert.Equal(2, result.ProcessedCount);
        Assert.NotNull(offset);
        Assert.Equal(2, offset.GlobalSequence);
        var run = Assert.Single(live.Runs);
        Assert.Equal(runId, run.RunId);
        Assert.True(run.IsActive);
        Assert.Equal(ProcessProjectedRunStatus.Active, run.Status);
        Assert.Equal(2, run.Freshness.SourceGlobalSequence);
        Assert.Equal(0, run.Freshness.Lag.BacklogEventCount);
        Assert.Equal([1, 2], history.Events.Select(runtimeEvent => runtimeEvent.GlobalSequence));
        Assert.NotNull(detail);
        Assert.Equal(runId, detail.RunId);
        Assert.Equal(ProcessProjectedRunStatus.Active, detail.Status);
    }

    [Fact]
    public async Task Replay_worker_dead_letters_failed_projection_without_advancing_offset()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var replay = new RecordingRuntimeEventReplayStore(
            StoredEvent(3, runId, ProcessRuntimeEventTypes.StepFailed, Now));
        var worker = new ProcessProjectionReplayWorker(
            replay,
            store,
            new ThrowingRuntimeProjector(new ProcessProjectorName("runtime.throwing")),
            new FixedProcessProjectionClock(Now));

        var result = await worker.ReplayAsync(new ProcessProjectionReplayRequest(
            new ProcessProjectorName("runtime.throwing"),
            new ProcessProjectionShardKey("root-alpha"),
            Take: 10,
            LatestKnownGlobalSequence: 3));
        var offset = await store.LoadOffsetAsync(new ProcessProjectorName("runtime.throwing"), new ProcessProjectionShardKey("root-alpha"));
        var deadLetters = await store.ReadDeadLettersAsync(new ProcessProjectorName("runtime.throwing"), new ProcessProjectionShardKey("root-alpha"), 10);

        Assert.Equal(ProcessProjectionReplayStatus.DeadLettered, result.Status);
        Assert.Equal(0, result.ProcessedCount);
        Assert.Null(offset);
        var deadLetter = Assert.Single(deadLetters);
        Assert.Equal(3, deadLetter.GlobalSequence);
        Assert.Equal("InvalidOperationException", deadLetter.ErrorClass);
    }

    [Fact]
    public async Task Live_last_hour_query_excludes_old_completed_runs()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.ProcessRunCompleted, Now.AddHours(-2)),
            latestKnownGlobalSequence: 1);
        var query = new ProcessRuntimeProjectionQueryService(store, ProcessProjectionJsonCodec.Default, new FixedProcessProjectionClock(Now));

        var live = await query.GetLiveProcessesAsync(new ProcessLiveProcessesQuery(Now, TimeSpan.FromHours(1), Take: 10));

        Assert.Empty(live.Runs);
    }

    [Fact]
    public async Task Live_last_hour_query_excludes_stale_active_runs()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddHours(-2)),
            latestKnownGlobalSequence: 1);
        var query = new ProcessRuntimeProjectionQueryService(store, ProcessProjectionJsonCodec.Default, new FixedProcessProjectionClock(Now));

        var live = await query.GetLiveProcessesAsync(new ProcessLiveProcessesQuery(Now, TimeSpan.FromHours(1), Take: 10));

        Assert.Empty(live.Runs);
    }

    [Fact]
    public async Task Runtime_workspace_projects_active_agents_from_running_step_assignments()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var stepId = ProcessStepInstanceId.New();
        var claimToken = DispatchClaimToken.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-5)),
            latestKnownGlobalSequence: 1);
        var state = new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                new ProcessRuntimeStepState(
                    stepId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Running,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    claimToken,
                    CompletedResultKey: null)
            ],
            [
                new DispatchClaimState(
                    claimToken,
                    stepId,
                    new DispatcherOwnerId("unit-test-dispatcher"),
                    DispatchClaimStatus.Claimed,
                    AttemptNumber: 1,
                    Now.AddMinutes(-5),
                    Now.AddMinutes(25),
                    RenewedAtUtc: null,
                    ResultIdempotencyKey: null)
            ],
            [],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-1));
        var assignment = new ProcessRuntimeStepAssignment(
            runId,
            planId,
            stepId,
            "implementation",
            "lead-engineer",
            "lead-engineer",
            "Lead engineer",
            ProcessLaunchExecutorKinds.Agent,
            "agent-dotnet-developer",
            ".NET Developer",
            "Implement the selected slice.",
            "sha256:readiness",
            "Matched role and workspace tool readiness.",
            [],
            [],
            [ProcessOperationContractNames.MutateProductTarget, ProcessOperationContractNames.RunValidation],
            ProcessOperationContractNames.ExternalProductTargetMutable,
            new Dictionary<string, string>(),
            BranchGate: null,
            Now.AddMinutes(-6));
        var query = new ProcessRuntimeProjectionQueryService(
            store,
            ProcessProjectionJsonCodec.Default,
            new FixedProcessProjectionClock(Now),
            new InMemoryRuntimeStateStore(state),
            new InMemoryAssignmentStore([assignment]));

        var workspace = await query.GetRuntimeWorkspaceAsync(new ProcessRuntimeWorkspaceQuery(
            Now,
            TimeSpan.FromHours(1),
            EventPage: 0,
            EventPageSize: 10,
            TakeRuns: 10,
            SelectedRunId: null));

        var activeAgent = Assert.Single(workspace.ActiveAgents);
        Assert.Equal(runId.Value, activeAgent.RunId);
        Assert.Equal(stepId.Value, activeAgent.StepInstanceId);
        Assert.Equal("implementation", activeAgent.StepKey);
        Assert.Equal("lead-engineer", activeAgent.RoleKey);
        Assert.Equal(".NET Developer", activeAgent.ExecutorDisplayName);
        Assert.Equal(ProcessRuntimeStepStatus.Running.ToString(), activeAgent.Status);
        Assert.True(activeAgent.IsWorking);
        Assert.False(activeAgent.IsLeaseExpired);
        Assert.Equal(Now.AddMinutes(-5), activeAgent.ClaimedAtUtc);
        Assert.Equal(Now.AddMinutes(25), activeAgent.LeaseExpiresAtUtc);
        var run = Assert.Single(workspace.Runs);
        Assert.Equal(1, run.ExecutableStepCount);
        Assert.Equal(0, run.CompletedStepCount);
        Assert.Equal(0, run.TerminalStepCount);
        Assert.Equal("0 of 1 executable steps complete", run.ProgressLabel);
    }

    [Fact]
    public async Task Live_process_query_enriches_run_metadata_from_launch_variables()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var parentRunId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var stepId = ProcessStepInstanceId.New();
        var parentStepId = ProcessStepInstanceId.New();
        var claimToken = DispatchClaimToken.New();
        var projectId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-5)),
            latestKnownGlobalSequence: 1);
        var state = new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                CreateStepState(stepId, claimToken)
            ],
            [
                CreateClaim(stepId, claimToken, Now.AddMinutes(-1), Now.AddMinutes(29))
            ],
            [],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-1));
        var assignment = CreateAssignment(
            runId,
            planId,
            stepId,
            "implementation",
            ".NET Developer") with
        {
            LaunchVariables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ProcessRuntimeLaunchVariables.ProjectId] = projectId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ProjectName] = "Apollo Delivery",
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = parentRunId.Value.ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] = parentStepId.Value.ToString("D"),
                [ProcessRuntimeLaunchVariables.ProcessDefinitionName] = "Project-scoped implementation subprocess"
            }
        };
        var query = new ProcessRuntimeProjectionQueryService(
            store,
            ProcessProjectionJsonCodec.Default,
            new FixedProcessProjectionClock(Now),
            new InMemoryRuntimeStateStore(state),
            new InMemoryAssignmentStore([assignment]));

        var live = await query.GetLiveProcessesAsync(new ProcessLiveProcessesQuery(Now, TimeSpan.FromHours(1), Take: 10));

        var run = Assert.Single(live.Runs);
        Assert.Equal(projectId, run.ProjectId);
        Assert.Equal("Apollo Delivery", run.ProjectName);
        Assert.True(run.IsSubprocess);
        Assert.Equal("Project-scoped implementation subprocess", run.ProcessName);
    }

    [Fact]
    public async Task Runtime_workspace_list_only_skips_detail_history_metrics_and_runtime_enrichment()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-5)),
            latestKnownGlobalSequence: 1);
        var countingStore = new CountingProjectionStore(store);
        var query = new ProcessRuntimeProjectionQueryService(
            countingStore,
            ProcessProjectionJsonCodec.Default,
            new FixedProcessProjectionClock(Now),
            new ThrowingRuntimeStateStore(),
            new ThrowingAssignmentStore(),
            new ThrowingObservationReader());

        var workspace = await query.GetRuntimeWorkspaceAsync(new ProcessRuntimeWorkspaceQuery(
            Now,
            TimeSpan.FromHours(1),
            EventPage: 0,
            EventPageSize: 10,
            TakeRuns: 10,
            runId,
            AutoSelectRun: true,
            ProcessRuntimeWorkspaceLoadOptions.ListOnly));

        var run = Assert.Single(workspace.Runs);
        Assert.Equal(runId, run.RunId);
        Assert.Null(workspace.SelectedRun);
        Assert.Empty(workspace.Events);
        Assert.Empty(workspace.MetricEvents);
        Assert.Empty(workspace.ActiveAgents);
        Assert.Equal(1, countingStore.ReadSnapshotsCallCount);
        Assert.Equal(0, countingStore.LoadSnapshotCallCount);
        Assert.Equal(0, countingStore.ReadHistoryCallCount);
    }

    [Fact]
    public async Task Live_process_query_projects_parent_waiting_on_active_child_run()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var parentPlanId = ProcessInstancePlanId.New();
        var childPlanId = ProcessInstancePlanId.New();
        var parentStepId = ProcessStepInstanceId.New();
        var childStepId = ProcessStepInstanceId.New();
        await ProjectAsync(
            store,
            StoredEvent(1, parentRunId, ProcessRuntimeEventTypes.StepWaiting, Now.AddMinutes(-5)),
            latestKnownGlobalSequence: 1);
        var parentState = new ProcessRuntimeStateSnapshot(
            parentRunId,
            parentRunId,
            parentPlanId,
            "sha256:parent-plan",
            ProcessRuntimeStatus.Active,
            [
                new ProcessRuntimeStepState(
                    parentStepId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Waiting,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: null,
                    CompletedResultKey: null)
            ],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-4));
        var childState = new ProcessRuntimeStateSnapshot(
            parentRunId,
            childRunId,
            childPlanId,
            "sha256:child-plan",
            ProcessRuntimeStatus.Active,
            [
                new ProcessRuntimeStepState(
                    childStepId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Blocked,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: null,
                    CompletedResultKey: null)
            ],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-3));
        var parentAssignment = CreateAssignment(
            parentRunId,
            parentPlanId,
            parentStepId,
            "capture-ui-screenshots",
            "Delivery QA Observer");
        var childAssignment = CreateAssignment(
            childRunId,
            childPlanId,
            childStepId,
            "store-ui-screenshots",
            "Delivery QA Observer") with
        {
            LaunchVariables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ParentProcessRunId"] = parentRunId.Value.ToString("D"),
                ["ParentProcessStepId"] = parentStepId.Value.ToString("D")
            }
        };
        var query = new ProcessRuntimeProjectionQueryService(
            store,
            ProcessProjectionJsonCodec.Default,
            new FixedProcessProjectionClock(Now),
            new InMemoryRuntimeStateStore(parentState, childState),
            new InMemoryAssignmentStore([parentAssignment, childAssignment]));

        var live = await query.GetLiveProcessesAsync(new ProcessLiveProcessesQuery(Now, TimeSpan.FromHours(1), Take: 10));

        var run = Assert.Single(live.Runs);
        var wait = Assert.Single(run.WaitingOnChildRuns);
        Assert.Equal(parentRunId.Value, wait.ParentRunId);
        Assert.Equal(parentStepId.Value, wait.ParentStepInstanceId);
        Assert.Equal("capture-ui-screenshots", wait.ParentStepKey);
        Assert.Equal(childRunId.Value, wait.ChildRunId);
        Assert.Equal(ProcessRuntimeStatus.Active.ToString(), wait.ChildRunStatus);
        Assert.Equal("store-ui-screenshots", wait.ChildStepKey);
        Assert.Equal(ProcessRuntimeStepStatus.Blocked.ToString(), wait.ChildStepStatus);
        Assert.Contains("waiting on child run", wait.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Live_process_query_projects_current_step_from_runtime_state()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var runningStepId = ProcessStepInstanceId.New();
        var blockedPlaceholderId = ProcessStepInstanceId.New();
        var claimToken = DispatchClaimToken.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-5)),
            latestKnownGlobalSequence: 1);
        var state = new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                new ProcessRuntimeStepState(
                    blockedPlaceholderId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Blocked,
                    IsExecutable: true,
                    AttemptNumber: 0,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: null,
                    CompletedResultKey: null),
                CreateStepState(runningStepId, claimToken)
            ],
            [
                CreateClaim(runningStepId, claimToken, Now.AddMinutes(-1), Now.AddMinutes(29))
            ],
            [],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-1));
        var assignment = CreateAssignment(
            runId,
            planId,
            runningStepId,
            "architecture-review",
            "Software Architect");
        var query = new ProcessRuntimeProjectionQueryService(
            store,
            ProcessProjectionJsonCodec.Default,
            new FixedProcessProjectionClock(Now),
            new InMemoryRuntimeStateStore(state),
            new InMemoryAssignmentStore([assignment]));

        var live = await query.GetLiveProcessesAsync(new ProcessLiveProcessesQuery(Now, TimeSpan.FromHours(1), Take: 10));

        var run = Assert.Single(live.Runs);
        Assert.NotNull(run.CurrentStep);
        Assert.Equal("architecture-review", run.CurrentStep.StepKey);
        Assert.Equal(ProcessRuntimeStepStatus.Running.ToString(), run.CurrentStep.StepStatus);
        Assert.Equal("Software Architect", run.CurrentStep.ExecutorDisplayName);
        Assert.True(run.CurrentStep.IsWorking);
        Assert.False(run.CurrentStep.IsLeaseExpired);
        Assert.Contains("architecture-review is Running", run.CurrentStep.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Live_process_query_marks_idle_attention_run_inactive_but_keeps_current_step_and_rework_action()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var blockedStepId = ProcessStepInstanceId.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.StepBlocked, Now.AddMinutes(-5)),
            latestKnownGlobalSequence: 1);
        var state = new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                new ProcessRuntimeStepState(
                    blockedStepId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Blocked,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: null,
                    CompletedResultKey: StrategyResultIdempotencyKey.New())
            ],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-1));
        var query = new ProcessRuntimeProjectionQueryService(
            store,
            ProcessProjectionJsonCodec.Default,
            new FixedProcessProjectionClock(Now),
            new InMemoryRuntimeStateStore(state),
            new InMemoryAssignmentStore(
            [
                CreateAssignment(runId, planId, blockedStepId, "capture-ui-screenshots", "Delivery QA Observer")
            ]));

        var live = await query.GetLiveProcessesAsync(new ProcessLiveProcessesQuery(Now, TimeSpan.FromHours(1), Take: 10));

        var run = Assert.Single(live.Runs);
        Assert.Equal(ProcessProjectedRunStatus.NeedsAttention, run.Status);
        Assert.False(run.IsActive);
        Assert.NotNull(run.CurrentStep);
        Assert.Equal("capture-ui-screenshots", run.CurrentStep.StepKey);
        Assert.Equal(ProcessRuntimeStepStatus.Blocked.ToString(), run.CurrentStep.StepStatus);
        Assert.False(run.CurrentStep.IsWorking);
        var action = Assert.Single(run.OperatorActions);
        Assert.Equal(blockedStepId.Value, action.StepInstanceId);
        Assert.Equal(ProcessRuntimeOperatorActionKind.RequestRework, action.Kind);
    }

    [Fact]
    public async Task Live_process_query_keeps_attention_run_active_when_claim_work_is_open()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var runningStepId = ProcessStepInstanceId.New();
        var claimToken = DispatchClaimToken.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.StepBlocked, Now.AddMinutes(-5)),
            latestKnownGlobalSequence: 1);
        var state = new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                CreateStepState(runningStepId, claimToken)
            ],
            [
                CreateClaim(runningStepId, claimToken, Now.AddMinutes(-1), Now.AddMinutes(29))
            ],
            [],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-1));
        var query = new ProcessRuntimeProjectionQueryService(
            store,
            ProcessProjectionJsonCodec.Default,
            new FixedProcessProjectionClock(Now),
            new InMemoryRuntimeStateStore(state),
            new InMemoryAssignmentStore(
            [
                CreateAssignment(runId, planId, runningStepId, "security-review", "Security Reviewer")
            ]));

        var live = await query.GetLiveProcessesAsync(new ProcessLiveProcessesQuery(Now, TimeSpan.FromHours(1), Take: 10));

        var run = Assert.Single(live.Runs);
        Assert.Equal(ProcessProjectedRunStatus.NeedsAttention, run.Status);
        Assert.True(run.IsActive);
        Assert.NotNull(run.CurrentStep);
        Assert.Equal("security-review", run.CurrentStep.StepKey);
        Assert.True(run.CurrentStep.IsWorking);
    }

    [Fact]
    public async Task Runtime_workspace_projects_operator_rework_actions_for_blocked_and_failed_steps()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var blockedStepId = ProcessStepInstanceId.New();
        var failedStepId = ProcessStepInstanceId.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.StepBlocked, Now.AddMinutes(-5)),
            latestKnownGlobalSequence: 1);
        var state = new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                new ProcessRuntimeStepState(
                    blockedStepId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Blocked,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: null,
                    CompletedResultKey: StrategyResultIdempotencyKey.New()),
                new ProcessRuntimeStepState(
                    failedStepId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Failed,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: null,
                    CompletedResultKey: StrategyResultIdempotencyKey.New())
            ],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-1));
        var query = new ProcessRuntimeProjectionQueryService(
            store,
            ProcessProjectionJsonCodec.Default,
            new FixedProcessProjectionClock(Now),
            new InMemoryRuntimeStateStore(state),
            new InMemoryAssignmentStore(
            [
                CreateAssignment(runId, planId, blockedStepId, "implement-code-change", ".NET Application Developer"),
                CreateAssignment(runId, planId, failedStepId, "feature-repair", ".NET Application Developer")
            ]));

        var workspace = await query.GetRuntimeWorkspaceAsync(new ProcessRuntimeWorkspaceQuery(
            Now,
            TimeSpan.FromHours(1),
            EventPage: 0,
            EventPageSize: 10,
            TakeRuns: 10,
            SelectedRunId: null));

        var run = Assert.Single(workspace.Runs);
        Assert.Collection(
            run.OperatorActions,
            action =>
            {
                Assert.Equal(failedStepId.Value, action.StepInstanceId);
                Assert.Equal("feature-repair", action.StepKey);
                Assert.Equal(ProcessRuntimeOperatorActionKind.RequestRework, action.Kind);
                Assert.True(action.IsEnabled);
            },
            action =>
            {
                Assert.Equal(blockedStepId.Value, action.StepInstanceId);
                Assert.Equal("implement-code-change", action.StepKey);
                Assert.Equal(".NET Application Developer", action.ExecutorDisplayName);
            });
    }

    [Fact]
    public async Task Runtime_workspace_operator_actions_include_execution_result_summary_for_blocked_repair_branch()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var blockedStepId = ProcessStepInstanceId.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.StepBlocked, Now.AddMinutes(-5)),
            latestKnownGlobalSequence: 1);
        var state = new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                new ProcessRuntimeStepState(
                    blockedStepId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Blocked,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: null,
                    CompletedResultKey: StrategyResultIdempotencyKey.New())
            ],
            [],
            [
                new StrategyResultReceipt(
                    blockedStepId,
                    new StrategyId("strategy.adapter.workflow.execute"),
                    StrategyResultIdempotencyKey.New(),
                    StrategyOutcome.NeedsManager,
                    ProcessRuntimeStepStatus.Blocked,
                    "sha256:result")
            ],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-1));
        var resultSummary = """
            {"status":"Blocked","reason":"Browser QA found the Compute control clipped at the mobile viewport.","branchOutcomeKey":"repair-required","branchOutcomeTitle":"Repair required","nextActions":["Widen or reflow the Compute button and rerun browser proof."]}
            """;
        var query = new ProcessRuntimeProjectionQueryService(
            store,
            ProcessProjectionJsonCodec.Default,
            new FixedProcessProjectionClock(Now),
            new InMemoryRuntimeStateStore(state),
            new InMemoryAssignmentStore(
            [
                CreateAssignment(runId, planId, blockedStepId, "qa-validation", "Delivery QA Observer")
            ]),
            new InMemoryObservationReader(
                CreateObservation(
                    runId,
                    blockedStepId,
                    "Delivery QA Observer",
                    "Completed",
                    Now.AddMinutes(-1),
                    resultSummary: resultSummary)));

        var workspace = await query.GetRuntimeWorkspaceAsync(new ProcessRuntimeWorkspaceQuery(
            Now,
            TimeSpan.FromHours(1),
            EventPage: 0,
            EventPageSize: 10,
            TakeRuns: 10,
            SelectedRunId: null));

        var run = Assert.Single(workspace.Runs);
        var action = Assert.Single(run.OperatorActions);
        Assert.Contains("Compute control clipped", action.ProblemSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("branch repair-required", action.ProblemSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("return a completed process-step outcome", action.RequiredOperatorDecision, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("branchOutcomeKey 'repair-required'", action.RequiredOperatorDecision, StringComparison.Ordinal);
        Assert.Contains("Widen or reflow the Compute button", action.RecommendedInstruction, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Runtime_projection_readback_includes_blocked_result_diagnostics_and_lineage()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var blockedStepId = ProcessStepInstanceId.New();
        var slotId = ArtifactSlotId.New();
        var artifactId = ArtifactInstanceId.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.StepBlocked, Now.AddMinutes(-5)),
            latestKnownGlobalSequence: 1);
        var state = new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                new ProcessRuntimeStepState(
                    blockedStepId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Blocked,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: null,
                    CompletedResultKey: StrategyResultIdempotencyKey.New())
            ],
            [],
            [
                new StrategyResultReceipt(
                    blockedStepId,
                    new StrategyId("strategy.adapter.workflow.execute"),
                    StrategyResultIdempotencyKey.New(),
                    StrategyOutcome.NeedsManager,
                    ProcessRuntimeStepStatus.Blocked,
                    "sha256:blocked-result",
                    [
                        new StrategyResultDiagnosticReceipt(
                            "process.runtime.missing_artifact",
                            StrategyDiagnosticSensitivity.Normal,
                            "sha256:diagnostic",
                            "Required artifact was not produced.",
                            RestrictedEvidenceReference: null,
                            ProcessDiagnosticRetrySafety.UnsafeToRetry,
                            ProcessDiagnosticIdempotencyClassification.Idempotent)
                    ],
                    [
                        new StrategyResultArtifactReceipt(
                            slotId,
                            artifactId,
                            "sha256:artifact")
                    ],
                    new ProcessRecoveryDecisionReceipt(
                        ProcessFailureCategory.MissingArtifact,
                        ProcessRecoveryDecisionKind.ManagerRequired,
                        "process.runtime.missing_artifact",
                        "process.manager-review-required",
                        "Manager review is required."))
            ],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-1));
        var query = new ProcessRuntimeProjectionQueryService(
            store,
            ProcessProjectionJsonCodec.Default,
            new FixedProcessProjectionClock(Now),
            new InMemoryRuntimeStateStore(state),
            new InMemoryAssignmentStore(
            [
                CreateAssignment(runId, planId, blockedStepId, "produce-evidence", "Process Worker")
            ]));

        var detail = await query.GetRunDetailAsync(new ProcessRunDetailQuery(runId));
        var history = await query.GetRunHistoryAsync(new ProcessRunHistoryQuery(runId, Now.AddHours(-1), Now, Take: 10));
        var live = await query.GetLiveProcessesAsync(new ProcessLiveProcessesQuery(Now, TimeSpan.FromHours(1), Take: 10));

        Assert.NotNull(detail);
        var detailDiagnostic = Assert.Single(detail.Diagnostics);
        Assert.Equal("process.runtime.missing_artifact", detailDiagnostic.Code);
        Assert.Equal("Runtime", detailDiagnostic.Category);
        Assert.Equal("produce-evidence", detailDiagnostic.StepKey);
        var lineage = Assert.Single(detail.ResultLineage);
        Assert.Equal(blockedStepId.Value, lineage.StepInstanceId);
        Assert.Equal(artifactId.Value, Assert.Single(lineage.ProducedArtifacts).ArtifactId);
        Assert.NotNull(lineage.RecoveryDecision);
        Assert.Equal("MissingArtifact", lineage.RecoveryDecision.FailureCategory);
        Assert.Equal("ManagerRequired", lineage.RecoveryDecision.DecisionKind);
        Assert.Contains("Required artifact was not produced", Assert.Single(history.Events).Summary, StringComparison.Ordinal);
        var liveRun = Assert.Single(live.Runs);
        Assert.Equal("process.runtime.missing_artifact", Assert.Single(liveRun.Diagnostics).Code);
        Assert.Equal("process.runtime.missing_artifact", Assert.Single(liveRun.CurrentStep!.Diagnostics).Code);
        Assert.Equal(slotId.Value, Assert.Single(liveRun.CurrentStep.ProducedArtifacts).SlotId);
    }

    [Fact]
    public async Task Runtime_workspace_operator_actions_include_failed_tool_receipts_for_blocked_step()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var blockedStepId = ProcessStepInstanceId.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.StepBlocked, Now.AddMinutes(-5)),
            latestKnownGlobalSequence: 1);
        var state = new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                new ProcessRuntimeStepState(
                    blockedStepId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Blocked,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: null,
                    CompletedResultKey: StrategyResultIdempotencyKey.New())
            ],
            [],
            [
                new StrategyResultReceipt(
                    blockedStepId,
                    new StrategyId("strategy.adapter.workflow.execute"),
                    StrategyResultIdempotencyKey.New(),
                    StrategyOutcome.NeedsManager,
                    ProcessRuntimeStepStatus.Blocked,
                    "sha256:result")
            ],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-1));
        var query = new ProcessRuntimeProjectionQueryService(
            store,
            ProcessProjectionJsonCodec.Default,
            new FixedProcessProjectionClock(Now),
            new InMemoryRuntimeStateStore(state),
            new InMemoryAssignmentStore(
            [
                CreateAssignment(runId, planId, blockedStepId, "targeted-validation", ".NET QA Review Lead")
            ]),
            new InMemoryObservationReader(
                CreateObservation(
                    runId,
                    blockedStepId,
                    ".NET QA Review Lead",
                    "Failed",
                    Now.AddMinutes(-1),
                    recentTools:
                    [
                        new ProcessExecutionToolObservation(
                            "workspace_dotnet_test",
                            "workspace",
                            @"targetPath=external-target/C/programovani/dotnet/calculator-output/Calculator.slnx arguments=-c Debug",
                            "Failed (exit 1)",
                            Now.AddMinutes(-3),
                            Now.AddMinutes(-2))
                    ],
                    artifacts:
                    [
                        new ProcessExecutionArtifactObservation(
                            "workspace-tool-output",
                            "dotnet_test stderr",
                            "managed-files/process-runs/run/steps/targeted-validation/dotnet-test-stderr.log",
                            "Captured stderr.",
                            Now.AddMinutes(-2))
                        {
                            ProducedBy = "workspace_dotnet_test"
                        },
                        new ProcessExecutionArtifactObservation(
                            "workspace-tool-output",
                            "dotnet_test stdout",
                            "managed-files/process-runs/run/steps/targeted-validation/dotnet-test-stdout.log",
                            "Captured stdout.",
                            Now.AddMinutes(-2))
                        {
                            ProducedBy = "workspace_dotnet_test"
                        }
                    ],
                    lastError: "Failed to convert System.Reflection.TypeExtensions.dll to webcil: Access to the path is denied.")));

        var workspace = await query.GetRuntimeWorkspaceAsync(new ProcessRuntimeWorkspaceQuery(
            Now,
            TimeSpan.FromHours(1),
            EventPage: 0,
            EventPageSize: 10,
            TakeRuns: 10,
            SelectedRunId: null));

        var run = Assert.Single(workspace.Runs);
        var action = Assert.Single(run.OperatorActions);
        Assert.Contains("workspace_dotnet_test", action.ProblemSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Calculator.slnx", action.ProblemSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Access to the path is denied", action.ProblemSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dotnet_test stderr", action.ProblemSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("blind retry", action.ProblemSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Previous failed tool receipt", action.RecommendedInstruction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Inspect the listed stdout/stderr", action.RecommendedInstruction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Runtime_workspace_operator_actions_are_suppressed_while_claim_is_open()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var blockedStepId = ProcessStepInstanceId.New();
        var runningStepId = ProcessStepInstanceId.New();
        var claimToken = DispatchClaimToken.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.StepBlocked, Now.AddMinutes(-5)),
            latestKnownGlobalSequence: 1);
        var state = new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                new ProcessRuntimeStepState(
                    blockedStepId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Blocked,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: null,
                    CompletedResultKey: StrategyResultIdempotencyKey.New()),
                CreateStepState(runningStepId, claimToken)
            ],
            [
                CreateClaim(runningStepId, claimToken, Now.AddMinutes(-1), Now.AddMinutes(29))
            ],
            [],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-1));
        var query = new ProcessRuntimeProjectionQueryService(
            store,
            ProcessProjectionJsonCodec.Default,
            new FixedProcessProjectionClock(Now),
            new InMemoryRuntimeStateStore(state),
            new InMemoryAssignmentStore(
            [
                CreateAssignment(runId, planId, blockedStepId, "feature-handoff-after-repair", "Delivery Manager"),
                CreateAssignment(runId, planId, runningStepId, "feature-repair", ".NET Application Developer")
            ]));

        var workspace = await query.GetRuntimeWorkspaceAsync(new ProcessRuntimeWorkspaceQuery(
            Now,
            TimeSpan.FromHours(1),
            EventPage: 0,
            EventPageSize: 10,
            TakeRuns: 10,
            SelectedRunId: null));

        var run = Assert.Single(workspace.Runs);
        Assert.Empty(run.OperatorActions);
    }

    [Fact]
    public async Task Runtime_workspace_projects_retry_action_for_expired_active_claim()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var stepId = ProcessStepInstanceId.New();
        var claimToken = DispatchClaimToken.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-5)),
            latestKnownGlobalSequence: 1);
        var state = new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                CreateStepState(stepId, claimToken)
            ],
            [
                CreateClaim(stepId, claimToken, Now.AddMinutes(-7), Now.AddMinutes(-2))
            ],
            [],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-2));
        var query = new ProcessRuntimeProjectionQueryService(
            store,
            ProcessProjectionJsonCodec.Default,
            new FixedProcessProjectionClock(Now),
            new InMemoryRuntimeStateStore(state),
            new InMemoryAssignmentStore(
            [
                CreateAssignment(runId, planId, stepId, "architecture-review", ".NET Solution Architect")
            ]));

        var workspace = await query.GetRuntimeWorkspaceAsync(new ProcessRuntimeWorkspaceQuery(
            Now,
            TimeSpan.FromHours(1),
            EventPage: 0,
            EventPageSize: 10,
            TakeRuns: 10,
            SelectedRunId: null));

        var run = Assert.Single(workspace.Runs);
        var action = Assert.Single(run.OperatorActions);
        Assert.Equal("Retry expired claim", action.Label);
        Assert.Equal("architecture-review", action.StepKey);
        Assert.Contains("dispatch lease expired", action.ProblemSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".NET Solution Architect", action.RequiredOperatorDecision, StringComparison.Ordinal);
        Assert.Contains("Preserve any managed artifacts", action.RecommendedInstruction, StringComparison.Ordinal);
        Assert.True(action.PrimaryRootCause);
    }

    [Fact]
    public async Task Runtime_workspace_operator_actions_skip_dependency_gated_blocked_steps()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var dependencyStepId = ProcessStepInstanceId.New();
        var blockedStepId = ProcessStepInstanceId.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.StepBlocked, Now.AddMinutes(-5)),
            latestKnownGlobalSequence: 1);
        var state = new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                new ProcessRuntimeStepState(
                    dependencyStepId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Pending,
                    IsExecutable: true,
                    AttemptNumber: 0,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: null,
                    CompletedResultKey: null),
                new ProcessRuntimeStepState(
                    blockedStepId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Blocked,
                    IsExecutable: true,
                    AttemptNumber: 0,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId> { dependencyStepId },
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: null,
                    CompletedResultKey: null)
            ],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-1));
        var query = new ProcessRuntimeProjectionQueryService(
            store,
            ProcessProjectionJsonCodec.Default,
            new FixedProcessProjectionClock(Now),
            new InMemoryRuntimeStateStore(state),
            new InMemoryAssignmentStore(
            [
                CreateAssignment(runId, planId, dependencyStepId, "feature-repair", ".NET Application Developer"),
                CreateAssignment(runId, planId, blockedStepId, "feature-handoff-after-repair", "Delivery Manager")
            ]));

        var workspace = await query.GetRuntimeWorkspaceAsync(new ProcessRuntimeWorkspaceQuery(
            Now,
            TimeSpan.FromHours(1),
            EventPage: 0,
            EventPageSize: 10,
            TakeRuns: 10,
            SelectedRunId: null));

        var run = Assert.Single(workspace.Runs);
        Assert.Empty(run.OperatorActions);
    }

    [Fact]
    public async Task Runtime_workspace_active_agents_include_only_live_nonstale_execution_observations()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var runningStepId = ProcessStepInstanceId.New();
        var completedStepId = ProcessStepInstanceId.New();
        var staleStepId = ProcessStepInstanceId.New();
        var claimToken = DispatchClaimToken.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-5)),
            latestKnownGlobalSequence: 1);
        var state = new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                CreateStepState(runningStepId, claimToken)
            ],
            [
                CreateClaim(runningStepId, claimToken, Now.AddMinutes(-1), Now.AddMinutes(29))
            ],
            [],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-1));
        var assignments = new[]
        {
            CreateAssignment(runId, planId, runningStepId, "implementation", ".NET Developer"),
                CreateAssignment(runId, planId, completedStepId, "peer-review", ".NET QA Review Lead"),
                CreateAssignment(runId, planId, staleStepId, "qa-validation", "Delivery QA Observer")
        };
        const string avatarImageUrl = "_content/CanDoItAll.Components.BaseLib/assets/identity/avatars/avatar-04.jpg";
        var query = new ProcessRuntimeProjectionQueryService(
            store,
            ProcessProjectionJsonCodec.Default,
            new FixedProcessProjectionClock(Now),
            new InMemoryRuntimeStateStore(state),
            new InMemoryAssignmentStore(assignments),
            new InMemoryObservationReader(
                CreateObservation(runId, runningStepId, ".NET Developer", "Running", Now.AddMinutes(-1), avatarImageUrl),
                CreateObservation(runId, completedStepId, ".NET QA Review Lead", "Completed", Now.AddMinutes(-1)),
                CreateObservation(runId, staleStepId, "Delivery QA Observer", "Running", Now.AddMinutes(-31))));

        var workspace = await query.GetRuntimeWorkspaceAsync(new ProcessRuntimeWorkspaceQuery(
            Now,
            TimeSpan.FromHours(1),
            EventPage: 0,
            EventPageSize: 10,
            TakeRuns: 10,
            SelectedRunId: null));

        var activeAgent = Assert.Single(workspace.ActiveAgents);
        Assert.Equal(runningStepId.Value, activeAgent.StepInstanceId);
        Assert.Equal(".NET Developer", activeAgent.ExecutorDisplayName);
        Assert.Equal(avatarImageUrl, activeAgent.AgentAvatarImageUrl);
        Assert.True(activeAgent.IsWorking);
        Assert.Equal("AgentFramework execution run", activeAgent.ObservationSource);
    }

    [Fact]
    public async Task Runtime_workspace_can_keep_all_runs_unselected()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var firstRunId = ProcessRunId.New();
        var secondRunId = ProcessRunId.New();
        await ProjectAsync(
            store,
            StoredEvent(1, firstRunId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-5)),
            latestKnownGlobalSequence: 2);
        await ProjectAsync(
            store,
            StoredEvent(2, secondRunId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-4)),
            latestKnownGlobalSequence: 2);
        var query = new ProcessRuntimeProjectionQueryService(store, ProcessProjectionJsonCodec.Default, new FixedProcessProjectionClock(Now));

        var workspace = await query.GetRuntimeWorkspaceAsync(new ProcessRuntimeWorkspaceQuery(
            Now,
            TimeSpan.FromHours(1),
            EventPage: 0,
            EventPageSize: 10,
            TakeRuns: 10,
            SelectedRunId: null,
            AutoSelectRun: false));

        Assert.Null(workspace.SelectedRun);
        Assert.Equal(2, workspace.Runs.Count);
        Assert.Equal([1, 2], workspace.Events.Select(runtimeEvent => runtimeEvent.GlobalSequence));
    }

    [Fact]
    public async Task Runtime_workspace_active_agents_do_not_scan_observations_without_active_runtime_steps()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var stepId = ProcessStepInstanceId.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-5)),
            latestKnownGlobalSequence: 1);
        var state = new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-1));
        var observationReader = new InMemoryObservationReader(
            CreateObservation(runId, stepId, ".NET Developer", "Running", Now.AddMinutes(-1)));
        var query = new ProcessRuntimeProjectionQueryService(
            store,
            ProcessProjectionJsonCodec.Default,
            new FixedProcessProjectionClock(Now),
            new InMemoryRuntimeStateStore(state),
            new InMemoryAssignmentStore(
            [
                CreateAssignment(runId, planId, stepId, "implementation", ".NET Developer")
            ]),
            observationReader);

        var workspace = await query.GetRuntimeWorkspaceAsync(new ProcessRuntimeWorkspaceQuery(
            Now,
            TimeSpan.FromHours(1),
            EventPage: 0,
            EventPageSize: 10,
            TakeRuns: 10,
            SelectedRunId: null));

        Assert.Empty(workspace.ActiveAgents);
        Assert.Equal(0, observationReader.CallCount);
    }

    [Fact]
    public async Task Runtime_workspace_active_agents_fall_back_to_runtime_claim_when_observation_reader_has_no_match()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var stepId = ProcessStepInstanceId.New();
        var claimToken = DispatchClaimToken.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-5)),
            latestKnownGlobalSequence: 1);
        var state = new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                CreateStepState(stepId, claimToken)
            ],
            [
                CreateClaim(stepId, claimToken, Now.AddMinutes(-1), Now.AddMinutes(29))
            ],
            [],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-1));
        var query = new ProcessRuntimeProjectionQueryService(
            store,
            ProcessProjectionJsonCodec.Default,
            new FixedProcessProjectionClock(Now),
            new InMemoryRuntimeStateStore(state),
            new InMemoryAssignmentStore(
            [
                CreateAssignment(runId, planId, stepId, "feature-intake", "Programming Workspace Analyst")
            ]),
            new InMemoryObservationReader());

        var workspace = await query.GetRuntimeWorkspaceAsync(new ProcessRuntimeWorkspaceQuery(
            Now,
            TimeSpan.FromHours(1),
            EventPage: 0,
            EventPageSize: 10,
            TakeRuns: 10,
            SelectedRunId: null));

        var activeAgent = Assert.Single(workspace.ActiveAgents);
        Assert.Equal(stepId.Value, activeAgent.StepInstanceId);
        Assert.Equal("feature-intake", activeAgent.StepKey);
        Assert.Equal("Programming Workspace Analyst", activeAgent.ExecutorDisplayName);
        Assert.True(activeAgent.IsWorking);
        Assert.Equal("Runtime claim without AgentFramework execution evidence", activeAgent.ObservationSource);
        Assert.Contains("No AgentFramework execution run was observed", activeAgent.CurrentActivity, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Projection_freshness_exposes_projector_lag()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        await ProjectAsync(
            store,
            StoredEvent(7, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-1)),
            latestKnownGlobalSequence: 10);
        var query = new ProcessRuntimeProjectionQueryService(store, ProcessProjectionJsonCodec.Default, new FixedProcessProjectionClock(Now));

        var live = await query.GetLiveProcessesAsync(new ProcessLiveProcessesQuery(Now, TimeSpan.FromHours(1), Take: 10));

        var run = Assert.Single(live.Runs);
        Assert.Equal(7, run.Freshness.SourceGlobalSequence);
        Assert.Equal(10, run.Freshness.Lag.LatestKnownGlobalSequence);
        Assert.Equal(3, run.Freshness.Lag.BacklogEventCount);
    }

    [Fact]
    public async Task Restricted_events_project_diagnostic_links_without_raw_payload_detail()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var restricted = StoredEvent(
            1,
            runId,
            ProcessRuntimeEventTypes.ManagerIncidentRaised,
            Now,
            sensitivity: ProcessEventSensitivity.Restricted,
            payloadHash: "hash:restricted-secret");
        await ProjectAsync(store, restricted, latestKnownGlobalSequence: 1);
        var query = new ProcessRuntimeProjectionQueryService(store, ProcessProjectionJsonCodec.Default, new FixedProcessProjectionClock(Now));

        var history = await query.GetRunHistoryAsync(new ProcessRunHistoryQuery(runId, Now.AddHours(-1), Now.AddHours(1), Take: 10));

        var runtimeEvent = Assert.Single(history.Events);
        Assert.Equal(ProcessProjectedSensitivity.Restricted, runtimeEvent.Sensitivity);
        Assert.Equal($"runtime-event:{restricted.Envelope.EventId}", runtimeEvent.RestrictedDiagnosticReference);
        Assert.DoesNotContain("restricted-secret", runtimeEvent.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Shell_projection_aggregates_metric_buckets_and_tool_usage_deterministically()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-5).AddSeconds(10)),
            latestKnownGlobalSequence: 3);
        await ProjectAsync(
            store,
            StoredEvent(2, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-5).AddSeconds(50)),
            latestKnownGlobalSequence: 3);
        await ProjectAsync(
            store,
            StoredEvent(3, runId, ProcessRuntimeEventTypes.ManagerIncidentRaised, Now.AddMinutes(-4).AddSeconds(5)),
            latestKnownGlobalSequence: 3);
        var clock = new FixedProcessProjectionClock(Now);
        var templateLoader = new ProcessTemplatePackLoader(Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));
        var service = new ProcessWorkspaceShellProjectionService(
            clock,
            new ProcessDefinitionCatalogProjectionService(templateLoader, clock),
            new ProcessDefinitionEditorProjectionService(templateLoader, clock),
            new ProcessDefinitionRoleEditorProjectionService(templateLoader, clock),
            new ProcessDefinitionCanvasEditorProjectionService(templateLoader, clock),
            new ProcessDefinitionStepEditorProjectionService(templateLoader, clock),
            new ProcessTemplateCatalogProjectionService(templateLoader, clock),
            new ProcessRuntimeProjectionQueryService(store, ProcessProjectionJsonCodec.Default, clock));

        var shell = await service.GetShellAsync(new ProcessWorkspaceShellRequest(
            ProcessWorkspaceShellScope.Global,
            new ProcessWorkspaceSelectionProjection(ProcessId: null, RunId: null, LaunchPlanId: null),
            new ProcessDefinitionCatalogQueryProjection(SearchText: null, SelectedDefinitionKey: null, ProcessDefinitionCatalogScopeKind.All, Take: 50),
            new ProcessTemplateCatalogQueryProjection(SearchText: null, ProcessTemplateCatalogCategoryKind.All, SelectedItemKey: null, ProcessTemplateCatalogPreviewTabKind.Overview, Take: 50),
            ForceRefresh: false,
            new ProcessRuntimeWorkspaceQueryProjection(ProcessRuntimeHistoryWindow.OneDay, EventPage: 0, EventPageSize: 25, SelectedRunId: null)));

        Assert.Equal(3, shell.Runtime.Stats.EventCount);
        Assert.Equal(
            [
                new DateTimeOffset(2026, 6, 15, 11, 55, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 15, 11, 56, 0, TimeSpan.Zero)
            ],
            shell.Runtime.MetricPoints.Select(point => point.TimestampUtc));
        Assert.Equal(2, shell.Runtime.MetricPoints[0].EventCount);
        Assert.Equal(0, shell.Runtime.MetricPoints[0].ManagerEventCount);
        Assert.Equal(2, shell.Runtime.MetricPoints[0].ToolCallCount);
        Assert.Equal(40_000, shell.Runtime.MetricPoints[0].DurationMs);
        Assert.Collection(
            shell.Runtime.ToolUsage,
            tool =>
            {
                Assert.Equal("Step Running", tool.ToolName);
                Assert.Equal(2, tool.CallCount);
                Assert.Equal(Now.AddMinutes(-5).AddSeconds(50), tool.LastUsedAtUtc);
            },
            tool =>
            {
                Assert.Equal("Manager Incident Raised", tool.ToolName);
                Assert.Equal(1, tool.CallCount);
                Assert.Equal(Now.AddMinutes(-4).AddSeconds(5), tool.LastUsedAtUtc);
            });
    }

    [Fact]
    public async Task Shell_projection_uses_full_history_window_for_metrics_when_event_ledger_is_paged()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-50)),
            latestKnownGlobalSequence: 7);
        await ProjectAsync(
            store,
            StoredEvent(2, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-49)),
            latestKnownGlobalSequence: 7);
        await ProjectAsync(
            store,
            StoredEvent(3, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-48)),
            latestKnownGlobalSequence: 7);
        await ProjectAsync(
            store,
            StoredEvent(4, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-47)),
            latestKnownGlobalSequence: 7);
        await ProjectAsync(
            store,
            StoredEvent(5, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-46)),
            latestKnownGlobalSequence: 7);
        await ProjectAsync(
            store,
            StoredEvent(6, runId, ProcessRuntimeEventTypes.ManagerIncidentRaised, Now.AddMinutes(-5)),
            latestKnownGlobalSequence: 7);
        await ProjectAsync(
            store,
            StoredEvent(7, runId, ProcessRuntimeEventTypes.StepBlocked, Now.AddMinutes(-4)),
            latestKnownGlobalSequence: 7);
        var clock = new FixedProcessProjectionClock(Now);
        var templateLoader = new ProcessTemplatePackLoader(Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));
        var service = new ProcessWorkspaceShellProjectionService(
            clock,
            new ProcessDefinitionCatalogProjectionService(templateLoader, clock),
            new ProcessDefinitionEditorProjectionService(templateLoader, clock),
            new ProcessDefinitionRoleEditorProjectionService(templateLoader, clock),
            new ProcessDefinitionCanvasEditorProjectionService(templateLoader, clock),
            new ProcessDefinitionStepEditorProjectionService(templateLoader, clock),
            new ProcessTemplateCatalogProjectionService(templateLoader, clock),
            new ProcessRuntimeProjectionQueryService(store, ProcessProjectionJsonCodec.Default, clock));

        var shell = await service.GetShellAsync(new ProcessWorkspaceShellRequest(
            ProcessWorkspaceShellScope.Global,
            new ProcessWorkspaceSelectionProjection(ProcessId: null, RunId: null, LaunchPlanId: null),
            new ProcessDefinitionCatalogQueryProjection(SearchText: null, SelectedDefinitionKey: null, ProcessDefinitionCatalogScopeKind.All, Take: 50),
            new ProcessTemplateCatalogQueryProjection(SearchText: null, ProcessTemplateCatalogCategoryKind.All, SelectedItemKey: null, ProcessTemplateCatalogPreviewTabKind.Overview, Take: 50),
            ForceRefresh: false,
            new ProcessRuntimeWorkspaceQueryProjection(ProcessRuntimeHistoryWindow.OneDay, EventPage: 0, EventPageSize: 5, SelectedRunId: null)));

        Assert.Equal(5, shell.Runtime.Events.Count);
        Assert.True(shell.Runtime.HasMoreEvents);
        Assert.Equal(7, shell.Runtime.Stats.EventCount);
        Assert.Equal(1, shell.Runtime.Stats.ManagerEventCount);
        Assert.Equal(7, shell.Runtime.Stats.ToolCallCount);
        Assert.Contains(shell.Runtime.MetricPoints, point =>
            point.TimestampUtc == new DateTimeOffset(2026, 6, 15, 11, 56, 0, TimeSpan.Zero) &&
            point.EventCount == 1);
        Assert.Contains(shell.Runtime.ToolUsage, tool =>
            tool.ToolName == "Manager Incident Raised" &&
            tool.CallCount == 1);
    }

    [Fact]
    public async Task Shell_projection_aggregates_usage_telemetry_into_stats_and_metric_buckets()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-5).AddSeconds(10)),
            latestKnownGlobalSequence: 2);
        await ProjectAsync(
            store,
            StoredEvent(2, runId, ProcessRuntimeEventTypes.ManagerIncidentRaised, Now.AddMinutes(-4).AddSeconds(5)),
            latestKnownGlobalSequence: 2);
        var usageReader = new InMemoryUsageTelemetryReader(
            new ProcessRuntimeUsageObservation(
                Guid.NewGuid(),
                Guid.NewGuid(),
                runId,
                StepInstanceId: null,
                Now.AddMinutes(-5).AddSeconds(20),
                "OpenAI default",
                "gpt-test",
                "agent-runtime",
                "Observed",
                IsKnownUsage: true,
                InputTokens: 100,
                CachedInputTokens: 10,
                OutputTokens: 20,
                ReasoningTokens: 0,
                TotalTokens: 120,
                EstimatedCostUsd: 0m,
                ActualCostUsd: 0.123456m),
            new ProcessRuntimeUsageObservation(
                Guid.NewGuid(),
                Guid.NewGuid(),
                runId,
                StepInstanceId: null,
                Now.AddMinutes(-4).AddSeconds(15),
                "OpenAI default",
                "gpt-test",
                "legacy-agent-run-metric",
                "EstimatedFromMetric",
                IsKnownUsage: false,
                InputTokens: 50,
                CachedInputTokens: 0,
                OutputTokens: 5,
                ReasoningTokens: 0,
                TotalTokens: 55,
                EstimatedCostUsd: 0.045m,
                ActualCostUsd: 0m));
        var clock = new FixedProcessProjectionClock(Now);
        var templateLoader = new ProcessTemplatePackLoader(Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));
        var service = new ProcessWorkspaceShellProjectionService(
            clock,
            new ProcessDefinitionCatalogProjectionService(templateLoader, clock),
            new ProcessDefinitionEditorProjectionService(templateLoader, clock),
            new ProcessDefinitionRoleEditorProjectionService(templateLoader, clock),
            new ProcessDefinitionCanvasEditorProjectionService(templateLoader, clock),
            new ProcessDefinitionStepEditorProjectionService(templateLoader, clock),
            new ProcessTemplateCatalogProjectionService(templateLoader, clock),
            new ProcessRuntimeProjectionQueryService(store, ProcessProjectionJsonCodec.Default, clock),
            runtimeUsageTelemetryReader: usageReader);

        var shell = await service.GetShellAsync(new ProcessWorkspaceShellRequest(
            ProcessWorkspaceShellScope.Global,
            new ProcessWorkspaceSelectionProjection(ProcessId: null, RunId: null, LaunchPlanId: null),
            new ProcessDefinitionCatalogQueryProjection(SearchText: null, SelectedDefinitionKey: null, ProcessDefinitionCatalogScopeKind.All, Take: 50),
            new ProcessTemplateCatalogQueryProjection(SearchText: null, ProcessTemplateCatalogCategoryKind.All, SelectedItemKey: null, ProcessTemplateCatalogPreviewTabKind.Overview, Take: 50),
            ForceRefresh: false,
            new ProcessRuntimeWorkspaceQueryProjection(ProcessRuntimeHistoryWindow.OneDay, EventPage: 0, EventPageSize: 25, SelectedRunId: null)));

        Assert.Equal(150, shell.Runtime.Stats.InputTokens);
        Assert.Equal(10, shell.Runtime.Stats.CachedInputTokens);
        Assert.Equal(25, shell.Runtime.Stats.OutputTokens);
        Assert.Equal(175, shell.Runtime.Stats.TotalTokens);
        Assert.Equal(0.045m, shell.Runtime.Stats.EstimatedCost);
        Assert.Equal(0.123456m, shell.Runtime.Stats.ActualCost);
        Assert.Equal(2, shell.Runtime.MetricPoints.Count);
        Assert.Equal(100, shell.Runtime.MetricPoints[0].InputTokens);
        Assert.Equal(20, shell.Runtime.MetricPoints[0].OutputTokens);
        Assert.Equal(120, shell.Runtime.MetricPoints[0].TotalTokens);
        Assert.Equal(0.123456m, shell.Runtime.MetricPoints[0].ActualCost);
        Assert.Equal(50, shell.Runtime.MetricPoints[1].InputTokens);
        Assert.Equal(5, shell.Runtime.MetricPoints[1].OutputTokens);
        Assert.Equal(55, shell.Runtime.MetricPoints[1].TotalTokens);
        Assert.Equal(0.045m, shell.Runtime.MetricPoints[1].EstimatedCost);
        Assert.Equal(1, usageReader.CallCount);
    }

    [Fact]
    public async Task Shell_projection_scopes_usage_telemetry_to_selected_run()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var selectedRunId = ProcessRunId.New();
        var unrelatedRunId = ProcessRunId.New();
        await ProjectAsync(
            store,
            StoredEvent(1, unrelatedRunId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-6)),
            latestKnownGlobalSequence: 2);
        await ProjectAsync(
            store,
            StoredEvent(2, selectedRunId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-5)),
            latestKnownGlobalSequence: 2);
        var usageReader = new InMemoryUsageTelemetryReader(
            new ProcessRuntimeUsageObservation(
                Guid.NewGuid(),
                Guid.NewGuid(),
                selectedRunId,
                StepInstanceId: null,
                Now.AddMinutes(-5),
                "OpenAI default",
                "gpt-test",
                "agent-runtime",
                "Observed",
                IsKnownUsage: true,
                InputTokens: 100,
                CachedInputTokens: 10,
                OutputTokens: 20,
                ReasoningTokens: 0,
                TotalTokens: 120,
                EstimatedCostUsd: 0m,
                ActualCostUsd: 0.123456m),
            new ProcessRuntimeUsageObservation(
                Guid.NewGuid(),
                Guid.NewGuid(),
                unrelatedRunId,
                StepInstanceId: null,
                Now.AddMinutes(-6),
                "OpenAI default",
                "gpt-test",
                "agent-runtime",
                "Observed",
                IsKnownUsage: true,
                InputTokens: 900,
                CachedInputTokens: 0,
                OutputTokens: 90,
                ReasoningTokens: 0,
                TotalTokens: 990,
                EstimatedCostUsd: 0m,
                ActualCostUsd: 9.9m));
        var clock = new FixedProcessProjectionClock(Now);
        var templateLoader = new ProcessTemplatePackLoader(Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));
        var service = new ProcessWorkspaceShellProjectionService(
            clock,
            new ProcessDefinitionCatalogProjectionService(templateLoader, clock),
            new ProcessDefinitionEditorProjectionService(templateLoader, clock),
            new ProcessDefinitionRoleEditorProjectionService(templateLoader, clock),
            new ProcessDefinitionCanvasEditorProjectionService(templateLoader, clock),
            new ProcessDefinitionStepEditorProjectionService(templateLoader, clock),
            new ProcessTemplateCatalogProjectionService(templateLoader, clock),
            new ProcessRuntimeProjectionQueryService(store, ProcessProjectionJsonCodec.Default, clock),
            runtimeUsageTelemetryReader: usageReader);

        var shell = await service.GetShellAsync(new ProcessWorkspaceShellRequest(
            ProcessWorkspaceShellScope.Global,
            new ProcessWorkspaceSelectionProjection(ProcessId: null, RunId: selectedRunId.Value, LaunchPlanId: null),
            new ProcessDefinitionCatalogQueryProjection(SearchText: null, SelectedDefinitionKey: null, ProcessDefinitionCatalogScopeKind.All, Take: 50),
            new ProcessTemplateCatalogQueryProjection(SearchText: null, ProcessTemplateCatalogCategoryKind.All, SelectedItemKey: null, ProcessTemplateCatalogPreviewTabKind.Overview, Take: 50),
            ForceRefresh: false,
            new ProcessRuntimeWorkspaceQueryProjection(ProcessRuntimeHistoryWindow.OneDay, EventPage: 0, EventPageSize: 25, selectedRunId.Value)));

        Assert.Equal(100, shell.Runtime.Stats.InputTokens);
        Assert.Equal(10, shell.Runtime.Stats.CachedInputTokens);
        Assert.Equal(20, shell.Runtime.Stats.OutputTokens);
        Assert.Equal(120, shell.Runtime.Stats.TotalTokens);
        Assert.Equal(0.123456m, shell.Runtime.Stats.ActualCost);
        Assert.NotNull(usageReader.LastQuery);
        Assert.Contains(selectedRunId, usageReader.LastQuery!.RunIds);
        Assert.DoesNotContain(unrelatedRunId, usageReader.LastQuery.RunIds);
    }

    [Fact]
    public async Task Shell_projection_selected_active_run_attention_summary_does_not_borrow_unselected_blocker()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var selectedRunId = ProcessRunId.New();
        var blockedRunId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var selectedStepId = ProcessStepInstanceId.New();
        var selectedClaimToken = DispatchClaimToken.New();
        await ProjectAsync(
            store,
            StoredEvent(1, blockedRunId, ProcessRuntimeEventTypes.StepBlocked, Now.AddMinutes(-3)),
            latestKnownGlobalSequence: 2);
        await ProjectAsync(
            store,
            StoredEvent(2, selectedRunId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-1)),
            latestKnownGlobalSequence: 2);
        var selectedState = new ProcessRuntimeStateSnapshot(
            selectedRunId,
            selectedRunId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                CreateStepState(selectedStepId, selectedClaimToken)
            ],
            [
                CreateClaim(selectedStepId, selectedClaimToken, Now.AddMinutes(-2), Now.AddMinutes(20))
            ],
            [],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-1));
        var clock = new FixedProcessProjectionClock(Now);
        var templateLoader = new ProcessTemplatePackLoader(Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));
        var service = new ProcessWorkspaceShellProjectionService(
            clock,
            new ProcessDefinitionCatalogProjectionService(templateLoader, clock),
            new ProcessDefinitionEditorProjectionService(templateLoader, clock),
            new ProcessDefinitionRoleEditorProjectionService(templateLoader, clock),
            new ProcessDefinitionCanvasEditorProjectionService(templateLoader, clock),
            new ProcessDefinitionStepEditorProjectionService(templateLoader, clock),
            new ProcessTemplateCatalogProjectionService(templateLoader, clock),
            new ProcessRuntimeProjectionQueryService(
                store,
                ProcessProjectionJsonCodec.Default,
                clock,
                new InMemoryRuntimeStateStore(selectedState),
                new InMemoryAssignmentStore(
                [
                    CreateAssignment(selectedRunId, planId, selectedStepId, "code-change", ".NET Application Developer")
                ]),
                new InMemoryObservationReader()));

        var shell = await service.GetShellAsync(new ProcessWorkspaceShellRequest(
            ProcessWorkspaceShellScope.Global,
            new ProcessWorkspaceSelectionProjection(ProcessId: null, RunId: selectedRunId.Value, LaunchPlanId: null),
            new ProcessDefinitionCatalogQueryProjection(SearchText: null, SelectedDefinitionKey: null, ProcessDefinitionCatalogScopeKind.All, Take: 50),
            new ProcessTemplateCatalogQueryProjection(SearchText: null, ProcessTemplateCatalogCategoryKind.All, SelectedItemKey: null, ProcessTemplateCatalogPreviewTabKind.Overview, Take: 50),
            ForceRefresh: false,
            new ProcessRuntimeWorkspaceQueryProjection(ProcessRuntimeHistoryWindow.OneDay, EventPage: 0, EventPageSize: 25, selectedRunId.Value)));

        Assert.Contains("active agent", shell.Runtime.AttentionSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(selectedRunId.Value.ToString("N")[..8], shell.Runtime.AttentionSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("StepBlocked", shell.Runtime.AttentionSummary, StringComparison.Ordinal);
        Assert.DoesNotContain(blockedRunId.Value.ToString("N")[..8], shell.Runtime.AttentionSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Shell_projection_selected_active_run_attention_summary_reports_current_step_without_agent_observation()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var stepId = ProcessStepInstanceId.New();
        var claimToken = DispatchClaimToken.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-1)),
            latestKnownGlobalSequence: 1);
        var state = new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                CreateStepState(stepId, claimToken)
            ],
            [
                CreateClaim(stepId, claimToken, Now.AddMinutes(-2), Now.AddMinutes(20))
            ],
            [],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-1));
        var clock = new FixedProcessProjectionClock(Now);
        var templateLoader = new ProcessTemplatePackLoader(Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));
        var service = new ProcessWorkspaceShellProjectionService(
            clock,
            new ProcessDefinitionCatalogProjectionService(templateLoader, clock),
            new ProcessDefinitionEditorProjectionService(templateLoader, clock),
            new ProcessDefinitionRoleEditorProjectionService(templateLoader, clock),
            new ProcessDefinitionCanvasEditorProjectionService(templateLoader, clock),
            new ProcessDefinitionStepEditorProjectionService(templateLoader, clock),
            new ProcessTemplateCatalogProjectionService(templateLoader, clock),
            new ProcessRuntimeProjectionQueryService(
                store,
                ProcessProjectionJsonCodec.Default,
                clock,
                new InMemoryRuntimeStateStore(state),
                new InMemoryAssignmentStore([]),
                new InMemoryObservationReader()));

        var shell = await service.GetShellAsync(new ProcessWorkspaceShellRequest(
            ProcessWorkspaceShellScope.Global,
            new ProcessWorkspaceSelectionProjection(ProcessId: null, RunId: runId.Value, LaunchPlanId: null),
            new ProcessDefinitionCatalogQueryProjection(SearchText: null, SelectedDefinitionKey: null, ProcessDefinitionCatalogScopeKind.All, Take: 50),
            new ProcessTemplateCatalogQueryProjection(SearchText: null, ProcessTemplateCatalogCategoryKind.All, SelectedItemKey: null, ProcessTemplateCatalogPreviewTabKind.Overview, Take: 50),
            ForceRefresh: false,
            new ProcessRuntimeWorkspaceQueryProjection(ProcessRuntimeHistoryWindow.OneDay, EventPage: 0, EventPageSize: 25, runId.Value)));

        Assert.Contains("Current step:", shell.Runtime.AttentionSummary, StringComparison.Ordinal);
        Assert.Contains(stepId.Value.ToString("D"), shell.Runtime.AttentionSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("no current operator action", shell.Runtime.AttentionSummary, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task ProjectAsync(
        EfProcessProjectionStore store,
        ProcessStoredRuntimeEvent runtimeEvent,
        long latestKnownGlobalSequence)
    {
        var replay = new RecordingRuntimeEventReplayStore(runtimeEvent);
        var clock = new FixedProcessProjectionClock(Now);
        var projector = new ProcessRuntimeProjectionProjector(store, ProcessProjectionJsonCodec.Default, clock);
        var worker = new ProcessProjectionReplayWorker(replay, store, projector, clock);
        await worker.ReplayAsync(new ProcessProjectionReplayRequest(
            ProcessRuntimeProjectionProjector.ProjectorName,
            new ProcessProjectionShardKey("root-alpha"),
            Take: 10,
            latestKnownGlobalSequence));
    }

    private static ProcessPersistenceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>()
            .UseInMemoryDatabase($"process-projections-{Guid.NewGuid():N}")
            .Options;
        return new ProcessPersistenceDbContext(options);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }

    private static ProcessStoredRuntimeEvent StoredEvent(
        long globalSequence,
        ProcessRunId runId,
        ProcessEventType eventType,
        DateTimeOffset occurredAtUtc,
        ProcessEventSensitivity sensitivity = ProcessEventSensitivity.Normal,
        string payloadHash = "hash:event")
    {
        var envelope = new ProcessRuntimeEventEnvelope(
            RuntimeEventId.New(),
            runId,
            runId,
            new ProcessCorrelationId("corr-alpha"),
            null,
            new ProcessEventActor(ProcessEventActorKind.System, new ProcessActorId("system")),
            ProcessContractVersions.RuntimeEventEnvelopeV1,
            sensitivity,
            occurredAtUtc,
            eventType,
            payloadHash);
        return new ProcessStoredRuntimeEvent(globalSequence, globalSequence, envelope);
    }

    private static ProcessRuntimeStepAssignment CreateAssignment(
        ProcessRunId runId,
        ProcessInstancePlanId planId,
        ProcessStepInstanceId stepId,
        string stepKey,
        string executorDisplayName)
    {
        return new ProcessRuntimeStepAssignment(
            runId,
            planId,
            stepId,
            stepKey,
            "lead-engineer",
            "lead-engineer",
            "Lead engineer",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            executorDisplayName,
            $"Execute {stepKey}.",
            "sha256:readiness",
            "Matched role and workspace tool readiness.",
            [],
            [],
            [ProcessOperationContractNames.ReadProjectStructure],
            ProcessOperationContractNames.ExternalProductTargetReadOnly,
            new Dictionary<string, string>(),
            BranchGate: null,
            Now.AddMinutes(-6));
    }

    private static ProcessRuntimeStepState CreateStepState(
        ProcessStepInstanceId stepId,
        DispatchClaimToken claimToken)
    {
        return new ProcessRuntimeStepState(
            stepId,
            ProcessStepDefinitionId.New(),
            ProcessRuntimeStepStatus.Running,
            IsExecutable: true,
            AttemptNumber: 1,
            DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
            RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
            claimToken,
            CompletedResultKey: null);
    }

    private static DispatchClaimState CreateClaim(
        ProcessStepInstanceId stepId,
        DispatchClaimToken claimToken,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        return new DispatchClaimState(
            claimToken,
            stepId,
            new DispatcherOwnerId("unit-test-dispatcher"),
            DispatchClaimStatus.Claimed,
            AttemptNumber: 1,
            createdAtUtc,
            expiresAtUtc,
            RenewedAtUtc: null,
            ResultIdempotencyKey: null);
    }

    private static ProcessExecutionObservation CreateObservation(
        ProcessRunId runId,
        ProcessStepInstanceId stepId,
        string agentName,
        string state,
        DateTimeOffset updatedAtUtc,
        string avatarImageUrl = "",
        string resultSummary = "",
        IReadOnlyList<ProcessExecutionToolObservation>? recentTools = null,
        IReadOnlyList<ProcessExecutionArtifactObservation>? artifacts = null,
        string lastError = "")
    {
        var isTerminal = string.Equals(state, "Completed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state, "Failed", StringComparison.OrdinalIgnoreCase);
        var resolvedResultSummary = string.IsNullOrWhiteSpace(resultSummary)
            ? (isTerminal ? "Execution run response persisted." : string.Empty)
            : resultSummary;
        return new ProcessExecutionObservation(
            Guid.NewGuid(),
            runId,
            stepId,
            Guid.NewGuid(),
            agentName,
            "OpenAI default",
            "gpt-test",
            state,
            isTerminal ? "Succeeded" : string.Empty,
            updatedAtUtc.AddMinutes(-1),
            updatedAtUtc,
            updatedAtUtc.AddMinutes(-1),
            isTerminal ? updatedAtUtc : null,
            $"Input for {agentName}.",
            resolvedResultSummary,
            [
                new ProcessExecutionActivityObservation(
                    updatedAtUtc,
                    state,
                    "Execution",
                    $"{agentName} is {state}.")
            ],
            recentTools ?? [],
            artifacts ?? [],
            LastError: lastError)
        {
            AgentAvatarImageUrl = avatarImageUrl
        };
    }

    private sealed class RecordingRuntimeEventReplayStore(params ProcessStoredRuntimeEvent[] events) : IProcessRuntimeEventReplayStore
    {
        private readonly IReadOnlyList<ProcessStoredRuntimeEvent> events = events;

        public Task<IReadOnlyList<ProcessStoredRuntimeEvent>> ReadAfterGlobalSequenceAsync(
            long globalSequenceExclusive,
            int take,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ProcessStoredRuntimeEvent> result = events
                .Where(runtimeEvent => runtimeEvent.GlobalSequence > globalSequenceExclusive)
                .OrderBy(runtimeEvent => runtimeEvent.GlobalSequence)
                .Take(take)
                .ToArray();
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<ProcessStoredRuntimeEvent>> ReadByRootRunAsync(
            ProcessRunId rootRunId,
            long rootSequenceExclusive,
            int take,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ProcessStoredRuntimeEvent> result = events
                .Where(runtimeEvent =>
                    runtimeEvent.Envelope.RootRunId == rootRunId &&
                    runtimeEvent.RootSequence > rootSequenceExclusive)
                .OrderBy(runtimeEvent => runtimeEvent.RootSequence)
                .Take(take)
                .ToArray();
            return Task.FromResult(result);
        }
    }

    private sealed class InMemoryRuntimeStateStore(params ProcessRuntimeStateSnapshot[] states) : IProcessRuntimeStateStore
    {
        public Task<ProcessRuntimeStateSnapshot?> LoadAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProcessRuntimeStateSnapshot?>(states.FirstOrDefault(state => state.RunId == runId));
    }

    private sealed class InMemoryAssignmentStore(IReadOnlyList<ProcessRuntimeStepAssignment> assignments) : IProcessRuntimeStepAssignmentStore
    {
        public ValueTask SaveAsync(
            IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> LoadByRunAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(assignments.Where(assignment => assignment.RunId == runId).ToArray() as IReadOnlyList<ProcessRuntimeStepAssignment>);

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> FindByLaunchVariablesAsync(
            IReadOnlyDictionary<string, string> requiredVariables,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ProcessRuntimeStepAssignment> matches = assignments
                .Where(assignment => requiredVariables.All(required =>
                    assignment.LaunchVariables.TryGetValue(required.Key, out var value) &&
                    string.Equals(value, required.Value, StringComparison.Ordinal)))
                .ToArray();
            return ValueTask.FromResult(matches);
        }

        public ValueTask<ProcessRuntimeStepAssignment?> LoadAsync(
            ProcessRunId runId,
            ProcessStepInstanceId stepInstanceId,
            CancellationToken cancellationToken = default)
        {
            var assignment = assignments.FirstOrDefault(item => item.RunId == runId && item.StepInstanceId == stepInstanceId);
            return ValueTask.FromResult(assignment);
        }
    }

    private sealed class InMemoryObservationReader(params ProcessExecutionObservation[] observations) : IProcessExecutionObservationReader
    {
        public int CallCount { get; private set; }

        public ValueTask<IReadOnlyList<ProcessExecutionObservation>> ListAsync(
            ProcessExecutionObservationQuery query,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var runIds = query.RunIds.ToHashSet();
            IReadOnlyList<ProcessExecutionObservation> result = observations
                .Where(observation =>
                    runIds.Contains(observation.RunId) &&
                    observation.UpdatedAtUtc >= query.FromUtc &&
                    observation.UpdatedAtUtc <= query.ToUtc)
                .OrderByDescending(observation => observation.UpdatedAtUtc)
                .Take(query.TakePerRun * Math.Max(1, query.RunIds.Count))
                .ToArray();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class CountingProjectionStore(IProcessProjectionStore inner) : IProcessProjectionStore
    {
        public int ReadSnapshotsCallCount { get; private set; }

        public int LoadSnapshotCallCount { get; private set; }

        public int ReadHistoryCallCount { get; private set; }

        public Task UpsertSnapshotAsync(
            ProcessProjectionSnapshot snapshot,
            CancellationToken cancellationToken = default)
            => inner.UpsertSnapshotAsync(snapshot, cancellationToken);

        public Task<ProcessProjectionSnapshot?> LoadSnapshotAsync(
            ProcessProjectorName projectorName,
            ProcessProjectionKey projectionKey,
            CancellationToken cancellationToken = default)
        {
            LoadSnapshotCallCount++;
            return inner.LoadSnapshotAsync(projectorName, projectionKey, cancellationToken);
        }

        public Task<IReadOnlyList<ProcessProjectionSnapshot>> ReadSnapshotsAsync(
            ProcessProjectorName projectorName,
            ProcessProjectionKeyPrefix projectionKeyPrefix,
            int take,
            CancellationToken cancellationToken = default)
        {
            ReadSnapshotsCallCount++;
            return inner.ReadSnapshotsAsync(projectorName, projectionKeyPrefix, take, cancellationToken);
        }

        public Task AppendHistoryAsync(
            ProcessProjectionHistoryRecord history,
            CancellationToken cancellationToken = default)
            => inner.AppendHistoryAsync(history, cancellationToken);

        public Task<IReadOnlyList<ProcessProjectionHistoryRecord>> ReadHistoryAsync(
            ProcessProjectionHistoryQuery query,
            CancellationToken cancellationToken = default)
        {
            ReadHistoryCallCount++;
            return inner.ReadHistoryAsync(query, cancellationToken);
        }

        public Task SaveOffsetAsync(
            ProcessProjectorOffset offset,
            CancellationToken cancellationToken = default)
            => inner.SaveOffsetAsync(offset, cancellationToken);

        public Task<ProcessProjectorOffset?> LoadOffsetAsync(
            ProcessProjectorName projectorName,
            ProcessProjectionShardKey shardKey,
            CancellationToken cancellationToken = default)
            => inner.LoadOffsetAsync(projectorName, shardKey, cancellationToken);

        public Task WriteDeadLetterAsync(
            ProcessProjectionDeadLetter deadLetter,
            CancellationToken cancellationToken = default)
            => inner.WriteDeadLetterAsync(deadLetter, cancellationToken);

        public Task<IReadOnlyList<ProcessProjectionDeadLetter>> ReadDeadLettersAsync(
            ProcessProjectorName projectorName,
            ProcessProjectionShardKey shardKey,
            int take,
            CancellationToken cancellationToken = default)
            => inner.ReadDeadLettersAsync(projectorName, shardKey, take, cancellationToken);
    }

    private sealed class ThrowingRuntimeStateStore : IProcessRuntimeStateStore
    {
        public Task<ProcessRuntimeStateSnapshot?> LoadAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("List-only runtime workspace must not load runtime state.");
    }

    private sealed class ThrowingAssignmentStore : IProcessRuntimeStepAssignmentStore
    {
        public ValueTask SaveAsync(
            IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("List-only runtime workspace must not save assignments.");

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> LoadByRunAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("List-only runtime workspace must not load assignments.");

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> FindByLaunchVariablesAsync(
            IReadOnlyDictionary<string, string> requiredVariables,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("List-only runtime workspace must not search assignments.");

        public ValueTask<ProcessRuntimeStepAssignment?> LoadAsync(
            ProcessRunId runId,
            ProcessStepInstanceId stepInstanceId,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("List-only runtime workspace must not load assignments.");
    }

    private sealed class ThrowingObservationReader : IProcessExecutionObservationReader
    {
        public ValueTask<IReadOnlyList<ProcessExecutionObservation>> ListAsync(
            ProcessExecutionObservationQuery query,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("List-only runtime workspace must not read active-agent observations.");
    }

    private sealed class InMemoryUsageTelemetryReader(params ProcessRuntimeUsageObservation[] observations) : IProcessRuntimeUsageTelemetryReader
    {
        public int CallCount { get; private set; }

        public ProcessRuntimeUsageTelemetryQuery? LastQuery { get; private set; }

        public ValueTask<IReadOnlyList<ProcessRuntimeUsageObservation>> ListAsync(
            ProcessRuntimeUsageTelemetryQuery query,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastQuery = query;
            var runIds = query.RunIds.ToHashSet();
            IReadOnlyList<ProcessRuntimeUsageObservation> result = observations
                .Where(observation =>
                    runIds.Contains(observation.RunId) &&
                    observation.CreatedAtUtc >= query.FromUtc &&
                    observation.CreatedAtUtc <= query.ToUtc)
                .OrderBy(observation => observation.CreatedAtUtc)
                .Take(query.TakePerRun * Math.Max(1, query.RunIds.Count))
                .ToArray();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ThrowingRuntimeProjector(ProcessProjectorName projectorName) : IProcessRuntimeProjector
    {
        public ProcessProjectorName ProjectorName { get; } = projectorName;

        public Task ProjectAsync(
            ProcessStoredRuntimeEvent runtimeEvent,
            ProcessProjectionExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException($"Cannot project event {runtimeEvent.Envelope.EventId}.");
        }
    }

    private sealed class FixedProcessProjectionClock(DateTimeOffset utcNow) : IProcessProjectionClock
    {
        public DateTimeOffset GetUtcNow() => utcNow;
    }
}
