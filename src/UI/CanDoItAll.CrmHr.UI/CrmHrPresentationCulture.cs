using System.Globalization;

namespace CanDoItAll.CrmHr.UI;

// The presentation-language policy of the CRM / HR surfaces. Interface words that a formatter generates (month names,
// AM/PM designators) are English whatever the server's ambient culture is, because the interface is English. The
// policy is deliberately narrow: numeric patterns, separators, the date order and the time zone stay with the
// ambient culture, persisted identifiers and machine formats are formatted explicitly where they are produced, and
// nothing here parses input or changes the process culture.
public static class CrmHrPresentationCulture
{
    public static CultureInfo English { get; } = CultureInfo.GetCultureInfo("en-US");

    // "Jan 2025": the English short month name and the four-digit year.
    public static string FormatMonth(DateOnly month)
        => month.ToString("MMM yyyy", English);

    public static string FormatYear(DateOnly year)
        => year.Year.ToString("0000", CultureInfo.InvariantCulture);

    // The ambient short date and short time pattern (order, separators, 12 or 24 hour clock) with English AM/PM
    // designators, so a 12-hour culture never prints designator words in another language.
    public static string FormatShortTimestamp(DateTime value)
    {
        var format = (DateTimeFormatInfo)CultureInfo.CurrentCulture.DateTimeFormat.Clone();
        format.AMDesignator = English.DateTimeFormat.AMDesignator;
        format.PMDesignator = English.DateTimeFormat.PMDesignator;
        return value.ToString("g", format);
    }
}
