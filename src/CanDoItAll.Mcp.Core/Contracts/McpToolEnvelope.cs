using System.Text.Json.Serialization;

namespace CanDoItAll.Mcp.Core.Contracts;

public record McpToolEnvelope<T>(
    bool Ok,
    string Tool,
    DateTimeOffset TimestampUtc,
    string CorrelationId,
    T? Data,
    IReadOnlyList<string> Warnings,
    ToolError? Error)
{
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Target { get; init; }

    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OperationId { get; init; }

    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Status { get; init; }

    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Summary { get; init; }

    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Diagnostics { get; init; }

    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? NextSuggestedTools { get; init; }

    public static McpToolEnvelope<T> Success(
        string tool,
        string correlationId,
        T data,
        IReadOnlyList<string>? warnings = null,
        string? target = null,
        string? operationId = null,
        string? status = null,
        string? summary = null,
        IReadOnlyList<string>? diagnostics = null,
        IReadOnlyList<string>? nextSuggestedTools = null)
    {
        return new McpToolEnvelope<T>(
            true,
            tool,
            DateTimeOffset.UtcNow,
            correlationId,
            data,
            warnings ?? [],
            null)
        {
            Target = target,
            OperationId = operationId,
            Status = status,
            Summary = summary,
            Diagnostics = diagnostics,
            NextSuggestedTools = nextSuggestedTools
        };
    }

    public static McpToolEnvelope<T> Failure(
        string tool,
        string correlationId,
        ToolError error,
        string? target = null,
        string? operationId = null,
        string? status = null,
        string? summary = null,
        IReadOnlyList<string>? diagnostics = null,
        IReadOnlyList<string>? nextSuggestedTools = null)
    {
        return new McpToolEnvelope<T>(
            false,
            tool,
            DateTimeOffset.UtcNow,
            correlationId,
            default,
            [],
            error)
        {
            Target = target,
            OperationId = operationId,
            Status = status,
            Summary = summary,
            Diagnostics = diagnostics,
            NextSuggestedTools = nextSuggestedTools
        };
    }
}

public sealed record ToolEnvelope<T>(
    bool Ok,
    string Tool,
    DateTimeOffset TimestampUtc,
    string CorrelationId,
    T? Data,
    IReadOnlyList<string> Warnings,
    ToolError? Error)
    : McpToolEnvelope<T>(Ok, Tool, TimestampUtc, CorrelationId, Data, Warnings, Error)
{
    public static ToolEnvelope<T> Success(string tool, string correlationId, T data, IReadOnlyList<string>? warnings = null)
    {
        return new ToolEnvelope<T>(
            true,
            tool,
            DateTimeOffset.UtcNow,
            correlationId,
            data,
            warnings ?? [],
            null);
    }

    public static ToolEnvelope<T> Failure(string tool, string correlationId, ToolError error)
    {
        return new ToolEnvelope<T>(
            false,
            tool,
            DateTimeOffset.UtcNow,
            correlationId,
            default,
            [],
            error);
    }
}

public record ToolError(string Code, string Message, object? Details = null);

public class ToolInvocationException(string code, string message, object? details = null) : Exception(message)
{
    public string Code { get; } = code;

    public object? Details { get; } = details;
}
