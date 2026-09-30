using CanDoItAll.Modules.Workspace.DataSources.Contracts;

namespace CanDoItAll.Workspace.DataSources.UI;

public sealed class DataSourceTransferSession(IDataSourcesOwner owner, DataSourceOperationLedger operations,
    ProfileSummary target, DataSourcesContext context) : IDisposable {
    private readonly DataSourceReadLane sourceReads = new();
    private readonly DataSourceReadLane previewReads = new();
    private readonly HashSet<TransferGroupKey> selected = [];
    private bool disposed;
    private long selectionRevision;
    public event Action? Changed;
    public ProfileSummary Target { get; } = target;
    public IReadOnlyList<TransferSource> Sources { get; private set; } = [];
    public IReadOnlyList<TransferPreview> Items { get; private set; } = [];
    public Guid? SourceId { get; private set; }
    public SchemaHealth? TargetHealth { get; private set; }
    public SchemaHealth? SourceHealth { get; private set; }
    public bool Loading { get; private set; }
    public bool ReplaceExisting { get; set; } = true;
    public string? Message { get; private set; }
    public DataSourceResult? Result { get; private set; }
    public bool SchemaCurrent => TargetHealth?.Status == SchemaStatus.Current && SourceHealth?.Status == SchemaStatus.Current;
    public bool CanTransfer => !disposed && !Loading && context == owner.Context && operations.CanDispatch && SchemaCurrent && SourceId.HasValue
        && SourceId != Target.Id && selected.Count > 0 && selected.All(key => Items.Any(item => item.Descriptor.Key == key && item.IsAvailable));
    public bool IsSelected(TransferGroupKey key) => selected.Contains(key);

    public async Task InitializeAsync(Guid? preferredSource) {
        using var read = sourceReads.Begin();
        Loading = true;
        Publish();
        try {
            var sources = await owner.ListSourcesAsync(Target.Id, read.Token);
            if (!read.IsCurrent) {
                return;
            }
            Sources = sources.Where(source => source.ProfileId != Target.Id).ToArray();
            var first = Sources.FirstOrDefault(source => source.ProfileId == preferredSource) ?? Sources.FirstOrDefault();
            await SelectSourceAsync(first?.ProfileId);
        } catch (Exception exception) {
            if (read.IsCurrent) {
                Message = DataSourcesSession.ReadError(exception);
            }
        } finally {
            if (read.IsCurrent) {
                Loading = false;
                Publish();
            }
        }
    }

    public async Task SelectSourceAsync(Guid? id) {
        if (disposed || id.HasValue && Sources.All(source => source.ProfileId != id)) {
            return;
        }
        SourceId = id;
        selectionRevision++;
        Items = [];
        selected.Clear();
        TargetHealth = null;
        SourceHealth = null;
        Result = null;
        Message = null;
        await RefreshAsync();
    }

    public async Task RefreshAsync() {
        if (disposed) {
            return;
        }
        using var read = previewReads.Begin();
        var revision = selectionRevision;
        var sourceId = SourceId;
        Loading = true;
        TargetHealth = null;
        SourceHealth = null;
        Publish();
        try {
            var targetHealth = await owner.ReadSchemaAsync(Target.Id, read.Token);
            var sourceHealth = sourceId.HasValue ? await owner.ReadSchemaAsync(sourceId.Value, read.Token) : null;
            if (!read.IsCurrent || revision != selectionRevision) {
                return;
            }
            TargetHealth = targetHealth.ProfileId == Target.Id ? targetHealth : null;
            SourceHealth = sourceHealth?.ProfileId == sourceId ? sourceHealth : null;
            if (!SchemaCurrent || sourceId is null) {
                Items = [];
                selected.Clear();
                Message = "Observe or apply the current schema in both databases before previewing transfer settings.";
                return;
            }
            var items = await owner.PreviewAsync(sourceId.Value, Target.Id, read.Token);
            if (read.IsCurrent && revision == selectionRevision) {
                Items = items.ToArray();
                selected.IntersectWith(items.Where(item => item.IsAvailable).Select(item => item.Descriptor.Key));
                Message = null;
            }
        } catch (Exception exception) {
            if (read.IsCurrent && revision == selectionRevision) {
                Items = [];
                selected.Clear();
                Message = DataSourcesSession.ReadError(exception);
            }
        } finally {
            if (read.IsCurrent && revision == selectionRevision) {
                Loading = false;
                Publish();
            }
        }
    }

    public void Select(TransferGroupKey key, bool value) {
        if (disposed || Loading || operations.IsBusy || !Items.Any(item => item.Descriptor.Key == key && item.IsAvailable)) {
            return;
        }
        if (value) {
            selected.Add(key);
        } else {
            selected.Remove(key);
        }
        Publish();
    }

    public async Task TransferAsync() {
        if (!CanTransfer || SourceId is not { } sourceId) {
            return;
        }
        var revision = selectionRevision;
        var request = new TransferRequest(context, sourceId, Target.Id, selected.ToArray(), ReplaceExisting);
        var result = await operations.RunAsync(context, DataSourceAction.Transfer, Target.Id, sourceId, () => owner.TransferAsync(request));
        if (disposed || revision != selectionRevision || result is null) {
            return;
        }
        Result = result;
        await RefreshAsync();
        Publish();
    }

    public async Task ApplySchemaAsync(bool source) {
        var health = source ? SourceHealth : TargetHealth;
        var profileId = source ? SourceId : Target.Id;
        if (disposed || Loading || context != owner.Context || !operations.CanDispatch || health?.CanApplySchema != true || profileId is not { } id) {
            return;
        }
        var revision = selectionRevision;
        var result = await operations.RunAsync(context, DataSourceAction.ApplySchema, id, null, () => owner.ExecuteAsync(new(context, id, DataSourceAction.ApplySchema)));
        if (!disposed && revision == selectionRevision && result is not null) {
            Result = result;
            await RefreshAsync();
        }
    }

    private void Publish() {
        if (!disposed) {
            Changed?.Invoke();
        }
    }
    public void Dispose() {
        disposed = true;
        sourceReads.Dispose();
        previewReads.Dispose();
    }
}
