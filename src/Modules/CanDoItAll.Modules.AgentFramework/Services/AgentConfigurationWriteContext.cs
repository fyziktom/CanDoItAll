using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentConfigurationWriteContext {
    private readonly AsyncLocal<Admission?> current = new();
    public EditingOperationIdentity? Origin => current.Value is { Active: true } value ? value.Operation : null;

    public IDisposable Enter(EditingScope scope, EditingOperationIdentity operation, Func<CancellationToken, ValueTask> authorize) {
        var previous = current.Value;
        var admission = new Admission(scope, operation, authorize);
        current.Value = admission;
        return new Lease(() => {
            admission.Active = false;
            current.Value = previous;
        });
    }

    public async ValueTask RequireAsync(Guid databaseProfileId, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (current.Value is not { } admission) {
            return;
        }
        if (!admission.Active || admission.Scope.Kind != EditingScopeKind.Dataset || admission.Scope.Id != databaseProfileId) {
            throw new UnauthorizedAccessException("The original agent configuration authority belongs to a different dataset or opening.");
        }
        await admission.Authorize(cancellationToken);
        if (!admission.Active || !ReferenceEquals(admission, current.Value)) {
            throw new UnauthorizedAccessException("The original agent configuration authority changed during admission.");
        }
    }

    private sealed class Admission(EditingScope scope, EditingOperationIdentity operation, Func<CancellationToken, ValueTask> authorize) {
        public EditingScope Scope { get; } = scope;
        public EditingOperationIdentity Operation { get; } = operation;
        public Func<CancellationToken, ValueTask> Authorize { get; } = authorize;
        public bool Active { get; set; } = true;
    }

    private sealed class Lease(Action release) : IDisposable {
        private Action? releaseAction = release;
        public void Dispose() => Interlocked.Exchange(ref releaseAction, null)?.Invoke();
    }
}
