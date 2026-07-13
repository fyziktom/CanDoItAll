namespace CanDoItAll.FileTools.Integration;

public enum FileToolsSemanticScopeKind
{
    Project,
    ProjectNode,
    ProcessRun,
    ResourceSource
}

public readonly record struct FileToolsSemanticScopeId
{
    public const int MaximumLength = 512;

    public FileToolsSemanticScopeId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim();
        if (normalized.Length > MaximumLength)
        {
            throw new ArgumentException("The semantic scope identifier is too long.", nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;
}

public sealed record FileToolsSemanticScope
{
    public FileToolsSemanticScope(
        FileToolsSemanticScopeKind kind,
        FileToolsSemanticScopeId id,
        string displayName)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (string.IsNullOrWhiteSpace(id.Value))
        {
            throw new ArgumentException("A semantic scope identifier is required.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        Kind = kind;
        Id = id;
        DisplayName = displayName.Trim();
    }

    public FileToolsSemanticScopeKind Kind { get; }

    public FileToolsSemanticScopeId Id { get; }

    public string DisplayName { get; }
}
