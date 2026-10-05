using System.Globalization;

namespace CanDoItAll.Workbench.Planning.UI;

public static class CalendarTimeDisplay {
    public static string Format(DateTimeOffset value, string timezone)
        => TimeZoneInfo.ConvertTime(value, TimeZoneInfo.FindSystemTimeZoneById(timezone))
            .ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture);
}
