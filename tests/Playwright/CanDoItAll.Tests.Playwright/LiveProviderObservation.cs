using System.Text.Json;

namespace CanDoItAll.Tests.Playwright;

public enum LiveProviderTerminal { Missing, Completed, Failed, Incomplete, Cancelled, TransportFailure, HttpRejected }
internal enum LiveProviderReason { None, OutputTokenLimit, ContentFilter, Other }
internal sealed record LiveProviderObservation(int Attempt, int? HttpStatus, LiveProviderTerminal Terminal,
    LiveProviderReason Reason, string? RequestCorrelationHash, int? InputTokens, int? OutputTokens);

internal static class LiveProviderStatusReader {
    internal static LiveProviderObservation Read(string json, LiveProviderObservation previous) {
        try {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.TryGetProperty("response", out var response)) {
                root = response;
            }
            if (!root.TryGetProperty("status", out var status)) {
                return previous;
            }
            var terminal = status.GetString() switch {
                "completed" => LiveProviderTerminal.Completed,
                "failed" => LiveProviderTerminal.Failed,
                "incomplete" => LiveProviderTerminal.Incomplete,
                "cancelled" => LiveProviderTerminal.Cancelled,
                _ => LiveProviderTerminal.Missing
            };
            var reason = LiveProviderReason.None;
            if (root.TryGetProperty("incomplete_details", out var details) && details.ValueKind == JsonValueKind.Object &&
                details.TryGetProperty("reason", out var reasonValue)) {
                reason = reasonValue.GetString() switch {
                    "max_output_tokens" => LiveProviderReason.OutputTokenLimit,
                    "content_filter" => LiveProviderReason.ContentFilter,
                    _ => LiveProviderReason.Other
                };
            }
            int? input = null;
            int? output = null;
            if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object) {
                if (usage.TryGetProperty("input_tokens", out var inputValue) && inputValue.TryGetInt32(out var inputCount) && inputCount >= 0) {
                    input = inputCount;
                }
                if (usage.TryGetProperty("output_tokens", out var outputValue) && outputValue.TryGetInt32(out var outputCount) && outputCount >= 0) {
                    output = outputCount;
                }
            }
            return previous with { Terminal = terminal, Reason = reason, InputTokens = input, OutputTokens = output };
        } catch (Exception exception) when (exception is JsonException or InvalidOperationException) {
            return previous;
        }
    }
}
