using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public static class ProjectStructureCanonicalTaskMutationPolicy
{
    public const string ErrorCode = "CanonicalTaskMutationRequiresTypedPath";

    public static void EnsureGenericCreateAllowed(
        ProjectObjectType objectType,
        string? objectSubtype)
    {
        if (IsTask(objectType, objectSubtype))
        {
            ThrowTypedPathRequired();
        }
    }

    public static void EnsureGenericUpdateAllowed(
        ProjectStructureNodeSummary existingNode,
        ProjectObjectType? requestedObjectType,
        string? requestedObjectSubtype)
    {
        ArgumentNullException.ThrowIfNull(existingNode);
        EnsureGenericUpdateAllowed(
            existingNode.ObjectType,
            existingNode.ObjectSubtype,
            requestedObjectType,
            requestedObjectSubtype);
    }

    public static void EnsureGenericUpdateAllowed(
        ProjectObjectType existingObjectType,
        string? existingObjectSubtype,
        ProjectObjectType? requestedObjectType,
        string? requestedObjectSubtype)
    {
        if (IsTask(existingObjectType, existingObjectSubtype))
        {
            ThrowTypedPathRequired();
        }

        var targetObjectType = requestedObjectType ?? existingObjectType;
        var targetObjectSubtype = requestedObjectSubtype is not null
            ? ProjectObjectSubtypePolicy.Normalize(targetObjectType, requestedObjectSubtype)
            : requestedObjectType.HasValue && requestedObjectType.Value != existingObjectType
                ? string.Empty
                : existingObjectSubtype;
        if (IsTask(targetObjectType, targetObjectSubtype))
        {
            ThrowTypedPathRequired();
        }
    }

    public static void EnsureGenericMetadataUpdateAllowed(ProjectStructureNodeSummary existingNode)
    {
        ArgumentNullException.ThrowIfNull(existingNode);
        EnsureGenericMetadataUpdateAllowed(
            existingNode.ObjectType,
            existingNode.ObjectSubtype);
    }

    public static void EnsureGenericMetadataUpdateAllowed(
        ProjectObjectType existingObjectType,
        string? existingObjectSubtype)
    {
        if (IsTask(existingObjectType, existingObjectSubtype))
        {
            ThrowTypedPathRequired();
        }
    }

    public static void EnsureGenericResourceAttachmentAllowed(
        ProjectObjectType objectType,
        string? objectSubtype)
    {
        if (IsTask(objectType, objectSubtype))
        {
            ThrowTypedPathRequired();
        }
    }

    internal static bool IsTask(ProjectObjectType objectType, string? objectSubtype)
    {
        return objectType == ProjectObjectType.WorkItem &&
            string.Equals(
                ProjectObjectSubtypePolicy.Normalize(objectType, objectSubtype),
                ProjectObjectSubtypePolicy.Task,
                StringComparison.Ordinal);
    }

    private static void ThrowTypedPathRequired()
    {
        throw new ProjectStructureAgentException(
            409,
            ErrorCode,
            "Canonical task creation and task estimate or metadata changes must use the typed task create/update path so lifecycle, assignment, and authoritative resource pricing remain consistent.");
    }
}
