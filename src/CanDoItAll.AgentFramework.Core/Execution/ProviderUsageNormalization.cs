using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record ProviderUsageNormalizationRequest(
    ProviderProfile Provider,
    string Model,
    string SourcePhase,
    ProviderUsageObservationStatus UsageStatus,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    int ReasoningTokens,
    int TotalTokens,
    int ToolCallCount,
    string ProviderResponseId,
    string ProviderRequestId,
    string RuntimeSessionKey,
    string RawUsageJson,
    string DiagnosticsJson);

public interface IProviderUsageNormalizer
{
    ProviderUsageObservation Normalize(ProviderUsageNormalizationRequest request);
}

public sealed class DefaultProviderUsageNormalizer : IProviderUsageNormalizer
{
    public static IProviderUsageNormalizer Instance { get; } = new DefaultProviderUsageNormalizer();

    private DefaultProviderUsageNormalizer()
    {
    }

    public ProviderUsageObservation Normalize(ProviderUsageNormalizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Provider);

        var normalized = request.Provider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi
            ? OpenAiProviderUsageNormalizer.Normalize(request)
            : ProviderUsageNormalizationSnapshot.FromRequest(request);

        return new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            ProviderName: request.Provider.Name,
            ProviderKind: request.Provider.Kind,
            Model: string.IsNullOrWhiteSpace(request.Model) ? request.Provider.DefaultModel : request.Model,
            TransportKind: request.Provider.Transport,
            SourcePhase: string.IsNullOrWhiteSpace(request.SourcePhase) ? ProviderUsageSourcePhases.AgentRuntime : request.SourcePhase,
            UsageStatus: normalized.UsageStatus,
            InputTokens: normalized.InputTokens,
            CachedInputTokens: normalized.CachedInputTokens,
            OutputTokens: normalized.OutputTokens,
            ReasoningTokens: normalized.ReasoningTokens,
            TotalTokens: normalized.TotalTokens,
            ToolCallCount: Math.Max(0, request.ToolCallCount))
        {
            ProviderResponseId = normalized.ProviderResponseId,
            ProviderRequestId = normalized.ProviderRequestId,
            RuntimeSessionKey = request.RuntimeSessionKey,
            RawUsageJson = request.RawUsageJson,
            DiagnosticsJson = normalized.DiagnosticsJson
        };
    }
}

