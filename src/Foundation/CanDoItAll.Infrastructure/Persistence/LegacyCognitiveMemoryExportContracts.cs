using System.Data.Common;

namespace CanDoItAll.Infrastructure.Persistence;

public enum LegacyCognitiveMemoryCompatibilityMode
{
    Disabled = 0,
    ReadOnlyArchive = 1
}

public enum LegacyCognitiveMemoryExportResultKind
{
    Disabled = 0,
    NoLegacyData = 1,
    Exported = 2,
    DuplicateBlocked = 3,
    Failed = 4
}

public enum LegacyCognitiveMemoryTableDisposition
{
    ReadOnlyArchive = 0
}

public sealed record LegacyCognitiveMemoryExportRequest(
    string ExportRootPath,
    string ExportId,
    bool CompatibilityEnabled,
    bool OverwriteExisting = false,
    LegacyCognitiveMemoryCompatibilityMode CompatibilityMode = LegacyCognitiveMemoryCompatibilityMode.ReadOnlyArchive);

public sealed record LegacyCognitiveMemoryExportResult(
    LegacyCognitiveMemoryExportResultKind Kind,
    string ExportId,
    string? ExportDirectory,
    string? ManifestPath,
    int TableCount,
    long RowCount,
    string? FailureMessage);

public sealed record LegacyCognitiveMemoryExportManifest(
    string SchemaVersion,
    string ExportId,
    DateTimeOffset GeneratedAtUtc,
    LegacyCognitiveMemoryCompatibilityMode CompatibilityMode,
    LegacyCognitiveMemoryExportResultKind ResultKind,
    string NativeImportPolicy,
    string RetentionPolicy,
    int TableCount,
    long RowCount,
    IReadOnlyList<LegacyCognitiveMemoryTableExportManifest> Tables,
    string? FailureMessage);

public sealed record LegacyCognitiveMemoryTableExportManifest(
    string TableName,
    LegacyCognitiveMemoryTableDisposition Disposition,
    long RowCount,
    string DataFile,
    string DataSha256,
    string IdMapFile,
    string IdMapSha256);

public sealed record LegacyCognitiveMemoryTableSnapshot(
    string TableName,
    IReadOnlyList<string> ColumnNames,
    IReadOnlyList<LegacyCognitiveMemoryRow> Rows);

public sealed record LegacyCognitiveMemoryRow(
    IReadOnlyDictionary<string, string?> Values);

public sealed record LegacyCognitiveMemoryReferenceMapEntry(
    string TableName,
    int RowOrdinal,
    string? PrimaryId,
    IReadOnlyDictionary<string, string> References);

public interface ILegacyCognitiveMemoryDataReader
{
    Task<IReadOnlyList<LegacyCognitiveMemoryTableSnapshot>> ReadLegacyTablesAsync(
        CancellationToken cancellationToken = default);
}

public interface ILegacyCognitiveMemoryExportService
{
    Task<LegacyCognitiveMemoryExportResult> ExportAsync(
        LegacyCognitiveMemoryExportRequest request,
        CancellationToken cancellationToken = default);
}

public interface ILegacyCognitiveMemoryExportServiceFactory
{
    ILegacyCognitiveMemoryExportService Create(DbConnection legacyMainDatabaseConnection);
}

public static class LegacyCognitiveMemoryExportConstants
{
    public const string SchemaVersion = "legacy-cognitive-memory-export.v1";
    public const string TableNamePrefix = "CognitiveMemory_";
    public const string ManifestFileName = "manifest.json";
    public const string NativeImportPolicy = "ManualNativeServiceImportRequired";
    public const string RetentionPolicy = "ReadOnlyUntilNativeImportOrExplicitDeletion";

    public static bool IsLegacyTableName(string tableName)
        => !string.IsNullOrWhiteSpace(tableName) &&
           tableName.StartsWith(TableNamePrefix, StringComparison.Ordinal) &&
           tableName.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
}
