using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureNonTaskWritePolicy
{
    public static bool CanUseStructureMutationTools(AgentProjectStructureAccessSettings access)
    {
        ArgumentNullException.ThrowIfNull(access);
        return access.CanWrite || access.CanWriteNonTaskStructure;
    }

    public static bool CanUseTaskMutationTools(AgentProjectStructureAccessSettings access)
    {
        ArgumentNullException.ThrowIfNull(access);
        return access.CanWrite || access.CanWriteTasks;
    }

    public static bool RequiresFullStructureWrite(string toolName)
    {
        return string.Equals(
            toolName,
            AgentToolInvocationPolicyMetadata.ProjectStructureImport,
            StringComparison.OrdinalIgnoreCase);
    }

    public static void EnsureNodeCreateAllowed(
        bool requiresNonTaskGuard,
        ProjectObjectType objectType,
        string? objectSubtype)
    {
        if (requiresNonTaskGuard && IsTask(objectType, objectSubtype))
        {
            ThrowTaskWriteDenied();
        }
    }

    public static void EnsureNodeUpdateAllowed(
        bool requiresNonTaskGuard,
        ProjectStructureNodeSummary existingNode,
        ProjectObjectType? requestedObjectType,
        string? requestedObjectSubtype)
    {
        ArgumentNullException.ThrowIfNull(existingNode);
        if (!requiresNonTaskGuard)
        {
            return;
        }

        EnsureNodesAllowed(requiresNonTaskGuard, [existingNode]);
        var targetObjectType = requestedObjectType ?? existingNode.ObjectType;
        var targetObjectSubtype = requestedObjectSubtype is not null
            ? ProjectObjectSubtypePolicy.Normalize(targetObjectType, requestedObjectSubtype)
            : requestedObjectType.HasValue && requestedObjectType.Value != existingNode.ObjectType
                ? string.Empty
                : existingNode.ObjectSubtype;
        if (IsTask(targetObjectType, targetObjectSubtype))
        {
            ThrowTaskWriteDenied();
        }
    }

    public static void EnsureNodesAllowed(
        bool requiresNonTaskGuard,
        IEnumerable<ProjectStructureNodeSummary> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        if (requiresNonTaskGuard && nodes.Any(node => IsTask(node.ObjectType, node.ObjectSubtype)))
        {
            ThrowTaskWriteDenied();
        }
    }

    public static void EnsureImportAllowed(bool requiresNonTaskGuard, string? leafWorkItemSubtype)
    {
        EnsureNodeCreateAllowed(requiresNonTaskGuard, ProjectObjectType.WorkItem, leafWorkItemSubtype);
    }

    internal static bool IsTask(ProjectObjectType objectType, string? objectSubtype)
    {
        return objectType == ProjectObjectType.WorkItem &&
            string.Equals(
                ProjectObjectSubtypePolicy.Normalize(objectType, objectSubtype),
                ProjectObjectSubtypePolicy.Task,
                StringComparison.Ordinal);
    }

    private static void ThrowTaskWriteDenied()
    {
        throw new ProjectStructureAgentException(
            403,
            "ProjectTaskWriteDenied",
            "This agent may write non-task project structure, but it may not create, change, move, reclassify, or delete task nodes. Enable project-task or full project-structure write access.");
    }
}
