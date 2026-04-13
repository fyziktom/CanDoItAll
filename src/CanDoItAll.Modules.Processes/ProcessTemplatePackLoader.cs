using System.Collections.ObjectModel;
using System.Reflection;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessTemplatePackLoader
{
    private const string ManifestFileName = "manifest.json";

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
        var manifest = ReadJson<ProcessTemplatePackManifest>(Path.Combine(root, ManifestFileName));

        var frameworkSources = ReadJson<List<ProcessFrameworkSource>>(Path.Combine(root, manifest.FrameworkSourcesPath));
        var roleTemplates = ReadJson<List<ProcessTemplateToolboxRoleSeed>>(Path.Combine(root, manifest.Toolbox.RoleTemplatesPath));
        var stepTemplates = ReadJson<List<ProcessTemplateToolboxStepSeed>>(Path.Combine(root, manifest.Toolbox.StepTemplatesPath));
        var chromeActions = string.IsNullOrWhiteSpace(manifest.Toolbox.ChromeActionsPath)
            ? new ProcessTemplateToolboxChromeCatalog()
            : ReadJson<ProcessTemplateToolboxChromeCatalog>(Path.Combine(root, manifest.Toolbox.ChromeActionsPath));
        var baselineScenarios = ReadJson<List<ProcessTemplateBaselineScenario>>(Path.Combine(root, manifest.SeedCatalog.BaselineScenariosPath));

        var sharedRoles = LoadJsonDirectory<ProcessTemplateRoleResource>(Path.Combine(root, "shared", "roles"), static item => item.Key);
        var sharedArtifacts = LoadJsonDirectory<ProcessTemplateArtifactResource>(Path.Combine(root, "shared", "artifacts"), static item => item.Key);
        var sharedChecklists = LoadJsonDirectory<ProcessTemplateChecklistResource>(Path.Combine(root, "shared", "checklists"), static item => item.Key);
        var sharedValidations = LoadJsonDirectory<ProcessTemplateValidationResource>(Path.Combine(root, "shared", "validations"), static item => item.Key);
        var sharedPrompts = LoadJsonDirectory<ProcessTemplatePromptResource>(Path.Combine(root, "shared", "prompts"), static item => item.Key);

        var processes = new Dictionary<string, ProcessTemplateDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in manifest.Processes.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            var processRoot = Path.GetFullPath(Path.Combine(root, entry.RelativePath));
            var definition = ReadJson<ProcessTemplateDefinition>(Path.Combine(processRoot, "definition.json"));
            definition.RelativePath = entry.RelativePath.Replace("\\", "/");
            definition.DefinitionJsonPath = Path.Combine(processRoot, "definition.json");
            definition.DefinitionMarkdownPath = Path.Combine(processRoot, "definition.md");
            definition.CurrentModuleImportEnvelopePath = Path.Combine(processRoot, "projection", "current-module.import-envelope.json");
            definition.CurrentModuleCompatibilityReportPath = Path.Combine(processRoot, "projection", "current-module.compatibility-report.json");
            definition.CurrentModuleCompatibilityReportMarkdownPath = Path.Combine(processRoot, "projection", "current-module.compatibility-report.md");
            definition.FlowchartPath = Path.Combine(processRoot, "mermaid", "flowchart.mmd");
            definition.SequencePath = Path.Combine(processRoot, "mermaid", "sequence.mmd");
            definition.LocalRoles = LoadJsonDirectory<ProcessTemplateRoleResource>(Path.Combine(processRoot, "roles"), static item => item.Key).Values.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase).ToList();
            definition.LocalArtifacts = LoadJsonDirectory<ProcessTemplateArtifactResource>(Path.Combine(processRoot, "artifacts"), static item => item.Key).Values.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase).ToList();
            definition.LocalChecklists = LoadJsonDirectory<ProcessTemplateChecklistResource>(Path.Combine(processRoot, "checklists"), static item => item.Key).Values.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase).ToList();
            definition.LocalValidations = LoadJsonDirectory<ProcessTemplateValidationResource>(Path.Combine(processRoot, "validations"), static item => item.Key).Values.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase).ToList();
            definition.LocalPrompts = LoadJsonDirectory<ProcessTemplatePromptResource>(Path.Combine(processRoot, "prompts"), static item => item.Key).Values.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase).ToList();

            if (definition.LocalRoleRefs.Count == 0)
            {
                definition.LocalRoleRefs = definition.LocalRoles.Select(item => item.Key).ToList();
            }

            if (definition.LocalArtifactRefs.Count == 0)
            {
                definition.LocalArtifactRefs = definition.LocalArtifacts.Select(item => item.Key).ToList();
            }

            if (definition.LocalChecklistRefs.Count == 0)
            {
                definition.LocalChecklistRefs = definition.LocalChecklists.Select(item => item.Key).ToList();
            }

            if (definition.LocalValidationRefs.Count == 0)
            {
                definition.LocalValidationRefs = definition.LocalValidations.Select(item => item.Key).ToList();
            }

            if (definition.LocalPromptRefs.Count == 0)
            {
                definition.LocalPromptRefs = definition.LocalPrompts.Select(item => item.Key).ToList();
            }

            processes[definition.Key] = definition;
        }

        return new ProcessTemplatePack
        {
            RootPath = root,
            Manifest = manifest,
            FrameworkSources = frameworkSources.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            RoleTemplates = roleTemplates,
            StepTemplates = stepTemplates,
            ChromeActions = chromeActions,
            Processes = new ReadOnlyDictionary<string, ProcessTemplateDefinition>(processes),
            BaselineScenarios = baselineScenarios,
            SharedRoles = new ReadOnlyDictionary<string, ProcessTemplateRoleResource>(sharedRoles),
            SharedArtifacts = new ReadOnlyDictionary<string, ProcessTemplateArtifactResource>(sharedArtifacts),
            SharedChecklists = new ReadOnlyDictionary<string, ProcessTemplateChecklistResource>(sharedChecklists),
            SharedValidations = new ReadOnlyDictionary<string, ProcessTemplateValidationResource>(sharedValidations),
            SharedPrompts = new ReadOnlyDictionary<string, ProcessTemplatePromptResource>(sharedPrompts)
        };
    }

    private static Dictionary<string, TResource> LoadJsonDirectory<TResource>(
        string directoryPath,
        Func<TResource, string> keySelector)
        where TResource : class, new()
    {
        var result = new Dictionary<string, TResource>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(directoryPath))
        {
            return result;
        }

        foreach (var path in Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            var resource = ReadJson<TResource>(path);
            var key = keySelector(resource);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            result[key] = resource;
        }

        return result;
    }

    private static T ReadJson<T>(string path)
        where T : class, new()
    {
        return JsonFileLoader.ReadRequired<T>(path);
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
        var discoveredRoot = AncestorFileLocator.FindContainingDirectory(
            relativeManifestPath,
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
        if (!string.IsNullOrWhiteSpace(discoveredRoot))
        {
            return discoveredRoot;
        }

        throw new InvalidOperationException(
            $"Unable to locate {ProcessTemplatePackOptions.DefaultRelativePackRoot}/{ManifestFileName} from the current execution root. " +
            $"Configure {ProcessTemplatePackOptions.SectionName}:PackRoot when the template pack lives outside the repository default layout.");
    }
}
