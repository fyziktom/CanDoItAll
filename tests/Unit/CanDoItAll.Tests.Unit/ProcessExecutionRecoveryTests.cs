using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
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
        Assert.True(result.Exhausted);
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

    private sealed class Fixture : ISandboxWorkspaceProcessRunPageStore, IProcessExecutionStopper, IProcessDecisionChildReader {
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
        internal ProcessCancellationRecoveryPending Pending { get; set; }
        internal Task<ProcessCancellationExecutionProgress> ReadAsync(CancellationToken cancellationToken = default)
            => new AgentFrameworkProcessExecutionRecovery(this, this, this).InspectNextAsync(Root, Process, After, cancellationToken);
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
