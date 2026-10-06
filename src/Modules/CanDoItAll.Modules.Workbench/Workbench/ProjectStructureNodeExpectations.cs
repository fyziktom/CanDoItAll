using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureNodeExpectations {
    public static void EnsureCurrent(IReadOnlyCollection<ProjectStructureNode>? expectedNodes, IReadOnlyCollection<ProjectObjectRecord> currentNodes) {
        if (expectedNodes is null) {
            return;
        }
        foreach (var expected in expectedNodes.Where(node => !node.IsSystemManaged)) {
            var current = currentNodes.FirstOrDefault(node => node.NodeKey == expected.Id);
            if (current is null || current.IsSystemManaged || current.Id != expected.RecordId ||
                current.ObjectType != expected.ObjectType || current.ObjectSubtype != expected.ObjectSubtype || current.ParentNodeKey != expected.ParentId) {
                throw new ProjectStructureEditConflictException();
            }
        }
    }

    public static async Task ReadAndEnsureCurrentAsync(WorkbenchDbContext context, Guid projectId,
        IReadOnlyCollection<ProjectStructureNode>? expectedNodes, CancellationToken cancellationToken) {
        if (expectedNodes is not { Count: > 0 }) {
            return;
        }
        var keys = expectedNodes.Where(node => !node.IsSystemManaged).Select(node => node.Id).ToArray();
        EnsureCurrent(expectedNodes, await context.Set<ProjectObjectRecord>()
            .Where(node => node.ProjectId == projectId && keys.Contains(node.NodeKey)).ToListAsync(cancellationToken));
    }
}
