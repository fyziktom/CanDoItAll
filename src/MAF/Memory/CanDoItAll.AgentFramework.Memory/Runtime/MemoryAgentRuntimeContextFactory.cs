using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.AgentFramework.Memory;

internal static class MemoryAgentRuntimeContextFactory
{
    private const string WorkflowSourceKind = "workflow";
    private const string WorkflowNodeSourceKind = "workflow-node";
    private const string ProcessSourceKind = "process";
    private const string ChatSessionSourceKind = "chat-session";

    public static MemoryRequestContext CreateRequestContext(
        WorkspaceScopeDescriptor workspaceScope,
        AgentRuntimeContextIntent contextIntent,
        AgentMemoryAccessSettings settings)
    {
        var effectiveWorkspaceScope = contextIntent.WorkspaceScope ?? workspaceScope;
        var projectId = effectiveWorkspaceScope.Kind == WorkspaceScopeKind.Project
            ? effectiveWorkspaceScope.Key
            : null;
        var workflowId = IsSourceKind(contextIntent, WorkflowSourceKind)
            ? contextIntent.SourceId
            : null;
        var workflowNodeId = IsSourceKind(contextIntent, WorkflowNodeSourceKind)
            ? contextIntent.SourceId
            : null;
        var processId = ResolveProcessId(contextIntent);
        var workspace = new MemoryWorkspaceContext(
            effectiveWorkspaceScope.Key,
            effectiveWorkspaceScope.DisplayName,
            CustomerId: null,
            effectiveWorkspaceScope.Kind.ToString(),
            []);
        var execution = new MemoryExecutionContext(
            projectId,
            ProjectName: null,
            processId,
            EmptyToNull(contextIntent.ProcessStepId),
            ProcessStepName: null,
            workflowId,
            workflowNodeId,
            ArtifactIds: []);
        var policy = MemoryPolicyContext.InternalDefault with
        {
            AllowedSourceScopes = settings.AllowedSourceScopes
        };
        return new MemoryRequestContext(
            workspace,
            execution,
            policy,
            MemoryBudget.Default,
            MemoryExtensionData.Empty);
    }

    public static MemoryLedgerRequester CreateRequester(
        AgentDefinition agent,
        AgentRuntimeContextIntent contextIntent,
        string? runtimeSessionKey = null)
    {
        var workflowId = IsSourceKind(contextIntent, WorkflowSourceKind)
            ? contextIntent.SourceId
            : null;
        var workflowNodeId = IsSourceKind(contextIntent, WorkflowNodeSourceKind)
            ? contextIntent.SourceId
            : null;
        return new MemoryLedgerRequester(
            agent.Id.ToString("D"),
            agent.Id.ToString("D"),
            agent.Workload.ToString(),
            ResolveSessionId(contextIntent, runtimeSessionKey),
            workflowId,
            workflowNodeId,
            ResolveProcessId(contextIntent),
            EmptyToNull(contextIntent.ProcessStepId));
    }

    public static MemoryProviderInstanceId? ResolveAssignmentProvider(
        AgentDefinition agent,
        AgentRuntimeContextIntent contextIntent,
        AgentMemoryAccessSettings settings)
    {
        foreach (var scope in AssignmentResolutionOrder)
        {
            var key = ResolveAssignmentKey(agent, contextIntent, scope);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var assignment = settings.ProviderAssignments.FirstOrDefault(item =>
                item.Scope == scope &&
                string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
            if (assignment is not null)
            {
                return assignment.ProviderInstanceId;
            }
        }

        return null;
    }

    private static string? ResolveAssignmentKey(
        AgentDefinition agent,
        AgentRuntimeContextIntent intent,
        MemoryProviderAssignmentScope scope)
    {
        return scope switch
        {
            MemoryProviderAssignmentScope.Agent => agent.Id.ToString("D"),
            MemoryProviderAssignmentScope.AgentRole => agent.Workload.ToString(),
            MemoryProviderAssignmentScope.Workflow when IsSourceKind(intent, WorkflowSourceKind) => EmptyToNull(intent.SourceId),
            MemoryProviderAssignmentScope.WorkflowNode when IsSourceKind(intent, WorkflowNodeSourceKind) => EmptyToNull(intent.SourceId),
            MemoryProviderAssignmentScope.Process => ResolveProcessId(intent),
            _ => null
        };
    }

    private static string? ResolveProcessId(AgentRuntimeContextIntent intent)
    {
        var processRunId = EmptyToNull(intent.ProcessRunId);
        if (processRunId is not null)
        {
            return processRunId;
        }

        return IsSourceKind(intent, ProcessSourceKind)
            ? EmptyToNull(intent.SourceId)
            : null;
    }

    private static string? ResolveSessionId(
        AgentRuntimeContextIntent intent,
        string? runtimeSessionKey)
    {
        return EmptyToNull(runtimeSessionKey) ??
               (IsSourceKind(intent, ChatSessionSourceKind) ? EmptyToNull(intent.SourceId) : null);
    }

    private static bool IsSourceKind(AgentRuntimeContextIntent intent, string sourceKind)
    {
        return string.Equals(intent.SourceKind, sourceKind, StringComparison.OrdinalIgnoreCase);
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static readonly MemoryProviderAssignmentScope[] AssignmentResolutionOrder =
    [
        MemoryProviderAssignmentScope.WorkflowNode,
        MemoryProviderAssignmentScope.Workflow,
        MemoryProviderAssignmentScope.Process,
        MemoryProviderAssignmentScope.Agent,
        MemoryProviderAssignmentScope.AgentRole
    ];
}
