using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessBackgroundReservationConsumerTests {
    [Theory]
    [InlineData(ExecutionRunSourceDisposition.Created, 0)]
    [InlineData(ExecutionRunSourceDisposition.ReusedCompleted, 1)]
    [InlineData(ExecutionRunSourceDisposition.ExistingAdmittedFailure, 1)]
    public async Task Actual_executor_adapter_uses_admitted_source_and_recovers_the_original_execution(
        ExecutionRunSourceDisposition disposition, int recoveryCalls) {
        var fixture = Create(disposition);
        var result = await AgentFrameworkProcessStepExecutor.ExecuteAdmittedProcessRunAsync(
            fixture.Service, fixture.Assignment, fixture.Request, default);
        Assert.Same(fixture.Proxy.Result, result);
        Assert.Equal(1, fixture.Proxy.Reservations);
        Assert.Equal(recoveryCalls, fixture.Proxy.Recoveries);
        Assert.Equal(fixture.Assignment.RunId.ToString(), fixture.Proxy.Source!.CorrelationId);
        Assert.Equal(fixture.Assignment.StepInstanceId.ToString(), fixture.Proxy.Source.CausationId);
        Assert.True(fixture.Proxy.Source.RequiresBackgroundAdmission);
        Assert.Equal(fixture.Proxy.Result.ExecutionRunId, result.ExecutionRunId);
    }

    [Fact]
    public async Task Existing_active_execution_retains_its_claim_and_does_not_invoke_a_second_provider() {
        var fixture = Create(ExecutionRunSourceDisposition.ExistingActive);
        var error = await Assert.ThrowsAsync<ProcessRuntimeDispatchInProgressException>(() =>
            AgentFrameworkProcessStepExecutor.ExecuteAdmittedProcessRunAsync(fixture.Service, fixture.Assignment, fixture.Request, default));
        Assert.Equal(fixture.Proxy.Result.ExecutionRunId, error.ExecutionRunId.Value);
        Assert.Equal(0, fixture.Proxy.Recoveries);
        Assert.Equal(1, fixture.Proxy.Reservations);
    }

    [Fact]
    public async Task Different_claim_with_unacknowledged_execution_keeps_original_identity_and_requires_reconciliation() {
        var fixture = Create(ExecutionRunSourceDisposition.SourceReconciliationRequired);
        var error = await Assert.ThrowsAsync<ProcessSourceExecutionReconciliationException>(() =>
            AgentFrameworkProcessStepExecutor.ExecuteAdmittedProcessRunAsync(fixture.Service, fixture.Assignment, fixture.Request, default));
        Assert.Equal(fixture.Proxy.Result.ExecutionRunId, error.ExecutionRunId.Value);
        Assert.Equal("process.adapter.prior_execution_unreconciled", error.Code);
        Assert.Equal(0, fixture.Proxy.Recoveries);
    }

    [Theory]
    [InlineData("tool-admission.reconciliation-required")]
    [InlineData("tool-admission.provider-reconciliation-required")]
    public async Task Owner_recovery_refusal_preserves_the_exact_error_and_execution_identity(string code) {
        var fixture = Create(ExecutionRunSourceDisposition.ExistingAdmittedFailure);
        var failure = new AgentToolAdmissionException(code, "The saved effect is uncertain.");
        fixture.Proxy.RecoveryFailure = failure;
        var error = await Assert.ThrowsAsync<ProcessSourceExecutionReconciliationException>(() =>
            AgentFrameworkProcessStepExecutor.ExecuteAdmittedProcessRunAsync(fixture.Service, fixture.Assignment, fixture.Request, default));
        Assert.Same(failure, error.InnerException);
        Assert.Equal(code, error.Code);
        Assert.Equal(fixture.Proxy.Result.ExecutionRunId, error.ExecutionRunId.Value);
    }

    [Fact]
    public async Task Unexpected_recovery_failure_is_not_rewritten_as_an_admission_rejection() {
        var fixture = Create(ExecutionRunSourceDisposition.ExistingAdmittedFailure);
        var failure = new IOException("Injected saved-result read acknowledgement loss.");
        fixture.Proxy.RecoveryFailure = failure;
        ProcessExecutionRunId? observed = null;
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() =>
            AgentFrameworkProcessStepExecutor.ExecuteAdmittedProcessRunAsync(fixture.Service, fixture.Assignment, fixture.Request, default,
                id => observed = id)));
        Assert.Equal(fixture.Proxy.Run.Id, observed!.Value.Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Background_source_scope_does_not_conflate_same_named_steps_in_other_runs_or_occurrences(bool anotherRun) {
        var fixture = Create(ExecutionRunSourceDisposition.Created);
        var key = ExecutionRunSourceKey.ForBackground(fixture.Proxy.Run.SourceKind, fixture.Proxy.Run.SourceId,
            fixture.Proxy.Run.CorrelationId, fixture.Proxy.Run.CausationId);
        var other = fixture.Proxy.Run with {
            CorrelationId = anotherRun ? Guid.NewGuid().ToString("D") : fixture.Proxy.Run.CorrelationId,
            CausationId = anotherRun ? fixture.Proxy.Run.CausationId : Guid.NewGuid().ToString("D")
        };
        Assert.True(key.Matches(fixture.Proxy.Run));
        Assert.False(key.MatchesBackgroundLineage(other));
        Assert.False(key.Matches(other));
        Assert.True(new ExecutionRunSourceKey(other.SourceKind, other.SourceId).Matches(other));
    }

    private static (IAgentFrameworkWorkspaceService Service, WorkspaceProxy Proxy, ProcessRuntimeStepAssignment Assignment,
        ExecutionRunRequest Request) Create(ExecutionRunSourceDisposition disposition) {
        var assignment = Assert.Single(ProcessPreparedLaunchFixture.Create().InitialCommit.InitialAssignments!);
        var agentId = Guid.Parse(assignment.ExecutorId);
        var runId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var run = new ExecutionRunRecord(runId, agentId, null, "Process source", "process-step", assignment.StepKey,
            assignment.RunId.ToString(), assignment.StepInstanceId.ToString(), "process-runtime", "system", "{}",
            "Original input", "Original result", "fixture", "model", ExecutionState.Completed, RunOutcome.Succeeded,
            now, now, now, now, string.Empty, null, []);
        var result = new ExecutionRunResult(runId, null, "Original result", null,
            new(Guid.NewGuid(), agentId, null, now, RunOutcome.Succeeded, "fixture", "model", 1, 0, 0, 0) { ExecutionRunId = runId }) { State = ExecutionState.Completed };
        var service = DispatchProxy.Create<IAgentFrameworkWorkspaceService, WorkspaceProxy>();
        var proxy = (WorkspaceProxy)(object)service;
        proxy.Disposition = disposition;
        proxy.Run = run;
        proxy.Result = result;
        var request = new ExecutionRunRequest(agentId, "A recomposed prompt", AgentExecutionOperationId.New(),
            Context: new(SourceKind: run.SourceKind, SourceId: run.SourceId, CorrelationId: run.CorrelationId, CausationId: run.CausationId,
                RequestedBy: run.RequestedBy, RequestedByKind: run.RequestedByKind, MetadataJson: run.MetadataJson));
        return (service, proxy, assignment, request);
    }

    public class WorkspaceProxy : DispatchProxy {
        public ExecutionRunSourceDisposition Disposition { get; set; }
        public ExecutionRunRecord Run { get; set; } = null!;
        public ExecutionRunResult Result { get; set; } = null!;
        public Exception? RecoveryFailure { get; set; }
        public ExecutionRunSourceKey? Source { get; private set; }
        public int Reservations { get; private set; }
        public int Recoveries { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (targetMethod?.Name == nameof(IAgentFrameworkWorkspaceService.ExecuteSameSourceRunAsync)) {
                Reservations++;
                Source = Assert.IsType<ExecutionRunSourceKey>(args![0]);
                return Task.FromResult(new ExecutionRunSourceExecutionResult(Disposition, Run,
                    Disposition == ExecutionRunSourceDisposition.Created ? Result : null));
            }
            if (targetMethod?.Name == nameof(IAgentFrameworkWorkspaceService.RecoverExecutionRunAsync)) {
                Recoveries++;
                Assert.Equal(Run.Id, Assert.IsType<Guid>(args![0]));
                if (RecoveryFailure is not null) {
                    return Task.FromException<ExecutionRunResult>(RecoveryFailure);
                }
                return Task.FromResult(Result);
            }
            throw new InvalidOperationException($"The Process consumer unexpectedly called '{targetMethod?.Name}'.");
        }
    }
}
