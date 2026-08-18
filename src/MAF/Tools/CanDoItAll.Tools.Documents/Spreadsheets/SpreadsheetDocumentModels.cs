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
    string Value)
{
    internal SpreadsheetScalarWriteValue? ScalarValue { get; init; }

    public SpreadsheetCellWrite WithScalarValue(SpreadsheetScalarWriteValue scalarValue)
    {
        ArgumentNullException.ThrowIfNull(scalarValue);
        return this with { ScalarValue = scalarValue };
    }
}

public sealed record SpreadsheetRangeWrite(
    string RangeAddress,
    IReadOnlyList<IReadOnlyList<string>> Values)
{
    internal IReadOnlyList<IReadOnlyList<SpreadsheetScalarWriteValue>>? ScalarValues { get; init; }

    public SpreadsheetRangeWrite WithScalarValues(
        IReadOnlyList<IReadOnlyList<SpreadsheetScalarWriteValue>> scalarValues)
    {
        ArgumentNullException.ThrowIfNull(scalarValues);
        if (Values is null || scalarValues.Count != Values.Count)
        {
            throw new ArgumentException(
                "Scalar spreadsheet rows must match the existing string-value rows.",
                nameof(scalarValues));
        }

        for (var rowIndex = 0; rowIndex < scalarValues.Count; rowIndex++)
        {
            IReadOnlyList<SpreadsheetScalarWriteValue>? scalarRow = scalarValues[rowIndex];
            IReadOnlyList<string>? valueRow = Values[rowIndex];
            if (scalarRow is null || valueRow is null || scalarRow.Count != valueRow.Count || scalarRow.Any(static value => value is null))
            {
                throw new ArgumentException(
                    "Each scalar spreadsheet row must match its existing string-value row and contain no null scalar descriptors.",
                    nameof(scalarValues));
            }
        }

        return this with { ScalarValues = scalarValues };
    }
}

public enum SpreadsheetScalarWriteValueKind
{
    Blank,
    Text,
    Integer,
    Decimal,
    FloatingPoint,
    Boolean
}

public sealed class SpreadsheetScalarWriteValue
{
    private SpreadsheetScalarWriteValue(
        SpreadsheetScalarWriteValueKind kind,
        object? value)
    {
        Kind = kind;
        Value = value;
    }

    public static SpreadsheetScalarWriteValue Blank { get; } = new(
        SpreadsheetScalarWriteValueKind.Blank,
        value: null);

    public SpreadsheetScalarWriteValueKind Kind { get; }

    internal object? Value { get; }

    public static SpreadsheetScalarWriteValue FromText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new SpreadsheetScalarWriteValue(SpreadsheetScalarWriteValueKind.Text, value);
    }

    public static SpreadsheetScalarWriteValue FromInteger(long value)
        => new(SpreadsheetScalarWriteValueKind.Integer, value);

    public static SpreadsheetScalarWriteValue FromDecimal(decimal value)
        => new(SpreadsheetScalarWriteValueKind.Decimal, value);

    public static SpreadsheetScalarWriteValue FromFloatingPoint(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Spreadsheet floating-point values must be finite.");
        }

        return new SpreadsheetScalarWriteValue(SpreadsheetScalarWriteValueKind.FloatingPoint, value);
    }

    public static SpreadsheetScalarWriteValue FromBoolean(bool value)
        => new(SpreadsheetScalarWriteValueKind.Boolean, value);
}

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
