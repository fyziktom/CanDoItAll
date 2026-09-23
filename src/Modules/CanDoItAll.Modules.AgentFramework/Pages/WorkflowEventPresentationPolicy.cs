using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.UI;

namespace CanDoItAll.Modules.AgentFramework.Pages;

internal enum WorkflowEventContentKind { Internal, Output, ExternalRequest, ExternalResponse, Diagnostic }

internal sealed record WorkflowEventPresentation(
    WorkflowEventContentKind Kind, string Summary, string Payload, string TechnicalDetail);

internal static class WorkflowEventPresentationPolicy {
    public const int MaximumPayloadLength = WorkflowRequestView.MaximumJsonLength;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static WorkflowEventPresentation Map(WorkflowEventRecord record) {
        var failure = record.Kind is WorkflowEventKind.Error or WorkflowEventKind.ExecutorFailed;
        var summary = failure ? "The workflow step failed. Review its configuration and retry." :
            record.Kind == WorkflowEventKind.Unknown ? "Runtime event recorded." : PublicText(record.Message, 1024);
        var kind = WorkflowEventContentKind.Internal;
        var payload = string.Empty;
        var technicalDetail = string.Empty;
        var envelope = ReadEnvelope(record.PayloadJson, out var isEnvelope);
        if ((failure || record.Kind == WorkflowEventKind.Cancelled) && envelope is not null && Enum.IsDefined(envelope.Source) &&
            WorkflowFailureDisplayFormatter.TryResolveDiagnosticUserMessage(record, out var diagnosticMessage)) {
            kind = WorkflowEventContentKind.Diagnostic;
            summary = PublicText(diagnosticMessage, 2048);
            if (WorkflowFailureDisplayFormatter.TryResolveDiagnosticTechnicalDetail(record, out var redacted)) {
                technicalDetail = PublicText(redacted, MaximumPayloadLength);
            }
        } else if (!failure) {
            kind = envelope switch {
                { Source: WorkflowEventPayloadSource.MafNative, EventType: "WorkflowOutputEvent" }
                    when record.Kind == WorkflowEventKind.Output => WorkflowEventContentKind.Output,
                { Source: WorkflowEventPayloadSource.CanDoItAllProgress, EventType: "WorkflowNodeCompleted" }
                    when record.Kind == WorkflowEventKind.ExecutorCompleted => WorkflowEventContentKind.Output,
                { Source: WorkflowEventPayloadSource.ExternalRequest, EventType: "WorkflowExternalRequest" }
                    when record.Kind == WorkflowEventKind.WaitingForInput => WorkflowEventContentKind.ExternalRequest,
                { Source: WorkflowEventPayloadSource.ExternalRequest, EventType: "WorkflowExternalResponse" }
                    => WorkflowEventContentKind.ExternalResponse,
                null when !isEnvelope && record.Kind == WorkflowEventKind.Output => WorkflowEventContentKind.Output,
                _ => WorkflowEventContentKind.Internal
            };
            if (kind != WorkflowEventContentKind.Internal) {
                payload = Bound(envelope?.InlineJson ?? record.PayloadJson, MaximumPayloadLength);
            }
        }

        return new(kind, summary, payload, technicalDetail);
    }

    public static string RunSummary(WorkflowRunState state, string summary)
        => state == WorkflowRunState.Failed
            ? "Workflow failed. Review the event diagnostics for repair guidance."
            : PublicText(summary, 2048);

    public static string PublicText(string? value, int maximumLength)
        => Bound(WorkflowExecutorRedaction.RedactText(value), maximumLength);

    public static string Bound(string? value, int maximumLength = MaximumPayloadLength) {
        var text = value?.Trim() ?? string.Empty;
        return text.Length <= maximumLength ? text : text[..(maximumLength - 1)] + "…";
    }

    private static WorkflowEventPayloadEnvelope? ReadEnvelope(string json, out bool isEnvelope) {
        isEnvelope = false;
        try {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) {
                return null;
            }
            isEnvelope = document.RootElement.EnumerateObject().Any(property =>
                property.Name.Equals(nameof(WorkflowEventPayloadEnvelope.EventType), StringComparison.OrdinalIgnoreCase) ||
                property.Name.Equals(nameof(WorkflowEventPayloadEnvelope.InlineJson), StringComparison.OrdinalIgnoreCase) ||
                property.Name.Equals(nameof(WorkflowEventPayloadEnvelope.Source), StringComparison.OrdinalIgnoreCase));
            return isEnvelope ? document.RootElement.Deserialize<WorkflowEventPayloadEnvelope>(JsonOptions) : null;
        } catch (Exception exception) when (exception is JsonException or ArgumentException) {
            isEnvelope = true;
            return null;
        }
    }
}
