using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessLiveEscalationActionPolicyTests
{
    [Fact]
    public void Blocked_step_escalation_requests_rework_instead_of_approval()
    {
        var stepRunId = Guid.NewGuid();

        var primary = ProcessLiveEscalationActionPolicy.ResolvePrimaryAction(
            ProcessEscalationKind.BlockedStep,
            stepRunId,
            sourceExecutionRunId: string.Empty,
            sourceApprovalId: string.Empty);
        var secondary = ProcessLiveEscalationActionPolicy.ResolveSecondaryAction(
            ProcessEscalationKind.BlockedStep,
            stepRunId,
            sourceExecutionRunId: string.Empty,
            sourceApprovalId: string.Empty);

        Assert.Equal(ProcessLiveEscalationActionKind.RequestRework, primary.Kind);
        Assert.Equal("Request rework", primary.Text);
        Assert.Equal(ProcessLiveEscalationActionKind.Resolve, secondary?.Kind);
    }

    [Fact]
    public void Approval_required_with_source_approval_uses_direct_approval_actions()
    {
        var executionRunId = Guid.NewGuid();

        var primary = ProcessLiveEscalationActionPolicy.ResolvePrimaryAction(
            ProcessEscalationKind.ApprovalRequired,
            stepRunId: Guid.NewGuid(),
            sourceExecutionRunId: executionRunId.ToString("D"),
            sourceApprovalId: "approval-123");
        var secondary = ProcessLiveEscalationActionPolicy.ResolveSecondaryAction(
            ProcessEscalationKind.ApprovalRequired,
            stepRunId: Guid.NewGuid(),
            sourceExecutionRunId: executionRunId.ToString("D"),
            sourceApprovalId: "approval-123");

        Assert.Equal(ProcessLiveEscalationActionKind.DecideApproval, primary.Kind);
        Assert.Equal(ProcessOperatorApprovalStatus.Approved, primary.ApprovalStatus);
        Assert.Equal(ProcessLiveEscalationActionKind.DecideApproval, secondary?.Kind);
        Assert.Equal(ProcessOperatorApprovalStatus.Rejected, secondary?.ApprovalStatus);
    }

    [Fact]
    public void Approval_required_without_source_approval_does_not_fake_a_decision()
    {
        var primary = ProcessLiveEscalationActionPolicy.ResolvePrimaryAction(
            ProcessEscalationKind.ApprovalRequired,
            stepRunId: Guid.NewGuid(),
            sourceExecutionRunId: string.Empty,
            sourceApprovalId: string.Empty);
        var secondary = ProcessLiveEscalationActionPolicy.ResolveSecondaryAction(
            ProcessEscalationKind.ApprovalRequired,
            stepRunId: Guid.NewGuid(),
            sourceExecutionRunId: string.Empty,
            sourceApprovalId: string.Empty);

        Assert.Equal(ProcessLiveEscalationActionKind.MessageManager, primary.Kind);
        Assert.Null(secondary);
    }
}
