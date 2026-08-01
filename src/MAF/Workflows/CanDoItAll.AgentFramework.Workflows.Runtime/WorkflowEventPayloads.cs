using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowEventPayloads
{
    public const int MaxInlinePayloadCharacters = 64_000;

    private const string TruncationMarker = "...[TRUNCATED]";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize(
        WorkflowEventPayloadSource source,
        string eventType,
        WorkflowNodeId? nodeId = null,
        WorkflowExecutorId? executorId = null,
        WorkflowExternalRequestId? requestId = null,
        WorkflowExternalRequestKind? requestKind = null,
        string inlineJson = "",
        string reference = "",
        int? originalInlineCharacters = null,
        bool? inlineTruncated = null,
        int? maxInlinePayloadCharacters = null,
        WorkflowUsageMetrics? usage = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        var inlineLimit = ResolveMaxInlinePayloadCharacters(maxInlinePayloadCharacters);
        var originalInlineJson = inlineJson ?? string.Empty;
        var redactedInlineJson = RedactInlinePayload(originalInlineJson);
        var boundedInlineJson = BoundInlinePayload(redactedInlineJson, inlineLimit);
        var redactedReference = WorkflowExecutorRedaction.RedactText(reference);
        var boundedReference = BoundInlinePayload(redactedReference, inlineLimit);
        var envelope = new WorkflowEventPayloadEnvelope(
            source,
            eventType.Trim(),
            nodeId,
            executorId,
            requestId,
            requestKind,
            boundedInlineJson,
            originalInlineCharacters ?? (string.IsNullOrEmpty(originalInlineJson) ? null : originalInlineJson.Length),
            inlineTruncated ?? originalInlineJson.Length > inlineLimit || redactedInlineJson.Length > inlineLimit,
            boundedReference)
        {
            Usage = usage
        };

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    private static string RedactInlinePayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return string.Empty;
        }

        return LooksLikeJson(payload)
            ? WorkflowExecutorRedaction.RedactJson(payload, int.MaxValue)
            : WorkflowExecutorRedaction.RedactText(payload);
    }

    private static string BoundInlinePayload(string payload, int maxInlinePayloadCharacters)
    {
        if (payload.Length <= maxInlinePayloadCharacters)
        {
            return payload;
        }

        if (maxInlinePayloadCharacters <= TruncationMarker.Length)
        {
            return payload[..maxInlinePayloadCharacters];
        }

        return string.Concat(
            payload.AsSpan(0, maxInlinePayloadCharacters - TruncationMarker.Length),
            TruncationMarker);
    }

    private static int ResolveMaxInlinePayloadCharacters(int? maxInlinePayloadCharacters)
    {
        var resolved = maxInlinePayloadCharacters.GetValueOrDefault(MaxInlinePayloadCharacters);
        if (resolved <= 0)
        {
            throw new InvalidOperationException("Workflow event inline payload limit must be positive.");
        }

        return resolved;
    }

    private static bool LooksLikeJson(string value)
    {
        var trimmed = value.AsSpan().TrimStart();
        return trimmed.Length > 0 && trimmed[0] is '{' or '[';
    }
}
