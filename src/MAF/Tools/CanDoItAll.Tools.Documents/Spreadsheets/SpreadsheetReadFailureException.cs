namespace CanDoItAll.Tools.Documents;

public enum SpreadsheetReadInputFailureKind
{
    WorkbookMissing,
    UnsupportedWorkbookFormat,
    InvalidWorkbook,
    WorksheetNotFound,
    InvalidCellAddress,
    InvalidRangeAddress,
    PreviewLimitOutOfRange,
    ReadLimitOutOfRange
}

public enum SpreadsheetReadLimitKind
{
    MaxWorksheets,
    MaxRows,
    MaxColumns
}

public sealed class SpreadsheetReadInputException : InvalidOperationException
{
    private SpreadsheetReadInputException(
        SpreadsheetReadInputFailureKind kind,
        SpreadsheetReadLimitKind? limitKind = null,
        int? minimum = null,
        int? maximum = null,
        Exception? innerException = null)
        : base(CreateMessage(kind, limitKind, minimum, maximum), innerException)
    {
        Kind = kind;
        LimitKind = limitKind;
        Minimum = minimum;
        Maximum = maximum;
    }

    public SpreadsheetReadInputFailureKind Kind { get; }

    public SpreadsheetReadLimitKind? LimitKind { get; }

    public int? Minimum { get; }

    public int? Maximum { get; }

    public static SpreadsheetReadInputException WorkbookMissing()
        => new(SpreadsheetReadInputFailureKind.WorkbookMissing);

    public static SpreadsheetReadInputException UnsupportedWorkbookFormat()
        => new(SpreadsheetReadInputFailureKind.UnsupportedWorkbookFormat);

    public static SpreadsheetReadInputException InvalidWorkbook(Exception innerException)
        => new(
            SpreadsheetReadInputFailureKind.InvalidWorkbook,
            innerException: innerException ?? throw new ArgumentNullException(nameof(innerException)));

    public static SpreadsheetReadInputException WorksheetNotFound()
        => new(SpreadsheetReadInputFailureKind.WorksheetNotFound);

    public static SpreadsheetReadInputException InvalidCellAddress()
        => new(SpreadsheetReadInputFailureKind.InvalidCellAddress);

    public static SpreadsheetReadInputException InvalidRangeAddress()
        => new(SpreadsheetReadInputFailureKind.InvalidRangeAddress);

    public static SpreadsheetReadInputException PreviewLimitOutOfRange(
        SpreadsheetReadLimitKind limitKind,
        int minimum,
        int maximum)
        => new(
            SpreadsheetReadInputFailureKind.PreviewLimitOutOfRange,
            limitKind,
            minimum,
            maximum);

    public static SpreadsheetReadInputException ReadLimitOutOfRange(
        SpreadsheetReadLimitKind limitKind,
        int minimum,
        int maximum)
        => new(
            SpreadsheetReadInputFailureKind.ReadLimitOutOfRange,
            limitKind,
            minimum,
            maximum);

    private static string CreateMessage(
        SpreadsheetReadInputFailureKind kind,
        SpreadsheetReadLimitKind? limitKind,
        int? minimum,
        int? maximum)
    {
        ValidateLimit(kind, limitKind, minimum, maximum);

        return kind switch
        {
            SpreadsheetReadInputFailureKind.WorkbookMissing =>
                "The spreadsheet input workbook is missing.",
            SpreadsheetReadInputFailureKind.UnsupportedWorkbookFormat =>
                "The spreadsheet input workbook format is unsupported.",
            SpreadsheetReadInputFailureKind.InvalidWorkbook =>
                "The spreadsheet input workbook is invalid or corrupt.",
            SpreadsheetReadInputFailureKind.WorksheetNotFound =>
                "The spreadsheet worksheet was not found.",
            SpreadsheetReadInputFailureKind.InvalidCellAddress =>
                "The spreadsheet cell address is invalid.",
            SpreadsheetReadInputFailureKind.InvalidRangeAddress =>
                "The spreadsheet range address is invalid.",
            SpreadsheetReadInputFailureKind.PreviewLimitOutOfRange =>
                $"The spreadsheet preview limit '{limitKind}' must be between {minimum} and {maximum}.",
            SpreadsheetReadInputFailureKind.ReadLimitOutOfRange =>
                $"The spreadsheet read limit '{limitKind}' must be between {minimum} and {maximum}.",
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unknown spreadsheet read input failure kind.")
        };
    }

    private static void ValidateLimit(
        SpreadsheetReadInputFailureKind kind,
        SpreadsheetReadLimitKind? limitKind,
        int? minimum,
        int? maximum)
    {
        var requiresLimit = kind is SpreadsheetReadInputFailureKind.PreviewLimitOutOfRange or
            SpreadsheetReadInputFailureKind.ReadLimitOutOfRange;
        if (!requiresLimit)
        {
            return;
        }

        if (!limitKind.HasValue || minimum is not > 0 || maximum is null || maximum < minimum)
        {
            throw new ArgumentException(
                "Spreadsheet limit failures require a limit kind and a valid inclusive range.");
        }
    }
}
