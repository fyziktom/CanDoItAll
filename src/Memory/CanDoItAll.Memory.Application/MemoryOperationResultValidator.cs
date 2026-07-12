using System.Text;
using System.Text.Json;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

internal static class MemoryOperationResultValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string? GetFailure(
        MemoryOperationRecord operation,
        MemoryOperationResult result,
        MemoryProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(provider);
        MemoryRequestContext context;
        try
        {
            context = operation.GetRequiredMemoryRequestContext();
        }
        catch (InvalidOperationException)
        {
            return "Memory provider final result cannot be validated without persisted request context.";
        }

        if (result.Warnings is null ||
            result.FeedbackHandles is null ||
            result.SourceRefs is null ||
            result.Warnings.Any(warning => warning is null || string.IsNullOrWhiteSpace(warning.Message)) ||
            result.FeedbackHandles.Any(handle => string.IsNullOrWhiteSpace(handle.Value)) ||
            result.SourceRefs.Any(string.IsNullOrWhiteSpace))
        {
            return "Memory provider returned malformed final operation metadata.";
        }

        if (result.SourceRefs.Count > provider.Manifest.Limits.MaxSourceItems)
        {
            return $"Memory provider final result exceeds the source limit of {provider.Manifest.Limits.MaxSourceItems}.";
        }

        if (GetPayloadFailure(result.Output, context, provider.Manifest.Limits) is { } payloadFailure)
        {
            return payloadFailure;
        }

        var metadataBytes = result.Warnings.Sum(warning => CountUtf8(warning.Message)) +
            result.FeedbackHandles.Sum(handle => CountUtf8(handle.Value)) +
            result.SourceRefs.Sum(CountUtf8);
        var payloadBytes = result.Output switch
        {
            { Kind: MemoryPayloadKind.Text, Text: { } text } => CountUtf8(text),
            { Kind: MemoryPayloadKind.Json, Json: { } json } => CountUtf8(json.GetRawText()),
            _ => 0
        };
        if (metadataBytes + payloadBytes > context.Budget.MaxSourceBytes)
        {
            return $"Memory provider final result exceeds the UTF-8 byte budget of {context.Budget.MaxSourceBytes}.";
        }

        return null;
    }

    private static string? GetPayloadFailure(
        MemoryPayload? payload,
        MemoryRequestContext context,
        MemoryProviderLimits limits)
    {
        if (payload is null)
        {
            return null;
        }

        if (payload.Kind == MemoryPayloadKind.Text)
        {
            return string.IsNullOrWhiteSpace(payload.Text) || payload.Json is not null
                ? "Memory provider returned a malformed text operation payload."
                : null;
        }

        if (payload.Kind != MemoryPayloadKind.Json || payload.Text is not null || payload.Json is not { } json)
        {
            return "Memory provider returned a malformed JSON operation payload.";
        }

        try
        {
            var contextPack = json.Deserialize<MemoryContextPack>(JsonOptions);
            return contextPack is null
                ? "Memory provider returned an empty final context pack."
                : MemoryContextPackValidator.GetFailure(contextPack, context.Budget, limits);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or ArgumentException)
        {
            return "Memory provider returned a malformed final context pack.";
        }
    }

    private static int CountUtf8(string value) => Encoding.UTF8.GetByteCount(value);
}
