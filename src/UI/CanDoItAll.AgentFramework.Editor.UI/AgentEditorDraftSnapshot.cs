using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.AgentFramework.Editor.UI;

public static class AgentEditorDraftSnapshot {
    public static AgentEditorModel Copy(AgentEditorModel source) => AgentEditorModelSnapshot.Copy(source);
    public static void ApplyTo(AgentEditorModel source, AgentEditorModel target) => AgentEditorModelSnapshot.ApplyTo(source, target);
    public static AgentWorkspaceToolAccessSettings CopyWorkspaceAccess(AgentWorkspaceToolAccessSettings source)
        => AgentEditorModelSnapshot.CopyWorkspaceAccess(source);
}