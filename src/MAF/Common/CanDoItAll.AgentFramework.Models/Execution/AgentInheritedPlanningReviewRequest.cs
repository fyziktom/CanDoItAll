using System.Text;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public enum AgentPlanningReviewChoice { Approve = 1, Reject }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentInheritedPlanningReviewRequest {
    public const int MaximumUtf8Bytes = 12 * 1024;

    public AgentInheritedPlanningReviewRequest(Guid actionId, AgentPlanningReviewChoice choice,
        AgentPlanningDecisionExpectation expected, AgentToolApprovalBinding binding, string approvalId) {
        ArgumentOutOfRangeException.ThrowIfEqual(actionId, Guid.Empty);
        if (!Enum.IsDefined(choice)) {
            throw new ArgumentOutOfRangeException(nameof(choice));
        }
        ArgumentNullException.ThrowIfNull(expected);
        AgentSavedEffectRequestValidation.RequireBinding(binding);
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Encoding.UTF8.GetByteCount(approvalId), 1024);
        ActionId = actionId;
        Choice = choice;
        Expected = expected;
        Binding = binding;
        ApprovalId = approvalId;
    }

    public Guid ActionId { get; }
    public AgentPlanningReviewChoice Choice { get; }
    public AgentPlanningDecisionExpectation Expected { get; }
    public AgentToolApprovalBinding Binding { get; }
    public string ApprovalId { get; }
}
