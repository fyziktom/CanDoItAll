namespace CanDoItAll.AgentFramework.Models;

public static class AgentToolEvidenceMessage
{
    public const string Prefix = "[Trusted tool outcome evidence]";
}

public sealed record AgentToolEvidenceOwnership(
    Guid ChatSessionId,
    Guid AgentId,
    Guid DatabaseProfileId,
    long DatabaseProfileGeneration,
    string SourceKind,
    string SourceId,
    WorkspaceScopeDescriptor WorkspaceScope);