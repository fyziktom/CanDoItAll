using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

internal static class ProjectStructureTestOrigin {
    internal static async Task<ProjectStructureGraphState> ReadGraphAsync(ProjectWorkbenchService workbench, Guid projectId)
        => ProjectStructureGraphStateGuard.Capture(await workbench.GetStructureAsync(projectId));

    internal static async Task<ProjectStructureNodeState> ReadStateAsync(ProjectWorkbenchService workbench, Guid projectId, string nodeId) {
        var graph = await ReadGraphAsync(workbench, projectId);
        return Assert.Single(graph.Nodes, node => node.State.NodeId == nodeId).State;
    }

    internal static async Task<ProjectStructureLink> ReadLinkAsync(ProjectWorkbenchService workbench, Guid projectId,
        string sourceId, string targetId, ProjectObjectLinkKind kind) {
        var origin = await workbench.GetStructureAsync(projectId);
        return Assert.Single(origin.Links, link => link.SourceId == sourceId && link.TargetId == targetId && link.Kind == kind);
    }

    internal static async Task<ProjectStructureAgentContext> ReadOwnerAsync(ProjectWorkbenchService workbench, Guid projectId) {
        var graph = await ReadGraphAsync(workbench, projectId);
        return new("test", "Test", "", "", "", "") {
            ExpectedProjectAdmission = graph.Project,
            ExpectedGraph = graph
        };
    }

    internal static async Task<ProjectStructureAgentContext> ReadTransferOwnerAsync(ProjectWorkbenchService workbench, Guid sourceId, Guid targetId) {
        var source = await ReadOwnerAsync(workbench, sourceId);
        var target = await ReadGraphAsync(workbench, targetId);
        return source with { ExpectedProjectAdmissions = [source.ExpectedProjectAdmission!, target.Project] };
    }
}
