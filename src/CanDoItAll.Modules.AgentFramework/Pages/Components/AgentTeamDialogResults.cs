namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public sealed record AgentTeamDetailsDialogResult(Guid TeamId);

public sealed record AgentTeamMembersDialogResult(Guid TeamId, IReadOnlyList<Guid> AgentIds);
