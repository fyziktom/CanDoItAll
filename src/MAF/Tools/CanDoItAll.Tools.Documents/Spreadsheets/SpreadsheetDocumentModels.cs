namespace CanDoItAll.Tools.Documents;

public sealed record SpreadsheetWorksheetSummary(
    string Name,
    int Position,
    string UsedRangeAddress,
    int UsedRowCount,
    int UsedColumnCount);

public sealed record SpreadsheetWorkbookSummary(
    string WorkbookPath,
    IReadOnlyList<SpreadsheetWorksheetSummary> Worksheets);

public sealed record SpreadsheetWorkbookPreviewRequest(
    string WorkbookPath,
    int MaxWorksheets = 2,
    int MaxRows = 8,
    int MaxColumns = 8)
{
    public const int MaximumWorksheets = 100;
    public const int MaximumRows = 1000;
    public const int MaximumColumns = 100;
}

public sealed record SpreadsheetWorkbookContentPreviewRequest(
    string WorkbookName,
    ReadOnlyMemory<byte> Content,
    int MaxWorksheets = 2,
    int MaxRows = 8,
    int MaxColumns = 8)
{
    public const int MaximumContentBytes = 16 * 1024 * 1024;
    public const int MaximumArchiveEntries = 2048;
    public const long MaximumExpandedBytes = 64L * 1024L * 1024L;
    public const long MaximumXmlPartBytes = 16L * 1024L * 1024L;
    public const long MaximumWorksheetXmlBytes = 32L * 1024L * 1024L;
    public const long MaximumStylesXmlBytes = 4L * 1024L * 1024L;
    public const long MaximumSharedStringsXmlBytes = 16L * 1024L * 1024L;
    public const int MaximumPackageWorksheets = 256;
    public const int MaximumPackageCells = 250_000;
    public const int MaximumPackageStyles = 20_000;
    public const int MaximumPackageSharedStrings = 250_000;
}

public sealed record SpreadsheetWorksheetPreview(
    string Name,
    int Position,
    string UsedRangeAddress,
    int UsedRowCount,
    int UsedColumnCount,
    IReadOnlyList<IReadOnlyList<string>> Values,
    string MarkdownTable,
    bool RowsTruncated,
    bool ColumnsTruncated)
{
    public bool IsTruncated => RowsTruncated || ColumnsTruncated;
}

public sealed record SpreadsheetWorkbookPreviewResult(
    string WorkbookPath,
    int TotalWorksheetCount,
    IReadOnlyList<SpreadsheetWorksheetPreview> Worksheets,
    bool WorksheetsTruncated)
{
    public bool IsTruncated => WorksheetsTruncated || Worksheets.Any(worksheet => worksheet.IsTruncated);
}

public sealed record SpreadsheetWorkbookContentPreviewResult(
    string DisplayName,
    int TotalWorksheetCount,
    IReadOnlyList<SpreadsheetWorksheetPreview> Worksheets,
    bool WorksheetsTruncated)
{
    public bool IsTruncated => WorksheetsTruncated || Worksheets.Any(worksheet => worksheet.IsTruncated);
}

public sealed record SpreadsheetCellValue(
    string Address,
    string Value);

public sealed record SpreadsheetRangeReadResult(
    string WorkbookPath,
    string WorksheetName,
    string RangeAddress,
    IReadOnlyList<IReadOnlyList<string>> Values,
    string MarkdownTable);

public sealed record SpreadsheetCellWrite(
    string CellAddress,
    string Value);

public sealed record SpreadsheetRangeWrite(
    string RangeAddress,
    IReadOnlyList<IReadOnlyList<string>> Values);

public sealed record SpreadsheetFunctionDescriptor(
    string Name,
    string Category,
    string Syntax,
    string Example,
    string Description,
    IReadOnlyList<string> Notes);

public sealed record SpreadsheetWriteRequest(
    string WorkbookPath,
    string OutputWorkbookPath,
    string WorksheetName,
    IReadOnlyList<SpreadsheetCellWrite> CellWrites,
    IReadOnlyList<SpreadsheetRangeWrite> RangeWrites,
    bool CreateWorkbookIfMissing,
    bool Overwrite);

public sealed record SpreadsheetWriteResult(
    string WorkbookPath,
    string WorksheetName,
    int CellWriteCount,
    int RangeWriteCount);

public interface ISpreadsheetWorkbookContentPreviewService
{
    SpreadsheetWorkbookContentPreviewResult PreviewWorkbook(
        SpreadsheetWorkbookContentPreviewRequest request);
}

public interface ISpreadsheetDocumentService : ISpreadsheetWorkbookContentPreviewService
{
    SpreadsheetWorkbookSummary InspectWorkbook(string workbookPath);

    SpreadsheetWorkbookPreviewResult PreviewWorkbook(SpreadsheetWorkbookPreviewRequest request);

    SpreadsheetCellValue ReadCell(string workbookPath, string worksheetName, string cellAddress);

    SpreadsheetRangeReadResult ReadRange(
        string workbookPath,
        string worksheetName,
        string rangeAddress,
        int maxRows,
        int maxColumns);

    SpreadsheetWriteResult Write(SpreadsheetWriteRequest request);
}
