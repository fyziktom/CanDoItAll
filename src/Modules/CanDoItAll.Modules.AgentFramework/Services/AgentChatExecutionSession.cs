using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class AgentChatExecutionSession(AgentChatOwnerId owner, AgentChatExecutionSnapshot snapshot) {
    public AgentChatOwnerId Owner { get; } = owner;
    public AgentChatExecutionSnapshot Snapshot { get; set; } = snapshot;
    public IAgentChatExecutionScope? Scope { get; set; }
    public Task ScopeRelease { get; set; } = Task.CompletedTask;
    public CancellationTokenSource? Cancellation { get; set; }
    public Task CancellationNotification { get; set; } = Task.CompletedTask;
    public TaskCompletionSource? Completion { get; set; }
    public bool Busy { get; set; }
    public ActiveAgentChatRunState PreviousRunState { get; set; }
    public bool Retired { get; set; }
    public ActiveAgentChatRunState? SettledRunState { get; set; }
    public AgentChatExecutionCompleted? SettledNotification { get; set; }
}
