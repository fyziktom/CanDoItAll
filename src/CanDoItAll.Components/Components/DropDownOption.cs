namespace CanDoItAll.Components;

internal sealed record DropDownOption<TValue>(string Key, string Text, TValue? Value);
