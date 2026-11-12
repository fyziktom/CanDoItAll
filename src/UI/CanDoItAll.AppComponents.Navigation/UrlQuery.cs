namespace CanDoItAll.AppComponents.Navigation;

public readonly record struct UrlQueryPair(string Key, string Value);

public sealed class UrlQuery {
    private UrlQuery(IReadOnlyList<UrlQueryPair> pairs) {
        Pairs = pairs;
    }

    public static UrlQuery Empty { get; } = new([]);

    public IReadOnlyList<UrlQueryPair> Pairs { get; }

    public static UrlQuery Parse(string? query) {
        if (string.IsNullOrEmpty(query)) {
            return Empty;
        }
        var pairs = new List<UrlQueryPair>();
        foreach (var segment in Segments(query)) {
            var key = KeyOf(segment);
            if (key.Length == 0) {
                continue;
            }
            var separator = segment.IndexOf('=');
            pairs.Add(new(key, separator < 0 ? string.Empty : Decode(segment[(separator + 1)..])));
        }
        return pairs.Count == 0 ? Empty : new(pairs);
    }

    public static UrlQuery FromUri(string uri) {
        ArgumentNullException.ThrowIfNull(uri);
        return Parse(UrlLocation.Split(uri).Query);
    }

    public static UrlQuery FromPairs(IEnumerable<UrlQueryPair> pairs) {
        ArgumentNullException.ThrowIfNull(pairs);
        var copy = pairs.ToArray();
        return copy.Length == 0 ? Empty : new(copy);
    }

    public bool Contains(string key) => Pairs.Any(pair => KeyEquals(pair.Key, key));

    public string? Get(string key) {
        foreach (var pair in Pairs) {
            if (KeyEquals(pair.Key, key)) {
                return pair.Value;
            }
        }
        return null;
    }

    public IReadOnlyList<string> GetAll(string key) => Pairs
        .Where(pair => KeyEquals(pair.Key, key))
        .Select(pair => pair.Value)
        .ToArray();

    public static bool KeyEquals(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    internal static IEnumerable<string> Segments(string query) {
        var text = query.StartsWith('?') ? query[1..] : query;
        return text.Split('&', StringSplitOptions.RemoveEmptyEntries);
    }

    internal static string KeyOf(string segment) {
        var separator = segment.IndexOf('=');
        return Decode(separator < 0 ? segment : segment[..separator]).Trim();
    }

    private static string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));
}
