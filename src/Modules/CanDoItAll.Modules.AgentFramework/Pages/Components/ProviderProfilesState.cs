namespace CanDoItAll.Modules.AgentFramework;

public enum ProviderEditorSection {
    Connection,
    Prices,
    Runtime,
    Thinking,
    Sharing,
    History
}

public enum ProviderProfilesLoadState { Loading, Ready, Failed }

public sealed record ProviderProfilesState(
    Guid? ProviderId = null,
    ProviderEditorSection Section = ProviderEditorSection.Connection,
    bool SharedConnectionsOpen = false);

public sealed record ProviderEditorSectionDefinition(ProviderEditorSection Section, string Label, string Icon);

public static class ProviderEditorSections {
    public static IReadOnlyList<ProviderEditorSectionDefinition> All { get; } = Array.AsReadOnly<ProviderEditorSectionDefinition>([
        new(ProviderEditorSection.Connection, "Connection", "plug"),
        new(ProviderEditorSection.Prices, "Prices", "paid"),
        new(ProviderEditorSection.Runtime, "Runtime", "tune"),
        new(ProviderEditorSection.Thinking, "Thinking", "psychology"),
        new(ProviderEditorSection.Sharing, "Sharing", "share"),
        new(ProviderEditorSection.History, "History", "history")
    ]);

    public static int IndexOf(ProviderEditorSection section) {
        for (var index = 0; index < All.Count; index++) {
            if (All[index].Section == section) {
                return index;
            }
        }
        throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown provider editor section.");
    }

    public static ProviderEditorSectionDefinition At(int index)
        => index >= 0 && index < All.Count ? All[index]
            : throw new ArgumentOutOfRangeException(nameof(index), index, "Unknown provider editor tab.");
}
