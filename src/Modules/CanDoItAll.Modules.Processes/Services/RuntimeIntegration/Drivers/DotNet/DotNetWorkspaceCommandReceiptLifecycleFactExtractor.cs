using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal sealed class DotNetWorkspaceCommandReceiptLifecycleFactExtractor : IWorkspaceCommandReceiptLifecycleFactExtractor
{
    private static readonly Regex LoopbackUrlPattern = new(
        @"https?://(?:localhost|127\.0\.0\.1|\[::1\]):\d+(?:/[^\s""'<>)]*)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public IReadOnlyList<WorkspaceCommandReceiptLifecycleFact> Extract(
        WorkspaceCommandReceiptLifecycleFactContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!IsRuntimeLifecycleTool(context.ToolName))
        {
            return [];
        }

        var lifecycleFacts = new List<WorkspaceCommandReceiptLifecycleFact>();
        foreach (var startupPath in context.TargetPaths
                     .Where(path => path.EndsWith("startup.json", StringComparison.OrdinalIgnoreCase))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Take(3))
        {
            lifecycleFacts.Add(new WorkspaceCommandReceiptLifecycleFact("startupReceipt", startupPath));
        }

        foreach (var url in ExtractLoopbackUrls(string.Join(' ', context.Stdout, context.Stderr))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Take(3))
        {
            lifecycleFacts.Add(new WorkspaceCommandReceiptLifecycleFact("hostUrl", url));
        }

        return lifecycleFacts;
    }

    private static bool IsRuntimeLifecycleTool(string toolName)
        => string.Equals(toolName, "workspace_dotnet_run", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(toolName, "workspace_dotnet_stop", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> ExtractLoopbackUrls(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return LoopbackUrlPattern
            .Matches(text)
            .Select(match => match.Value.TrimEnd('.', ',', ';'))
            .ToArray();
    }
}

