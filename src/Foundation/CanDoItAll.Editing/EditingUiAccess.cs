namespace CanDoItAll.Editing;

public sealed class EditingUiGrant : IDisposable {
    private readonly CancellationTokenSource lifetime = new();
    private readonly CancellationToken lifetimeToken;
    private readonly object sync = new();
    private bool disposed;
    public event Action? Disposed;
    public EditingTopic Topic { get; }
    public string PrincipalStamp { get; }
    public string Actor { get; }
    public CancellationToken Lifetime => lifetimeToken;
    public bool IsDisposed => Volatile.Read(ref disposed);

    public EditingUiGrant(EditingTopic topic, string principalStamp, string actor) {
        Topic = topic;
        PrincipalStamp = principalStamp;
        Actor = actor;
        lifetimeToken = lifetime.Token;
    }

    public void Revoke() {
        lock (sync) {
            if (!disposed) {
                lifetime.Cancel();
            }
        }
    }

    public void Dispose() {
        lock (sync) {
            if (disposed) {
                return;
            }
            disposed = true;
            lifetime.Cancel();
            lifetime.Dispose();
        }
        Disposed?.Invoke();
    }
}

public interface IEditingUiAccess {
    Task<EditingUiGrant> OpenAsync(EditingTopic topic, CancellationToken cancellationToken);
    ValueTask ValidateAsync(EditingUiGrant grant, bool write, CancellationToken cancellationToken);
}
