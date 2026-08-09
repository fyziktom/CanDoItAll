using CanDoItAll.SharedKernel;
using System.Text.Json.Serialization;

namespace CanDoItAll.Infrastructure.ControlPlane;

public enum DatabaseProviderKind
{
    PostgreSql = 1,
    InMemory = 2
}

public enum DatabaseProfileSourceKind
{
    PostgresConnection = 3,
    InMemory = 6
}

public enum DatabaseProfileStorageMode
{
    ExternalWorkspaceRoot,
    Ephemeral
}

public enum DatabaseProfileResolutionSource
{
    ExplicitOverride,
    PersistedActiveProfile,
    PersistedCatalogFallback,
    AutoProvisionedPostgreSql
}

public sealed class DatabaseProfileRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string DisplayName { get; set; } = string.Empty;

    public DatabaseProviderKind ProviderKind { get; set; } = DatabaseProviderKind.PostgreSql;

    public DatabaseProfileSourceKind SourceKind { get; set; } = DatabaseProfileSourceKind.PostgresConnection;

    public PostgreSqlDatabaseProfileConnection? PostgreSql { get; set; }

    public InMemoryDatabaseProfileConnection? InMemory { get; set; }

    public DatabaseProfileStorageDescriptor Storage { get; set; } = new();

    public DatabaseProfileRuntimeMetadata Runtime { get; set; } = new();

    public DatabaseProfileAuditMetadata Audit { get; set; } = new();
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
    public DatabaseProfileStorageMode Mode { get; set; } = DatabaseProfileStorageMode.ExternalWorkspaceRoot;

    public HostBoundPathRecord? WorkspacePath { get; set; }

    [JsonIgnore]
    public string WorkspaceRoot
    {
        get => WorkspacePath?.Path ?? LegacyWorkspaceRoot ?? string.Empty;
        set
        {
            WorkspacePath = string.IsNullOrWhiteSpace(value)
                ? null
                : HostBoundPathPolicy.BindCurrent(value, DateTimeOffset.UtcNow);
            LegacyWorkspaceRoot = null;
        }
    }

    [JsonPropertyName("workspaceRoot")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyWorkspaceRoot { get; set; }
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
    DateTimeOffset? LastUsedUtc)
{
    public bool IsPendingRestartActivation { get; init; }

    public HostBoundPathState WorkspacePathState { get; init; } = HostBoundPathState.NeedsRebind;

    public bool RequiresWorkspaceRebind => WorkspacePathState != HostBoundPathState.Active;
}

public enum DatabaseProfileSchemaStatus
{
    Unknown,
    Current,
    NeedsMigration,
    Unavailable
}

public sealed record DatabaseProfileSchemaHealth(
    Guid ProfileId,
    DatabaseProfileSchemaStatus Status,
    string Summary,
    int PendingMigrationCount,
    IReadOnlyList<string> PendingMigrations,
    IReadOnlyList<string> SchemaIssues,
    bool CanApplySchema)
{
    public bool RequiresAction => Status is DatabaseProfileSchemaStatus.NeedsMigration or DatabaseProfileSchemaStatus.Unavailable;
}

public sealed class DatabaseProfileEditorModel
{
    public Guid? Id { get; set; }

    public string DisplayName { get; set; } = "PostgreSQL workspace";

    public DatabaseProviderKind ProviderKind { get; set; } = DatabaseProviderKind.PostgreSql;

    public DatabaseProfileSourceKind SourceKind { get; set; } = DatabaseProfileSourceKind.PostgresConnection;

    public string? InMemoryDatabaseName { get; set; }

    public string? WorkspaceRoot { get; set; }

    public HostBoundPathState WorkspacePathState { get; set; } = HostBoundPathState.NeedsRebind;

    public string PostgresHost { get; set; } = "localhost";

    public int PostgresPort { get; set; } = 5432;

    public string PostgresDatabaseName { get; set; } = "candoitall";

    public string PostgresUsername { get; set; } = "postgres";

    public string PostgresPassword { get; set; } = string.Empty;

    public string? PostgresAdminDatabaseName { get; set; }

    public bool PostgresTrustServerCertificate { get; set; }

    public bool IsRuntimeLocked { get; set; }
}

public sealed class DatabaseSelectionStateModel
{
    private Guid _runtimeProfileId;

    public Guid ActiveProfileId { get; set; }

    public Guid RuntimeProfileId
    {
        get => _runtimeProfileId == Guid.Empty ? ActiveProfileId : _runtimeProfileId;
        set => _runtimeProfileId = value;
    }

    public string DisplayName { get; set; } = string.Empty;

    public DatabaseProviderKind ProviderKind { get; set; }

    public DatabaseProfileSourceKind SourceKind { get; set; }

    public DatabaseProfileResolutionSource ResolutionSource { get; set; }

    public bool IsRuntimeLocked { get; set; }

    public string Fingerprint { get; set; } = string.Empty;

    public string WorkspaceRoot { get; set; } = string.Empty;

    public HostBoundPathState WorkspacePathState { get; set; } = HostBoundPathState.NeedsRebind;

    public bool RequiresWorkspaceRebind => WorkspacePathState != HostBoundPathState.Active;

    public string Descriptor { get; set; } = string.Empty;

    public Guid? PendingRestartProfileId { get; set; }

    public string PendingRestartDisplayName { get; set; } = string.Empty;

    public string PendingRestartDescriptor { get; set; } = string.Empty;

    public string PendingRestartFingerprint { get; set; } = string.Empty;

    public bool HasPendingRestartActivation => PendingRestartProfileId.HasValue;
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

public sealed record ControlPlaneSecretContinuityReport(int ProtectedPasswordCount);

public interface IControlPlaneSecretContinuityVerifier
{
    Task<ControlPlaneSecretContinuityReport> VerifyAsync(CancellationToken cancellationToken = default);
}

public interface IActiveDatabaseProfileResolver
{
    ResolvedDatabaseProfile ResolveCurrentProfile();
}

public interface IDatabaseProfileRuntimeAccessor : IActiveDatabaseProfileResolver
{
    ResolvedDatabaseProfile ResolveProfile(Guid profileId);
}

public interface ICanonicalRuntimeDatabase
{
    ResolvedDatabaseProfile Profile { get; }

    long Generation { get; }
}

public interface IDatabaseProfileService
{
    Task<IReadOnlyList<DatabaseProfileSummary>> ListAsync(CancellationToken cancellationToken = default);

    Task<DatabaseProfileEditorModel> GetEditorAsync(Guid? id = null, CancellationToken cancellationToken = default);

    Result Validate(DatabaseProfileEditorModel model);

    Task<Result<Guid>> SaveAsync(DatabaseProfileEditorModel model, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result> ActivateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result> RebindWorkspaceAsync(Guid id, string workspaceRoot, CancellationToken cancellationToken = default);

    Task<Result> RollbackWorkspacePathMigrationAsync(CancellationToken cancellationToken = default);

    Task<DatabaseSelectionStateModel> GetCurrentSelectionAsync(CancellationToken cancellationToken = default);
}
