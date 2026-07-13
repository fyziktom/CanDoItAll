namespace CanDoItAll.FileTools.Integration;

public sealed record ProcessRunFileScopeSet
{
    public const int MaximumScopes = 64;

    public ProcessRunFileScopeSet(
        Guid runId,
        IReadOnlyList<FileToolsSemanticScope> scopes,
        string fingerprint)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("A process run identifier is required.", nameof(runId));
        }

        ArgumentNullException.ThrowIfNull(scopes);
        if (scopes.Count == 0 || scopes.Count > MaximumScopes)
        {
            throw new ArgumentOutOfRangeException(nameof(scopes));
        }

        var scopeIds = new HashSet<FileToolsSemanticScopeId>();
        foreach (FileToolsSemanticScope scope in scopes)
        {
            if (scope.Kind != FileToolsSemanticScopeKind.ProcessRun || !scopeIds.Add(scope.Id))
            {
                throw new ArgumentException("Process-run file scopes must be unique process-run scopes.", nameof(scopes));
            }
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        string normalizedFingerprint = fingerprint.Trim();
        if (normalizedFingerprint.Length > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(fingerprint));
        }

        RunId = runId;
        Scopes = scopes;
        Fingerprint = normalizedFingerprint;
    }

    public Guid RunId { get; }

    public IReadOnlyList<FileToolsSemanticScope> Scopes { get; }

    public string Fingerprint { get; }
}

public interface IProcessRunFileScopeProvider
{
    ValueTask<ProcessRunFileScopeSet> ResolveAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}
