using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.Workflows.Templates;

public sealed record WorkflowTemplatePack(
    string RootPath,
    WorkflowTemplatePackManifest Manifest,
    IReadOnlyList<WorkflowTemplateDefinition> Workflows)
{
    public WorkflowValueShape JsonShape => WorkflowTemplateModelMaterializer.CreateJsonShape(Manifest);

    public WorkflowRuntimePolicy RuntimePolicy => WorkflowTemplateModelMaterializer.CreateRuntimePolicy(Manifest);

    public WorkflowGraph CreateGraph(
        WorkflowTemplateDefinition template,
        WorkflowComponentId componentId)
        => WorkflowTemplateGraphMaterializer.CreateGraph(this, template, componentId);

    public WorkflowDefinition CreateDefinition(
        WorkflowTemplateDefinition template,
        LlmCallComponent component)
        => WorkflowTemplateGraphMaterializer.CreateDefinition(this, template, component);

    public IReadOnlyList<WorkflowInputParameterDescriptor> CreateInputParameters(WorkflowTemplateDefinition template)
        => WorkflowTemplateInputParameterMaterializer.CreateInputParameters(template);

    public WorkflowModelSettings CreateModelSettings()
        => WorkflowTemplateModelMaterializer.CreateModelSettings(Manifest);

    public string CreateComponentInstructions(WorkflowTemplateDefinition template)
        => WorkflowTemplateModelMaterializer.CreateComponentInstructions(Manifest, template);

    internal static WorkflowTemplateContext CreateContext(WorkflowTemplateDefinition template)
        => new(
            string.IsNullOrWhiteSpace(template.SourcePath) ? template.Key : template.SourcePath,
            template.Key,
            string.IsNullOrWhiteSpace(template.SourcePath) ? template.Key : $"{template.SourcePath}#{template.Key}");
}

public sealed class WorkflowTemplatePackLoader
{
    private readonly string? configuredPackRoot;
    private readonly IWorkflowExecutorCatalog? executorCatalog;
    private readonly Lazy<WorkflowTemplatePack> pack;

    public WorkflowTemplatePackLoader(string? packRoot = null)
        : this(packRoot, executorCatalog: null)
    {
    }

    public WorkflowTemplatePackLoader(IWorkflowExecutorCatalog executorCatalog)
        : this(packRoot: null, executorCatalog)
    {
    }

    public WorkflowTemplatePackLoader(
        string? packRoot,
        IWorkflowExecutorCatalog? executorCatalog)
    {
        configuredPackRoot = packRoot;
        this.executorCatalog = executorCatalog;
        pack = new Lazy<WorkflowTemplatePack>(LoadCore, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public WorkflowTemplatePack Load() => pack.Value;

    public static string FindPackRoot(string? packRoot = null)
        => WorkflowTemplatePackRootResolver.Resolve(packRoot);

    private WorkflowTemplatePack LoadCore()
    {
        var root = WorkflowTemplatePackRootResolver.Resolve(configuredPackRoot);
        var manifestPath = Path.Combine(root, WorkflowTemplatePackOptions.ManifestFileName);
        var manifest = WorkflowTemplateYamlReader.Read<WorkflowTemplatePackManifest>(
            manifestPath,
            WorkflowTemplateFailureKind.ManifestLoadFailed,
            new WorkflowTemplateContext(manifestPath, string.Empty, WorkflowTemplatePackOptions.ManifestFileName));
        var workflows = new List<WorkflowTemplateDefinition>();

        foreach (var (file, index) in manifest.WorkflowFiles.Select((file, index) => (file, index)))
        {
            var referenceContext = new WorkflowTemplateContext(
                manifestPath,
                string.Empty,
                $"workflowFiles[{index}].relativePath");
            var relativePath = WorkflowTemplateDiagnostics.Require(
                file.RelativePath,
                "workflow file relative path",
                referenceContext,
                "Set workflowFiles[].relativePath to a YAML file under the workflow template pack root.");
            var workflowFilePath = Path.GetFullPath(Path.Combine(root, relativePath));
            var workflowFile = WorkflowTemplateYamlReader.Read<WorkflowTemplateFile>(
                workflowFilePath,
                WorkflowTemplateFailureKind.WorkflowLoadFailed,
                new WorkflowTemplateContext(workflowFilePath, string.Empty, "workflows"));
            foreach (var workflow in workflowFile.Workflows)
            {
                workflow.SourcePath = workflowFilePath;
                workflows.Add(workflow);
            }
        }

        ThrowForDuplicateKeys(root, workflows);

        var templatePack = new WorkflowTemplatePack(root, manifest, workflows);
        WorkflowTemplatePackValidator.Validate(templatePack, executorCatalog);
        return templatePack;
    }

    private static void ThrowForDuplicateKeys(
        string root,
        IReadOnlyList<WorkflowTemplateDefinition> workflows)
    {
        var duplicateKeys = workflows
            .GroupBy(workflow => workflow.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (duplicateKeys.Length == 0)
        {
            return;
        }

        throw WorkflowTemplateDiagnostics.CreateException(
            WorkflowTemplateFailureKind.DuplicateWorkflowKey,
            $"Workflow template pack contains duplicate workflow key(s): {string.Join(", ", duplicateKeys)}.",
            new WorkflowTemplateContext(root, string.Join(", ", duplicateKeys), "workflows[].key"),
            "Use a unique workflow key for each template across the pack.");
    }
}

public static class WorkflowTemplateServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflowTemplateServices(this IServiceCollection services)
    {
        services.TryAddScoped(serviceProvider => new WorkflowTemplatePackLoader(
            serviceProvider.GetRequiredService<IWorkflowExecutorCatalog>()));
        services.TryAddSingleton<WorkflowPreviewSimulationTemplateLoader>();
        return services;
    }
}
