using System.Collections.Immutable;
using System.Text;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Models;

public sealed record AgentPlanningReview(AgentPlanningDecisionExpectation Expected, Guid ChatSessionId,
    AgentPlanningOwner Owner, DecisionDataPolicy Policy, AgentPlanningGenerationState State,
    DateTimeOffset ObservedAtUtc, DateTimeOffset? BasisCapturedAtUtc, TimeSpan ChargedWork,
    TimeSpan? RemainingWork, bool HasUnsettledWork, int AutomaticReplans,
    ImmutableArray<AgentPlanningAction> AvailableActions, AgentPlanningPublicationReview? Publication);

public sealed record AgentPlanningPublicationReview {
    public const int MaximumContentUtf8Bytes = 300_000;

    public AgentPlanningPublicationReview(AgentToolApprovalBinding binding, string approvalId, string content) {
        AgentSavedEffectRequestValidation.RequireBinding(binding);
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Encoding.UTF8.GetByteCount(approvalId), 1024);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Encoding.UTF8.GetByteCount(content), MaximumContentUtf8Bytes);
        Binding = binding;
        ApprovalId = approvalId;
        Content = content;
    }

    public AgentToolApprovalBinding Binding { get; }
    public string ApprovalId { get; }
    public string Content { get; }
}
