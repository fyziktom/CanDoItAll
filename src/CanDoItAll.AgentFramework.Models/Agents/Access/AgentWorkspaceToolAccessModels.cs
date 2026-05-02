using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.AgentFramework.Models;

public sealed class AgentWorkspaceToolAccessSettings
{
    public bool CanReadFiles { get; set; } = true;

    public bool CanWriteFiles { get; set; }

    public List<string> AllowedExternalTargetAliases { get; set; } = [];

    public bool CanReadStorage { get; set; }

    public bool CanWriteStorage { get; set; }

    public bool AllowAllStorageCatalogs { get; set; }

    public List<Guid> AllowedStorageCatalogIds { get; set; } = [];
}

public static class AgentWorkspaceToolAccessMetadata
{
    private const string RootPropertyName = "workspaceTools";
    private const string CanReadFilesPropertyName = "canReadFiles";
    private const string CanWriteFilesPropertyName = "canWriteFiles";
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
                CanReadFiles = TryReadBoolean(workspaceTools, CanReadFilesPropertyName, defaultValue: true),
                CanWriteFiles = TryReadBoolean(workspaceTools, CanWriteFilesPropertyName),
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
            [CanReadFilesPropertyName] = normalized.CanReadFiles,
            [CanWriteFilesPropertyName] = normalized.CanWriteFiles,
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

        return new AgentWorkspaceToolAccessSettings
        {
            CanReadFiles = settings.CanReadFiles || settings.CanWriteFiles || allowedExternalAliases.Count > 0,
            CanWriteFiles = settings.CanWriteFiles,
            AllowedExternalTargetAliases = allowedExternalAliases,
            CanReadStorage = settings.CanReadStorage || settings.CanWriteStorage,
            CanWriteStorage = settings.CanWriteStorage,
            AllowAllStorageCatalogs = settings.AllowAllStorageCatalogs,
            AllowedStorageCatalogIds = allowedStorageIds
        };
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
        return settings.CanReadFiles &&
               !settings.CanWriteFiles &&
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
