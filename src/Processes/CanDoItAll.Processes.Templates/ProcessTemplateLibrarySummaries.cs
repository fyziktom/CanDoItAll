using System.Text;
using System.Text.Json;

namespace CanDoItAll.Processes.Templates;

public sealed record ProcessTemplateDefinitionLibrarySummary(
    string SourceJsonRelativePath,
    string SourceJsonHash,
    string CanonicalJson,
    string GeneratedMarkdown,
    string GeneratedMermaid,
    IReadOnlyList<ProcessTemplateDefinitionLibraryStructureNodeSummary> StructureNodes);

public sealed record ProcessTemplateDefinitionLibraryStructureNodeSummary(
    string NodeKey,
    string? ParentNodeKey,
    string Kind,
    string Title,
    string Summary,
    int Depth);

internal static class ProcessTemplateLibrarySummaryBuilder
{
    public static ProcessTemplateDefinitionLibrarySummary Build(
        string definitionRelativePath,
        ProcessTemplateDefinitionDocument definition)
    {
        var sourceJsonRelativePath = Path.Combine(definitionRelativePath, "definition.json")
            .Replace('\\', '/');
        var canonicalJson = JsonSerializer.Serialize(
            definition,
            ProcessTemplateJsonContext.Default.ProcessTemplateDefinitionDocument);
        using var document = JsonDocument.Parse(canonicalJson);
        return new ProcessTemplateDefinitionLibrarySummary(
            sourceJsonRelativePath,
            ProcessTemplateContentHasher.ComputeCanonicalHash(document.RootElement),
            canonicalJson,
            BuildMarkdown(definition),
            BuildMermaid(definition),
            BuildStructure(definition));
    }

