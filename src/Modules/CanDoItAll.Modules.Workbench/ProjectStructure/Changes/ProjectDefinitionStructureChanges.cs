using CanDoItAll.Editing;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectDefinitionStructureChanges(ProjectStructureChangeFeed feed,
    ProjectStructureChangeSession session) : IProjectCommittedChangeSink {
    public void PublishCommitted(EditingScope scope, Guid projectId, EditingChangeKind kind) {
        if (scope.Kind != EditingScopeKind.Dataset || scope.Id == Guid.Empty || projectId == Guid.Empty) {
            throw new InvalidOperationException("Project structure contributions require a committed dataset Project identity.");
        }
        var change = kind switch {
            EditingChangeKind.Created => ProjectStructureChangeKind.Created,
            EditingChangeKind.EditableContentChanged => ProjectStructureChangeKind.Updated,
            EditingChangeKind.MetadataChanged => ProjectStructureChangeKind.RelationshipChanged,
            EditingChangeKind.Deleted => ProjectStructureChangeKind.Deleted,
            _ => (ProjectStructureChangeKind?)null
        };
        if (change is { } observed) {
            feed.Publish(scope.Id, projectId, session,
                [new(ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId), "Project definition", observed)]);
        }
    }
}
