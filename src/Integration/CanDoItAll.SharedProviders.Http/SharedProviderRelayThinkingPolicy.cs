using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.SharedProviders.Http;

public sealed record SharedProviderThinkingRequestResult(
    ReadOnlyMemory<byte> Payload,
    SharedProviderReasoningEffort? Effort,
    bool IsOverride,
    SharedProviderFailure? Failure);

public static class SharedProviderRelayThinkingPolicy {
    public const string ChatEffortProperty = "reasoning_effort";
    public const string ResponsesReasoningProperty = "reasoning";
    public const string ResponsesEffortProperty = "effort";
    public const string TemperatureProperty = "temperature";

    public static SharedProviderThinkingRequestResult Apply(
        SharedProviderRelayOperation operation,
        ReadOnlyMemory<byte> upstreamPayload,
        SharedProviderThinkingCapability? capability) {
        capability?.Validate();
        if (operation == SharedProviderRelayOperation.ImageGenerations) {
            return new(upstreamPayload, null, false, null);
        }
        var payload = JsonNode.Parse(upstreamPayload.Span)!.AsObject();
        if (capability?.OmitTemperature == true) {
            payload.Remove(TemperatureProperty);
        }
        var configured = operation == SharedProviderRelayOperation.Responses
            ? payload[ResponsesReasoningProperty]?[ResponsesEffortProperty]
            : payload[ChatEffortProperty];
        SharedProviderReasoningEffort? effort = capability?.DefaultEffort;
        var isOverride = configured is not null;
        if (isOverride) {
            if (configured is not JsonValue value || !value.TryGetValue<string>(out var token) ||
                !SharedProviderThinkingCapability.TryParseEffort(token, out var parsed)) {
                return Reject(true);
            }
            effort = parsed;
        }
        if (effort.HasValue && (capability?.Support != SharedProviderThinkingSupport.Supported ||
            !capability.AllowedEfforts.Contains(effort.Value))) {
            return Reject(isOverride);
        }
        if (effort.HasValue) {
            var token = SharedProviderThinkingCapability.FormatEffort(effort.Value);
            if (operation == SharedProviderRelayOperation.Responses) {
                payload[ResponsesReasoningProperty] = new JsonObject { [ResponsesEffortProperty] = token };
            } else {
                payload[ChatEffortProperty] = token;
            }
        }
        return new(JsonSerializer.SerializeToUtf8Bytes(payload), effort, isOverride, null);
    }

    private static SharedProviderThinkingRequestResult Reject(bool isOverride) => new(
        ReadOnlyMemory<byte>.Empty, null, isOverride,
        new SharedProviderFailure(
            isOverride ? SharedProviderFailureCategory.Validation : SharedProviderFailureCategory.Unavailable,
            new SharedProviderFailureCode(isOverride
                ? "shared_provider_thinking_effort_not_supported"
                : "shared_provider_thinking_default_invalid"),
            isOverride
                ? "The selected source model does not support this thinking effort. Synchronize its capabilities."
                : "The source provider's thinking default is invalid for this model. Select a supported override or repair the source default.",
            ChatEffortProperty));
}
