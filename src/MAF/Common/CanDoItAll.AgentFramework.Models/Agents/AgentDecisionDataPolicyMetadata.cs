using System.Text;
using System.Text.Json;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Models;

public static class AgentDecisionDataPolicyMetadata {
    public const string PropertyName = "decisionDataPolicyOverride";

    public static DecisionDataPolicyOverride? Read(string configurationJson) {
        using var document = Parse(configurationJson);
        var values = ReservedValues(document.RootElement);
        if (values.Length == 0) {
            return null;
        }
        if (values.Length != 1) {
            throw new AgentDecisionDataPolicyMetadataException();
        }
        try {
            return DecisionDataPolicyOverrideCodec.Deserialize(values[0]);
        } catch (Exception failure) when (failure is JsonException or ArgumentException or NotSupportedException) {
            throw new AgentDecisionDataPolicyMetadataException(failure);
        }
    }

    public static string Replace(string configurationJson, DecisionDataPolicyOverride policy) {
        ArgumentNullException.ThrowIfNull(policy);
        var encoded = DecisionDataPolicyOverrideCodec.Serialize(policy);
        using var document = Parse(configurationJson);
        return Write(document.RootElement, [encoded]);
    }

    public static string ProjectOrdinaryConfiguration(string configurationJson) {
        using var document = Parse(configurationJson);
        return document.RootElement.EnumerateObject().Any(property => property.NameEquals(PropertyName))
            ? Write(document.RootElement, [])
            : configurationJson;
    }

    public static AgentDefinition Preserve(AgentDefinition? original, AgentDefinition candidate) {
        ArgumentNullException.ThrowIfNull(candidate);
        if (original is not null && original.Id != candidate.Id) {
            throw new ArgumentException("Policy preservation requires the original Agent identity.", nameof(original));
        }
        return candidate with {
            ConfigurationJson = Preserve(original?.ConfigurationJson, candidate.ConfigurationJson)
        };
    }

    public static string Preserve(string? originalConfigurationJson, string candidateConfigurationJson) {
        using var candidate = Parse(candidateConfigurationJson);
        var supplied = ReservedValues(candidate.RootElement);
        if (originalConfigurationJson is null) {
            if (supplied.Length != 0) {
                throw new AgentDecisionDataPolicyMetadataException();
            }
            return candidateConfigurationJson;
        }
        using var original = Parse(originalConfigurationJson);
        var retained = ReservedValues(original.RootElement);
        return retained.SequenceEqual(supplied, StringComparer.Ordinal)
            ? candidateConfigurationJson
            : Write(candidate.RootElement, retained);
    }

    public static IReadOnlyList<AgentDefinition> Preserve(
        IReadOnlyList<AgentDefinition> originals, IReadOnlyList<AgentDefinition> candidates) {
        ArgumentNullException.ThrowIfNull(originals);
        ArgumentNullException.ThrowIfNull(candidates);
        var byId = new Dictionary<Guid, AgentDefinition>();
        foreach (var original in originals) {
            if (!byId.TryAdd(original.Id, original)) {
                throw new AgentDecisionDataPolicyMetadataException();
            }
        }
        var selected = new HashSet<Guid>();
        return candidates.Select(candidate => {
            if (!selected.Add(candidate.Id)) {
                throw new AgentDecisionDataPolicyMetadataException();
            }
            return Preserve(byId.GetValueOrDefault(candidate.Id), candidate);
        }).ToArray();
    }

    private static JsonDocument Parse(string configurationJson) {
        ArgumentNullException.ThrowIfNull(configurationJson);
        JsonDocument document;
        try {
            document = JsonDocument.Parse(string.IsNullOrWhiteSpace(configurationJson) ? "{}" : configurationJson);
        } catch (JsonException failure) {
            throw new AgentDecisionDataPolicyMetadataException(failure);
        }
        if (document.RootElement.ValueKind == JsonValueKind.Object) {
            return document;
        }
        document.Dispose();
        throw new AgentDecisionDataPolicyMetadataException();
    }

    private static string[] ReservedValues(JsonElement root) => root.EnumerateObject()
        .Where(property => property.NameEquals(PropertyName))
        .Select(property => property.Value.GetRawText()).ToArray();

    private static string Write(JsonElement root, IReadOnlyList<string> reservedValues) {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) {
            writer.WriteStartObject();
            foreach (var property in root.EnumerateObject()) {
                if (!property.NameEquals(PropertyName)) {
                    property.WriteTo(writer);
                }
            }
            foreach (var value in reservedValues) {
                writer.WritePropertyName(PropertyName);
                writer.WriteRawValue(value);
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}

public sealed class AgentDecisionDataPolicyMetadataException(Exception? innerException = null)
    : InvalidOperationException("Agent policy metadata is invalid, ambiguous, or cannot be assigned by this writer.", innerException);
