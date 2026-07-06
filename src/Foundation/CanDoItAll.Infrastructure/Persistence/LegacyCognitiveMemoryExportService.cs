using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Infrastructure.Persistence;

public sealed class LegacyCognitiveMemoryExportService(
    ILegacyCognitiveMemoryDataReader dataReader,
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

        var exportDirectory = ResolveExportDirectory(request.ExportRootPath, request.ExportId);
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

        Directory.CreateDirectory(exportDirectory);

        try {
            var tables = await dataReader.ReadLegacyTablesAsync(cancellationToken);
            var tableManifests = new List<LegacyCognitiveMemoryTableExportManifest>();

            foreach (var table in tables.OrderBy(table => table.TableName, StringComparer.Ordinal)) {
                ValidateTableSnapshot(table);
                tableManifests.Add(await WriteTableAsync(exportDirectory, table, cancellationToken));
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

            await WriteManifestAsync(manifestPath, manifest, cancellationToken);
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

            await TryWriteFailureManifestAsync(manifestPath, manifest, cancellationToken, logger);
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
        string exportDirectory,
        LegacyCognitiveMemoryTableSnapshot table,
        CancellationToken cancellationToken) {
        var dataFileName = $"{table.TableName}.ndjson";
        var idMapFileName = $"{table.TableName}.id-map.ndjson";
        var dataFilePath = Path.Combine(exportDirectory, dataFileName);
        var idMapFilePath = Path.Combine(exportDirectory, idMapFileName);

        await WriteRowsAsync(dataFilePath, table.Rows, cancellationToken);
        await WriteReferenceMapAsync(idMapFilePath, table, cancellationToken);

        return new LegacyCognitiveMemoryTableExportManifest(
            table.TableName,
            LegacyCognitiveMemoryTableDisposition.ReadOnlyArchive,
            table.Rows.Count,
            dataFileName,
            ComputeSha256(dataFilePath),
            idMapFileName,
            ComputeSha256(idMapFilePath));
    }

    private static async Task WriteRowsAsync(
        string path,
        IReadOnlyList<LegacyCognitiveMemoryRow> rows,
        CancellationToken cancellationToken) {
        await using var stream = File.Create(path);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        foreach (var row in rows) {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = NormalizeValues(row.Values);
            var json = JsonSerializer.Serialize(normalized, LineJsonOptions);
            await writer.WriteLineAsync(json);
        }
    }

    private static async Task WriteReferenceMapAsync(
        string path,
        LegacyCognitiveMemoryTableSnapshot table,
        CancellationToken cancellationToken) {
        await using var stream = File.Create(path);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        for (var index = 0; index < table.Rows.Count; index++) {
            cancellationToken.ThrowIfCancellationRequested();
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
            await writer.WriteLineAsync(json);
        }
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

    private static async Task WriteManifestAsync(
        string manifestPath,
        LegacyCognitiveMemoryExportManifest manifest,
        CancellationToken cancellationToken) {
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        await File.WriteAllTextAsync(manifestPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
    }

    private static async Task TryWriteFailureManifestAsync(
        string manifestPath,
        LegacyCognitiveMemoryExportManifest manifest,
        CancellationToken cancellationToken,
        ILogger<LegacyCognitiveMemoryExportService>? logger) {
        try {
            await WriteManifestAsync(manifestPath, manifest, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException) {
            logger?.LogWarning(
                exception,
                "Failed to write legacy Cognitive Memory export failure manifest. ExportId={ExportId}",
                manifest.ExportId);
        }
    }

    private static string ResolveExportDirectory(string exportRootPath, string exportId) {
        var trimmedExportId = exportId.Trim();
        if (trimmedExportId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            trimmedExportId is "." or ".." ||
            trimmedExportId.Contains('/') ||
            trimmedExportId.Contains('\\')) {
            throw new ArgumentException(
                "Export id must be a single safe path segment.",
                nameof(exportId));
        }

        var rootFullPath = Path.GetFullPath(exportRootPath);
        var exportFullPath = Path.GetFullPath(Path.Combine(rootFullPath, trimmedExportId));
        var rootWithSeparator = Path.EndsInDirectorySeparator(rootFullPath)
            ? rootFullPath
            : rootFullPath + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!exportFullPath.StartsWith(rootWithSeparator, comparison)) {
            throw new ArgumentException(
                "Export id must resolve inside the export root path.",
                nameof(exportId));
        }

        return exportFullPath;
    }

    private static string ComputeSha256(string path) {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}

public sealed class LegacyCognitiveMemoryExportServiceFactory(
    ILoggerFactory loggerFactory) : ILegacyCognitiveMemoryExportServiceFactory
{
    public ILegacyCognitiveMemoryExportService Create(DbConnection legacyMainDatabaseConnection) {
        ArgumentNullException.ThrowIfNull(legacyMainDatabaseConnection);
        return new LegacyCognitiveMemoryExportService(
            new PostgreSqlLegacyCognitiveMemoryDataReader(legacyMainDatabaseConnection),
            logger: loggerFactory.CreateLogger<LegacyCognitiveMemoryExportService>());
    }
}
