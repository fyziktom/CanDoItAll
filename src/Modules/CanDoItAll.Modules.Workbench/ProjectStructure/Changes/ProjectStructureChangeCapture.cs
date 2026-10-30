using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureChangeCapture : IDisposable {
    private readonly WorkbenchDbContext context;
    private readonly ProjectStructureChangeFeed feed;
    private readonly ProjectStructureChangeSession session;
    private readonly Guid profileId;
    private readonly Dictionary<(Guid ProjectId, string NodeId), ProjectStructureChangedNode> accepted = new();
    private readonly Dictionary<(Guid ProjectId, string NodeId), ProjectStructureChangedNode> pending = new();

    public ProjectStructureChangeCapture(WorkbenchDbContext context, ProjectStructureChangeFeed feed,
        ProjectStructureChangeSession session, Guid profileId) {
        this.context = context;
        this.feed = feed;
        this.session = session;
        this.profileId = profileId;
        context.SavingChanges += Saving;
        context.SavedChanges += Saved;
        context.SaveChangesFailed += Failed;
    }

    public void Publish() {
        foreach (var project in accepted.GroupBy(item => item.Key.ProjectId)) {
            feed.Publish(profileId, project.Key, session, project.Select(item => item.Value).ToArray());
        }
        accepted.Clear();
    }

    private void Saving(object? sender, SavingChangesEventArgs args) {
        pending.Clear();
        context.ChangeTracker.DetectChanges();
        foreach (var entry in context.ChangeTracker.Entries().Where(entry =>
                entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)) {
            if (entry.Entity is ProjectObjectRecord node) {
                var kind = entry.State switch {
                    EntityState.Added => ProjectStructureChangeKind.Created,
                    EntityState.Deleted => ProjectStructureChangeKind.Deleted,
                    _ => ProjectStructureChangeKind.Updated
                };
                pending[(node.ProjectId, node.NodeKey)] = new(node.NodeKey, node.Title, kind);
                if (entry.State == EntityState.Modified && entry.Property(nameof(ProjectObjectRecord.ProjectId)).IsModified) {
                    var oldProject = (Guid)entry.Property(nameof(ProjectObjectRecord.ProjectId)).OriginalValue!;
                    pending[(oldProject, node.NodeKey)] = new(node.NodeKey, node.Title, ProjectStructureChangeKind.Deleted);
                }
            } else if (entry.Entity is ProjectObjectLinkRecord link) {
                AddRelationship(link.ProjectId, link.SourceNodeKey);
                AddRelationship(link.ProjectId, link.TargetNodeKey);
            } else if (entry.Entity is ProjectStructureProjectionLayoutRecord layout) {
                pending.TryAdd((layout.ProjectId, layout.NodeKey), new(layout.NodeKey, layout.NodeKey, ProjectStructureChangeKind.Updated));
            }
        }
    }

    private void AddRelationship(Guid projectId, string nodeId) {
        pending.TryAdd((projectId, nodeId), new(nodeId, nodeId, ProjectStructureChangeKind.RelationshipChanged));
    }

    private void Saved(object? sender, SavedChangesEventArgs args) {
        foreach (var (key, node) in pending) {
            if (node.Kind != ProjectStructureChangeKind.RelationshipChanged || !accepted.ContainsKey(key)) {
                accepted[key] = node;
            }
        }
        pending.Clear();
    }

    private void Failed(object? sender, SaveChangesFailedEventArgs args) => pending.Clear();

    public void Dispose() {
        context.SavingChanges -= Saving;
        context.SavedChanges -= Saved;
        context.SaveChangesFailed -= Failed;
        accepted.Clear();
        pending.Clear();
    }
}
