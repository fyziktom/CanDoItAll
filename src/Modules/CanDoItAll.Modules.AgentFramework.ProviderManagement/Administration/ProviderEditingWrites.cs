using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class ProviderEditingWrites {
    private readonly AsyncLocal<Admission?> current = new();
    public bool IsActive => current.Value is not null;
    public EditingOperationIdentity? Origin => current.Value is { Active: true } value ? value.Operation : null;

    public IDisposable Enter(EditingTopic topic, EditingOperationIdentity operation, Func<CancellationToken, ValueTask> authorize) {
        var previous = current.Value;
        var admission = new Admission(topic, operation, authorize);
        current.Value = admission;
        return new Lease(() => {
            admission.Active = false;
            current.Value = previous;
        });
    }

    public async ValueTask RequireAsync(Guid databaseProfileId, Guid? resourceId, CancellationToken cancellationToken,
        EditingOwner owner = EditingOwner.ProviderProfile) {
        cancellationToken.ThrowIfCancellationRequested();
        if (current.Value is not { } admission) {
            return;
        }
        if (!admission.Active || admission.Topic.Scope.Kind != EditingScopeKind.Dataset ||
            admission.Topic.Scope.Id != databaseProfileId || admission.Topic.Owner != owner ||
            admission.Topic.ResourceId is { } id && id != resourceId) {
            throw new UnauthorizedAccessException("The original provider editor belongs to a different dataset, target or opening.");
        }
        await admission.Authorize(cancellationToken);
        if (!admission.Active || !ReferenceEquals(admission, current.Value)) {
            throw new UnauthorizedAccessException("The original provider authority changed during admission.");
        }
    }

    private sealed class Admission(EditingTopic topic, EditingOperationIdentity operation, Func<CancellationToken, ValueTask> authorize) {
        public EditingTopic Topic { get; } = topic;
        public EditingOperationIdentity Operation { get; } = operation;
        public Func<CancellationToken, ValueTask> Authorize { get; } = authorize;
        public bool Active { get; set; } = true;
    }

    private sealed class Lease(Action release) : IDisposable {
        private Action? releaseAction = release;
        public void Dispose() => Interlocked.Exchange(ref releaseAction, null)?.Invoke();
    }
}
