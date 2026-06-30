using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static partial class ProjectStructureDotNetRuntimeMetadataHydrator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string NormalizeMetadataJson(
        ProjectObjectType objectType,
        string? objectSubtype,
        string? notes,
        string? metadataJson)
    {
        if (!CanHydrate(objectType, objectSubtype, metadataJson))
        {
            return string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson;
        }

        var metadata = ProjectObjectMetadataSerializer.Parse(metadataJson);
        Hydrate(objectType, objectSubtype, notes, metadata);
        return MergeHydratedMetadata(metadataJson, metadata);
    }

    public static void Hydrate(
        ProjectObjectType objectType,
        string? objectSubtype,
        string? notes,
        ProjectObjectMetadataEnvelope metadata)
    {
        if (objectType != ProjectObjectType.Environment)
        {
            return;
        }

        metadata.Environment ??= new ProjectEnvironmentMetadata();
        var environment = metadata.Environment;
        var environmentKind = environment.EnvironmentKind == default && !string.IsNullOrWhiteSpace(objectSubtype)
            ? ProjectNodeKindRegistry.ResolveEnvironmentKind(objectSubtype)
            : environment.EnvironmentKind;

        if (!IsDotNetKind(environmentKind))
        {
            return;
        }

        environment.EnvironmentKind = environmentKind;
        if (string.IsNullOrWhiteSpace(notes))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(environment.WorkingDirectory) &&
            TryExtractWorkingDirectory(notes, out var workingDirectory))
        {
            environment.WorkingDirectory = workingDirectory;
        }

        if (string.IsNullOrWhiteSpace(environment.ProjectPath) &&
            TryExtractProjectPath(notes, out var projectPath))
        {
            environment.ProjectPath = ResolveProjectPath(projectPath, environment.WorkingDirectory);
        }

        if (string.IsNullOrWhiteSpace(environment.LocalhostUrl) &&
            TryExtractLocalhostUrl(notes, out var localhostUrl))
        {
            environment.LocalhostUrl = localhostUrl;
            environment.RuntimeProtocol = localhostUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                ? ProjectRuntimeProtocol.Http
                : ProjectRuntimeProtocol.Https;
        }
    }

    private static bool CanHydrate(ProjectObjectType objectType, string? objectSubtype, string? metadataJson)
    {
        if (objectType != ProjectObjectType.Environment)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(objectSubtype) &&
            IsDotNetKind(ProjectNodeKindRegistry.ResolveEnvironmentKind(objectSubtype)))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return false;
        }

        var metadata = ProjectObjectMetadataSerializer.Parse(metadataJson);
        return metadata.Environment is not null &&
               IsDotNetKind(metadata.Environment.EnvironmentKind);
    }

    private static string MergeHydratedMetadata(string? originalJson, ProjectObjectMetadataEnvelope metadata)
    {
        var hydratedRoot = JsonNode.Parse(ProjectObjectMetadataSerializer.Serialize(metadata)) as JsonObject
                           ?? new JsonObject();

        if (string.IsNullOrWhiteSpace(originalJson) ||
            JsonNode.Parse(originalJson) is not JsonObject originalRoot)
        {
            return hydratedRoot.ToJsonString(JsonOptions);
        }

        foreach (var property in originalRoot)
        {
            if (!hydratedRoot.ContainsKey(property.Key))
            {
                hydratedRoot[property.Key] = property.Value?.DeepClone();
            }
        }

        return hydratedRoot.ToJsonString(JsonOptions);
    }

    private static bool IsDotNetKind(ProjectEnvironmentKind kind)
        => kind is ProjectEnvironmentKind.DotNetRuntime or ProjectEnvironmentKind.DotNetWatch or ProjectEnvironmentKind.DotNetRelease;

    private static bool TryExtractWorkingDirectory(string notes, out string workingDirectory)
    {
        var match = WorkingDirectoryFromUsingRegex().Match(notes);
        if (!match.Success)
        {
            match = WorkingDirectoryLabeledRegex().Match(notes);
        }

        if (!match.Success)
        {
            workingDirectory = string.Empty;
            return false;
        }

        var candidate = CleanPath(match.Groups["path"].Value);
        if (Path.IsPathRooted(candidate))
        {
            workingDirectory = GetFullPathOrOriginal(candidate);
            return true;
        }

        workingDirectory = string.Empty;
        return false;
    }

    private static bool TryExtractProjectPath(string notes, out string projectPath)
    {
        var match = ProjectPathRegex().Match(notes);
        if (!match.Success)
        {
            projectPath = string.Empty;
            return false;
        }

        projectPath = CleanPath(FirstSuccessfulGroup(match, "double", "single", "plain"));
        return !string.IsNullOrWhiteSpace(projectPath);
    }

    private static bool TryExtractLocalhostUrl(string notes, out string localhostUrl)
    {
        var match = LocalhostUrlRegex().Match(notes);
        if (!match.Success)
        {
            localhostUrl = string.Empty;
            return false;
        }

        localhostUrl = match.Value.Trim().TrimEnd('.', ',', ';');
        return true;
    }

    private static string ResolveProjectPath(string projectPath, string workingDirectory)
    {
        if (Path.IsPathRooted(projectPath) ||
            string.IsNullOrWhiteSpace(workingDirectory) ||
            !Path.IsPathRooted(workingDirectory))
        {
            return GetFullPathOrOriginal(projectPath);
        }

        return GetFullPathOrOriginal(Path.Combine(workingDirectory, projectPath));
    }

    private static string FirstSuccessfulGroup(Match match, params string[] groupNames)
    {
        foreach (var groupName in groupNames)
        {
            var value = match.Groups[groupName].Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string CleanPath(string value)
        => value.Trim().Trim('`', '"', (char)39).TrimEnd('.', ',', ';');

    private static string GetFullPathOrOriginal(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (ArgumentException)
        {
            return path;
        }
        catch (NotSupportedException)
        {
            return path;
        }
        catch (PathTooLongException)
        {
            return path;
        }
    }

    [GeneratedRegex("""\bfrom\s+(?<path>[A-Za-z]:\\.+?)\s+using\s+`""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WorkingDirectoryFromUsingRegex();

    [GeneratedRegex("""\b(?:working directory|workingDirectory|repository root|repo root|product root)\s*[:=]\s*(?<path>[A-Za-z]:\\[^\r\n`]+)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WorkingDirectoryLabeledRegex();

    [GeneratedRegex("""--project\s+(?:"(?<double>[^"]+)"|'(?<single>[^']+)'|(?<plain>[^\s`]+))""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProjectPathRegex();

    [GeneratedRegex("""https?://(?:localhost|127\.0\.0\.1|\[::1\])(?::\d+)?[^\s`'")]*""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LocalhostUrlRegex();
}
