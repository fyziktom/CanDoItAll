using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CanDoItAll.SharedKernel;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Preset of workspace tool permissions, as a JSON integer: 0 Custom (no preset; only the explicit flags apply),
/// 1 ReadOnly (read files), 2 SoftwareDevelopment (read, write, validation commands, local scripts, scaffolding, path
/// management, artifact transformation), 3 QualityValidation (read, write, validation commands, local scripts,
/// artifact transformation), 4 ArchitectureReview (read, write, artifact transformation), 5 SecurityReview (read,
/// write, validation commands, artifact transformation), 6 BusinessAnalysis (read, write, artifact transformation). A
/// preset is a minimum: its permissions are added to the explicit flags and cannot be switched off by them.
/// </summary>
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

/// <summary>
/// Workspace file, command and storage tool permissions of an agent, the <c>workspaceToolAccess</c> member of the agent
/// editor form, stored in the agent's <c>configurationJson</c> under <c>workspaceTools</c>. A save replaces that
/// section. When the section is absent, the agent has the default: profile Custom with only file reading allowed; an
/// omitted object in a save therefore resets the agent to that default. The tools also need the agent's
/// <c>canUseTools</c> permission, and tools that change files or run commands need approval of each call unless the
/// run suppresses approvals.
/// </summary>
public sealed class AgentWorkspaceToolAccessSettings
{
    /// <summary>
    /// Permission preset, as a JSON integer: 0 Custom (the default), 1 ReadOnly, 2 SoftwareDevelopment, 3
    /// QualityValidation, 4 ArchitectureReview, 5 SecurityReview, 6 BusinessAnalysis. The preset's permissions are
    /// added to the explicit flags below and cannot be switched off by them; see the
    /// <c>AgentWorkspaceToolProfileKind</c> schema for what each preset grants. An undefined value is stored as Custom.
    /// </summary>
    public AgentWorkspaceToolProfileKind Profile { get; set; } = AgentWorkspaceToolProfileKind.Custom;

    /// <summary>
    /// Allows reading workspace files and folders (listing, search, read, compare, git history and inspection). True by
    /// default, and implied by every write, command or external-folder permission.
    /// </summary>
    public bool CanReadFiles { get; set; } = true;

    /// <summary>
    /// Allows creating and writing workspace files (including archives and spreadsheets). Implied by
    /// <c>canManageWorkspacePaths</c>, <c>canScaffoldProjects</c> and <c>canTransformArtifacts</c>.
    /// </summary>
    public bool CanWriteFiles { get; set; }

    /// <summary>Allows running the .NET restore, build, test, run and stop commands in the workspace.</summary>
    public bool CanRunValidationCommands { get; set; }

    /// <summary>Allows running Python files and PowerShell scripts from the workspace.</summary>
    public bool CanRunLocalScripts { get; set; }

    /// <summary>Allows creating projects from .NET templates in the workspace.</summary>
    public bool CanScaffoldProjects { get; set; }

    /// <summary>
    /// Allows copying, moving and deleting workspace paths and changing git state (stage, commit, branch).
    /// </summary>
    public bool CanManageWorkspacePaths { get; set; }

    /// <summary>
    /// Allows converting documents and analyzing images; it also enables image analysis of Project Structure assets
    /// for agents with Project Structure read access.
    /// </summary>
    public bool CanTransformArtifacts { get; set; }

    /// <summary>
    /// External folders outside the workspace that the agent may write under, as external-target aliases of the form
    /// <c>external-target/v1/{rootId}/{encoded path}</c>. Values that are not aliases are dropped silently; legacy
    /// aliases that embed a drive letter are rejected. A plain file-system path cannot be sent here. Send back the
    /// values as read.
    /// </summary>
    public List<string> AllowedExternalTargetAliases { get; set; } = [];

    /// <summary>
    /// Server-issued root bindings that let the aliases in <c>allowedExternalTargetAliases</c> resolve to folders on
    /// the host. Send back the values as read; bindings that no alias uses are removed on save.
    /// </summary>
    public List<ExternalTargetRootBinding> ExternalTargetRootBindings { get; set; } = [];

    /// <summary>
    /// Adds the storage tools that list catalogs, browse and read text files of the permitted catalogs.
    /// </summary>
    public bool CanReadStorage { get; set; }

    /// <summary>
    /// Adds the storage tools that write text files and delete objects in the permitted catalogs (approval required by
    /// default); implies <c>canReadStorage</c>.
    /// </summary>
    public bool CanWriteStorage { get; set; }

    /// <summary>True to permit every storage catalog; <c>allowedStorageCatalogIds</c> is then not needed.</summary>
    public bool AllowAllStorageCatalogs { get; set; }

