using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.SharedProviders.Abstractions;

/// <summary>
/// Whether a shared model supports configurable reasoning (thinking), as a string token: <c>unknown</c>,
/// <c>unsupported</c> or <c>supported</c>.
/// </summary>
[JsonConverter(typeof(SharedProviderThinkingSupportConverter))]
public enum SharedProviderThinkingSupport { Unknown, Unsupported, Supported }

/// <summary>
/// How reasoning is controlled for a shared model, as a string token: <c>unspecified</c> (no control; used when
/// reasoning is not supported), <c>booleanToggle</c> (on or off: the allowed efforts are exactly <c>none</c> and
/// <c>medium</c>) or <c>effortLevels</c> (one of the allowed effort levels).
/// </summary>
[JsonConverter(typeof(SharedProviderThinkingControlConverter))]
public enum SharedProviderThinkingControl { Unspecified, BooleanToggle, EffortLevels }

/// <summary>
/// Reasoning effort level of a shared model, as a string token: <c>none</c>, <c>minimal</c>, <c>low</c>,
/// <c>medium</c>, <c>high</c>, <c>xhigh</c> or <c>max</c>.
/// </summary>
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

/// <summary>
/// Reasoning (thinking) support of a shared model and the effort levels a request may choose. When
/// <c>support</c> is not <c>supported</c>, <c>control</c> is <c>unspecified</c>, <c>allowedEfforts</c> is empty and
/// <c>defaultEffort</c> is null.
/// </summary>
/// <param name="Support">
/// Whether the model supports configurable reasoning, as a string token: <c>unknown</c>, <c>unsupported</c> or
/// <c>supported</c>.
/// </param>
/// <param name="Control">
/// How reasoning is controlled, as a string token: <c>unspecified</c>, <c>booleanToggle</c> (the allowed efforts are
/// exactly <c>none</c> and <c>medium</c>) or <c>effortLevels</c>.
/// </param>
/// <param name="AllowedEfforts">
/// Effort levels a request may send as <c>reasoning_effort</c> (chat completions) or <c>reasoning.effort</c>
/// (responses), as string tokens from <c>none</c>, <c>minimal</c>, <c>low</c>, <c>medium</c>, <c>high</c>,
/// <c>xhigh</c> and <c>max</c>; at most seven, without duplicates. Another level is rejected with
/// <c>shared_provider_thinking_effort_not_supported</c>.
/// </param>
/// <param name="DefaultEffort">
/// Effort level the relay applies when a request sets none, as a string token from the same list; null when there is
/// no default.
/// </param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SharedProviderThinkingCapability(
    [property: JsonPropertyName("support")] SharedProviderThinkingSupport Support,
    [property: JsonPropertyName("control")] SharedProviderThinkingControl Control,
    [property: JsonPropertyName("allowedEfforts")] IReadOnlyList<SharedProviderReasoningEffort> AllowedEfforts,
    [property: JsonPropertyName("defaultEffort")] SharedProviderReasoningEffort? DefaultEffort) {
    /// <summary>
    /// True when the relay removes <c>temperature</c> from requests to this model, because the model does not accept
    /// it. Omitted when false.
    /// </summary>
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
