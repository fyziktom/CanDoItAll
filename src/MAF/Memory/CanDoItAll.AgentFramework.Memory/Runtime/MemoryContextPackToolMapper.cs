using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.AgentFramework.Memory;

internal static class MemoryContextPackToolMapper
{
    public static MemoryContextPackToolResult Map(MemoryContextPack contextPack) =>
        new(
            MemoryToolTrustFraming.Boundary,
            contextPack.ContextPackId.Value,
            MemoryToolTrustFraming.Frame(contextPack.Summary),
            contextPack.Sections.Select(section => new MemoryContextSectionToolResult(
                MemoryToolTrustFraming.Frame(section.Title),
                MemoryToolTrustFraming.Frame(section.Text),
                section.Citations.Select(citation =>
                    new MemoryToolCitationResult(
                        MemoryToolTrustFraming.Frame(citation.SourceRef),
                        MemoryToolTrustFraming.Frame(citation.Label))).ToArray(),
                section.Confidence)).ToArray(),
            contextPack.Warnings.Select(MapWarning).ToArray(),
            contextPack.ProviderConfidence,
            MapFeedbackHandle(contextPack.FeedbackHandle));

    public static MemoryToolWarningResult MapWarning(MemoryWarning warning) =>
        new(warning.Kind.ToString(), MemoryToolTrustFraming.Frame(warning.Message));

    public static MemoryFeedbackHandleToolResult? MapFeedbackHandle(MemoryFeedbackHandle? handle) =>
        handle is null ? null : new MemoryFeedbackHandleToolResult(handle.Value.Value);
}
