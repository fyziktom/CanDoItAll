using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureProcessParentNodePolicy
{
    public static string? NormalizeCreateParentNodeKey(
        ProjectStructureProcessNodeContextDescriptor? context,
        string? requestedParentNodeKey)
    {
        if (context is null || string.IsNullOrWhiteSpace(requestedParentNodeKey))
        {
            return requestedParentNodeKey;
        }

        var requested = requestedParentNodeKey.Trim();
        var current = context.CurrentProcessRunNodeId.Trim();
        var target = context.PreferredWritebackNodeId;
        if (string.IsNullOrWhiteSpace(current) ||
            string.IsNullOrWhiteSpace(target) ||
            string.Equals(requested, target, StringComparison.Ordinal) ||
            !string.Equals(requested, current, StringComparison.Ordinal))
        {
            return requestedParentNodeKey;
        }

        return target;
    }
}
