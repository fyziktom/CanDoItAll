using System.Text.Json;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureWorkflowInputSettingsNormalizer
{
    public static ProjectStructureWorkflowInputSettings Normalize(ProjectStructureWorkflowInputSettings? inputSettings)
    {
        var source = inputSettings ?? ProjectStructureWorkflowInputSettings.Default();
        if (!source.IncludeProject)
        {
            throw new ProjectStructureAgentException(
                400,
                "WorkflowInputProjectRequired",
                "Workflow nodes must include project details in the run input.");
        }

        if (!source.IncludeParentNode || !source.IncludeParentNodeDetails)
        {
            throw new ProjectStructureAgentException(
                400,
                "WorkflowInputParentRequired",
                "Workflow nodes must include parent node details in the run input.");
        }

        var manualInputJson = string.IsNullOrWhiteSpace(source.ManualInputJson)
            ? "{}"
            : source.ManualInputJson.Trim();
        try
        {
            using var _ = JsonDocument.Parse(manualInputJson);
        }
        catch (JsonException exception)
        {
            throw new ProjectStructureAgentException(
                400,
                "WorkflowManualInputInvalid",
                $"Manual workflow input JSON is invalid: {exception.Message}.");
        }

        return new ProjectStructureWorkflowInputSettings
        {
            IncludeProject = true,
            IncludeParentNode = true,
            IncludeParentNodeDetails = true,
            IncludeParentSubtree = source.IncludeParentSubtree,
            IncludeAssets = source.IncludeAssets,
            SelectedNodeIds = NormalizeNodeIds(source.SelectedNodeIds),
            AdditionalSources = NormalizeSources(source.AdditionalSources),
            ManualInputJson = manualInputJson
        };
    }

    public static IReadOnlyList<string> NormalizeNodeIds(IReadOnlyList<string>? nodeIds)
        => nodeIds?
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Select(nodeId => nodeId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList()
            ?? [];

    private static IReadOnlyList<ProjectStructureWorkflowInputSource> NormalizeSources(
        IReadOnlyList<ProjectStructureWorkflowInputSource>? sources)
    {
        if (sources is null || sources.Count == 0)
        {
            return [];
        }

        var normalized = new List<ProjectStructureWorkflowInputSource>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources.Where(item => item.IsEnabled))
        {
            var key = source.Key?.Trim() ?? string.Empty;
            var value = source.Value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                throw new ProjectStructureAgentException(
                    400,
                    "WorkflowInputSourceInvalid",
                    "Enabled workflow input sources require both key and value.");
            }

            if (!seen.Add($"{source.Kind}:{key}"))
            {
                throw new ProjectStructureAgentException(
                    400,
                    "WorkflowInputSourceDuplicate",
                    $"Workflow input source '{key}' is duplicated for kind '{source.Kind}'.");
            }

            normalized.Add(source with
            {
                Key = key,
                Label = source.Label?.Trim() ?? string.Empty,
                Value = value
            });
        }

        return normalized;
    }
}
