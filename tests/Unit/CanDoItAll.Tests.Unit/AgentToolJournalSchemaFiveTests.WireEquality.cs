using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed partial class AgentToolJournalSchemaFiveTests {
    [Fact]
    public void Wire_equality_preserves_default_json_for_all_supported_journal_versions() {
        foreach (var schema in Enumerable.Range(1, 5)) {
            var journal = Create(schema, effectsOnly: schema == AgentToolJournalRecord.EffectJournalSchemaVersion);
            journal.Validate();
            var restored = RestoreWireJournal(journal);
            restored.Validate();
            Assert.NotSame(journal, restored);
            Assert.True(AssertWireEquality(journal, restored));
            Assert.True(AssertWireEquality(journal, journal with { }));
            AgentToolJournalTransitions.ValidatePersistence(journal, restored);
            AgentToolJournalTransitions.ValidatePersistence(journal, journal);
            AgentToolJournalTransitions.ValidatePersistence(journal, journal with { });
        }
    }

    [Fact]
    public void Wire_equality_preserves_conditional_members_and_exact_capture_offsets() {
        var legacy = Create(1, effectsOnly: false);
        AssertSameRevisionWireConflict(legacy, legacy with { ProviderDispatches = [] });
        var context = legacy with { RuntimeContext = new("original-context", WorkspaceScopeDescriptor.Sandbox) };
        AssertSameRevisionWireConflict(context, context with {
            RuntimeContext = context.RuntimeContext! with { Attachments = [] }
        });

        var journal = WithWireResult(Create(), "original-result");
        AssertSameRevisionWireConflict(journal, SetProposal(journal, Proposal(journal) with {
            DisclosureEvidence = Envelope("original-disclosure")
        }));
        AssertSameRevisionWireConflict(journal, journal with {
            ReceiptReconciliationCursor = new(journal.Batches[0].Id, Proposal(journal).IntentId, 0, 0)
        });
        AssertSameRevisionWireConflict(journal, journal with { ActiveDispatchLeaseId = Guid.NewGuid() });

        var typed = Create(4, effectsOnly: false);
        var attachment = typed.RuntimeContext!.Attachments[0] with {
            FreshUntilUtc = DateTimeOffset.UnixEpoch.AddHours(2)
        };
        typed = typed with { RuntimeContext = typed.RuntimeContext! with { Attachments = [attachment] } };
        foreach (var changed in new[] {
            attachment with { CapturedAtUtc = attachment.CapturedAtUtc.ToOffset(TimeSpan.FromHours(1)) },
            attachment with { FreshUntilUtc = attachment.FreshUntilUtc!.Value.ToOffset(TimeSpan.FromHours(1)) }
        }) {
            Assert.Equal(attachment, changed);
            AssertSameRevisionWireConflict(typed, typed with {
                RuntimeContext = typed.RuntimeContext! with { Attachments = [changed] }
            });
        }
    }

    [Fact]
    public void Wire_equality_preserves_encoding_replacement_and_large_single_tokens() {
        var original = Create();
        foreach (var content in new[] { "ascii", "\"\\\b\f\n\r\t", "<>&+'`", "é漢😀", "lone-\ud800-surrogate" }) {
            var journal = WithWireResult(original, content);
            journal.Validate();
            var restored = RestoreWireJournal(journal);
            restored.Validate();
            Assert.True(AssertWireEquality(journal, restored));
            AgentToolJournalTransitions.ValidatePersistence(journal, restored);
        }

        var malformedUtf16 = WithWireResult(original, "replacement-\ud800");
        var replacement = WithWireResult(original, "replacement-\ufffd");
        malformedUtf16.Validate();
        replacement.Validate();
        Assert.NotEqual(Proposal(malformedUtf16).Result!.PayloadJson, Proposal(replacement).Result!.PayloadJson);
        Assert.True(AssertWireEquality(malformedUtf16, replacement));
        AgentToolJournalTransitions.ValidatePersistence(malformedUtf16, replacement);

        var payload = new string('x', AgentToolProtocolEnvelope.MaximumUtf8Bytes);
        var large = WithWireResult(original, payload);
        large.Validate();
        var decoded = RestoreWireJournal(large);
        decoded.Validate();
        Assert.True(AssertWireEquality(large, decoded));
        var changed = WithWireResult(original, payload[..^1] + "y");
        changed.Validate();
        Assert.False(AssertWireEquality(large, changed));
    }

    [Fact]
    public void Wire_equality_checks_chunk_content_length_and_maximum_cardinality_trailing_fields() {
        var original = Create();
        var payload = new string('x', 96 * 1024);
        var baseline = WithWireResult(original, payload);
        foreach (var changed in new[] {
            "y" + payload[1..],
            payload[..(payload.Length / 2)] + "y" + payload[(payload.Length / 2 + 1)..],
            payload[..^1] + "y",
            payload[..^1],
            payload + "x"
        }) {
            var target = WithWireResult(original, changed);
            Assert.False(AssertWireEquality(baseline, target));
            Assert.False(AssertWireEquality(target, baseline));
        }

        var template = Proposal(original) with {
            Payload = new("x", 1, Envelope().Digest, "{}", AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt)
        };
        var batches = Enumerable.Range(0, AgentToolJournalRecord.MaximumBatches).Select(batch =>
            original.Batches[0] with {
                Id = new(Guid.NewGuid()), Ordinal = batch,
                Proposals = Enumerable.Range(0, AgentToolJournalRecord.MaximumCallsPerBatch).Select(ordinal =>
                    template with {
                        IntentId = new(Guid.NewGuid()), Ordinal = ordinal, CallId = $"call-{ordinal}"
                    }).ToImmutableArray()
            }).ToImmutableArray();
        var maximum = original with { Batches = batches, OriginalInput = new(Guid.NewGuid(), "trailing-input") };
        Assert.Equal(32_768, maximum.Batches.Sum(batch => batch.Proposals.Length));
        Assert.InRange(JsonSerializer.SerializeToUtf8Bytes(maximum).Length, 1, AgentToolJournalRecord.MaximumUtf8Bytes);
        maximum.Validate();
        Assert.True(AssertWireEquality(maximum, maximum with { }));
        Assert.False(AssertWireEquality(maximum, maximum with {
            OriginalInput = maximum.OriginalInput! with { Content = "trailing-input!" }
        }));
    }

    [Fact]
    public void Wire_comparison_preserves_late_serializer_failures_and_validation_order() {
        var original = Create(1, effectsOnly: false);
        original = original with { Segments = [original.Segments[0] with {
            RestartCheckpoint = Envelope(new string('x', 64 * 1024))
        }] };
        original.Validate();
        var malformed = original with { SchemaVersion = 2, Batches = default };
        var expected = Record.Exception(() => {
            _ = JsonSerializer.Serialize(original) == JsonSerializer.Serialize(malformed);
        });
        var actual = Record.Exception(() => {
            _ = AgentToolJournalWireEquality.AreEqual(original, malformed);
        });
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Throws<ArgumentNullException>(() => AgentToolJournalWireEquality.AreEqual(null!, original));
        Assert.Throws<ArgumentNullException>(() => AgentToolJournalWireEquality.AreEqual(original, null!));

        AgentToolJournalTransitions.ValidatePersistence(null, null);
        AgentToolJournalTransitions.ValidatePersistence(null, original);
        Assert.Throws<AgentToolAdmissionException>(() => AgentToolJournalTransitions.ValidatePersistence(original, null));
        var invalidCurrent = original with { Revision = 0 };
        var invalidProposed = original with { Session = null! };
        var firstFailure = Assert.Throws<ArgumentNullException>(() =>
            AgentToolJournalTransitions.ValidatePersistence(invalidCurrent, invalidProposed));
        Assert.Equal(nameof(AgentToolJournalRecord.Session), firstFailure.ParamName);
        Assert.Throws<ArgumentNullException>(() => AgentToolJournalTransitions.ValidatePersistence(null, invalidProposed));
        for (var attempt = 0; attempt < 2; attempt++) {
            Assert.Throws<InvalidDataException>(() => AgentToolJournalTransitions.ValidatePersistence(invalidCurrent, invalidCurrent));
            Assert.Throws<InvalidDataException>(() => AgentToolJournalTransitions.ValidatePersistence(invalidCurrent, null));
            Assert.Throws<InvalidDataException>(() => AgentToolJournalTransitions.ValidatePersistence(original, invalidCurrent));
        }
        AgentToolJournalTransitions.ValidatePersistence(original, original);
    }

    private static AgentToolJournalRecord RestoreWireJournal(AgentToolJournalRecord journal)
        => JsonSerializer.Deserialize<AgentToolJournalRecord>(JsonSerializer.SerializeToUtf8Bytes(journal))!;

    private static AgentToolJournalRecord WithWireResult(AgentToolJournalRecord journal, string payload)
        => SetProposal(journal, Proposal(journal) with { Result = Envelope(payload) });

    private static bool AssertWireEquality(AgentToolJournalRecord left, AgentToolJournalRecord right) {
        var expected = JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right);
        Assert.Equal(expected, AgentToolJournalWireEquality.AreEqual(left, right));
        return expected;
    }

    private static void AssertSameRevisionWireConflict(AgentToolJournalRecord current, AgentToolJournalRecord proposed) {
        current.Validate();
        proposed.Validate();
        Assert.Equal(current.Revision, proposed.Revision);
        Assert.False(AssertWireEquality(current, proposed));
        Assert.Throws<AgentToolAdmissionException>(() => AgentToolJournalTransitions.ValidatePersistence(current, proposed));
    }
}
