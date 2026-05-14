namespace CanDoItAll.Components.BaseLib;

public sealed record TimelineItem
{
    public required string Label { get; init; }

    public string? Description { get; init; }

    public string? Meta { get; init; }

    public DateTimeOffset? Timestamp { get; init; }

    public string? Icon { get; init; }

    public string Tone { get; init; } = "neutral";
}

public sealed record EntityPickerItem
{
    public required string Id { get; init; }

    public required string Label { get; init; }

    public string? Description { get; init; }

    public string? Meta { get; init; }

    public string? Icon { get; init; }

    public bool Disabled { get; init; }
}

public sealed record StatusCheckItem
{
    public required string Label { get; init; }

    public string? Description { get; init; }

    public string Status { get; init; } = "Pending";

    public string Tone { get; init; } = "neutral";

    public bool Complete { get; init; }
}

public sealed record ActionReviewFact(string Label, string Value);
