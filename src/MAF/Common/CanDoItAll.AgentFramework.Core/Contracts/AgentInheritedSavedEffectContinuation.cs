using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum AgentInheritedContinuationPurpose { Dispatch, ManagerReview }

public sealed class AgentInheritedSavedEffectContinuation {
    private readonly Func<ExecutionRunRecord, CancellationToken, Task> requireOriginalParent;
    private readonly AgentPlanningDecisionExpectation? reviewExpected;
    private readonly AgentToolApprovalBinding? reviewBinding;
    private readonly Func<CancellationToken, Task>? requireCurrentManager;

    public AgentInheritedSavedEffectContinuation(AgentPlanningInheritedOrigin original,
        Func<ExecutionRunRecord, CancellationToken, Task> requireOriginalParent)
        : this(original, requireOriginalParent, null, null) { }

    private AgentInheritedSavedEffectContinuation(AgentPlanningInheritedOrigin original,
        Func<ExecutionRunRecord, CancellationToken, Task> requireOriginalParent,
        AgentPlanningDecisionExpectation? reviewExpected, AgentToolApprovalBinding? reviewBinding,
        Func<CancellationToken, Task>? requireCurrentManager = null, AgentPlanningReviewIntent? reviewIntent = null) {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(requireOriginalParent);
        _ = original.ResolvePolicy();
        Original = original;
        this.requireOriginalParent = requireOriginalParent;
        this.reviewExpected = reviewExpected;
        this.reviewBinding = reviewBinding;
        this.requireCurrentManager = requireCurrentManager;
        ReviewIntent = reviewIntent;
    }

    public AgentPlanningInheritedOrigin Original { get; }
    internal AgentPlanningReviewIntent? ReviewIntent { get; }
    internal AgentInheritedContinuationPurpose Purpose => reviewExpected is null
        ? AgentInheritedContinuationPurpose.Dispatch : AgentInheritedContinuationPurpose.ManagerReview;

    public static AgentInheritedSavedEffectContinuation ForManagerReview(AgentPlanningInheritedOrigin original,
        AgentPlanningDecisionExpectation expected, AgentToolApprovalBinding binding,
        Func<ExecutionRunRecord, CancellationToken, Task> requireCurrentReview) {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(binding);
        return new(original, requireCurrentReview, expected, binding);
    }

    public static AgentInheritedSavedEffectContinuation ForManagerReview(AgentPlanningInheritedOrigin original,
        AgentPlanningDecisionExpectation expected, AgentToolApprovalBinding binding,
        Func<ExecutionRunRecord, CancellationToken, Task> requireCurrentReview,
        Func<CancellationToken, Task> requireCurrentManager) {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(requireCurrentManager);
        return new(original, requireCurrentReview, expected, binding, requireCurrentManager);
    }

    internal AgentInheritedSavedEffectContinuation BindReviewIntent(AgentPlanningReviewIntent intent) {
        if (Purpose != AgentInheritedContinuationPurpose.ManagerReview || requireCurrentManager is null || ReviewIntent is not null) {
            throw new NotSupportedException("A manager action requires a fresh capability with current manager authority.");
        }
        return new(Original, requireOriginalParent, reviewExpected, reviewBinding, requireCurrentManager, intent);
    }

    internal async Task RequireCurrentManagerAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        await (requireCurrentManager
            ?? throw new NotSupportedException("This continuation cannot observe current manager authority."))(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    internal void RequireSelection(AgentPlanningDecisionExpectation expected, AgentToolApprovalBinding binding) {
        if (reviewExpected is not null && (reviewExpected != expected || reviewBinding != binding)) {
            throw new AgentToolAdmissionException("tool-admission.inherited-review-changed",
                "Manager continuation requires its exact displayed expectation and original proposal.");
        }
    }

    internal void RequireOriginal(ExecutionRunRecord run) {
        if (run.ToolAdmission?.SchemaVersion != AgentToolJournalRecord.InheritedPlanningSchemaVersion ||
            run.ToolAdmission.PlanningDecision?.InheritedOrigin != Original ||
            run.ToolAdmission.Session.Profile != Original.Profile || run.AgentId != Original.AgentId ||
            run.ToolAdmission.Session.Reference.BackgroundSource != Original.Source ||
            reviewExpected is { } selected && (run.Id != selected.ExecutionRunId ||
                run.ToolAdmission.PlanningDecision.DecisionId != selected.DecisionId ||
                run.ToolAdmission.PlanningDecision.CurrentGeneration != selected.Generation)) {
            throw new AgentToolAdmissionException("tool-admission.inherited-continuation-changed",
                "The saved effect no longer has its exact original parent, source, material and grant.");
        }
        _ = AgentPlanningRunMetadata.Read(run);
    }

    internal async Task RequireCurrentAsync(ExecutionRunRecord run, CancellationToken cancellationToken) {
        RequireOriginal(run);
        cancellationToken.ThrowIfCancellationRequested();
        await requireOriginalParent(run, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
