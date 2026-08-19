namespace CanDoItAll.AppComponents;

public enum SelectedReferenceStatusTone
{
    Neutral,
    Info,
    Success,
    Warning,
    Danger
}

public sealed record SelectedReferenceItem<TKey>(
    TKey Key,
    string PrimaryText,
    string IdentifierText)
    where TKey : notnull
{
    public string DetailText { get; init; } = string.Empty;

    public string StatusText { get; init; } = string.Empty;

    public SelectedReferenceStatusTone StatusTone { get; init; } = SelectedReferenceStatusTone.Neutral;

    public bool CanRemove { get; init; } = true;

    public string TestId { get; init; } = string.Empty;
}
