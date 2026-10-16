using System.Globalization;

namespace CanDoItAll.AppComponents.Navigation;

public sealed class UrlQueryWriter {
    private readonly IReadOnlyCollection<string> ownedKeys;
    private readonly List<UrlQueryPair> pairs = [];

    public UrlQueryWriter(IReadOnlyCollection<string> ownedKeys) {
        ArgumentNullException.ThrowIfNull(ownedKeys);
        this.ownedKeys = ownedKeys;
    }

    public IReadOnlyList<UrlQueryPair> Pairs => pairs;

    public UrlQueryWriter Text(string key, string? value) {
        var text = value?.Trim();
        if (!string.IsNullOrEmpty(text)) {
            Add(key, text);
        }
        return this;
    }

    public UrlQueryWriter Id(string key, Guid? value) {
        if (value is { } id && id != Guid.Empty) {
            Add(key, id.ToString("D"));
        }
        return this;
    }

    public UrlQueryWriter Token<T>(string key, T? value, UrlTokenMap<T> map) where T : struct, Enum {
        ArgumentNullException.ThrowIfNull(map);
        if (value is { } token) {
            Add(key, map.Format(token));
        }
        return this;
    }

    public UrlQueryWriter Token<T>(string key, T value, UrlTokenMap<T> map, T defaultValue) where T : struct, Enum {
        ArgumentNullException.ThrowIfNull(map);
        if (!EqualityComparer<T>.Default.Equals(value, defaultValue)) {
            Add(key, map.Format(value));
        }
        return this;
    }

    public UrlQueryWriter Tokens<T>(string key, IEnumerable<T> values, UrlTokenMap<T> map) where T : struct, Enum {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(map);
        var selected = values.ToHashSet();
        var ordered = map.Values.Where(selected.Contains).Select(map.Format).ToArray();
        if (selected.Count != ordered.Length) {
            throw new ArgumentOutOfRangeException(nameof(values), $"A value has no URL token in the {typeof(T).Name} map.");
        }
        if (ordered.Length > 0) {
            Add(key, string.Join(',', ordered));
        }
        return this;
    }

    public UrlQueryWriter Page(string key, int page) {
        if (page < 1) {
            throw new ArgumentOutOfRangeException(nameof(page), page, "A page number starts at 1.");
        }
        if (page > 1) {
            Add(key, page.ToString(CultureInfo.InvariantCulture));
        }
        return this;
    }

    public UrlQueryWriter Number(string key, int? value, int? defaultValue = null) {
        if (value is { } number && number != defaultValue) {
            Add(key, number.ToString(CultureInfo.InvariantCulture));
        }
        return this;
    }

    public UrlQueryWriter Date(string key, DateOnly? value) {
        if (value is { } date) {
            Add(key, date.ToString(UrlFormats.Date, CultureInfo.InvariantCulture));
        }
        return this;
    }

    public UrlQueryWriter Instant(string key, DateTimeOffset? value) {
        if (value is { } instant) {
            Add(key, instant.ToUniversalTime().ToString(UrlFormats.Instant, CultureInfo.InvariantCulture));
        }
        return this;
    }

    public UrlQueryWriter Flag(string key, bool value) => Flag(key, value, defaultValue: false);

    public UrlQueryWriter Flag(string key, bool value, bool defaultValue) {
        if (value != defaultValue) {
            Add(key, value ? "true" : "false");
        }
        return this;
    }

    private void Add(string key, string value) {
        if (!ownedKeys.Any(owned => string.Equals(owned, key, StringComparison.Ordinal))) {
            throw new InvalidOperationException($"Query key '{key}' is not declared by this URL state codec.");
        }
        if (pairs.Any(pair => UrlQuery.KeyEquals(pair.Key, key))) {
            throw new InvalidOperationException($"Query key '{key}' was written twice.");
        }
        pairs.Add(new(key, value));
    }
}

public static class UrlFormats {
    public const string Date = "yyyy-MM-dd";
    public const string Instant = "yyyy-MM-dd'T'HH:mm:ss'Z'";
}
