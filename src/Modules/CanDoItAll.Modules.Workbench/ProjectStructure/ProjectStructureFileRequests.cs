using System.Text;
using CanDoItAll.FileTools.Integration;

namespace CanDoItAll.Modules.Workbench;

internal enum ProjectStructureNodeFileScopeMode
{
    KnownFile,
    Collection
}

internal readonly record struct ProjectStructureNodeFileScopeKey
{
    private const string PersistedPrefix = "node:v1";
    private const string ProjectedPrefix = "node:v2";

    private ProjectStructureNodeFileScopeKey(
        ProjectStructureNodeFileScopeMode mode,
        Guid projectObjectId,
        Guid projectId,
        string nodeKey)
    {
        Mode = mode;
        ProjectObjectId = projectObjectId;
        ProjectId = projectId;
        NodeKey = nodeKey;
    }

    public ProjectStructureNodeFileScopeMode Mode { get; }

    public Guid ProjectObjectId { get; }

    public Guid ProjectId { get; }

    public string NodeKey { get; }

    public bool IsProjected => ProjectObjectId == Guid.Empty;

    public static ProjectStructureNodeFileScopeKey CreatePersisted(
        ProjectStructureNodeFileScopeMode mode,
        Guid projectObjectId)
    {
        if (projectObjectId == Guid.Empty)
        {
            throw new ArgumentException("A project object identifier is required.", nameof(projectObjectId));
        }

        return new ProjectStructureNodeFileScopeKey(
            mode,
            projectObjectId,
            Guid.Empty,
            string.Empty);
    }

    public static ProjectStructureNodeFileScopeKey CreateProjected(
        ProjectStructureNodeFileScopeMode mode,
        Guid projectId,
        string nodeKey)
    {
        if (mode != ProjectStructureNodeFileScopeMode.KnownFile)
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project identifier is required.", nameof(projectId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(nodeKey);
        return new ProjectStructureNodeFileScopeKey(mode, Guid.Empty, projectId, nodeKey.Trim());
    }

    public FileToolsSemanticScopeId ToScopeId()
    {
        string mode = Mode switch
        {
            ProjectStructureNodeFileScopeMode.KnownFile => "known",
            ProjectStructureNodeFileScopeMode.Collection => "collection",
            _ => throw new ArgumentOutOfRangeException(nameof(Mode))
        };

        if (!IsProjected)
        {
            return new FileToolsSemanticScopeId($"{PersistedPrefix}:{mode}:{ProjectObjectId:N}");
        }

        if (ProjectId == Guid.Empty || string.IsNullOrWhiteSpace(NodeKey))
        {
            throw new InvalidOperationException("The projected project-node file scope is incomplete.");
        }

        return new FileToolsSemanticScopeId(
            $"{ProjectedPrefix}:{mode}:{ProjectId:N}:{EncodeNodeKey(NodeKey)}");
    }

    public static bool TryParse(FileToolsSemanticScopeId scopeId, out ProjectStructureNodeFileScopeKey key)
    {
        key = default;
        string[] parts = scopeId.Value.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length < 4 ||
            !string.Equals(parts[0], "node", StringComparison.Ordinal) ||
            !TryParseMode(parts[2], out ProjectStructureNodeFileScopeMode mode))
        {
            return false;
        }

        if (string.Equals(parts[1], "v1", StringComparison.Ordinal))
        {
            if (parts.Length != 4 ||
                !Guid.TryParseExact(parts[3], "N", out Guid projectObjectId) ||
                projectObjectId == Guid.Empty)
            {
                return false;
            }

            key = CreatePersisted(mode, projectObjectId);
            return true;
        }

        if (!string.Equals(parts[1], "v2", StringComparison.Ordinal) ||
            parts.Length != 5 ||
            mode != ProjectStructureNodeFileScopeMode.KnownFile ||
            !Guid.TryParseExact(parts[3], "N", out Guid projectId) ||
            projectId == Guid.Empty ||
            !TryDecodeNodeKey(parts[4], out string nodeKey))
        {
            return false;
        }

        key = CreateProjected(mode, projectId, nodeKey);
        return true;
    }

    private static bool TryParseMode(string value, out ProjectStructureNodeFileScopeMode mode)
    {
        ProjectStructureNodeFileScopeMode? parsed = value switch
        {
            "known" => ProjectStructureNodeFileScopeMode.KnownFile,
            "collection" => ProjectStructureNodeFileScopeMode.Collection,
            _ => null
        };
        mode = parsed.GetValueOrDefault();
        return parsed.HasValue;
    }

    private static string EncodeNodeKey(string nodeKey)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(nodeKey))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static bool TryDecodeNodeKey(string value, out string nodeKey)
    {
        nodeKey = string.Empty;
        try
        {
            string normalized = value
                .Replace('-', '+')
                .Replace('_', '/');
            int padding = normalized.Length % 4;
            if (padding > 0)
            {
                normalized = normalized.PadRight(normalized.Length + (4 - padding), '=');
            }

            nodeKey = Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
            return !string.IsNullOrWhiteSpace(nodeKey) &&
                   string.Equals(value, EncodeNodeKey(nodeKey), StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            nodeKey = string.Empty;
            return false;
        }
    }
}

public abstract record ProjectStructureFileCollectionRequest(Guid ProjectId, string DisplayName)
{
    public abstract string Identity { get; }
}

public sealed record ProjectStructureProjectFileCollectionRequest(Guid ProjectId, string DisplayName)
    : ProjectStructureFileCollectionRequest(ProjectId, DisplayName)
{
    public override string Identity => $"project:{ProjectId:N}";
}

public sealed record ProjectStructureNodeFileCollectionRequest(
    Guid ProjectId,
    string NodeId,
    string DisplayName)
    : ProjectStructureFileCollectionRequest(ProjectId, DisplayName)
{
    public override string Identity => $"node:{ProjectId:N}:{NodeId}";
}
