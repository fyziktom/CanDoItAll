using System.Collections.Immutable;
using System.ComponentModel;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

[Description("An editable node's original occurrence, content and parent relationship within a mutation graph.")]
public sealed record ProjectStructureGraphNodeState(
    [property: Description("Original parent node key, or null for an unparented node.")] string? ParentId,
    [property: Description("Original persisted node occurrence and content fingerprint.")] ProjectStructureNodeState State);

[Description("Complete editable graph from a canonical read, used to reject stale graph mutations.")]
public sealed record ProjectStructureGraphState(
    [property: Description("Project lifetime that owned the graph when it was read.")] ProjectWriteAdmission Project,
    [property: Description("Original editable nodes and their parent relationships. Return the complete collection unchanged.")] ImmutableArray<ProjectStructureGraphNodeState> Nodes,
    [property: Description("Original user-authored links. Return the complete collection unchanged.")] ImmutableArray<ProjectStructureLink> Links);

internal static class ProjectStructureGraphStateGuard {
    internal static ProjectStructureGraphState Capture(ProjectStructureSurface surface) {
        var admission = surface.ExpectedProjectAdmission ?? throw new ProjectStructureEditConflictException();
        return new(admission,
            surface.Nodes.Where(node => !node.IsSystemManaged)
                .Select(node => new ProjectStructureGraphNodeState(node.ParentId,
                    ProjectStructureNodeStateGuard.Capture(node, admission) ?? throw new ProjectStructureEditConflictException())).ToImmutableArray(),
            surface.Links.Where(link => link.IsUserAuthored).ToImmutableArray());
    }

    internal static ProjectStructureGraphState CaptureForCreatedNodeCleanup(ProjectStructureSurface surface, ProjectStructureNodeState? createdState) {
        var graph = Capture(surface);
        if (createdState is null || createdState.Project != graph.Project ||
            !graph.Nodes.Any(node => node.State.NodeId == createdState.NodeId && node.State.RecordId == createdState.RecordId)) {
            throw new ProjectStructureEditConflictException();
        }
        return graph;
    }

    internal static ProjectStructureAgentContext Require(ProjectStructureAgentContext? owner, Guid projectId) {
        var graph = owner?.ExpectedGraph;
        if (graph is null || graph.Project is null || graph.Project.ProjectId != projectId || graph.Nodes.IsDefault || graph.Links.IsDefault ||
            graph.Nodes.Any(node => node is null || node.State is null) || graph.Links.Any(link => link is null) ||
            owner!.ExpectedProjectAdmission is { } original && original != graph.Project ||
            graph.Nodes.Select(node => node.State.NodeId).Distinct(StringComparer.Ordinal).Count() != graph.Nodes.Length ||
            graph.Nodes.Any(node => node.State.Project != graph.Project)) {
            throw new ProjectStructureEditConflictException();
        }
        return owner! with { ExpectedProjectAdmission = graph.Project };
    }

    internal static async Task EnsureAsync(WorkbenchDbContext context, ProjectStructureAgentContext? owner, Guid projectId,
        IReadOnlyCollection<string> roots, IReadOnlyCollection<string>? participants, bool descendants,
        IReadOnlyCollection<ProjectObjectRecord> nodes, CancellationToken cancellationToken) {
        Require(owner, projectId);
        await ProjectNodeBindingStorage.LoadAsync(context, nodes, cancellationToken);
        var links = await context.Set<ProjectObjectLinkRecord>().Where(link => link.ProjectId == projectId && !link.IsSystemManaged)
            .ToArrayAsync(cancellationToken);
        EnsureCurrent(owner, projectId, roots, participants, descendants, nodes, links);
    }

    internal static void EnsureCurrent(ProjectStructureAgentContext? owner, Guid projectId,
        IReadOnlyCollection<string> roots, IReadOnlyCollection<string>? participants, bool descendants,
        IReadOnlyCollection<ProjectObjectRecord> currentRecords, IReadOnlyCollection<ProjectObjectLinkRecord> links) {
        var expected = Require(owner, projectId).ExpectedGraph!;
        var nodes = currentRecords.Where(node => !node.IsSystemManaged).ToArray();
        var projectRoot = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId);
        var originalKeys = ResolveKeys(expected.Nodes.Select(node => (node.State.NodeId, node.ParentId)), roots, descendants, projectRoot);
        var currentKeys = ResolveKeys(nodes.Select(node => (node.NodeKey, node.ParentNodeKey)), roots, descendants, projectRoot);
        if (!originalKeys.SetEquals(currentKeys)) {
            throw new ProjectStructureEditConflictException();
        }
        if (participants is not null) {
            originalKeys.UnionWith(participants.Where(key => expected.Nodes.Any(node => node.State.NodeId == key)));
            currentKeys.UnionWith(participants.Where(key => nodes.Any(node => node.NodeKey == key)));
        }
        if (!originalKeys.SetEquals(currentKeys)) {
            throw new ProjectStructureEditConflictException();
        }
        var currentNodes = nodes.Where(node => currentKeys.Contains(node.NodeKey)).ToArray();
        var originalNodes = expected.Nodes.Where(node => originalKeys.Contains(node.State.NodeId)).ToArray();
        if (currentNodes.Length != originalNodes.Length || roots.Any(root => !originalKeys.Contains(root))) {
            throw new ProjectStructureEditConflictException();
        }
        foreach (var original in originalNodes) {
            ProjectStructureNodeStateGuard.EnsureCurrent(original.State,
                currentNodes.Single(node => node.NodeKey == original.State.NodeId));
        }
        var currentLinks = links.Where(link => !link.IsSystemManaged && (currentKeys.Contains(link.SourceNodeKey) || currentKeys.Contains(link.TargetNodeKey))).Select(link => new ProjectStructureLink(link.SourceNodeKey, link.TargetNodeKey, link.LinkKind, true, link.Id));
        var originalLinks = expected.Links.Where(link => originalKeys.Contains(link.SourceId) || originalKeys.Contains(link.TargetId));
        if (!currentLinks.OrderBy(link => link.RecordId).SequenceEqual(originalLinks.OrderBy(link => link.RecordId))) {
            throw new ProjectStructureEditConflictException();
        }
    }

    internal static ProjectStructureGraphState AfterOwnDeletion(ProjectStructureGraphState original, string root) {
        var removed = ResolveKeys(original.Nodes.Select(node => (node.State.NodeId, node.ParentId)), [root], true);
        return original with {
            Nodes = original.Nodes.Where(node => !removed.Contains(node.State.NodeId)).ToImmutableArray(),
            Links = original.Links.Where(link => !removed.Contains(link.SourceId) && !removed.Contains(link.TargetId)).ToImmutableArray()
        };
    }

    private static HashSet<string> ResolveKeys(IEnumerable<(string Id, string? Parent)> nodes, IReadOnlyCollection<string> roots, bool descendants, string? projectRoot = null) {
        var entries = nodes.ToArray();
        var keys = entries.Where(node => roots.Contains(node.Id)).Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        if (projectRoot is not null && roots.Contains(projectRoot)) {
            keys.Add(projectRoot);
        }
        if (!descendants) {
            return keys;
        }
        var children = entries.Where(node => node.Parent is not null).ToLookup(node => node.Parent!, StringComparer.Ordinal);
        var pending = new Queue<string>(keys);
        while (pending.TryDequeue(out var parent)) {
            foreach (var child in children[parent]) {
                if (keys.Add(child.Id)) {
                    pending.Enqueue(child.Id);
                }
            }
        }
        return keys;
    }
}
