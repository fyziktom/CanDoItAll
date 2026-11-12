using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessAuthoringChanges(EditingChangeFeed feed, ICanonicalRuntimeDatabase database,
    ProcessAuthoringAdmissionPolicy admissions) : IProcessAuthoringCommitObserver {
    public static readonly Guid GlobalResourceId = new("f0d6295d-2b8d-4801-b23b-69924feb0410");

    public void Committed(ProcessAuthoringCommit command) {
        var scope = new EditingScope(EditingScopeKind.Dataset, command.Address.DatabaseProfileId, database.Generation);
        var resource = command.Address.ProjectId == Guid.Empty ? GlobalResourceId : command.Address.ProjectId;
        var kind = command.Lifecycle switch {
            ProcessAuthoringLifecycle.Deleted => EditingChangeKind.Deleted,
            ProcessAuthoringLifecycle.Archived or ProcessAuthoringLifecycle.Published => EditingChangeKind.LifecycleChanged,
            _ => EditingChangeKind.EditableContentChanged
        };
        feed.Publish(new(scope, EditingOwner.ProcessAuthoring, resource), kind, admissions.UiAuthority?.EditorId, command.OperationId);
    }
}
