using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.AgentFramework.Models;

public enum AgentWorkspaceToolProfileKind
{
    Custom,
    ReadOnly,
    SoftwareDevelopment,
    QualityValidation,
    ArchitectureReview,
    SecurityReview,
    BusinessAnalysis
}

public enum AgentWorkspaceToolPermissionKind
{
    ReadFiles,
    WriteFiles,
    ManagePaths,
    RunValidationCommands,
    ScaffoldProjects,
    RunLocalScripts,
    TransformArtifacts
}

public static class AgentWorkspaceToolAccessProfiles
{
    public const string CustomProfileKey = "custom";
    public const string ReadOnlyProfileKey = "read-only";
    public const string SoftwareDevelopmentProfileKey = "software-development";
    public const string QualityValidationProfileKey = "quality-validation";
    public const string ArchitectureReviewProfileKey = "architecture-review";
    public const string SecurityReviewProfileKey = "security-review";
    public const string BusinessAnalysisProfileKey = "business-analysis";

    public static AgentWorkspaceToolAccessSettings CreateSettings(AgentWorkspaceToolProfileKind profile)
    {
        return profile switch
        {
            AgentWorkspaceToolProfileKind.SoftwareDevelopment => new AgentWorkspaceToolAccessSettings
            {
                Profile = profile,
                CanReadFiles = true,
                CanWriteFiles = true,
                CanRunValidationCommands = true,
                CanRunLocalScripts = true,
                CanScaffoldProjects = true,
                CanManageWorkspacePaths = true,
                CanTransformArtifacts = true
            },
            AgentWorkspaceToolProfileKind.QualityValidation => new AgentWorkspaceToolAccessSettings
            {
                Profile = profile,
                CanReadFiles = true,
                CanWriteFiles = true,
                CanRunValidationCommands = true,
                CanRunLocalScripts = true,
                CanTransformArtifacts = true
            },
            AgentWorkspaceToolProfileKind.ArchitectureReview => new AgentWorkspaceToolAccessSettings
            {
                Profile = profile,
                CanReadFiles = true,
                CanWriteFiles = true,
                CanTransformArtifacts = true
            },
            AgentWorkspaceToolProfileKind.SecurityReview => new AgentWorkspaceToolAccessSettings
            {
                Profile = profile,
                CanReadFiles = true,
                CanWriteFiles = true,
                CanRunValidationCommands = true,
                CanTransformArtifacts = true
            },
            AgentWorkspaceToolProfileKind.BusinessAnalysis => new AgentWorkspaceToolAccessSettings
            {
                Profile = profile,
                CanReadFiles = true,
                CanWriteFiles = true,
                CanTransformArtifacts = true
            },
            AgentWorkspaceToolProfileKind.ReadOnly => new AgentWorkspaceToolAccessSettings
            {
                Profile = profile,
                CanReadFiles = true
            },
            _ => new AgentWorkspaceToolAccessSettings
            {
                Profile = AgentWorkspaceToolProfileKind.Custom,
                CanReadFiles = true
            }
        };
    }

    public static string GetProfileKey(AgentWorkspaceToolProfileKind profile)
    {
        return profile switch
        {
            AgentWorkspaceToolProfileKind.ReadOnly => ReadOnlyProfileKey,
            AgentWorkspaceToolProfileKind.SoftwareDevelopment => SoftwareDevelopmentProfileKey,
            AgentWorkspaceToolProfileKind.QualityValidation => QualityValidationProfileKey,
            AgentWorkspaceToolProfileKind.ArchitectureReview => ArchitectureReviewProfileKey,
            AgentWorkspaceToolProfileKind.SecurityReview => SecurityReviewProfileKey,
            AgentWorkspaceToolProfileKind.BusinessAnalysis => BusinessAnalysisProfileKey,
            _ => CustomProfileKey
        };
    }

