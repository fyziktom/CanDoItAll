using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectStructureImportSourceIdentity(
    string SourceKey,
    [property: JsonConverter(typeof(JsonNumberEnumConverter<ProjectStructureImportSourceKind>))]
    ProjectStructureImportSourceKind SourceKind,
    string ContainerNodeId) {

    public static string? ReadOptionalKey(JsonElement node, ISet<string> importedKeys, bool required = false) {
        if (!node.TryGetProperty("sourceKey", out var property)) {
            if (required) {
                throw new ProjectStructureAgentException(400, "ImportSourceKeyRequired", "Every JSON outline node must provide sourceKey when requireSourceKeys is true.");
            }
            return null;
        }
        var key = property.ValueKind == JsonValueKind.String ? property.GetString() : null;
        if (key is null || key.Length is 0 or > 128 || !char.IsAsciiLetterOrDigit(key[0]) ||
            key.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_' or '.' or ':' or '/'))) {
            throw new ProjectStructureAgentException(400, "InvalidImportSourceKey",
                "sourceKey must start with an ASCII letter or digit and contain at most 128 ASCII letters, digits, '-', '_', '.', ':' or '/'.");
        }
        if (!importedKeys.Add(key)) {
            throw new ProjectStructureAgentException(400, "DuplicateImportSourceKey",
                "sourceKey values must be unique within the complete JSON outline, using case-sensitive comparison.");
        }
        return key;
    }

    public string ToMetadataJson() => ProjectObjectMetadataSerializer.Serialize(new() { ImportSource = this });
}
