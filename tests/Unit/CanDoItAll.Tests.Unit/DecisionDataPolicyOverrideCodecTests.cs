using System.Text;
using System.Text.Json;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class DecisionDataPolicyOverrideCodecTests {
    private const string Inherited = """
        {"schemaVersion":1,"readBasisMode":null,"publicationFreshness":null,"onBasisChange":null,"maximumReplans":null,"maximumWorkDuration":null,"maximumBasisAge":null}
        """;

    [Fact]
    public void Inheritance_is_explicit_and_resolution_uses_the_supplied_complete_parent() {
        var value = DecisionDataPolicyOverrideCodec.Deserialize(Inherited);
        Assert.Equal(new DecisionDataPolicyOverride(), value);
        Assert.Equal(Inherited, DecisionDataPolicyOverrideCodec.Serialize(value));
        var first = new DecisionDataPolicy(ReadBasisMode.RunSnapshot, PublicationFreshness.RequireCurrent, OnBasisChange.Fail,
            new(0, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(3)));
        var second = new DecisionDataPolicy(ReadBasisMode.StepSnapshot, PublicationFreshness.RequireCurrent, OnBasisChange.PauseForReview,
            new(0, TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(5)));
        var owner = new DecisionDataPolicyCapabilities(true, true, true, true, true, true,
            PublicationFreshness.RequireCurrent, new(5, TimeSpan.FromHours(1), TimeSpan.FromHours(1)));

        Assert.Equal(first, DecisionDataPolicyResolver.Resolve(first, owner, definition: value));
        Assert.Equal(second, DecisionDataPolicyResolver.Resolve(second, owner, definition: value));
        Assert.Throws<ArgumentNullException>(() => DecisionDataPolicyResolver.Resolve(null!, owner, definition: value));

        var replanOnly = DecisionDataPolicyOverrideCodec.Deserialize(Inherited.Replace("\"onBasisChange\":null", "\"onBasisChange\":2", StringComparison.Ordinal));
        Assert.Equal(OnBasisChange.Replan, replanOnly.OnBasisChange);
        Assert.Null(replanOnly.MaximumReplans);
        Assert.Equal(DecisionDataPolicyError.IncompatibleReplanBudget, Assert.Throws<DecisionDataPolicyException>(() =>
            DecisionDataPolicyResolver.Resolve(first, owner, definition: replanOnly)).Error);
        var acceptedParent = new DecisionDataPolicy(ReadBasisMode.RunSnapshot, PublicationFreshness.RequireCurrent, OnBasisChange.Replan,
            new(2, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(3)));
        Assert.Equal(acceptedParent, DecisionDataPolicyResolver.Resolve(acceptedParent, owner, definition: replanOnly));
    }

    [Fact]
    public void Version_one_roundtrips_partial_values_and_exact_duration_limits() {
        var values = new[] {
            new DecisionDataPolicyOverride(ReadBasisMode: ReadBasisMode.RunSnapshot),
            new DecisionDataPolicyOverride(PublicationFreshness: PublicationFreshness.PinnedObservation),
            new DecisionDataPolicyOverride(OnBasisChange: OnBasisChange.Fail, MaximumReplans: 0),
            new DecisionDataPolicyOverride(OnBasisChange: OnBasisChange.PauseForReview),
            new DecisionDataPolicyOverride(ReadBasisMode.StepSnapshot, PublicationFreshness.RequireCurrent, OnBasisChange.Replan,
                int.MaxValue, TimeSpan.FromTicks(1), TimeSpan.MaxValue)
        };
        foreach (var value in values) {
            var encoded = DecisionDataPolicyOverrideCodec.Serialize(value);
            Assert.Equal(value, DecisionDataPolicyOverrideCodec.Deserialize(encoded));
            Assert.True(Encoding.UTF8.GetByteCount(encoded) <= DecisionDataPolicyOverrideCodec.MaximumUtf8Bytes);
        }
        Assert.Equal("""
            {"schemaVersion":1,"readBasisMode":1,"publicationFreshness":1,"onBasisChange":2,"maximumReplans":2147483647,"maximumWorkDuration":"00:00:00.0000001","maximumBasisAge":"10675199.02:48:05.4775807"}
            """, DecisionDataPolicyOverrideCodec.Serialize(values[^1]));
    }

    [Fact]
    public void Every_field_is_required_and_unknown_duplicate_or_future_fields_are_rejected() {
        using var document = JsonDocument.Parse(Inherited);
        var fields = document.RootElement.EnumerateObject().Select(property => $"\"{property.Name}\":{property.Value.GetRawText()}").ToArray();
        for (var index = 0; index < fields.Length; index++) {
            var missing = "{" + string.Join(",", fields.Where((_, position) => position != index)) + "}";
            var duplicate = Inherited[..^1] + "," + fields[index] + "}";
            Assert.Throws<JsonException>(() => DecisionDataPolicyOverrideCodec.Deserialize(missing));
            Assert.Throws<JsonException>(() => DecisionDataPolicyOverrideCodec.Deserialize(duplicate));
        }
        foreach (var invalid in new[] {
            Inherited.Replace("\"schemaVersion\":1", "\"schemaVersion\":2", StringComparison.Ordinal),
            Inherited.Replace("\"schemaVersion\":1", "\"schemaVersion\":null", StringComparison.Ordinal),
            Inherited.Replace("\"schemaVersion\":1", "\"SchemaVersion\":1", StringComparison.Ordinal),
            Inherited[..^1] + ",\"parentPolicy\":null}",
            Inherited[..^1] + ",\"schema\\u0056ersion\":1}"
        }) {
            Assert.Throws<JsonException>(() => DecisionDataPolicyOverrideCodec.Deserialize(invalid));
        }
        var reversed = "{" + string.Join(",", fields.Reverse()) + "}";
        Assert.Equal(Inherited, DecisionDataPolicyOverrideCodec.Serialize(DecisionDataPolicyOverrideCodec.Deserialize(reversed)));
    }

    [Fact]
    public void Invalid_supplied_scalars_are_rejected_on_read_and_write() {
        (string Property, string JsonValue, DecisionDataPolicyOverride Value)[] invalid = [
            ("readBasisMode", "99", new(ReadBasisMode: (ReadBasisMode)99)),
            ("publicationFreshness", "-1", new(PublicationFreshness: (PublicationFreshness)(-1))),
            ("onBasisChange", "99", new(OnBasisChange: (OnBasisChange)99)),
            ("maximumReplans", "-1", new(MaximumReplans: -1)),
            ("maximumWorkDuration", "\"00:00:00\"", new(MaximumWorkDuration: TimeSpan.Zero)),
            ("maximumBasisAge", "\"-00:00:00.0000001\"", new(MaximumBasisAge: TimeSpan.FromTicks(-1)))
        ];
        foreach (var item in invalid) {
            var json = Inherited.Replace($"\"{item.Property}\":null", $"\"{item.Property}\":{item.JsonValue}", StringComparison.Ordinal);
            Assert.Throws<JsonException>(() => DecisionDataPolicyOverrideCodec.Deserialize(json));
            Assert.Throws<JsonException>(() => DecisionDataPolicyOverrideCodec.Serialize(item.Value));
        }
        foreach (var replacement in new[] { "\"1\"", "true", "{}", "[]", "1.5", "2147483648" }) {
            var json = Inherited.Replace("\"maximumReplans\":null", "\"maximumReplans\":" + replacement, StringComparison.Ordinal);
            Assert.Throws<JsonException>(() => DecisionDataPolicyOverrideCodec.Deserialize(json));
        }
        foreach (var replacement in new[] { "1", "{}", "\"PT1S\"", "\"00:00:01,1\"", "\"10675199.02:48:05.4775808\"" }) {
            var json = Inherited.Replace("\"maximumWorkDuration\":null", "\"maximumWorkDuration\":" + replacement, StringComparison.Ordinal);
            Assert.Throws<JsonException>(() => DecisionDataPolicyOverrideCodec.Deserialize(json));
        }
    }

    [Fact]
    public void Object_shape_tokens_and_utf8_limit_are_enforced() {
        foreach (var invalid in new[] { "null", "[]", "{}", "\"policy\"", Inherited + "{}", Inherited[..^1], Inherited[..^1] + ",}", "/*policy*/" + Inherited }) {
            Assert.Throws<JsonException>(() => DecisionDataPolicyOverrideCodec.Deserialize(invalid));
        }
        var atLimit = new string(' ', DecisionDataPolicyOverrideCodec.MaximumUtf8Bytes - Encoding.UTF8.GetByteCount(Inherited)) + Inherited;
        Assert.Equal(DecisionDataPolicyOverrideCodec.MaximumUtf8Bytes, Encoding.UTF8.GetByteCount(atLimit));
        Assert.Equal(new DecisionDataPolicyOverride(), DecisionDataPolicyOverrideCodec.Deserialize(atLimit));
        Assert.Throws<JsonException>(() => DecisionDataPolicyOverrideCodec.Deserialize(atLimit + " "));
        Assert.Throws<ArgumentNullException>(() => DecisionDataPolicyOverrideCodec.Serialize(null!));
        Assert.Throws<ArgumentNullException>(() => DecisionDataPolicyOverrideCodec.Deserialize(null!));
    }

    [Fact]
    public void Existing_complete_policy_wire_and_ordinary_override_serialization_are_unchanged() {
        const string complete = """
            {"schemaVersion":1,"readBasisMode":1,"publicationFreshness":1,"onBasisChange":1,"maximumReplans":0,"maximumWorkDuration":"00:10:00","maximumBasisAge":"00:15:00"}
            """;
        var original = DecisionDataPolicyCodec.Deserialize(complete);
        var preference = new DecisionDataPolicyOverride(ReadBasisMode: ReadBasisMode.RunSnapshot);
        var ordinaryBefore = JsonSerializer.Serialize(preference);

        Assert.Equal(preference, DecisionDataPolicyOverrideCodec.Deserialize(DecisionDataPolicyOverrideCodec.Serialize(preference)));

        Assert.Equal(complete, DecisionDataPolicyCodec.Serialize(original));
        Assert.Equal(ordinaryBefore, JsonSerializer.Serialize(preference));
        Assert.Throws<JsonException>(() => DecisionDataPolicyCodec.Deserialize(Inherited));
    }
}
