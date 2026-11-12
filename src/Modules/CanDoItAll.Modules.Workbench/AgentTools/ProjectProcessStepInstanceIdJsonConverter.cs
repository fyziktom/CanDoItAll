using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectProcessStepInstanceIdJsonConverter : JsonConverter<ProcessStepInstanceId> {
    public override ProcessStepInstanceId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        var identity = JsonSerializer.Deserialize<ProcessStepIdentity>(ref reader, options);
        if (identity is null || identity.Value == Guid.Empty) {
            throw new JsonException("The saved Process step instance identifier is missing or empty.");
        }
        return new(identity.Value);
    }

    public override void Write(Utf8JsonWriter writer, ProcessStepInstanceId value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, new ProcessStepIdentity(value.Value), options);

    private sealed record ProcessStepIdentity(Guid Value);
}
