using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Templates;

internal static class WorkflowTemplatePackValidator
{
    public static void Validate(
        WorkflowTemplatePack templatePack,
        IWorkflowExecutorCatalog? executorCatalog)
    {
        ValidateInputParameters(templatePack);
        ValidateDescriptorReferences(templatePack, executorCatalog);
        ValidateGraphs(templatePack, executorCatalog);
    }

    private static void ValidateInputParameters(WorkflowTemplatePack templatePack)
    {
        foreach (var template in templatePack.Workflows)
        {
            _ = templatePack.CreateInputParameters(template);
        }
    }

    private static void ValidateDescriptorReferences(
        WorkflowTemplatePack templatePack,
        IWorkflowExecutorCatalog? executorCatalog)
    {
        if (executorCatalog is null)
        {
            return;
        }

        foreach (var template in templatePack.Workflows)
        {
            foreach (var (node, index) in template.Graph.Nodes.Select((node, index) => (node, index)))
            {
                if (node.Executor is null)
                {
                    continue;
                }

                var context = WorkflowTemplatePack
                    .CreateContext(template)
                    .WithNode(node.Id, $"graph.nodes[{index}].executor.id");
                var executorId = WorkflowTemplateDiagnostics.Require(
                    node.Executor.Id,
                    $"node '{node.Id}' executor.id",
                    context,
                    "Set executor.id to a registered workflow executor id.");
                if (!executorCatalog.TryGetExecutor(new WorkflowExecutorId(executorId), out _))
                {
                    throw WorkflowTemplateDiagnostics.CreateException(
                        WorkflowTemplateFailureKind.DescriptorValidationFailed,
                        $"Workflow template references executor '{executorId}' that is not registered.",
                        context.WithExecutor(node.Id, executorId, $"graph.nodes[{index}].executor.id"),
                        "Register a descriptor source for this executor or change the template executor id to a registered descriptor.");
                }

            }
        }
    }

    private static void ValidateGraphs(
        WorkflowTemplatePack templatePack,
        IWorkflowExecutorCatalog? executorCatalog)
    {
        var validator = executorCatalog is null
            ? new WorkflowDefinitionValidator()
            : new WorkflowDefinitionValidator(
                executorCatalog,
                WorkflowDefinitionValidationOptions.RegisteredExecutorsOnly);

        foreach (var template in templatePack.Workflows)
        {
            var component = CreateTemplateValidationComponent(templatePack, template);
            WorkflowDefinition definition;
            try
            {
                definition = templatePack.CreateDefinition(template, component);
            }
            catch (WorkflowTemplatePackException)
            {
                throw;
            }
            catch (InvalidOperationException exception)
            {
                throw WorkflowTemplateDiagnostics.CreateException(
                    WorkflowTemplateFailureKind.GraphMaterializationFailed,
                    "Workflow template failed graph conversion.",
                    WorkflowTemplatePack.CreateContext(template),
                    "Fix graph nodes, edges, ports, executor settings, and runtime policy values.",
                    exception);
            }

            var validation = validator.Validate(definition, [component]);
            if (validation.Succeeded)
            {
                continue;
            }

            var issue = validation.Issues.First();
            throw WorkflowTemplateDiagnostics.CreateException(
                WorkflowTemplateFailureKind.SemanticValidationFailed,
                $"Workflow template failed semantic validation: {FormatValidationIssues(validation.Issues)}",
                CreateIssueContext(template, issue),
                "Fix the referenced graph node, edge, executor, route, or component metadata so the generated workflow definition validates.");
        }
    }

    private static LlmCallComponent CreateTemplateValidationComponent(
        WorkflowTemplatePack templatePack,
        WorkflowTemplateDefinition template)
        => new(
            WorkflowComponentId.New(),
            string.IsNullOrWhiteSpace(template.Name) ? template.Key : template.Name,
            ProviderProfileId: null,
            Model: "template-validation",
            WorkflowModality.Text,
            templatePack.CreateModelSettings(),
            templatePack.CreateComponentInstructions(template),
            templatePack.JsonShape,
            templatePack.JsonShape,
            AgentPermissionsPolicy.Default,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private static WorkflowTemplateContext CreateIssueContext(
        WorkflowTemplateDefinition template,
        WorkflowValidationIssue issue)
    {
        var context = WorkflowTemplatePack.CreateContext(template);
        if (issue.NodeId is { } nodeId)
        {
            var executorId = template.Graph.Nodes
                .FirstOrDefault(node => string.Equals(node.Id, nodeId.Value, StringComparison.OrdinalIgnoreCase))
                ?.Executor
                ?.Id ?? string.Empty;
            return string.IsNullOrWhiteSpace(executorId)
                ? context.WithNode(nodeId.Value, "graph.nodes")
                : context.WithExecutor(nodeId.Value, executorId, "graph.nodes[].executor.id");
        }

        if (issue.EdgeId is { } edgeId)
        {
            return context.WithYamlPath($"graph.edges[{edgeId.Value}]");
        }

        return context;
    }

    private static string FormatValidationIssues(IReadOnlyList<WorkflowValidationIssue> issues)
        => string.Join(
            " ",
            issues.Select(issue =>
            {
                var location = issue.NodeId is { } nodeId
                    ? $"node '{nodeId}'"
                    : issue.EdgeId is { } edgeId
                        ? $"edge '{edgeId}'"
                        : "template";
                return $"{location}: {issue.Message}";
            }));
}
