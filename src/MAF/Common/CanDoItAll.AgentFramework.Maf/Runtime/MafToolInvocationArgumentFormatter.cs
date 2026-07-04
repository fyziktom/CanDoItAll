using System.Text.Json;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafToolInvocationArgumentFormatter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string ResolveToolName(ToolCallContent toolCall)
    {
        ArgumentNullException.ThrowIfNull(toolCall);

        return toolCall switch
        {
            FunctionCallContent functionCall when !string.IsNullOrWhiteSpace(functionCall.Name) => functionCall.Name,
            McpServerToolCallContent mcpToolCall when !string.IsNullOrWhiteSpace(mcpToolCall.Name) => mcpToolCall.Name,
            _ => "Unnamed tool"
        };
    }

    public static string ResolveToolCallKey(ToolCallContent toolCall)
    {
        ArgumentNullException.ThrowIfNull(toolCall);

        return toolCall.CallId
            ?? $"{ResolveToolName(toolCall)}|{DescribeToolCallArguments(toolCall)}";
    }

    public static string ResolveToolInvocationSignature(ToolCallContent toolCall)
    {
        ArgumentNullException.ThrowIfNull(toolCall);

        return $"{ResolveToolName(toolCall)}|{DescribeToolCallArguments(toolCall)}";
    }

    public static string DescribeToolInvocation(ToolCallContent toolCall)
    {
        ArgumentNullException.ThrowIfNull(toolCall);

        var toolName = ResolveToolName(toolCall);
        var arguments = DescribeToolCallArguments(toolCall);
        return string.IsNullOrWhiteSpace(arguments)
            ? $"Invoking tool '{toolName}'."
            : $"Invoking tool '{toolName}' with {arguments}.";
    }

    public static string DescribeToolCallArguments(ToolCallContent toolCall)
    {
        ArgumentNullException.ThrowIfNull(toolCall);

        return toolCall switch
        {
            FunctionCallContent functionCall => SummarizeArguments(functionCall.Arguments),
            McpServerToolCallContent mcpToolCall => SummarizeArguments(mcpToolCall.Arguments),
            _ => string.Empty
        };
    }

    public static string DescribeArguments(string? argumentsJson)
    {
        return string.IsNullOrWhiteSpace(argumentsJson)
            ? string.Empty
            : FormatArgumentSummary(DeserializeArguments(argumentsJson));
    }

    public static string FormatInlineArgumentSummary(string argumentSummary)
    {
        return string.IsNullOrWhiteSpace(argumentSummary)
            ? string.Empty
            : $" with {argumentSummary}";
    }

    public static string SummarizeArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return string.Empty;
        }

        return FormatArgumentSummary(arguments);
    }

    public static string FormatArgumentSummary(IEnumerable<KeyValuePair<string, object?>> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var parts = arguments
            .Where(item => item.Value is not null)
            .Select(item => $"{item.Key}={FormatArgumentValue(item.Value)}")
            .ToList();

        return parts.Count == 0
            ? string.Empty
            : string.Join(", ", parts);
    }

    public static string FormatArgumentValue(object? value)
    {
        if (value is null)
        {
            return "<null>";
        }

        var text = value switch
        {
            string stringValue => stringValue,
            JsonElement jsonValue => jsonValue.ToString(),
            _ => JsonSerializer.Serialize(value, SerializerOptions)
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            return "\"\"";
        }

        text = text.ReplaceLineEndings(" ").Trim();
        if (text.Length > 120)
        {
            text = text[..120] + $"...#{StableContentHash.ComputeShortSha256Hex(text)}";
        }

        return $"\"{text}\"";
    }

    public static Dictionary<string, object?> DeserializeArguments(string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            return document.RootElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => ConvertJsonValue(property.Value));
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static object? ConvertJsonValue(JsonElement value)
    {
        return value.ValueKind switch
        {
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
