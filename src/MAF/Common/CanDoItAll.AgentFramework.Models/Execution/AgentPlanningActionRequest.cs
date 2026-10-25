using System.Text;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public enum AgentPlanningAction { Approve = 1, Refresh, Cancel }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentPlanningActionRequest {
    public const int MaximumUtf8Bytes = 8 * 1024;

    public AgentPlanningActionRequest(AgentPlanningAction action, Guid chatSessionId,
        AgentPlanningDecisionExpectation expected, AgentToolApprovalBinding? binding = null, string? approvalId = null) {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentOutOfRangeException.ThrowIfEqual(chatSessionId, Guid.Empty);
        if (!Enum.IsDefined(action)) {
            throw new ArgumentOutOfRangeException(nameof(action));
        }
        if (action == AgentPlanningAction.Approve) {
            AgentSavedEffectRequestValidation.RequireBinding(binding);
            ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(Encoding.UTF8.GetByteCount(approvalId), 1024);
        } else if (binding is not null || approvalId is not null) {
            throw new ArgumentException("Refresh and Cancel cannot carry a publication approval.");
        }
        Action = action;
        ChatSessionId = chatSessionId;
        Expected = expected;
        Binding = binding;
        ApprovalId = approvalId;
    }

    public AgentPlanningAction Action { get; }
    public Guid ChatSessionId { get; }
    public AgentPlanningDecisionExpectation Expected { get; }
    public AgentToolApprovalBinding? Binding { get; }
    public string? ApprovalId { get; }
}
