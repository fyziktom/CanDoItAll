using System.Text.RegularExpressions;

namespace CanDoItAll.AppComponents.Navigation;

public sealed class UrlTokenMap<T> where T : struct, Enum {
    private readonly IReadOnlyDictionary<T, string> tokens;
    private readonly IReadOnlyDictionary<string, T> values;

    public UrlTokenMap(params (T Value, string Token)[] entries) {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Length == 0) {
            throw new ArgumentException("A token map needs at least one value.", nameof(entries));
        }
        var byValue = new Dictionary<T, string>();
        var byToken = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var (value, token) in entries) {
            if (!Enum.IsDefined(value)) {
                throw new ArgumentOutOfRangeException(nameof(entries), value, "The token map contains an undefined value.");
            }
            if (!IsCanonicalToken(token)) {
                throw new ArgumentException($"URL token '{token}' must be lowercase kebab-case.", nameof(entries));
            }
            if (!byValue.TryAdd(value, token) || !byToken.TryAdd(token, value)) {
                throw new ArgumentException($"URL token '{token}' or its value is mapped twice.", nameof(entries));
            }
        }
        tokens = byValue;
        values = byToken;
        Values = entries.Select(entry => entry.Value).ToArray();
    }

    private UrlTokenMap(UrlTokenMap<T> source, IReadOnlyDictionary<string, T> values) {
        tokens = source.tokens;
        this.values = values;
        Values = source.Values;
    }

    public IReadOnlyList<T> Values { get; }

    public UrlTokenMap<T> WithAliases(params (string Alias, T Value)[] aliases) {
        ArgumentNullException.ThrowIfNull(aliases);
        var byToken = new Dictionary<string, T>(values, StringComparer.OrdinalIgnoreCase);
        foreach (var (alias, value) in aliases) {
            if (string.IsNullOrWhiteSpace(alias) || !tokens.ContainsKey(value)) {
                throw new ArgumentException("An alias needs text and a mapped value.", nameof(aliases));
            }
            if (!byToken.TryAdd(alias.Trim(), value)) {
                throw new ArgumentException($"URL alias '{alias}' collides with an existing token.", nameof(aliases));
            }
        }
        return new(this, byToken);
    }

    public bool Contains(T value) => tokens.ContainsKey(value);

    public string Format(T value) => tokens.TryGetValue(value, out var token)
        ? token
        : throw new ArgumentOutOfRangeException(nameof(value), value, $"No URL token is mapped for {typeof(T).Name}.");

    public bool TryParse(string? text, out T value) {
        value = default;
        return !string.IsNullOrWhiteSpace(text) && values.TryGetValue(text.Trim(), out value);
    }

    public T? Parse(string? text) => TryParse(text, out var value) ? value : null;

    public static bool IsCanonicalToken(string? token) => UrlTokens.IsCanonical(token);
}

public static partial class UrlTokens {
    public static bool IsCanonical(string? token) => token is not null && CanonicalToken().IsMatch(token);

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalToken();
}
