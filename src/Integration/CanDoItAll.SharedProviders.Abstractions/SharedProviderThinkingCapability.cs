using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.SharedProviders.Abstractions;

[JsonConverter(typeof(SharedProviderThinkingSupportConverter))]
public enum SharedProviderThinkingSupport { Unknown, Unsupported, Supported }

[JsonConverter(typeof(SharedProviderThinkingControlConverter))]
public enum SharedProviderThinkingControl { Unspecified, BooleanToggle, EffortLevels }

[JsonConverter(typeof(SharedProviderReasoningEffortConverter))]
public enum SharedProviderReasoningEffort {
    None,
    Minimal,
    Low,
    Medium,
    High,
    [JsonStringEnumMemberName("xhigh")]
    ExtraHigh,
    Max
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SharedProviderThinkingCapability(
    [property: JsonPropertyName("support")] SharedProviderThinkingSupport Support,
    [property: JsonPropertyName("control")] SharedProviderThinkingControl Control,
    [property: JsonPropertyName("allowedEfforts")] IReadOnlyList<SharedProviderReasoningEffort> AllowedEfforts,
    [property: JsonPropertyName("defaultEffort")] SharedProviderReasoningEffort? DefaultEffort) {
    [JsonPropertyName("omitTemperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool OmitTemperature { get; init; }

    public void Validate() {
        if (!Enum.IsDefined(Support) || !Enum.IsDefined(Control) ||
            AllowedEfforts is null || AllowedEfforts.Count > 7 ||
            AllowedEfforts.Any(effort => !Enum.IsDefined(effort)) ||
            AllowedEfforts.Distinct().Count() != AllowedEfforts.Count ||
            DefaultEffort.HasValue && !Enum.IsDefined(DefaultEffort.Value)) {
            throw new JsonException("Invalid shared model thinking capability.");
        }
        if (Support == SharedProviderThinkingSupport.Supported) {
            if (AllowedEfforts.Count == 0 || Control == SharedProviderThinkingControl.Unspecified ||
                Control == SharedProviderThinkingControl.BooleanToggle &&
                !(AllowedEfforts.Count == 2 && AllowedEfforts.Contains(SharedProviderReasoningEffort.None) &&
                  AllowedEfforts.Contains(SharedProviderReasoningEffort.Medium))) {
                throw new JsonException("Incoherent supported shared model thinking capability.");
            }
        } else if (AllowedEfforts.Count != 0 || DefaultEffort.HasValue ||
                   Control != SharedProviderThinkingControl.Unspecified) {
            throw new JsonException("An unsupported or unknown model cannot declare thinking controls.");
        }
    }

    public SharedProviderThinkingCapability Snapshot() {
        Validate();
        return this with { AllowedEfforts = Array.AsReadOnly(AllowedEfforts.Order().ToArray()) };
    }

    public static bool TryParseEffort(string? value, out SharedProviderReasoningEffort effort) {
        var parsed = value switch {
            "none" => SharedProviderReasoningEffort.None,
            "minimal" => SharedProviderReasoningEffort.Minimal,
            "low" => SharedProviderReasoningEffort.Low,
            "medium" => SharedProviderReasoningEffort.Medium,
            "high" => SharedProviderReasoningEffort.High,
            "xhigh" => SharedProviderReasoningEffort.ExtraHigh,
            "max" => SharedProviderReasoningEffort.Max,
            _ => (SharedProviderReasoningEffort?)null
        };
        effort = parsed.GetValueOrDefault();
        return parsed.HasValue;
    }

    public static string FormatEffort(SharedProviderReasoningEffort effort) => effort switch {
        SharedProviderReasoningEffort.None => "none",
        SharedProviderReasoningEffort.Minimal => "minimal",
        SharedProviderReasoningEffort.Low => "low",
        SharedProviderReasoningEffort.Medium => "medium",
        SharedProviderReasoningEffort.High => "high",
        SharedProviderReasoningEffort.ExtraHigh => "xhigh",
        SharedProviderReasoningEffort.Max => "max",
        _ => throw new ArgumentOutOfRangeException(nameof(effort))
    };
}

public sealed class SharedProviderThinkingSupportConverter()
    : JsonStringEnumConverter<SharedProviderThinkingSupport>(JsonNamingPolicy.CamelCase, false);

public sealed class SharedProviderThinkingControlConverter()
    : JsonStringEnumConverter<SharedProviderThinkingControl>(JsonNamingPolicy.CamelCase, false);

public sealed class SharedProviderReasoningEffortConverter()
    : JsonStringEnumConverter<SharedProviderReasoningEffort>(JsonNamingPolicy.CamelCase, false);
