using System.Text.Json;

namespace CanDoItAll.Processes.Templates;

public enum ProcessTemplateCanvasToolboxActionKind
{
    Step,
    BranchRouter,
    RoleBinding,
    ArtifactExpectation,
    SubprocessBoundary
}

public sealed record ProcessTemplateDefinitionCanvasAuthoringDefaults(
    IReadOnlyList<ProcessTemplateDefinitionCanvasStepSummary> Steps,
    IReadOnlyList<ProcessTemplateDefinitionCanvasToolboxActionSummary> ToolboxActions);

public sealed record ProcessTemplateDefinitionCanvasStepSummary(
    string Key,
    string Title,
    string Subtitle,
    string StepKind,
    int Order,
    string Notes,
    string DecisionRoleKey,
    string SubprocessProcessKey,
    string SubprocessDefinitionSnapshotName,
    double CanvasX,
    double CanvasY,
    double BranchCanvasX,
    double BranchCanvasY,
    IReadOnlyList<ProcessTemplateDefinitionCanvasDependencySummary> Dependencies,
    IReadOnlyList<ProcessTemplateDefinitionCanvasBranchOutcomeSummary> BranchOutcomes,
    IReadOnlyList<ProcessTemplateDefinitionCanvasArtifactExpectationSummary> ArtifactExpectations);

public sealed record ProcessTemplateDefinitionCanvasDependencySummary(
    string DependsOnStepKey,
    string DependsOnBranchOutcomeKey);

public sealed record ProcessTemplateDefinitionCanvasBranchOutcomeSummary(
    string Key,
    string Title,
    string Description);

public sealed record ProcessTemplateDefinitionCanvasArtifactExpectationSummary(
    string Key,
    string Title,
    string ArtifactKind,
    bool IsRequired);

public sealed record ProcessTemplateDefinitionCanvasToolboxActionSummary(
    string ActionId,
    ProcessTemplateCanvasToolboxActionKind Kind,
    string Label,
    string Summary,
    string TemplateStepKey,
    string TemplateStepKind,
    string TemplateTitle);

internal static class ProcessTemplateCanvasSummaryBuilder
{
    private static readonly string StepTemplatesRelativePath = Path.Combine("toolbox", "step-templates.json");

