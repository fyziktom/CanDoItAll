using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal static class AgentSavedEffectCancellation {
    internal static ExecutionRunDetail Apply(ExecutionRunDetail current, DateTimeOffset now,
        AgentPlanningReviewIntent? intent = null) {
        intent?.RequirePending(current);
        var journal = current.Run.ToolAdmission!;
        var decisions = current.Run.PendingApprovals.Select(item => new PendingToolApprovalDecision(item.ApprovalId, false) {
            ToolAdmission = item.ToolAdmission
        }).ToImmutableArray();
        var approvals = ExecutionRunStateTransitions.ApplyApprovalDecision(current.Approvals, current.Run, decisions,
            now, intent is null ? AgentPendingApprovalCancellation.Phase : AgentPlanningReviewIntent.SourceKind,
            intent?.Digest.Value ?? current.Run.Id.ToString("D")).RunApprovals;
        var changed = AgentToolJournalTransitions.RejectUndispatchedOnCancellation(journal);
        changed = changed with { Revision = checked(changed.Revision + 1) };
        AgentToolJournalTransitions.ValidatePersistence(journal, changed);
        var log = new ExecutionLogEntry(Guid.NewGuid(), current.Run.AgentId, current.Run.ChatSessionId, now,
            ExecutionState.Failed, AgentPendingApprovalCancellation.Phase, AgentPendingApprovalCancellation.Summary) {
            ExecutionRunId = current.Run.Id
        };
        return current with {
            Run = current.Run with {
                ToolAdmission = changed, State = ExecutionState.Failed, Outcome = RunOutcome.Cancelled,
                PendingApprovals = [], RuntimeSessionKey = string.Empty, SerializedSessionStateJson = null,
                ResultSummary = AgentPendingApprovalCancellation.Summary, CompletedAtUtc = now,
                Revision = checked(current.Run.Revision + 1), UpdatedAtUtc = now
            },
            Approvals = approvals,
            ChatSession = current.ChatSession is { } chat ? chat with { UpdatedAtUtc = now, Compatibility = null } : null,
            ExecutionLog = [log, .. current.ExecutionLog]
        };
    }
}
