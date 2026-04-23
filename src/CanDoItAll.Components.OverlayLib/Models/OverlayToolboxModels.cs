namespace CanDoItAll.Components.OverlayLib;

public sealed record OverlayToolboxBadge(
    string Text,
    string Tone = "label",
    string DataTestId = "");

public sealed record OverlayToolboxSection(
    string Key,
    string Label,
    string Description,
    IReadOnlyList<OverlayToolboxGroup> Groups,
    string Tone = "accent",
    bool IsExpanded = true,
    string DataTestId = "",
    string BodyDataTestId = "");

public sealed record OverlayToolboxGroup(
    string Key,
    string Label,
    string Summary,
    IReadOnlyList<OverlayToolboxItem> Items,
    string Icon = "",
    string Tone = "neutral",
    bool IsExpanded = true,
    string DataTestId = "",
    string BodyDataTestId = "");

public sealed record OverlayToolboxItem(
    string ActionId,
    string Label,
    string Summary,
    string Icon = "",
    string Glyph = "",
    string Tone = "neutral",
    bool IsDisabled = false,
    string DataTestId = "",
    string Metadata = "");
