using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessRuntimeWorkAdmissionTests(ITestOutputHelper output) {
    [Theory]
    [InlineData(ClaimChange.Renew)]
    [InlineData(ClaimChange.Owner)]
    [InlineData(ClaimChange.Attempt)]
    [InlineData(ClaimChange.Reactivate)]
    [InlineData(ClaimChange.StartRunning)]
    [InlineData(ClaimChange.NewToken)]
    public void Live_claim_changes_require_fresh_admission_from_persisted_fields(ClaimChange change) {
        var state = Claimed();
        var current = ProcessPersistenceMappers.ToEntity(state);
        var claim = state.Claims.Single();
        var step = state.Steps.Single();
        if (change == ClaimChange.Reactivate) {
            current.Claims.Single().Status = DispatchClaimStatus.Released;
            current.Steps.Single().ActiveClaimToken = null;
        }
        var next = change switch {
            ClaimChange.Renew => state with { Claims = [claim with { ExpiresAtUtc = claim.ExpiresAtUtc.AddSeconds(1) }] },
            ClaimChange.Owner => state with { Claims = [claim with { OwnerId = new("replacement-owner") }] },
            ClaimChange.Attempt => state with { Claims = [claim with { AttemptNumber = 2 }] },
            ClaimChange.StartRunning => state with { Steps = [step with { Status = ProcessRuntimeStepStatus.Running }] },
            ClaimChange.NewToken => state with { Claims = [claim with { ClaimToken = new(Guid.NewGuid()) }] },
            _ => state
        };
        var request = Request(next, next);
        Assert.Equal(new ProcessRuntimeWorkAdmission(true, true), ProcessRuntimeWorkAdmission.Read(current, request));
    }

    [Theory]
    [InlineData(DispatchClaimStatus.Released)]
    [InlineData(DispatchClaimStatus.Expired)]
    [InlineData(DispatchClaimStatus.Completed)]
    [InlineData(DispatchClaimStatus.Cancelled)]
    public void Closing_an_existing_claim_does_not_admit_work(DispatchClaimStatus status) {
        var state = Claimed();
        var next = state with { Claims = [state.Claims.Single() with { Status = status }],
            Steps = [state.Steps.Single() with { ActiveClaimToken = null, Status = ProcessRuntimeStepStatus.Waiting }] };
        Assert.Equal(default, ProcessRuntimeWorkAdmission.Read(ProcessPersistenceMappers.ToEntity(state), Request(state, next)));
    }

    [Theory]
    [InlineData(ProcessRuntimeStepStatus.Failed)]
    [InlineData(ProcessRuntimeStepStatus.Blocked)]
    [InlineData(ProcessRuntimeStepStatus.Waiting)]
    public void Rework_that_resets_attempts_still_requires_root_admission(ProcessRuntimeStepStatus status) {
        var state = Claimed();
        state = state with { Claims = [], Steps = [state.Steps.Single() with { Status = status, ActiveClaimToken = null, AttemptNumber = 5 }] };
        var next = state with { Steps = [state.Steps.Single() with { Status = ProcessRuntimeStepStatus.Ready, AttemptNumber = 0 }] };
        Assert.Equal(new ProcessRuntimeWorkAdmission(false, true), ProcessRuntimeWorkAdmission.Read(ProcessPersistenceMappers.ToEntity(state), Request(state, next)));
    }

    [Fact]
    public void Recorded_reconciliation_is_allowed_but_cannot_smuggle_a_new_claim() {
        var state = Claimed();
        state = state with { Status = ProcessRuntimeStatus.CancelRequested, Claims = [],
            Steps = [state.Steps.Single() with { Status = ProcessRuntimeStepStatus.Cancelled, ActiveClaimToken = null }] };
        var next = state with { Steps = [state.Steps.Single() with { Status = ProcessRuntimeStepStatus.Waiting }] };
        var request = Request(state, next) with { Decision = new ProcessDecisionChangePhase(state.Steps.Single().StepInstanceId,
            new(new(new(Guid.NewGuid()), 1), 1), ProcessDecisionPhaseChange.BeginReconciliation) };
        var persisted = ProcessPersistenceMappers.ToEntity(state);
        Assert.Equal(default, ProcessRuntimeWorkAdmission.Read(persisted, request));
        request = request with { Mutation = request.Mutation with { State = next with { Claims = Claimed().Claims } } };
        Assert.Equal(new ProcessRuntimeWorkAdmission(true, true), ProcessRuntimeWorkAdmission.Read(persisted, request));
    }

    [Fact]
    public void Unchanged_claims_and_observation_time_do_not_create_a_new_admission() {
        var state = Claimed();
        Assert.Equal(default, ProcessRuntimeWorkAdmission.Read(ProcessPersistenceMappers.ToEntity(state),
            Request(state, state with { UpdatedAtUtc = state.UpdatedAtUtc.AddSeconds(1) })));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(128)]
    [InlineData(1024)]
    public void Persisted_work_classification_records_cost_without_remapping_runtime_payloads(int steps) {
        var state = Claimed();
        var step = state.Steps.Single();
        state = state with { Claims = [], Steps = Enumerable.Range(0, steps).Select(_ => step with {
            StepInstanceId = ProcessStepInstanceId.New(), Status = ProcessRuntimeStepStatus.Planned, ActiveClaimToken = null
        }).ToArray() };
        var current = ProcessPersistenceMappers.ToEntity(state);
        var request = Request(state, state);
        for (var warmup = 0; warmup < 10; warmup++) {
            Assert.Equal(default, ProcessRuntimeWorkAdmission.Read(current, request));
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();
        for (var iteration = 0; iteration < 100; iteration++) {
            Assert.Equal(default, ProcessRuntimeWorkAdmission.Read(current, request));
        }
        timer.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine(JsonSerializer.Serialize(new { Workload = "root-work-admission", Steps = steps,
            Iterations = 100, MeanMicroseconds = timer.Elapsed.TotalMicroseconds / 100, AllocatedBytesPerCall = allocated / 100 }));
    }

    private static ProcessRuntimeStateSnapshot Claimed() {
        var state = ProcessProjectAdmissionFixture.Initial().OriginalState;
        var step = state.Steps.Single();
        DispatchClaimState claim = new(new(Guid.NewGuid()), step.StepInstanceId, new("original-owner"), DispatchClaimStatus.Claimed,
            1, state.UpdatedAtUtc, state.UpdatedAtUtc.AddMinutes(1), null, null);
        return state with { Status = ProcessRuntimeStatus.Active, Claims = [claim],
            Steps = [step with { Status = ProcessRuntimeStepStatus.Claimed, AttemptNumber = 1, ActiveClaimToken = claim.ClaimToken }] };
    }

    private static ProcessRuntimeCommitRequest Request(ProcessRuntimeStateSnapshot original, ProcessRuntimeStateSnapshot next)
        => new(RuntimeCommandId.New(), original, new(ProcessRuntimeTransitionOutcome.Applied, next, [], [], [], []));

    public enum ClaimChange { Renew, Owner, Attempt, Reactivate, StartRunning, NewToken }
}
