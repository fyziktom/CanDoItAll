using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tools.Documents;

namespace CanDoItAll.Tests.Unit;

public sealed class SpreadsheetPreviewTests
{
    [Fact]
    public void PreviewWorkbookReturnsBoundedTypedPreviewAndPreservesFormulas()
    {
        using var temp = new TempDirectory();
        var workbookPath = Path.Combine(temp.Path, "bounded.xlsx");
        var service = new ClosedXmlSpreadsheetDocumentService();
        WriteWorksheet(
            service,
            workbookPath,
            "First",
            [
                ["Name", "Total", "Extra"],
                ["Alpha", "=SUM(1,2)", "Ignored"],
                ["Omega", "9", "Ignored"]
            ],
            createWorkbookIfMissing: true);
        WriteWorksheet(service, workbookPath, "Second", [["Second"]]);
        WriteWorksheet(service, workbookPath, "Third", [["Third"]]);

        var result = service.PreviewWorkbook(new SpreadsheetWorkbookPreviewRequest(
            workbookPath,
            MaxWorksheets: 2,
            MaxRows: 2,
            MaxColumns: 2));

        Assert.Equal(workbookPath, result.WorkbookPath);
        Assert.Equal(3, result.TotalWorksheetCount);
        Assert.Equal(2, result.Worksheets.Count);
        Assert.True(result.WorksheetsTruncated);
        Assert.True(result.IsTruncated);

        var first = result.Worksheets[0];
        Assert.Equal("First", first.Name);
        Assert.Equal(1, first.Position);
        Assert.Equal("A1:C3", first.UsedRangeAddress);
        Assert.Equal(3, first.UsedRowCount);
        Assert.Equal(3, first.UsedColumnCount);
        Assert.Equal(2, first.Values.Count);
        Assert.All(first.Values, row => Assert.Equal(2, row.Count));
        Assert.Equal("=SUM(1,2)", first.Values[1][1]);
        Assert.Contains("| Name | Total |", first.MarkdownTable, StringComparison.Ordinal);
        Assert.True(first.RowsTruncated);
        Assert.True(first.ColumnsTruncated);
        Assert.True(first.IsTruncated);
    }

    [Fact]
    public void PreviewWorkbookReturnsEmptyPreviewForEmptyWorksheet()
    {
        using var temp = new TempDirectory();
        var workbookPath = Path.Combine(temp.Path, "empty.xlsx");
        var service = new ClosedXmlSpreadsheetDocumentService();
        WriteWorksheet(service, workbookPath, "Empty", [], createWorkbookIfMissing: true);

        var result = service.PreviewWorkbook(new SpreadsheetWorkbookPreviewRequest(workbookPath));

        var worksheet = Assert.Single(result.Worksheets);
        Assert.Equal(string.Empty, worksheet.UsedRangeAddress);
        Assert.Equal(0, worksheet.UsedRowCount);
        Assert.Equal(0, worksheet.UsedColumnCount);
        Assert.Empty(worksheet.Values);
        Assert.Equal(string.Empty, worksheet.MarkdownTable);
        Assert.False(worksheet.IsTruncated);
        Assert.False(result.IsTruncated);
    }

    [Fact]
    public void PreviewWorkbookReportsRowAndColumnTruncationIndependently()
    {
        using var temp = new TempDirectory();
        var workbookPath = Path.Combine(temp.Path, "dimensions.xlsx");
        var service = new ClosedXmlSpreadsheetDocumentService();
        WriteWorksheet(
            service,
            workbookPath,
            "Dimensions",
            [["A", "B"], ["C", "D"]],
            createWorkbookIfMissing: true);

        var rowBound = Assert.Single(service.PreviewWorkbook(new SpreadsheetWorkbookPreviewRequest(
            workbookPath,
            MaxWorksheets: 1,
            MaxRows: 1,
            MaxColumns: 2)).Worksheets);
        var columnBound = Assert.Single(service.PreviewWorkbook(new SpreadsheetWorkbookPreviewRequest(
            workbookPath,
            MaxWorksheets: 1,
            MaxRows: 2,
            MaxColumns: 1)).Worksheets);

        Assert.True(rowBound.RowsTruncated);
        Assert.False(rowBound.ColumnsTruncated);
        Assert.False(columnBound.RowsTruncated);
        Assert.True(columnBound.ColumnsTruncated);
    }

