using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;

namespace CanDoItAll.Modules.Prompts;

public sealed class PromptGalleryChanges(IEditingChangeObserver observer, IEditingChangePublisher publisher,
    EditingScope scope, EditingOperationContext operations) {
    private readonly AsyncLocal<Func<CancellationToken, ValueTask>?> commitAdmission = new();
    public EditingScope Scope { get; } = scope;
    internal EditingOperationIdentity? CurrentOperation => operations.Current;

    public IEditingChangeSubscription Subscribe(Guid? promptId = null)
        => observer.Subscribe(new(Scope, EditingOwner.PromptGallery, promptId));

    public IDisposable Enter(EditingOperationIdentity? operation) => operations.Enter(operation);

    public IDisposable EnterUiWrite(Func<CancellationToken, ValueTask> admission) {
        var previous = commitAdmission.Value;
        commitAdmission.Value = admission;
        return new AdmissionScope(() => commitAdmission.Value = previous);
    }

    internal ValueTask DemandCommitAsync(CancellationToken cancellationToken)
        => commitAdmission.Value?.Invoke(cancellationToken) ?? ValueTask.CompletedTask;

    private sealed class AdmissionScope(Action restore) : IDisposable {
        public void Dispose() => restore();
    }

    public void Publish(Guid promptId, EditingChangeKind kind) {
        var origin = operations.Current;
        publisher.Publish(new(Scope, EditingOwner.PromptGallery, promptId), kind, origin?.EditorId, origin?.OperationId);
    }
}
