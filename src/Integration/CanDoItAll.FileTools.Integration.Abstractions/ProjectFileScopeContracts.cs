namespace CanDoItAll.FileTools.Integration;

public sealed record ProjectFileScopeSet
{
    public const int MaximumScopes = 64;

    public ProjectFileScopeSet(
        Guid rootProjectId,
        IReadOnlyList<FileToolsSemanticScope> scopes,
        string fingerprint)
    {
        if (rootProjectId == Guid.Empty)
        {
            throw new ArgumentException("A root project identifier is required.", nameof(rootProjectId));
        }

        ArgumentNullException.ThrowIfNull(scopes);
        if (scopes.Count == 0 || scopes.Count > MaximumScopes)
        {
            throw new ArgumentOutOfRangeException(nameof(scopes));
        }

        if (scopes.Any(scope => scope.Kind != FileToolsSemanticScopeKind.Project) ||
            scopes.Select(scope => scope.Id).Distinct().Count() != scopes.Count)
        {
            throw new ArgumentException("Project file scopes must be unique project scopes.", nameof(scopes));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        string normalizedFingerprint = fingerprint.Trim();
        if (normalizedFingerprint.Length > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(fingerprint));
        }

        RootProjectId = rootProjectId;
        Scopes = scopes;
        Fingerprint = normalizedFingerprint;
    }

    public Guid RootProjectId { get; }

    public IReadOnlyList<FileToolsSemanticScope> Scopes { get; }

    public string Fingerprint { get; }
}

public interface IProjectFileScopeProvider
{
    ValueTask<ProjectFileScopeSet> ResolveAsync(
        Guid projectId,
        bool includeSubprojects,
        CancellationToken cancellationToken = default);
}
