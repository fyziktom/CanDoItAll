using CanDoItAll.FileTools.Integration;

namespace CanDoItAll.Modules.Workbench;

internal enum ProjectStructureNodeFileScopeMode
{
    KnownFile,
    Collection
}

internal readonly record struct ProjectStructureNodeFileScopeKey(
    ProjectStructureNodeFileScopeMode Mode,
    Guid ProjectObjectId)
{
    private const string Prefix = "node:v1";

    public FileToolsSemanticScopeId ToScopeId()
    {
        string mode = Mode switch
        {
            ProjectStructureNodeFileScopeMode.KnownFile => "known",
            ProjectStructureNodeFileScopeMode.Collection => "collection",
            _ => throw new ArgumentOutOfRangeException(nameof(Mode))
        };
        return new FileToolsSemanticScopeId($"{Prefix}:{mode}:{ProjectObjectId:N}");
    }

    public static bool TryParse(FileToolsSemanticScopeId scopeId, out ProjectStructureNodeFileScopeKey key)
    {
        key = default;
        string[] parts = scopeId.Value.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 4 ||
            !string.Equals(parts[0], "node", StringComparison.Ordinal) ||
            !string.Equals(parts[1], "v1", StringComparison.Ordinal) ||
            !Guid.TryParseExact(parts[3], "N", out Guid projectObjectId) ||
            projectObjectId == Guid.Empty)
        {
            return false;
        }

        ProjectStructureNodeFileScopeMode? mode = parts[2] switch
        {
            "known" => ProjectStructureNodeFileScopeMode.KnownFile,
            "collection" => ProjectStructureNodeFileScopeMode.Collection,
            _ => null
        };
        if (!mode.HasValue)
        {
            return false;
        }

        key = new ProjectStructureNodeFileScopeKey(mode.Value, projectObjectId);
        return true;
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
