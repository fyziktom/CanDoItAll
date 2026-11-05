using System.Collections.Immutable;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectWorkAssignmentStructureChanges(
    DbContextOptions<WorkbenchDbContext> options,
    CoordinatedDatabaseTransaction transactions,
    ProjectWriteAdmissionService admissions,
    ProjectStructureChangeFeed feed,
    ProjectStructureChangeSession session) {
    private const int MaximumNodes = 5_000;
    private readonly Guid profileId = admissions.DatabaseProfileId;

    public async Task<IReadOnlyList<ProjectWorkAssignmentNodeChange>> CaptureAsync(
        IReadOnlyCollection<(Guid ProjectId, string NodeKey)> nodes, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(nodes);
        if (nodes.Count > MaximumNodes || nodes.Any(node => node.ProjectId == Guid.Empty || string.IsNullOrWhiteSpace(node.NodeKey))) {
            throw new ArgumentException($"Work node change capture requires at most {MaximumNodes} complete node references.", nameof(nodes));
        }
        if (nodes.Count == 0) {
            return [];
        }
        var targets = nodes.Select(node => (node.ProjectId, NodeKey: node.NodeKey.Trim())).ToHashSet();
        var projects = targets.Select(node => node.ProjectId).Distinct().ToArray();
        var keys = targets.Select(node => node.NodeKey).Distinct().ToArray();
        await using var context = await transactions.CreateEnlistedAsync(options, static value => new WorkbenchDbContext(value), cancellationToken);
        var current = await context.Set<ProjectObjectRecord>().AsNoTracking()
            .Where(node => projects.Contains(node.ProjectId) && keys.Contains(node.NodeKey) &&
                node.ObjectType == ProjectObjectType.WorkItem && !node.IsSystemManaged)
            .Select(node => new ProjectWorkAssignmentNodeChange(profileId, node.ProjectId, node.NodeKey, node.Title))
            .Take(MaximumNodes + 1).ToArrayAsync(cancellationToken);
        if (current.Length > MaximumNodes) {
            throw new InvalidOperationException("Work node change capture exceeded its bounded projection.");
        }
        return current.Where(node => targets.Contains((node.ProjectId, node.NodeKey))).ToImmutableArray();
    }

    public void PublishCommitted(IReadOnlyList<ProjectWorkAssignmentNodeChange> nodes) {
        ArgumentNullException.ThrowIfNull(nodes);
        if (nodes.Count > MaximumNodes || nodes.Any(node => node.DatabaseProfileId != profileId)) {
            throw new InvalidOperationException("Committed Work node changes must belong to the current database profile and bounded batch.");
        }
        foreach (var project in nodes.GroupBy(node => node.ProjectId)) {
            feed.Publish(profileId, project.Key, session, project.Select(node =>
                new ProjectStructureChangedNode(node.NodeKey, node.Title, ProjectStructureChangeKind.Updated)).ToArray());
        }
    }
}
