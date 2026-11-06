using CanDoItAll.Editing;

namespace CanDoItAll.Modules.Prompts;

public sealed class PromptGalleryUiGrant(EditingScope scope, string principalStamp) : IDisposable {
    private readonly CancellationTokenSource lifetime = new();
    private readonly object sync = new();
    private bool disposed;
    public event Action? Disposed;
    public EditingScope Scope { get; } = scope;
    public string PrincipalStamp { get; } = principalStamp;
    public CancellationToken Lifetime => lifetime.Token;
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

public interface IPromptGalleryUiAccess {
    Task<PromptGalleryUiGrant> OpenAsync(EditingScope scope, CancellationToken cancellationToken);
    ValueTask ValidateAsync(PromptGalleryUiGrant grant, bool write, CancellationToken cancellationToken);
}

internal sealed class UnavailablePromptGalleryUiAccess : IPromptGalleryUiAccess {
    public Task<PromptGalleryUiGrant> OpenAsync(EditingScope scope, CancellationToken cancellationToken)
        => Task.FromException<PromptGalleryUiGrant>(new UnauthorizedAccessException("Prompt Gallery UI access is unavailable in this host."));
    public ValueTask ValidateAsync(PromptGalleryUiGrant grant, bool write, CancellationToken cancellationToken)
        => ValueTask.FromException(new UnauthorizedAccessException("Prompt Gallery UI access is unavailable in this host."));
}
