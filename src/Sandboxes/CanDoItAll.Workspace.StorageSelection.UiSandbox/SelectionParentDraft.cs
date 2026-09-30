namespace CanDoItAll.Workspace.StorageSelection.UiSandbox;

public sealed class SelectionParentDraft : IDisposable {
    private readonly CancellationTokenSource lifetime = new();
    private bool disposed;
    public SelectionParentDraft(string name, IReadOnlyList<Guid> ids) {
        Name = name;
        Ids = ids;
        Token = lifetime.Token;
    }
    public string Name { get; }
    public IReadOnlyList<Guid> Ids { get; set; }
    public CancellationToken Token { get; }
    public bool AllowAll { get; set; }
    public bool Disabled { get; set; }
    public int Applies { get; set; }
    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