    public static ProcessTemplateDefinitionCanvasAuthoringDefaults Build(
        string root,
        ProcessTemplateDefinitionDocument definition)
        => new(
            definition.Steps
                .Select(CreateStepSummary)
                .OrderBy(step => step.Order)
                .ThenBy(step => step.Key, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            LoadToolboxActions(root));

    private static ProcessTemplateDefinitionCanvasStepSummary CreateStepSummary(
        ProcessTemplateDefinitionStepDocument step)
        => new(
            NormalizeOptional(step.Key, "step"),
            NormalizeOptional(step.Title, NormalizeOptional(step.Key, "Step")),
            NormalizeOptional(step.Subtitle, string.Empty),
            NormalizeOptional(step.StepKind, "Work"),
            step.Order,
            NormalizeOptional(step.Notes, string.Empty),
            NormalizeOptional(step.DecisionRoleKey, string.Empty),
            NormalizeOptional(step.SubprocessProcessKey, string.Empty),
            NormalizeOptional(step.SubprocessDefinitionSnapshotName, string.Empty),
            step.CanvasX,
            step.CanvasY,
            step.BranchCanvasX,
            step.BranchCanvasY,
            CreateDependencies(step),
            step.BranchOutcomes
                .Select(outcome => new ProcessTemplateDefinitionCanvasBranchOutcomeSummary(
                    NormalizeOptional(outcome.Key, "outcome"),
                    NormalizeOptional(outcome.Title, NormalizeOptional(outcome.Key, "Outcome")),
                    NormalizeOptional(outcome.Description, string.Empty)))
                .ToArray(),
            step.ArtifactExpectations
                .Select((artifact, index) => new ProcessTemplateDefinitionCanvasArtifactExpectationSummary(
                    NormalizeOptional(artifact.Key, $"{NormalizeOptional(step.Key, "step")}-artifact-{index + 1}"),
                    NormalizeOptional(artifact.Title, NormalizeOptional(artifact.TemplateKey, $"Artifact {index + 1}")),
                    NormalizeOptional(artifact.ArtifactKind, "Artifact"),
                    artifact.IsRequired))
                .ToArray());

    private static IReadOnlyList<ProcessTemplateDefinitionCanvasDependencySummary> CreateDependencies(
        ProcessTemplateDefinitionStepDocument step)
    {
        var dependencies = step.Dependencies
            .Select(dependency => new ProcessTemplateDefinitionCanvasDependencySummary(
                NormalizeOptional(dependency.DependsOnStepKey, string.Empty),
                NormalizeOptional(dependency.DependsOnBranchOutcomeKey, string.Empty)))
            .Where(dependency => !string.IsNullOrWhiteSpace(dependency.DependsOnStepKey))
            .ToArray();
        if (dependencies.Length > 0)
        {
            return dependencies;
        }

        return string.IsNullOrWhiteSpace(step.DependsOnStepKey)
            ? []
            :
            [
                new ProcessTemplateDefinitionCanvasDependencySummary(
                    NormalizeOptional(step.DependsOnStepKey, string.Empty),
                    NormalizeOptional(step.DependsOnBranchOutcomeKey, string.Empty))
            ];
    }

    private static IReadOnlyList<ProcessTemplateDefinitionCanvasToolboxActionSummary> LoadToolboxActions(
        string root)
    {
        var actions = new List<ProcessTemplateDefinitionCanvasToolboxActionSummary>();
        var path = Path.Combine(root, StepTemplatesRelativePath);
        if (File.Exists(path))
        {
            try
            {
                using var stream = File.OpenRead(path);
                var documents = JsonSerializer.Deserialize(
                    stream,
                    ProcessTemplateJsonContext.Default.ProcessTemplateStepTemplateActionDocumentArray) ?? [];
                foreach (var document in documents)
                {
                    var template = document.Template;
                    var stepKind = NormalizeOptional(template.StepKind, "Work");
                    actions.Add(new ProcessTemplateDefinitionCanvasToolboxActionSummary(
                        NormalizeOptional(document.ActionId, $"process-step.{NormalizeOptional(template.Key, "step")}"),
                        ResolveToolboxKind(stepKind),
                        NormalizeOptional(document.Label, NormalizeOptional(template.Title, "Step")),
                        NormalizeOptional(document.Summary, NormalizeOptional(template.Notes, string.Empty)),
                        NormalizeOptional(template.Key, "step"),
                        stepKind,
                        NormalizeOptional(template.Title, NormalizeOptional(document.Label, "Step"))));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                throw new InvalidOperationException(
                    $"Process step toolbox JSON file '{path}' could not be loaded: {exception.Message}",
                    exception);
            }
        }

        actions.Add(new ProcessTemplateDefinitionCanvasToolboxActionSummary(
            "process-canvas.add-role-binding",
            ProcessTemplateCanvasToolboxActionKind.RoleBinding,
            "Role binding",
            "Connect the selected step to an available role responsibility.",
            TemplateStepKey: string.Empty,
            TemplateStepKind: string.Empty,
            TemplateTitle: string.Empty));
        actions.Add(new ProcessTemplateDefinitionCanvasToolboxActionSummary(
            "process-canvas.add-artifact-expectation",
            ProcessTemplateCanvasToolboxActionKind.ArtifactExpectation,
            "Artifact expectation",
            "Attach an artifact expectation to the selected step.",
            TemplateStepKey: string.Empty,
            TemplateStepKind: string.Empty,
            TemplateTitle: string.Empty));

        return actions;
    }

    private static ProcessTemplateCanvasToolboxActionKind ResolveToolboxKind(string stepKind)
        => stepKind.Trim().ToLowerInvariant() switch
        {
            "decision" => ProcessTemplateCanvasToolboxActionKind.BranchRouter,
            "subprocess" => ProcessTemplateCanvasToolboxActionKind.SubprocessBoundary,
            _ => ProcessTemplateCanvasToolboxActionKind.Step
        };

    private static string NormalizeOptional(string? value, string defaultValue)
        => string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
}
