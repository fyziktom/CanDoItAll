namespace CanDoItAll.Modules.AgentFramework;

public sealed record AgentEditorSectionDefinition(AgentEditorSection Section, string Label);

public static class AgentEditorSections {
    public static IReadOnlyList<AgentEditorSectionDefinition> All { get; } = Array.AsReadOnly<AgentEditorSectionDefinition>([
        new(AgentEditorSection.Identity, "Identity"),
        new(AgentEditorSection.Runtime, "Runtime"),
        new(AgentEditorSection.Memory, "Memory"),
        new(AgentEditorSection.Images, "Images"),
        new(AgentEditorSection.ProjectStructureAccess, "Project Structure Access"),
        new(AgentEditorSection.WorkspaceTools, "Workspace Tools"),
        new(AgentEditorSection.Secrets, "Secrets"),
        new(AgentEditorSection.ProcessAccess, "Process Access"),
        new(AgentEditorSection.Capabilities, "Capabilities"),
        new(AgentEditorSection.Voice, "Voice"),
    ]);

    public static int IndexOf(AgentEditorSection section) {
        for (var index = 0; index < All.Count; index++) {
            if (All[index].Section == section) {
                return index;
            }
        }
        throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown agent editor section.");
    }

    public static AgentEditorSectionDefinition At(int index)
        => index >= 0 && index < All.Count ? All[index]
            : throw new ArgumentOutOfRangeException(nameof(index), index, "Unknown agent editor tab.");
}
