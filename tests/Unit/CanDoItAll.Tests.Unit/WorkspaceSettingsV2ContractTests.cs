using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceSettingsV2ContractTests {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly DatasetNamespaceId Namespace = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly WorkspaceSettingsHead Head = new(Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("33333333-3333-3333-3333-333333333333"));

    [Fact]
    public void V2_read_preserves_legacy_absence_and_requires_a_head_for_configured_policy() {
        var options = new JsonSerializerOptions(Json) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        foreach (var expectation in new[] { Empty(), Existing() }) {
            var snapshot = new WorkspaceSettingsSnapshotV2(expectation, Values(), null);
            var wire = JsonSerializer.SerializeToNode(snapshot, options)!.AsObject();
            Assert.Equal(2, wire["schemaVersion"]!.GetValue<int>());
            Assert.True(wire.ContainsKey("decisionDataPolicy"));
            Assert.Null(wire["decisionDataPolicy"]);
            Assert.Equal(snapshot, wire.Deserialize<WorkspaceSettingsSnapshotV2>(options));
        }
        var configured = new WorkspaceSettingsSnapshotV2(Existing(), Values(), Policy());
        Assert.Equal(configured, JsonSerializer.Deserialize<WorkspaceSettingsSnapshotV2>(JsonSerializer.Serialize(configured, Json), Json));
        Assert.Throws<ArgumentException>(() => new WorkspaceSettingsSnapshotV2(Empty(), Values(), Policy()));
        var contradictory = JsonSerializer.SerializeToNode(configured, Json)!.AsObject();
        contradictory["expectation"] = JsonSerializer.SerializeToNode(Empty(), Json);
        Assert.Throws<JsonException>(() => contradictory.Deserialize<WorkspaceSettingsSnapshotV2>(Json));
    }

    [Fact]
    public void Replacement_captures_the_original_expectation_exact_values_and_complete_policy() {
        foreach (var expected in new[] { Empty(), Existing() }) {
            var command = new ReplaceWorkspaceSettingsV2Command(expected, Values(), Policy());
            var wire = JsonSerializer.SerializeToNode(command, Json)!.AsObject();
            Assert.Equal(new[] { "decisionDataPolicy", "expected", "schemaVersion", "values" },
                wire.Select(property => property.Key).Order(StringComparer.Ordinal));
            Assert.Equal(expected, wire["expected"]!.Deserialize<WorkspaceSettingsExpectation>(Json));
            Assert.Equal("  Original  ", wire["values"]!["workspaceName"]!.GetValue<string>());
            Assert.Equal(command, wire.Deserialize<ReplaceWorkspaceSettingsV2Command>(Json));
            Assert.Equal(Policy(), command.DecisionDataPolicy);
        }
        Assert.Throws<ArgumentNullException>(() => new ReplaceWorkspaceSettingsV2Command(null!, Values(), Policy()));
        Assert.Throws<ArgumentNullException>(() => new ReplaceWorkspaceSettingsV2Command(Existing(), null!, Policy()));
        Assert.Throws<ArgumentNullException>(() => new ReplaceWorkspaceSettingsV2Command(Existing(), Values(), null!));
        Assert.Throws<ArgumentNullException>(() => new WorkspaceSettingsSnapshotV2(null!, Values(), null));
        Assert.Throws<ArgumentNullException>(() => new WorkspaceSettingsSnapshotV2(Existing(), null!, null));
    }

    [Fact]
    public void V2_contracts_reject_missing_duplicate_unknown_or_wrong_version_members() {
        AssertTopLevelContract(new ReplaceWorkspaceSettingsV2Command(Existing(), Values(), Policy()));
        AssertTopLevelContract(new WorkspaceSettingsSnapshotV2(Existing(), Values(), Policy()));
        var command = CommandNode();
        command["decisionDataPolicy"] = null;
        Assert.Throws<JsonException>(() => command.Deserialize<ReplaceWorkspaceSettingsV2Command>(Json));
        command["decisionDataPolicy"] = JsonNode.Parse(DecisionDataPolicyOverrideCodec.Serialize(new()));
        Assert.Throws<JsonException>(() => command.Deserialize<ReplaceWorkspaceSettingsV2Command>(Json));
        command["decisionDataPolicy"] = JsonSerializer.SerializeToNode(Policy(), Json);
        command["expected"] = null;
        Assert.Throws<JsonException>(() => command.Deserialize<ReplaceWorkspaceSettingsV2Command>(Json));
    }

    [Fact]
    public void Nested_original_values_head_and_complete_policy_reject_ambiguous_fields_at_the_serializer_boundary() {
        var command = CommandNode();
        var snapshot = JsonSerializer.SerializeToNode(new WorkspaceSettingsSnapshotV2(Existing(), Values(), Policy()), Json)!.AsObject();
        foreach (var original in new[] { command, snapshot }) {
            var expectationName = original.ContainsKey("expected") ? "expected" : "expectation";
            foreach (var path in new[] { new[] { "values" }, new[] { expectationName }, new[] { expectationName, "head" }, new[] { "decisionDataPolicy" } }) {
                var target = original;
                foreach (var member in path) {
                    target = target[member]!.AsObject();
                }
                var objectText = target.ToJsonString();
                var whole = original.ToJsonString();
                foreach (var property in target) {
                    var propertyText = JsonSerializer.Serialize(property.Key) + ":" + (property.Value?.ToJsonString() ?? "null");
                    var duplicate = whole.Replace(objectText, objectText[..^1] + "," + propertyText + "}", StringComparison.Ordinal);
                    AssertInvalid(original, duplicate);
                    var missing = original.DeepClone().AsObject();
                    var missingTarget = missing;
                    foreach (var member in path) {
                        missingTarget = missingTarget[member]!.AsObject();
                    }
                    Assert.True(missingTarget.Remove(property.Key));
                    AssertInvalid(original, missing.ToJsonString());
                }
                var unknown = whole.Replace(objectText, objectText[..^1] + ",\"unexpected\":true}", StringComparison.Ordinal);
                AssertInvalid(original, unknown);
            }
        }
    }

    [Fact]
    public void Root_policy_is_complete_and_not_reinterpreted_by_serializer_options() {
        var configured = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        configured.Converters.Add(new JsonStringEnumConverter());
        var command = new ReplaceWorkspaceSettingsV2Command(Existing(), Values(), Policy());
        var wire = JsonSerializer.Serialize(command, Json);
        Assert.Equal(wire, JsonSerializer.Serialize(command, configured));
        Assert.Equal(command, JsonSerializer.Deserialize<ReplaceWorkspaceSettingsV2Command>(wire, configured));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ReplaceWorkspaceSettingsV2Command>(
            wire.Replace("\"workspaceName\"", "\"WorkspaceName\"", StringComparison.Ordinal), configured));
        foreach (var invalid in new[] {
            wire.Replace("\"readBasisMode\":1", "\"readBasisMode\":99", StringComparison.Ordinal),
            wire.Replace("\"maximumWorkDuration\":\"00:10:00\"", "\"maximumWorkDuration\":\"00:00:00\"", StringComparison.Ordinal),
            wire.Replace("\"maximumReplans\":0", "\"maximumReplans\":1", StringComparison.Ordinal),
            wire.Replace("\"decisionDataPolicy\":{", "\"decisionDataPolicy\":{" + new string(' ', DecisionDataPolicyCodec.MaximumUtf8Bytes), StringComparison.Ordinal)
        }) {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ReplaceWorkspaceSettingsV2Command>(invalid, configured));
        }
    }

    [Fact]
    public void V1_wire_remains_six_values_and_cannot_be_silently_upgraded_or_clear_policy() {
        var original = new ReplaceWorkspaceSettingsCommand(Existing(), Values());
        var before = JsonSerializer.Serialize(original, Json);
        var legacySnapshot = new WorkspaceSettingsSnapshot(Existing(), Values());
        var snapshotBefore = JsonSerializer.Serialize(legacySnapshot, Json);
        var v2 = new ReplaceWorkspaceSettingsV2Command(original.Expected, original.Values, Policy());
        var v2Wire = JsonSerializer.Serialize(v2, Json);

        Assert.Equal(before, JsonSerializer.Serialize(original, Json));
        Assert.Equal(snapshotBefore, JsonSerializer.Serialize(legacySnapshot, Json));
        Assert.Equal(6, JsonSerializer.SerializeToNode(original.Values, Json)!.AsObject().Count);
        Assert.Equal(new[] { "expected", "values" }, JsonNode.Parse(before)!.AsObject().Select(property => property.Key).Order(StringComparer.Ordinal));
        Assert.Equal(original, JsonSerializer.Deserialize<ReplaceWorkspaceSettingsCommand>(before, Json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ReplaceWorkspaceSettingsV2Command>(before, Json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ReplaceWorkspaceSettingsCommand>(v2Wire, Json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorkspaceSettingsSnapshotV2>(snapshotBefore, Json));
        Assert.Null(typeof(WorkspaceSettingsValues).GetProperty(nameof(ReplaceWorkspaceSettingsV2Command.DecisionDataPolicy)));
    }

    private static void AssertTopLevelContract<T>(T value) {
        var node = JsonSerializer.SerializeToNode(value, Json)!.AsObject();
        var wire = node.ToJsonString();
        foreach (var property in node) {
            var missing = node.DeepClone().AsObject();
            Assert.True(missing.Remove(property.Key));
            Assert.Throws<JsonException>(() => missing.Deserialize<T>(Json));
            var duplicate = wire[..^1] + "," + JsonSerializer.Serialize(property.Key) + ":" + (property.Value?.ToJsonString() ?? "null") + "}";
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<T>(duplicate, Json));
        }
        foreach (var invalid in new[] { "null", "[]", "{}", wire + "{}", wire[..^1] + ",\"unknown\":true}" }) {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<T>(invalid, Json));
        }
        foreach (var version in new[] { "1", "3", "null", "\"2\"", "true" }) {
            var invalid = wire.Replace("\"schemaVersion\":2", "\"schemaVersion\":" + version, StringComparison.Ordinal);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<T>(invalid, Json));
        }
    }

    private static void AssertInvalid(JsonObject original, string json) {
        if (original.ContainsKey("expected")) {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ReplaceWorkspaceSettingsV2Command>(json, Json));
        } else {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorkspaceSettingsSnapshotV2>(json, Json));
        }
    }

    private static JsonObject CommandNode() => JsonSerializer.SerializeToNode(new ReplaceWorkspaceSettingsV2Command(Existing(), Values(), Policy()), Json)!.AsObject();
    private static WorkspaceSettingsExpectation Empty() => new(Namespace, WorkspaceSettingsHeadState.Empty, null);
    private static WorkspaceSettingsExpectation Existing() => new(Namespace, WorkspaceSettingsHeadState.Existing, Head);
    private static WorkspaceSettingsValues Values() => new("  Original  ", null, " Markdown ", "eur", "de-DE", "  Keep notes  ");
    private static DecisionDataPolicy Policy() => new(ReadBasisMode.StepSnapshot, PublicationFreshness.RequireCurrent, OnBasisChange.PauseForReview,
        new(0, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15)));
}