    public static bool TryParseProfileKey(string? profileKey, out AgentWorkspaceToolProfileKind profile)
    {
        profile = AgentWorkspaceToolProfileKind.Custom;
        if (string.IsNullOrWhiteSpace(profileKey))
        {
            return false;
        }

        var normalized = profileKey.Trim();
        if (string.Equals(normalized, CustomProfileKey, StringComparison.OrdinalIgnoreCase))
        {
            profile = AgentWorkspaceToolProfileKind.Custom;
            return true;
        }

        if (string.Equals(normalized, ReadOnlyProfileKey, StringComparison.OrdinalIgnoreCase))
        {
            profile = AgentWorkspaceToolProfileKind.ReadOnly;
            return true;
        }

        if (string.Equals(normalized, SoftwareDevelopmentProfileKey, StringComparison.OrdinalIgnoreCase))
        {
            profile = AgentWorkspaceToolProfileKind.SoftwareDevelopment;
            return true;
        }

        if (string.Equals(normalized, QualityValidationProfileKey, StringComparison.OrdinalIgnoreCase))
        {
            profile = AgentWorkspaceToolProfileKind.QualityValidation;
            return true;
        }

        if (string.Equals(normalized, ArchitectureReviewProfileKey, StringComparison.OrdinalIgnoreCase))
        {
            profile = AgentWorkspaceToolProfileKind.ArchitectureReview;
            return true;
        }

        if (string.Equals(normalized, SecurityReviewProfileKey, StringComparison.OrdinalIgnoreCase))
        {
            profile = AgentWorkspaceToolProfileKind.SecurityReview;
            return true;
        }

        if (string.Equals(normalized, BusinessAnalysisProfileKey, StringComparison.OrdinalIgnoreCase))
        {
            profile = AgentWorkspaceToolProfileKind.BusinessAnalysis;
            return true;
        }

        return false;
    }
}

public sealed class AgentWorkspaceToolAccessSettings
{
    public AgentWorkspaceToolProfileKind Profile { get; set; } = AgentWorkspaceToolProfileKind.Custom;

    public bool CanReadFiles { get; set; } = true;

    public bool CanWriteFiles { get; set; }

    public bool CanRunValidationCommands { get; set; }

    public bool CanRunLocalScripts { get; set; }

    public bool CanScaffoldProjects { get; set; }

    public bool CanManageWorkspacePaths { get; set; }

    public bool CanTransformArtifacts { get; set; }

    public List<string> AllowedExternalTargetAliases { get; set; } = [];

    public bool CanReadStorage { get; set; }

    public bool CanWriteStorage { get; set; }

    public bool AllowAllStorageCatalogs { get; set; }

    public List<Guid> AllowedStorageCatalogIds { get; set; } = [];
}

public static class AgentWorkspaceToolAccessMetadata
{
    private const string RootPropertyName = "workspaceTools";
    private const string ProfilePropertyName = "profile";
    private const string CanReadFilesPropertyName = "canReadFiles";
    private const string CanWriteFilesPropertyName = "canWriteFiles";
    private const string CanRunValidationCommandsPropertyName = "canRunValidationCommands";
    private const string CanRunLocalScriptsPropertyName = "canRunLocalScripts";
    private const string CanScaffoldProjectsPropertyName = "canScaffoldProjects";
    private const string CanManageWorkspacePathsPropertyName = "canManageWorkspacePaths";
    private const string CanTransformArtifactsPropertyName = "canTransformArtifacts";
    private const string AllowedExternalTargetAliasesPropertyName = "allowedExternalTargetAliases";
    private const string CanReadStoragePropertyName = "canReadStorage";
    private const string CanWriteStoragePropertyName = "canWriteStorage";
    private const string AllowAllStorageCatalogsPropertyName = "allowAllStorageCatalogs";
    private const string AllowedStorageCatalogIdsPropertyName = "allowedStorageCatalogIds";
    private const string ExternalTargetAliasRoot = "external-target";

    public static AgentWorkspaceToolAccessSettings Read(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return Normalize(new AgentWorkspaceToolAccessSettings());
        }

        try
        {
            var root = JsonNode.Parse(configurationJson)?.AsObject();
            var workspaceTools = root?[RootPropertyName]?.AsObject();
            if (workspaceTools is null)
            {
                return Normalize(new AgentWorkspaceToolAccessSettings());
            }

            var settings = new AgentWorkspaceToolAccessSettings
            {
                Profile = TryReadProfile(workspaceTools),
                CanReadFiles = TryReadBoolean(workspaceTools, CanReadFilesPropertyName, defaultValue: true),
                CanWriteFiles = TryReadBoolean(workspaceTools, CanWriteFilesPropertyName),
                CanRunValidationCommands = TryReadBoolean(workspaceTools, CanRunValidationCommandsPropertyName),
                CanRunLocalScripts = TryReadBoolean(workspaceTools, CanRunLocalScriptsPropertyName),
                CanScaffoldProjects = TryReadBoolean(workspaceTools, CanScaffoldProjectsPropertyName),
                CanManageWorkspacePaths = TryReadBoolean(workspaceTools, CanManageWorkspacePathsPropertyName),
                CanTransformArtifacts = TryReadBoolean(workspaceTools, CanTransformArtifactsPropertyName),
                CanReadStorage = TryReadBoolean(workspaceTools, CanReadStoragePropertyName),
                CanWriteStorage = TryReadBoolean(workspaceTools, CanWriteStoragePropertyName),
                AllowAllStorageCatalogs = TryReadBoolean(workspaceTools, AllowAllStorageCatalogsPropertyName)
            };

            if (workspaceTools[AllowedExternalTargetAliasesPropertyName] is JsonArray externalAliases)
            {
                settings.AllowedExternalTargetAliases = externalAliases
                    .Select(item => item?.GetValue<string>())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(NormalizeExternalTargetAlias)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (workspaceTools[AllowedStorageCatalogIdsPropertyName] is JsonArray storageIds)
            {
                settings.AllowedStorageCatalogIds = storageIds
                    .Select(item => item?.GetValue<string>())
                    .Where(item => Guid.TryParse(item, out _))
                    .Select(item => Guid.Parse(item!))
                    .Distinct()
                    .OrderBy(item => item)
                    .ToList();
            }

            return Normalize(settings);
        }
        catch (JsonException)
        {
            return Normalize(new AgentWorkspaceToolAccessSettings());
        }
    }

