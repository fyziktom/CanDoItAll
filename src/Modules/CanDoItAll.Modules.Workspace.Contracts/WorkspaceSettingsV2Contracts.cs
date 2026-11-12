using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workspace;

[JsonConverter(typeof(WorkspaceSettingsSnapshotV2JsonConverter))]
public sealed record WorkspaceSettingsSnapshotV2 {
    public const int SchemaVersion = 2;

    public WorkspaceSettingsSnapshotV2(WorkspaceSettingsExpectation expectation, WorkspaceSettingsValues values,
        DecisionDataPolicy? decisionDataPolicy) {
        ArgumentNullException.ThrowIfNull(expectation);
        ArgumentNullException.ThrowIfNull(values);
        if (expectation.State == WorkspaceSettingsHeadState.Empty && decisionDataPolicy is not null) {
            throw new ArgumentException("An empty settings head cannot contain a configured root policy.", nameof(decisionDataPolicy));
        }
        Expectation = expectation;
        Values = values;
        DecisionDataPolicy = decisionDataPolicy;
    }

    public WorkspaceSettingsExpectation Expectation { get; }
    public WorkspaceSettingsValues Values { get; }
    public DecisionDataPolicy? DecisionDataPolicy { get; }
}

[JsonConverter(typeof(ReplaceWorkspaceSettingsV2CommandJsonConverter))]
public sealed record ReplaceWorkspaceSettingsV2Command {
    public const int SchemaVersion = WorkspaceSettingsSnapshotV2.SchemaVersion;

    public ReplaceWorkspaceSettingsV2Command(WorkspaceSettingsExpectation expected, WorkspaceSettingsValues values,
        DecisionDataPolicy decisionDataPolicy) {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(decisionDataPolicy);
        Expected = expected;
        Values = values;
        DecisionDataPolicy = decisionDataPolicy;
    }

    public WorkspaceSettingsExpectation Expected { get; }
    public WorkspaceSettingsValues Values { get; }
    public DecisionDataPolicy DecisionDataPolicy { get; }
}

internal sealed class WorkspaceSettingsSnapshotV2JsonConverter : JsonConverter<WorkspaceSettingsSnapshotV2> {
    public override bool HandleNull => true;

    public override WorkspaceSettingsSnapshotV2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        var (expectation, values, policy) = WorkspaceSettingsV2Json.Read(ref reader, replacement: false, options);
        try {
            return new(expectation, values, policy);
        } catch (ArgumentException exception) {
            throw new JsonException("The Workspace v2 snapshot has no persisted head for its configured policy.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, WorkspaceSettingsSnapshotV2 value, JsonSerializerOptions options) {
        ArgumentNullException.ThrowIfNull(value);
        WorkspaceSettingsV2Json.Write(writer, value.Expectation, value.Values, value.DecisionDataPolicy, replacement: false, options);
    }
}

internal sealed class ReplaceWorkspaceSettingsV2CommandJsonConverter : JsonConverter<ReplaceWorkspaceSettingsV2Command> {
    public override bool HandleNull => true;

    public override ReplaceWorkspaceSettingsV2Command Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        var (expected, values, policy) = WorkspaceSettingsV2Json.Read(ref reader, replacement: true, options);
        return new(expected, values, policy ?? throw new JsonException("A Workspace v2 replacement requires a complete root policy."));
    }

    public override void Write(Utf8JsonWriter writer, ReplaceWorkspaceSettingsV2Command value, JsonSerializerOptions options) {
        ArgumentNullException.ThrowIfNull(value);
        WorkspaceSettingsV2Json.Write(writer, value.Expected, value.Values, value.DecisionDataPolicy, replacement: true, options);
    }
}

internal static class WorkspaceSettingsV2Json {
    private const string Version = "schemaVersion";
    private const string Expectation = "expectation";
    private const string Expected = "expected";
    private const string Values = "values";
    private const string Policy = "decisionDataPolicy";

    internal static (WorkspaceSettingsExpectation Expected, WorkspaceSettingsValues Values, DecisionDataPolicy? Policy) Read(
        ref Utf8JsonReader reader, bool replacement, JsonSerializerOptions options) {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var expectationName = replacement ? Expected : Expectation;
        RequireFields(root, [Version, expectationName, Values, Policy]);
        var version = root.GetProperty(Version);
        if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var schema) || schema != WorkspaceSettingsSnapshotV2.SchemaVersion) {
            throw new JsonException("The Workspace settings contract requires schema version two.");
        }
        var values = root.GetProperty(Values);
        RequireFields(values, ["workspaceName", "defaultProviderProfileId", "defaultPromptOutputFormat", "currencyCode", "currencyCultureName", "notes"]);
        var policy = root.GetProperty(Policy);
        if (replacement && policy.ValueKind == JsonValueKind.Null) {
            throw new JsonException("A Workspace v2 replacement cannot omit or inherit its root policy.");
        }
        try {
            return (root.GetProperty(expectationName).Deserialize<WorkspaceSettingsExpectation>(options)
                ?? throw new JsonException("Workspace v2 requires the original dataset and settings-head expectation."),
                values.Deserialize<WorkspaceSettingsValues>(options)
                ?? throw new JsonException("Workspace v2 requires all six settings values."),
                policy.ValueKind == JsonValueKind.Null ? null : policy.Deserialize<DecisionDataPolicy>(options)
                ?? throw new JsonException("A configured Workspace root policy must be complete."));
        } catch (ArgumentException exception) {
            throw new JsonException("The Workspace v2 contract contains invalid original evidence or settings values.", exception);
        }
    }

    internal static void Write(Utf8JsonWriter writer, WorkspaceSettingsExpectation expected, WorkspaceSettingsValues values,
        DecisionDataPolicy? policy, bool replacement, JsonSerializerOptions options) {
        writer.WriteStartObject();
        writer.WriteNumber(Version, WorkspaceSettingsSnapshotV2.SchemaVersion);
        writer.WritePropertyName(replacement ? Expected : Expectation);
        JsonSerializer.Serialize(writer, expected, options);
        writer.WritePropertyName(Values);
        JsonSerializer.Serialize(writer, values, options);
        writer.WritePropertyName(Policy);
        if (policy is null) {
            writer.WriteNullValue();
        } else {
            JsonSerializer.Serialize(writer, policy, options);
        }
        writer.WriteEndObject();
    }

    private static void RequireFields(JsonElement value, ReadOnlySpan<string> required) {
        if (value.ValueKind != JsonValueKind.Object) {
            throw new JsonException("Workspace v2 settings and values must be explicit objects.");
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject()) {
            if (!required.Contains(property.Name) || !names.Add(property.Name)) {
                throw new JsonException("Workspace v2 requires unique, known members with their exact names.");
            }
        }
        if (names.Count != required.Length) {
            throw new JsonException("Workspace v2 requires every contract member, including explicit nullable read values.");
        }
    }
}