    private static string BuildMarkdown(ProcessTemplateDefinitionDocument definition)
    {
        var builder = new StringBuilder();
        var key = NormalizeOptional(definition.Key, "process");
        builder.AppendLine($"# {NormalizeOptional(definition.DisplayName, key)}");
        builder.AppendLine();
        builder.AppendLine($"Generated from canonical JSON process template `{key}`.");
        builder.AppendLine();
        builder.AppendLine(NormalizeOptional(definition.Summary, "No summary provided."));
        builder.AppendLine();
        builder.AppendLine("## Governance");
        builder.AppendLine();
        builder.AppendLine($"- Criticality: {NormalizeOptional(definition.Criticality, "Unspecified")}");
        builder.AppendLine($"- Operating mode: {NormalizeOptional(definition.OperatingMode, "Unspecified")}");
        builder.AppendLine($"- Autonomy: {NormalizeOptional(definition.AutonomyLevel, "Unspecified")}");
        builder.AppendLine();
        builder.AppendLine("## Roles");
        builder.AppendLine();
        if (definition.RoleUsages.Count == 0)
        {
            builder.AppendLine("- No role usages are declared.");
        }
        else
        {
            foreach (var role in definition.RoleUsages)
            {
                builder.AppendLine($"- {NormalizeOptional(role.DisplayName, NormalizeOptional(role.Key, "Role"))}: {NormalizeOptional(role.Purpose, NormalizeOptional(role.Notes, "No role purpose provided."))}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Steps");
        builder.AppendLine();
        if (definition.Steps.Count == 0)
        {
            builder.AppendLine("- No steps are declared.");
        }
        else
        {
            foreach (var step in definition.Steps.OrderBy(item => item.Order).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine($"{step.Order + 1}. {NormalizeOptional(step.Title, NormalizeOptional(step.Key, "Step"))} - {NormalizeOptional(step.OutputContractSummary, NormalizeOptional(step.Notes, "No step output provided."))}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildMermaid(ProcessTemplateDefinitionDocument definition)
    {
        var builder = new StringBuilder();
        var processKey = NormalizeOptional(definition.Key, "process");
        var processNodeId = CreateMermaidNodeId(processKey, fallbackIndex: 0);
        builder.AppendLine("flowchart TD");
        builder.AppendLine($"    {processNodeId}[\"{EscapeMermaidLabel(NormalizeOptional(definition.DisplayName, processKey))}\"]");

        var orderedSteps = definition.Steps
            .OrderBy(step => step.Order)
            .ThenBy(step => step.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        for (var index = 0; index < orderedSteps.Length; index++)
        {
            var step = orderedSteps[index];
            var stepKey = NormalizeOptional(step.Key, $"step-{index + 1}");
            var stepNodeId = CreateMermaidNodeId(stepKey, index + 1);
            builder.AppendLine($"    {stepNodeId}[\"{EscapeMermaidLabel(NormalizeOptional(step.Title, stepKey))}\"]");
            builder.AppendLine($"    {processNodeId} --> {stepNodeId}");

            foreach (var outcome in step.BranchOutcomes)
            {
                var targetStepKey = NormalizeOptional(outcome.RouteTargetStepKey, string.Empty);
                if (string.IsNullOrWhiteSpace(targetStepKey))
                {
                    continue;
                }

                var targetNodeId = CreateMermaidNodeId(targetStepKey, index + 100);
                builder.AppendLine($"    {stepNodeId} -- \"{EscapeMermaidLabel(NormalizeOptional(outcome.Title, outcome.Key))}\" --> {targetNodeId}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static IReadOnlyList<ProcessTemplateDefinitionLibraryStructureNodeSummary> BuildStructure(
        ProcessTemplateDefinitionDocument definition)
    {
        var nodes = new List<ProcessTemplateDefinitionLibraryStructureNodeSummary>();
        var definitionKey = NormalizeOptional(definition.Key, "process");
        var rootKey = $"process:{definitionKey}";
        nodes.Add(new ProcessTemplateDefinitionLibraryStructureNodeSummary(
            rootKey,
            ParentNodeKey: null,
            "Process",
            NormalizeOptional(definition.DisplayName, definitionKey),
            NormalizeOptional(definition.Summary, string.Empty),
            Depth: 0));

        var rolesKey = $"{rootKey}:roles";
        nodes.Add(new ProcessTemplateDefinitionLibraryStructureNodeSummary(
            rolesKey,
            rootKey,
            "Section",
            "Roles",
            $"{definition.RoleUsages.Count} role usage(s)",
            Depth: 1));
        foreach (var role in definition.RoleUsages)
        {
            var roleKey = NormalizeOptional(role.Key, NormalizeOptional(role.RoleResourceKey, "role"));
            nodes.Add(new ProcessTemplateDefinitionLibraryStructureNodeSummary(
                $"{rolesKey}:{roleKey}",
                rolesKey,
                "Role",
                NormalizeOptional(role.DisplayName, roleKey),
                NormalizeOptional(role.Purpose, NormalizeOptional(role.Notes, string.Empty)),
                Depth: 2));
        }

        var stepsKey = $"{rootKey}:steps";
        nodes.Add(new ProcessTemplateDefinitionLibraryStructureNodeSummary(
            stepsKey,
            rootKey,
            "Section",
            "Steps",
            $"{definition.Steps.Count} step(s)",
            Depth: 1));
        foreach (var step in definition.Steps.OrderBy(item => item.Order).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            var stepKey = NormalizeOptional(step.Key, $"step-{step.Order + 1}");
            var stepNodeKey = $"{stepsKey}:{stepKey}";
            nodes.Add(new ProcessTemplateDefinitionLibraryStructureNodeSummary(
                stepNodeKey,
                stepsKey,
                "Step",
                NormalizeOptional(step.Title, stepKey),
                NormalizeOptional(step.Notes, NormalizeOptional(step.OutputContractSummary, string.Empty)),
                Depth: 2));

            foreach (var branch in step.BranchOutcomes)
            {
                var branchKey = NormalizeOptional(branch.Key, "branch");
                nodes.Add(new ProcessTemplateDefinitionLibraryStructureNodeSummary(
                    $"{stepNodeKey}:branch:{branchKey}",
                    stepNodeKey,
                    "Branch",
                    NormalizeOptional(branch.Title, branchKey),
                    NormalizeOptional(branch.Description, branch.RouteTargetKind),
                    Depth: 3));
            }

            foreach (var artifact in step.ArtifactExpectations)
            {
                var artifactKey = NormalizeOptional(artifact.Key, NormalizeOptional(artifact.TemplateKey, "artifact"));
                nodes.Add(new ProcessTemplateDefinitionLibraryStructureNodeSummary(
                    $"{stepNodeKey}:artifact:{artifactKey}",
                    stepNodeKey,
                    "Artifact",
                    NormalizeOptional(artifact.Title, artifactKey),
                    NormalizeOptional(artifact.ValidationRequirementSummary, artifact.ArtifactKind),
                    Depth: 3));
            }
        }

        return nodes;
    }

    private static string CreateMermaidNodeId(string value, int fallbackIndex)
    {
        var normalized = new string(value
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '_')
            .ToArray())
            .Trim('_');
        return string.IsNullOrWhiteSpace(normalized)
            ? $"node_{fallbackIndex}"
            : $"node_{normalized}";
    }

    private static string EscapeMermaidLabel(string value)
        => value.Replace("\"", "'", StringComparison.Ordinal).Replace("[", "(", StringComparison.Ordinal).Replace("]", ")", StringComparison.Ordinal);

    private static string NormalizeOptional(string? value, string defaultValue)
        => string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
}
