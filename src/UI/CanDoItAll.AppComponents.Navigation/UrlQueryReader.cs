using System.Globalization;

namespace CanDoItAll.AppComponents.Navigation;

public static class UrlQueryReader {
    public const int MaximumPage = 100_000;

    public static string? GetText(this UrlQuery query, string key, int maximumLength) {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumLength, 1);
        var text = query.Get(key)?.Trim();
        return string.IsNullOrEmpty(text) || text.Length > maximumLength ? null : text;
    }

    public static Guid? GetId(this UrlQuery query, string key) {
        ArgumentNullException.ThrowIfNull(query);
        return Guid.TryParse(query.Get(key)?.Trim(), out var id) && id != Guid.Empty ? id : null;
    }

    public static T? GetToken<T>(this UrlQuery query, string key, UrlTokenMap<T> map) where T : struct, Enum {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(map);
        return map.Parse(query.Get(key));
    }

    public static T GetToken<T>(this UrlQuery query, string key, UrlTokenMap<T> map, T defaultValue) where T : struct, Enum
        => query.GetToken(key, map) ?? defaultValue;

    public static IReadOnlySet<T> GetTokens<T>(this UrlQuery query, string key, UrlTokenMap<T> map) where T : struct, Enum {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(map);
        var selected = new HashSet<T>();
        foreach (var raw in query.GetAll(key)) {
            foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                if (map.TryParse(token, out var value)) {
                    selected.Add(value);
                }
            }
        }
        return selected;
    }

    public static int GetPage(this UrlQuery query, string key, int maximum = MaximumPage) {
        ArgumentNullException.ThrowIfNull(query);
        return int.TryParse(query.Get(key), NumberStyles.None, CultureInfo.InvariantCulture, out var page) && page >= 1 && page <= maximum
            ? page
            : 1;
    }

    public static int? GetNumber(this UrlQuery query, string key, int minimum, int maximum) {
        ArgumentNullException.ThrowIfNull(query);
        return int.TryParse(query.Get(key), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) &&
               number >= minimum && number <= maximum
            ? number
            : null;
    }

    public static DateOnly? GetDate(this UrlQuery query, string key) {
        ArgumentNullException.ThrowIfNull(query);
        return DateOnly.TryParseExact(query.Get(key)?.Trim(), UrlFormats.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    public static DateTimeOffset? GetInstant(this UrlQuery query, string key) {
        ArgumentNullException.ThrowIfNull(query);
        var text = query.Get(key)?.Trim();
        if (string.IsNullOrEmpty(text) ||
            !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var instant)) {
            return null;
        }
        return new DateTimeOffset(instant.UtcDateTime.Ticks - instant.UtcDateTime.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
    }

    public static bool GetFlag(this UrlQuery query, string key) => query.GetFlag(key, defaultValue: false);

    public static bool GetFlag(this UrlQuery query, string key, bool defaultValue) {
        ArgumentNullException.ThrowIfNull(query);
        var text = query.Get(key)?.Trim();
        if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "1", StringComparison.Ordinal)) {
            return true;
        }
        if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "0", StringComparison.Ordinal)) {
            return false;
        }
        return defaultValue;
    }
}
