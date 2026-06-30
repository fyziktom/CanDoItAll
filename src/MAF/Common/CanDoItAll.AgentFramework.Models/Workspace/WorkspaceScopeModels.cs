namespace CanDoItAll.AgentFramework.Models;

public enum WorkspaceScopeKind
{
    Sandbox,
    Process,
    Project,
    Tenant,
    Organization
}

public sealed record WorkspaceScopeDescriptor
{
    public static WorkspaceScopeDescriptor Sandbox { get; } = new(WorkspaceScopeKind.Sandbox);

    public WorkspaceScopeDescriptor(WorkspaceScopeKind kind, string? key = null)
    {
        Kind = kind;
        Key = NormalizeKey(kind, key);
    }

    public WorkspaceScopeKind Kind { get; init; }

    public string Key { get; init; }

    public bool IsDefaultSandbox => Kind == WorkspaceScopeKind.Sandbox && string.IsNullOrWhiteSpace(Key);

    public string DisplayName => IsDefaultSandbox
        ? "sandbox/default"
        : $"{KindSegment}/{KeySegment}";

    public string PartitionRelativePath => IsDefaultSandbox
        ? string.Empty
        : CombineRelative("scopes", KindSegment, KeySegment);

    public string DataRootRelativePath => CombineManagedRoot("data");

    public string ArtifactRootRelativePath => CombineManagedRoot("artifacts");

    public string IntegrationMapRootRelativePath => CombineManagedRoot("integration-map");

    public string OutputRootRelativePath => CombineManagedRoot("output");

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
