namespace CanDoItAll.RemoteInstances;

public static class DefinitionSharingTags {
    public const string Api = "api";
    public const string Shared = "shared";

    private static readonly IReadOnlyList<string> ApiOnly = Array.AsReadOnly<string>([Api]);
    private static readonly IReadOnlyList<string> SharedOnly = Array.AsReadOnly<string>([Shared]);
    private static readonly IReadOnlyList<string> SharedAndApi = Array.AsReadOnly<string>([Shared, Api]);

    public static bool IsManaged(string? tag) {
        var value = tag?.Trim();
        return string.Equals(value, Api, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, Shared, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> Custom(IEnumerable<string>? tags) =>
        tags?.Where(tag => !IsManaged(tag)).ToArray() ?? [];

    public static IReadOnlyList<string> Project(IEnumerable<string>? tags, bool apiAvailable, bool sharedTemplate) {
        IReadOnlyList<string> managed = (apiAvailable, sharedTemplate) switch {
            (true, true) => SharedAndApi,
            (true, false) => ApiOnly,
            (false, true) => SharedOnly,
            _ => Array.Empty<string>()
        };
        if (tags is null or IReadOnlyCollection<string> { Count: 0 }) {
            return managed;
        }
        var custom = Custom(tags);
        return custom.Count == 0 ? managed : [.. managed, .. custom];
    }
}
