using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

/// <summary>
/// SB15: direct tests for per-proposal approval decision validation
/// (<see cref="AgentApprovalDecisionMismatchException"/>) and the per-proposal application of
/// decisions onto pending approvals (<see cref="ExecutionRunStateTransitions.ApplyApprovalDecision"/>).
/// Instantiates the extracted owners directly rather than driving the whole continuation pipeline.
/// </summary>
public sealed class AgentApprovalDecisionValidationTests
{
    private static readonly PendingToolApprovalRecord ApprovalOne = new(
        "approval-1",
        "call-1",
        "workspace_write_file",
        "function",
        string.Empty,
        """{"path":"artifacts/one.md"}""");

    private static readonly PendingToolApprovalRecord ApprovalTwo = new(
        "approval-2",
        "call-2",
        "workspace_delete_file",
        "function",
        string.Empty,
        """{"path":"artifacts/two.md"}""");

    [Fact]
    public void ValidateExactCoverage_accepts_a_decision_set_that_matches_exactly()
    {
        var decisions = new[]
        {
            new PendingToolApprovalDecision(ApprovalOne.ApprovalId, true),
            new PendingToolApprovalDecision(ApprovalTwo.ApprovalId, false)
        };

        AgentApprovalDecisionMismatchException.ValidateExactCoverage(
            decisions,
            [ApprovalOne, ApprovalTwo]);
    }

    [Fact]
    public void ValidateExactCoverage_rejects_a_partial_set_missing_a_pending_approval()
    {
        var decisions = new[] { new PendingToolApprovalDecision(ApprovalOne.ApprovalId, true) };

        var exception = Assert.Throws<AgentApprovalDecisionMismatchException>(() =>
            AgentApprovalDecisionMismatchException.ValidateExactCoverage(
                decisions,
                [ApprovalOne, ApprovalTwo]));

        Assert.Contains(ApprovalTwo.ApprovalId, exception.MissingApprovalIds);
        Assert.Empty(exception.UnknownApprovalIds);
        Assert.Empty(exception.DuplicateApprovalIds);
        Assert.Contains(ApprovalTwo.ApprovalId, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateExactCoverage_rejects_a_decision_referencing_an_unknown_approval_id()
    {
        var decisions = new[]
        {
            new PendingToolApprovalDecision(ApprovalOne.ApprovalId, true),
            new PendingToolApprovalDecision("approval-does-not-exist", true)
        };

        var exception = Assert.Throws<AgentApprovalDecisionMismatchException>(() =>
            AgentApprovalDecisionMismatchException.ValidateExactCoverage(
                decisions,
                [ApprovalOne]));

        Assert.Contains("approval-does-not-exist", exception.UnknownApprovalIds);
    }

    [Fact]
    public void ValidateExactCoverage_rejects_a_duplicate_decision_for_the_same_approval_id()
    {
        var decisions = new[]
        {
            new PendingToolApprovalDecision(ApprovalOne.ApprovalId, true),
            new PendingToolApprovalDecision(ApprovalOne.ApprovalId, false)
        };

        var exception = Assert.Throws<AgentApprovalDecisionMismatchException>(() =>
            AgentApprovalDecisionMismatchException.ValidateExactCoverage(
                decisions,
                [ApprovalOne]));

        Assert.Contains(ApprovalOne.ApprovalId, exception.DuplicateApprovalIds);
    }

    [Fact]
    public void ValidateExactCoverage_rejects_an_empty_decision_list()
    {
        Assert.Throws<ArgumentException>(() =>
            AgentApprovalDecisionMismatchException.ValidateExactCoverage(
                [],
                [ApprovalOne]));
    }

    [Fact]
    public void ApplyApprovalDecision_applies_one_approved_and_one_rejected_decision_independently()
    {
        var run = CreateRun([ApprovalOne, ApprovalTwo]);
        var decisions = new[]
        {
            new PendingToolApprovalDecision(ApprovalOne.ApprovalId, true),
            new PendingToolApprovalDecision(ApprovalTwo.ApprovalId, false)
        };

        var result = ExecutionRunStateTransitions.ApplyApprovalDecision(
            [],
            run,
            decisions,
            DateTimeOffset.UtcNow,
            "chat-session",
            "session-1");

        var approvedRecord = Assert.Single(result.Decided, item => item.ApprovalId == ApprovalOne.ApprovalId);
        var rejectedRecord = Assert.Single(result.Decided, item => item.ApprovalId == ApprovalTwo.ApprovalId);
        Assert.Equal(ExecutionApprovalStatus.Approved, approvedRecord.Status);
        Assert.Equal(ExecutionApprovalStatus.Rejected, rejectedRecord.Status);
        Assert.Equal(2, result.Decided.Count);
        Assert.Equal(2, result.RunApprovals.Count);
    }

    [Fact]
    public void ApplyApprovalDecision_throws_when_a_pending_approval_has_no_matching_decision()
    {
        // Defensive backstop: callers must validate exact coverage first (see
        // AgentApprovalDecisionMismatchException.ValidateExactCoverage); this proves the backstop
        // itself fails closed rather than silently skipping an undecided approval.
        var run = CreateRun([ApprovalOne, ApprovalTwo]);
        var decisions = new[] { new PendingToolApprovalDecision(ApprovalOne.ApprovalId, true) };

        Assert.Throws<InvalidOperationException>(() =>
            ExecutionRunStateTransitions.ApplyApprovalDecision(
                [],
                run,
                decisions,
                DateTimeOffset.UtcNow,
                "chat-session",
                "session-1"));
    }

    [Fact]
    public void CreateContinuationStartRun_summary_reflects_all_approved_versus_mixed_or_rejected()
    {
        var run = CreateRun([ApprovalOne, ApprovalTwo]);

        var allApprovedRun = ExecutionRunStateTransitions.CreateContinuationStartRun(
            run,
            allApproved: true,
            effectiveAutoApprove: false,
            DateTimeOffset.UtcNow);
        var mixedRun = ExecutionRunStateTransitions.CreateContinuationStartRun(
            run,
            allApproved: false,
            effectiveAutoApprove: false,
            DateTimeOffset.UtcNow);

        Assert.Contains("approval", allApprovedRun.ResultSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rejected", allApprovedRun.ResultSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rejected", mixedRun.ResultSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(allApprovedRun.PendingApprovals);
        Assert.Equal(ExecutionState.Running, allApprovedRun.State);
    }

    private static ExecutionRunRecord CreateRun(IReadOnlyList<PendingToolApprovalRecord> pendingApprovals)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Approval decision test run",
            "chat-session",
            Guid.NewGuid().ToString("N"),
            string.Empty,
            string.Empty,
            "test",
            "interactive",
            "{}",
            "Update workspace files",
            string.Empty,
            "test-provider",
            "test-model",
            ExecutionState.WaitingOnTool,
            null,
            now,
            now,
            now,
            null,
            "runtime-session",
            "{}",
            pendingApprovals);
    }
}
