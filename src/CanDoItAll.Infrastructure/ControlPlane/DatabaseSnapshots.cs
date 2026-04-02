using System.Data.Common;
using System.Globalization;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    public string? SqliteMigration { get; set; }

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
    IDatabaseProfileRuntimeAccessor profileAccessor,
    IDatabaseProfileService profileService,
    IAppDatabaseBootstrapper bootstrapper,
    ISwitchableAppDbContextFactory dbContextFactory,
    IControlPlanePathResolver controlPlanePathResolver,
    IStorageTransferPipeline storageTransferPipeline,
    IOptions<ControlPlaneOptions> controlPlaneOptions,
    IClock clock,
    ILogger<DatabaseSnapshotService> logger) : IDatabaseSnapshotService
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private static readonly string[] ProfileScopedStorageFolders = ["managed-files", "exports", "evidence"];
    private readonly ControlPlaneOptions _controlPlaneOptions = controlPlaneOptions.Value;

    public async Task<Result<DatabaseSnapshotExportResult>> CreateSnapshotAsync(
        Guid sourceProfileId,
        DatabaseSnapshotTransportKind transportKind,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sourceProfile = profileAccessor.ResolveProfile(sourceProfileId);
            await bootstrapper.EnsureProfileReadyAsync(sourceProfile, cancellationToken);

            var snapshotId = Guid.NewGuid();
            var packagePath = controlPlanePathResolver.ResolveSnapshotPackagePath(snapshotId);
            var manifest = await ExportSnapshotPackageAsync(
                sourceProfile,
                snapshotId,
                packagePath,
                transportKind,
                cancellationToken);

            string? ipfsCid = null;
            if (transportKind == DatabaseSnapshotTransportKind.Ipfs)
            {
                ipfsCid = await UploadSnapshotToIpfsAsync(packagePath, cancellationToken);
                manifest.Transport.Cid = ipfsCid;
            }

            logger.LogInformation(
                "Created database snapshot {SnapshotId} from profile {ProfileId}. Transport={TransportKind}. PackagePath={PackagePath}. Cid={Cid}.",
                snapshotId,
                sourceProfileId,
                transportKind,
                packagePath,
                ipfsCid);

            return Result<DatabaseSnapshotExportResult>.Success(new DatabaseSnapshotExportResult(
                manifest,
                packagePath,
                ipfsCid));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Creating a database snapshot failed for profile {ProfileId}.", sourceProfileId);
            return Result<DatabaseSnapshotExportResult>.Failure(
                Error.Failure($"Creating a database snapshot failed: {ex.Message}"));
        }
    }

    public async Task<Result<DatabaseSnapshotMaterializationResult>> CloneAsync(
        DatabaseCloneRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Result<DatabaseSnapshotMaterializationResult>.Failure(
                Error.Validation("Clone display name is required."));
        }

        var snapshotResult = await CreateSnapshotAsync(
            request.SourceProfileId,
            request.TransportKind,
            cancellationToken);
        if (snapshotResult.IsFailure)
        {
            return Result<DatabaseSnapshotMaterializationResult>.Failure(snapshotResult.Errors);
        }

        var materializationRequest = new DatabaseSnapshotMaterializationRequest
        {
            DisplayName = request.DisplayName.Trim(),
            PackagePath = request.TransportKind == DatabaseSnapshotTransportKind.Local
                ? snapshotResult.Value!.PackagePath
                : null,
            SnapshotCid = request.TransportKind == DatabaseSnapshotTransportKind.Ipfs
                ? snapshotResult.Value!.IpfsCid
                : null
        };

        return await MaterializeSnapshotCoreAsync(
            materializationRequest,
            request.TransportKind == DatabaseSnapshotTransportKind.Ipfs
                ? DatabaseProfileSourceKind.IpfsSnapshot
                : DatabaseProfileSourceKind.SnapshotCache,
            cancellationToken);
    }

    public Task<Result<DatabaseSnapshotMaterializationResult>> MaterializeSnapshotAsync(
        DatabaseSnapshotMaterializationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sourceKind = string.IsNullOrWhiteSpace(request.SnapshotCid)
            ? DatabaseProfileSourceKind.SnapshotCache
            : DatabaseProfileSourceKind.IpfsSnapshot;

        return MaterializeSnapshotCoreAsync(request, sourceKind, cancellationToken);
    }

    private async Task<Result<DatabaseSnapshotMaterializationResult>> MaterializeSnapshotCoreAsync(
        DatabaseSnapshotMaterializationRequest request,
        DatabaseProfileSourceKind sourceKind,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Result<DatabaseSnapshotMaterializationResult>.Failure(
                Error.Validation("Snapshot profile name is required."));
        }

        var hasPackagePath = !string.IsNullOrWhiteSpace(request.PackagePath);
        var hasSnapshotCid = !string.IsNullOrWhiteSpace(request.SnapshotCid);
        if (hasPackagePath == hasSnapshotCid)
        {
            return Result<DatabaseSnapshotMaterializationResult>.Failure(
                Error.Validation("Choose either a local snapshot package path or an IPFS CID."));
        }

        try
        {
            var packagePath = hasPackagePath
                ? Path.GetFullPath(request.PackagePath!)
                : await DownloadSnapshotFromIpfsAsync(request.SnapshotCid!, cancellationToken);
            var extractionRoot = ExtractSnapshotPackage(packagePath);

            try
            {
                var manifest = await ReadManifestAsync(extractionRoot, cancellationToken);
                var saveResult = await profileService.SaveAsync(new DatabaseProfileEditorModel
                {
                    DisplayName = request.DisplayName.Trim(),
                    ProviderKind = DatabaseProviderKind.Sqlite,
                    SourceKind = sourceKind,
                    OriginProfileId = manifest.SourceProfileId,
                    OriginSnapshotId = manifest.SnapshotId
                }, cancellationToken);
                if (saveResult.IsFailure)
                {
                    return Result<DatabaseSnapshotMaterializationResult>.Failure(saveResult.Errors);
                }

                var targetProfile = profileAccessor.ResolveProfile(saveResult.Value);
                await bootstrapper.EnsureProfileReadyAsync(targetProfile, cancellationToken);
                await RestoreSnapshotAsync(extractionRoot, manifest, targetProfile, cancellationToken);

                logger.LogInformation(
                    "Materialized snapshot {SnapshotId} into profile {ProfileId} from {PackagePath}. SourceKind={SourceKind}.",
                    manifest.SnapshotId,
                    saveResult.Value,
                    packagePath,
                    sourceKind);

                return Result<DatabaseSnapshotMaterializationResult>.Success(
                    new DatabaseSnapshotMaterializationResult(
                        saveResult.Value,
                        manifest,
                        packagePath,
                        hasSnapshotCid ? request.SnapshotCid : null));
            }
            finally
            {
                DeleteDirectoryIfExists(extractionRoot);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Materializing a snapshot-backed profile failed.");
            return Result<DatabaseSnapshotMaterializationResult>.Failure(
                Error.Failure($"Materializing a snapshot-backed profile failed: {ex.Message}"));
        }
    }

    private async Task<DatabaseSnapshotManifest> ExportSnapshotPackageAsync(
        ResolvedDatabaseProfile sourceProfile,
        Guid snapshotId,
        string packagePath,
        DatabaseSnapshotTransportKind transportKind,
        CancellationToken cancellationToken)
    {
        var workingRoot = Path.Combine(
            controlPlanePathResolver.ResolveSnapshotsRootPath(),
            $"{snapshotId:N}.work");
        DeleteDirectoryIfExists(workingRoot);
        Directory.CreateDirectory(workingRoot);

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextForProfileAsync(sourceProfile, cancellationToken);
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
            var connection = dbContext.Database.GetDbConnection();
            var tables = dbContext.Model.GetRelationalModel().Tables
                .Where(table => !string.Equals(table.Name, "__EFMigrationsHistory", StringComparison.Ordinal))
                .OrderBy(table => table.Schema, StringComparer.OrdinalIgnoreCase)
                .ThenBy(table => table.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var tablesRoot = Path.Combine(workingRoot, "tables");
            Directory.CreateDirectory(tablesRoot);

            var tableManifests = new List<DatabaseSnapshotTableManifest>(tables.Count);
            for (var index = 0; index < tables.Count; index++)
            {
                var table = tables[index];
                var payload = await ReadTablePayloadAsync(connection, table.Name, table.Schema, cancellationToken);
                var fileName = $"{index + 1:00}-{SanitizePathSegment(table.Name)}.json";
                var relativePath = Path.Combine("tables", fileName).Replace('\\', '/');
                var fullPath = Path.Combine(tablesRoot, fileName);

                await File.WriteAllTextAsync(
                    fullPath,
                    JsonSerializer.Serialize(payload, SerializerOptions),
                    cancellationToken);

                tableManifests.Add(new DatabaseSnapshotTableManifest
                {
                    Name = payload.Name,
                    Schema = payload.Schema,
                    FilePath = relativePath,
                    RowCount = payload.Rows.Count
                });
            }

            var storageFolders = await CopyStorageFoldersAsync(
                sourceProfile.Profile.Storage.WorkspaceRoot,
                workingRoot,
                cancellationToken);
            var appliedMigration = (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).LastOrDefault();
            var manifest = new DatabaseSnapshotManifest
            {
                SnapshotId = snapshotId,
                SourceProfileId = sourceProfile.Profile.Id,
                ProviderKind = sourceProfile.Profile.ProviderKind,
                AppSchema = new DatabaseSnapshotAppSchemaManifest
                {
                    SqliteMigration = sourceProfile.Profile.ProviderKind == DatabaseProviderKind.Sqlite
                        ? appliedMigration
                        : null,
                    PostgreSqlMigration = sourceProfile.Profile.ProviderKind == DatabaseProviderKind.PostgreSql
                        ? appliedMigration
                        : null
                },
                CreatedUtc = clock.GetUtcNow(),
                TableCount = tableManifests.Count,
                StorageFolders = storageFolders,
                Transport = new DatabaseSnapshotTransportDescriptor
                {
                    Kind = transportKind,
                    PackagePath = packagePath
                },
                Tables = tableManifests
            };

            await File.WriteAllTextAsync(
                Path.Combine(workingRoot, "manifest.json"),
                JsonSerializer.Serialize(manifest, SerializerOptions),
                cancellationToken);

            Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);
            if (File.Exists(packagePath))
            {
                File.Delete(packagePath);
            }

            ZipFile.CreateFromDirectory(workingRoot, packagePath, CompressionLevel.Optimal, includeBaseDirectory: false);
            return manifest;
        }
        finally
        {
            DeleteDirectoryIfExists(workingRoot);
        }
    }

    private async Task RestoreSnapshotAsync(
        string extractionRoot,
        DatabaseSnapshotManifest manifest,
        ResolvedDatabaseProfile targetProfile,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextForProfileAsync(targetProfile, cancellationToken);
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();

        await ExecuteNonQueryAsync(connection, "pragma foreign_keys = off;", cancellationToken);

        try
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            for (var index = manifest.Tables.Count - 1; index >= 0; index--)
            {
                await ExecuteNonQueryAsync(
                    connection,
                    $"delete from {QuoteTableIdentifier(schema: null, manifest.Tables[index].Name)};",
                    cancellationToken,
                    transaction);
            }

            foreach (var table in manifest.Tables)
            {
                var payload = await ReadTablePayloadAsync(extractionRoot, table.FilePath, cancellationToken);
                await InsertRowsAsync(connection, payload, cancellationToken, transaction);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await ExecuteNonQueryAsync(connection, "pragma foreign_keys = on;", cancellationToken);
        }

        await RestoreStorageFoldersAsync(
            extractionRoot,
            targetProfile.Profile.Storage.WorkspaceRoot,
            cancellationToken);
    }

    private async Task<string> UploadSnapshotToIpfsAsync(string packagePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_controlPlaneOptions.IpfsApiBaseUrl))
        {
            throw new InvalidOperationException("ControlPlane:IpfsApiBaseUrl is required for IPFS snapshot transport.");
        }

        using var client = new HttpClient();
        await using var packageStream = File.OpenRead(packagePath);
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(packageStream), "file", Path.GetFileName(packagePath));

        using var addResponse = await client.PostAsync(BuildIpfsApiUri("add"), content, cancellationToken);
        addResponse.EnsureSuccessStatusCode();

        var addPayload = await addResponse.Content.ReadFromJsonAsync<IpfsAddResponse>(
            SerializerOptions,
            cancellationToken);
        if (addPayload is null || string.IsNullOrWhiteSpace(addPayload.Hash))
        {
            throw new InvalidOperationException("The IPFS add response did not return a CID.");
        }

        using var pinResponse = await client.PostAsync(
            BuildIpfsApiUri("pin/add", addPayload.Hash),
            content: null,
            cancellationToken);
        pinResponse.EnsureSuccessStatusCode();

        return addPayload.Hash;
    }

    private async Task<string> DownloadSnapshotFromIpfsAsync(string snapshotCid, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_controlPlaneOptions.IpfsApiBaseUrl))
        {
            throw new InvalidOperationException("ControlPlane:IpfsApiBaseUrl is required for IPFS snapshot transport.");
        }

        using var client = new HttpClient();
        using var response = await client.GetAsync(BuildIpfsApiUri("cat", snapshotCid), cancellationToken);
        response.EnsureSuccessStatusCode();

        var packagePath = controlPlanePathResolver.ResolveSnapshotPackagePath(Guid.NewGuid());
        Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);
        await using var targetStream = File.Create(packagePath);
        await response.Content.CopyToAsync(targetStream, cancellationToken);
        return packagePath;
    }

    private Uri BuildIpfsApiUri(string action, string? arg = null)
    {
        var configuredBaseUrl = _controlPlaneOptions.IpfsApiBaseUrl
            ?? throw new InvalidOperationException("ControlPlane:IpfsApiBaseUrl is required for IPFS snapshot transport.");
        var baseUri = new Uri(configuredBaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? configuredBaseUrl
            : configuredBaseUrl + "/");
        var apiRoot = baseUri.AbsolutePath.TrimEnd('/').EndsWith("/api/v0", StringComparison.OrdinalIgnoreCase)
            ? baseUri
            : new Uri(baseUri, "api/v0/");
        var endpoint = new Uri(apiRoot, action);

        if (string.IsNullOrWhiteSpace(arg))
        {
            return endpoint;
        }

        var builder = new UriBuilder(endpoint)
        {
            Query = $"arg={Uri.EscapeDataString(arg)}"
        };
        return builder.Uri;
    }

    private static async Task<DatabaseSnapshotTablePayload> ReadTablePayloadAsync(
        DbConnection connection,
        string tableName,
        string? schema,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"select * from {QuoteTableIdentifier(schema, tableName)};";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var payload = new DatabaseSnapshotTablePayload
        {
            Name = tableName,
            Schema = schema
        };

        for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
        {
            payload.Columns.Add(new DatabaseSnapshotColumnPayload
            {
                Name = reader.GetName(ordinal),
                Kind = GetKindName(reader.GetFieldType(ordinal))
            });
        }

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new List<DatabaseSnapshotScalarValue>(reader.FieldCount);
            for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
            {
                row.Add(SerializeScalarValue(reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal)));
            }

            payload.Rows.Add(row);
        }

        return payload;
    }

    private static async Task InsertRowsAsync(
        DbConnection connection,
        DatabaseSnapshotTablePayload payload,
        CancellationToken cancellationToken,
        DbTransaction transaction)
    {
        if (payload.Columns.Count == 0 || payload.Rows.Count == 0)
        {
            return;
        }

        var columnList = string.Join(", ", payload.Columns.Select(column => QuoteIdentifier(column.Name)));
        var parameterList = string.Join(", ", payload.Columns.Select((_, index) => $"@p{index}"));
        var commandText = $"insert into {QuoteTableIdentifier(schema: null, payload.Name)} ({columnList}) values ({parameterList});";

        foreach (var row in payload.Rows)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = commandText;

            for (var ordinal = 0; ordinal < payload.Columns.Count; ordinal++)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@p{ordinal}";
                parameter.Value = DeserializeScalarValue(row[ordinal]);
                command.Parameters.Add(parameter);
            }

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task ExecuteNonQueryAsync(
        DbConnection connection,
        string commandText,
        CancellationToken cancellationToken,
        DbTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<DatabaseSnapshotManifest> ReadManifestAsync(string extractionRoot, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(extractionRoot, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException("The snapshot package is missing manifest.json.");
        }

        var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
        return JsonSerializer.Deserialize<DatabaseSnapshotManifest>(json, SerializerOptions)
            ?? throw new InvalidOperationException("The snapshot manifest is invalid.");
    }

    private static string ExtractSnapshotPackage(string packagePath)
    {
        if (!File.Exists(packagePath))
        {
            throw new InvalidOperationException($"Snapshot package '{packagePath}' was not found.");
        }

        var extractionRoot = Path.Combine(
            Path.GetDirectoryName(packagePath)!,
            $"{Path.GetFileNameWithoutExtension(packagePath)}.{Guid.NewGuid():N}.extract");
        Directory.CreateDirectory(extractionRoot);

        using var archive = ZipFile.OpenRead(packagePath);
        var normalizedRoot = extractionRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        foreach (var entry in archive.Entries)
        {
            var destinationPath = Path.GetFullPath(Path.Combine(
                extractionRoot,
                entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
            if (!destinationPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(destinationPath, extractionRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The snapshot package contains an unsafe file path.");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            entry.ExtractToFile(destinationPath, overwrite: true);
        }

        return extractionRoot;
    }

    private static async Task<DatabaseSnapshotTablePayload> ReadTablePayloadAsync(
        string extractionRoot,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(Path.Combine(
            extractionRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var normalizedRoot = extractionRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The snapshot package contains an invalid table file path.");
        }

        var json = await File.ReadAllTextAsync(fullPath, cancellationToken);
        return JsonSerializer.Deserialize<DatabaseSnapshotTablePayload>(json, SerializerOptions)
            ?? throw new InvalidOperationException($"The snapshot table payload '{relativePath}' is invalid.");
    }

    private async Task<List<string>> CopyStorageFoldersAsync(
        string workspaceRoot,
        string workingRoot,
        CancellationToken cancellationToken)
    {
        var copiedFolders = ProfileScopedStorageFolders
            .Where(folder => Directory.Exists(Path.Combine(workspaceRoot, folder)))
            .ToList();
        if (copiedFolders.Count == 0)
        {
            return [];
        }

        var items = BuildStorageTransferItems(
            workspaceRoot,
            string.Empty,
            "storage",
            copiedFolders);
        await ExecuteStorageFolderTransferAsync(
            workspaceRoot,
            workingRoot,
            items,
            cancellationToken);
        return copiedFolders;
    }

    private async Task RestoreStorageFoldersAsync(
        string extractionRoot,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        foreach (var folder in ProfileScopedStorageFolders)
        {
            var destinationPath = Path.Combine(workspaceRoot, folder);

            if (Directory.Exists(destinationPath))
            {
                Directory.Delete(destinationPath, recursive: true);
            }

            Directory.CreateDirectory(destinationPath);
        }

        var foldersToRestore = ProfileScopedStorageFolders
            .Where(folder => Directory.Exists(Path.Combine(extractionRoot, "storage", folder)))
            .ToList();
        if (foldersToRestore.Count == 0)
        {
            return;
        }

        var items = BuildStorageTransferItems(
            extractionRoot,
            "storage",
            string.Empty,
            foldersToRestore);
        await ExecuteStorageFolderTransferAsync(
            extractionRoot,
            workspaceRoot,
            items,
            cancellationToken);
    }

    private async Task ExecuteStorageFolderTransferAsync(
        string sourceRoot,
        string targetRoot,
        IReadOnlyList<StorageTransferItem> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        var sourceStorage = CreateFileSystemTransferStorage(sourceRoot, canWrite: false);
        var targetStorage = CreateFileSystemTransferStorage(targetRoot, canWrite: true);
        var transferOptions = new StorageTransferOptions(
            MaxConcurrency: 4,
            MaxAttempts: 2,
            VerifyTargetContent: true,
            ProgressCallback: (progress, _) =>
            {
                logger.LogDebug(
                    "Snapshot storage transfer progress {Completed}/{Total}. Item={SourcePath} -> {TargetPath}. Success={IsSuccess}.",
                    progress.CompletedCount,
                    progress.TotalCount,
                    progress.CurrentItem.SourcePath,
                    progress.CurrentItem.TargetPath,
                    progress.CurrentItem.IsSuccess);
                return ValueTask.CompletedTask;
            });

        var result = await storageTransferPipeline.ExecuteAsync(
            new StorageTransferManifest(
                null,
                null,
                items,
                sourceStorage,
                targetStorage,
                transferOptions),
            cancellationToken);
        if (result.FailureCount > 0)
        {
            var failureMessages = string.Join(
                " | ",
                result.Items
                    .Where(item => !item.IsSuccess)
                    .Select(item => $"{item.SourcePath} -> {item.TargetPath}: {item.Message}"));
            throw new InvalidOperationException($"Snapshot storage transfer failed: {failureMessages}");
        }
    }

    private static List<StorageTransferItem> BuildStorageTransferItems(
        string rootPath,
        string sourcePrefix,
        string targetPrefix,
        IReadOnlyList<string> folders)
    {
        var items = new List<StorageTransferItem>();

        foreach (var folder in folders)
        {
            var sourceBasePath = string.IsNullOrWhiteSpace(sourcePrefix)
                ? Path.Combine(rootPath, folder)
                : Path.Combine(rootPath, sourcePrefix, folder);
            if (!Directory.Exists(sourceBasePath))
            {
                continue;
            }

            foreach (var file in Directory.GetFiles(sourceBasePath, "*", SearchOption.AllDirectories))
            {
                var relativeFilePath = Path.GetRelativePath(sourceBasePath, file).Replace('\\', '/');
                var sourcePath = CombineStoragePath(sourcePrefix, folder, relativeFilePath);
                var targetPath = CombineStoragePath(targetPrefix, folder, relativeFilePath);

                items.Add(new StorageTransferItem(
                    sourcePath,
                    targetPath,
                    "application/octet-stream",
                    ResolveStorageUsagePurpose(folder)));
            }
        }

        return items;
    }

    private static StorageCatalogRecord CreateFileSystemTransferStorage(string rootPath, bool canWrite)
    {
        var capabilityMask = StorageCapability.Read |
                             StorageCapability.Download |
                             StorageCapability.BatchTransfer |
                             StorageCapability.ConnectionTest;
        if (canWrite)
        {
            capabilityMask |= StorageCapability.Write |
                              StorageCapability.Delete |
                              StorageCapability.MutableUpdate |
                              StorageCapability.BatchFolderUpload;
        }

        return new StorageCatalogRecord
        {
            Id = Guid.NewGuid(),
            Name = canWrite ? "Snapshot transfer target" : "Snapshot transfer source",
            ProviderKind = StorageProviderKind.FileSystem,
            ConnectionMode = StorageConnectionMode.Local,
            EndpointOrRoot = rootPath,
            CapabilityMask = capabilityMask,
            HealthStatus = StorageHealthStatus.Healthy,
            IsEnabled = true,
            IsReadOnly = !canWrite
        };
    }

    private static string CombineStoragePath(string prefix, string folder, string relativePath)
    {
        var segments = new[] { prefix, folder, relativePath }
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .Select(segment => segment.Trim().Trim('/'));
        return string.Join('/', segments);
    }

    private static StorageUsagePurpose ResolveStorageUsagePurpose(string folder)
    {
        return folder switch
        {
            "managed-files" => StorageUsagePurpose.ProjectAsset,
            "exports" => StorageUsagePurpose.WorkspaceExport,
            "evidence" => StorageUsagePurpose.Evidence,
            _ => StorageUsagePurpose.Unknown
        };
    }

    private static string QuoteTableIdentifier(string? schema, string tableName)
    {
        return string.IsNullOrWhiteSpace(schema)
            ? QuoteIdentifier(tableName)
            : $"{QuoteIdentifier(schema)}.{QuoteIdentifier(tableName)}";
    }

    private static string QuoteIdentifier(string value)
    {
        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static DatabaseSnapshotScalarValue SerializeScalarValue(object? value)
    {
        if (value is null or DBNull)
        {
            return new DatabaseSnapshotScalarValue();
        }

        return value switch
        {
            string stringValue => new DatabaseSnapshotScalarValue
            {
                Kind = "string",
                Value = stringValue
            },
            Guid guidValue => new DatabaseSnapshotScalarValue
            {
                Kind = "guid",
                Value = guidValue.ToString("D")
            },
            bool boolValue => new DatabaseSnapshotScalarValue
            {
                Kind = "bool",
                Value = boolValue ? "true" : "false"
            },
            short shortValue => CreateNumberValue("int16", shortValue),
            int intValue => CreateNumberValue("int32", intValue),
            long longValue => CreateNumberValue("int64", longValue),
            byte byteValue => CreateNumberValue("byte", byteValue),
            decimal decimalValue => CreateNumberValue("decimal", decimalValue),
            float floatValue => new DatabaseSnapshotScalarValue
            {
                Kind = "single",
                Value = floatValue.ToString("R", CultureInfo.InvariantCulture)
            },
            double doubleValue => new DatabaseSnapshotScalarValue
            {
                Kind = "double",
                Value = doubleValue.ToString("R", CultureInfo.InvariantCulture)
            },
            DateTime dateTimeValue => new DatabaseSnapshotScalarValue
            {
                Kind = "datetime",
                Value = dateTimeValue.ToString("O", CultureInfo.InvariantCulture)
            },
            DateTimeOffset dateTimeOffsetValue => new DatabaseSnapshotScalarValue
            {
                Kind = "datetimeoffset",
                Value = dateTimeOffsetValue.ToString("O", CultureInfo.InvariantCulture)
            },
            DateOnly dateOnlyValue => new DatabaseSnapshotScalarValue
            {
                Kind = "dateonly",
                Value = dateOnlyValue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            },
            TimeOnly timeOnlyValue => new DatabaseSnapshotScalarValue
            {
                Kind = "timeonly",
                Value = timeOnlyValue.ToString("HH':'mm':'ss'.'fffffff", CultureInfo.InvariantCulture)
            },
            TimeSpan timeSpanValue => new DatabaseSnapshotScalarValue
            {
                Kind = "timespan",
                Value = timeSpanValue.ToString("c", CultureInfo.InvariantCulture)
            },
            byte[] bytesValue => new DatabaseSnapshotScalarValue
            {
                Kind = "bytes",
                Value = Convert.ToBase64String(bytesValue)
            },
            char charValue => new DatabaseSnapshotScalarValue
            {
                Kind = "char",
                Value = charValue.ToString()
            },
            _ => new DatabaseSnapshotScalarValue
            {
                Kind = "string",
                Value = Convert.ToString(value, CultureInfo.InvariantCulture)
            }
        };
    }

    private static DatabaseSnapshotScalarValue CreateNumberValue<T>(string kind, T value)
        where T : struct, IFormattable
    {
        return new DatabaseSnapshotScalarValue
        {
            Kind = kind,
            Value = value.ToString(null, CultureInfo.InvariantCulture)
        };
    }

    private static object DeserializeScalarValue(DatabaseSnapshotScalarValue value)
    {
        return value.Kind switch
        {
            "null" => DBNull.Value,
            "string" => value.Value ?? string.Empty,
            "guid" => value.Value ?? string.Empty,
            "bool" => string.Equals(value.Value, "true", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
            "int16" => short.Parse(value.Value ?? "0", CultureInfo.InvariantCulture),
            "int32" => int.Parse(value.Value ?? "0", CultureInfo.InvariantCulture),
            "int64" => long.Parse(value.Value ?? "0", CultureInfo.InvariantCulture),
            "byte" => byte.Parse(value.Value ?? "0", CultureInfo.InvariantCulture),
            "decimal" => decimal.Parse(value.Value ?? "0", CultureInfo.InvariantCulture),
            "single" => float.Parse(value.Value ?? "0", CultureInfo.InvariantCulture),
            "double" => double.Parse(value.Value ?? "0", CultureInfo.InvariantCulture),
            "datetime" => value.Value ?? string.Empty,
            "datetimeoffset" => value.Value ?? string.Empty,
            "dateonly" => value.Value ?? string.Empty,
            "timeonly" => value.Value ?? string.Empty,
            "timespan" => value.Value ?? string.Empty,
            "bytes" => Convert.FromBase64String(value.Value ?? string.Empty),
            "char" => value.Value ?? string.Empty,
            _ => value.Value ?? string.Empty
        };
    }

    private static string GetKindName(Type type)
    {
        if (type == typeof(string))
        {
            return "string";
        }

        if (type == typeof(Guid))
        {
            return "guid";
        }

        if (type == typeof(bool))
        {
            return "bool";
        }

        if (type == typeof(short))
        {
            return "int16";
        }

        if (type == typeof(int))
        {
            return "int32";
        }

        if (type == typeof(long))
        {
            return "int64";
        }

        if (type == typeof(byte))
        {
            return "byte";
        }

        if (type == typeof(decimal))
        {
            return "decimal";
        }

        if (type == typeof(float))
        {
            return "single";
        }

        if (type == typeof(double))
        {
            return "double";
        }

        if (type == typeof(DateTime))
        {
            return "datetime";
        }

        if (type == typeof(DateTimeOffset))
        {
            return "datetimeoffset";
        }

        if (type == typeof(DateOnly))
        {
            return "dateonly";
        }

        if (type == typeof(TimeOnly))
        {
            return "timeonly";
        }

        if (type == typeof(TimeSpan))
        {
            return "timespan";
        }

        if (type == typeof(byte[]))
        {
            return "bytes";
        }

        if (type == typeof(char))
        {
            return "char";
        }

        return "string";
    }

    private static string SanitizePathSegment(string value)
    {
        var sanitized = string.Concat(value.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));

        return string.IsNullOrWhiteSpace(sanitized) ? "table" : sanitized;
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record IpfsAddResponse(string Hash, string Name, string Size);
}
