using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum WorkflowExecutorExecutionAuditStatus
{
    Started,
    Completed,
    Failed
}

public sealed record WorkflowExecutorExecutionAuditRecord(
    WorkflowId WorkflowId,
    WorkflowVersionId VersionId,
    WorkflowRunId? RunId,
    WorkflowNodeId NodeId,
    WorkflowExecutorId ExecutorId,
    WorkflowExecutorSourceKind SourceKind,
    string PluginId,
    string PackageId,
    string PluginConnectionId,
    WorkflowExecutorExecutionAuditStatus Status,
    int AttemptNumber,
    int MaxAttempts,
    int TimeoutSeconds,
    bool CaptureOutputArtifact,
    string RedactedSettingsSummary,
    string RedactedMessage,
    int? PayloadCharacters,
    DateTimeOffset OccurredAtUtc);

public interface IWorkflowExecutorExecutionObserver
{
    ValueTask RecordAsync(
        WorkflowExecutorExecutionAuditRecord auditRecord,
        CancellationToken cancellationToken = default);
}

public sealed class NullWorkflowExecutorExecutionObserver : IWorkflowExecutorExecutionObserver
{
    public ValueTask RecordAsync(
        WorkflowExecutorExecutionAuditRecord auditRecord,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}

public sealed class WorkflowExecutorExecutionAuditScope : IDisposable
{
    private static readonly AsyncLocal<WorkflowRunId?> CurrentRun = new();
    private readonly WorkflowRunId? previousRunId;

    private WorkflowExecutorExecutionAuditScope(WorkflowRunId runId)
    {
        previousRunId = CurrentRun.Value;
        CurrentRun.Value = runId;
    }

    public static WorkflowRunId? CurrentRunId => CurrentRun.Value;

    public static WorkflowExecutorExecutionAuditScope Push(WorkflowRunId runId)
        => new(runId);

    public void Dispose()
    {
        CurrentRun.Value = previousRunId;
    }
}

public static class WorkflowExecutorPayloadPolicy
{
    public const int MaxPluginOutputPayloadCharacters = 262_144;
    public const int MaxRedactedSummaryCharacters = 4096;

    public static void ThrowIfPluginPayloadTooLarge(
        WorkflowExecutorDescriptor descriptor,
        WorkflowNodeId nodeId,
        string payloadJson)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.Source.Kind == WorkflowExecutorSourceKind.BuiltIn ||
            payloadJson.Length <= MaxPluginOutputPayloadCharacters)
        {
            return;
        }

        throw new WorkflowExecutorPayloadTooLargeException(
            nodeId,
            descriptor.Id,
            payloadJson.Length,
            MaxPluginOutputPayloadCharacters,
            $"Plugin workflow executor '{descriptor.Id}' on node '{nodeId}' returned {payloadJson.Length} characters, exceeding the plugin output payload limit of {MaxPluginOutputPayloadCharacters} characters.");
    }
}

public sealed class WorkflowExecutorPayloadTooLargeException : InvalidOperationException
{
    public WorkflowExecutorPayloadTooLargeException(
        WorkflowNodeId nodeId,
        WorkflowExecutorId executorId,
        int payloadCharacters,
        int maxPayloadCharacters,
        string message)
        : base(message)
    {
        NodeId = nodeId;
        ExecutorId = executorId;
        PayloadCharacters = payloadCharacters;
        MaxPayloadCharacters = maxPayloadCharacters;
    }

    public WorkflowNodeId NodeId { get; }

    public WorkflowExecutorId ExecutorId { get; }

    public int PayloadCharacters { get; }

    public int MaxPayloadCharacters { get; }
}

public sealed class WorkflowExecutorSanitizedException : Exception
{
    public WorkflowExecutorSanitizedException(
        string originalExceptionType,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        OriginalExceptionType = originalExceptionType;
    }

    public string OriginalExceptionType { get; }
}

public static class WorkflowExecutorRedaction
{
    private static readonly Regex SecretKeyValueRegex = new(
        "(\"?(?:api[_-]?key|authorization|bearer|client[_-]?secret|password|secret|token)\"?\\s*[:=]\\s*)(\"[^\"]*\"|[^;\\s,}]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BearerTokenRegex = new(
        "Bearer\\s+[^\\s,;\"}]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex OpenAiStyleKeyRegex = new(
        "sk-[A-Za-z0-9_-]{8,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "apiKey",
        "api_key",
        "apikey",
        "authorization",
        "bearer",
        "clientSecret",
        "client_secret",
        "password",
        "secret",
        "secretId",
        "secretNameSnapshot",
        "token"
    };

    public static string RedactText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var redacted = SecretKeyValueRegex.Replace(value, match => $"{match.Groups[1].Value}[REDACTED]");
        redacted = BearerTokenRegex.Replace(redacted, "Bearer [REDACTED]");
        return OpenAiStyleKeyRegex.Replace(redacted, "[REDACTED]");
    }

    public static string RedactSettingsJson(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return "{}";
        }

        try
        {
            var node = JsonNode.Parse(settingsJson);
            RedactNode(node);
            var redactedJson = node?.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? "{}";
            return Truncate(redactedJson, WorkflowExecutorPayloadPolicy.MaxRedactedSummaryCharacters);
        }
        catch (JsonException)
        {
            return Truncate(RedactText(settingsJson), WorkflowExecutorPayloadPolicy.MaxRedactedSummaryCharacters);
        }
    }

    public static string ReadStringProperty(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(propertyName))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(propertyName, out var property) ||
                property.ValueKind != JsonValueKind.String)
            {
                return string.Empty;
            }

            return property.GetString()?.Trim() ?? string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    public static Exception? SanitizeException(Exception? exception)
    {
        if (exception is null)
        {
            return null;
        }

        if (exception is WorkflowExecutorPayloadTooLargeException)
        {
            return exception;
        }

        return new WorkflowExecutorSanitizedException(
            exception.GetType().FullName ?? exception.GetType().Name,
            RedactText(exception.Message),
            SanitizeException(exception.InnerException));
    }

    private static void RedactNode(JsonNode? node)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (var propertyName in jsonObject.Select(property => property.Key).ToArray())
            {
                if (IsSensitiveProperty(propertyName))
                {
                    jsonObject[propertyName] = "[REDACTED]";
                    continue;
                }

                RedactNode(jsonObject[propertyName]);
            }

            return;
        }

        if (node is JsonArray jsonArray)
        {
            foreach (var item in jsonArray)
            {
                RedactNode(item);
            }
        }
    }

    private static bool IsSensitiveProperty(string propertyName)
        => SensitivePropertyNames.Contains(propertyName) ||
           propertyName.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
           propertyName.Contains("token", StringComparison.OrdinalIgnoreCase) ||
           propertyName.Contains("password", StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string value, int maxCharacters)
        => value.Length <= maxCharacters
            ? value
            : string.Concat(value.AsSpan(0, maxCharacters), "...[TRUNCATED]");
}
