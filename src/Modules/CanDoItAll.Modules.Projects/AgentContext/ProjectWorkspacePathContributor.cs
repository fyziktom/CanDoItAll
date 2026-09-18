using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectWorkspacePathContributor : IToolInvocationPolicyContextContributor {
    private static readonly ProjectWorkspaceScopePolicy Policy = new();

    public ToolInvocationPolicyContext Contribute(ToolInvocationPolicyContext context,
        WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState? auditScope) {
        ArgumentNullException.ThrowIfNull(context);
        var scope = WorkspacePathScopeContribution.GetContextScope(context);
        if (scope?.Kind != WorkspaceScopeKind.Project) {
            return context;
        }
        if (context.WorkspacePaths is not null) {
            throw new InvalidOperationException("An execution workspace already has a path policy contribution.");
        }
        return context with { WorkspacePaths = ContributeWorkspacePaths(scope) };
    }

    public WorkspacePathScopeContribution? ContributeWorkspacePaths(WorkspaceScopeDescriptor scope) {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.Kind == WorkspaceScopeKind.Project
            ? new(scope, Policy.EvaluateProjectScope, request => RestrictSearchRoots(scope, request))
            : null;
    }

    private static IReadOnlyList<string> RestrictSearchRoots(WorkspaceScopeDescriptor scope, WorkspaceSearchRootRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        if (request.IsWorkspaceRoot) {
            return ManagedProjectMediaPath.ResolveTextAssetRelativeRoots(scope.Key);
        }
        return ManagedProjectMediaPath.IsProjectMediaPath(request.RelativePath) &&
            !ManagedProjectMediaPath.IsForProject(request.RelativePath, scope.Key)
                ? []
                : [request.RelativePath];
    }
}
