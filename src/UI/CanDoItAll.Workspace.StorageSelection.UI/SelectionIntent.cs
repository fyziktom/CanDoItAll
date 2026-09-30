using CanDoItAll.Modules.Workspace.StorageSelection.Contracts;

namespace CanDoItAll.Workspace.StorageSelection.UI;

internal sealed class SelectionIntent : IDisposable {
    private readonly CancellationTokenSource cancellation;
    private bool disposed;
    public SelectionIntent(IStorageCatalogSelectionSource source, CancellationToken parentLifetime, IReadOnlyList<Guid> ids, bool allowAll, bool disabled) {
        Source = source;
        Context = source.Context;
        ParentLifetime = parentLifetime;
        Ids = Normalize(ids);
        AllowAll = allowAll;
        Disabled = disabled;
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(parentLifetime);
        Token = cancellation.Token;
    }

    public IStorageCatalogSelectionSource Source { get; }
    public StorageCatalogSelectionContext Context { get; }
    public CancellationToken ParentLifetime { get; }
    public CancellationToken Token { get; }
    public IReadOnlyList<Guid> Ids { get; }
    public bool AllowAll { get; }
    public bool Disabled { get; }
    public bool IsCurrent => !disposed && !Token.IsCancellationRequested && Source.IsCurrent && Source.Context == Context;
    public bool CanEdit => IsCurrent && !AllowAll && !Disabled;

    public bool Matches(CancellationToken parentLifetime, IReadOnlyList<Guid> ids, bool allowAll, bool disabled) =>
        ParentLifetime == parentLifetime && Source.Context == Context && AllowAll == allowAll && Disabled == disabled && Ids.SequenceEqual(Normalize(ids));

    public static IReadOnlyList<Guid> Normalize(IEnumerable<Guid> ids) => [.. ids.Where(id => id != Guid.Empty).Distinct().Order()];
    public static IReadOnlyList<StorageSelectionItem> NormalizeCatalogs(IEnumerable<StorageSelectionItem> catalogs) =>
        [.. catalogs.Where(row => row.Id != Guid.Empty).DistinctBy(row => row.Id).OrderBy(row => row.DisplayOrder).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)];

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