    [Theory]
    [InlineData(0, 1, 1, nameof(SpreadsheetWorkbookPreviewRequest.MaxWorksheets))]
    [InlineData(1, 0, 1, nameof(SpreadsheetWorkbookPreviewRequest.MaxRows))]
    [InlineData(1, 1, 0, nameof(SpreadsheetWorkbookPreviewRequest.MaxColumns))]
    [InlineData(101, 1, 1, nameof(SpreadsheetWorkbookPreviewRequest.MaxWorksheets))]
    [InlineData(1, 1001, 1, nameof(SpreadsheetWorkbookPreviewRequest.MaxRows))]
    [InlineData(1, 1, 101, nameof(SpreadsheetWorkbookPreviewRequest.MaxColumns))]
    public void PreviewWorkbookRejectsOutOfRangeBounds(
        int maxWorksheets,
        int maxRows,
        int maxColumns,
        string expectedParameterName)
    {
        var service = new ClosedXmlSpreadsheetDocumentService();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => service.PreviewWorkbook(
            new SpreadsheetWorkbookPreviewRequest("unused.xlsx", maxWorksheets, maxRows, maxColumns)));

        Assert.Equal(expectedParameterName, exception.ParamName);
    }

    [Fact]
    public void PreviewWorkbookReportsMissingWorkbook()
    {
        using var temp = new TempDirectory();
        var missingPath = Path.Combine(temp.Path, "missing.xlsx");
        var service = new ClosedXmlSpreadsheetDocumentService();

        var exception = Assert.Throws<FileNotFoundException>(() => service.PreviewWorkbook(
            new SpreadsheetWorkbookPreviewRequest(missingPath)));

        Assert.Equal(missingPath, exception.FileName);
    }

    [Fact]
    public void PreviewWorkbookReportsMalformedWorkbook()
    {
        using var temp = new TempDirectory();
        var malformedPath = Path.Combine(temp.Path, "malformed.xlsx");
        File.WriteAllText(malformedPath, "not an xlsx workbook");
        var service = new ClosedXmlSpreadsheetDocumentService();

        var exception = Assert.Throws<InvalidDataException>(() => service.PreviewWorkbook(
            new SpreadsheetWorkbookPreviewRequest(malformedPath)));

        Assert.Contains("could not be opened", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void RuntimePluginDelegatesPreviewAndPersistsReadReceipt()
    {
        using var temp = new TempDirectory();
        var workbookPath = Path.Combine(temp.Path, "delegated.xlsx");
        File.WriteAllBytes(workbookPath, [0]);
        var documents = new RecordingSpreadsheetDocumentService();
        var plugin = CreatePlugin(temp.Path, documents, canReadFiles: true);
        var run = CreateExecutionRunRecord();

        SpreadsheetWorkbookPreviewResult result;
        using (WorkspaceExecutionAuditContext.BeginScope(run))
        {
            result = plugin.PreviewWorkbook(
                "delegated.xlsx",
                maxWorksheets: 1,
                maxRows: 2,
                maxColumns: 3);
        }

        Assert.Equal("delegated.xlsx", result.WorkbookPath);
        Assert.Equal(1, documents.PreviewCallCount);
        Assert.NotNull(documents.PreviewRequest);
        Assert.Equal(Path.GetFullPath(workbookPath), documents.PreviewRequest.WorkbookPath);
        Assert.Equal(1, documents.PreviewRequest.MaxWorksheets);
        Assert.Equal(2, documents.PreviewRequest.MaxRows);
        Assert.Equal(3, documents.PreviewRequest.MaxColumns);

        var receipt = Assert.Single(ReadReceipts(temp.Path, run.Id));
        Assert.Equal("workspace_spreadsheet_preview", receipt.ToolName);
        Assert.Equal("ReadOnlyWorkspace", receipt.RiskClass);
        Assert.Contains("maxWorksheets=1", receipt.RequestSummary, StringComparison.Ordinal);
        Assert.Contains("maxRows=2", receipt.RequestSummary, StringComparison.Ordinal);
        Assert.Contains("maxColumns=3", receipt.RequestSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimePluginDeniesPreviewWithoutFileReadAccessBeforeDelegation()
    {
        using var temp = new TempDirectory();
        File.WriteAllBytes(Path.Combine(temp.Path, "denied.xlsx"), [0]);
        var documents = new RecordingSpreadsheetDocumentService();
        var plugin = CreatePlugin(temp.Path, documents, canReadFiles: false);

        var exception = Assert.Throws<InvalidOperationException>(() => plugin.PreviewWorkbook("denied.xlsx"));

        Assert.Contains("not allowed to read workspace files", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, documents.PreviewCallCount);
    }

    [Fact]
    public void RuntimePluginDeniesOutOfScopePreviewBeforeDelegation()
    {
        using var parent = new TempDirectory();
        var workspaceRoot = Path.Combine(parent.Path, "workspace");
        Directory.CreateDirectory(workspaceRoot);
        File.WriteAllBytes(Path.Combine(parent.Path, "outside.xlsx"), [0]);
        var documents = new RecordingSpreadsheetDocumentService();
        var plugin = CreatePlugin(workspaceRoot, documents, canReadFiles: true);

        Assert.Throws<InvalidOperationException>(() => plugin.PreviewWorkbook("../outside.xlsx"));
        Assert.Equal(0, documents.PreviewCallCount);
    }

    private static void WriteWorksheet(
        ISpreadsheetDocumentService service,
        string workbookPath,
        string worksheetName,
        IReadOnlyList<IReadOnlyList<string>> values,
        bool createWorkbookIfMissing = false)
    {
        var rangeWrites = values.Count == 0
            ? Array.Empty<SpreadsheetRangeWrite>()
            : [new SpreadsheetRangeWrite(BuildRangeAddress(values), values)];
        service.Write(new SpreadsheetWriteRequest(
            workbookPath,
            workbookPath,
            worksheetName,
            [],
            rangeWrites,
            createWorkbookIfMissing,
            Overwrite: true));
    }

    private static string BuildRangeAddress(IReadOnlyList<IReadOnlyList<string>> values)
    {
        var columnCount = values.Max(row => row.Count);
        Assert.InRange(columnCount, 1, 26);
        return $"A1:{(char)('A' + columnCount - 1)}{values.Count}";
    }

    private static WorkspaceSpreadsheetRuntimePlugin CreatePlugin(
        string workspaceRoot,
        ISpreadsheetDocumentService documents,
        bool canReadFiles)
        => new(
            documents,
            workspaceRoot,
            WorkspaceScopeDescriptor.Sandbox,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = canReadFiles,
                CanWriteFiles = false,
                CanTransformArtifacts = false
            });

    private static IReadOnlyList<ToolExecutionReceiptRecord> ReadReceipts(string workspaceRoot, Guid runId)
    {
        var receiptRoot = Path.Combine(
            workspaceRoot,
            "data",
            "execution",
            "runs",
            runId.ToString("N"),
            "audit",
            "receipts");
        return Directory.EnumerateFiles(receiptRoot, "*.json")
            .Select(path => JsonSerializer.Deserialize<ToolExecutionReceiptRecord>(
                File.ReadAllText(path),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)))
            .OfType<ToolExecutionReceiptRecord>()
            .ToArray();
    }

    private static ExecutionRunRecord CreateExecutionRunRecord()
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Spreadsheet preview receipt test",
            SourceKind: "unit-test",
            SourceId: "spreadsheet-preview-receipt",
            CorrelationId: "spreadsheet-preview-receipt",
            CausationId: string.Empty,
            RequestedBy: "unit-test",
            RequestedByKind: "system",
            MetadataJson: "{}",
            InputSummary: "Input",
            ResultSummary: string.Empty,
            ProviderName: "Provider",
            Model: "model",
            State: ExecutionState.Running,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: []);
    }

    private sealed class RecordingSpreadsheetDocumentService : ISpreadsheetDocumentService
    {
        public int PreviewCallCount { get; private set; }

        public SpreadsheetWorkbookPreviewRequest? PreviewRequest { get; private set; }

        public SpreadsheetWorkbookPreviewResult PreviewWorkbook(SpreadsheetWorkbookPreviewRequest request)
        {
            PreviewCallCount++;
            PreviewRequest = request;
            return new SpreadsheetWorkbookPreviewResult(
                request.WorkbookPath,
                TotalWorksheetCount: 1,
                [
                    new SpreadsheetWorksheetPreview(
                        "Delegated",
                        Position: 1,
                        UsedRangeAddress: "A1",
                        UsedRowCount: 1,
                        UsedColumnCount: 1,
                        Values: [["value"]],
                        MarkdownTable: "| value |\n| --- |",
                        RowsTruncated: false,
                        ColumnsTruncated: false)
                ],
                WorksheetsTruncated: false);
        }

        public SpreadsheetWorkbookSummary InspectWorkbook(string workbookPath)
            => throw new NotSupportedException();

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
                $"candoitall-spreadsheet-preview-{Guid.NewGuid():N}");
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
