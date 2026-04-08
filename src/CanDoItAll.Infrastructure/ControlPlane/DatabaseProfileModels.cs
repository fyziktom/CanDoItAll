using CanDoItAll.SharedKernel;

namespace CanDoItAll.Infrastructure.ControlPlane;

public enum DatabaseProviderKind
{
    Sqlite,
    PostgreSql,
    InMemory
}

public enum DatabaseProfileSourceKind
{
    ManagedSqlite,
    ExternalSqliteFile,
    ImportedSqlite,
    PostgresConnection,
    SnapshotCache,
    IpfsSnapshot,
    InMemory
}

public enum DatabaseProfileStorageMode
{
    ManagedPerProfile,
    ExternalWorkspaceRoot,
    Ephemeral
}

public enum DatabaseProfileResolutionSource
{
    ExplicitOverride,
    PersistedActiveProfile,
    PersistedCatalogFallback,
    LegacyDiscovery,
    AutoProvisionedManagedSqlite
}

public sealed class DatabaseProfileRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string DisplayName { get; set; } = string.Empty;

    public DatabaseProviderKind ProviderKind { get; set; } = DatabaseProviderKind.Sqlite;

    public DatabaseProfileSourceKind SourceKind { get; set; } = DatabaseProfileSourceKind.ManagedSqlite;

    public SqliteDatabaseProfileConnection? Sqlite { get; set; }

    public PostgreSqlDatabaseProfileConnection? PostgreSql { get; set; }

    public InMemoryDatabaseProfileConnection? InMemory { get; set; }

    public DatabaseProfileStorageDescriptor Storage { get; set; } = new();

    public DatabaseProfileCloneMetadata Clone { get; set; } = new();

    public DatabaseProfileRuntimeMetadata Runtime { get; set; } = new();

    public DatabaseProfileAuditMetadata Audit { get; set; } = new();
}

public sealed class SqliteDatabaseProfileConnection
{
    public string DatabasePath { get; set; } = string.Empty;
}

public sealed class PostgreSqlDatabaseProfileConnection
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 5432;

    public string DatabaseName { get; set; } = "candoitall";

    public string Username { get; set; } = "postgres";

    public string? EncryptedPassword { get; set; }

    public string? AdminDatabaseName { get; set; }

    public bool TrustServerCertificate { get; set; }
}

public sealed class InMemoryDatabaseProfileConnection
{
    public string DatabaseName { get; set; } = "candoitall";
}

public sealed class DatabaseProfileStorageDescriptor
{
    public DatabaseProfileStorageMode Mode { get; set; } = DatabaseProfileStorageMode.ManagedPerProfile;

    public string WorkspaceRoot { get; set; } = string.Empty;
}

public sealed class DatabaseProfileCloneMetadata
{
    public Guid? OriginProfileId { get; set; }

    public Guid? OriginSnapshotId { get; set; }
}

public sealed class DatabaseProfileRuntimeMetadata
{
    public string Fingerprint { get; set; } = string.Empty;

    public bool LockedByRuntimeOverride { get; set; }
}

public sealed class DatabaseProfileAuditMetadata
{
    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? LastUsedUtc { get; set; }

    public DateTimeOffset? LastSuccessfulOpenUtc { get; set; }
}

public sealed class DatabaseActiveProfileState
{
    public Guid? ActiveProfileId { get; set; }

    public DateTimeOffset? LastPromptShownAtUtc { get; set; }

    public long LastSwitchGeneration { get; set; }
}

public sealed record DatabaseProfileSummary(
    Guid Id,
    string DisplayName,
    DatabaseProviderKind ProviderKind,
    DatabaseProfileSourceKind SourceKind,
    string Descriptor,
    string Fingerprint,
    bool IsActive,
    bool IsRuntimeLocked,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? LastUsedUtc);

public sealed class DatabaseProfileEditorModel
{
    public Guid? Id { get; set; }

    public string DisplayName { get; set; } = "Managed SQLite workspace";

    public DatabaseProviderKind ProviderKind { get; set; } = DatabaseProviderKind.Sqlite;

    public DatabaseProfileSourceKind SourceKind { get; set; } = DatabaseProfileSourceKind.ManagedSqlite;

    public string? SqliteDatabasePath { get; set; }

    public string? WorkspaceRoot { get; set; }

    public string PostgresHost { get; set; } = "localhost";

    public int PostgresPort { get; set; } = 5432;

    public string PostgresDatabaseName { get; set; } = "candoitall";

    public string PostgresUsername { get; set; } = "postgres";

    public string PostgresPassword { get; set; } = string.Empty;

    public string? PostgresAdminDatabaseName { get; set; }

    public bool PostgresTrustServerCertificate { get; set; }

    public Guid? OriginProfileId { get; set; }

    public Guid? OriginSnapshotId { get; set; }

    public bool IsRuntimeLocked { get; set; }
}

public sealed class DatabaseSelectionStateModel
{
    public Guid ActiveProfileId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public DatabaseProviderKind ProviderKind { get; set; }

    public DatabaseProfileSourceKind SourceKind { get; set; }

    public DatabaseProfileResolutionSource ResolutionSource { get; set; }

    public bool IsRuntimeLocked { get; set; }

    public string Fingerprint { get; set; } = string.Empty;

    public string WorkspaceRoot { get; set; } = string.Empty;

    public string Descriptor { get; set; } = string.Empty;
}

public sealed record ResolvedDatabaseProfile(
    DatabaseProfileRecord Profile,
    DatabaseProfileResolutionSource ResolutionSource,
    string ConnectionString);

public interface IControlPlaneSecretProtector
{
    string Protect(string plainText);

    string Unprotect(string protectedValue);
}

public interface IActiveDatabaseProfileResolver
{
    ResolvedDatabaseProfile ResolveCurrentProfile();
}

public interface IDatabaseProfileRuntimeAccessor : IActiveDatabaseProfileResolver
{
    ResolvedDatabaseProfile ResolveProfile(Guid profileId);
}

public interface IDatabaseProfileService
{
    Task<IReadOnlyList<DatabaseProfileSummary>> ListAsync(CancellationToken cancellationToken = default);

    Task<DatabaseProfileEditorModel> GetEditorAsync(Guid? id = null, CancellationToken cancellationToken = default);

    Result Validate(DatabaseProfileEditorModel model);

    Task<Result<Guid>> SaveAsync(DatabaseProfileEditorModel model, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result> ActivateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<DatabaseSelectionStateModel> GetCurrentSelectionAsync(CancellationToken cancellationToken = default);
}

public interface IDatabaseSnapshotService
{
    Task<Result<DatabaseSnapshotExportResult>> CreateSnapshotAsync(
        Guid sourceProfileId,
        DatabaseSnapshotTransportKind transportKind,
        CancellationToken cancellationToken = default);

    Task<Result<DatabaseSnapshotMaterializationResult>> CloneAsync(
        DatabaseCloneRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<DatabaseSnapshotMaterializationResult>> MaterializeSnapshotAsync(
        DatabaseSnapshotMaterializationRequest request,
        CancellationToken cancellationToken = default);
}
