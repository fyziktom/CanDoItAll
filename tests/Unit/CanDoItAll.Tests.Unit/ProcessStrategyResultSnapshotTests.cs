using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessStrategyResultSnapshotTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Live_owner_evidence_stays_in_memory_and_cannot_arrive_through_JSON(bool adapter) {
        var evidence = new ProcessExecutionOwnerEvidence<string>("live-only-owner-evidence");
        var result = Result() with { OwnerEvidence = evidence };
        Assert.True(ProcessStrategyResultSnapshot.TryCapture(result, out var snapshot, out _));
        Assert.Same(evidence, snapshot.OwnerEvidence);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = adapter ? JsonSerializer.Serialize(ProcessExecutionAdapterResult.Succeeded("Success", Hash) with {
            OwnerEvidence = evidence
        }, options) : JsonSerializer.Serialize(snapshot, options);
        Assert.DoesNotContain("live-only-owner-evidence", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ownerEvidence", json, StringComparison.Ordinal);
        var supplied = json[..^1] + ",\"ownerEvidence\":{\"workTicks\":0}}";
        Assert.Null(adapter ? JsonSerializer.Deserialize<ProcessExecutionAdapterResult>(supplied, options)!.OwnerEvidence
            : JsonSerializer.Deserialize<StrategyResultEnvelope>(supplied, options)!.OwnerEvidence);
    }

    [Fact]
    public void Captured_artifacts_and_host_facts_do_not_follow_mutable_caller_collections() {
        var artifact = new ProducedArtifactRef(ArtifactInstanceId.New(), ArtifactSlotId.New(), Hash);
        List<ProducedArtifactRef> artifacts = [artifact];
        var fact = new ProcessHostCapabilityFact(new("host.fixture"), ProcessHostCapabilityAvailability.Available,
            ProcessHostCapabilityReason.Ready, ProcessHostExecutionPort.ManagedProcessHost);
        List<ProcessHostCapabilityFact> facts = [fact];
        var input = Result() with { ProducedArtifacts = artifacts, HostCapabilityEvidence = new(new("fixture"), facts) };
        Assert.True(ProcessStrategyResultSnapshot.TryCapture(input, out var captured, out var refusal));
        artifacts.Clear();
        facts.Clear();
        Assert.Equal(ProcessStrategyResultRefusal.None, refusal);
        Assert.Equal(artifact, Assert.Single(captured!.ProducedArtifacts));
        Assert.Equal(fact, Assert.Single(captured.HostCapabilityEvidence!.Capabilities));
    }

    [Theory]
    [InlineData(Invalid.EmptyExecution)]
    [InlineData(Invalid.NullArtifact)]
    [InlineData(Invalid.TooManyArtifacts)]
    [InlineData(Invalid.NullDiagnostics)]
    [InlineData(Invalid.EmptyProfile)]
    [InlineData(Invalid.RepeatedCapability)]
    public void Invalid_or_unbounded_inputs_cannot_acquire_a_snapshot(Invalid change) {
        var fact = new ProcessHostCapabilityFact(new("host.fixture"), ProcessHostCapabilityAvailability.Available,
            ProcessHostCapabilityReason.Ready, ProcessHostExecutionPort.ManagedProcessHost);
        var input = change switch {
            Invalid.EmptyExecution => Result() with { ExecutionRunId = default(ProcessExecutionRunId) },
            Invalid.NullArtifact => Result() with { ProducedArtifacts = [null!] },
            Invalid.TooManyArtifacts => Result() with { ProducedArtifacts = Enumerable.Repeat(
                new ProducedArtifactRef(ArtifactInstanceId.New(), ArtifactSlotId.New(), Hash), ProcessStrategyResultLimits.MaximumArtifacts + 1).ToArray() },
            Invalid.NullDiagnostics => Result() with { Diagnostics = null! },
            Invalid.EmptyProfile => Result() with { HostCapabilityEvidence = new(default, []) },
            Invalid.RepeatedCapability => Result() with { HostCapabilityEvidence = new(new("fixture"), [fact, fact]) },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        Assert.False(ProcessStrategyResultSnapshot.TryCapture(input, out var captured, out var refusal));
        Assert.Null(captured);
        Assert.Equal(change is Invalid.EmptyProfile or Invalid.RepeatedCapability
            ? ProcessStrategyResultRefusal.InvalidHostCapabilities : ProcessStrategyResultRefusal.InvalidReceipt, refusal);
    }

    private static string Hash => "sha256:" + new string('a', 64);
    private static StrategyResultEnvelope Result() => new(new("fixture"), "1.0.0", Guid.NewGuid(), StrategyOutcome.Succeeded, [], [], [], [], Hash);
    public enum Invalid { EmptyExecution, NullArtifact, TooManyArtifacts, NullDiagnostics, EmptyProfile, RepeatedCapability }
}
