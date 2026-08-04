using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class CompositeWorkflowExecutorExecutionObserver(
    IEnumerable<IWorkflowExecutorExecutionAuditSink> sinks) : IWorkflowExecutorExecutionObserver
{
    private readonly IReadOnlyList<IWorkflowExecutorExecutionAuditSink> orderedSinks = sinks
        .OrderBy(sink => sink.GetType().FullName, StringComparer.Ordinal)
        .ToArray();

    public async ValueTask RecordAsync(
        WorkflowExecutorExecutionAuditRecord auditRecord,
        CancellationToken cancellationToken = default)
    {
        foreach (var sink in orderedSinks)
        {
            await sink.RecordAsync(auditRecord, cancellationToken);
        }
    }
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

public static class WorkflowExecutorRedaction
{
    private static readonly JsonSerializerOptions RedactedJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex SensitiveLineValueRegex = new(
        "(\"?(?:auth|authorization|connection[\\s._-]*strings?|cookies?|headers?|private[\\s._-]*keys?|subscription[\\s._-]*keys?|[A-Za-z0-9._-]*signatures?)\"?\\s*[:=]\\s*)(\"[^\"]*\"|'[^']*'|[^\\r\\n]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SecretKeyValueRegex = new(
        "(\"?(?:access[\\s._-]*keys?|account[\\s._-]*keys?|api[\\s._-]*keys?|auth|authorization|aws[\\s._-]*access[\\s._-]*key[\\s._-]*ids?|bearer|client[\\s._-]*secrets?|credentials?|passwords?|pwd|secrets?|shared[\\s._-]*access[\\s._-]*signatures?|sig|subscription[\\s._-]*keys?|[A-Za-z0-9._-]*signatures?|tokens?)\"?\\s*[:=]\\s*)(\"[^\"]*\"|'[^']*'|[^;\\s,}]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BearerTokenRegex = new(
        "Bearer\\s+[^\\s,;\"}]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex OpenAiStyleKeyRegex = new(
        "sk-[A-Za-z0-9_-]{8,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PemPrivateKeyRegex = new(
        "-----BEGIN [^-\\r\\n]*PRIVATE KEY-----[\\s\\S]*?-----END [^-\\r\\n]*PRIVATE KEY-----",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] SensitivePropertyNameFragments =
    [
        "accesskey",
        "accountkey",
        "apikey",
        "subscriptionkey",
        "authorization",
        "awsaccesskeyid",
        "bearer",
        "clientsecret",
        "connectionstring",
        "cookie",
        "credential",
        "header",
        "password",
        "privatekey",
        "secret",
        "sharedaccesssignature",
        "signature",
        "token"
    ];

    private static readonly string[] SensitiveExactPropertyNames =
    [
        "pwd",
        "sig",
        "auth"
    ];

    public static string RedactText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var redacted = PemPrivateKeyRegex.Replace(value, "[REDACTED PRIVATE KEY]");
        redacted = BearerTokenRegex.Replace(redacted, "Bearer [REDACTED]");
        redacted = SensitiveLineValueRegex.Replace(
            redacted,
            match => $"{match.Groups[1].Value}[REDACTED]");
        redacted = SecretKeyValueRegex.Replace(
            redacted,
            match => $"{match.Groups[1].Value}[REDACTED]");
        return OpenAiStyleKeyRegex.Replace(redacted, "[REDACTED]");
    }

    public static string RedactSettingsJson(string? settingsJson)
        => RedactJson(settingsJson, WorkflowExecutorPayloadPolicy.MaxRedactedSummaryCharacters);

    public static string RedactJson(string? json, int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "{}";
        }

        try
        {
            var node = JsonNode.Parse(json);
            node = RedactNode(node);
            var redactedJson = node?.ToJsonString(RedactedJsonOptions) ?? "{}";
            return Truncate(redactedJson, maxCharacters);
        }
        catch (JsonException)
        {
            return Truncate(RedactText(json), maxCharacters);
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

    private static JsonNode? RedactNode(JsonNode? node)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (var propertyName in jsonObject.Select(property => property.Key).ToArray())
            {
                if (IsSensitivePropertyName(propertyName))
                {
                    jsonObject[propertyName] = "[REDACTED]";
                    continue;
                }

                var child = jsonObject[propertyName];
                var redactedChild = RedactNode(child);
                if (!ReferenceEquals(child, redactedChild))
                {
                    jsonObject[propertyName] = redactedChild;
                }
            }

            return jsonObject;
        }

        if (node is JsonArray jsonArray)
        {
            for (var index = 0; index < jsonArray.Count; index++)
            {
                var child = jsonArray[index];
                var redactedChild = RedactNode(child);
                if (!ReferenceEquals(child, redactedChild))
                {
                    jsonArray[index] = redactedChild;
                }
            }

            return jsonArray;
        }

        return node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var value)
            ? JsonValue.Create(RedactText(value))
            : node;
    }

    public static bool IsSensitivePropertyName(string propertyName)
    {
        var normalizedPropertyName = NormalizeSensitivePropertyName(propertyName);
        return SensitiveExactPropertyNames.Contains(
                   normalizedPropertyName,
                   StringComparer.OrdinalIgnoreCase) ||
               SensitivePropertyNameFragments.Any(fragment =>
            normalizedPropertyName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeSensitivePropertyName(string propertyName)
        => string.Concat(propertyName.Where(char.IsLetterOrDigit));

    private static string Truncate(string value, int maxCharacters)
        => value.Length <= maxCharacters
            ? value
            : string.Concat(value.AsSpan(0, maxCharacters), "...[TRUNCATED]");
}
