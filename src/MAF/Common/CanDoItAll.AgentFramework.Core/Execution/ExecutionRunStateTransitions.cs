using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal static class ExecutionRunStateTransitions
{
    public static ExecutionRunRecord CreateContinuationStartRun(
        ExecutionRunRecord run,
        bool approved,
        bool effectiveAutoApprove,
        DateTimeOffset decidedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(run);

        return run with
        {
            Revision = NextRevision(run.Revision),
            UpdatedAtUtc = decidedAtUtc,
            CompletedAtUtc = null,
            PendingApprovals = [],
            AutoApprovePendingToolCalls = effectiveAutoApprove,
            State = ExecutionState.Running,
            Outcome = null,
            ResultSummary = approved
                ? "Resuming execution after approval."
                : "Resuming execution after rejection."
        };
    }

    public static bool PendingApprovalStateMatches(
        ExecutionRunRecord expectedRun,
        ExecutionRunRecord currentRun)
    {
        ArgumentNullException.ThrowIfNull(expectedRun);
        ArgumentNullException.ThrowIfNull(currentRun);

        if (expectedRun.Revision != currentRun.Revision)
        {
            return false;
        }

        if (!string.Equals(expectedRun.RuntimeSessionKey, currentRun.RuntimeSessionKey, StringComparison.Ordinal) ||
            !string.Equals(expectedRun.SerializedSessionStateJson, currentRun.SerializedSessionStateJson, StringComparison.Ordinal) ||
            expectedRun.AutoApprovePendingToolCalls != currentRun.AutoApprovePendingToolCalls)
        {
            return false;
        }

        var expectedApprovals = expectedRun.PendingApprovals
            .OrderBy(item => item.ApprovalId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var currentApprovals = currentRun.PendingApprovals
            .OrderBy(item => item.ApprovalId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return expectedApprovals.SequenceEqual(currentApprovals);
    }

    public static (IReadOnlyList<ExecutionApprovalRecord> RunApprovals, IReadOnlyList<ExecutionApprovalRecord> Pending) SynchronizePendingApprovals(
        IReadOnlyList<ExecutionApprovalRecord> existingRunApprovals,
        ExecutionRunRecord run,
        IReadOnlyList<PendingToolApprovalRecord> pendingApprovals,
        DateTimeOffset requestedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(existingRunApprovals);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(pendingApprovals);

        var approvals = existingRunApprovals.ToList();
        var pending = new List<ExecutionApprovalRecord>();

        foreach (var pendingApproval in pendingApprovals)
        {
            var existingIndex = approvals.FindIndex(item =>
                item.ExecutionRunId == run.Id &&
                string.Equals(item.ApprovalId, pendingApproval.ApprovalId, StringComparison.OrdinalIgnoreCase));

            var record = existingIndex >= 0
                ? approvals[existingIndex] with
                {
                    CallId = pendingApproval.CallId,
                    ToolName = pendingApproval.ToolName,
                    ToolKind = pendingApproval.ToolKind,
                    Details = pendingApproval.Details,
                    ArgumentsJson = pendingApproval.ArgumentsJson,
                    Status = ExecutionApprovalStatus.Pending,
                    DecisionSourceKind = string.Empty,
                    DecisionSourceId = string.Empty,
                    DecisionNotes = string.Empty,
                    DecidedAtUtc = null
                }
                : CreateApprovalRecord(run, pendingApproval, ExecutionApprovalStatus.Pending, requestedAtUtc);

            if (existingIndex >= 0)
            {
                approvals[existingIndex] = record;
            }
            else
            {
                approvals.Add(record);
            }

            pending.Add(record);
        }

        return (approvals, pending);
    }

    public static (IReadOnlyList<ExecutionApprovalRecord> RunApprovals, IReadOnlyList<ExecutionApprovalRecord> Decided) ApplyApprovalDecision(
        IReadOnlyList<ExecutionApprovalRecord> existingRunApprovals,
        ExecutionRunRecord run,
        bool approved,
        DateTimeOffset decidedAtUtc,
        string decisionSourceKind,
        string decisionSourceId)
    {
        ArgumentNullException.ThrowIfNull(existingRunApprovals);
        ArgumentNullException.ThrowIfNull(run);

        var approvals = existingRunApprovals.ToList();
        var decided = new List<ExecutionApprovalRecord>();

        foreach (var pendingApproval in run.PendingApprovals)
        {
            var existingIndex = approvals.FindIndex(item =>
                item.ExecutionRunId == run.Id &&
                string.Equals(item.ApprovalId, pendingApproval.ApprovalId, StringComparison.OrdinalIgnoreCase));

            var record = (existingIndex >= 0
                    ? approvals[existingIndex]
                    : CreateApprovalRecord(run, pendingApproval, ExecutionApprovalStatus.Pending, run.UpdatedAtUtc))
                with
                {
                    CallId = pendingApproval.CallId,
                    ToolName = pendingApproval.ToolName,
                    ToolKind = pendingApproval.ToolKind,
                    Details = pendingApproval.Details,
                    ArgumentsJson = AgentToolInvocationPolicyMetadata.ProtectApprovalArgumentsForAudit(
                        pendingApproval.ToolName,
                        pendingApproval.ArgumentsJson),
                    Status = approved ? ExecutionApprovalStatus.Approved : ExecutionApprovalStatus.Rejected,
                    DecidedAtUtc = decidedAtUtc,
                    DecisionSourceKind = decisionSourceKind,
                    DecisionSourceId = decisionSourceId,
                    DecisionNotes = approved
                        ? "Approved through execution continuation."
                        : "Rejected through execution continuation."
                };

            if (existingIndex >= 0)
            {
                approvals[existingIndex] = record;
            }
            else
            {
                approvals.Add(record);
            }

            decided.Add(record);
        }

        return (approvals, decided);
    }

    public static bool MatchesApprovalStatus(
        IReadOnlyList<ExecutionApprovalRecord> approvals,
        ExecutionRunRecord run,
        ExecutionApprovalStatus approvalStatus)
    {
        ArgumentNullException.ThrowIfNull(approvals);
        ArgumentNullException.ThrowIfNull(run);

        var runApprovals = approvals.Where(item => item.ExecutionRunId == run.Id).ToList();
        if (approvalStatus == ExecutionApprovalStatus.Pending)
        {
            return run.PendingApprovals.Count > 0 || runApprovals.Any(item => item.Status == ExecutionApprovalStatus.Pending);
        }

        return runApprovals.Any(item => item.Status == approvalStatus);
    }

    private static ExecutionApprovalRecord CreateApprovalRecord(
        ExecutionRunRecord run,
        PendingToolApprovalRecord pendingApproval,
        ExecutionApprovalStatus status,
        DateTimeOffset requestedAtUtc)
    {
        return new ExecutionApprovalRecord(
            ApprovalId: pendingApproval.ApprovalId,
            ExecutionRunId: run.Id,
            CallId: pendingApproval.CallId,
            ToolName: pendingApproval.ToolName,
            ToolKind: pendingApproval.ToolKind,
            Details: pendingApproval.Details,
            ArgumentsJson: pendingApproval.ArgumentsJson,
            Status: status,
            RequestedAtUtc: requestedAtUtc,
            DecidedAtUtc: null,
            DecisionSourceKind: string.Empty,
            DecisionSourceId: string.Empty,
            DecisionNotes: string.Empty);
    }

    private static long NextRevision(long revision)
        => revision <= 0 ? 1L : revision + 1L;
}
