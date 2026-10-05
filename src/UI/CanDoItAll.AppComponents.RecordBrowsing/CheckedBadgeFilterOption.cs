namespace CanDoItAll.AppComponents;

public sealed record CheckedBadgeFilterOption<TValue>(
    TValue Value,
    string Label,
    string TestId)
    where TValue : notnull;
