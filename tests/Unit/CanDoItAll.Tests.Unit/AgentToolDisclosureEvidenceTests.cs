using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.Runtime;

public sealed class AgentToolDisclosureEvidenceTests {
    [Fact]
    public async Task Nested_async_invocations_keep_their_original_owner_evidence_separate() {
        var outerEvidence = Evidence("outer");
        var innerEvidence = Evidence("inner");
        using var outer = AgentToolInvocationEffectScope.Begin();
        AgentToolInvocationEffectScope.RecordDisclosureEvidence(outerEvidence);
        AgentToolInvocationEffectScope.RecordCommitted("fixture-owner", "original-id");
        await Task.Yield();
        using (var inner = AgentToolInvocationEffectScope.Begin()) {
            Assert.Null(inner.DisclosureEvidence);
            Assert.Null(inner.CommittedEffect);
            AgentToolInvocationEffectScope.RecordDisclosureEvidence(innerEvidence);
            await Task.Yield();
            Assert.Equal(innerEvidence, inner.DisclosureEvidence);
        }
        AgentToolInvocationEffectScope.RecordDisclosureEvidence(outerEvidence);
        Assert.Equal(outerEvidence, outer.DisclosureEvidence);
        Assert.Equal(new AgentToolCommittedEffect("fixture-owner", "original-id"), outer.CommittedEffect);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Repeated_capture_accepts_only_the_same_immutable_evidence(bool changed) {
        using var effect = AgentToolInvocationEffectScope.Begin();
        var original = Evidence("original-lifetime");
        AgentToolInvocationEffectScope.RecordDisclosureEvidence(original);
        var next = Evidence(changed ? "replacement-lifetime" : "original-lifetime");
        if (changed) {
            Assert.Throws<InvalidOperationException>(() => AgentToolInvocationEffectScope.RecordDisclosureEvidence(next));
        } else {
            AgentToolInvocationEffectScope.RecordDisclosureEvidence(next);
        }
        Assert.Equal(original, effect.DisclosureEvidence);
    }

    [Fact]
    public void An_owner_call_without_an_invocation_scope_does_not_seed_the_next_invocation() {
        AgentToolInvocationEffectScope.RecordDisclosureEvidence(Evidence("ordinary-owner-call"));
        using var next = AgentToolInvocationEffectScope.Begin();
        Assert.Null(next.DisclosureEvidence);
    }

    [Fact]
    public void Legacy_proposal_json_keeps_its_exact_shape_when_evidence_is_absent() {
        var arguments = "{}";
        var payload = new AgentToolPreparedPayload("evidence_fixture_read", 1,
            AgentToolProtocolEnvelope.ComputeDigest(arguments), arguments, AgentToolProposalEffect.Read,
            AgentToolProposalRecovery.RevalidateAndRead);
        var proposal = new AgentToolProposalRecord(new(Guid.NewGuid()), 0, "diagnostic-call", payload, false,
            AgentToolProposalState.Completed, ExecutionApprovalStatus.Approved, payload.Digest,
            DispatchClaimId: Guid.NewGuid(), Result: Evidence("legacy-result"), EffectState: AgentToolEffectState.None);
        var saved = JsonSerializer.Serialize(proposal);
        Assert.DoesNotContain(nameof(AgentToolProposalRecord.DisclosureEvidence), saved, StringComparison.Ordinal);
        var restored = JsonSerializer.Deserialize<AgentToolProposalRecord>(saved)!;
        Assert.Null(restored.DisclosureEvidence);
        Assert.Equal(proposal, restored);
        Assert.Equal(saved, JsonSerializer.Serialize(restored));
    }

    private static AgentToolProtocolEnvelope Evidence(string value)
        => AgentToolProtocolEnvelope.Create("fixture-owner-disclosure", 1, JsonSerializer.Serialize(new { value }));
}
