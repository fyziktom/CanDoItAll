using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkspaceExecutionAuditContext
{
    private static readonly AsyncLocal<WorkspaceExecutionAuditScopeState?> CurrentState = new();

    public static WorkspaceExecutionAuditScopeState? Current => CurrentState.Value;

    public static IDisposable BeginScope(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        var previous = CurrentState.Value;
        CurrentState.Value = new WorkspaceExecutionAuditScopeState(
            run.Id,
            run.AgentId,
            run.ChatSessionId,
            run.CorrelationId,
            run.SourceKind,
            run.SourceId,
            run.ProcessRunId,
            run.ProcessStepId,
            run.SchedulerRunId,
            run.MessageId,
            run.ProviderName,
            run.Model);
        return new Scope(previous);
    }

    public sealed record WorkspaceExecutionAuditScopeState(
        Guid ExecutionRunId,
        Guid AgentId,
        Guid? ChatSessionId,
        string CorrelationId,
        string SourceKind,
        string SourceId,
        string ProcessRunId,
        string ProcessStepId,
        string SchedulerRunId,
        string MessageId,
        string ProviderName,
        string Model);

    private sealed class Scope(WorkspaceExecutionAuditScopeState? previous) : IDisposable
    {
        public void Dispose()
        {
            CurrentState.Value = previous;
        }
    }
}
