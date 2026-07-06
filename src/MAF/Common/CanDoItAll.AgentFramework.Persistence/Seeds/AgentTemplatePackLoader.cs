using System.Reflection;
using System.Text.Json;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class AgentTemplatePackLoader
{
    private const string ManifestFileName = "manifest.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string? configuredPackRoot;
    private readonly Lazy<AgentTemplatePack> pack;

    public AgentTemplatePackLoader(string? packRoot = null)
    {
        configuredPackRoot = packRoot;
        pack = new Lazy<AgentTemplatePack>(LoadCore, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public AgentTemplatePack Load() => pack.Value;

    public static string FindPackRoot(string? packRoot = null) => ResolvePackRoot(packRoot);

    private AgentTemplatePack LoadCore()
    {
        var root = ResolvePackRoot(configuredPackRoot);
        var manifest = ReadJson<AgentTemplatePackManifest>(Path.Combine(root, ManifestFileName));
        var teams = new List<AgentTemplateTeam>();
        var memberKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var teamReference in manifest.Teams.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            var teamRoot = Path.GetFullPath(Path.Combine(root, Require(teamReference.RelativePath, "team relative path")));
            var team = ReadJson<AgentTemplateTeam>(Path.Combine(teamRoot, "team.json"));
            team.RelativePath = teamReference.RelativePath.Replace("\\", "/");
            team.RootPath = teamRoot;
            team.MemberTemplates = LoadMembers(team, teamRoot, memberKeys);
            teams.Add(team);
        }

        return new AgentTemplatePack(root, manifest, teams);
    }

    private static IReadOnlyList<AgentTemplateMember> LoadMembers(
        AgentTemplateTeam team,
        string teamRoot,
        HashSet<string> memberKeys)
    {
        var members = new List<AgentTemplateMember>();
        foreach (var memberReference in team.Members.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!memberKeys.Add(memberReference.Key))
            {
                throw new InvalidOperationException(
                    $"Agent template member '{memberReference.Key}' appears in more than one team.");
            }

            var memberRoot = Path.GetFullPath(Path.Combine(teamRoot, Require(memberReference.RelativePath, "member relative path")));
            var settings = ReadJson<AgentTemplateSettings>(Path.Combine(memberRoot, "settings.json"));
            var skills = ReadJson<AgentTemplateSkills>(Path.Combine(memberRoot, "skills.json"));
            var instructionsPath = Path.Combine(memberRoot, "instructions.md");
            if (!File.Exists(instructionsPath))
            {
                throw new InvalidOperationException($"Agent template instructions '{instructionsPath}' were not found.");
            }

            members.Add(new AgentTemplateMember(
                memberReference.Key,
                memberReference.RelativePath.Replace("\\", "/"),
                memberRoot,
                File.ReadAllText(instructionsPath).Trim(),
                settings,
                skills));
        }

        return members;
    }

    private static T ReadJson<T>(string path)
        where T : class, new()
    {
        try
        {
            return JsonFileLoader.ReadRequired<T>(path, JsonOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidOperationException(
                $"Agent template JSON file '{path}' could not be loaded: {exception.Message}",
                exception);
        }
    }

    private static string ResolvePackRoot(string? explicitRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            var normalizedExplicitRoot = Path.GetFullPath(explicitRoot);
            if (File.Exists(Path.Combine(normalizedExplicitRoot, ManifestFileName)))
            {
                return normalizedExplicitRoot;
            }

            if (File.Exists(normalizedExplicitRoot) &&
                string.Equals(Path.GetFileName(normalizedExplicitRoot), ManifestFileName, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetDirectoryName(normalizedExplicitRoot)!;
            }
        }

        var relativeManifestPath = Path.Combine(
            AgentTemplatePackOptions.TemplatesRootDirectoryName,
            AgentTemplatePackOptions.AgentsDirectoryName,
            ManifestFileName);
        var discoveredRoot = AncestorFileLocator.FindContainingDirectory(
            relativeManifestPath,
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
        if (!string.IsNullOrWhiteSpace(discoveredRoot))
        {
            return discoveredRoot;
        }

        throw new InvalidOperationException(
            $"Unable to locate {AgentTemplatePackOptions.DefaultRelativePackRoot}/{ManifestFileName} from the current execution root.");
    }

    private static string Require(string value, string label)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Agent template {label} is required.")
            : value.Trim();
    }
}

internal static class AgentTemplatePackOptions
{
    public const string TemplatesRootDirectoryName = "Templates";
    public const string AgentsDirectoryName = "Agents";
    public const string DefaultRelativePackRoot = TemplatesRootDirectoryName + "/" + AgentsDirectoryName;
}

internal sealed record AgentTemplatePack(
    string RootPath,
    AgentTemplatePackManifest Manifest,
    IReadOnlyList<AgentTemplateTeam> Teams);

internal sealed class AgentTemplatePackManifest
{
    public string PackKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string SeedMarker { get; set; } = string.Empty;

    public string SeedVersion { get; set; } = string.Empty;

    public List<AgentTemplateTeamReference> Teams { get; set; } = [];
}

internal sealed class AgentTemplateTeamReference
{
    public string Key { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;
}

internal sealed class AgentTemplateTeam
{
    public string Key { get; set; } = string.Empty;

    public string StableIdKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public JsonElement Settings { get; set; }

    public List<AgentTemplateMemberReference> Members { get; set; } = [];

    public string RelativePath { get; set; } = string.Empty;

    public string RootPath { get; set; } = string.Empty;

    public IReadOnlyList<AgentTemplateMember> MemberTemplates { get; set; } = [];
}

internal sealed class AgentTemplateMemberReference
{
    public string Key { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;
}

internal sealed record AgentTemplateMember(
    string Key,
    string RelativePath,
    string RootPath,
    string Instructions,
    AgentTemplateSettings Settings,
    AgentTemplateSkills Skills);

internal sealed class AgentTemplateSettings
{
    public string StableIdKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string RoleTitle { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string AvatarImageUrl { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string ProviderProfileKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string Workload { get; set; } = string.Empty;

    public string ChatHistoryMode { get; set; } = string.Empty;

    public double Temperature { get; set; } = 0.2d;

    public bool RequirePerServiceCallChatHistoryPersistence { get; set; }

    public bool EnableBackgroundResponses { get; set; }

    public Dictionary<string, JsonElement> Configuration { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool ApplyDefaultReasoningEffort { get; set; } = true;

    public bool IsTemplate { get; set; }

    public string TemplateKey { get; set; } = string.Empty;

    public AgentTemplatePermissions Permissions { get; set; } = new();

    public AgentTemplateAccess Access { get; set; } = new();

    public List<string> Tags { get; set; } = [];
}

internal sealed class AgentTemplatePermissions
{
    public bool? CanUseTools { get; set; }

    public bool? CanAskOtherAgents { get; set; }

    public bool? CanEscalateToHuman { get; set; }

    public bool? CanObserveOtherAgents { get; set; }

    public bool? CanScheduleWork { get; set; }

    public bool? RequiresApprovalForExternalCalls { get; set; }

    public bool? AutoApproveExternalCallsByDefault { get; set; }
}

internal sealed class AgentTemplateAccess
{
    public AgentTemplateProjectStructureAccess? ProjectStructure { get; set; }

    public AgentTemplateProcessAccess? Processes { get; set; }

    public AgentTemplateWorkspaceToolAccess? WorkspaceTools { get; set; }

    public AgentTemplateImageGenerationAccess? ImageGeneration { get; set; }
}

internal sealed class AgentTemplateProjectStructureAccess
{
    public bool CanRead { get; set; }

    public bool CanWrite { get; set; }

    public bool AllowAllProjects { get; set; }

    public List<Guid> AllowedProjectIds { get; set; } = [];
}

internal sealed class AgentTemplateProcessAccess
{
    public bool CanRead { get; set; }

    public bool CanWrite { get; set; }

    public bool AllowAllDefinitions { get; set; }

    public List<Guid> AllowedDefinitionIds { get; set; } = [];
}

internal sealed class AgentTemplateWorkspaceToolAccess
{
    public string Profile { get; set; } = string.Empty;

    public bool? CanReadFiles { get; set; }

    public bool? CanWriteFiles { get; set; }

    public bool? CanManageWorkspacePaths { get; set; }

    public bool? CanRunValidationCommands { get; set; }

    public bool? CanScaffoldProjects { get; set; }

    public bool? CanRunLocalScripts { get; set; }

    public bool? CanTransformArtifacts { get; set; }

    public bool CanReadStorage { get; set; }

    public bool CanWriteStorage { get; set; }

    public bool AllowAllStorageCatalogs { get; set; }

    public List<Guid> AllowedStorageCatalogIds { get; set; } = [];

    public List<string> AllowedExternalTargetAliases { get; set; } = [];
}

internal sealed class AgentTemplateImageGenerationAccess
{
    public bool CanGenerateImages { get; set; }

    public string PreferredProviderProfileKey { get; set; } = string.Empty;

    public string DefaultModel { get; set; } = string.Empty;

    public bool CanStoreImagesAsProjectAssets { get; set; }
}

internal sealed class AgentTemplateSkills
{
    public List<string> CapabilityKeys { get; set; } = [];

    public string Notes { get; set; } = string.Empty;
}
