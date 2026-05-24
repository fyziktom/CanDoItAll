using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Infrastructure.ControlPlane;

public enum DatabaseSnapshotTransportKind
{
    Local,
    Ipfs
}

public sealed class DatabaseSnapshotManifest
{
    public Guid SnapshotId { get; set; }

    public Guid SourceProfileId { get; set; }

    public DatabaseProviderKind ProviderKind { get; set; }

    public DatabaseSnapshotAppSchemaManifest AppSchema { get; set; } = new();

    public DateTimeOffset CreatedUtc { get; set; }

    public int TableCount { get; set; }

    public List<string> StorageFolders { get; set; } = [];

    public DatabaseSnapshotTransportDescriptor Transport { get; set; } = new();

    public List<DatabaseSnapshotTableManifest> Tables { get; set; } = [];
}

public sealed class DatabaseSnapshotAppSchemaManifest
{
    public string? PostgreSqlMigration { get; set; }
}

public sealed class DatabaseSnapshotTransportDescriptor
{
    public DatabaseSnapshotTransportKind Kind { get; set; } = DatabaseSnapshotTransportKind.Local;

    public string? PackagePath { get; set; }

    public string? Cid { get; set; }
}

public sealed class DatabaseSnapshotTableManifest
{
    public string Name { get; set; } = string.Empty;

    public string? Schema { get; set; }

    public string FilePath { get; set; } = string.Empty;

    public int RowCount { get; set; }
}

public sealed class DatabaseSnapshotTablePayload
{
    public string Name { get; set; } = string.Empty;

    public string? Schema { get; set; }

    public List<DatabaseSnapshotColumnPayload> Columns { get; set; } = [];

    public List<List<DatabaseSnapshotScalarValue>> Rows { get; set; } = [];
}

public sealed class DatabaseSnapshotColumnPayload
{
    public string Name { get; set; } = string.Empty;

    public string Kind { get; set; } = "string";
}

public sealed class DatabaseSnapshotScalarValue
{
    public string Kind { get; set; } = "null";

    public string? Value { get; set; }
}

public sealed class DatabaseCloneRequest
{
    public Guid SourceProfileId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public DatabaseSnapshotTransportKind TransportKind { get; set; } = DatabaseSnapshotTransportKind.Local;
}

public sealed class DatabaseSnapshotMaterializationRequest
{
    public string DisplayName { get; set; } = string.Empty;

    public string? PackagePath { get; set; }

    public string? SnapshotCid { get; set; }
}

public sealed record DatabaseSnapshotExportResult(
    DatabaseSnapshotManifest Manifest,
    string PackagePath,
    string? IpfsCid);

public sealed record DatabaseSnapshotMaterializationResult(
    Guid ProfileId,
    DatabaseSnapshotManifest Manifest,
    string PackagePath,
    string? IpfsCid);

public sealed class DatabaseSnapshotService(
    ILogger<DatabaseSnapshotService> logger) : IDatabaseSnapshotService
{
    private static readonly Error DeferredError = Error.Failure(
        "Database snapshots are deferred after PostgreSQL-only runtime conversion. Reintroduce export/import as a separate portable workflow, not as a runtime database provider.");

    public Task<Result<DatabaseSnapshotExportResult>> CreateSnapshotAsync(
        Guid sourceProfileId,
        DatabaseSnapshotTransportKind transportKind,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Database snapshot export was requested for profile {ProfileId} with transport {TransportKind}, but snapshot support is deferred.",
            sourceProfileId,
            transportKind);

        return Task.FromResult(Result<DatabaseSnapshotExportResult>.Failure(DeferredError));
    }

    public Task<Result<DatabaseSnapshotMaterializationResult>> CloneAsync(
        DatabaseCloneRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        logger.LogInformation(
            "Database clone was requested for profile {ProfileId}, but snapshot support is deferred.",
            request.SourceProfileId);

        return Task.FromResult(Result<DatabaseSnapshotMaterializationResult>.Failure(DeferredError));
    }

    public Task<Result<DatabaseSnapshotMaterializationResult>> MaterializeSnapshotAsync(
        DatabaseSnapshotMaterializationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        logger.LogInformation(
            "Database snapshot materialization was requested for package {PackagePath} or CID {Cid}, but snapshot support is deferred.",
            request.PackagePath,
            request.SnapshotCid);

        return Task.FromResult(Result<DatabaseSnapshotMaterializationResult>.Failure(DeferredError));
    }
}