    /// <summary>
    /// Identifiers of the storage catalogs the storage tools may use when <c>allowAllStorageCatalogs</c> is false. They
    /// are not checked for existence when saved.
    /// </summary>
    public List<Guid> AllowedStorageCatalogIds { get; set; } = [];
}

public sealed record EffectiveExternalTargetAccessScope(
    IReadOnlyList<string> WritableAliases,
    IReadOnlyList<string> ReadOnlyAliases)
{
    public static EffectiveExternalTargetAccessScope Empty { get; } = new([], []);

    public bool CanRead(string? pathOrAlias)
    {
        return ResolveMostSpecificMatchLength(pathOrAlias, WritableAliases) >= 0 ||
               ResolveMostSpecificMatchLength(pathOrAlias, ReadOnlyAliases) >= 0;
    }

    public bool CanWrite(string? pathOrAlias)
    {
        var writableMatchLength = ResolveMostSpecificMatchLength(pathOrAlias, WritableAliases);
        if (writableMatchLength < 0)
        {
            return false;
        }

        var readOnlyMatchLength = ResolveMostSpecificMatchLength(pathOrAlias, ReadOnlyAliases);
        return writableMatchLength >= readOnlyMatchLength;
    }

    public bool HasEffectiveReadOnlyDescendant(string? pathOrAlias)
    {
        var normalizedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(pathOrAlias);
        if (string.IsNullOrWhiteSpace(normalizedAlias))
        {
            return false;
        }

        return ReadOnlyAliases
            .Select(AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias)
            .Where(readOnlyAlias =>
                !string.IsNullOrWhiteSpace(readOnlyAlias) &&
                !ExternalTargetAliasCodec.EqualityComparer.Equals(readOnlyAlias, normalizedAlias) &&
                ExternalTargetAliasCodec.IsAliasWithinRoot(readOnlyAlias, normalizedAlias))
            .Any(readOnlyAlias => !CanWrite(readOnlyAlias));
    }

    private static int ResolveMostSpecificMatchLength(
        string? pathOrAlias,
        IReadOnlyList<string> aliases)
    {
        var normalizedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(pathOrAlias);
        if (string.IsNullOrWhiteSpace(normalizedAlias))
        {
            return -1;
        }

        return aliases
            .Select(AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias)
            .Where(rootAlias =>
                !string.IsNullOrWhiteSpace(rootAlias) &&
                ExternalTargetAliasCodec.IsAliasWithinRoot(normalizedAlias, rootAlias))
            .Select(rootAlias => rootAlias!.Length)
            .DefaultIfEmpty(-1)
            .Max();
    }
}

public static class EffectiveExternalTargetAccessResolver
{
    public static EffectiveExternalTargetAccessScope Resolve(
        AgentWorkspaceToolAccessSettings? configuredAccess,
        IReadOnlyList<string>? runWritableAliases = null,
        IReadOnlyList<string>? runReadOnlyAliases = null,
        bool invocationScopeIsAuthoritative = false)
    {
        var configuredWritableAliases = invocationScopeIsAuthoritative
            ? []
            : NormalizeAliases(configuredAccess?.AllowedExternalTargetAliases);
        var invocationWritableAliases = NormalizeAliases(runWritableAliases);
        var invocationReadOnlyAliases = NormalizeAliases(runReadOnlyAliases);
        var writableAliases = configuredWritableAliases
            .Where(configuredAlias =>
                !AgentWorkspaceToolAccessMetadata.IsExternalTargetAliasAllowed(
                    configuredAlias,
                    invocationReadOnlyAliases))
            .Concat(invocationWritableAliases)
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .ToArray();
        var readOnlyAliases = invocationReadOnlyAliases
            .Where(alias => !invocationWritableAliases.Contains(
                alias,
                ExternalTargetAliasCodec.EqualityComparer))
            .ToArray();

        return new EffectiveExternalTargetAccessScope(writableAliases, readOnlyAliases);
    }

    private static IReadOnlyList<string> NormalizeAliases(IEnumerable<string>? aliases)
    {
        return aliases?
            .Select(AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias)
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Cast<string>()
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .ToArray()
            ?? [];
    }
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
    private const string ExternalTargetRootBindingsPropertyName = "externalTargetRootBindings";
    private const string CanReadStoragePropertyName = "canReadStorage";
    private const string CanWriteStoragePropertyName = "canWriteStorage";
    private const string AllowAllStorageCatalogsPropertyName = "allowAllStorageCatalogs";
    private const string AllowedStorageCatalogIdsPropertyName = "allowedStorageCatalogIds";
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

