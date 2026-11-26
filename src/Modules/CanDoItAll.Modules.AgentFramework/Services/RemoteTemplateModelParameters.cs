using System.Globalization;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.RemoteInstances;

namespace CanDoItAll.Modules.AgentFramework;

public static class RemoteTemplateModelParameters {
    private static readonly HashSet<string> TokenLimits = new(StringComparer.OrdinalIgnoreCase) {
        AgentProviderModelParameterPolicy.MaxOutputTokensConfigurationPropertyName,
        AgentProviderModelParameterPolicy.OllamaNumPredictConfigurationPropertyName,
        AgentProviderModelParameterPolicy.OllamaNumPredictSnakeConfigurationPropertyName
    };

    public static void RequirePortable(string? json) {
        if (string.IsNullOrWhiteSpace(json)) {
            return;
        }
        try {
            using var document = JsonDocument.Parse(json);
            if (IsPortable(document.RootElement, allowEnvelope: true)) {
                return;
            }
        } catch (JsonException) {
        }
        throw new RemoteInstanceReadException("The model-parameter JSON must contain only numeric output token limits before this definition can be shared.");
    }

    private static bool IsPortable(JsonElement element, bool allowEnvelope) {
        if (element.ValueKind != JsonValueKind.Object) {
            return false;
        }
        foreach (var field in element.EnumerateObject()) {
            if (allowEnvelope && field.Name.Equals(AgentProviderModelParameterPolicy.ModelParametersConfigurationPropertyName, StringComparison.OrdinalIgnoreCase)) {
                if (!IsPortable(field.Value, allowEnvelope: false)) {
                    return false;
                }
            } else if (!TokenLimits.Contains(field.Name) ||
                !int.TryParse(field.Value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit) || limit <= 0) {
                return false;
            }
        }
        return true;
    }
}
