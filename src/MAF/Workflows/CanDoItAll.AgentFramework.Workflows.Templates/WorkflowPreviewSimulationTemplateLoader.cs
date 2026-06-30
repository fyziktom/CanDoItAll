using System.Text.Json;

namespace CanDoItAll.AgentFramework.Workflows.Templates;

public sealed class WorkflowPreviewSimulationTemplateLoader
{
    private const string PreviewSimulationsDirectoryName = "preview-simulations";
    private const string ExecutorsFileName = "executors.json";

    public WorkflowPreviewSimulationTemplateCatalog Load(string? packRoot = null)
    {
        var root = WorkflowTemplatePackLoader.FindPackRoot(packRoot);
        var path = Path.Combine(root, PreviewSimulationsDirectoryName, ExecutorsFileName);
        if (!File.Exists(path))
        {
            return new WorkflowPreviewSimulationTemplateCatalog();
        }

        WorkflowPreviewSimulationTemplateCatalog catalog;
        try
        {
            using var stream = File.OpenRead(path);
            catalog = JsonSerializer.Deserialize<WorkflowPreviewSimulationTemplateCatalog>(
                stream,
                WorkflowTemplateJson.Options) ?? new WorkflowPreviewSimulationTemplateCatalog();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw WorkflowTemplateDiagnostics.CreateException(
                WorkflowTemplateFailureKind.PreviewSimulationInvalid,
                "Workflow preview simulation template file could not be loaded.",
                new WorkflowTemplateContext(path, string.Empty, "preview-simulations/executors.json"),
                "Fix preview-simulations/executors.json path, permissions, and JSON syntax.",
                exception);
        }

        Validate(catalog, path);
        return catalog;
    }

    private static void Validate(
        WorkflowPreviewSimulationTemplateCatalog catalog,
        string path)
    {
        foreach (var (executor, executorTemplates) in catalog.Executors)
        {
            var executorContext = new WorkflowTemplateContext(
                path,
                string.Empty,
                $"executors.{executor}",
                ExecutorId: executor);
            if (string.IsNullOrWhiteSpace(executor))
            {
                throw WorkflowTemplateDiagnostics.CreateException(
                    WorkflowTemplateFailureKind.PreviewSimulationInvalid,
                    "Workflow preview simulation executor key is empty.",
                    executorContext,
                    "Use the workflow executor id as each preview simulation executor key.");
            }

            foreach (var (operation, template) in executorTemplates.Operations)
            {
                var operationContext = executorContext.WithYamlPath($"executors.{executor}.operations.{operation}");
                if (string.IsNullOrWhiteSpace(operation))
                {
                    throw WorkflowTemplateDiagnostics.CreateException(
                        WorkflowTemplateFailureKind.PreviewSimulationInvalid,
                        "Workflow preview simulation operation key is empty.",
                        operationContext,
                        "Use the executor operation name as each preview simulation operation key.");
                }

                if (string.IsNullOrWhiteSpace(template.Description))
                {
                    throw WorkflowTemplateDiagnostics.CreateException(
                        WorkflowTemplateFailureKind.PreviewSimulationInvalid,
                        "Workflow preview simulation template is missing a description.",
                        operationContext.WithYamlPath($"{operationContext.YamlPath}.description"),
                        "Set description so preview UI can explain why the step is being simulated.");
                }

                if (template.OutputTemplate.ValueKind is JsonValueKind.Undefined)
                {
                    throw WorkflowTemplateDiagnostics.CreateException(
                        WorkflowTemplateFailureKind.PreviewSimulationInvalid,
                        "Workflow preview simulation template is missing outputTemplate.",
                        operationContext.WithYamlPath($"{operationContext.YamlPath}.outputTemplate"),
                        "Set outputTemplate to the JSON payload emitted when the preview simulation skips the executor.");
                }
            }
        }
    }
}
