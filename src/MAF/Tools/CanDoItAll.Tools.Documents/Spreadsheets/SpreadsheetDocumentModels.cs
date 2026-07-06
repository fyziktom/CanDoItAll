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

public interface ISpreadsheetDocumentService
{
    SpreadsheetWorkbookSummary InspectWorkbook(string workbookPath);

    SpreadsheetCellValue ReadCell(string workbookPath, string worksheetName, string cellAddress);

    SpreadsheetRangeReadResult ReadRange(
        string workbookPath,
        string worksheetName,
        string rangeAddress,
        int maxRows,
        int maxColumns);

    SpreadsheetWriteResult Write(SpreadsheetWriteRequest request);
}
