namespace CanDoItAll.Modules.Resources;

public enum ResourceFileSourceClass
{
    Project,
    FileSystem,
    Ipfs,
    Ftp
}

public readonly record struct ResourceFileSourceKey
{
    private const string ProjectPrefix = "project:";
    private const string StoragePrefix = "storage:";

    private ResourceFileSourceKey(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ResourceFileSourceKey ForProject(Guid projectId)
        => projectId == Guid.Empty
            ? throw new ArgumentException("A project source identifier is required.", nameof(projectId))
            : new ResourceFileSourceKey($"{ProjectPrefix}{projectId:N}");

    public static ResourceFileSourceKey ForStorage(Guid storageId)
        => storageId == Guid.Empty
            ? throw new ArgumentException("A storage source identifier is required.", nameof(storageId))
            : new ResourceFileSourceKey($"{StoragePrefix}{storageId:N}");

    // The key is a typed identity, not an opaque string: any accepted spelling of the GUID digits resolves to the one
    // canonical key the catalog generates, so stored or submitted keys compare equal to ForProject/ForStorage.
    // Prefixes stay case-sensitive and strict so source kinds cannot be confused.
    public static bool TryParse(string? value, out ResourceFileSourceKey key)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (TryParseIdentifier(normalized, ProjectPrefix, out Guid projectId))
        {
            key = ForProject(projectId);
            return true;
        }

        if (TryParseIdentifier(normalized, StoragePrefix, out Guid storageId))
        {
            key = ForStorage(storageId);
            return true;
        }

        key = default;
        return false;
    }

    public bool TryGetProjectId(out Guid projectId)
        => TryParseIdentifier(Value, ProjectPrefix, out projectId);

    public bool TryGetStorageId(out Guid storageId)
        => TryParseIdentifier(Value, StoragePrefix, out storageId);

    public override string ToString() => Value ?? string.Empty;

    private static bool TryParseIdentifier(string? value, string prefix, out Guid id)
    {
        id = Guid.Empty;
        return value is not null &&
               value.StartsWith(prefix, StringComparison.Ordinal) &&
               value.Length == prefix.Length + 32 &&
               Guid.TryParseExact(value[prefix.Length..], "N", out id) &&
               id != Guid.Empty;
    }
}
