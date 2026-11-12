using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class AgentChatCatalogActivityLease(IAgentExecutionActivityOperationLease operation,
    Action<ActiveAgentChatRunState?, Guid?, Guid?> changed) : IAgentExecutionActivityOperationLease {
    public AgentExecutionActivityStreamId StreamId => operation.StreamId;
    public Guid? AgentId => operation.AgentId;
    public Guid? ChatSessionId => operation.ChatSessionId;
    public Guid? ExecutionRunId => operation.ExecutionRunId;
    public bool IsTerminal => operation.IsTerminal;
    public void BindAgent(Guid agentId) => operation.BindAgent(agentId);
    public void BindContext(AgentChatContextSource source, long version) => operation.BindContext(source, version);
    public void BindChatSession(Guid sessionId) {
        operation.BindChatSession(sessionId);
        changed(null, ChatSessionId, ExecutionRunId);
    }
    public void BindExecutionRun(Guid runId, Guid? sessionId = null) {
        operation.BindExecutionRun(runId, sessionId);
        changed(null, ChatSessionId, ExecutionRunId);
    }
    public void Report(AgentExecutionActivityPhase phase, string message) {
        operation.Report(phase, message);
        changed(phase switch {
            AgentExecutionActivityPhase.AwaitingApproval => ActiveAgentChatRunState.AwaitingApproval,
            AgentExecutionActivityPhase.AwaitingContinuation => ActiveAgentChatRunState.AwaitingContinuation,
            _ => null
        }, ChatSessionId, ExecutionRunId);
    }
    public void Complete(string message) => operation.Complete(message);
    public void Fail(string message, string? errorCode = null) => operation.Fail(message, errorCode);
    public void Cancel(string message) => operation.Cancel(message);
    public void Suspend(string message) => operation.Suspend(message);
    public void SuspendForContinuation(AgentEffectContinuation continuation, string message) {
        operation.SuspendForContinuation(continuation, message);
        changed(ActiveAgentChatRunState.AwaitingContinuation, ChatSessionId, ExecutionRunId);
    }
    public void Dispose() => operation.Dispose();
}
