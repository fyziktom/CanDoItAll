using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowFailureDisplayFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string ToUserMessage(WorkflowFailureDiagnosticEnvelope diagnostic)
        => WorkflowFailureDiagnosticMapper.ToUserMessage(diagnostic);

    public static string ToUserMessage(WorkflowEventRecord workflowEvent)
    {
        ArgumentNullException.ThrowIfNull(workflowEvent);

        if (TryResolveDiagnosticUserMessage(workflowEvent, out var diagnosticMessage))
        {
            return diagnosticMessage;
        }

        return workflowEvent.Kind is WorkflowEventKind.Error or WorkflowEventKind.ExecutorFailed
            ? ToUserMessage(workflowEvent.Message)
            : workflowEvent.Message;
    }

    public static bool TryResolveDiagnosticUserMessage(
        WorkflowEventRecord workflowEvent,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(workflowEvent);

        message = string.Empty;
        if (!TryResolveDiagnostic(workflowEvent, out var diagnostic))
        {
            return false;
        }

        message = ToUserMessage(diagnostic);
        return true;
    }

    public static bool TryResolveDiagnosticTechnicalDetail(
        WorkflowEventRecord workflowEvent,
        out string detail)
    {
        ArgumentNullException.ThrowIfNull(workflowEvent);

        detail = string.Empty;
        if (!TryResolveDiagnostic(workflowEvent, out var diagnostic) ||
            string.IsNullOrWhiteSpace(diagnostic.RedactedTechnicalDetail))
        {
            return false;
        }

        detail = diagnostic.RedactedTechnicalDetail;
        return true;
    }

    private static bool TryResolveDiagnostic(
        WorkflowEventRecord workflowEvent,
        [NotNullWhen(true)] out WorkflowFailureDiagnosticEnvelope? diagnostic)
    {
        diagnostic = null;
        if (string.IsNullOrWhiteSpace(workflowEvent.PayloadJson))
        {
            return false;
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<WorkflowEventPayloadEnvelope>(
                workflowEvent.PayloadJson,
                JsonOptions);
            if (string.IsNullOrWhiteSpace(envelope?.InlineJson))
            {
                return false;
            }

            diagnostic = JsonSerializer.Deserialize<WorkflowFailureDiagnosticEnvelope>(
                envelope.InlineJson,
                JsonOptions);
            return diagnostic is not null;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static string ToUserMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var executorId = ExtractBetween(message, "Workflow executor '", "'");
        var nodeId = ExtractBetween(message, "failed on node '", "'");
        var rootCause = ExtractRootCause(message);
        var explanation = ExplainRootCause(rootCause);

        if (!string.IsNullOrWhiteSpace(executorId) && !string.IsNullOrWhiteSpace(nodeId))
        {
            return string.IsNullOrWhiteSpace(explanation)
                ? $"Step '{nodeId}' failed while running executor '{executorId}'. {rootCause}"
                : $"Step '{nodeId}' failed while running executor '{executorId}'. {explanation}";
        }

        return string.IsNullOrWhiteSpace(explanation)
            ? rootCause
            : explanation;
    }

    private static string ExtractRootCause(string message)
    {
        var normalized = message.Replace("\r", string.Empty, StringComparison.Ordinal);
        var markers = new[]
        {
            "WorkflowExecutorSanitizedException:",
            "InvalidOperationException:",
            "ArgumentException:"
        };

        foreach (var marker in markers)
        {
            var index = normalized.LastIndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
            {
                continue;
            }

            var start = index + marker.Length;
            var end = normalized.IndexOf('\n', start);
            if (end < 0)
            {
                end = normalized.IndexOf(" ---", start, StringComparison.Ordinal);
            }

            var rootCause = end < 0
                ? normalized[start..]
                : normalized[start..end];
            return Clean(rootCause);
        }

        return Clean(normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? normalized);
    }

    private static string ExplainRootCause(string rootCause)
    {
        if (rootCause.Contains("Project-structure executor setting 'ProjectId' or 'ProjectIdJsonPath' is required", StringComparison.Ordinal))
        {
            return "The project-structure step needs a project id before it can store the workflow output. Run preview again and choose a project, or add a project id to the preview input.";
        }

        if (rootCause.Contains("Project-structure executor setting 'NodeId' is required", StringComparison.Ordinal) ||
            rootCause.Contains("Project-structure task creation requires 'NodeId'", StringComparison.Ordinal))
        {
            return "The project-structure step needs a parent node id before it can create project nodes. Run preview again and provide the target node.";
        }

        if (rootCause.Contains("path '", StringComparison.Ordinal) &&
            rootCause.Contains("was not found in the workflow payload", StringComparison.Ordinal))
        {
            return $"A configured JSON path did not exist in the workflow payload. {rootCause}";
        }

        return rootCause;
    }

    private static string ExtractBetween(
        string value,
        string startMarker,
        string endMarker)
    {
        var start = value.IndexOf(startMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        start += startMarker.Length;
        var end = value.IndexOf(endMarker, start, StringComparison.Ordinal);
        return end < 0
            ? string.Empty
            : value[start..end];
    }

    private static string Clean(string value)
        => WorkflowExecutorRedaction.RedactText(
            string.Join(" ", value.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim());
}
