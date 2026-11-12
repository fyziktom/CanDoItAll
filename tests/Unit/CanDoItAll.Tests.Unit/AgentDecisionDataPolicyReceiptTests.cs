using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentDecisionDataPolicyReceiptTests {
    private static readonly DateTimeOffset AcceptedAt = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Publication_requires_both_exact_policy_and_receipt_before_it_can_acknowledge() {
        var command = Command();
        var agent = new AgentDefinition(command.Expectation.AgentId, "Publication", "", "", "", AgentLifecycleStatus.Active,
            null, "", default, default, 0, false, false, "{\"ordinary\":7}", false, "", AgentPermissionsPolicy.Default, [], [], AcceptedAt, AcceptedAt);
        var original = SandboxWorkspaceDocument.Empty.ToCatalog() with {
            Agents = [agent, agent with { Id = Guid.NewGuid(), Name = "Unrelated Agent" }], CatalogDataRevision = command.Expectation.CatalogRevision
        };
        var publication = new AgentPolicyPublication(command, AcceptedAt);
        var prepared = publication.Apply(original) with { CatalogDataRevision = original.CatalogDataRevision.Next() };
        var targetKeys = FileSandboxWorkspaceWriteSet.Catalog(original, prepared with { AgentDecisionDataPolicyReceipts = null });
        var publicationKeys = FileSandboxWorkspaceWriteSet.Catalog(original, prepared);
        Assert.True(targetKeys.Contains(publicationKeys));
        Assert.True(publicationKeys.Contains(targetKeys));
        Assert.Null(original.AgentDecisionDataPolicyReceipts);
        Assert.False(publication.WriteEntered);
        Assert.Null(publication.Acknowledgement);
        Assert.Throws<InvalidOperationException>(() => publication.EnterWrite(prepared with { AgentDecisionDataPolicyReceipts = null }));
        Assert.Throws<InvalidOperationException>(() => publication.EnterWrite(prepared with { Agents = [agent] }));
        Assert.Throws<InvalidOperationException>(() => publication.EnterWrite(prepared with { CatalogDataRevision = prepared.CatalogDataRevision.Next() }));
        publication.EnterWrite(prepared);
        Assert.True(publication.WriteEntered);
        Assert.Null(publication.Acknowledgement);
        publication.Acknowledge();
        Assert.Equal(command, publication.Acknowledgement!.Command);
        Assert.Equal(Assert.Single(prepared.AgentDecisionDataPolicyReceipts!.Receipts).Snapshot, publication.Acknowledgement.Snapshot);
        using var configuration = JsonDocument.Parse(prepared.Agents.Single(item => item.Id == agent.Id).ConfigurationJson);
        Assert.Equal(7, configuration.RootElement.GetProperty("ordinary").GetInt32());
    }

    [Fact]
    public void Historical_receipt_requires_the_complete_original_command_and_preserves_exact_int64_values() {
        var original = Command(long.MaxValue - 1);
        var receipt = new AgentDecisionDataPolicyReceipt(original, AcceptedAt);
        var log = new AgentDecisionDataPolicyReceiptLog([receipt]);
        var roundTrip = JsonSerializer.Deserialize<AgentDecisionDataPolicyReceiptLog>(JsonSerializer.Serialize(log))!;
        var accepted = Assert.IsType<AgentDecisionDataPolicyLookup.Accepted>(roundTrip.Find(original, AcceptedAt));
        Assert.Equal(receipt, accepted.Receipt);
        Assert.Equal(long.MaxValue, accepted.Receipt.Snapshot.Expectation.CatalogRevision.Value);
        Assert.Equal(original.Expectation.Workspace.DatabaseProfileGeneration, accepted.Receipt.Snapshot.Expectation.Workspace.DatabaseProfileGeneration);
        var expected = original.Expectation;
        var workspace = expected.Workspace;
        foreach (var command in new[] {
            new ReplaceAgentDecisionDataPolicyCommand(original.OperationId, expected, new(ReadBasisMode.StepSnapshot)),
            new(original.OperationId, new(workspace, Guid.NewGuid(), expected.CatalogRevision), original.Policy),
            new(original.OperationId, new(workspace, expected.AgentId, new(1)), original.Policy),
            new(original.OperationId, new(new(workspace.DatabaseProfileId, workspace.WorkspaceScope, new(1)), expected.AgentId, expected.CatalogRevision), original.Policy),
            new(original.OperationId, new(new(Guid.NewGuid(), workspace.WorkspaceScope, workspace.DatabaseProfileGeneration), expected.AgentId, expected.CatalogRevision), original.Policy),
            new(original.OperationId, new(new(workspace.DatabaseProfileId, WorkspaceScopeDescriptor.Project("other"), workspace.DatabaseProfileGeneration), expected.AgentId, expected.CatalogRevision), original.Policy)
        }) {
            Assert.IsType<AgentDecisionDataPolicyLookup.Conflict>(roundTrip.Find(command, AcceptedAt));
        }
        Assert.IsType<AgentDecisionDataPolicyLookup.NotObserved>(roundTrip.Find(Command(), AcceptedAt));
    }

    [Fact]
    public void Capacity_never_evicts_live_evidence_and_expiration_is_exact() {
        var original = Command();
        var log = new AgentDecisionDataPolicyReceiptLog(Enumerable.Range(0, AgentDecisionDataPolicyReceiptLog.MaximumReceipts)
            .Select(_ => new AgentDecisionDataPolicyReceipt(Command(), AcceptedAt)).ToImmutableArray());
        var expiry = AcceptedAt + AgentDecisionDataPolicyReceiptLog.Retention;
        Assert.IsType<AgentDecisionDataPolicyLookup.Accepted>(log.Find(log.Receipts[0].Command, expiry.AddTicks(-1)));
        Assert.Throws<AgentDecisionDataPolicyReceiptCapacityException>(() => log.Append(original, expiry.AddTicks(-1)));
        Assert.IsType<AgentDecisionDataPolicyLookup.NotObserved>(log.Find(log.Receipts[0].Command, expiry));
        var next = log.Append(original, expiry);
        Assert.Equal(original, Assert.Single(next.Receipts).Command);
        Assert.IsType<AgentDecisionDataPolicyLookup.NotObserved>(next.Find(log.Receipts[0].Command, expiry));
        Assert.Throws<AgentDecisionDataPolicyReceiptConflictException>(() => next.Append(original, expiry));
        Assert.Equal(AgentDecisionDataPolicyReceiptLog.MaximumReceipts, log.Receipts.Length);
    }

    [Fact]
    public void Maximum_log_and_unicode_scope_round_trip_in_the_real_pretty_catalog_format() {
        var command = Command(long.MaxValue - 1, WorkspaceScopeDescriptor.Organization(new string('ž', 256)));
        var log = new AgentDecisionDataPolicyReceiptLog(Enumerable.Range(0, AgentDecisionDataPolicyReceiptLog.MaximumReceipts)
            .Select(_ => new AgentDecisionDataPolicyReceipt(new(Guid.NewGuid(), command.Expectation, command.Policy), AcceptedAt))
            .ToImmutableArray());
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var catalog = SandboxWorkspaceDocument.Empty.ToCatalog() with { AgentDecisionDataPolicyReceipts = log };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(catalog, options);
        var restored = JsonSerializer.Deserialize<SandboxWorkspaceCatalog>(bytes, options)!;
        Assert.Equal(log.Receipts.ToArray(), restored.AgentDecisionDataPolicyReceipts!.Receipts.ToArray());
        Assert.InRange(bytes.Length, 1, AgentDecisionDataPolicyReceiptLog.MaximumReceipts * AgentDecisionDataPolicyReceipt.MaximumUtf8Bytes);
    }

    [Fact]
    public void Missing_legacy_log_is_preserved_but_explicit_null_or_malformed_log_is_not_absence() {
        var catalog = SandboxWorkspaceDocument.Empty.ToCatalog();
        var json = JsonSerializer.Serialize(catalog, JsonSerializerOptions.Web);
        Assert.DoesNotContain("agentDecisionDataPolicyReceipts", json, StringComparison.Ordinal);
        Assert.Null(JsonSerializer.Deserialize<SandboxWorkspaceCatalog>(json, JsonSerializerOptions.Web)!.AgentDecisionDataPolicyReceipts);
        foreach (var malformed in new[] { "null", "{}", "[null]", "[{}]" }) {
            var root = JsonNode.Parse(json)!.AsObject();
            root["agentDecisionDataPolicyReceipts"] = JsonNode.Parse(malformed);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SandboxWorkspaceCatalog>(root.ToJsonString(), JsonSerializerOptions.Web));
        }
        var log = AgentDecisionDataPolicyReceiptLog.Empty.Append(Command(), AcceptedAt);
        var document = SandboxWorkspaceDocument.Empty with { AgentDecisionDataPolicyReceipts = log };
        Assert.Same(log, SandboxWorkspaceDocument.Combine(document.ToCatalog(), document.ToExecutionState()).AgentDecisionDataPolicyReceipts);
    }

    [Fact]
    public void Strict_receipt_refuses_missing_duplicate_unknown_null_and_oversized_fields() {
        var valid = JsonSerializer.Serialize(new AgentDecisionDataPolicyReceipt(Command(), AcceptedAt));
        foreach (var property in JsonNode.Parse(valid)!.AsObject().Select(item => item.Key).ToArray()) {
            var absent = JsonNode.Parse(valid)!.AsObject();
            absent.Remove(property);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AgentDecisionDataPolicyReceipt>(absent.ToJsonString()));
        }
        foreach (var malformed in new[] {
            "null", "[]", "{}", valid[..^1] + ",\"schemaVersion\":1}", valid[..^1] + ",\"future\":1}",
            valid.Replace("\"policy\":{", "\"policy\":{\"future\":1,", StringComparison.Ordinal),
            valid.Replace("\"readBasisMode\":0", "\"readBasisMode\":0,\"readBasisMode\":1", StringComparison.Ordinal),
            valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":2", StringComparison.Ordinal),
            valid[..^1] + new string(' ', AgentDecisionDataPolicyReceipt.MaximumUtf8Bytes) + "}"
        }) {
            Assert.NotEqual(valid, malformed);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AgentDecisionDataPolicyReceipt>(malformed));
        }
        var duplicate = $"[{valid},{valid}]";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AgentDecisionDataPolicyReceiptLog>(duplicate));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AgentDecisionDataPolicyReceiptLog>(
            "[" + string.Join(',', Enumerable.Repeat(valid, AgentDecisionDataPolicyReceiptLog.MaximumReceipts + 1)) + "]"));
    }

    [Fact]
    public void Invalid_identity_revision_scope_and_time_cannot_be_historical_acceptance() {
        var json = JsonSerializer.Serialize(new AgentDecisionDataPolicyReceipt(Command(), AcceptedAt));
        foreach (var mutation in new Action<JsonObject>[] {
            root => root["operationId"] = Guid.Empty,
            root => root["agentId"] = Guid.Empty,
            root => root["databaseProfileId"] = Guid.Empty,
            root => root["databaseProfileGeneration"] = -1,
            root => root["catalogRevision"] = 0,
            root => root["catalogRevision"] = long.MaxValue,
            root => root["scopeKind"] = 900,
            root => root["scopeKey"] = "Not Canonical",
            root => root["scopeKey"] = new string('a', 257),
            root => root["scopeKey"] = null,
            root => root["policy"] = null,
            root => root["acceptedAtUtc"] = default(DateTimeOffset),
            root => root["acceptedAtUtc"] = DateTimeOffset.MaxValue,
            root => root["acceptedAtUtc"] = AcceptedAt.ToOffset(TimeSpan.FromHours(1))
        }) {
            var root = JsonNode.Parse(json)!.AsObject();
            mutation(root);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AgentDecisionDataPolicyReceipt>(root.ToJsonString()));
        }
    }

    private static ReplaceAgentDecisionDataPolicyCommand Command(long revision = 1, WorkspaceScopeDescriptor? scope = null)
        => new(Guid.NewGuid(), new(new(Guid.NewGuid(), scope ?? WorkspaceScopeDescriptor.Sandbox, new(long.MaxValue)),
            Guid.NewGuid(), new(revision)), new(ReadBasisMode.RunSnapshot, MaximumWorkDuration: TimeSpan.FromTicks(1), MaximumBasisAge: TimeSpan.FromTicks(1)));
}
