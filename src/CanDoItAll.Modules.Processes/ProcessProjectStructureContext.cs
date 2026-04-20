using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessProjectStructureContext
{
    public Guid ProjectId { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string NodeTitle { get; set; } = string.Empty;

    public string? ParentNodeId { get; set; }

    public string ParentNodeTitle { get; set; } = string.Empty;

    public string ResolveTargetNodeId()
        => string.IsNullOrWhiteSpace(ParentNodeId) ? NodeId : ParentNodeId.Trim();

    public string ResolveTargetNodeTitle()
        => string.IsNullOrWhiteSpace(ParentNodeTitle) ? NodeTitle : ParentNodeTitle.Trim();
}

public static class ProcessProjectStructureContextFormatter
{
    private const string ContextPrefix = "Project structure context JSON: ";

    public static string AppendToTriggerReason(
        string? triggerReason,
        ProcessProjectStructureContext? context)
    {
        var segments = new List<string>();
        if (!string.IsNullOrWhiteSpace(triggerReason))
        {
            segments.Add(RemoveSerializedContext(triggerReason));
        }

        if (context is not null)
        {
            segments.Add($"Project structure target: {FormatNodeLabel(context.ResolveTargetNodeTitle(), context.ResolveTargetNodeId())}");
            segments.Add($"{ContextPrefix}{JsonSerializer.Serialize(context)}");
        }

        return string.Join(Environment.NewLine, segments.Where(segment => !string.IsNullOrWhiteSpace(segment)));
    }

    public static string RemoveSerializedContext(string? triggerReason)
    {
        if (string.IsNullOrWhiteSpace(triggerReason))
        {
            return string.Empty;
        }

        var lines = triggerReason
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .Where(line => !line.TrimStart().StartsWith(ContextPrefix, StringComparison.Ordinal))
            .ToList();

        return string.Join(Environment.NewLine, lines).Trim();
    }

    public static bool TryParse(
        string? triggerReason,
        out ProcessProjectStructureContext? context)
    {
        context = null;
        if (string.IsNullOrWhiteSpace(triggerReason))
        {
            return false;
        }

        foreach (var line in triggerReason.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith(ContextPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var payload = trimmed[ContextPrefix.Length..];
            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            try
            {
                context = JsonSerializer.Deserialize<ProcessProjectStructureContext>(payload);
            }
            catch (JsonException)
            {
                context = null;
            }

            return context is not null;
        }

        return false;
    }

    public static string BuildPromptSummary(ProcessProjectStructureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var lines = new List<string>
        {
            $"- Project id: {context.ProjectId:D}",
            $"- Selected process node: {FormatNodeLabel(context.NodeTitle, context.NodeId)}",
            $"- Target work node: {FormatNodeLabel(context.ResolveTargetNodeTitle(), context.ResolveTargetNodeId())}"
        };

        if (!string.IsNullOrWhiteSpace(context.ParentNodeId))
        {
            lines.Add($"- Parent node id: {context.ParentNodeId.Trim()}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatNodeLabel(string? title, string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.IsNullOrWhiteSpace(nodeId) ? "Unknown node" : nodeId.Trim();
        }

        return string.IsNullOrWhiteSpace(nodeId)
            ? title.Trim()
            : $"{title.Trim()} ({nodeId.Trim()})";
    }
}
