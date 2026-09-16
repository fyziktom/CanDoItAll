using System.Text.Json;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

public static class AgentToolArgumentDisplayFormatter {
    public const int MaximumDisplayLength = 4096;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string DescribeArguments(string? argumentsJson, string? toolName = null, AgentToolPolicyCatalog? catalog = null) {
        try {
            return string.IsNullOrWhiteSpace(argumentsJson)
                ? string.Empty
                : FormatArgumentSummary(toolName, DeserializeArguments(argumentsJson), catalog);
        } catch (ArgumentException) {
            return string.Empty;
        }
    }

    public static string FormatInlineArgumentSummary(string argumentSummary) {
        return string.IsNullOrWhiteSpace(argumentSummary)
            ? string.Empty
            : $" with {argumentSummary}";
    }

    public static string SummarizeArguments(IDictionary<string, object?>? arguments)
        => SummarizeArguments(string.Empty, arguments);

    public static string SummarizeArguments(
        string? toolName,
        IDictionary<string, object?>? arguments,
        AgentToolPolicyCatalog? catalog = null) {
        if (arguments is null || arguments.Count == 0) {
            return string.Empty;
        }

        return FormatArgumentSummary(toolName, arguments, catalog);
    }

    public static string FormatArgumentSummary(IEnumerable<KeyValuePair<string, object?>> arguments)
        => FormatArgumentSummary(string.Empty, arguments);

    public static string FormatArgumentSummary(
        string? toolName,
        IEnumerable<KeyValuePair<string, object?>> arguments,
        AgentToolPolicyCatalog? catalog = null) {
        ArgumentNullException.ThrowIfNull(arguments);

        var sanitizedArguments = AgentToolInvocationPolicyMetadata.SanitizeArgumentsForDisplay(
            toolName,
            arguments,
            catalog);
        var parts = sanitizedArguments
            .Where(item => item.Value is not null)
            .Select(item => $"{item.Key}={FormatArgumentValue(item.Value)}")
            .ToList();

        var summary = string.Join(", ", parts);
        if (summary.Length <= MaximumDisplayLength) {
            return summary;
        }
        var suffix = $"…#{StableContentHash.ComputeShortSha256Hex(summary)}";
        return summary[..(MaximumDisplayLength - suffix.Length)] + suffix;
    }

    public static string FormatArgumentValue(object? value) {
        if (value is null) {
            return "<null>";
        }

        var text = value switch {
            string stringValue => stringValue,
            JsonElement jsonValue => jsonValue.ToString(),
            _ => JsonSerializer.Serialize(value, SerializerOptions)
        };

        if (string.IsNullOrWhiteSpace(text)) {
            return "\"\"";
        }

        text = text.ReplaceLineEndings(" ").Trim();
        if (text.Length > 120) {
            text = text[..120] + $"...#{StableContentHash.ComputeShortSha256Hex(text)}";
        }

        return $"\"{text}\"";
    }

    public static Dictionary<string, object?> DeserializeArguments(string? argumentsJson) {
        if (string.IsNullOrWhiteSpace(argumentsJson)) {
            return [];
        }

        try {
            using var document = JsonDocument.Parse(argumentsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object) {
                return [];
            }

            return document.RootElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => ConvertJsonValue(property.Value));
        } catch (JsonException) {
            return [];
        }
    }

    public static object? ConvertJsonValue(JsonElement value) {
        return value.ValueKind switch {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when value.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.Array => value.EnumerateArray().Select(ConvertJsonValue).ToList(),
            JsonValueKind.Object => value.EnumerateObject().ToDictionary(property => property.Name, property => ConvertJsonValue(property.Value)),
            _ => value.ToString()
        };
    }
}
