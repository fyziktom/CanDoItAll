using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal static class AgentPendingApprovalCancellation {
    internal const string Phase = "approval-cancellation";
    internal const string Summary = "Execution cancelled while awaiting approval. Undispatched proposals were rejected without contacting the provider.";

    internal static void RequireSupported(ExecutionRunRecord run) {
        if (!string.IsNullOrWhiteSpace(run.ProcessRunId) ||
            run.ToolAdmission is not { Support: AgentToolAdmissionSupport.Recoverable, Segments.Length: > 0 } journal ||
            journal.Session.Reference.BackgroundSource is not null) {
            throw new InvalidOperationException("Cancel this execution through its owning operation; direct approval cancellation is unavailable.");
        }
    }

    internal static ExecutionRunDetail Apply(ExecutionRunDetail current, ExecutionRunRecord authorizedRun, DateTimeOffset cancelledAtUtc) {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(authorizedRun);
        var run = current.Run;
        RequireSupported(run);
        if (run.Id != authorizedRun.Id || run.AgentId != authorizedRun.AgentId || run.ChatSessionId != authorizedRun.ChatSessionId ||
            run.MetadataJson != authorizedRun.MetadataJson || run.ToolAdmission?.Session != authorizedRun.ToolAdmission?.Session) {
            throw new InvalidOperationException("The execution authority changed before cancellation. Reload the thread.");
        }
        if (run.Outcome == RunOutcome.Cancelled) {
            return current;
        }
        if (run.State != ExecutionState.WaitingOnTool || run.CompletedAtUtc.HasValue || run.PendingApprovals.Count == 0 ||
            !ExecutionRunStateTransitions.PendingApprovalStateMatches(authorizedRun, run)) {
            throw new InvalidOperationException("This execution is no longer waiting on the selected approvals. Reload the thread.");
        }
        var journal = run.ToolAdmission!;
        var decisions = run.PendingApprovals.Select(item => new PendingToolApprovalDecision(item.ApprovalId, false) {
            ToolAdmission = item.ToolAdmission
        }).ToArray();
        var approvals = ExecutionRunStateTransitions.ApplyApprovalDecision(current.Approvals, run, decisions, cancelledAtUtc,
            Phase, run.Id.ToString("D")).RunApprovals;
        var rejectedIds = decisions.Select(item => item.ApprovalId).ToHashSet(StringComparer.Ordinal);
        var cancelledJournal = AgentToolJournalTransitions.RejectUndispatchedOnCancellation(journal) with { Revision = journal.Revision + 1 };
        AgentToolJournalTransitions.ValidatePersistence(journal, cancelledJournal);
        var cancelledRun = run with {
            State = ExecutionState.Failed, Outcome = RunOutcome.Cancelled, ResultSummary = Summary,
            UpdatedAtUtc = cancelledAtUtc, CompletedAtUtc = cancelledAtUtc, Revision = run.Revision + 1,
            RuntimeSessionKey = string.Empty, SerializedSessionStateJson = null, PendingApprovals = [], ToolAdmission = cancelledJournal
        };
        var log = new ExecutionLogEntry(Guid.NewGuid(), run.AgentId, run.ChatSessionId, cancelledAtUtc,
            ExecutionState.Failed, Phase, Summary) { ExecutionRunId = run.Id };
        return current with {
            Run = cancelledRun,
            ChatSession = current.ChatSession is { } session && session.LatestExecutionRunId == run.Id
                ? session with { UpdatedAtUtc = cancelledAtUtc, Compatibility = null }
                : current.ChatSession,
            Approvals = approvals.Select(item => rejectedIds.Contains(item.ApprovalId) ? item with { DecisionNotes = Summary } : item).ToArray(),
            ExecutionLog = [log, .. current.ExecutionLog]
        };
    }
}
