using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal readonly record struct MafToolApprovalPath(
    bool Automatic, bool WrapperAvailable, bool WrapperEffectiveForProvider, bool ApplicationAvailable = false);

internal delegate ScriptContentInspection MafToolPolicyScriptInspector(string functionName,
    IReadOnlyList<KeyValuePair<string, object?>> arguments,
    WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState? auditScope,
    string manifest, EffectiveExternalTargetAccessScope? externalTargets);

internal static class MafToolInvocationPolicyContextFactory {
    internal static ToolInvocationPolicyContext Create(AgentDefinition agentDefinition, string functionName,
        IReadOnlyList<KeyValuePair<string, object?>> invocationArguments, ToolInvocationClassification classification,
        AgentToolPolicyCatalog toolPolicies, bool isKnownTool, MafToolApprovalPath approval,
        AgentExecutionGovernanceSnapshot? executionGovernance, MafToolPolicyScriptInspector inspectScript,
        WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState? auditScope,
        IReadOnlyList<AgentToolInvocationTrace> traces) {
        var configuredWorkspaceAccess = AgentWorkspaceToolAccessMetadata.Read(agentDefinition.ConfigurationJson);
        var redactedArguments = AgentToolInvocationPolicyMetadata.RedactArguments(functionName, invocationArguments, toolPolicies);
        var pathArguments = ToolInvocationPathArgumentResolver.Resolve(functionName, invocationArguments);
        var externalTargetAccess = EffectiveExternalTargetAccessResolver.Resolve(
            configuredWorkspaceAccess,
            auditScope?.AllowedExternalTargetAliases,
            auditScope?.ReadOnlyExternalTargetAliases,
            auditScope?.InvocationExternalTargetScopeIsAuthoritative == true);
        var scriptSideEffectManifestJson = MafScriptPolicyInspectionService.TryGetStringArgument(
            invocationArguments,
            GovernedScriptSideEffectManifest.ArgumentName) ?? string.Empty;
        var scriptInspection = inspectScript(
            functionName,
            invocationArguments,
            auditScope,
            scriptSideEffectManifestJson,
            externalTargetAccess);
        return new ToolInvocationPolicyContext(
            AgentId: agentDefinition.Id,
            AgentName: agentDefinition.Name,
            ToolName: functionName,
            RedactedArguments: redactedArguments,
            Classification: classification,
            IsKnownTool: isKnownTool,
            AutoApprovalAllowed: approval.Automatic,
            ApprovalWrapperAvailable: approval.WrapperAvailable,
            ExecutionRunId: auditScope?.ExecutionRunId.ToString("D") ?? string.Empty,
            SourceKind: auditScope?.SourceKind ?? string.Empty,
            ProcessRunId: string.Empty,
            ProcessStepId: string.Empty,
            AllowedExternalTargetAliases: externalTargetAccess.WritableAliases,
            ReadOnlyExternalTargetAliases: externalTargetAccess.ReadOnlyAliases,
            ApprovalWrapperEffectiveForProvider: approval.WrapperEffectiveForProvider,
            ApplicationApprovalAvailable: approval.ApplicationAvailable,
            ContextWorkspaceScopeKind: auditScope?.ContextWorkspaceScope?.Kind.ToString() ?? string.Empty,
            ContextWorkspaceScopeKey: auditScope?.ContextWorkspaceScope?.Key ?? string.Empty,
            InspectedScriptContent: scriptInspection.Content,
            ScriptInspectionFailure: scriptInspection.FailureMessage,
            ScriptSideEffectManifestJson: scriptSideEffectManifestJson,
            ToolInvocationTraces: traces) {
            DeclaredCapability = toolPolicies.TryResolve(functionName, out var declaredCapability)
                ? declaredCapability
                : null,
            SourceId = auditScope?.SourceId ?? string.Empty,
            AllowedManagedArtifactReadRefs = auditScope?.AllowedManagedArtifactReadRefs ?? [],
            ScriptEnvironmentAccessAllowed = configuredWorkspaceAccess.CanScriptsReadEnvironment,
            ExecutionGovernance = executionGovernance,
            PathArguments = pathArguments
        };
    }
}
