using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessExecutionRecoveryTests {
    [Theory]
    [InlineData(ProcessCancellationRecoveryPending.None)]
    [InlineData(ProcessCancellationRecoveryPending.ActiveWork)]
    [InlineData(ProcessCancellationRecoveryPending.UnresolvedEffects)]
    public async Task Exactly_one_selected_original_execution_reaches_its_stop_owner(ProcessCancellationRecoveryPending pending) {
        var fixture = new Fixture { Pending = pending };
        var result = await fixture.ReadAsync();
        Assert.Equal(new ProcessExecutionRunId(fixture.Run.Id), result.After);
        Assert.False(result.Exhausted);
        Assert.Equal(pending, result.Pending);
        Assert.Equal(1, fixture.Stops);
        Assert.Equal(new(fixture.Process.Value, null, 1), fixture.Query);
    }

    [Theory]
    [InlineData(ProcessExecutionRunPageStatus.IndexUnavailable)]
    [InlineData(ProcessExecutionRunPageStatus.PublicationPending)]
    [InlineData(ProcessExecutionRunPageStatus.LayoutUnavailable)]
    public async Task Unavailable_index_never_proves_empty_work_or_changes_the_cursor(ProcessExecutionRunPageStatus status) {
        var fixture = new Fixture { After = ProcessExecutionRunId.New(), Page = ProcessExecutionRunPage.Pending(status) };
        var result = await fixture.ReadAsync();
        Assert.Equal(fixture.After, result.After);
        Assert.False(result.Exhausted);
        Assert.NotEqual(ProcessCancellationRecoveryPending.None, result.Pending);
        Assert.Equal(0, fixture.Stops);
        Assert.Equal(0, fixture.WorkflowReads);
    }

    [Fact]
    public async Task A_ready_empty_page_preserves_the_original_cursor() {
        var fixture = new Fixture { After = ProcessExecutionRunId.New() };
        fixture.Page = new(ProcessExecutionRunPageStatus.Ready, [], fixture.After.Value.Value, true);
        var result = await fixture.ReadAsync();
        Assert.Equal(fixture.After, result.After);
        Assert.True(result.Exhausted);
        Assert.Equal(ProcessCancellationRecoveryPending.None, result.Pending);
        Assert.Equal(0, fixture.Stops);
    }

    [Fact]
    public async Task Legacy_unknown_projection_for_another_run_advances_without_stopping_it() {
        var fixture = new Fixture();
        fixture.Run = fixture.Run with { ProcessRunId = ProcessRunId.New().ToString() };
        var result = await fixture.ReadAsync();
        Assert.Equal(new ProcessExecutionRunId(fixture.Run.Id), result.After);
        Assert.Equal(ProcessCancellationRecoveryPending.None, result.Pending);
        Assert.Equal(0, fixture.Stops);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_Process_identity_is_pending_only_for_a_Process_source(bool processSource) {
        var fixture = new Fixture();
        fixture.Run = fixture.Run with { ProcessRunId = string.Empty, SourceKind = processSource ? "process-step" : "test-non-process" };
        var result = await fixture.ReadAsync();
        Assert.Equal(processSource ? ProcessCancellationRecoveryPending.InvalidEvidence : ProcessCancellationRecoveryPending.None, result.Pending);
        Assert.Equal(new ProcessExecutionRunId(fixture.Run.Id), result.After);
        Assert.Equal(0, fixture.Stops);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task Invalid_page_shapes_cannot_advance_or_signal(int shape) {
        var fixture = new Fixture();
        fixture.Page = shape switch {
            0 => new((ProcessExecutionRunPageStatus)99, [], null, true),
            1 => new(ProcessExecutionRunPageStatus.IndexUnavailable, [], null, true),
            2 => new(ProcessExecutionRunPageStatus.Ready, [fixture.Run, fixture.Run], fixture.Run.Id, false),
            3 => new(ProcessExecutionRunPageStatus.Ready, [], null, false),
            4 => new(ProcessExecutionRunPageStatus.Ready, [fixture.Run], Guid.NewGuid(), true),
            5 => new(ProcessExecutionRunPageStatus.Ready, [fixture.Run with { Id = Guid.Empty }], Guid.Empty, true),
            _ => new(ProcessExecutionRunPageStatus.Ready, [], Guid.NewGuid(), true)
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.ReadAsync());
        Assert.Equal(0, fixture.Stops);
    }

    [Fact]
    public async Task Repeated_cursor_is_refused_before_the_stop_owner() {
        var fixture = new Fixture();
        fixture.After = new(fixture.Run.Id);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.ReadAsync());
        Assert.Equal(0, fixture.Stops);
    }

    [Fact]
    public async Task Unknown_stop_result_is_not_interpreted_as_closed() {
        var fixture = new Fixture { Pending = (ProcessCancellationRecoveryPending)99 };
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.ReadAsync());
        Assert.Equal(1, fixture.Stops);
    }

    [Fact]
    public async Task Cancellation_is_forwarded_to_selection_without_stopping_anything() {
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.ReadAsync(cancellation.Token));
        Assert.Equal(0, fixture.Stops);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Agent_and_Workflow_candidates_share_one_ordered_cursor_without_skipping_either_owner(bool workflowFirst) {
        var fixture = new Fixture();
        var low = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var high = Guid.Parse("20000000-0000-0000-0000-000000000001");
        fixture.Workflow = new(workflowFirst ? low : high);
        fixture.Run = fixture.Run with { Id = workflowFirst ? high : low };
        var result = await fixture.ReadAsync();
        Assert.Equal(low, result.After!.Value.Value);
        Assert.False(result.Exhausted);
        Assert.Equal(workflowFirst ? 0 : 1, fixture.Stops);
        Assert.Equal(workflowFirst ? 1 : 0, fixture.WorkflowStops);
        Assert.Equal(1, fixture.WorkflowReads);
    }

    [Theory]
    [InlineData(WorkflowProcessCancellationStatus.Closed, ProcessCancellationRecoveryPending.None)]
    [InlineData(WorkflowProcessCancellationStatus.Pending, ProcessCancellationRecoveryPending.ActiveWork)]
    [InlineData(WorkflowProcessCancellationStatus.ReconciliationRequired, ProcessCancellationRecoveryPending.InvalidEvidence)]
    public async Task Equal_GUIDs_visit_both_owners_before_advancing_and_keep_pending_evidence(
        WorkflowProcessCancellationStatus outcome, ProcessCancellationRecoveryPending expected) {
        var fixture = new Fixture { WorkflowOutcome = outcome };
        fixture.Workflow = new(fixture.Run.Id);
        var result = await fixture.ReadAsync();
        Assert.Equal(fixture.Run.Id, result.After!.Value.Value);
        Assert.Equal(expected, result.Pending);
        Assert.Equal(1, fixture.Stops);
        Assert.Equal(1, fixture.WorkflowStops);
        Assert.False(result.Exhausted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Workflow_stopping_requires_the_original_stopped_root_and_selected_membership(bool wrongRoot) {
        var fixture = new Fixture { Workflow = new(Guid.NewGuid()), RootStopped = wrongRoot, WrongRoot = wrongRoot };
        fixture.Page = new(ProcessExecutionRunPageStatus.Ready, [], null, true);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.ReadAsync());
        Assert.Equal(0, fixture.Stops);
        Assert.Equal(0, fixture.WorkflowStops);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Workflow_empty_or_repeated_identity_is_refused_without_an_owner_effect(bool repeated) {
        var fixture = new Fixture { After = ProcessExecutionRunId.New() };
        fixture.Page = new(ProcessExecutionRunPageStatus.Ready, [], fixture.After.Value.Value, true);
        fixture.Workflow = repeated ? new(fixture.After.Value.Value) : default(WorkflowRunId);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.ReadAsync());
        Assert.Equal(0, fixture.WorkflowStops);
    }

    [Fact]
    public async Task A_Workflow_signal_does_not_erase_pending_Agent_effects_for_the_same_GUID() {
        var fixture = new Fixture { Pending = ProcessCancellationRecoveryPending.UnresolvedEffects, WorkflowOutcome = WorkflowProcessCancellationStatus.Pending };
        fixture.Workflow = new(fixture.Run.Id);
        var result = await fixture.ReadAsync();
        Assert.Equal(ProcessCancellationRecoveryPending.UnresolvedEffects, result.Pending);
        Assert.Equal(1, fixture.Stops);
        Assert.Equal(1, fixture.WorkflowStops);
    }

    [Fact]
    public async Task Unknown_Workflow_owner_outcome_is_not_closed() {
        var fixture = new Fixture { Workflow = new(Guid.NewGuid()), WorkflowOutcome = (WorkflowProcessCancellationStatus)99 };
        fixture.Page = new(ProcessExecutionRunPageStatus.Ready, [], null, true);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.ReadAsync());
        Assert.Equal(1, fixture.WorkflowStops);
    }

    private sealed class Fixture : ISandboxWorkspaceProcessRunPageStore, IProcessExecutionStopper, IProcessDecisionChildReader,
        IWorkflowProcessCancellationRecovery, IProcessRootCancellationBarrierReader, IProcessRuntimeStateStore {
        internal Fixture() {
            Run = new AgentInheritedParentStopFixture().Detail!.Run with {
                ProcessRunId = Process.ToString(), SourceKind = "process-step", ToolAdmission = null
            };
        }
        internal ProcessRunId Root { get; } = ProcessRunId.New();
        internal ProcessRunId Process { get; } = ProcessRunId.New();
        internal ExecutionRunRecord Run { get; set; }
        internal ProcessExecutionRunId? After { get; set; }
        internal ProcessExecutionRunPage? Page { get; set; }
        internal ProcessExecutionRunPageQuery? Query { get; private set; }
        internal int Stops { get; private set; }
        internal int WorkflowReads { get; private set; }
        internal int WorkflowStops { get; private set; }
        internal WorkflowRunId? Workflow { get; set; }
        internal WorkflowProcessCancellationStatus WorkflowOutcome { get; set; }
        internal bool RootStopped { get; set; } = true;
        internal bool WrongRoot { get; set; }
        internal ProcessCancellationRecoveryPending Pending { get; set; }
        internal Task<ProcessCancellationExecutionProgress> ReadAsync(CancellationToken cancellationToken = default)
            => new AgentFrameworkProcessExecutionRecovery(this, this, this, this, this, this).InspectNextAsync(Root, Process, After, cancellationToken);
        public Task<WorkflowRunId?> FindNextAsync(WorkflowProcessRunId runId, WorkflowRunId? after, CancellationToken cancellationToken) {
            Assert.Equal(Process.Value, runId.Value);
            Assert.Equal(After?.Value, after?.Value);
            WorkflowReads++;
            return Task.FromResult(Workflow);
        }
        public Task<WorkflowProcessCancellationStatus> StopAsync(WorkflowProcessRunId runId, WorkflowRunId workflow, CancellationToken cancellationToken) {
            Assert.Equal(Process.Value, runId.Value);
            Assert.Equal(Workflow, workflow);
            WorkflowStops++;
            return Task.FromResult(WorkflowOutcome);
        }
        public Task<bool> IsStoppedAsync(ProcessRunId root, CancellationToken cancellationToken) {
            Assert.Equal(Root, root);
            return Task.FromResult(RootStopped);
        }
        public Task<ProcessRuntimeStateSnapshot?> LoadAsync(ProcessRunId runId, CancellationToken cancellationToken) {
            Assert.Equal(Process, runId);
            ProcessRuntimeStateSnapshot state = new(WrongRoot ? ProcessRunId.New() : Root, Process, new(Guid.NewGuid()), new string('a', 64),
                ProcessRuntimeStatus.Cancelled, [], [], [], new HashSet<ArtifactSlotId>(), DateTimeOffset.UtcNow);
            return Task.FromResult<ProcessRuntimeStateSnapshot?>(state);
        }
        public Task<ProcessExecutionRunPage> ReadProcessExecutionRunPageAsync(ProcessExecutionRunPageQuery query, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            Query = query;
            return Task.FromResult(Page ?? new(ProcessExecutionRunPageStatus.Ready, [Run], Run.Id, true));
        }
        public Task<ProcessCancellationRecoveryPending> StopAsync(ProcessRunId rootRunId, ProcessRunId runId,
            ProcessExecutionRunId executionRunId, CancellationToken cancellationToken) {
            Assert.Equal(Root, rootRunId);
            Assert.Equal(Process, runId);
            Assert.Equal(Run.Id, executionRunId.Value);
            Stops++;
            return Task.FromResult(Pending);
        }
        public Task<ProcessDecisionChildObservation?> ReadAsync(ProcessDecisionChildAddress address, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Ordinary recovery cannot read an inherited child.");
    }
}
