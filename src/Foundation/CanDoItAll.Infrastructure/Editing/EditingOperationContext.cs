namespace CanDoItAll.Infrastructure.Editing;

public sealed record EditingOperationIdentity(Guid EditorId, Guid OperationId);

public sealed class EditingOperationContext {
    private readonly AsyncLocal<EditingOperationIdentity?> current = new();

    public EditingOperationIdentity? Current => current.Value;

    public IDisposable Enter(EditingOperationIdentity? operation) {
        var previous = current.Value;
        current.Value = operation;
        return new OperationLease(() => current.Value = previous);
    }

    private sealed class OperationLease(Action restore) : IDisposable {
        private Action? release = restore;

        public void Dispose() => Interlocked.Exchange(ref release, null)?.Invoke();
    }
}
