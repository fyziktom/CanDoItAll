using System.Globalization;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.WorkflowAuthoring.UI;

public sealed class WorkflowRouteDraft {
    public const string InvalidIndexMessage = "Fan-out target index must be an integer zero or greater.";
    public WorkflowRouteKind Kind { get; set; } = WorkflowRouteKind.Always;
    public string Label { get; set; } = string.Empty;
    public string JsonPath { get; set; } = "$.status";
    public WorkflowRouteOperator Operator { get; set; } = WorkflowRouteOperator.Equals;
    public WorkflowRouteValueKind ValueKind { get; set; } = WorkflowRouteValueKind.String;
    public string ExpectedValue { get; set; } = "approved";
    public bool CaseSensitive { get; set; }
    public string FanOutIndexText { get; set; } = string.Empty;
    public int? FanOutTargetIndex {
        get => int.TryParse(FanOutIndexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ? index : null;
        set => FanOutIndexText = value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }
    public bool HasInvalidFanOutIndex => Kind == WorkflowRouteKind.FanOutSelector &&
        !string.IsNullOrWhiteSpace(FanOutIndexText) && FanOutTargetIndex is not >= 0;
}
