using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record WorkspaceSearchRootRequest(string RelativePath, bool IsWorkspaceRoot);

public sealed record WorkspacePathScopeContribution(
    WorkspaceScopeDescriptor Scope,
    Func<ToolInvocationPolicyContext, string, ToolInvocationPolicyDecision?> RestrictInvocation,
    Func<WorkspaceSearchRootRequest, IReadOnlyList<string>> RestrictSearchRoots) {
    public void RequireScope(WorkspaceScopeDescriptor? scope) {
        if (Scope is null || scope is null || Scope != scope) {
            throw new InvalidOperationException("A workspace path contribution must match the exact execution workspace scope.");
        }
        if (RestrictInvocation is null || RestrictSearchRoots is null) {
            throw new InvalidOperationException("A workspace path contribution requires both restriction delegates.");
        }
    }

    public static WorkspaceScopeDescriptor? GetContextScope(ToolInvocationPolicyContext context) {
        ArgumentNullException.ThrowIfNull(context);
        if (!Enum.TryParse<WorkspaceScopeKind>(context.ContextWorkspaceScopeKind, true, out var kind) ||
            !Enum.IsDefined(kind) || (kind != WorkspaceScopeKind.Sandbox && string.IsNullOrWhiteSpace(context.ContextWorkspaceScopeKey))) {
            return null;
        }
        return WorkspaceScopeDescriptor.Sandbox with {
            Kind = kind,
            Key = context.ContextWorkspaceScopeKey
        };
    }

    public static WorkspacePathScopeContribution? Resolve(WorkspaceScopeDescriptor? scope,
        IEnumerable<IToolInvocationPolicyContextContributor> contributors) {
        ArgumentNullException.ThrowIfNull(contributors);
        if (scope is null) {
            return null;
        }
        WorkspacePathScopeContribution? result = null;
        foreach (var contributor in contributors) {
            var contribution = contributor.ContributeWorkspacePaths(scope);
            if (contribution is null) {
                continue;
            }
            contribution.RequireScope(scope);
            if (result is not null) {
                throw new InvalidOperationException("An execution workspace has more than one path policy contribution.");
            }
            result = contribution;
        }
        RequireAvailable(scope, result);
        return result;
    }

    public static void RequireAvailable(WorkspaceScopeDescriptor? scope, WorkspacePathScopeContribution? contribution) {
        if (contribution is not null) {
            contribution.RequireScope(scope);
        } else if (scope?.Kind == WorkspaceScopeKind.Project) {
            throw new InvalidOperationException("This Project workspace requires its registered owner path policy contribution.");
        }
    }

    internal static ToolInvocationPolicyDecision? Evaluate(ToolInvocationPolicyContext context, string signature) {
        var scope = GetContextScope(context);
        RequireAvailable(scope, context.WorkspacePaths);
        var decision = context.WorkspacePaths?.RestrictInvocation(context, signature);
        if (decision is not null && decision.Kind is not (ToolInvocationDecisionKind.Deny or ToolInvocationDecisionKind.SkipExecution)) {
            throw new InvalidOperationException("A workspace path contribution may only deny, skip or defer to the remaining invocation policy.");
        }
        return decision;
    }
}
