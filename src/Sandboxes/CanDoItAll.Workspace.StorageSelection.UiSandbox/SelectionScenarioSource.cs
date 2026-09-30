using CanDoItAll.Modules.Workspace.StorageSelection.Contracts;

namespace CanDoItAll.Workspace.StorageSelection.UiSandbox;

public enum SelectionScenario { Representative, Empty, Large, Failure, PartialFailure }

public sealed class SelectionScenarioSource : IStorageCatalogSelectionSource, IDisposable {
    public static readonly Guid AlphaId = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DisabledId = new("22222222-2222-2222-2222-222222222222");
    public static readonly Guid ReadOnlyId = new("33333333-3333-3333-3333-333333333333");
    public static readonly Guid MissingId = new("44444444-4444-4444-4444-444444444444");
    private readonly List<TaskCompletionSource> pending = [];
    private IReadOnlyList<StorageSelectionItem> rows = Representative(0);
    private bool disposed;
    private SelectionScenario scenario;
    private int scenarioReads;
    public StorageCatalogSelectionContext Context { get; private set; } = new(Guid.NewGuid(), 0);
    public bool IsCurrent => !disposed;
    public event Action? ContextChanged;
    public int Reads { get; private set; }
    public int PendingReads => pending.Count;
    public bool Hold { get; set; }
    public bool IgnoreCancellation { get; set; }

    public void Reset(SelectionScenario next) {
        ObjectDisposedException.ThrowIf(disposed, this);
        scenario = next;
        scenarioReads = 0;
        Context = Context with { Generation = Context.Generation + 1 };
        rows = next switch {
            SelectionScenario.Empty => [],
            SelectionScenario.Large => [.. Representative(Context.Generation), .. Enumerable.Range(1, 497).Select(index =>
                new StorageSelectionItem(new Guid(index, 1, 0, new byte[8]), $"Archive {index:000} with a long readable catalog name", "File system", "Local", $"/scenario/archive-{index}", index + 3, true, false, index % 7 == 0, "Unknown"))],
            _ => Representative(Context.Generation)
        };
        ContextChanged?.Invoke();
    }

    public async Task<IReadOnlyList<StorageSelectionItem>> ListAsync(CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(disposed, this);
        Reads++;
        scenarioReads++;
        var snapshot = rows;
        var fail = scenario == SelectionScenario.Failure || scenario == SelectionScenario.PartialFailure && scenarioReads > 1;
        if (Hold) {
            if (pending.Count >= 4) {
                throw new InvalidOperationException("The bounded scenario queue is full; release pending reads.");
            }
            var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Add(held);
            try {
                await held.Task.WaitAsync(IgnoreCancellation ? CancellationToken.None : cancellationToken);
            } finally {
                pending.Remove(held);
            }
        }
        if (fail) {
            throw new InvalidOperationException("Private scenario diagnostic must not appear in the UI.");
        }
        return snapshot;
    }

    public void Release() {
        if (pending.FirstOrDefault() is { } held) {
            pending.Remove(held);
            held.TrySetResult();
        }
    }

    public void Recover() {
        scenario = SelectionScenario.Representative;
        scenarioReads = 0;
    }

    private static IReadOnlyList<StorageSelectionItem> Representative(long generation) => [
        new(AlphaId, $"Customer documents · profile {generation}", "File system", "Local", "/scenario/documents", 0, true, true, false, "Healthy"),
        new(DisabledId, "Disabled archive", "FTP", "Remote", "ftp://example.test/archive", 1, false, false, false, "Unavailable"),
        new(ReadOnlyId, "Read-only library", "IPFS", "Remote", "https://example.test/ipfs", 2, true, false, true, "Unknown")
    ];

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        foreach (var held in pending.ToArray()) {
            held.TrySetCanceled();
        }
        pending.Clear();
        ContextChanged?.Invoke();
        ContextChanged = null;
    }
}
