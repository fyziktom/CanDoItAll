using System.Text;

namespace CanDoItAll.AppComponents.Navigation;

public readonly record struct UrlLocationParts(string Path, string Query, string Fragment);

public static class UrlLocation {
    public static UrlLocationParts Split(string uri) {
        ArgumentNullException.ThrowIfNull(uri);
        var hash = uri.IndexOf('#');
        var fragment = hash < 0 ? string.Empty : uri[hash..];
        var beforeHash = hash < 0 ? uri : uri[..hash];
        var question = beforeHash.IndexOf('?');
        return question < 0
            ? new(beforeHash, string.Empty, fragment)
            : new(beforeHash[..question], beforeHash[question..], fragment);
    }

    public static string Compose<TState>(string uri, IUrlStateCodec<TState> codec, TState state) {
        ArgumentNullException.ThrowIfNull(codec);
        return Compose(uri, codec.Keys, codec.Pairs(state));
    }

    public static string Compose(string uri, IReadOnlyCollection<string> ownedKeys, IReadOnlyList<UrlQueryPair> ownedPairs) {
        ArgumentNullException.ThrowIfNull(ownedKeys);
        ArgumentNullException.ThrowIfNull(ownedPairs);
        var parts = Split(uri);
        var builder = new StringBuilder(parts.Path);
        var first = true;
        foreach (var pair in ownedPairs) {
            Append(builder, ref first, Encode(pair.Key) + "=" + Encode(pair.Value));
        }
        foreach (var segment in UrlQuery.Segments(parts.Query)) {
            var key = UrlQuery.KeyOf(segment);
            if (key.Length > 0 && !ownedKeys.Any(owned => UrlQuery.KeyEquals(owned, key))) {
                Append(builder, ref first, segment);
            }
        }
        return builder.Append(parts.Fragment).ToString();
    }

    public static string Format(IEnumerable<UrlQueryPair> pairs) {
        ArgumentNullException.ThrowIfNull(pairs);
        var builder = new StringBuilder();
        var first = true;
        foreach (var pair in pairs) {
            Append(builder, ref first, Encode(pair.Key) + "=" + Encode(pair.Value));
        }
        return builder.ToString();
    }

    public static string PathAndQuery(string uri) {
        ArgumentNullException.ThrowIfNull(uri);
        var scheme = uri.IndexOf("://", StringComparison.Ordinal);
        if (scheme < 0) {
            return uri;
        }
        var authorityEnd = uri.IndexOfAny(['/', '?', '#'], scheme + 3);
        if (authorityEnd < 0) {
            return "/";
        }
        return uri[authorityEnd] == '/' ? uri[authorityEnd..] : "/" + uri[authorityEnd..];
    }

    private static void Append(StringBuilder builder, ref bool first, string segment) {
        builder.Append(first ? '?' : '&');
        builder.Append(segment);
        first = false;
    }

    private static string Encode(string value) => Uri.EscapeDataString(value);
}
