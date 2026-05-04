namespace CanDoItAll.AgentFramework.Models;

public enum AgentProcessCooperationMode
{
    SingleAgent,
    ProcessArtifactHandoff,
    MafLocalHandoff,
    A2ARemoteHandoff,
    Hybrid
}

public sealed record AgentProcessCooperationMetadata(
    AgentProcessCooperationMode CooperationMode,
    AgentWorkspaceToolProfileKind WorkspaceToolProfile,
    string Summary);
