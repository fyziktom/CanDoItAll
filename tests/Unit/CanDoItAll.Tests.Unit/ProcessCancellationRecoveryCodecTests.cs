using System.Text.Json.Nodes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessCancellationRecoveryCodecTests {
    [Fact]
    public void Every_original_intent_retains_all_strong_ids_expectations_actor_and_timestamp() {
        foreach (var (_, intent) in RecoveryContractFixture.Intents()) {
            var json = ProcessCancellationRecoveryCodec.WriteIntent(RecoveryContractFixture.Root, intent);
            var restored = ProcessCancellationRecoveryCodec.ReadIntent(RecoveryContractFixture.Root, json);
            Assert.Equal(intent, restored);
            Assert.Equal(json, ProcessCancellationRecoveryCodec.WriteIntent(RecoveryContractFixture.Root, restored));
        }
        Assert.Null(ProcessCancellationRecoveryCodec.ReadIntent(RecoveryContractFixture.Root, null));
    }

    [Fact]
    public void Positions_round_trip_with_exact_nullable_keysets() {
        foreach (var position in RecoveryContractFixture.Positions()) {
            var json = ProcessCancellationRecoveryCodec.WritePosition(position);
            Assert.Equal(position, ProcessCancellationRecoveryCodec.ReadPosition(json));
        }
    }

    [Fact]
    public void Existing_v1_positions_remain_byte_exact_without_new_cursor_fields_or_false_flags() {
        string[] original = ["{\"version\":1,\"stage\":0}",
            "{\"version\":1,\"stage\":0,\"runAfter\":\"6a146dbd-1c0a-4a2d-9dda-154bfb66eb53\"}",
            "{\"version\":1,\"stage\":1,\"decisionId\":\"6a146dbd-1c0a-4a2d-9dda-154bfb66eb53\",\"generation\":2}",
            "{\"version\":1,\"stage\":4}"];
        foreach (var json in original) {
            var restored = ProcessCancellationRecoveryCodec.ReadPosition(json);
            Assert.Null(restored.CurrentRun);
            Assert.Null(restored.ExecutionAfter);
            Assert.False(restored.CycleHadPending);
            Assert.Equal(json, ProcessCancellationRecoveryCodec.WritePosition(restored));
        }
        Assert.Throws<InvalidDataException>(() => ProcessCancellationRecoveryCodec.ReadPosition("{\"version\":1,\"stage\":0,\"cycleHadPending\":false}"));
    }

    [Fact]
    public void Unsupported_versions_unknown_fields_duplicate_fields_and_noncanonical_text_are_refused() {
        var json = ProcessCancellationRecoveryCodec.WriteIntent(RecoveryContractFixture.Root, RecoveryContractFixture.Intents()[0].Intent)!;
        foreach (var invalid in new[] {
            json.Replace("\"version\":1", "\"version\":2", StringComparison.Ordinal),
            json.Insert(1, "\"extra\":1,"), json.Insert(1, "\"version\":1,"), " " + json,
            json.Replace("\"kind\":0", "\"kind\":99", StringComparison.Ordinal),
            json.Replace("\"context\":{", "\"Context\":{", StringComparison.Ordinal)
        }) {
            Assert.Throws<InvalidDataException>(() => ProcessCancellationRecoveryCodec.ReadIntent(RecoveryContractFixture.Root, invalid));
        }
    }

    [Fact]
    public void Wrong_root_actor_empty_actions_and_missing_context_cannot_be_reinterpreted() {
        var json = ProcessCancellationRecoveryCodec.WriteIntent(RecoveryContractFixture.Root, RecoveryContractFixture.Intents()[0].Intent)!;
        Assert.Throws<InvalidDataException>(() => ProcessCancellationRecoveryCodec.ReadIntent(new ProcessRunId(Guid.NewGuid()), json));
        foreach (var transform in new Action<JsonObject>[] {
            document => document["context"] = null,
            document => document["context"]!["actionId"] = Guid.Empty.ToString("D"),
            document => document["context"]!["actorKind"] = 2,
            document => document["context"]!["actorId"] = "another-owner",
            document => document["context"]!["occurredAtUtc"] = "2026-10-22T09:00:00+01:00"
        }) {
            var document = JsonNode.Parse(json)!.AsObject();
            transform(document);
            Assert.Throws<InvalidDataException>(() => ProcessCancellationRecoveryCodec.ReadIntent(RecoveryContractFixture.Root, document.ToJsonString()));
        }
    }

    [Fact]
    public void Mixed_terminal_scope_and_cursor_fields_are_rejected_without_losing_original_identity() {
        var intents = RecoveryContractFixture.Intents();
        var final = JsonNode.Parse(ProcessCancellationRecoveryCodec.WriteIntent(RecoveryContractFixture.Root, intents[2].Intent)!)!.AsObject();
        final["terminal"]!["childId"] = Guid.NewGuid().ToString("D");
        Assert.Throws<InvalidDataException>(() => ProcessCancellationRecoveryCodec.ReadIntent(RecoveryContractFixture.Root, final.ToJsonString()));
        var account = JsonNode.Parse(ProcessCancellationRecoveryCodec.WriteIntent(RecoveryContractFixture.Root, intents[1].Intent)!)!.AsObject();
        account["terminal"]!["budgetRevision"] = 0;
        Assert.Throws<InvalidDataException>(() => ProcessCancellationRecoveryCodec.ReadIntent(RecoveryContractFixture.Root, account.ToJsonString()));
        var cancel = JsonNode.Parse(ProcessCancellationRecoveryCodec.WriteIntent(RecoveryContractFixture.Root, intents[0].Intent)!)!.AsObject();
        cancel["terminal"] = final["terminal"]!.DeepClone();
        Assert.Throws<InvalidDataException>(() => ProcessCancellationRecoveryCodec.ReadIntent(RecoveryContractFixture.Root, cancel.ToJsonString()));
        Assert.Throws<InvalidDataException>(() => ProcessCancellationRecoveryCodec.ReadPosition("{\"version\":1,\"stage\":1,\"generation\":1}"));
        Assert.Throws<InvalidDataException>(() => ProcessCancellationRecoveryCodec.ReadPosition("{\"version\":2,\"stage\":0}"));
    }

    [Fact]
    public void Bounded_utf8_records_reject_oversized_empty_and_malformed_input() {
        foreach (var text in new[] { "", " ", "null", "{", new string('é', 5000) }) {
            Assert.Throws<InvalidDataException>(() => ProcessCancellationRecoveryCodec.ReadIntent(RecoveryContractFixture.Root, text));
            Assert.Throws<InvalidDataException>(() => ProcessCancellationRecoveryCodec.ReadPosition(text));
        }
    }

    [Fact]
    public void Claimed_rows_require_matching_active_lease_and_stage_bound_intent() {
        var claim = RecoveryContractFixture.Claim();
        ProcessCancellationRecoveryEntity row = new() {
            RootRunId = claim.RootRunId.Value, Origin = claim.Origin, BarrierCommandId = claim.BarrierCommandId!.Value.Value,
            LeaseToken = claim.LeaseToken, LeaseGeneration = claim.LeaseGeneration, LeaseExpiresAtUtc = claim.LeaseExpiresAtUtc,
            PositionJson = ProcessCancellationRecoveryCodec.WritePosition(claim.Position)
        };
        Assert.Equal(claim, ProcessCancellationRecoveryCodec.ReadClaim(row));
        row.IntentJson = ProcessCancellationRecoveryCodec.WriteIntent(claim.RootRunId, RecoveryContractFixture.Intents()[4].Intent);
        Assert.Throws<InvalidDataException>(() => ProcessCancellationRecoveryCodec.ReadClaim(row));
        row.IntentJson = null;
        row.Completed = true;
        Assert.Throws<InvalidDataException>(() => ProcessCancellationRecoveryCodec.ReadClaim(row));
        row.Completed = false;
        row.LeaseToken = null;
        Assert.Throws<InvalidDataException>(() => ProcessCancellationRecoveryCodec.ReadClaim(row));
    }
}
