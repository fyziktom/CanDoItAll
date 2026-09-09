using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafToolInvocationArgumentFormatter {
    public static string ResolveToolName(ToolCallContent toolCall) {
        ArgumentNullException.ThrowIfNull(toolCall);

        return toolCall switch {
            FunctionCallContent functionCall when !string.IsNullOrWhiteSpace(functionCall.Name) => functionCall.Name,
            McpServerToolCallContent mcpToolCall when !string.IsNullOrWhiteSpace(mcpToolCall.Name) => mcpToolCall.Name,
            _ => "Unnamed tool"
        };
    }

    public static string ResolveToolCallKey(ToolCallContent toolCall) {
        ArgumentNullException.ThrowIfNull(toolCall);

        return toolCall.CallId
            ?? $"{ResolveToolName(toolCall)}|{DescribeToolCallArguments(toolCall)}";
    }

    public static string ResolveToolInvocationSignature(ToolCallContent toolCall) {
        ArgumentNullException.ThrowIfNull(toolCall);

        return $"{ResolveToolName(toolCall)}|{DescribeToolCallArguments(toolCall)}";
    }

    public static string DescribeToolInvocation(ToolCallContent toolCall) {
        ArgumentNullException.ThrowIfNull(toolCall);

        var toolName = ResolveToolName(toolCall);
        var arguments = DescribeToolCallArguments(toolCall);
        return string.IsNullOrWhiteSpace(arguments)
            ? $"Invoking tool '{toolName}'."
            : $"Invoking tool '{toolName}' with {arguments}.";
    }

    public static string DescribeToolCallArguments(ToolCallContent toolCall) {
        ArgumentNullException.ThrowIfNull(toolCall);

        var toolName = ResolveToolName(toolCall);
        return toolCall switch {
            FunctionCallContent functionCall => SummarizeArguments(toolName, functionCall.Arguments),
            McpServerToolCallContent mcpToolCall => SummarizeArguments(toolName, mcpToolCall.Arguments),
            _ => string.Empty
        };
    }

    public static string DescribeArguments(string? argumentsJson, string? toolName = null)
        => AgentToolArgumentDisplayFormatter.DescribeArguments(argumentsJson, toolName);

    public static string FormatInlineArgumentSummary(string argumentSummary)
        => AgentToolArgumentDisplayFormatter.FormatInlineArgumentSummary(argumentSummary);

    public static string SummarizeArguments(IDictionary<string, object?>? arguments)
        => AgentToolArgumentDisplayFormatter.SummarizeArguments(arguments);

    public static string SummarizeArguments(string? toolName, IDictionary<string, object?>? arguments)
        => AgentToolArgumentDisplayFormatter.SummarizeArguments(toolName, arguments);

    public static string FormatArgumentSummary(IEnumerable<KeyValuePair<string, object?>> arguments)
        => AgentToolArgumentDisplayFormatter.FormatArgumentSummary(arguments);

    public static string FormatArgumentSummary(string? toolName, IEnumerable<KeyValuePair<string, object?>> arguments)
        => AgentToolArgumentDisplayFormatter.FormatArgumentSummary(toolName, arguments);

    public static string FormatArgumentValue(object? value)
        => AgentToolArgumentDisplayFormatter.FormatArgumentValue(value);

    public static Dictionary<string, object?> DeserializeArguments(string? argumentsJson)
        => AgentToolArgumentDisplayFormatter.DeserializeArguments(argumentsJson);

    public static object? ConvertJsonValue(JsonElement value)
        => AgentToolArgumentDisplayFormatter.ConvertJsonValue(value);
}
