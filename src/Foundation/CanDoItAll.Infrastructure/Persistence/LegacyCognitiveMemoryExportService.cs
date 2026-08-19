using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Infrastructure.Persistence;

public sealed class LegacyCognitiveMemoryExportService(
    ILegacyCognitiveMemoryDataReader dataReader,
    IPhysicalFileSystemPathPolicyFactory pathPolicyFactory,
    DurableFileWriter durableFileWriter,
    TimeProvider? timeProvider = null,
    ILogger<LegacyCognitiveMemoryExportService>? logger = null) : ILegacyCognitiveMemoryExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private static readonly JsonSerializerOptions LineJsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly HashSet<string> ReferenceColumnNames = new(StringComparer.Ordinal)
    {
        "Id",
        "MemoryRecordId",
        "SourceManifestId",
        "SourceItemId",
        "SourceSnapshotId",
        "RecallContextPackId",
        "ContextPackId",
        "RecallTraceId",
        "FeedbackId",
        "FeedbackHandle",
        "CorrelationId",
        "CausationId",
        "OperationId",
        "OutcomeCorrelationId"
    };

    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<LegacyCognitiveMemoryExportResult> ExportAsync(
        LegacyCognitiveMemoryExportRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExportRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExportId);

        if (!request.CompatibilityEnabled ||
            request.CompatibilityMode == LegacyCognitiveMemoryCompatibilityMode.Disabled) {
            return new LegacyCognitiveMemoryExportResult(
                LegacyCognitiveMemoryExportResultKind.Disabled,
                request.ExportId,
                ExportDirectory: null,
                ManifestPath: null,
                TableCount: 0,
                RowCount: 0,
                FailureMessage: "Legacy Cognitive Memory compatibility is disabled.");
        }

        string exportId = ValidateExportId(request.ExportId);
        string exportRoot = pathPolicyFactory.Create(request.ExportRootPath).RootPath;
        using IDisposable coordination = durableFileWriter.AcquireCoordination(
            exportRoot,
            Path.Combine(exportRoot, ".legacy-memory-exports.candoitall.lock"),
            TimeSpan.FromSeconds(15),
            requirePrivateUnixMode: true,
            cancellationToken);
        IPhysicalFileSystemPathPolicy pathPolicy = pathPolicyFactory.Create(exportRoot);
        var exportDirectory = ResolveExportDirectory(pathPolicy, exportId);
        var manifestPath = Path.Combine(exportDirectory, LegacyCognitiveMemoryExportConstants.ManifestFileName);
        if (File.Exists(manifestPath) && !request.OverwriteExisting) {
            return new LegacyCognitiveMemoryExportResult(
                LegacyCognitiveMemoryExportResultKind.DuplicateBlocked,
                request.ExportId,
                exportDirectory,
                manifestPath,
                TableCount: 0,
                RowCount: 0,
                FailureMessage: "An export manifest already exists for this export id.");
        }

        durableFileWriter.EnsureDirectory(
            exportRoot,
            exportDirectory,
            requirePrivateUnixMode: true);

        try {
            var tables = await dataReader.ReadLegacyTablesAsync(cancellationToken);
            var tableManifests = new List<LegacyCognitiveMemoryTableExportManifest>();

            foreach (var table in tables.OrderBy(table => table.TableName, StringComparer.Ordinal)) {
                ValidateTableSnapshot(table);
                tableManifests.Add(await WriteTableAsync(
                    exportRoot,
                    exportDirectory,
                    table,
                    cancellationToken));
            }

            var rowCount = tableManifests.Sum(table => table.RowCount);
            var resultKind = rowCount == 0
                ? LegacyCognitiveMemoryExportResultKind.NoLegacyData
                : LegacyCognitiveMemoryExportResultKind.Exported;
            var manifest = CreateManifest(
                request,
                resultKind,
                tableManifests,
                failureMessage: null);

            await WriteManifestAsync(exportRoot, manifestPath, manifest, cancellationToken);
            return new LegacyCognitiveMemoryExportResult(
                resultKind,
                request.ExportId,
                exportDirectory,
                manifestPath,
                tableManifests.Count,
                rowCount,
                FailureMessage: null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException) {
            logger?.LogError(
                exception,
                "Legacy Cognitive Memory export failed. ExportId={ExportId}; CompatibilityMode={CompatibilityMode}",
                request.ExportId,
                request.CompatibilityMode);

            var manifest = CreateManifest(
                request,
                LegacyCognitiveMemoryExportResultKind.Failed,
                [],
                exception.Message);

            await WriteFailureManifestAsync(
                exportRoot,
                manifestPath,
                manifest,
                exception,
                cancellationToken);
            return new LegacyCognitiveMemoryExportResult(
                LegacyCognitiveMemoryExportResultKind.Failed,
                request.ExportId,
                exportDirectory,
                manifestPath,
                TableCount: 0,
                RowCount: 0,
                FailureMessage: exception.Message);
        }
    }

    private async Task<LegacyCognitiveMemoryTableExportManifest> WriteTableAsync(
        string exportRoot,
        string exportDirectory,
        LegacyCognitiveMemoryTableSnapshot table,
        CancellationToken cancellationToken) {
        var dataFileName = $"{table.TableName}.ndjson";
        var idMapFileName = $"{table.TableName}.id-map.ndjson";
        var dataFilePath = Path.Combine(exportDirectory, dataFileName);
        var idMapFilePath = Path.Combine(exportDirectory, idMapFileName);

        await WriteRowsAsync(exportRoot, dataFilePath, table.Rows, cancellationToken);
        await WriteReferenceMapAsync(exportRoot, idMapFilePath, table, cancellationToken);

        return new LegacyCognitiveMemoryTableExportManifest(
            table.TableName,
            LegacyCognitiveMemoryTableDisposition.ReadOnlyArchive,
            table.Rows.Count,
            dataFileName,
            ComputeSha256(dataFilePath),
            idMapFileName,
            ComputeSha256(idMapFilePath));
    }

    private Task WriteRowsAsync(
        string exportRoot,
        string path,
        IReadOnlyList<LegacyCognitiveMemoryRow> rows,
        CancellationToken cancellationToken) {
        return durableFileWriter.WriteStreamAsync(
            exportRoot,
            path,
            async (stream, token) => {
                await using var writer = new StreamWriter(
                    stream,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    bufferSize: 64 * 1024,
                    leaveOpen: true);
                foreach (var row in rows) {
                    token.ThrowIfCancellationRequested();
                    var normalized = NormalizeValues(row.Values);
                    var json = JsonSerializer.Serialize(normalized, LineJsonOptions);
                    await writer.WriteLineAsync(json.AsMemory(), token);
                }

                await writer.FlushAsync(token);
            },
            DurableFileWriteOptions.Private,
            cancellationToken);
    }

    private Task WriteReferenceMapAsync(
        string exportRoot,
        string path,
        LegacyCognitiveMemoryTableSnapshot table,
        CancellationToken cancellationToken) {
        return durableFileWriter.WriteStreamAsync(
            exportRoot,
            path,
            async (stream, token) => {
                await using var writer = new StreamWriter(
                    stream,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    bufferSize: 64 * 1024,
                    leaveOpen: true);
                for (var index = 0; index < table.Rows.Count; index++) {
                    token.ThrowIfCancellationRequested();
                    var references = ExtractReferences(table.Rows[index].Values);
                    if (references.Count == 0) {
                        continue;
                    }

                    references.TryGetValue("Id", out var primaryId);
                    var entry = new LegacyCognitiveMemoryReferenceMapEntry(
                        table.TableName,
                        index,
                        primaryId,
                        references);
                    var json = JsonSerializer.Serialize(entry, LineJsonOptions);
                    await writer.WriteLineAsync(json.AsMemory(), token);
                }

                await writer.FlushAsync(token);
            },
            DurableFileWriteOptions.Private,
            cancellationToken);
    }

    private static SortedDictionary<string, string?> NormalizeValues(
        IReadOnlyDictionary<string, string?> values) {
        var normalized = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, value) in values) {
            normalized[key] = value;
        }

        return normalized;
    }

    private static SortedDictionary<string, string> ExtractReferences(
        IReadOnlyDictionary<string, string?> values) {
        var references = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (columnName, value) in values) {
            if (!ReferenceColumnNames.Contains(columnName) || string.IsNullOrWhiteSpace(value)) {
                continue;
            }

            references[columnName] = value;
        }

        return references;
    }

    private static void ValidateTableSnapshot(LegacyCognitiveMemoryTableSnapshot table) {
        ArgumentNullException.ThrowIfNull(table);

        if (!LegacyCognitiveMemoryExportConstants.IsLegacyTableName(table.TableName)) {
            throw new InvalidOperationException(
                $"Refusing to export invalid legacy Cognitive Memory table name '{table.TableName}'.");
        }

        var columnSet = new HashSet<string>(table.ColumnNames, StringComparer.Ordinal);
        foreach (var row in table.Rows) {
            foreach (var columnName in row.Values.Keys) {
                if (!columnSet.Contains(columnName)) {
                    throw new InvalidOperationException(
                        $"Legacy Cognitive Memory table '{table.TableName}' row contains undeclared column '{columnName}'.");
                }
            }
        }
    }

    private LegacyCognitiveMemoryExportManifest CreateManifest(
        LegacyCognitiveMemoryExportRequest request,
        LegacyCognitiveMemoryExportResultKind resultKind,
        IReadOnlyList<LegacyCognitiveMemoryTableExportManifest> tableManifests,
        string? failureMessage)
        => new(
            LegacyCognitiveMemoryExportConstants.SchemaVersion,
            request.ExportId,
            timeProvider.GetUtcNow(),
            request.CompatibilityMode,
            resultKind,
            LegacyCognitiveMemoryExportConstants.NativeImportPolicy,
            LegacyCognitiveMemoryExportConstants.RetentionPolicy,
            tableManifests.Count,
            tableManifests.Sum(table => table.RowCount),
            tableManifests,
            failureMessage);

    private Task WriteManifestAsync(
        string exportRoot,
        string manifestPath,
        LegacyCognitiveMemoryExportManifest manifest,
        CancellationToken cancellationToken) {
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        return durableFileWriter.WriteTextAsync(
            exportRoot,
            manifestPath,
            json,
            DurableFileWriteOptions.Private,
            cancellationToken);
    }

    private async Task WriteFailureManifestAsync(
        string exportRoot,
        string manifestPath,
        LegacyCognitiveMemoryExportManifest manifest,
        Exception exportFailure,
        CancellationToken cancellationToken) {
        try {
            await WriteManifestAsync(exportRoot, manifestPath, manifest, cancellationToken);
        }
        catch (Exception manifestFailure) when (manifestFailure is not OperationCanceledException) {
            throw new AggregateException(
                "The legacy Cognitive Memory export failed and its failure manifest could not be persisted.",
                exportFailure,
                manifestFailure);
        }
    }

    private static string ValidateExportId(string exportId) {
        var trimmedExportId = exportId.Trim();
        if (trimmedExportId is "." or ".." ||
            trimmedExportId.Contains('/') ||
            trimmedExportId.Contains('\\')) {
            throw new ArgumentException(
                "Export id must be a single safe path segment.",
                nameof(exportId));
        }

        return trimmedExportId;
    }

    private static string ResolveExportDirectory(
        IPhysicalFileSystemPathPolicy pathPolicy,
        string exportId) {
        PortablePhysicalFileName encoded = PortablePhysicalFileNamePolicy.Encode(exportId);

        return pathPolicy.ResolveContainedPath(encoded.PhysicalName);
    }

    private static string ComputeSha256(string path) {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}

public sealed class LegacyCognitiveMemoryExportServiceFactory(
    IPhysicalFileSystemPathPolicyFactory pathPolicyFactory,
    DurableFileWriter durableFileWriter,
    ILoggerFactory loggerFactory) : ILegacyCognitiveMemoryExportServiceFactory
{
    public ILegacyCognitiveMemoryExportService Create(DbConnection legacyMainDatabaseConnection) {
        ArgumentNullException.ThrowIfNull(legacyMainDatabaseConnection);
        return new LegacyCognitiveMemoryExportService(
            new PostgreSqlLegacyCognitiveMemoryDataReader(legacyMainDatabaseConnection),
            pathPolicyFactory,
            durableFileWriter,
            logger: loggerFactory.CreateLogger<LegacyCognitiveMemoryExportService>());
    }
}
