using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;

namespace CanDoItAll.Modules.Projects;

public sealed record ProjectAssignmentSaveReceipt(Guid Id, ProjectAssignmentObservation Accepted);
public sealed record ProjectAssignmentDeleteReceipt(Guid Id, ProjectAssignmentObservation Original);

public sealed class ProjectAssignmentChanges(EditingChangeFeed feed, EditingScope scope, EditingOperationContext operations) {
    private readonly AsyncLocal<Func<CancellationToken, ValueTask>?> commitAdmission = new();
    public EditingScope Scope { get; } = scope;
    public IEditingChangeSubscription Subscribe(Guid projectId) => feed.Subscribe(new(Scope, EditingOwner.ProjectAssignmentRecords, projectId));
    public IDisposable Enter(EditingOperationIdentity operation) => operations.Enter(operation);

    public IDisposable EnterUiWrite(Func<CancellationToken, ValueTask> admission) {
        var previous = commitAdmission.Value;
        commitAdmission.Value = admission;
        return new AdmissionScope(() => commitAdmission.Value = previous);
    }

    public ValueTask DemandCommitAsync(CancellationToken cancellationToken) =>
        commitAdmission.Value?.Invoke(cancellationToken) ?? ValueTask.CompletedTask;

    public void Publish(Guid assignmentId, Guid projectId, EditingChangeKind kind = EditingChangeKind.EditableContentChanged) {
        var origin = operations.Current;
        feed.Publish(new(Scope, EditingOwner.ProjectAssignment, assignmentId), kind, origin?.EditorId, origin?.OperationId);
        feed.Publish(new(Scope, EditingOwner.ProjectAssignmentRecords, projectId), EditingChangeKind.EditableContentChanged,
            origin?.EditorId, origin?.OperationId);
    }

    private sealed class AdmissionScope(Action restore) : IDisposable {
        public void Dispose() => restore();
    }
}