            if (workspaceTools[ExternalTargetRootBindingsPropertyName] is JsonArray rootBindings)
            {
                settings.ExternalTargetRootBindings = rootBindings
                    .Select(item => item as JsonObject ??
                        throw new InvalidOperationException(
                            "An external-target root binding must be a JSON object."))
                    .Select(binding => new ExternalTargetRootBinding(
                        binding["rootId"]?.GetValue<string>() ?? string.Empty,
                        binding["hostPlatform"]?.GetValue<string>() ?? string.Empty,
                        binding["protectedRootToken"]?.GetValue<string>() ?? string.Empty))
                    .ToList();
            }

            if (workspaceTools[AllowedExternalTargetAliasesPropertyName] is JsonArray externalAliases)
            {
                settings.AllowedExternalTargetAliases = externalAliases
                    .Select(item => item?.GetValue<string>())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(NormalizeExternalTargetAlias)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Cast<string>()
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal)
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
        return Write(configurationJson, settings, externalTargetRegistry: null);
    }

    public static string Write(
        string? configurationJson,
        AgentWorkspaceToolAccessSettings? settings,
        IExternalTargetPathRegistry? externalTargetRegistry)
    {
        var normalized = Normalize(settings ?? new AgentWorkspaceToolAccessSettings());
        var root = ParseObject(configurationJson);

        if (IsDefault(normalized))
        {
            root.Remove(RootPropertyName);
            return root.ToJsonString();
        }

        var persistedAliases = normalized.AllowedExternalTargetAliases
            .Select(alias => ExternalTargetAliasCodec.NormalizeVersionedAlias(alias) ??
                externalTargetRegistry?.MigrateLegacyAliasForWrite(alias) ??
                throw new InvalidOperationException(
                    "A legacy external-target alias requires an Infrastructure-owned registry to migrate it for writing."))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(alias => alias, StringComparer.Ordinal)
            .ToArray();
        var generatedBindings = externalTargetRegistry?.ExportBindings(persistedAliases) ?? [];
        var rootBindings = normalized.ExternalTargetRootBindings
            .Concat(generatedBindings)
            .GroupBy(binding => binding.RootId, StringComparer.Ordinal)
            .Select(group => group.Distinct().Count() == 1
                ? group.First()
                : throw new InvalidOperationException(
                    $"Conflicting external-target root bindings use identity '{group.Key}'."))
            .Where(binding => persistedAliases.Any(alias => AliasUsesRoot(alias, binding.RootId)))
            .OrderBy(binding => binding.RootId, StringComparer.Ordinal)
            .ToArray();
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
                persistedAliases
                    .Select(alias => JsonValue.Create(alias))
                    .ToArray()),
            [ExternalTargetRootBindingsPropertyName] = new JsonArray(
                rootBindings
                    .Select(binding => new JsonObject
                    {
                        ["rootId"] = binding.RootId,
                        ["hostPlatform"] = binding.HostPlatform,
                        ["protectedRootToken"] = binding.ProtectedRootToken
                    })
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
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .OrderBy(alias => alias, StringComparer.Ordinal)
            .ToList();
        var referencedRootIds = allowedExternalAliases
            .Select(alias => ExternalTargetAliasCodec.TryParseVersionedAlias(alias, out var rootId, out _, out _)
                ? rootId
                : null)
            .Where(rootId => rootId is not null)
            .ToHashSet(StringComparer.Ordinal);
        if (settings.ExternalTargetRootBindings.Any(binding =>
                binding is null ||
                binding.RootId.Length != ExternalTargetAliasCodec.RootIdLength ||
                !binding.RootId.All(Uri.IsHexDigit) ||
                string.IsNullOrWhiteSpace(binding.HostPlatform) ||
                string.IsNullOrWhiteSpace(binding.ProtectedRootToken)))
        {
            throw new InvalidOperationException("An external-target root binding is malformed.");
        }

        var externalTargetRootBindings = settings.ExternalTargetRootBindings
            .Select(binding => binding with
            {
                RootId = binding.RootId.ToLowerInvariant(),
                HostPlatform = binding.HostPlatform.Trim().ToLowerInvariant()
            })
            .Where(binding => referencedRootIds.Contains(binding.RootId))
            .Distinct()
            .OrderBy(binding => binding.RootId, StringComparer.Ordinal)
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
            ExternalTargetRootBindings = externalTargetRootBindings,
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
            case "workspace_list_directory":
            case "workspace_list_files":
            case "workspace_search":
            case "workspace_read_file":
            case "workspace_stat_path":
            case "workspace_hash_path":
            case "workspace_diff_text":
            case "workspace_git_status":
            case "workspace_git_diff":
            case "workspace_git_log":
            case "workspace_git_show":
            case "workspace_inspect_spreadsheet":
            case "workspace_spreadsheet_summary":
            case "workspace_read_spreadsheet_cell":
            case "workspace_read_spreadsheet_range":
            case "workspace_spreadsheet_function_catalog":
            case "workspace_inspect_image":
                permission = AgentWorkspaceToolPermissionKind.ReadFiles;
                return true;
            case "workspace_create_directory":
            case "workspace_write_file":
            case "workspace_append_file":
            case "workspace_zip_path":
            case "workspace_unzip_archive":
            case "workspace_write_spreadsheet":
                permission = AgentWorkspaceToolPermissionKind.WriteFiles;
                return true;
            case "workspace_copy_path":
            case "workspace_move_path":
            case "workspace_delete_path":
            case "workspace_git_add":
            case "workspace_git_unstage":
            case "workspace_git_commit":
            case "workspace_git_branch_create":
            case "workspace_git_switch":
                permission = AgentWorkspaceToolPermissionKind.ManagePaths;
                return true;
            case "workspace_dotnet_restore":
            case "workspace_dotnet_build":
            case "workspace_dotnet_publish":
            case "workspace_static_serve":
            case "workspace_dotnet_test":
            case "workspace_dotnet_run":
            case "workspace_dotnet_stop":
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
            case "workspace_analyze_image":
            case "workspace_analyze_images":
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

        var trimmed = pathOrAlias.Trim();
        if (!ExternalTargetAliasCodec.IsAnyAlias(trimmed))
        {
            return null;
        }

        var annotationStripped = StripInlineExternalTargetAliasAnnotations(
                StripEscapedLineBreakPathAnnotations(trimmed))
            .TrimEnd(',', ';', ':', ')', ']', '}');

        var versionedAlias = ExternalTargetAliasCodec.NormalizeVersionedAlias(annotationStripped);
        if (!string.IsNullOrWhiteSpace(versionedAlias))
        {
            return versionedAlias;
        }

        return ExternalTargetAliasCodec.TryNormalizeLegacyAlias(annotationStripped, out var legacyAlias)
            ? legacyAlias
            : null;
    }

    public static string? NormalizeExternalTargetAlias(
        string? pathOrAlias,
        IExternalTargetPathRegistry externalTargetRegistry)
    {
        ArgumentNullException.ThrowIfNull(externalTargetRegistry);
        var normalizedAlias = NormalizeExternalTargetAlias(pathOrAlias);
        if (!string.IsNullOrWhiteSpace(normalizedAlias))
        {
            return ExternalTargetAliasCodec.IsVersionedAlias(normalizedAlias)
                ? normalizedAlias
                : externalTargetRegistry.MigrateLegacyAliasForWrite(normalizedAlias);
        }

        if (string.IsNullOrWhiteSpace(pathOrAlias))
        {
            return null;
        }

        return externalTargetRegistry.TryCreateAlias(pathOrAlias.Trim(), out var alias)
            ? alias
            : null;
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
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
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
               settings.ExternalTargetRootBindings.Count == 0 &&
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

    private static string StripEscapedLineBreakPathAnnotations(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(
                value,
                @"(?i)(?:\\|/)n(?:Acceptance|Accepted|Approved|Alias|Aliases|All|App|Application|Architecture|Archetype|Code|Deliverable|Directory|Escalation|Exact|Feature|Features|Files?|Generated|Mapped|Mapping|Node|Notes?|Output|Path|Product|Project|Requirement|Requirements|Required|Root|Source|Status|Workspace|Worksp|Evidence|Validation|Validate|Tests?|Startup|Browser|Agents?|Use|The|This|Then|Next)\b.*$",
                string.Empty,
                RegexOptions.CultureInvariant)
            .Trim();
    }

    private static string StripInlineExternalTargetAliasAnnotations(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(
                value,
                @"(?i)\s+(?:Workspace\s+alias|Mapped\s+alias|All\s+generated|All\s+app(?:lication)?|Generated\s+app(?:lication)?|App(?:lication)?\s+source|Source\s+belongs|Code\s+belongs|Files?\s+belong|Output\s+directory|Acceptance|Accepted|Approved|Architecture|Archetype|Deliverable|Escalation|Exact|Feature|Features|Requirement|Requirements|Required|Evidence|Validation|Validate|Tests?|Startup|Browser|Agents?\s+must|Use\s+only|Do\s+not|The\s+app|This\s+app)\b.*$",
                string.Empty,
                RegexOptions.CultureInvariant)
            .Trim();
    }

    private static bool IsAliasWithinRoot(string alias, string rootAlias)
    {
        return ExternalTargetAliasCodec.IsAliasWithinRoot(alias, rootAlias);
    }

    private static bool AliasUsesRoot(string alias, string rootId)
    {
        return ExternalTargetAliasCodec.TryParseVersionedAlias(alias, out var aliasRootId, out _, out _) &&
               string.Equals(aliasRootId, rootId, StringComparison.Ordinal);
    }
}
