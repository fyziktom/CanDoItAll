namespace CanDoItAll.Modules.AgentFramework;

public sealed record AgentEditorProject(Guid Id, string Name);
public sealed record AgentEditorSecret(Guid Id, string Name, string KindLabel);
