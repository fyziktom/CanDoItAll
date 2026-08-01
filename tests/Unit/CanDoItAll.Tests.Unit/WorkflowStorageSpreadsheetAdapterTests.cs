using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;
using CanDoItAll.SharedKernel.Configuration;
using CanDoItAll.Tools.Documents;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowStorageSpreadsheetAdapterTests
{
    [Fact]
    public void Added_operations_preserve_existing_numeric_values()
    {
        var storageValues = new Dictionary<WorkflowStorageFileOperation, int>
        {
            [WorkflowStorageFileOperation.List] = 0,
            [WorkflowStorageFileOperation.Exists] = 1,
            [WorkflowStorageFileOperation.Tree] = 2,
            [WorkflowStorageFileOperation.Stat] = 3,
            [WorkflowStorageFileOperation.ReadText] = 4,
            [WorkflowStorageFileOperation.WriteText] = 5,
            [WorkflowStorageFileOperation.AppendText] = 6,
            [WorkflowStorageFileOperation.CreateDirectory] = 7,
            [WorkflowStorageFileOperation.Delete] = 8,
            [WorkflowStorageFileOperation.Copy] = 9,
            [WorkflowStorageFileOperation.Move] = 10,
            [WorkflowStorageFileOperation.Hash] = 11,
            [WorkflowStorageFileOperation.Zip] = 12,
            [WorkflowStorageFileOperation.Unzip] = 13,
            [WorkflowStorageFileOperation.SearchText] = 14,
            [WorkflowStorageFileOperation.DiffText] = 15
        };
        var spreadsheetValues = new Dictionary<WorkflowSpreadsheetOperation, int>
        {
            [WorkflowSpreadsheetOperation.WorkbookSummary] = 0,
            [WorkflowSpreadsheetOperation.ReadCell] = 1,
            [WorkflowSpreadsheetOperation.ReadRange] = 2,
            [WorkflowSpreadsheetOperation.WriteCell] = 3,
            [WorkflowSpreadsheetOperation.WriteRange] = 4,
            [WorkflowSpreadsheetOperation.ApplyBatch] = 5,
            [WorkflowSpreadsheetOperation.RangeToMarkdown] = 6
        };

        Assert.All(storageValues, pair => Assert.Equal(pair.Value, (int)pair.Key));
        Assert.Equal(16, (int)WorkflowStorageFileOperation.ListDirectory);
        Assert.All(spreadsheetValues, pair => Assert.Equal(pair.Value, (int)pair.Key));
        Assert.Equal(7, (int)WorkflowSpreadsheetOperation.Preview);
    }

    [Fact]
    public async Task Storage_list_directory_returns_only_direct_files_and_directories_and_applies_filters()
    {
        using var temp = new TempDirectory();
        var items = Path.Combine(temp.Path, "items");
        Directory.CreateDirectory(Path.Combine(items, "folder"));
        await File.WriteAllTextAsync(Path.Combine(items, "keep.txt"), "keep");
        await File.WriteAllTextAsync(Path.Combine(items, "skip.log"), "skip");
        await File.WriteAllTextAsync(Path.Combine(items, "folder", "nested.txt"), "nested");
        var files = new RecordingWorkspaceFileService(new WorkspaceFileService(temp.Path));

        var result = await ExecuteAsync(
            new WorkspaceFileWorkflowExecutor(files),
            new WorkflowStorageFileExecutorSettings
            {
                Operation = WorkflowStorageFileOperation.ListDirectory,
                Path = "items",
                MaxResults = 10,
                IncludeGlobs = ["items/*"],
                ExcludeGlobs = ["items/skip*"]
            });
        var payload = DeserializePayload<WorkspaceFileListResult>(result);

        Assert.Equal(["items/folder", "items/keep.txt"], payload.Entries.Select(entry => entry.RelativePath));
        Assert.Equal("directory", payload.Entries[0].PathKind);
        Assert.Equal("file", payload.Entries[1].PathKind);
        Assert.DoesNotContain(payload.Entries, entry => entry.RelativePath.Contains("nested", StringComparison.Ordinal));
        Assert.True(payload.IsTruncated);
        Assert.Equal(1, files.ListDirectoryCalls);
        Assert.Equal(0, files.ListFilesCalls);
        Assert.Equal("items", files.LastRelativePath);
        Assert.Equal(10, files.LastMaxResults);
    }

    [Fact]
    public async Task Storage_list_directory_forwards_max_results_and_reports_truncation()
    {
        using var temp = new TempDirectory();
        var items = Path.Combine(temp.Path, "bounded");
        Directory.CreateDirectory(items);
        await File.WriteAllTextAsync(Path.Combine(items, "a.txt"), "a");
        await File.WriteAllTextAsync(Path.Combine(items, "b.txt"), "b");
        await File.WriteAllTextAsync(Path.Combine(items, "c.txt"), "c");
        var files = new RecordingWorkspaceFileService(new WorkspaceFileService(temp.Path));

        var result = await ExecuteAsync(
            new WorkspaceFileWorkflowExecutor(files),
            new WorkflowStorageFileExecutorSettings
            {
                Operation = WorkflowStorageFileOperation.ListDirectory,
                Path = "bounded",
                MaxResults = 1
            });
        var payload = DeserializePayload<WorkspaceFileListResult>(result);

        Assert.Single(payload.Entries);
        Assert.True(payload.IsTruncated);
        Assert.Equal(1, files.ListDirectoryCalls);
        Assert.Equal(0, files.ListFilesCalls);
        Assert.Equal(1, files.LastMaxResults);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("missing")]
    public async Task Storage_list_directory_propagates_traversal_and_service_failures(string path)
    {
        using var temp = new TempDirectory();
        var files = new RecordingWorkspaceFileService(new WorkspaceFileService(temp.Path));
        var executor = new WorkspaceFileWorkflowExecutor(files);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(
            executor,
            new WorkflowStorageFileExecutorSettings
            {
                Operation = WorkflowStorageFileOperation.ListDirectory,
                Path = path
            }));

        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
        Assert.Equal(1, files.ListDirectoryCalls);
        Assert.Equal(0, files.ListFilesCalls);
    }

    [Fact]
    public void Spreadsheet_preview_defaults_and_schema_are_exposed_automatically()
    {
        var descriptor = BuiltInWorkflowExecutorDescriptors.Spreadsheet;
        var defaults = WorkflowExecutorJson.Deserialize<WorkflowSpreadsheetExecutorSettings>(descriptor.DefaultSettingsJson);
        var operationField = Assert.Single(descriptor.ConfigurationSchema.Fields, field => field.Key == "operation");
        var maxWorksheetsField = Assert.Single(descriptor.ConfigurationSchema.Fields, field => field.Key == "maxWorksheets");
        var previewOption = Assert.Single(operationField.Options, option => option.Value == nameof(WorkflowSpreadsheetOperation.Preview));

        Assert.Equal(2, new WorkflowSpreadsheetExecutorSettings().MaxWorksheets);
        Assert.Equal(2, defaults.MaxWorksheets);
        Assert.Equal(ConfigurationFieldType.Number, maxWorksheetsField.FieldType);
        Assert.Contains("7", previewOption.AcceptedValues);
    }

    [Fact]
    public async Task Spreadsheet_preview_resolves_once_and_delegates_once_to_shared_service()
    {
        var fullPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "workflow-preview.xlsx"));
        var paths = new RecordingPathResolutionService(fullPath);
        var documents = new RecordingSpreadsheetDocumentService();
        var executor = new SpreadsheetWorkflowExecutor(documents, paths);

        var result = await ExecuteAsync(
            executor,
            new WorkflowSpreadsheetExecutorSettings
            {
                Operation = WorkflowSpreadsheetOperation.Preview,
                WorkbookPath = "  inputs/workbook.xlsx  ",
                MaxWorksheets = 3,
                MaxRows = 4,
                MaxColumns = 5
            });
        var payload = DeserializePayload<SpreadsheetWorkbookPreviewResult>(result);

        Assert.Equal(fullPath, payload.WorkbookPath);
        Assert.Equal(1, paths.ResolveFilePathCalls);
        Assert.Equal("inputs/workbook.xlsx", paths.LastPath);
        Assert.False(paths.LastAllowMissing);
        Assert.Equal(1, documents.PreviewCalls);
        var request = Assert.IsType<SpreadsheetWorkbookPreviewRequest>(documents.LastPreviewRequest);
        Assert.Equal(fullPath, request.WorkbookPath);
        Assert.Equal(3, request.MaxWorksheets);
        Assert.Equal(4, request.MaxRows);
        Assert.Equal(5, request.MaxColumns);
    }

    [Fact]
    public async Task Spreadsheet_preview_propagates_shared_service_failure()
    {
        var expected = new InvalidDataException("preview failed");
        var paths = new RecordingPathResolutionService(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "workflow-preview-failure.xlsx")));
        var documents = new RecordingSpreadsheetDocumentService { PreviewException = expected };
        var executor = new SpreadsheetWorkflowExecutor(documents, paths);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => ExecuteAsync(
            executor,
            new WorkflowSpreadsheetExecutorSettings
            {
                Operation = WorkflowSpreadsheetOperation.Preview,
                WorkbookPath = "inputs/workbook.xlsx"
            }));

        Assert.Same(expected, exception);
        Assert.Equal(1, paths.ResolveFilePathCalls);
        Assert.Equal(1, documents.PreviewCalls);
    }

    private static T DeserializePayload<T>(WorkflowNodeExecutionResult result)
        => JsonSerializer.Deserialize<T>(result.PayloadJson, WorkflowExecutorJson.Options)
           ?? throw new InvalidOperationException($"Workflow payload could not be deserialized as {typeof(T).Name}.");

    private static async Task<WorkflowNodeExecutionResult> ExecuteAsync<TSettings>(
        IWorkflowExecutor executor,
        TSettings settings)
    {
        var settingsJson = WorkflowExecutorJson.Serialize(settings);
        var node = new WorkflowNode(
            new WorkflowNodeId("adapter"),
            WorkflowNodeKind.Executor,
            "adapter",
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: executor.Descriptor.InputShape,
                ResultShape: executor.Descriptor.ResultShape)
            {
                ExecutorId = executor.Descriptor.Id,
                ExecutorSettingsJson = settingsJson
            });
        var context = new WorkflowExecutorExecutionContext(
            Definition: null!,
            node,
            executor.Descriptor,
            settingsJson,
            WorkflowExecutorExecutionPolicy.Default);

        return await executor.ExecuteAsync(context, new WorkflowNodeInput("{}"));
    }

    private sealed class RecordingWorkspaceFileService(IWorkspaceFileService inner) : IWorkspaceFileService
    {
        public int ListDirectoryCalls { get; private set; }

        public int ListFilesCalls { get; private set; }

        public string? LastRelativePath { get; private set; }

        public int LastMaxResults { get; private set; }

        public WorkspaceFileListResult ListDirectory(string? relativePath = null, int maxResults = 100)
        {
            ListDirectoryCalls++;
            LastRelativePath = relativePath;
            LastMaxResults = maxResults;
            return inner.ListDirectory(relativePath, maxResults);
        }

        public WorkspaceFileListResult ListFiles(string? relativePath = null, string searchPattern = "*", int maxResults = 100)
        {
            ListFilesCalls++;
            throw new InvalidOperationException("ListDirectory must not delegate to ListFiles.");
        }

        public WorkspaceTextSearchResult SearchText(string query, string? relativePath = null, int maxResults = 20)
            => throw new NotSupportedException();

        public WorkspaceTextFileReadResult ReadTextFile(string path, int maxCharacters = 12000)
            => throw new NotSupportedException();

        public WorkspacePathStatResult StatPath(string path)
            => throw new NotSupportedException();

        public WorkspacePathHashResult HashPath(string path, int maxFiles = 200, long maxBytes = 10485760)
            => throw new NotSupportedException();

        public WorkspaceFileMutationResult CreateDirectory(string path)
            => throw new NotSupportedException();

        public WorkspaceFileMutationResult WriteTextFile(string path, string content, bool overwrite = true)
            => throw new NotSupportedException();

        public WorkspaceFileMutationResult AppendTextFile(string path, string content)
            => throw new NotSupportedException();

        public WorkspaceFileMutationResult CopyPath(string sourcePath, string destinationPath, bool overwrite = false)
            => throw new NotSupportedException();

        public WorkspaceFileMutationResult MovePath(string sourcePath, string destinationPath, bool overwrite = false)
            => throw new NotSupportedException();

        public WorkspaceFileMutationResult DeletePath(string path, bool recursive = false)
            => throw new NotSupportedException();

        public WorkspaceArchiveMutationResult ZipPath(string sourcePath, string destinationPath, bool overwrite = false, int maxFiles = 200, long maxBytes = 10485760)
            => throw new NotSupportedException();

        public WorkspaceArchiveMutationResult UnzipArchive(string sourcePath, string destinationPath, bool overwrite = false, int maxFiles = 200, long maxBytes = 10485760)
            => throw new NotSupportedException();

        public WorkspaceTextDiffResult DiffTextFiles(string leftPath, string rightPath, int maxLines = 160)
            => throw new NotSupportedException();
    }

    private sealed class RecordingPathResolutionService(string fullPath) : IWorkspacePathResolutionService
    {
        public int ResolveFilePathCalls { get; private set; }

        public string LastPath { get; private set; } = string.Empty;

        public bool LastAllowMissing { get; private set; }

        public WorkspaceResolvedPath ResolveFilePath(string path, bool allowMissing)
        {
            ResolveFilePathCalls++;
            LastPath = path;
            LastAllowMissing = allowMissing;
            return new WorkspaceResolvedPath(fullPath, path, IsWorkspacePath: true);
        }

        public WorkspaceResolvedPath ResolveDirectoryPath(string path, bool allowMissing)
            => throw new NotSupportedException();
    }

    private sealed class RecordingSpreadsheetDocumentService : ISpreadsheetDocumentService
    {
        public int PreviewCalls { get; private set; }

        public SpreadsheetWorkbookPreviewRequest? LastPreviewRequest { get; private set; }

        public Exception? PreviewException { get; init; }

        public SpreadsheetWorkbookSummary InspectWorkbook(string workbookPath)
            => throw new NotSupportedException();

        public SpreadsheetWorkbookPreviewResult PreviewWorkbook(SpreadsheetWorkbookPreviewRequest request)
        {
            PreviewCalls++;
            LastPreviewRequest = request;
            if (PreviewException is not null)
            {
                throw PreviewException;
            }

            return new SpreadsheetWorkbookPreviewResult(
                request.WorkbookPath,
                TotalWorksheetCount: 0,
                Worksheets: [],
                WorksheetsTruncated: false);
        }

        public SpreadsheetCellValue ReadCell(string workbookPath, string worksheetName, string cellAddress)
            => throw new NotSupportedException();

        public SpreadsheetRangeReadResult ReadRange(
            string workbookPath,
            string worksheetName,
            string rangeAddress,
            int maxRows,
            int maxColumns)
            => throw new NotSupportedException();

        public SpreadsheetWriteResult Write(SpreadsheetWriteRequest request)
            => throw new NotSupportedException();
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"candoitall-workflow-adapter-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
