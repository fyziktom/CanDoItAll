namespace CanDoItAll.Modules.CognitiveMemory;

public sealed record CognitiveMemoryAgentContextPackage(
    Guid TraceId,
    CognitiveMemoryRecallContextPackId ContextPackId,
    string Title,
    string Summary,
    IReadOnlyList<CognitiveMemoryAgentContextSection> Sections)
{
    public int IncludedSectionCount => Sections.Count;

    public static CognitiveMemoryAgentContextPackage FromRecallResult(CognitiveMemoryRecallResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new CognitiveMemoryAgentContextPackage(
            result.TraceId,
            result.ContextPack.Id,
            result.ContextPack.Title,
            result.ContextPack.Summary,
            result.ContextPack.Sections
                .Where(section => !string.IsNullOrWhiteSpace(section.Content))
                .Take(8)
                .Select(CognitiveMemoryAgentContextSection.FromRecallSection)
                .ToArray());
    }
}

public sealed record CognitiveMemoryAgentContextSection(
    string Title,
    string Content,
    IReadOnlyList<string> SourceLocators)
{
    public static CognitiveMemoryAgentContextSection FromRecallSection(CognitiveMemoryRecallContextSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        return new CognitiveMemoryAgentContextSection(
            section.Title,
            section.Content.Trim(),
            section.SourceRefs
                .Where(sourceRef => sourceRef.IncludedInContext)
                .Select(sourceRef => sourceRef.Locator)
                .Where(locator => !string.IsNullOrWhiteSpace(locator))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToArray());
    }
}
