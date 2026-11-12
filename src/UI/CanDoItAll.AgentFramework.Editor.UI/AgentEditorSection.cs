namespace CanDoItAll.Modules.AgentFramework;

public enum AgentEditorSection {
    Identity,
    Runtime,
    Memory,
    Images,
    ProjectStructureAccess,
    WorkspaceTools,
    Secrets,
    ProcessAccess,
    Capabilities,
    Voice
}

public enum AgentEditorLoadState { Loading, Ready, Failed }
