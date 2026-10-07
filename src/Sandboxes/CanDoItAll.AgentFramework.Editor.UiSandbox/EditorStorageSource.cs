using CanDoItAll.Modules.Workspace.StorageSelection.Contracts;

namespace CanDoItAll.AgentFramework.Editor.UiSandbox;

public sealed class EditorStorageSource : IStorageCatalogSelectionSource {
    public static readonly Guid DocumentsId = new("11111111-1100-1100-1100-111111111111");
    public static readonly Guid DisabledId = new("22222222-2200-2200-2200-222222222222");
    public StorageCatalogSelectionContext Context { get; } = new(Guid.NewGuid(), 0);
    public bool IsCurrent => true;
    public event Action? ContextChanged { add { } remove { } }
    public Task<IReadOnlyList<StorageSelectionItem>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<StorageSelectionItem>>([
            new(DocumentsId, "Documents · 東京", "Fixture", "In memory", "fixture/documents", 0, true, true, false, "Healthy"),
            new(DisabledId, "Disabled archive", "Fixture", "In memory", "fixture/archive", 1, false, false, true, "Unavailable")
        ]);
}
