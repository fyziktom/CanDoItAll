using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class AgentToolDisclosureEvidencePersistenceTests {
    [Theory]
    [InlineData(EvidenceChange.Replace)]
    [InlineData(EvidenceChange.Remove)]
    [InlineData(EvidenceChange.RestampLegacy)]
    public async Task Independent_writer_cannot_replace_remove_or_fabricate_original_evidence(EvidenceChange change) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var journal = fixture.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
        using var bound = lease.Bind();
        var claim = await PrepareAsync(journal, lease);
        var evidence = change == EvidenceChange.RestampLegacy ? null : Evidence("original");
        await journal.CompleteInvocationAsync(claim, AgentToolAdmissionJournalFixture.Envelope("{\"originalId\":42}"),
            AgentToolEffectState.Committed, default, disclosureEvidence: evidence);
        var original = Assert.Single((await journal.ReadAsync(lease, default)).Batches[0].Proposals);

        var replacement = change == EvidenceChange.Remove ? null : Evidence("replacement");
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() =>
            ((ISandboxWorkspaceExecutionRunMutationStore)fixture.NewStore()).UpdateExecutionRunDetailAsync(fixture.Session.ExecutionRunId,
                detail => {
                    var current = detail.Run.ToolAdmission!;
                    var batch = current.Batches[0];
                    var next = current with {
                        Revision = current.Revision + 1,
                        Batches = current.Batches.SetItem(0, batch with {
                            Proposals = batch.Proposals.SetItem(0, original with { DisclosureEvidence = replacement })
                        })
                    };
                    return detail with { Run = detail.Run with { ToolAdmission = next, Revision = detail.Run.Revision + 1 } };
                }));
        Assert.Equal("tool-admission.conflict", denied.Code);
        var retained = Assert.Single((await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!.Batches[0].Proposals);
        Assert.Equal(original, retained);
    }

    [Theory]
    [InlineData((int)ExistingRunDetailCommitStage.JournalPersisted)]
    [InlineData((int)ExistingRunDetailCommitStage.RunPersisted)]
    [InlineData((int)ExistingRunDetailCommitStage.WorkspaceIndexPersisted)]
    public async Task Lost_file_checkpoint_ack_restores_result_and_evidence_together(int stageValue) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var fail = false;
        var failing = fixture.NewStore(stage => {
            if (fail && stage == (ExistingRunDetailCommitStage)stageValue) {
                fail = false;
                throw new IOException("Injected result/evidence acknowledgement loss.");
            }
        });
        var journal = fixture.NewJournal(failing);
        var evidence = Evidence("original");
        var result = AgentToolAdmissionJournalFixture.Envelope("{\"originalId\":42}");
        AgentToolBusinessIntentId intent;
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            var claim = await PrepareAsync(journal, lease);
            intent = claim.Proposal.IntentId;
            fail = true;
            await Assert.ThrowsAsync<IOException>(() => journal.CompleteInvocationAsync(claim, result,
                AgentToolEffectState.Committed, default, disclosureEvidence: evidence));
        }

        var restored = Assert.Single((await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!.Batches[0].Proposals);
        Assert.Equal(intent, restored.IntentId);
        Assert.Equal(AgentToolProposalState.Completed, restored.State);
        Assert.Equal(AgentToolEffectState.Committed, restored.EffectState);
        Assert.Equal(result, restored.Result);
        Assert.Equal(evidence, restored.DisclosureEvidence);
        Assert.Equal(restored, Assert.Single((await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!.Batches[0].Proposals));
    }

    private static async Task<AgentToolInvocationClaim> PrepareAsync(AgentToolAdmissionJournal journal, AgentToolRunLease lease) {
        await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
        var payload = AgentToolAdmissionJournalFixture.Payload();
        var admitted = await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
            [new("original-call", payload, false)], default);
        return await journal.ClaimInvocationAsync(lease, admitted.Batches[0].Id, "original-call", payload, default);
    }

    private static AgentToolProtocolEnvelope Evidence(string value)
        => AgentToolProtocolEnvelope.Create("fixture-owner-admission", 1, System.Text.Json.JsonSerializer.Serialize(new { value }));

    public enum EvidenceChange {
        Replace,
        Remove,
        RestampLegacy
    }
}
