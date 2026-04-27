using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectNodeAssignmentPolicyBridge : IProjectNodeAssignmentPolicyBridge
{
    public bool SupportsCanonicalNodeScope(ProjectPartyAssignmentRole role)
    {
        return ProjectNodeKindRegistry.SupportsCanonicalNodeScope(role);
    }

    public bool RequiresCanonicalNodeScope(ProjectPartyAssignmentRole role)
    {
        return ProjectNodeKindRegistry.RequiresCanonicalNodeScope(role);
    }

    public ProjectNodeAssignmentSemantics Resolve(ProjectObjectType objectType, string objectSubtype)
    {
        return new ProjectNodeAssignmentSemantics(
            ProjectNodeKindRegistry.ResolveAllowedAssignmentRoles(objectType, objectSubtype),
            ProjectNodeKindRegistry.ResolveReplacementRoles(objectType, objectSubtype),
            ProjectNodeKindRegistry.ResolvePreferredRole(objectType, objectSubtype));
    }
}