    public static string Write(
        string? configurationJson,
        AgentWorkspaceToolAccessSettings? settings)
    {
        var normalized = Normalize(settings ?? new AgentWorkspaceToolAccessSettings());
        var root = ParseObject(configurationJson);

        if (IsDefault(normalized))
        {
            root.Remove(RootPropertyName);
            return root.ToJsonString();
        }

        root[RootPropertyName] = new JsonObject
        {
            [ProfilePropertyName] = AgentWorkspaceToolAccessProfiles.GetProfileKey(normalized.Profile),
            [CanReadFilesPropertyName] = normalized.CanReadFiles,
            [CanWriteFilesPropertyName] = normalized.CanWriteFiles,
            [CanRunValidationCommandsPropertyName] = normalized.CanRunValidationCommands,
            [CanRunLocalScriptsPropertyName] = normalized.CanRunLocalScripts,
            [CanScaffoldProjectsPropertyName] = normalized.CanScaffoldProjects,
            [CanManageWorkspacePathsPropertyName] = normalized.CanManageWorkspacePaths,
            [CanTransformArtifactsPropertyName] = normalized.CanTransformArtifacts,
            [AllowedExternalTargetAliasesPropertyName] = new JsonArray(
                normalized.AllowedExternalTargetAliases
                    .Select(alias => JsonValue.Create(alias))
                    .ToArray()),
            [CanReadStoragePropertyName] = normalized.CanReadStorage,
            [CanWriteStoragePropertyName] = normalized.CanWriteStorage,
            [AllowAllStorageCatalogsPropertyName] = normalized.AllowAllStorageCatalogs,
            [AllowedStorageCatalogIdsPropertyName] = new JsonArray(
                normalized.AllowedStorageCatalogIds
                    .Select(storageId => JsonValue.Create(storageId.ToString("D")))
                    .ToArray())
        };

        return root.ToJsonString();
    }

    public static AgentWorkspaceToolAccessSettings Normalize(AgentWorkspaceToolAccessSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var profile = Enum.IsDefined(settings.Profile)
            ? settings.Profile
            : AgentWorkspaceToolProfileKind.Custom;
        var profileSettings = profile == AgentWorkspaceToolProfileKind.Custom
            ? new AgentWorkspaceToolAccessSettings { CanReadFiles = false }
            : AgentWorkspaceToolAccessProfiles.CreateSettings(profile);
        var allowedExternalAliases = settings.AllowedExternalTargetAliases
            .Select(NormalizeExternalTargetAlias)
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var allowedStorageIds = settings.AllowedStorageCatalogIds
            .Where(storageId => storageId != Guid.Empty)
            .Distinct()
            .OrderBy(storageId => storageId)
            .ToList();

        var canManageWorkspacePaths = settings.CanManageWorkspacePaths || profileSettings.CanManageWorkspacePaths;
        var canScaffoldProjects = settings.CanScaffoldProjects || profileSettings.CanScaffoldProjects;
        var canTransformArtifacts = settings.CanTransformArtifacts || profileSettings.CanTransformArtifacts;
        var canWriteFiles = settings.CanWriteFiles ||
                            profileSettings.CanWriteFiles ||
                            canManageWorkspacePaths ||
                            canScaffoldProjects ||
                            canTransformArtifacts;
        var canRunValidationCommands = settings.CanRunValidationCommands || profileSettings.CanRunValidationCommands;
        var canRunLocalScripts = settings.CanRunLocalScripts || profileSettings.CanRunLocalScripts;

        return new AgentWorkspaceToolAccessSettings
        {
            Profile = profile,
            CanReadFiles = settings.CanReadFiles ||
                           profileSettings.CanReadFiles ||
                           canWriteFiles ||
                           canRunValidationCommands ||
                           canRunLocalScripts ||
                           allowedExternalAliases.Count > 0,
            CanWriteFiles = canWriteFiles,
            CanRunValidationCommands = canRunValidationCommands,
            CanRunLocalScripts = canRunLocalScripts,
            CanScaffoldProjects = canScaffoldProjects,
            CanManageWorkspacePaths = canManageWorkspacePaths,
            CanTransformArtifacts = canTransformArtifacts,
            AllowedExternalTargetAliases = allowedExternalAliases,
            CanReadStorage = settings.CanReadStorage || settings.CanWriteStorage,
            CanWriteStorage = settings.CanWriteStorage,
            AllowAllStorageCatalogs = settings.AllowAllStorageCatalogs,
            AllowedStorageCatalogIds = allowedStorageIds
        };
    }

