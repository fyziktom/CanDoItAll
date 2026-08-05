namespace CanDoItAll.Tools.Documents;

public enum SpreadsheetRangeCapacityDimension
{
    Rows,
    Columns
}

public sealed class SpreadsheetRangeCapacityExceededException : InvalidOperationException
{
    private SpreadsheetRangeCapacityExceededException(
        string rangeAddress,
        SpreadsheetRangeCapacityDimension dimension,
        int capacity,
        int suppliedCount,
        int? valuesRowNumber)
        : base(CreateMessage(rangeAddress, dimension, capacity, suppliedCount, valuesRowNumber))
    {
        RangeAddress = rangeAddress;
        Dimension = dimension;
        Capacity = capacity;
        SuppliedCount = suppliedCount;
        ValuesRowNumber = valuesRowNumber;
    }

    public string RangeAddress { get; }

    public SpreadsheetRangeCapacityDimension Dimension { get; }

    public int Capacity { get; }

    public int SuppliedCount { get; }

    public int? ValuesRowNumber { get; }

    public static SpreadsheetRangeCapacityExceededException Rows(
        string rangeAddress,
        int capacity,
        int suppliedCount)
        => new(
            rangeAddress,
            SpreadsheetRangeCapacityDimension.Rows,
            capacity,
            suppliedCount,
            valuesRowNumber: null);

    public static SpreadsheetRangeCapacityExceededException Columns(
        string rangeAddress,
        int capacity,
        int suppliedCount,
        int valuesRowNumber)
        => new(
            rangeAddress,
            SpreadsheetRangeCapacityDimension.Columns,
            capacity,
            suppliedCount,
            valuesRowNumber);

    private static string CreateMessage(
        string rangeAddress,
        SpreadsheetRangeCapacityDimension dimension,
        int capacity,
        int suppliedCount,
        int? valuesRowNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rangeAddress);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfNegative(suppliedCount);

        return dimension switch
        {
            SpreadsheetRangeCapacityDimension.Rows =>
                $"Spreadsheet range '{rangeAddress}' has {capacity} row(s), but {suppliedCount} row(s) were supplied.",
            SpreadsheetRangeCapacityDimension.Columns when valuesRowNumber is > 0 =>
                $"Spreadsheet range '{rangeAddress}' has {capacity} column(s), but row {valuesRowNumber.Value} supplied {suppliedCount} value(s).",
            SpreadsheetRangeCapacityDimension.Columns =>
                throw new ArgumentOutOfRangeException(nameof(valuesRowNumber), valuesRowNumber, "A one-based values row number is required for a column-capacity failure."),
            _ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "Unknown spreadsheet range dimension.")
        };
    }
}
