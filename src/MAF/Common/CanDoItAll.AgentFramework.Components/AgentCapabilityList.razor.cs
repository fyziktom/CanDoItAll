using CanDoItAll.AgentFramework.Models;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.AgentFramework.Components;

public partial class AgentCapabilityList
{
    [Parameter, EditorRequired]
    public IReadOnlyList<CapabilityCatalogItem> Items { get; set; } = [];

    [Parameter, EditorRequired]
    public IReadOnlyCollection<Guid> AttachedCapabilityIds { get; set; } = [];

    [Parameter]
    public bool IsBusy { get; set; }

    [Parameter]
    public bool CanVerify { get; set; } = true;

    [Parameter, EditorRequired]
    public EventCallback<Guid> AssignmentRequested { get; set; }

    [Parameter, EditorRequired]
    public EventCallback<Guid> VerificationRequested { get; set; }

    [Parameter]
    public EventCallback<Guid> DetailsRequested { get; set; }

    [Parameter]
    public string TestIdPrefix { get; set; } = "agent-capability";

    [Parameter]
    public string? ListTestId { get; set; }

    [Parameter]
    public string ColumnTemplateLg { get; set; } = "repeat(2,minmax(16rem,1fr))";

    private string ResolvedListTestId => string.IsNullOrWhiteSpace(ListTestId)
        ? $"{NormalizedTestIdPrefix}-list"
        : ListTestId.Trim();
    private string CardTestId => $"{NormalizedTestIdPrefix}-card";
    private string AssignmentTestId => $"{NormalizedTestIdPrefix}-toggle";
    private string VerificationTestId => $"{NormalizedTestIdPrefix}-verify";
    private string DetailsTestId => $"{NormalizedTestIdPrefix}-details";
    private string NormalizedTestIdPrefix => string.IsNullOrWhiteSpace(TestIdPrefix)
        ? "agent-capability"
        : TestIdPrefix.Trim();

    private static string ResolveKindLabel(CapabilityKind kind)
    {
        return kind switch
        {
            CapabilityKind.McpServer => "MCP server",
            CapabilityKind.AiContext => "AI context",
            _ => kind.ToString()
        };
    }

    private static (string Text, string Tone) ResolveProofBadge(CapabilityProofStatus status)
    {
        return status switch
        {
            CapabilityProofStatus.Verified => ("Verified", "success"),
            CapabilityProofStatus.PendingReview => ("Pending review", "warning"),
            CapabilityProofStatus.Failed => ("Failed", "danger"),
            _ => ("Not run", "neutral")
        };
    }

    private static string ResolveEndpointSummary(CapabilityCatalogItem capability)
    {
        return string.IsNullOrWhiteSpace(capability.EndpointOrPath)
            ? "No endpoint or path configured"
            : capability.EndpointOrPath;
    }
}
