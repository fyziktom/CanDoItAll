using CanDoItAll.Modules.Workspace.DataSources.Contracts;

namespace CanDoItAll.Workspace.DataSources.UiSandbox;

public enum DataSourcesScenario { Ready, Empty, Locked, Unavailable, Loading, SchemaNeeded, PendingRestart, PartialTransfer, UnknownWrite, RefreshFailure, DelayedWrite }

public sealed class DataSourcesScenarioOwner(DataSourcesScenario scenario = DataSourcesScenario.Ready) : IDataSourcesOwner {
    public static readonly Guid ProfileA = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid ProfileB = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly TransferGroupKey PreferenceGroup = new("workspace-default-provider");
    public static readonly TransferGroupKey AgentsGroup = new("ai-agents");
    private readonly Dictionary<Guid, ProfileValues> profiles = scenario == DataSourcesScenario.Empty ? [] : new() {
        [ProfileA] = Values(ProfileA, "Workspace A"), [ProfileB] = Values(ProfileB, "Workspace B")
    };
    private readonly HashSet<Guid> schemaApplied = [];
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Guid? pending = scenario == DataSourcesScenario.PendingRestart ? ProfileB : null;
    public DataSourcesContext Context { get; set; } = new(ProfileA, 0);
    public List<DataSourceAction> Commands { get; } = [];
    public ProfileValues? LastSave { get; private set; }
    public TransferRequest? LastTransfer { get; private set; }
    public bool ReceivedPassword { get; private set; }
    public bool FailObservation { get; set; }
    public Func<Guid, Task<ProfileEditor>>? EditorRead { get; set; }
    public Func<Guid, Task<SchemaHealth>>? SchemaRead { get; set; }
    public Func<Task>? BeforeListRead { get; set; }
    public Func<Task>? BeforeWrite { get; set; }
    public void Release() => release.TrySetResult();

    public async Task<RuntimeSelection> ReadRuntimeAsync(CancellationToken cancellationToken) {
        await ObserveAsync();
        return new(ProfileA, "Workspace A", DataSourceProvider.PostgreSql, "Simulated PostgreSQL A", "Simulated root A",
            scenario == DataSourcesScenario.Locked ? DataSourceResolution.ExplicitOverride : DataSourceResolution.PersistedActiveProfile,
            scenario == DataSourcesScenario.Locked, pending, pending.HasValue ? "Workspace B" : string.Empty, "Simulated PostgreSQL B");
    }
    public async Task<IReadOnlyList<ProfileSummary>> ListAsync(CancellationToken cancellationToken) {
        if (BeforeListRead is not null) {
            await BeforeListRead();
        }
        await ObserveAsync();
        return profiles.Values.Select(value => new ProfileSummary(value.Id!.Value, value.DisplayName,
            DataSourceProvider.PostgreSql, "Simulated saved connection", value.Id == ProfileA,
            scenario == DataSourcesScenario.Locked, pending == value.Id, DateTimeOffset.UnixEpoch, null)).ToArray();
    }
    public async Task<ProfileEditor> ReadEditorAsync(Guid id, CancellationToken cancellationToken) {
        if (EditorRead is not null) {
            return await EditorRead(id);
        }
        await ObserveAsync();
        return profiles.TryGetValue(id, out var values) ? new(values, DataSourceProvider.PostgreSql,
            scenario == DataSourcesScenario.Locked, true) : throw new DataSourcesException(DataSourceFailure.Missing);
    }
    public async Task<SchemaHealth> ReadSchemaAsync(Guid id, CancellationToken cancellationToken) {
        if (SchemaRead is not null) {
            return await SchemaRead(id);
        }
        await ObserveAsync();
        var needed = scenario == DataSourcesScenario.SchemaNeeded && !schemaApplied.Contains(id);
        return new(id, needed ? SchemaStatus.NeedsMigration : SchemaStatus.Current,
            needed ? "Simulated missing schema." : "Database schema is current.", [], [], scenario != DataSourcesScenario.Locked);
    }
    public async Task<IReadOnlyList<TransferSource>> ListSourcesAsync(Guid targetId, CancellationToken cancellationToken) {
        await ObserveAsync();
        return profiles.Values.Where(value => value.Id != targetId).Select(value => new TransferSource(value.Id!.Value, value.DisplayName, "Simulated source")).ToArray();
    }
    public async Task<IReadOnlyList<TransferPreview>> PreviewAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken) {
        await ObserveAsync();
        return [new(new(PreferenceGroup, "Workspace default provider", "Copies a simulated preference.", false), true, "One record available.", null, 1, 0),
            new(new(AgentsGroup, "AI agents", "Copies a simulated file catalog.", false), true, "One record available.", null, 1, 0)];
    }
    public async Task<DataSourceResult> SaveAsync(DataSourcesContext context, ProfileValues values, ProfilePasswordIntent password) {
        ReceivedPassword = !string.IsNullOrEmpty(password.Take());
        LastSave = values;
        await WriteAsync(DataSourceAction.Save);
        var id = values.Id ?? Guid.NewGuid();
        profiles[id] = values with { Id = id };
        return new(id, DataSourceOutcome.Confirmed, "Data source saved.");
    }
    public async Task<DataSourceResult> ExecuteAsync(DataSourceCommand command) {
        await WriteAsync(command.Action);
        if (command.Action == DataSourceAction.Delete) {
            profiles.Remove(command.ProfileId);
        }
        if (command.Action is DataSourceAction.ApplySchema or DataSourceAction.CreateEmpty) {
            schemaApplied.Add(command.ProfileId);
        }
        if (command.Action == DataSourceAction.ActivateForRestart) {
            pending = command.ProfileId;
        }
        return new(command.ProfileId, DataSourceOutcome.Confirmed, "Simulated operation acknowledged at the original target.", RequiresRestart: pending.HasValue);
    }
    public async Task<DataSourceResult> TransferAsync(TransferRequest request) {
        LastTransfer = request;
        await WriteAsync(DataSourceAction.Transfer);
        var items = request.Groups.Select((key, index) => {
            var success = scenario != DataSourcesScenario.PartialTransfer || index == 0;
            return new TransferGroupResult(key, key.Value, success, success ? "Copied one record." : "Later group failed; earlier progress remains.", success ? 1 : 0);
        }).ToArray();
        return new(request.TargetProfileId, items.All(item => item.Success) ? DataSourceOutcome.Confirmed : DataSourceOutcome.Partial,
            "Simulated group outcomes retained.", items);
    }
    private async Task ObserveAsync() {
        if (scenario == DataSourcesScenario.Loading) {
            await release.Task;
        }
        if (scenario == DataSourcesScenario.Unavailable || FailObservation) {
            throw new DataSourcesException(DataSourceFailure.Unavailable);
        }
    }
    private async Task WriteAsync(DataSourceAction action) {
        Commands.Add(action);
        if (BeforeWrite is not null) {
            await BeforeWrite();
        }
        if (scenario == DataSourcesScenario.DelayedWrite) {
            await release.Task;
        }
        if (scenario == DataSourcesScenario.UnknownWrite) {
            throw new IOException("Simulated lost acknowledgement.");
        }
        FailObservation = scenario == DataSourcesScenario.RefreshFailure;
    }
    public static ProfileValues Values(Guid? id, string name) => new(id, name, "Simulated workspace", "example.invalid", 5432,
        "simulation", "simulation", "postgres", false);
}
