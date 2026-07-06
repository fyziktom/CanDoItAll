using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace CanDoItAll.Tools.Documents;

public sealed class ClosedXmlSpreadsheetDocumentService : ISpreadsheetDocumentService
{
    public SpreadsheetWorkbookSummary InspectWorkbook(string workbookPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workbookPath);
        using var workbook = new XLWorkbook(workbookPath);
        var worksheets = workbook.Worksheets
            .Select((worksheet, index) =>
            {
                var usedRange = worksheet.RangeUsed();
                return new SpreadsheetWorksheetSummary(
                    worksheet.Name,
                    index + 1,
                    usedRange?.RangeAddress.ToStringRelative() ?? string.Empty,
                    usedRange?.RowCount() ?? 0,
                    usedRange?.ColumnCount() ?? 0);
            })
            .ToArray();

        return new SpreadsheetWorkbookSummary(workbookPath, worksheets);
    }

    public SpreadsheetCellValue ReadCell(string workbookPath, string worksheetName, string cellAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workbookPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(worksheetName);
        ArgumentException.ThrowIfNullOrWhiteSpace(cellAddress);

        using var workbook = new XLWorkbook(workbookPath);
        var worksheet = GetWorksheet(workbook, worksheetName);
        var cell = worksheet.Cell(cellAddress);

        return new SpreadsheetCellValue(
            cell.Address.ToStringRelative(),
            CellToString(cell));
    }

    public SpreadsheetRangeReadResult ReadRange(
        string workbookPath,
        string worksheetName,
        string rangeAddress,
        int maxRows,
        int maxColumns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workbookPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(worksheetName);
        ArgumentException.ThrowIfNullOrWhiteSpace(rangeAddress);

        var rowLimit = Math.Clamp(maxRows, 1, 1000);
        var columnLimit = Math.Clamp(maxColumns, 1, 100);
        using var workbook = new XLWorkbook(workbookPath);
        var worksheet = GetWorksheet(workbook, worksheetName);
        var range = worksheet.Range(rangeAddress);
        var rowCount = Math.Min(range.RowCount(), rowLimit);
        var columnCount = Math.Min(range.ColumnCount(), columnLimit);
        var rows = new List<IReadOnlyList<string>>(rowCount);

        for (var rowIndex = 1; rowIndex <= rowCount; rowIndex++)
        {
            var row = new List<string>(columnCount);
            for (var columnIndex = 1; columnIndex <= columnCount; columnIndex++)
            {
                row.Add(CellToString(range.Cell(rowIndex, columnIndex)));
            }

            rows.Add(row);
        }

        return new SpreadsheetRangeReadResult(
            workbookPath,
            worksheet.Name,
            range.RangeAddress.ToStringRelative(),
            rows,
            BuildMarkdownTable(rows));
    }

    public SpreadsheetWriteResult Write(SpreadsheetWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkbookPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorksheetName);

        var outputPath = string.IsNullOrWhiteSpace(request.OutputWorkbookPath)
            ? request.WorkbookPath
            : request.OutputWorkbookPath;
        if (File.Exists(outputPath) && !request.Overwrite)
        {
            throw new InvalidOperationException($"Spreadsheet output path '{outputPath}' already exists.");
        }

        using var workbook = File.Exists(request.WorkbookPath)
            ? new XLWorkbook(request.WorkbookPath)
            : CreateWorkbook(request);
        var worksheet = workbook.Worksheets.FirstOrDefault(item => string.Equals(item.Name, request.WorksheetName, StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.Add(request.WorksheetName);

        foreach (var cellWrite in request.CellWrites)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(cellWrite.CellAddress);
            WriteCellValue(worksheet.Cell(cellWrite.CellAddress), cellWrite.Value);
        }

        foreach (var rangeWrite in request.RangeWrites)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rangeWrite.RangeAddress);
            var range = worksheet.Range(rangeWrite.RangeAddress);
            WriteRange(range, rangeWrite.Values);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        workbook.SaveAs(outputPath);

        return new SpreadsheetWriteResult(
            outputPath,
            worksheet.Name,
            request.CellWrites.Count,
            request.RangeWrites.Count);
    }

    private static XLWorkbook CreateWorkbook(SpreadsheetWriteRequest request)
    {
        if (!request.CreateWorkbookIfMissing)
        {
            throw new FileNotFoundException($"Spreadsheet workbook '{request.WorkbookPath}' was not found.", request.WorkbookPath);
        }

        return new XLWorkbook();
    }

    private static IXLWorksheet GetWorksheet(XLWorkbook workbook, string worksheetName)
    {
        return workbook.Worksheets.FirstOrDefault(item => string.Equals(item.Name, worksheetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Spreadsheet worksheet '{worksheetName}' was not found.");
    }

    private static void WriteRange(IXLRange range, IReadOnlyList<IReadOnlyList<string>> values)
    {
        if (values.Count > range.RowCount())
        {
            throw new InvalidOperationException($"Spreadsheet range '{range.RangeAddress.ToStringRelative()}' has {range.RowCount()} row(s), but {values.Count} row(s) were supplied.");
        }

        for (var rowIndex = 0; rowIndex < values.Count; rowIndex++)
        {
            var row = values[rowIndex];
            if (row.Count > range.ColumnCount())
            {
                throw new InvalidOperationException($"Spreadsheet range '{range.RangeAddress.ToStringRelative()}' has {range.ColumnCount()} column(s), but row {rowIndex + 1} supplied {row.Count} value(s).");
            }

            for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                WriteCellValue(range.Cell(rowIndex + 1, columnIndex + 1), row[columnIndex]);
            }
        }
    }

    private static void WriteCellValue(IXLCell cell, string value)
    {
        var normalized = value ?? string.Empty;
        if (normalized.Length > 1 && normalized.StartsWith('='))
        {
            cell.FormulaA1 = normalized[1..];
            return;
        }

        cell.Value = normalized;
    }

    private static string BuildMarkdownTable(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (rows.Count == 0)
        {
            return string.Empty;
        }

        var columnCount = rows.Max(row => row.Count);
        var builder = new StringBuilder();
        AppendMarkdownRow(builder, NormalizeRow(rows[0], columnCount));
        AppendMarkdownRow(builder, Enumerable.Repeat("---", columnCount));

        foreach (var row in rows.Skip(1))
        {
            AppendMarkdownRow(builder, NormalizeRow(row, columnCount));
        }

        return builder.ToString().TrimEnd();
    }

    private static IReadOnlyList<string> NormalizeRow(IReadOnlyList<string> row, int columnCount)
    {
        var values = new string[columnCount];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = index < row.Count ? row[index] : string.Empty;
        }

        return values;
    }

    private static void AppendMarkdownRow(StringBuilder builder, IEnumerable<string> cells)
    {
        builder.Append('|');
        foreach (var cell in cells)
        {
            builder
                .Append(' ')
                .Append(EscapeMarkdownCell(cell))
                .Append(" |");
        }

        builder.AppendLine();
    }

    private static string EscapeMarkdownCell(string value)
        => value.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");

    private static string CellToString(IXLCell cell)
    {
        if (cell.HasFormula)
        {
            return "=" + cell.FormulaA1;
        }

        if (cell.IsEmpty())
        {
            return string.Empty;
        }

        return cell.Value.Type switch
        {
            XLDataType.Blank => string.Empty,
            XLDataType.Boolean => cell.GetBoolean().ToString(CultureInfo.InvariantCulture),
            XLDataType.Number => cell.GetDouble().ToString(CultureInfo.InvariantCulture),
            XLDataType.DateTime => cell.GetDateTime().ToString("O", CultureInfo.InvariantCulture),
            XLDataType.TimeSpan => cell.GetTimeSpan().ToString(),
            _ => cell.GetFormattedString()
        };
    }
}
