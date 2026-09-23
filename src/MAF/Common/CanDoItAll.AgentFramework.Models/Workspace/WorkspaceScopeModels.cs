namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Kind of workspace scope, as a JSON integer: 0 Sandbox, 1 Process, 2 Project, 3 Tenant, 4 Organization.
/// </summary>
public enum WorkspaceScopeKind
{
    Sandbox,
    Process,
    Project,
    Tenant,
    Organization
}

/// <summary>
/// Workspace scope: the partition of workspace data that work runs in, identified by <c>kind</c> and <c>key</c>. The
/// other members are relative workspace paths and labels derived from those two; they are computed by the server and
/// ignored when sent.
/// </summary>
public sealed record WorkspaceScopeDescriptor
{
    public const string DataManagedRootName = "data";
    public const string ArtifactManagedRootName = "artifacts";
    public const string IntegrationMapManagedRootName = "integration-map";
    public const string OutputManagedRootName = "output";

    public static IReadOnlyList<string> ManagedRootNames { get; } =
    [
        DataManagedRootName,
        ArtifactManagedRootName,
        IntegrationMapManagedRootName,
        OutputManagedRootName
    ];

    public static WorkspaceScopeDescriptor Sandbox { get; } = new(WorkspaceScopeKind.Sandbox);

    public WorkspaceScopeDescriptor(WorkspaceScopeKind kind, string? key = null)
    {
        Kind = kind;
        Key = NormalizeKey(kind, key);
    }

    /// <summary>
    /// Kind of scope, as a JSON integer: 0 Sandbox, 1 Process, 2 Project, 3 Tenant, 4 Organization.
    /// </summary>
    public WorkspaceScopeKind Kind { get; init; }

    /// <summary>
    /// Scope key, lower-cased, with every character other than a letter or digit turned into a hyphen and repeated or
    /// outer hyphens removed; for example a project identifier. Empty only for the default sandbox; the other kinds
    /// require a key.
    /// </summary>
    public string Key { get; init; }

    /// <summary>Computed: true for kind 0 Sandbox with an empty key.</summary>
    public bool IsDefaultSandbox => Kind == WorkspaceScopeKind.Sandbox && string.IsNullOrWhiteSpace(Key);

    /// <summary>
    /// Computed label: the lower-case kind and the key separated by a slash, for example <c>project/alpha</c>, or
    /// <c>sandbox/default</c> for the default sandbox.
    /// </summary>
    public string DisplayName => IsDefaultSandbox
        ? "sandbox/default"
        : $"{KindSegment}/{KeySegment}";

    /// <summary>
    /// Computed: <c>scopes/{kind}/{key}</c>, the scope's partition below each managed root; empty for the default
    /// sandbox.
    /// </summary>
    public string PartitionRelativePath => IsDefaultSandbox
        ? string.Empty
        : CombineRelative("scopes", KindSegment, KeySegment);

    /// <summary>
    /// Computed: <c>data</c> followed by the partition path, or <c>data</c> alone for the default sandbox.
    /// </summary>
    public string DataRootRelativePath => CombineManagedRoot(DataManagedRootName);

    /// <summary>
    /// Computed: <c>artifacts</c> followed by the partition path, or <c>artifacts</c> alone for the default sandbox.
    /// </summary>
    public string ArtifactRootRelativePath => CombineManagedRoot(ArtifactManagedRootName);

    /// <summary>
    /// Computed: <c>integration-map</c> followed by the partition path, or <c>integration-map</c> alone for the
    /// default sandbox.
    /// </summary>
    public string IntegrationMapRootRelativePath => CombineManagedRoot(IntegrationMapManagedRootName);

    /// <summary>
    /// Computed: <c>output</c> followed by the partition path, or <c>output</c> alone for the default sandbox.
    /// </summary>
    public string OutputRootRelativePath => CombineManagedRoot(OutputManagedRootName);

    /// <summary>
    /// Computed: the data, artifacts, integration-map and output root paths, in that order.
    /// </summary>
    public IReadOnlyList<string> ManagedRootRelativePaths =>
    [
        DataRootRelativePath,
        ArtifactRootRelativePath,
        IntegrationMapRootRelativePath,
        OutputRootRelativePath
    ];

    public string ResolveDataRoot(string workspaceRoot)
        => ResolveWorkspacePath(workspaceRoot, DataRootRelativePath);

    public string ResolveArtifactRoot(string workspaceRoot)
        => ResolveWorkspacePath(workspaceRoot, ArtifactRootRelativePath);

    public string ResolveIntegrationMapRoot(string workspaceRoot)
        => ResolveWorkspacePath(workspaceRoot, IntegrationMapRootRelativePath);

    public string ResolveOutputRoot(string workspaceRoot)
        => ResolveWorkspacePath(workspaceRoot, OutputRootRelativePath);

    public string CombineDataPath(params string[] segments)
        => CombineManagedPath(DataRootRelativePath, segments);

    public string CombineArtifactPath(params string[] segments)
        => CombineManagedPath(ArtifactRootRelativePath, segments);

    public string CombineIntegrationMapPath(params string[] segments)
        => CombineManagedPath(IntegrationMapRootRelativePath, segments);

    public string CombineOutputPath(params string[] segments)
        => CombineManagedPath(OutputRootRelativePath, segments);

    public static WorkspaceScopeDescriptor Process(string processId)
        => new(WorkspaceScopeKind.Process, processId);

    public static WorkspaceScopeDescriptor Project(string projectId)
        => new(WorkspaceScopeKind.Project, projectId);

    public static WorkspaceScopeDescriptor Tenant(string tenantId)
        => new(WorkspaceScopeKind.Tenant, tenantId);

    public static WorkspaceScopeDescriptor Organization(string organizationId)
        => new(WorkspaceScopeKind.Organization, organizationId);

    public static string NormalizeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Replace('\\', '/').Trim();
        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        return normalized.Trim('/');
    }

    private string KindSegment => NormalizeSegment(Kind.ToString());

    private string KeySegment => string.IsNullOrWhiteSpace(Key)
        ? string.Empty
        : NormalizeSegment(Key);

    private string CombineManagedRoot(string rootName)
        => IsDefaultSandbox
            ? rootName
            : CombineRelative(rootName, PartitionRelativePath);

    private static string ResolveWorkspacePath(string workspaceRoot, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        return Path.GetFullPath(Path.Combine(
            Path.GetFullPath(workspaceRoot),
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string NormalizeKey(WorkspaceScopeKind kind, string? key)
    {
        if (kind == WorkspaceScopeKind.Sandbox && string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        var normalized = NormalizeSegment(key ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException($"Workspace scope '{kind}' requires a non-empty key.", nameof(key));
        }

        return normalized;
    }

    private static string CombineManagedPath(string rootRelativePath, IReadOnlyList<string> segments)
    {
        var normalizedSegments = segments
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .Select(NormalizeRelativePath)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();

        return CombineRelative([rootRelativePath, .. normalizedSegments]);
    }

    private static string CombineRelative(params IEnumerable<string> parts)
    {
        return string.Join(
            '/',
            parts
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(NormalizeRelativePath)
                .Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string NormalizeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var characters = value
            .Trim()
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray();
        var collapsed = new string(characters).Trim('-');
        while (collapsed.Contains("--", StringComparison.Ordinal))
        {
            collapsed = collapsed.Replace("--", "-", StringComparison.Ordinal);
        }

        return collapsed;
    }
}
