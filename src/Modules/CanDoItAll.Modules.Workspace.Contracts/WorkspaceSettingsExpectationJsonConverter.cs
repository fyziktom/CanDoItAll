using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workspace;

internal sealed class WorkspaceSettingsExpectationJsonConverter : JsonConverter<WorkspaceSettingsExpectation> {
    public override bool HandleNull => true;

    public override WorkspaceSettingsExpectation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType != JsonTokenType.StartObject) {
            throw new JsonException("A settings expectation must be an explicit object.");
        }
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject()) {
            if (property.Name is not ("namespace" or "state" or "head") || !names.Add(property.Name)) {
                throw new JsonException("A settings expectation requires unique known members.");
            }
        }
        if (names.Count != 3) {
            throw new JsonException("A settings expectation requires namespace, state and head.");
        }
        var stateValue = root.GetProperty("state");
        if (stateValue.ValueKind != JsonValueKind.String) {
            throw new JsonException("A settings head state must be empty or existing.");
        }
        var state = stateValue.GetString() switch {
            "empty" => WorkspaceSettingsHeadState.Empty,
            "existing" => WorkspaceSettingsHeadState.Existing,
            _ => throw new JsonException("The settings head state is unsupported.")
        };
        var headValue = root.GetProperty("head");
        if (headValue.ValueKind == JsonValueKind.Object) {
            var headNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in headValue.EnumerateObject()) {
                if (property.Name is not ("id" or "revision") || !headNames.Add(property.Name)) {
                    throw new JsonException("A settings head requires unique row and revision members.");
                }
            }
            if (headNames.Count != 2) {
                throw new JsonException("A settings head requires both row and revision identifiers.");
            }
        }
        try {
            var @namespace = root.GetProperty("namespace").Deserialize<DatasetNamespaceId>(options)
                ?? throw new JsonException("A settings expectation requires its original dataset namespace.");
            var head = headValue.Deserialize<WorkspaceSettingsHead>(options);
            return new(@namespace, state, head);
        } catch (ArgumentException exception) {
            throw new JsonException("The settings expectation contains an invalid head or state.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, WorkspaceSettingsExpectation value, JsonSerializerOptions options) {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        writer.WritePropertyName("namespace");
        JsonSerializer.Serialize(writer, value.Namespace, options);
        writer.WriteString("state", value.State == WorkspaceSettingsHeadState.Empty ? "empty" : "existing");
        writer.WritePropertyName("head");
        JsonSerializer.Serialize(writer, value.Head, options);
        writer.WriteEndObject();
    }
}
