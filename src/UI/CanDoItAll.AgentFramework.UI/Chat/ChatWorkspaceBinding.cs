using Microsoft.AspNetCore.Components;

namespace CanDoItAll.AgentFramework.UI.Chat;

public sealed record ChatWorkspaceBinding(
    ChatWorkspacePresentation Presentation,
    EventCallback<ChatWorkspaceIntent> Intent,
    AgentActivityState? Activity = null);
