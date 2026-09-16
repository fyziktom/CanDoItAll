namespace CanDoItAll.AgentFramework.Core;

public sealed record ToolInvocationWorkspaceScopeFacts(
    bool HasMutationIntent,
    string ManagedArtifactRoot,
    bool UsesScopedRecovery);

public enum ToolInvocationScopePolicyPhase {
    PathArguments,
    Contract,
    BeforeExternalTargetBoundary,
    AfterExternalTargetBoundary,
    AfterReadOnlyTargetBoundary
}

public interface IToolInvocationScopePolicy {
    ToolInvocationPolicyDecision? EvaluateRestrictions(ToolInvocationPolicyContext context, string signature);

    ToolInvocationPolicyDecision? EvaluateRestrictions(ToolInvocationPolicyContext context, string signature,
        ToolInvocationScopePolicyPhase phase)
        => phase == ToolInvocationScopePolicyPhase.Contract ? EvaluateRestrictions(context, signature) : null;

    ToolInvocationWorkspaceScopeFacts GetWorkspaceFacts(ToolInvocationPolicyContext context);

    bool TryCreateRecoverableDeniedResult(string toolName, ToolInvocationPolicyDecision decision,
        ToolInvocationPolicyContext context, out string result);
}