    public static bool IsWorkspaceToolAllowed(
        AgentWorkspaceToolAccessSettings settings,
        string? toolName)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!TryResolveWorkspaceToolPermission(toolName, out var permission))
        {
            return true;
        }

        var normalized = Normalize(settings);
        return permission switch
        {
            AgentWorkspaceToolPermissionKind.ReadFiles => normalized.CanReadFiles,
            AgentWorkspaceToolPermissionKind.WriteFiles => normalized.CanWriteFiles,
            AgentWorkspaceToolPermissionKind.ManagePaths => normalized.CanManageWorkspacePaths,
            AgentWorkspaceToolPermissionKind.RunValidationCommands => normalized.CanRunValidationCommands,
            AgentWorkspaceToolPermissionKind.ScaffoldProjects => normalized.CanScaffoldProjects,
            AgentWorkspaceToolPermissionKind.RunLocalScripts => normalized.CanRunLocalScripts,
            AgentWorkspaceToolPermissionKind.TransformArtifacts => normalized.CanTransformArtifacts,
            _ => false
        };
    }

    public static bool TryResolveWorkspaceToolPermission(
        string? toolName,
        out AgentWorkspaceToolPermissionKind permission)
    {
        permission = AgentWorkspaceToolPermissionKind.ReadFiles;
        var normalizedToolName = NormalizeWorkspaceToolName(toolName);
        if (string.IsNullOrWhiteSpace(normalizedToolName))
        {
            return false;
        }

        switch (normalizedToolName)
        {
            case "workspace_execution_boundary":
            case "workspace_list_files":
            case "workspace_search":
            case "workspace_read_file":
            case "workspace_stat_path":
            case "workspace_diff_text":
            case "workspace_git_status":
            case "workspace_git_diff":
            case "workspace_inspect_spreadsheet":
                permission = AgentWorkspaceToolPermissionKind.ReadFiles;
                return true;
            case "workspace_create_directory":
            case "workspace_write_file":
            case "workspace_append_file":
                permission = AgentWorkspaceToolPermissionKind.WriteFiles;
                return true;
            case "workspace_copy_path":
            case "workspace_move_path":
            case "workspace_delete_path":
                permission = AgentWorkspaceToolPermissionKind.ManagePaths;
                return true;
            case "workspace_dotnet_restore":
            case "workspace_dotnet_build":
            case "workspace_dotnet_test":
            case "workspace_dotnet_run":
                permission = AgentWorkspaceToolPermissionKind.RunValidationCommands;
                return true;
            case "workspace_dotnet_new":
                permission = AgentWorkspaceToolPermissionKind.ScaffoldProjects;
                return true;
            case "workspace_python_run_file":
            case "workspace_pwsh_run_script":
                permission = AgentWorkspaceToolPermissionKind.RunLocalScripts;
                return true;
            case "workspace_convert_document":
                permission = AgentWorkspaceToolPermissionKind.TransformArtifacts;
                return true;
            default:
                return false;
        }
    }

    public static string? NormalizeExternalTargetAlias(string? pathOrAlias)
    {
        if (string.IsNullOrWhiteSpace(pathOrAlias))
        {
            return null;
        }

        var trimmed = ExpandPortablePath(pathOrAlias).Replace('\\', '/').Trim().TrimEnd('/');
        while (trimmed.Contains("//", StringComparison.Ordinal))
        {
            trimmed = trimmed.Replace("//", "/", StringComparison.Ordinal);
        }

        if (trimmed.StartsWith($"{ExternalTargetAliasRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedAlias = NormalizeAliasCase(trimmed);
            return IsUsableExternalTargetAlias(normalizedAlias) ? normalizedAlias : null;
        }

        if (trimmed.Length == 2 &&
            char.IsLetter(trimmed[0]) &&
            trimmed[1] == ':')
        {
            return null;
        }

        string fullPath;
        try
        {
            if (!Path.IsPathRooted(trimmed))
            {
                return null;
            }

            fullPath = Path.GetFullPath(trimmed);
        }
        catch (Exception)
        {
            return null;
        }

        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        var trimmedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (trimmedRoot.Length != 2 ||
            trimmedRoot[1] != ':' ||
            !char.IsLetter(trimmedRoot[0]))
        {
            return null;
        }

        var relativeWithinDrive = fullPath.Length <= root.Length
            ? string.Empty
            : fullPath[root.Length..]
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/')
                .Trim('/');
        if (string.IsNullOrWhiteSpace(relativeWithinDrive))
        {
            return null;
        }

        return $"{ExternalTargetAliasRoot}/{char.ToUpperInvariant(trimmedRoot[0])}/{relativeWithinDrive}";
    }

    public static bool IsExternalTargetAliasAllowed(
        string? pathOrAlias,
        IReadOnlyList<string>? allowedAliases)
    {
        var normalizedAlias = NormalizeExternalTargetAlias(pathOrAlias);
        if (string.IsNullOrWhiteSpace(normalizedAlias))
        {
            return false;
        }

        return Normalize(settings: allowedAliases)
            .Any(allowed => IsAliasWithinRoot(normalizedAlias, allowed));
    }

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string>? settings)
    {
        return settings?
            .Select(NormalizeExternalTargetAlias)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];
    }

    private static bool IsDefault(AgentWorkspaceToolAccessSettings settings)
    {
        return settings.Profile == AgentWorkspaceToolProfileKind.Custom &&
               settings.CanReadFiles &&
               !settings.CanWriteFiles &&
               !settings.CanRunValidationCommands &&
               !settings.CanRunLocalScripts &&
               !settings.CanScaffoldProjects &&
               !settings.CanManageWorkspacePaths &&
               !settings.CanTransformArtifacts &&
               settings.AllowedExternalTargetAliases.Count == 0 &&
               !settings.CanReadStorage &&
               !settings.CanWriteStorage &&
               !settings.AllowAllStorageCatalogs &&
               settings.AllowedStorageCatalogIds.Count == 0;
    }

    private static bool TryReadBoolean(JsonObject node, string propertyName, bool defaultValue = false)
    {
        return node[propertyName] is JsonValue value && value.TryGetValue<bool>(out var parsedValue)
            ? parsedValue
            : defaultValue;
    }

    private static AgentWorkspaceToolProfileKind TryReadProfile(JsonObject node)
    {
        if (node[ProfilePropertyName] is not JsonValue value ||
            !value.TryGetValue<string>(out var profileKey) ||
            !AgentWorkspaceToolAccessProfiles.TryParseProfileKey(profileKey, out var profile))
        {
            return AgentWorkspaceToolProfileKind.Custom;
        }

        return profile;
    }

    private static string NormalizeWorkspaceToolName(string? toolName)
    {
        return string.IsNullOrWhiteSpace(toolName)
            ? string.Empty
            : toolName.Trim().Replace('-', '_');
    }

    private static JsonObject ParseObject(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(configurationJson)?.AsObject() ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private static string ExpandPortablePath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        if (string.Equals(expanded, "~", StringComparison.Ordinal))
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (expanded.StartsWith("~/", StringComparison.Ordinal) || expanded.StartsWith("~\\", StringComparison.Ordinal))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), expanded[2..]);
        }

        return expanded;
    }

    private static string NormalizeAliasCase(string alias)
    {
        var segments = alias.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length == 0
            ? string.Empty
            : string.Join('/', segments.Select((segment, index) =>
                index == 0
                    ? ExternalTargetAliasRoot
                    : index == 1 && segment.Length == 1 && char.IsLetter(segment[0])
                        ? char.ToUpperInvariant(segment[0]).ToString()
                        : segment));
    }

    private static bool IsUsableExternalTargetAlias(string alias)
    {
        var segments = alias.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length > 2 &&
               string.Equals(segments[0], ExternalTargetAliasRoot, StringComparison.OrdinalIgnoreCase) &&
               segments[1].Length == 1 &&
               char.IsLetter(segments[1][0]);
    }

    private static bool IsAliasWithinRoot(string alias, string rootAlias)
    {
        return string.Equals(alias, rootAlias, StringComparison.OrdinalIgnoreCase) ||
               alias.StartsWith(rootAlias.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);
    }
}
