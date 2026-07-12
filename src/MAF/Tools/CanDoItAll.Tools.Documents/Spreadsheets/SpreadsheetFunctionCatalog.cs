namespace CanDoItAll.Tools.Documents;

public static class SpreadsheetFunctionCatalog
{
    private static readonly IReadOnlyList<SpreadsheetFunctionDescriptor> Functions =
    [
        Function("SUM", "Aggregation", "SUM(number1, [number2], ...)", "=SUM(B2:B20)", "Adds numbers or ranges.", "Use for totals and subtotals."),
        Function("SUMIF", "Aggregation", "SUMIF(range, criteria, [sum_range])", "=SUMIF(A2:A20,\"Hardware\",D2:D20)", "Adds cells that match one condition.", "Use quotes around text criteria."),
        Function("SUMIFS", "Aggregation", "SUMIFS(sum_range, criteria_range1, criteria1, [criteria_range2, criteria2], ...)", "=SUMIFS(D2:D20,A2:A20,\"Hardware\",C2:C20,\">0\")", "Adds cells that match multiple conditions.", "The sum range comes first."),
        Function("AVERAGE", "Aggregation", "AVERAGE(number1, [number2], ...)", "=AVERAGE(E2:E20)", "Returns the arithmetic mean.", "Blank cells are ignored."),
        Function("AVERAGEIF", "Aggregation", "AVERAGEIF(range, criteria, [average_range])", "=AVERAGEIF(A2:A20,\"Hardware\",E2:E20)", "Averages cells that match one condition.", "Use when a single filter is enough."),
        Function("AVERAGEIFS", "Aggregation", "AVERAGEIFS(average_range, criteria_range1, criteria1, [criteria_range2, criteria2], ...)", "=AVERAGEIFS(E2:E20,A2:A20,\"Hardware\",C2:C20,\">0\")", "Averages cells that match multiple conditions.", "The average range comes first."),
        Function("MIN", "Aggregation", "MIN(number1, [number2], ...)", "=MIN(E2:E20)", "Returns the smallest numeric value.", "Good for downside or low-margin checks."),
        Function("MAX", "Aggregation", "MAX(number1, [number2], ...)", "=MAX(E2:E20)", "Returns the largest numeric value.", "Good for upside or outlier checks."),
        Function("COUNT", "Counting", "COUNT(value1, [value2], ...)", "=COUNT(B2:B20)", "Counts numeric cells.", "Use COUNTA for text or mixed content."),
        Function("COUNTA", "Counting", "COUNTA(value1, [value2], ...)", "=COUNTA(A2:A20)", "Counts non-empty cells.", "Useful for row counts when IDs are text."),
        Function("COUNTIF", "Counting", "COUNTIF(range, criteria)", "=COUNTIF(A2:A20,\"Hardware\")", "Counts cells matching one condition.", "Criteria can use comparison operators."),
        Function("COUNTIFS", "Counting", "COUNTIFS(criteria_range1, criteria1, [criteria_range2, criteria2], ...)", "=COUNTIFS(A2:A20,\"Hardware\",C2:C20,\">0\")", "Counts rows matching multiple conditions.", "All criteria must match."),
        Function("IF", "Logic", "IF(logical_test, value_if_true, value_if_false)", "=IF(E2>=0.3,\"Target\",\"Review\")", "Returns one of two values based on a condition.", "Keep nested IF chains short."),
        Function("IFS", "Logic", "IFS(logical_test1, value_if_true1, [logical_test2, value_if_true2], ...)", "=IFS(E2>=0.4,\"High\",E2>=0.25,\"Medium\",TRUE,\"Low\")", "Returns the first value whose condition is true.", "Use TRUE as the final default condition."),
        Function("AND", "Logic", "AND(logical1, [logical2], ...)", "=AND(C2>0,D2>0,E2>=0.25)", "Returns TRUE only when all conditions are true.", "Useful inside IF."),
        Function("OR", "Logic", "OR(logical1, [logical2], ...)", "=OR(E2<0.2,F2=\"Missing\")", "Returns TRUE when any condition is true.", "Useful for exception flags."),
        Function("NOT", "Logic", "NOT(logical)", "=NOT(ISBLANK(A2))", "Reverses TRUE/FALSE.", "Use sparingly for readable formulas."),
        Function("IFERROR", "Logic", "IFERROR(value, value_if_error)", "=IFERROR((D2-C2)/D2,\"\")", "Returns a fallback when a formula errors.", "Use for ratios that can divide by zero."),
        Function("XLOOKUP", "Lookup", "XLOOKUP(lookup_value, lookup_array, return_array, [if_not_found])", "=XLOOKUP(A2,Items!A:A,Items!D:D,\"Missing\")", "Looks up a value and returns a matching result.", "Prefer over VLOOKUP for new workbooks."),
        Function("INDEX", "Lookup", "INDEX(array, row_num, [column_num])", "=INDEX(D2:D20,MATCH(A2,A2:A20,0))", "Returns a value by row and column position.", "Often paired with MATCH."),
        Function("MATCH", "Lookup", "MATCH(lookup_value, lookup_array, [match_type])", "=MATCH(\"Total\",A:A,0)", "Returns the position of a matching value.", "Use 0 for exact match."),
        Function("ROUND", "Math", "ROUND(number, num_digits)", "=ROUND(E2,2)", "Rounds to a fixed number of digits.", "Use for presentation, not source data loss."),
        Function("ROUNDUP", "Math", "ROUNDUP(number, num_digits)", "=ROUNDUP(D2,0)", "Rounds away from zero.", "Useful for required units or packs."),
        Function("ROUNDDOWN", "Math", "ROUNDDOWN(number, num_digits)", "=ROUNDDOWN(E2,2)", "Rounds toward zero.", "Useful for conservative display."),
        Function("ABS", "Math", "ABS(number)", "=ABS(D2-C2)", "Returns absolute value.", "Useful for variance magnitude."),
        Function("CONCAT", "Text", "CONCAT(text1, [text2], ...)", "=CONCAT(A2,\" - \",B2)", "Combines text values.", "TEXTJOIN is better when delimiters matter."),
        Function("TEXTJOIN", "Text", "TEXTJOIN(delimiter, ignore_empty, text1, [text2], ...)", "=TEXTJOIN(\" \",TRUE,A2,B2,C2)", "Combines text with a delimiter.", "Set ignore_empty to TRUE for cleaner labels."),
        Function("LEFT", "Text", "LEFT(text, [num_chars])", "=LEFT(A2,3)", "Returns characters from the start of text.", "Useful for code prefixes."),
        Function("RIGHT", "Text", "RIGHT(text, [num_chars])", "=RIGHT(A2,4)", "Returns characters from the end of text.", "Useful for suffix IDs."),
        Function("MID", "Text", "MID(text, start_num, num_chars)", "=MID(A2,4,6)", "Returns characters from the middle of text.", "Use fixed positions only when the source format is stable."),
        Function("LEN", "Text", "LEN(text)", "=LEN(A2)", "Returns text length.", "Useful for data quality checks."),
        Function("TODAY", "Date", "TODAY()", "=TODAY()", "Returns the current date.", "Workbook recalculation updates the value."),
        Function("EOMONTH", "Date", "EOMONTH(start_date, months)", "=EOMONTH(A2,0)", "Returns the last day of a month offset.", "Useful for monthly forecasts."),
        Function("PMT", "Financial", "PMT(rate, nper, pv, [fv], [type])", "=PMT(B2/12,C2,-D2)", "Calculates a loan payment.", "Keep rate period aligned with nper."),
        Function("NPV", "Financial", "NPV(rate, value1, [value2], ...)", "=NPV(B2,C3:C14)+C2", "Calculates net present value for periodic cash flows.", "Add the initial cash flow separately when it occurs at time zero."),
        Function("IRR", "Financial", "IRR(values, [guess])", "=IRR(C2:C14)", "Calculates internal rate of return.", "Requires at least one positive and one negative cash flow."),
        Function("FILTER", "Dynamic Arrays", "FILTER(array, include, [if_empty])", "=FILTER(A2:E20,E2:E20>=0.3,\"No matches\")", "Returns rows that meet a condition.", "Requires dynamic-array support in the spreadsheet app."),
        Function("UNIQUE", "Dynamic Arrays", "UNIQUE(array)", "=UNIQUE(A2:A20)", "Returns distinct values.", "Useful for category lists."),
        Function("SORT", "Dynamic Arrays", "SORT(array, [sort_index], [sort_order], [by_col])", "=SORT(A2:E20,5,-1)", "Sorts a range or array.", "Use -1 for descending order.")
    ];

    public static IReadOnlyList<SpreadsheetFunctionDescriptor> List(string? query = null, string? category = null, int maxResults = 50)
    {
        var normalizedQuery = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
        var normalizedCategory = string.IsNullOrWhiteSpace(category) ? string.Empty : category.Trim();
        var limit = Math.Clamp(maxResults, 1, 100);
        var matches = Functions.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(normalizedCategory))
        {
            matches = matches.Where(function =>
                function.Category.Contains(normalizedCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            matches = matches.Where(function =>
                function.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                function.Syntax.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                function.Description.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase));
        }

        return matches
            .OrderBy(function => function.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(function => function.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
    }

    private static SpreadsheetFunctionDescriptor Function(
        string name,
        string category,
        string syntax,
        string example,
        string description,
        params string[] notes)
        => new(name, category, syntax, example, description, notes);
}
