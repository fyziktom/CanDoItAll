using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed partial class AgentToolJournalSchemaFiveTests {
    [Fact]
    public void Counted_json_size_matches_default_wire_for_all_supported_journal_versions() {
        foreach (var schema in Enumerable.Range(1, 5)) {
            var journal = Create(schema, effectsOnly: schema == AgentToolJournalRecord.EffectJournalSchemaVersion);
            journal.Validate();
            AssertDefaultJsonCount(journal);
            var restored = JsonSerializer.Deserialize<AgentToolJournalRecord>(JsonSerializer.SerializeToUtf8Bytes(journal))!;
            restored.Validate();
            AssertDefaultJsonCount(restored);
            Assert.Equal(AgentToolJournalJsonSize.Count(journal), AgentToolJournalJsonSize.Count(restored));
        }

        var owner = Create();
        owner = SetProposal(owner, Confirm(Proposal(owner))) with {
            ToolContinuationRequirement = AgentToolContinuationRequirement.ProviderInputRequired
        };
        owner.Validate();
        AssertDefaultJsonCount(owner);
        var json = JsonSerializer.Serialize(owner);
        Assert.Contains("\"HasUnresolvedEffects\":false", json);
        Assert.Contains("\"OwnerReceipt\":", json);
        Assert.DoesNotContain("\"DisclosureEvidence\":", json);
    }

    [Fact]
    public void Counted_json_size_preserves_default_escaping_and_large_single_tokens() {
        var original = Create();
        foreach (var content in new[] { "ascii", "\"\\\b\f\n\r\t", "<>&+'`", "é漢😀", "lone-\ud800-surrogate" }) {
            var journal = SetProposal(original, Confirm(Proposal(original)) with {
                OwnerReceipt = new(new("owner", "target"), Envelope(content))
            }) with { OriginalInput = new(Guid.NewGuid(), content) };
            journal.Validate();
            var expected = JsonSerializer.SerializeToUtf8Bytes(journal);
            Assert.Equal(expected.LongLength, AgentToolJournalJsonSize.Count(journal));
            Assert.True(expected.LongLength > Encoding.UTF8.GetByteCount(content));
        }

        var large = SetProposal(original, Confirm(Proposal(original)) with {
            OwnerReceipt = new(new("owner", "target"), Envelope(new string('x', AgentToolProtocolEnvelope.MaximumUtf8Bytes)))
        });
        large.Validate();
        AssertDefaultJsonCount(large);
        Assert.True(AgentToolJournalJsonSize.Count(large) > AgentToolProtocolEnvelope.MaximumUtf8Bytes);
    }

    [Fact]
    public void Whole_journal_encoded_boundary_remains_exact_at_limit_minus_equal_and_plus_one() {
        var original = Create();
        var template = Confirm(Proposal(original));
        var proposals = Enumerable.Range(0, 3).Select(index => template with {
            IntentId = new(Guid.NewGuid()), Ordinal = index, CallId = $"boundary-{index}",
            OwnerReceipt = new(new("owner", "target"), Envelope("x"))
        }).ToImmutableArray();
        var baseline = original with { Batches = [original.Batches[0] with { Proposals = proposals }] };
        var overhead = JsonSerializer.SerializeToUtf8Bytes(baseline).Length - proposals.Length;
        var full = Envelope(new string('x', AgentToolProtocolEnvelope.MaximumUtf8Bytes));
        foreach (var delta in new[] { -1, 0, 1 }) {
            var expectedLength = AgentToolJournalRecord.MaximumUtf8Bytes + delta;
            var lastLength = expectedLength - overhead - 2 * AgentToolProtocolEnvelope.MaximumUtf8Bytes;
            Assert.InRange(lastLength, 1, AgentToolProtocolEnvelope.MaximumUtf8Bytes);
            var receipts = new[] { full, full, Envelope(new string('x', lastLength)) };
            var journal = baseline with {
                Batches = [baseline.Batches[0] with {
                    Proposals = proposals.Select((proposal, index) => proposal with {
                        OwnerReceipt = proposal.OwnerReceipt! with { Receipt = receipts[index] }
                    }).ToImmutableArray()
                }]
            };
            var rawBytes = Encoding.UTF8.GetByteCount(journal.Batches[0].Response.PayloadJson) +
                journal.Batches[0].Proposals.Sum(proposal =>
                    (long)Encoding.UTF8.GetByteCount(proposal.Payload.ArgumentsJson) +
                    Encoding.UTF8.GetByteCount(proposal.Payload.SourcePreparation!.PayloadJson) +
                    Encoding.UTF8.GetByteCount(proposal.OwnerReceipt!.Receipt.PayloadJson) +
                    Encoding.UTF8.GetByteCount(proposal.OwnerReceipt.Effect.SourceKind) +
                    Encoding.UTF8.GetByteCount(proposal.OwnerReceipt.Effect.SourceId));
            Assert.True(rawBytes < AgentToolJournalRecord.MaximumUtf8Bytes);
            Assert.Equal(expectedLength, JsonSerializer.SerializeToUtf8Bytes(journal).Length);
            Assert.Equal((long)expectedLength, AgentToolJournalJsonSize.Count(journal));
            if (delta <= 0) {
                journal.Validate();
            } else {
                var failure = Assert.Throws<InvalidDataException>(() => journal.Validate());
                Assert.Equal("The tool admission journal exceeds its supported serialized payload bound.", failure.Message);
                Assert.Throws<InvalidDataException>(() => journal.Validate());
            }
        }
    }

    [Fact]
    public void Json_count_preserves_serialization_failures_and_invalid_copies_never_become_valid() {
        var valid = Create();
        valid.Validate();
        valid.Validate();
        var malformed = valid with { Batches = default };
        var expected = Record.Exception(() => {
            _ = JsonSerializer.SerializeToUtf8Bytes(malformed);
        });
        var actual = Record.Exception(() => {
            _ = AgentToolJournalJsonSize.Count(malformed);
        });
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType());
        for (var attempt = 0; attempt < 2; attempt++) {
            Assert.Throws<InvalidDataException>(() => malformed.Validate());
        }
        var changed = valid with { Revision = 0 };
        Assert.Throws<InvalidDataException>(() => changed.Validate());
        valid.Validate();
        AssertDefaultJsonCount(valid);
        Assert.Throws<ArgumentNullException>(() => AgentToolJournalJsonSize.Count(null!));
    }

    [Fact]
    public void Json_count_covers_every_proposal_and_trailing_fields_at_maximum_batch_cardinality() {
        var original = Create();
        var proposal = Proposal(original);
        var batches = Enumerable.Range(0, AgentToolJournalRecord.MaximumBatches).Select(batch =>
            original.Batches[0] with {
                Id = new(Guid.NewGuid()), Ordinal = batch,
                Proposals = Enumerable.Range(0, AgentToolJournalRecord.MaximumCallsPerBatch).Select(ordinal =>
                    proposal with {
                        IntentId = new(Guid.NewGuid()), Ordinal = ordinal, CallId = $"call-{ordinal}"
                    }).ToImmutableArray()
            }).ToImmutableArray();
        var journal = original with { Batches = batches, OriginalInput = new(Guid.NewGuid(), "trailing-input") };
        Assert.Equal(32_768, journal.Batches.Sum(batch => batch.Proposals.Length));
        var expected = JsonSerializer.SerializeToUtf8Bytes(journal).LongLength;
        Assert.Equal(expected, AgentToolJournalJsonSize.Count(journal));
        if (expected <= AgentToolJournalRecord.MaximumUtf8Bytes) {
            journal.Validate();
        } else {
            Assert.Throws<InvalidDataException>(() => journal.Validate());
        }
        var extended = journal with { OriginalInput = journal.OriginalInput! with { Content = "trailing-input!" } };
        Assert.Equal(expected + 1, JsonSerializer.SerializeToUtf8Bytes(extended).LongLength);
        Assert.Equal(expected + 1, AgentToolJournalJsonSize.Count(extended));
    }

    private static void AssertDefaultJsonCount(AgentToolJournalRecord journal)
        => Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(journal).LongLength, AgentToolJournalJsonSize.Count(journal));
}
