using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkspaceExecutionAuditContext
{
    private static readonly AsyncLocal<WorkspaceExecutionAuditScopeState?> CurrentState = new();

    public static WorkspaceExecutionAuditScopeState? Current => CurrentState.Value;

    public static IDisposable BeginScope(
        ExecutionRunRecord run,
        WorkspaceScopeDescriptor? contextWorkspaceScope = null,
        WorkspaceScopeDescriptor? executionWorkspaceScope = null)
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
            run.Model,
            ExecutionInvocationMetadata.ResolveExternalTargetRootBindings(run),
            ExecutionInvocationMetadata.ResolveAllowedExternalTargetAliases(run),
            ExecutionInvocationMetadata.ResolveReadOnlyExternalTargetAliases(run),
            ExecutionInvocationMetadata.ResolveAllowedManagedArtifactReadRefs(run),
            ExecutionInvocationMetadata.ResolveProcessCooperationMode(run),
            ExecutionInvocationMetadata.ResolveProcessWorkspaceToolProfile(run),
            ExecutionInvocationMetadata.ResolveProcessBrowserToolsAllowed(run),
            ExecutionInvocationMetadata.ResolveProcessAllowsProductMutation(run),
            ExecutionInvocationMetadata.ResolveProcessRequiresProductMutationBeforeManagedOutput(run),
            ExecutionInvocationMetadata.ResolveProcessProductMutationToolNames(run),
            ExecutionInvocationMetadata.ResolveProcessProductMutationRequiredBranchOutcomeKeys(run),
            ExecutionInvocationMetadata.ResolveProcessStepAllowedOperations(run),
            ExecutionInvocationMetadata.ResolveProcessStepTargetScope(run),
            contextWorkspaceScope ?? ExecutionInvocationMetadata.ResolveContextWorkspaceScope(run),
            ExecutionInvocationMetadata.ResolveProjectStructureLaunchAgent(run),
            ExecutionInvocationMetadata.ResolveProjectStructureProcessNodeContext(run),
            executionWorkspaceScope);
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
        string Model,
        IReadOnlyList<ExternalTargetRootBinding> ExternalTargetRootBindings,
        IReadOnlyList<string> AllowedExternalTargetAliases,
        IReadOnlyList<string> ReadOnlyExternalTargetAliases,
        IReadOnlyList<string> AllowedManagedArtifactReadRefs,
        AgentProcessCooperationMode? ProcessCooperationMode,
        AgentWorkspaceToolProfileKind? WorkspaceToolProfileOverride,
        bool ProcessBrowserToolsAllowed,
        bool ProcessAllowsProductMutation,
        bool ProcessRequiresProductMutationBeforeManagedOutput,
        IReadOnlyList<string> ProcessProductMutationToolNames,
        IReadOnlyList<string> ProcessProductMutationRequiredBranchOutcomeKeys,
        IReadOnlyList<string> ProcessStepAllowedOperations,
        string ProcessStepTargetScope,
        WorkspaceScopeDescriptor? ContextWorkspaceScope,
        ProjectStructureAgentIdentityDescriptor? ProjectStructureLaunchAgent,
        ProjectStructureProcessNodeContextDescriptor? ProjectStructureProcessNodeContext,
        WorkspaceScopeDescriptor? ExecutionWorkspaceScope = null);

    private sealed class Scope(WorkspaceExecutionAuditScopeState? previous) : IDisposable
    {
        public void Dispose()
        {
            CurrentState.Value = previous;
        }
    }
}
