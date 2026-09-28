using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class MafLongJournalIntegrationTests {
    [Fact]
    public async Task A_full_process_window_persists_and_reopens_with_complete_response_protocol() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var journal = fixture.NewJournal();
        var text = new string('x', 128 * 1024);
        var response = MafToolProtocolCodec.Encode(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
        const int turns = 256;
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var current = lease.Bind();
            await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
            for (var turn = 0; turn < turns; turn++) {
                await journal.AdmitBatchAsync(lease, MafToolProtocolCodec.Digest(turn), response, [], default);
            }
        }

        var saved = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
        Assert.Equal(turns, saved.Batches.Length);
        Assert.False(saved.HasUnresolvedEffects);
        Assert.All(saved.Batches, batch => Assert.Equal(text, MafToolProtocolCodec.Decode<ChatResponse>(batch.Response).Text));
        var beyondLimit = saved with {
            Batches = Enumerable.Range(0, AgentToolJournalRecord.MaximumBatches + 1)
                .Select(ordinal => new AgentToolBatchRecord(new(Guid.NewGuid()), ordinal,
                    MafToolProtocolCodec.Digest(ordinal), response, [])).ToImmutableArray()
        };
        Assert.Throws<InvalidDataException>(() => beyondLimit.Validate());
    }
}
