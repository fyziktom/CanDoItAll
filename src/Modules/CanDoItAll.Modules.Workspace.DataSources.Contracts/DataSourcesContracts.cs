namespace CanDoItAll.Modules.Workspace.DataSources.Contracts;

public enum DataSourceProvider { PostgreSql, InMemory }
public enum DataSourceResolution { ExplicitOverride, PersistedActiveProfile, PersistedCatalogFallback, AutoProvisionedPostgreSql }
public enum SchemaStatus { Unknown, Current, NeedsMigration, Unavailable }
public enum DataSourceAction { Save, Delete, TestConnection, CreateEmpty, ApplySchema, ActivateForRestart, Transfer }
public enum DataSourceOutcome { Confirmed, Refused, Unknown, Partial }
public enum DataSourceFailure { Missing, Locked, Unavailable, InvalidRequest, StaleContext }

public sealed record DataSourcesContext(Guid RuntimeProfileId, long Generation);
public sealed record RuntimeSelection(Guid RuntimeProfileId, string DisplayName, DataSourceProvider ProviderKind,
    string Descriptor, string WorkspaceRoot, DataSourceResolution ResolutionSource, bool IsRuntimeLocked,
    Guid? PendingRestartProfileId, string PendingRestartDisplayName, string PendingRestartDescriptor) {
    public bool HasPendingRestartActivation => PendingRestartProfileId.HasValue;
}
public sealed record ProfileSummary(Guid Id, string DisplayName, DataSourceProvider ProviderKind, string Descriptor,
    bool IsActive, bool IsRuntimeLocked, bool IsPendingRestartActivation, DateTimeOffset CreatedUtc, DateTimeOffset? LastUsedUtc);
public sealed record ProfileValues(Guid? Id, string DisplayName, string? WorkspaceRoot, string PostgresHost,
    int PostgresPort, string PostgresDatabaseName, string PostgresUsername, string? PostgresAdminDatabaseName,
    bool PostgresTrustServerCertificate);
public sealed record ProfileEditor(ProfileValues Values, DataSourceProvider ProviderKind, bool IsRuntimeLocked, bool HasPassword);
public sealed record SchemaHealth(Guid ProfileId, SchemaStatus Status, string Summary,
    IReadOnlyList<string> PendingMigrations, IReadOnlyList<string> SchemaIssues, bool CanApplySchema) {
    public bool RequiresAction => Status != SchemaStatus.Current;
}
public readonly record struct TransferGroupKey(string Value) {
    public override string ToString() => Value;
}
public sealed record TransferSource(Guid ProfileId, string DisplayName, string Descriptor);
public sealed record TransferGroup(TransferGroupKey Key, string Label, string Description, bool IsSensitive);
public sealed record TransferPreview(TransferGroup Descriptor, bool IsAvailable, string Summary, string? Warning,
    int SourceRecordCount, int TargetRecordCount);
public sealed record TransferRequest(DataSourcesContext Context, Guid SourceProfileId, Guid TargetProfileId,
    IReadOnlyList<TransferGroupKey> Groups, bool ReplaceExisting);
public sealed record TransferGroupResult(TransferGroupKey Key, string Label, bool Success, string Message, int RecordsCopied);
public sealed record DataSourceCommand(DataSourcesContext Context, Guid ProfileId, DataSourceAction Action);
public sealed record DataSourceResult(Guid ProfileId, DataSourceOutcome Outcome, string Message,
    IReadOnlyList<TransferGroupResult>? Groups = null, bool RequiresRestart = false);

public sealed class ProfilePasswordIntent(string value) : IDisposable {
    private string? pending = value;
    public string Take() => Interlocked.Exchange(ref pending, null)
        ?? throw new InvalidOperationException("The password intent has already been consumed.");
    public void Dispose() => Interlocked.Exchange(ref pending, null);
    public override string ToString() => "[one-use password intent]";
}

public sealed class DataSourcesException(DataSourceFailure failure) : Exception("The data source operation is unavailable.") {
    public DataSourceFailure Failure { get; } = failure;
}

public interface IDataSourcesOwner {
    DataSourcesContext Context { get; }
    Task<RuntimeSelection> ReadRuntimeAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ProfileSummary>> ListAsync(CancellationToken cancellationToken);
    Task<ProfileEditor> ReadEditorAsync(Guid id, CancellationToken cancellationToken);
    Task<SchemaHealth> ReadSchemaAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<TransferSource>> ListSourcesAsync(Guid targetId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TransferPreview>> PreviewAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken);
    Task<DataSourceResult> SaveAsync(DataSourcesContext context, ProfileValues values, ProfilePasswordIntent password);
    Task<DataSourceResult> ExecuteAsync(DataSourceCommand command);
    Task<DataSourceResult> TransferAsync(TransferRequest request);
}
