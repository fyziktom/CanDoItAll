using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace CanDoItAll.Processes.Templates;

public sealed class ProcessTemplatePackLoader
{
    private const string ManifestFileName = "manifest.json";
    private const string DefinitionFileName = "definition.json";

    private readonly string? configuredPackRoot;
    private readonly Lazy<ProcessTemplatePack> pack;

    public ProcessTemplatePackLoader(string? packRoot = null)
    {
        configuredPackRoot = packRoot;
        pack = new Lazy<ProcessTemplatePack>(LoadCore, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public ProcessTemplatePack Load() => pack.Value;

    public static string FindPackRoot(string? packRoot = null) => ResolvePackRoot(packRoot);

    private ProcessTemplatePack LoadCore()
    {
        var root = ResolvePackRoot(configuredPackRoot);
        var manifestPath = Path.Combine(root, ManifestFileName);
        var manifest = ReadJson(manifestPath, ProcessTemplateJsonContext.Default.ProcessTemplatePackManifest);
        var definitions = new List<ProcessTemplateDefinitionSummary>(manifest.Processes.Count);

        foreach (var entry in manifest.Processes.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = Require(entry.RelativePath, "process relative path", manifestPath);
            var definitionPath = Path.GetFullPath(Path.Combine(root, relativePath, DefinitionFileName));
            var definition = ReadJson(definitionPath, ProcessTemplateJsonContext.Default.ProcessTemplateDefinitionDocument);
            var key = Require(definition.Key, "definition key", definitionPath);
            if (!string.Equals(key, Require(entry.Key, "process key", manifestPath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Process template '{definitionPath}' key '{key}' does not match manifest key '{entry.Key}'.");
            }

            definitions.Add(new ProcessTemplateDefinitionSummary(
                key,
                relativePath,
                Require(definition.DisplayName, "definition display name", definitionPath),
                Require(definition.Summary, "definition summary", definitionPath),
                NormalizeOptional(definition.Criticality, "Unspecified"),
                NormalizeOptional(definition.OperatingMode, "Unspecified"),
                NormalizeOptional(definition.AutonomyLevel, "Unspecified"),
                File.GetLastWriteTimeUtc(definitionPath),
                new ProcessTemplateDefinitionAuthoringDefaults(
                    NormalizeOptional(definition.ValueStatement, string.Empty),
                    NormalizeOptional(definition.CustomerName, string.Empty),
                    NormalizeOptional(definition.OwnerName, string.Empty),
                    NormalizeOptional(definition.InterfaceContractSummary, string.Empty),
                    NormalizeOptional(definition.ManagerOverrideSummary, string.Empty),
                    NormalizeOptional(definition.GovernanceNotes, string.Empty),
                    NormalizeOptional(definition.ChangeSummary, string.Empty),
                    NormalizeOptional(definition.GovernancePolicySummary, string.Empty),
                    NormalizeOptional(definition.ConstitutionRuleSummary, string.Empty),
                    NormalizeOptional(definition.OperatingModeSummary, string.Empty),
                    NormalizeOptional(definition.SimulationReadinessSummary, string.Empty),
                    definition.Steps.Count,
                    definition.RoleUsages.Count(role => role.IsRequired),
                    definition.Steps.Sum(step => step.ArtifactExpectations.Count(artifact => artifact.IsRequired)))));
        }

        return new ProcessTemplatePack(root, manifest, definitions);
    }

    private static T ReadJson<T>(
        string path,
        JsonTypeInfo<T> jsonTypeInfo)
        where T : class
    {
        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize(stream, jsonTypeInfo)
                   ?? throw new InvalidOperationException($"Process template JSON file '{path}' was empty.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidOperationException(
                $"Process template JSON file '{path}' could not be loaded: {exception.Message}",
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
            ProcessTemplatePackOptions.TemplatesRootDirectoryName,
            ProcessTemplatePackOptions.ProcessesDirectoryName,
            ManifestFileName);
        var discoveredRoot = FindContainingDirectory(
            relativeManifestPath,
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
        if (!string.IsNullOrWhiteSpace(discoveredRoot))
        {
            return discoveredRoot;
        }

        throw new InvalidOperationException(
            $"Unable to locate {ProcessTemplatePackOptions.DefaultRelativePackRoot}/{ManifestFileName} from the current execution root. " +
            "Configure a process template pack root when the template pack lives outside the repository default layout.");
    }

    private static string? FindContainingDirectory(string relativeFilePath, params string?[] startPaths)
    {
        foreach (var startPath in startPaths
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Select(path => Path.GetFullPath(path!))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var current = new DirectoryInfo(startPath);
            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, relativeFilePath);
                if (File.Exists(candidate))
                {
                    return Path.GetDirectoryName(candidate);
                }

                current = current.Parent;
            }
        }

        return null;
    }

    private static string Require(string? value, string description, string context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Process template {description} is missing in '{context}'.");
        }

        return value.Trim();
    }

    private static string NormalizeOptional(string? value, string defaultValue)
        => string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();

}

public static class ProcessTemplatePackOptions
{
    public const string TemplatesRootDirectoryName = "Templates";
    public const string ProcessesDirectoryName = "Processes";
    public static readonly string DefaultRelativePackRoot = Path.Combine(TemplatesRootDirectoryName, ProcessesDirectoryName);
}

public sealed record ProcessTemplatePack(
    string RootPath,
    ProcessTemplatePackManifest Manifest,
    IReadOnlyList<ProcessTemplateDefinitionSummary> Definitions);

public sealed record ProcessTemplateDefinitionSummary(
    string Key,
    string RelativePath,
    string DisplayName,
    string Summary,
    string Criticality,
    string OperatingMode,
    string AutonomyLevel,
    DateTimeOffset UpdatedAtUtc,
    ProcessTemplateDefinitionAuthoringDefaults AuthoringDefaults);

public sealed record ProcessTemplateDefinitionAuthoringDefaults(
    string ValueStatement,
    string CustomerName,
    string OwnerName,
    string InterfaceContractSummary,
    string ManagerOverrideSummary,
    string GovernanceNotes,
    string ChangeSummary,
    string GovernancePolicySummary,
    string ConstitutionRuleSummary,
    string OperatingModeSummary,
    string SimulationReadinessSummary,
    int StepCount,
    int RequiredRoleCount,
    int RequiredArtifactExpectationCount);

public sealed class ProcessTemplatePackManifest
{
    public string PackKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public DateTimeOffset GeneratedAtUtc { get; set; }

    public List<ProcessTemplateManifestProcessEntry> Processes { get; set; } = [];
}

public sealed class ProcessTemplateManifestProcessEntry
{
    public string Key { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;
}

public sealed class ProcessTemplateDefinitionDocument
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Criticality { get; set; } = string.Empty;

    public string OperatingMode { get; set; } = string.Empty;

    public string AutonomyLevel { get; set; } = string.Empty;

    public string ValueStatement { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string OwnerName { get; set; } = string.Empty;

    public string InterfaceContractSummary { get; set; } = string.Empty;

    public string ManagerOverrideSummary { get; set; } = string.Empty;

    public string GovernanceNotes { get; set; } = string.Empty;

    public string ChangeSummary { get; set; } = string.Empty;

    public string GovernancePolicySummary { get; set; } = string.Empty;

    public string ConstitutionRuleSummary { get; set; } = string.Empty;

    public string OperatingModeSummary { get; set; } = string.Empty;

    public string SimulationReadinessSummary { get; set; } = string.Empty;

    public List<ProcessTemplateDefinitionRoleUsageDocument> RoleUsages { get; set; } = [];

    public List<ProcessTemplateDefinitionStepDocument> Steps { get; set; } = [];
}

public sealed class ProcessTemplateDefinitionRoleUsageDocument
{
    public bool IsRequired { get; set; }
}

public sealed class ProcessTemplateDefinitionStepDocument
{
    public List<ProcessTemplateDefinitionArtifactExpectationDocument> ArtifactExpectations { get; set; } = [];
}

public sealed class ProcessTemplateDefinitionArtifactExpectationDocument
{
    public bool IsRequired { get; set; }
}
