using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure;

public sealed partial class ProjectStructureWorkflowExecutor
{
    private static ProjectStructureRuntimeAgentContext BuildAgentContext(WorkflowNodeInput input)
    {
        var fallback = new ProjectStructureRuntimeAgentContext(
            "workflow-executor",
            "Workflow executor",
            Environment.MachineName,
            string.Empty,
            string.Empty,
            Guid.NewGuid().ToString("N"));

        return string.IsNullOrWhiteSpace(ReadRunContextString(input, "agentId"))
            ? fallback
            : new ProjectStructureRuntimeAgentContext(
                ReadRunContextString(input, "agentId"),
                ReadRunContextString(input, "agentName", fallback.AgentName),
                ReadRunContextString(input, "machineName", fallback.MachineName),
                ReadRunContextString(input, "repositoryRoot", fallback.RepositoryRoot),
                ReadRunContextString(input, "branchName", fallback.BranchName),
                ReadRunContextString(input, "sessionId", fallback.SessionId));
    }

    private static string ReadRunContextString(
        WorkflowNodeInput input,
        string propertyName,
        string fallback = "")
        => TryResolveInputJsonString(input, $"$.runContext.{propertyName}", out var value) &&
           !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;

    private static string NormalizeAssetKind(string value)
        => string.IsNullOrWhiteSpace(value) ? "md" : value.Trim().TrimStart('.').ToLowerInvariant();

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var sanitized = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray()).Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "asset" : sanitized;
    }

    private static string Require(string value, string name)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Project-structure executor setting '{name}' is required.")
            : value.Trim();

    private sealed record WorkflowTaskNodeSource(
        string Title,
        string Summary,
        string Owner,
        DateTimeOffset? DueUtc,
        string Urgency,
        bool RequiresResponse,
        bool Asap,
        string SourceEmailId,
        IReadOnlyList<string> Evidence);
}
