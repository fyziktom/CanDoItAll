using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectChanges(EditingChangeFeed feed, EditingScope scope, EditingOperationContext operations,
    IEnumerable<IProjectCommittedChangeSink> contributions) {
    private readonly AsyncLocal<Func<CancellationToken, ValueTask>?> commitAdmission = new();
    public EditingScope Scope { get; } = scope;
    public IDisposable Enter(EditingOperationIdentity identity) => operations.Enter(identity);

    public IDisposable EnterUiWrite(Func<CancellationToken, ValueTask> admission) {
        var previous = commitAdmission.Value;
        commitAdmission.Value = admission;
        return new AdmissionScope(() => commitAdmission.Value = previous);
    }

    public ValueTask DemandCommitAsync(CancellationToken cancellationToken) =>
        commitAdmission.Value?.Invoke(cancellationToken) ?? ValueTask.CompletedTask;

    public void Publish(Guid projectId, EditingChangeKind kind, Guid? operationId = null) {
        var origin = operations.Current;
        feed.Publish(new(Scope, EditingOwner.Project, projectId), kind, origin?.EditorId, operationId ?? origin?.OperationId);
        foreach (var contribution in contributions) {
            contribution.PublishCommitted(Scope, projectId, kind);
        }
    }

    private sealed class AdmissionScope(Action restore) : IDisposable {
        public void Dispose() => restore();
    }
}
