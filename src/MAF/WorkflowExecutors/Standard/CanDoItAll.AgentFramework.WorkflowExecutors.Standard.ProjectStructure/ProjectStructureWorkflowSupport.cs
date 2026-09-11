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
    private static string NormalizeAssetKind(string value)
        => string.IsNullOrWhiteSpace(value) ? "md" : value.Trim().TrimStart('.').ToLowerInvariant();

    private static string SanitizeFileName(string value)
    {
        string displayName = string.IsNullOrWhiteSpace(value) ? "asset" : value.Trim();
        return PortablePhysicalFileNamePolicy.Encode(displayName).PhysicalName;
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
