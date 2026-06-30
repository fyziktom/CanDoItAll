using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CanDoItAll.AgentFramework.Workflows.Templates;

public static class WorkflowTemplatePackOptions
{
    public const string TemplatesRootDirectoryName = "Templates";
    public const string WorkflowsDirectoryName = "Workflows";
    public const string DefaultRelativePackRoot = "Templates/Workflows";
    public const string ManifestFileName = "manifest.yaml";
}

internal static class WorkflowTemplateJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

internal static class WorkflowTemplateYamlReader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static T Read<T>(
        string path,
        WorkflowTemplateFailureKind failureKind,
        WorkflowTemplateContext context)
        where T : class, new()
    {
        try
        {
            using var reader = File.OpenText(path);
            return Deserializer.Deserialize<T>(reader) ?? new T();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or YamlException)
        {
            throw WorkflowTemplateDiagnostics.CreateException(
                failureKind,
                "Workflow template YAML file could not be loaded.",
                context,
                "Fix the YAML file path, permissions, and syntax before loading the workflow template pack.",
                exception);
        }
    }
}

internal static class WorkflowTemplatePackRootResolver
{
    public static string Resolve(string? explicitRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            var normalizedExplicitRoot = Path.GetFullPath(explicitRoot);
            if (File.Exists(Path.Combine(normalizedExplicitRoot, WorkflowTemplatePackOptions.ManifestFileName)))
            {
                return normalizedExplicitRoot;
            }

            if (File.Exists(normalizedExplicitRoot) &&
                string.Equals(
                    Path.GetFileName(normalizedExplicitRoot),
                    WorkflowTemplatePackOptions.ManifestFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetDirectoryName(normalizedExplicitRoot)!;
            }
        }

        var relativeManifestPath = Path.Combine(
            WorkflowTemplatePackOptions.TemplatesRootDirectoryName,
            WorkflowTemplatePackOptions.WorkflowsDirectoryName,
            WorkflowTemplatePackOptions.ManifestFileName);
        var discoveredRoot = AncestorFileLocator.FindContainingDirectory(
            relativeManifestPath,
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
        if (!string.IsNullOrWhiteSpace(discoveredRoot))
        {
            return discoveredRoot;
        }

        throw WorkflowTemplateDiagnostics.CreateException(
            WorkflowTemplateFailureKind.ManifestLoadFailed,
            $"Unable to locate {WorkflowTemplatePackOptions.DefaultRelativePackRoot}/{WorkflowTemplatePackOptions.ManifestFileName} from the current execution root.",
            new WorkflowTemplateContext(
                WorkflowTemplatePackOptions.DefaultRelativePackRoot,
                string.Empty,
                WorkflowTemplatePackOptions.ManifestFileName),
            "Configure a workflow template pack root when the template pack lives outside the repository default layout.");
    }
}
