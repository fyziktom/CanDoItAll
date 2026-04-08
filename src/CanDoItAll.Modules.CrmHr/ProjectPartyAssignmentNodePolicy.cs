using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.CrmHr;

public sealed class ProjectPartyAssignmentNodePolicy(
    IProjectNodeScopeBridge projectNodeScopeBridge,
    IProjectNodeAssignmentPolicyBridge nodeAssignmentPolicyBridge)
{
    public async Task<(ProjectNodeScopeResolution? NodeScope, Error? Error)> ResolveScopeAsync(
        Guid projectId,
        string nodeKey,
        IReadOnlyCollection<ProjectPartyAssignmentRole> roles,
        bool allowUnresolvedNamedScope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roles);

        var needsCanonicalValidation = roles.Any(nodeAssignmentPolicyBridge.SupportsCanonicalNodeScope);
        if (!needsCanonicalValidation)
        {
            return (null, null);
        }

        if (string.IsNullOrWhiteSpace(nodeKey))
        {
            return roles.Any(nodeAssignmentPolicyBridge.RequiresCanonicalNodeScope)
                ? (null, Error.Validation("A node is required for this assignment.", "crmhr.project-assignment.node-required"))
                : (null, null);
        }

        var nodeScope = await projectNodeScopeBridge.ResolveAsync(
            projectId,
            new ProjectNodeReference(nodeKey),
            cancellationToken);
        if (!nodeScope.ExistsInProject)
        {
            if (!nodeScope.ExistsInOtherProject &&
                allowUnresolvedNamedScope &&
                roles.All(role => !nodeAssignmentPolicyBridge.RequiresCanonicalNodeScope(role)))
            {
                return (null, null);
            }

            return nodeScope.ExistsInOtherProject
                ? (null, Error.Validation("The selected node belongs to another project.", "crmhr.project-assignment.node-project-mismatch"))
                : (null, Error.Validation("The selected node was not found.", "crmhr.project-assignment.node-not-found"));
        }

        if (!nodeScope.IsCanonicalNode)
        {
            return (null, Error.Validation(
                "The selected node is projection-only and cannot own canonical assignments.",
                "crmhr.project-assignment.node-projection-not-allowed"));
        }

        return (nodeScope, null);
    }

    public Error? ValidateRole(ProjectPartyAssignmentRole role, ProjectNodeScopeResolution? nodeScope)
    {
        if (!nodeAssignmentPolicyBridge.SupportsCanonicalNodeScope(role) || nodeScope is null)
        {
            return null;
        }

        if (nodeScope.ObjectType is null)
        {
            return Error.Validation("The selected node was not found.", "crmhr.project-assignment.node-not-found");
        }

        var semantics = nodeAssignmentPolicyBridge.Resolve(nodeScope.ObjectType.Value, nodeScope.ObjectSubtype);
        return semantics.AllowedRoles.Contains(role)
            ? null
            : Error.Validation("The selected node does not allow this assignment role.", "crmhr.project-assignment.node-role-not-allowed");
    }
}
