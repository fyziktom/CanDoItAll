using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Templates;
using CanDoItAll.AgentFramework.WorkflowAuthoring.UI;

namespace CanDoItAll.Modules.AgentFramework.Pages;

internal static class WorkflowPreviewPreparation {
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly Lazy<WorkflowPreviewSimulationTemplateCatalog> SimulationTemplateCatalog = new(() => new WorkflowPreviewSimulationTemplateLoader().Load());

    public static WorkflowPreviewRequirements Analyze(
        WorkflowDefinition definition,
        IReadOnlyList<WorkflowExecutorDescriptor> executors)
    {
        var requirements = definition.Graph.Nodes
            .Where(node => node.Settings.ExecutorId == WorkflowExecutorIds.ProjectStructure)
            .Select(CreateProjectRequirement)
            .Where(requirement => requirement is not null)
            .Select(requirement => requirement!)
            .ToArray();

        var simulationRequirements = CreateSimulationRequirements(definition, executors, requirements);
        return requirements.Length == 0 && simulationRequirements.Count == 0
            ? WorkflowPreviewRequirements.Empty
            : new WorkflowPreviewRequirements(requirements, simulationRequirements);
    }

    private static WorkflowPreviewProjectRequirement? CreateProjectRequirement(WorkflowNode node)
    {
        WorkflowProjectStructureExecutorSettings settings;
        try
        {
            settings = string.IsNullOrWhiteSpace(node.Settings.ExecutorSettingsJson)
                ? new WorkflowProjectStructureExecutorSettings()
                : JsonSerializer.Deserialize<WorkflowProjectStructureExecutorSettings>(node.Settings.ExecutorSettingsJson, JsonOptions)
                    ?? throw new JsonException("Project Structure settings must be an object.");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"Project Structure configuration is invalid for node '{node.Id}'.");
        }

        return new WorkflowPreviewProjectRequirement(
            node.Id,
            node.Name,
            settings.Operation,
            settings.Operation is WorkflowProjectStructureOperation.CreateAsset or WorkflowProjectStructureOperation.CreateTaskNodes);
    }

    private static IReadOnlyList<WorkflowPreviewSimulationRequirement> CreateSimulationRequirements(
        WorkflowDefinition definition,
        IReadOnlyList<WorkflowExecutorDescriptor> executors,
        IReadOnlyList<WorkflowPreviewProjectRequirement> projectRequirements)
    {
        var executorsById = executors
            .GroupBy(executor => executor.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var projectRequirementsByNodeId = projectRequirements.ToDictionary(requirement => requirement.NodeId);
        var requirements = new List<WorkflowPreviewSimulationRequirement>();

        foreach (var node in definition.Graph.Nodes)
        {
            if (node.Settings.ExecutorId is not { } executorId)
            {
                continue;
            }

            if (projectRequirementsByNodeId.TryGetValue(node.Id, out var projectRequirement) &&
                projectRequirement.IsWriteOperation &&
                TryCreateConfiguredSimulationRequirement(node, executorId, projectRequirement.Operation.ToString(), out var configuredRequirement))
            {
                requirements.Add(configuredRequirement);
                continue;
            }

            if (executorsById.TryGetValue(executorId, out var descriptor) &&
                descriptor.Simulation.SupportsPreviewSimulation &&
                !string.IsNullOrWhiteSpace(descriptor.Simulation.OutputTemplateJson))
            {
                requirements.Add(new WorkflowPreviewSimulationRequirement(
                    node.Id,
                    node.Name,
                    executorId,
                    descriptor.Simulation.Description,
                    descriptor.Simulation.OutputTemplateJson));
            }
        }

        return requirements;
    }

    private static bool TryCreateConfiguredSimulationRequirement(
        WorkflowNode node,
        WorkflowExecutorId executorId,
        string operation,
        out WorkflowPreviewSimulationRequirement requirement)
    {
        requirement = null!;
        if (!TryGetConfiguredTemplate(executorId, operation, out var template))
        {
            return false;
        }

        requirement = new WorkflowPreviewSimulationRequirement(
            node.Id,
            node.Name,
            executorId,
            template.Description,
            template.OutputTemplate.GetRawText());
        return true;
    }

    private static bool TryGetConfiguredTemplate(
        WorkflowExecutorId executorId,
        string operation,
        out WorkflowPreviewSimulationTemplate template)
    {
        template = null!;
        if (!SimulationTemplateCatalog.Value.Executors.TryGetValue(executorId.Value, out var executorTemplates) ||
            !executorTemplates.Operations.TryGetValue(operation, out var resolvedTemplate) ||
            resolvedTemplate.OutputTemplate.ValueKind == JsonValueKind.Undefined)
        {
            return false;
        }

        template = resolvedTemplate;
        return true;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

}
