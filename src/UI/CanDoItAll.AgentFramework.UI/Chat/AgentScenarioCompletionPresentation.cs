using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.UI.Chat;

public sealed record AgentScenarioCompletionPresentation(string Tone, string Message) {
    public static AgentScenarioCompletionPresentation From(ExecutionRunRecord? run, AgentEffectContinuation? continuation,
        bool hasPendingApprovals, string? response) {
        if (continuation is not null || run?.ToolAdmission?.ToolContinuationRequirement is
                AgentToolContinuationRequirement.EffectReconciliationRequired or AgentToolContinuationRequirement.ProviderInputRequired) {
            return new("warning", "Receipt reconciliation or explicit continuation is still required. Receipt reconciliation does not produce a new assistant response.");
        }
        if (hasPendingApprovals) {
            return new("warning", "Review the run's pending approvals before continuing.");
        }
        return run?.State == ExecutionState.Completed && !string.IsNullOrWhiteSpace(response)
            ? new("success", "The run completed and its response evidence is available.")
            : new("info", "The latest scenario state was refreshed. No completed assistant response is confirmed by this view.");
    }
}