internal static class OpenAiProviderUsageNormalizer
{
    public static ProviderUsageNormalizationSnapshot Normalize(ProviderUsageNormalizationRequest request)
    {
        var fallback = ProviderUsageNormalizationSnapshot.FromRequest(request);
        if (string.IsNullOrWhiteSpace(request.RawUsageJson))
        {
            return fallback;
        }

        try
        {
            using var document = JsonDocument.Parse(request.RawUsageJson);
            var root = document.RootElement;
            var responseId = CoalesceText(
                request.ProviderResponseId,
                ReadString(root, "id"),
                ReadString(root, "response_id"),
                ReadString(root, "responseId"));
            var requestId = CoalesceText(
                request.ProviderRequestId,
                ReadString(root, "request_id"),
                ReadString(root, "requestId"),
                ReadString(root, "x_request_id"),
                ReadString(root, "xRequestId"));

            if (TryGetProperty(root, "usage", out var usageProperty))
            {
                if (usageProperty.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    return fallback with
                    {
                        UsageStatus = ProviderUsageObservationStatus.UsageUnavailable,
                        InputTokens = 0,
                        CachedInputTokens = 0,
                        OutputTokens = 0,
                        ReasoningTokens = 0,
                        TotalTokens = 0,
                        ProviderResponseId = responseId,
                        ProviderRequestId = requestId
                    };
                }

                root = usageProperty;
            }

            var inputTokens = ReadTokenCount(root, "input_tokens", "inputTokens", "inputTokenCount")
                ?? fallback.InputTokens;
            var cachedInputTokens = ReadNestedTokenCount(root, "input_tokens_details", "cached_tokens")
                ?? ReadNestedTokenCount(root, "inputTokensDetails", "cachedTokens")
                ?? ReadTokenCount(root, "cached_input_tokens", "cachedInputTokens", "cachedInputTokenCount")
                ?? ReadAdditionalCount(root, "input_tokens_details.cached_tokens", "cached_tokens", "cachedInputTokens", "CachedInputTokenCount")
                ?? fallback.CachedInputTokens;
            var outputTokens = ReadTokenCount(root, "output_tokens", "outputTokens", "outputTokenCount")
                ?? fallback.OutputTokens;
            var reasoningTokens = ReadNestedTokenCount(root, "output_tokens_details", "reasoning_tokens")
                ?? ReadNestedTokenCount(root, "outputTokensDetails", "reasoningTokens")
                ?? ReadTokenCount(root, "reasoning_tokens", "reasoningTokens", "ReasoningTokens")
                ?? ReadAdditionalCount(root, "output_tokens_details.reasoning_tokens", "reasoning_tokens", "reasoningTokens", "ReasoningTokens")
                ?? fallback.ReasoningTokens;
            var totalTokens = ReadTokenCount(root, "total_tokens", "totalTokens", "totalTokenCount")
                ?? fallback.TotalTokens;

            if (totalTokens <= 0 && inputTokens + outputTokens > 0)
            {
                totalTokens = inputTokens + outputTokens;
            }

            return fallback with
            {
                UsageStatus = ProviderUsageObservationStatus.Observed,
                InputTokens = Math.Max(0, inputTokens),
                CachedInputTokens = Math.Clamp(cachedInputTokens, 0, Math.Max(0, inputTokens)),
                OutputTokens = Math.Max(0, outputTokens),
                ReasoningTokens = Math.Max(0, reasoningTokens),
                TotalTokens = Math.Max(0, totalTokens),
                ProviderResponseId = responseId,
                ProviderRequestId = requestId
            };
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private static int? ReadNestedTokenCount(JsonElement root, string objectName, string propertyName)
    {
        return TryGetProperty(root, objectName, out var nested)
            ? ReadTokenCount(nested, propertyName)
            : null;
    }

    private static int? ReadAdditionalCount(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, "additionalCounts", out var additionalCounts) &&
            !TryGetProperty(root, "additional_counts", out additionalCounts))
        {
            return null;
        }

        foreach (var name in names)
        {
            var value = ReadTokenCount(additionalCounts, name);
            if (value.HasValue)
            {
                return value;
            }
        }

        return null;
    }

    private static int? ReadTokenCount(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!TryGetProperty(element, propertyName, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var longValue))
            {
                return ClampTokenCount(longValue);
            }

            if (property.ValueKind == JsonValueKind.String &&
                long.TryParse(property.GetString(), out var parsedValue))
            {
                return ClampTokenCount(parsedValue);
            }
        }

        return null;
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return TryGetProperty(element, propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var candidate in element.EnumerateObject())
            {
                if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    property = candidate.Value;
                    return true;
                }
            }
        }

        property = default;
        return false;
    }

    private static string CoalesceText(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static int ClampTokenCount(long tokenCount)
    {
        if (tokenCount <= 0)
        {
            return 0;
        }

        return tokenCount > int.MaxValue
            ? int.MaxValue
            : (int)tokenCount;
    }
}

internal sealed record ProviderUsageNormalizationSnapshot(
    ProviderUsageObservationStatus UsageStatus,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    int ReasoningTokens,
    int TotalTokens,
    string ProviderResponseId,
    string ProviderRequestId,
    string DiagnosticsJson)
{
    public static ProviderUsageNormalizationSnapshot FromRequest(ProviderUsageNormalizationRequest request)
    {
        var inputTokens = Math.Max(0, request.InputTokens);
        var outputTokens = Math.Max(0, request.OutputTokens);
        var totalTokens = Math.Max(0, request.TotalTokens);
        if (totalTokens == 0 && inputTokens + outputTokens > 0)
        {
            totalTokens = inputTokens + outputTokens;
        }

        if (!ProviderPricingCalculator.IsKnownUsageStatus(request.UsageStatus))
        {
            inputTokens = 0;
            outputTokens = 0;
            totalTokens = 0;
        }

        return new ProviderUsageNormalizationSnapshot(
            request.UsageStatus,
            inputTokens,
            Math.Clamp(request.CachedInputTokens, 0, inputTokens),
            outputTokens,
            ProviderPricingCalculator.IsKnownUsageStatus(request.UsageStatus) ? Math.Max(0, request.ReasoningTokens) : 0,
            totalTokens,
            request.ProviderResponseId,
            request.ProviderRequestId,
            request.DiagnosticsJson);
    }
}
