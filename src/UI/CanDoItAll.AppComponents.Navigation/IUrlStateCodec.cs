namespace CanDoItAll.AppComponents.Navigation;

public interface IUrlStateCodec<TState> {
    IReadOnlyList<string> Keys { get; }

    TState Read(UrlQuery query);

    void Write(TState state, UrlQueryWriter query);
}

public static class UrlStateCodec {
    public static IReadOnlyList<UrlQueryPair> Pairs<TState>(this IUrlStateCodec<TState> codec, TState state) {
        ArgumentNullException.ThrowIfNull(codec);
        var writer = new UrlQueryWriter(codec.Keys);
        codec.Write(state, writer);
        return writer.Pairs;
    }

    public static string Format<TState>(this IUrlStateCodec<TState> codec, TState state)
        => UrlLocation.Format(codec.Pairs(state));

    public static TState Normalize<TState>(this IUrlStateCodec<TState> codec, TState state)
        => codec.Read(UrlQuery.FromPairs(codec.Pairs(state)));

    public static TState Read<TState>(this IUrlStateCodec<TState> codec, string uri) {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Read(UrlQuery.FromUri(uri));
    }

    public static bool SameState<TState>(this IUrlStateCodec<TState> codec, TState left, TState right)
        => string.Equals(codec.Format(left), codec.Format(right), StringComparison.Ordinal);

    public static string Href<TState>(this IUrlStateCodec<TState> codec, string path, TState state) {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return path + codec.Format(state);
    }

    // Every owned key with its canonical value, or null when the state omits it: the overrides that describe a state
    // on top of the current location, for example for a location-based identity while the location is still in flight.
    public static IReadOnlyList<KeyValuePair<string, string?>> OwnedValues<TState>(this IUrlStateCodec<TState> codec, TState state) {
        var pairs = codec.Pairs(state);
        return codec.Keys
            .Select(key => new KeyValuePair<string, string?>(key,
                pairs.Where(pair => UrlQuery.KeyEquals(pair.Key, key)).Select(pair => pair.Value).FirstOrDefault()))
            .ToArray();
    }
}
