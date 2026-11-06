using CanDoItAll.Editing;

namespace CanDoItAll.Modules.Projects;

public interface IProjectCommittedChangeSink {
    void PublishCommitted(EditingScope scope, Guid projectId, EditingChangeKind kind);
}
