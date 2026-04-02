namespace CanDoItAll.Components.BaseLib;

public sealed class StorageSummaryModel
{
    public string Eyebrow { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Footnote { get; init; } = string.Empty;

    public IReadOnlyList<StorageSummaryBadge> Badges { get; init; } = [];

    public IReadOnlyList<StorageSummaryFact> Facts { get; init; } = [];
}

public sealed record StorageSummaryBadge(string Text, string Tone = "neutral");

public sealed record StorageSummaryFact(string Label, string Value);
