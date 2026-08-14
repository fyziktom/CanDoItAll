using System.Text.Json;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Tests.Support;
using Npgsql;

namespace CanDoItAll.Tests.Unit;

public sealed class LegacyCognitiveMemoryExportServiceTests
{
    [Fact]
    public async Task ExportAsync_disabled_returns_disabled_without_reading_legacy_tables()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot("legacy-cognitive-memory-export-disabled");
        try {
            var reader = new FakeLegacyCognitiveMemoryDataReader([]);
            var service = CreateService(reader);

            var result = await service.ExportAsync(new LegacyCognitiveMemoryExportRequest(
                rootPath,
                "export-1",
                CompatibilityEnabled: false));

            Assert.Equal(LegacyCognitiveMemoryExportResultKind.Disabled, result.Kind);
            Assert.Equal(0, reader.ReadCount);
            Assert.False(Directory.Exists(Path.Combine(rootPath, "export-1")));
        }
        finally {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task ExportAsync_empty_legacy_database_writes_no_data_manifest()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot("legacy-cognitive-memory-export-empty");
        try {
            var reader = new FakeLegacyCognitiveMemoryDataReader([]);
            var service = CreateService(reader);

            var result = await service.ExportAsync(CreateEnabledRequest(rootPath, "empty-export"));

            Assert.Equal(LegacyCognitiveMemoryExportResultKind.NoLegacyData, result.Kind);
            Assert.Equal(1, reader.ReadCount);
            var manifest = await ReadManifestAsync(result.ManifestPath);
            Assert.Equal(LegacyCognitiveMemoryExportResultKind.NoLegacyData, manifest.ResultKind);
            Assert.Equal(0, manifest.TableCount);
            Assert.Equal(0, manifest.RowCount);
            Assert.Equal(LegacyCognitiveMemoryExportConstants.NativeImportPolicy, manifest.NativeImportPolicy);
            Assert.Equal(LegacyCognitiveMemoryExportConstants.RetentionPolicy, manifest.RetentionPolicy);
        }
        finally {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task ExportAsync_populated_legacy_database_writes_data_manifest_and_reference_map()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot("legacy-cognitive-memory-export-populated");
        try {
            var table = new LegacyCognitiveMemoryTableSnapshot(
                "CognitiveMemory_RecallContextPacks",
                ["Id", "MemoryRecordId", "SourceItemId", "CorrelationId", "Text"],
                [
                    new LegacyCognitiveMemoryRow(new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        ["Id"] = "pack-1",
                        ["MemoryRecordId"] = "record-1",
                        ["SourceItemId"] = "source-1",
                        ["CorrelationId"] = "correlation-1",
                        ["Text"] = "Relevant context"
                    })
                ]);
            var reader = new FakeLegacyCognitiveMemoryDataReader([table]);
            var service = CreateService(reader);

            var result = await service.ExportAsync(CreateEnabledRequest(rootPath, "populated-export"));

            Assert.Equal(LegacyCognitiveMemoryExportResultKind.Exported, result.Kind);
            var manifest = await ReadManifestAsync(result.ManifestPath);
            var exportedTable = Assert.Single(manifest.Tables);
            Assert.Equal("CognitiveMemory_RecallContextPacks", exportedTable.TableName);
            Assert.Equal(1, exportedTable.RowCount);
            Assert.Equal(LegacyCognitiveMemoryTableDisposition.ReadOnlyArchive, exportedTable.Disposition);

            var dataPath = Path.Combine(result.ExportDirectory!, exportedTable.DataFile);
            var dataLine = Assert.Single(await File.ReadAllLinesAsync(dataPath));
            Assert.Contains("\"Text\":\"Relevant context\"", dataLine, StringComparison.Ordinal);

            var idMapPath = Path.Combine(result.ExportDirectory!, exportedTable.IdMapFile);
            var idMapLine = Assert.Single(await File.ReadAllLinesAsync(idMapPath));
            var entry = JsonSerializer.Deserialize<LegacyCognitiveMemoryReferenceMapEntry>(idMapLine, JsonOptions)
                ?? throw new InvalidOperationException("Expected an id-map entry.");
            Assert.Equal("pack-1", entry.PrimaryId);
            Assert.Equal("record-1", entry.References["MemoryRecordId"]);
            Assert.Equal("source-1", entry.References["SourceItemId"]);
            Assert.Equal("correlation-1", entry.References["CorrelationId"]);
        }
        finally {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task ExportAsync_duplicate_export_without_overwrite_is_blocked_before_reading()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot("legacy-cognitive-memory-export-duplicate");
        try {
            var firstReader = new FakeLegacyCognitiveMemoryDataReader([]);
            var service = CreateService(firstReader);
            var firstResult = await service.ExportAsync(CreateEnabledRequest(rootPath, "duplicate-export"));
            Assert.Equal(LegacyCognitiveMemoryExportResultKind.NoLegacyData, firstResult.Kind);

            var blockingReader = new FakeLegacyCognitiveMemoryDataReader(
                [],
                new InvalidOperationException("Reader should not be invoked for duplicate exports."));
            var duplicateService = CreateService(blockingReader);

            var duplicateResult = await duplicateService.ExportAsync(CreateEnabledRequest(rootPath, "duplicate-export"));

            Assert.Equal(LegacyCognitiveMemoryExportResultKind.DuplicateBlocked, duplicateResult.Kind);
            Assert.Equal(0, blockingReader.ReadCount);
        }
        finally {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("nested/export")]
    [InlineData("nested\\export")]
    public async Task ExportAsync_unsafe_export_id_is_rejected_before_reading(
        string exportId)
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot("legacy-cognitive-memory-export-unsafe-id");
        try {
            var reader = new FakeLegacyCognitiveMemoryDataReader([]);
            var service = CreateService(reader);

            await Assert.ThrowsAsync<ArgumentException>(() => service.ExportAsync(CreateEnabledRequest(rootPath, exportId)));
            Assert.Equal(0, reader.ReadCount);
            Assert.Empty(Directory.EnumerateFileSystemEntries(rootPath));
        }
        finally {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    [Fact]
    public async Task ExportAsync_partial_reader_failure_writes_failed_manifest()
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot("legacy-cognitive-memory-export-failure");
        try {
            var reader = new FakeLegacyCognitiveMemoryDataReader(
                [],
                new InvalidOperationException("simulated partial export failure"));
            var service = CreateService(reader);

            var result = await service.ExportAsync(CreateEnabledRequest(rootPath, "failed-export"));

            Assert.Equal(LegacyCognitiveMemoryExportResultKind.Failed, result.Kind);
            Assert.Equal(1, reader.ReadCount);
            var manifest = await ReadManifestAsync(result.ManifestPath);
            Assert.Equal(LegacyCognitiveMemoryExportResultKind.Failed, manifest.ResultKind);
            Assert.Equal("simulated partial export failure", manifest.FailureMessage);
            Assert.Equal(0, manifest.TableCount);
            Assert.Equal(0, manifest.RowCount);
        }
        finally {
            TestFileSystem.DeleteDirectoryWithRetry(rootPath);
        }
    }

    private static LegacyCognitiveMemoryExportRequest CreateEnabledRequest(
        string rootPath,
        string exportId)
        => new(
            rootPath,
            exportId,
            CompatibilityEnabled: true,
            OverwriteExisting: false,
            LegacyCognitiveMemoryCompatibilityMode.ReadOnlyArchive);

    private static LegacyCognitiveMemoryExportService CreateService(
        ILegacyCognitiveMemoryDataReader dataReader)
        => new(
            dataReader,
            TestWorkspaceServices.PhysicalPathPolicyFactory,
            new DurableFileWriter(TestWorkspaceServices.PhysicalPathPolicyFactory));

    private static async Task<LegacyCognitiveMemoryExportManifest> ReadManifestAsync(string? manifestPath)
    {
        Assert.False(string.IsNullOrWhiteSpace(manifestPath));
        var json = await File.ReadAllTextAsync(manifestPath);
        return JsonSerializer.Deserialize<LegacyCognitiveMemoryExportManifest>(json, JsonOptions)
            ?? throw new InvalidOperationException("Expected a legacy Cognitive Memory export manifest.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class FakeLegacyCognitiveMemoryDataReader(
        IReadOnlyList<LegacyCognitiveMemoryTableSnapshot> tables,
        Exception? exception = null) : ILegacyCognitiveMemoryDataReader
    {
        public int ReadCount { get; private set; }

        public Task<IReadOnlyList<LegacyCognitiveMemoryTableSnapshot>> ReadLegacyTablesAsync(
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            if (exception is not null) {
                throw exception;
            }

            return Task.FromResult(tables);
        }
    }
}

public sealed class PostgreSqlLegacyCognitiveMemoryDataReaderTests
{
    [Fact]
    public async Task ReadLegacyTablesAsync_reads_only_cognitive_memory_tables_from_postgresql()
    {
        await using var database = PostgresTestDatabaseLease.Create("legacy-cognitive-memory-reader");
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand()) {
            command.CommandText =
                """
                CREATE TABLE "CognitiveMemory_RecallContextPacks" (
                    "Id" text NOT NULL PRIMARY KEY,
                    "MemoryRecordId" text NULL,
                    "SourceItemId" text NULL,
                    "CorrelationId" text NULL,
                    "Text" text NULL
                );

                CREATE TABLE "Projects_Projects" (
                    "Id" text NOT NULL PRIMARY KEY
                );

                INSERT INTO "CognitiveMemory_RecallContextPacks"
                    ("Id", "MemoryRecordId", "SourceItemId", "CorrelationId", "Text")
                VALUES
                    ('pack-1', 'record-1', 'source-1', 'correlation-1', 'Relevant context');

                INSERT INTO "Projects_Projects" ("Id") VALUES ('project-1');
                """;
            await command.ExecuteNonQueryAsync();
        }

        var reader = new PostgreSqlLegacyCognitiveMemoryDataReader(connection);

        var tables = await reader.ReadLegacyTablesAsync();

        var table = Assert.Single(tables);
        Assert.Equal("CognitiveMemory_RecallContextPacks", table.TableName);
        Assert.DoesNotContain(tables, item => item.TableName == "Projects_Projects");

        var row = Assert.Single(table.Rows);
        Assert.Equal("pack-1", row.Values["Id"]);
        Assert.Equal("record-1", row.Values["MemoryRecordId"]);
        Assert.Equal("source-1", row.Values["SourceItemId"]);
        Assert.Equal("correlation-1", row.Values["CorrelationId"]);
        Assert.Equal("Relevant context", row.Values["Text"]);
    }
}
