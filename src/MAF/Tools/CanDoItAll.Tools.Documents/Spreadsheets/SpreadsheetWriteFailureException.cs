namespace CanDoItAll.Tools.Documents;

public enum SpreadsheetWriteInputFailureKind
{
    UnsupportedInputWorkbookFormat,
    UnsupportedOutputWorkbookFormat,
    InvalidWorksheetName,
    MissingCellWrites,
    MissingCellWrite,
    InvalidCellAddress,
    MissingRangeWrites,
    MissingRangeWrite,
    InvalidRangeAddress,
    MissingRangeValues,
    MissingRangeRow,
    InputWorkbookMissing
}

public sealed class SpreadsheetWriteInputException : InvalidOperationException
{
    private SpreadsheetWriteInputException(
        SpreadsheetWriteInputFailureKind kind,
        int? writeNumber = null,
        int? valuesRowNumber = null)
        : base(CreateMessage(kind, writeNumber, valuesRowNumber))
    {
        Kind = kind;
        WriteNumber = writeNumber;
        ValuesRowNumber = valuesRowNumber;
    }

    public SpreadsheetWriteInputFailureKind Kind { get; }

    public int? WriteNumber { get; }

    public int? ValuesRowNumber { get; }

    public static SpreadsheetWriteInputException UnsupportedInputWorkbookFormat()
        => new(SpreadsheetWriteInputFailureKind.UnsupportedInputWorkbookFormat);

    public static SpreadsheetWriteInputException UnsupportedOutputWorkbookFormat()
        => new(SpreadsheetWriteInputFailureKind.UnsupportedOutputWorkbookFormat);

    public static SpreadsheetWriteInputException InvalidWorksheetName()
        => new(SpreadsheetWriteInputFailureKind.InvalidWorksheetName);

    public static SpreadsheetWriteInputException MissingCellWrites()
        => new(SpreadsheetWriteInputFailureKind.MissingCellWrites);

    public static SpreadsheetWriteInputException MissingCellWrite(int writeNumber)
        => new(SpreadsheetWriteInputFailureKind.MissingCellWrite, writeNumber);

    public static SpreadsheetWriteInputException InvalidCellAddress(int writeNumber)
        => new(SpreadsheetWriteInputFailureKind.InvalidCellAddress, writeNumber);

    public static SpreadsheetWriteInputException MissingRangeWrites()
        => new(SpreadsheetWriteInputFailureKind.MissingRangeWrites);

    public static SpreadsheetWriteInputException MissingRangeWrite(int writeNumber)
        => new(SpreadsheetWriteInputFailureKind.MissingRangeWrite, writeNumber);

    public static SpreadsheetWriteInputException InvalidRangeAddress(int writeNumber)
        => new(SpreadsheetWriteInputFailureKind.InvalidRangeAddress, writeNumber);

    public static SpreadsheetWriteInputException MissingRangeValues(int writeNumber)
        => new(SpreadsheetWriteInputFailureKind.MissingRangeValues, writeNumber);

    public static SpreadsheetWriteInputException MissingRangeRow(
        int writeNumber,
        int valuesRowNumber)
        => new(
            SpreadsheetWriteInputFailureKind.MissingRangeRow,
            writeNumber,
            valuesRowNumber);

    public static SpreadsheetWriteInputException InputWorkbookMissing()
        => new(SpreadsheetWriteInputFailureKind.InputWorkbookMissing);

    private static string CreateMessage(
        SpreadsheetWriteInputFailureKind kind,
        int? writeNumber,
        int? valuesRowNumber)
    {
        ValidateOptionalNumber(writeNumber, nameof(writeNumber));
        ValidateOptionalNumber(valuesRowNumber, nameof(valuesRowNumber));

        return kind switch
        {
            SpreadsheetWriteInputFailureKind.UnsupportedInputWorkbookFormat =>
                "The spreadsheet input workbook format is unsupported.",
            SpreadsheetWriteInputFailureKind.UnsupportedOutputWorkbookFormat =>
                "The spreadsheet output workbook format is unsupported.",
            SpreadsheetWriteInputFailureKind.InvalidWorksheetName =>
                "The spreadsheet worksheet name is invalid.",
            SpreadsheetWriteInputFailureKind.MissingCellWrites =>
                "The spreadsheet cell-writes collection is missing.",
            SpreadsheetWriteInputFailureKind.MissingCellWrite when writeNumber.HasValue =>
                $"Spreadsheet cell write {writeNumber.Value} is missing.",
            SpreadsheetWriteInputFailureKind.InvalidCellAddress when writeNumber.HasValue =>
                $"Spreadsheet cell write {writeNumber.Value} has an invalid cell address.",
            SpreadsheetWriteInputFailureKind.MissingRangeWrites =>
                "The spreadsheet range-writes collection is missing.",
            SpreadsheetWriteInputFailureKind.MissingRangeWrite when writeNumber.HasValue =>
                $"Spreadsheet range write {writeNumber.Value} is missing.",
            SpreadsheetWriteInputFailureKind.InvalidRangeAddress when writeNumber.HasValue =>
                $"Spreadsheet range write {writeNumber.Value} has an invalid range address.",
            SpreadsheetWriteInputFailureKind.MissingRangeValues when writeNumber.HasValue =>
                $"Spreadsheet range write {writeNumber.Value} has no values collection.",
            SpreadsheetWriteInputFailureKind.MissingRangeRow when writeNumber.HasValue && valuesRowNumber.HasValue =>
                $"Spreadsheet range write {writeNumber.Value} values row {valuesRowNumber.Value} is missing.",
            SpreadsheetWriteInputFailureKind.InputWorkbookMissing =>
                "The spreadsheet input workbook is missing.",
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "The spreadsheet write failure kind is missing required context or is unknown.")
        };
    }

    private static void ValidateOptionalNumber(int? value, string parameterName)
    {
        if (value is <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "A one-based number is required.");
        }
    }
}

public sealed class SpreadsheetWriteConflictException : InvalidOperationException
{
    private SpreadsheetWriteConflictException()
        : base("The spreadsheet output workbook already exists.")
    {
    }

    public static SpreadsheetWriteConflictException OutputWorkbookExists()
        => new();
}
