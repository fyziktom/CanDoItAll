using System.Text.Json;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.AgentFramework.Memory;

internal static class MemoryFinalOperationResultShaper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static MemoryFinalOperationToolResult? FromOperation(MemoryOperationRecord? operation)
    {
        var finalResult = operation?.GetFinalOperationResult();
        if (finalResult is null)
        {
            return null;
        }

        var contextPack = TryReadContextPack(finalResult.Output, out var outputIsReadable);
        return new MemoryFinalOperationToolResult(
            MemoryToolTrustFraming.Boundary,
            finalResult.Status,
            finalResult.Output?.Kind,
            finalResult.Output?.Kind == MemoryPayloadKind.Text
                ? MemoryToolTrustFraming.FrameOptional(finalResult.Output.Text)
                : null,
            contextPack,
            outputIsReadable,
            finalResult.Warnings.Select(MemoryContextPackToolMapper.MapWarning).ToArray(),
            finalResult.FeedbackHandles
                .Select(handle => MemoryContextPackToolMapper.MapFeedbackHandle(handle))
                .OfType<MemoryFeedbackHandleToolResult>()
                .ToArray(),
            finalResult.SourceRefs.Select(MemoryToolTrustFraming.Frame).ToArray());
    }

    private static MemoryContextPackToolResult? TryReadContextPack(
        MemoryPayload? output,
        out bool outputIsReadable)
    {
        if (output is null || output.Kind == MemoryPayloadKind.Text)
        {
            outputIsReadable = true;
            return null;
        }

        if (output.Kind != MemoryPayloadKind.Json || output.Json is not { } json)
        {
            outputIsReadable = false;
            return null;
        }

        try
        {
            var contextPack = json.Deserialize<MemoryContextPack>(JsonOptions);
            outputIsReadable = contextPack is not null;
            return contextPack is null ? null : MemoryContextPackToolMapper.Map(contextPack);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or ArgumentException)
        {
            outputIsReadable = false;
            return null;
        }
    }
}
