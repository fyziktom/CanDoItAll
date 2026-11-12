using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.UI;

public static class ProcessRuntimeDisplay {
    public static IReadOnlyList<ToolFamilySummary> BuildToolFamilySummaries(
        IReadOnlyList<ProcessRuntimeToolUsageProjection> tools) {
        if (tools.Count == 0) {
            return [];
        }

        return tools
            .GroupBy(ResolveToolFamily)
            .Select(group => CreateToolFamilySummary(
                group.Key,
                group
                    .OrderByDescending(tool => tool.CallCount)
                    .ThenBy(tool => tool.ToolName, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .OrderByDescending(summary => summary.CallCount)
            .ThenBy(summary => summary.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string ResolveAgentDisplayName(ProcessRuntimeActiveAgentProjection agent)
        => FirstNonEmpty(agent.AgentName, agent.ExecutorDisplayName, agent.ExecutorId, "Unknown agent");

    public static string ResolveAgentCardSummary(ProcessRuntimeActiveAgentProjection agent)
        => FirstNonEmpty(agent.CurrentActivity, agent.Summary);

    public static ToolFamilySummary CreateToolFamilySummary(
        LiveProcessToolFamily family,
        IReadOnlyList<ProcessRuntimeToolUsageProjection> tools) {
        var metadata = ResolveToolFamilyMetadata(family);
        return new ToolFamilySummary(
            family,
            metadata.Label,
            metadata.Icon,
            metadata.Tone,
            metadata.CssClass,
            tools.Count,
            tools.Sum(tool => tool.CallCount),
            tools.Max(tool => tool.LastUsedAtUtc),
            tools);
    }

    public static LiveProcessToolFamily ResolveToolFamily(ProcessRuntimeToolUsageProjection tool) {
        var normalized = $"{tool.ToolName} {tool.Summary}".ToLowerInvariant();
        if (normalized.Contains("browser", StringComparison.Ordinal) ||
            normalized.Contains("playwright", StringComparison.Ordinal) ||
            normalized.Contains("screenshot", StringComparison.Ordinal) ||
            normalized.Contains("snapshot", StringComparison.Ordinal) ||
            normalized.Contains("console", StringComparison.Ordinal)) {
            return LiveProcessToolFamily.Browser;
        }

        if (normalized.Contains("project_structure", StringComparison.Ordinal) ||
            normalized.Contains("project structure", StringComparison.Ordinal)) {
            return LiveProcessToolFamily.ProjectStructure;
        }

        if (normalized.Contains("dotnet", StringComparison.Ordinal) ||
            normalized.Contains("msbuild", StringComparison.Ordinal) ||
            normalized.Contains("restore", StringComparison.Ordinal) ||
            normalized.Contains("build", StringComparison.Ordinal) ||
            normalized.Contains("test", StringComparison.Ordinal)) {
            return LiveProcessToolFamily.DotNet;
        }

        if (normalized.Contains("workspace", StringComparison.Ordinal) ||
            normalized.Contains("pwsh", StringComparison.Ordinal) ||
            normalized.Contains("shell", StringComparison.Ordinal) ||
            normalized.Contains("file", StringComparison.Ordinal) ||
            normalized.Contains("directory", StringComparison.Ordinal)) {
            return LiveProcessToolFamily.Workspace;
        }

        if (normalized.Contains("agent", StringComparison.Ordinal) ||
            normalized.Contains("model", StringComparison.Ordinal) ||
            normalized.Contains("prompt", StringComparison.Ordinal) ||
            normalized.Contains("completion", StringComparison.Ordinal)) {
            return LiveProcessToolFamily.AgentRuntime;
        }

        if (normalized.Contains("storage", StringComparison.Ordinal) ||
            normalized.Contains("artifact", StringComparison.Ordinal) ||
            normalized.Contains("evidence", StringComparison.Ordinal)) {
            return LiveProcessToolFamily.Storage;
        }

        if (normalized.Contains("process", StringComparison.Ordinal) ||
            normalized.Contains("step", StringComparison.Ordinal) ||
            normalized.Contains("manager", StringComparison.Ordinal)) {
            return LiveProcessToolFamily.ProcessRuntime;
        }

        return LiveProcessToolFamily.Other;
    }

    public static (string Label, string Icon, string Tone, string CssClass) ResolveToolFamilyMetadata(
        LiveProcessToolFamily family)
        => family switch {
            LiveProcessToolFamily.Browser => ("Browser", "travel_explore", "info", "live-processes-tool-card--browser"),
            LiveProcessToolFamily.DotNet => (".NET", "terminal", "success", "live-processes-tool-card--dotnet"),
            LiveProcessToolFamily.ProjectStructure => ("Project structure", "account_tree", "accent", "live-processes-tool-card--structure"),
            LiveProcessToolFamily.Workspace => ("Workspace", "folder_open", "neutral", "live-processes-tool-card--workspace"),
            LiveProcessToolFamily.ProcessRuntime => ("Process runtime", "schema", "warning", "live-processes-tool-card--runtime"),
            LiveProcessToolFamily.AgentRuntime => ("Agent runtime", "smart_toy", "info", "live-processes-tool-card--agent"),
            LiveProcessToolFamily.Storage => ("Artifacts", "inventory_2", "neutral", "live-processes-tool-card--storage"),
            _ => ("Other", "extension", "neutral", "live-processes-tool-card--other")
        };

    public static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    public static bool IsAttentionEventType(string eventType)
        => eventType.Contains("Blocked", StringComparison.OrdinalIgnoreCase) ||
           eventType.Contains("Failed", StringComparison.OrdinalIgnoreCase) ||
           eventType.Contains("Denied", StringComparison.OrdinalIgnoreCase) ||
           eventType.Contains("Rejected", StringComparison.OrdinalIgnoreCase) ||
           eventType.Contains("Escalated", StringComparison.OrdinalIgnoreCase) ||
           eventType.Contains("Incident", StringComparison.OrdinalIgnoreCase);

}
