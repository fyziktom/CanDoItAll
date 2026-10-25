using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessRuntimeCommitObservationTests {
    [Theory]
    [InlineData(ProcessRuntimeTransitionOutcome.Applied)]
    [InlineData(ProcessRuntimeTransitionOutcome.Duplicate)]
    [InlineData(ProcessRuntimeTransitionOutcome.Rejected)]
    public void Pure_mutation_never_claims_a_fresh_commit(ProcessRuntimeTransitionOutcome outcome) {
        var result = ProcessRuntimeCommitResult.FromMutation(new(outcome, State(), [], [], [], []));
        Assert.False(result.CommittedNow);
        Assert.Equal(outcome, result.Outcome);
    }

    [Fact]
    public void Commit_observation_is_omitted_from_json_and_has_no_JSON_setter() {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var result = new ProcessRuntimeCommitResult(ProcessRuntimeTransitionOutcome.Applied, State(), [], [], [], []) { CommittedNow = true };
        var node = JsonSerializer.SerializeToNode(result, options)!.AsObject();
        Assert.False(node.ContainsKey("committedNow"));
        var contract = options.GetTypeInfo(typeof(ProcessRuntimeCommitResult));
        var property = Assert.Single(contract.Properties, item => item.Name == "committedNow");
        Assert.Null(property.Get);
        Assert.Null(property.Set);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Default_dispatch_identities_are_rejected(int missing) {
        ProcessRuntimeDispatchPrecondition condition = new(missing == 0 ? default : ProcessStepInstanceId.New(),
            missing == 1 ? default : DispatchClaimToken.New(), missing == 2 ? default : new DispatcherOwnerId("capture"));
        Assert.Throws<InvalidDataException>(condition.Validate);
    }

    private static ProcessRuntimeStateSnapshot State() {
        var runId = ProcessRunId.New();
        return new(runId, runId, ProcessInstancePlanId.New(), "sha256:commit-observation",
            ProcessRuntimeStatus.Active, [], [], [], new HashSet<ArtifactSlotId>(),
            new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
    }
}
