namespace CanDoItAll.Modules.Processes;

internal enum ProcessLiveEscalationActionKind
{
    MessageManager,
    DecideApproval,
    RequestRework,
    Resolve
}

internal readonly record struct ProcessLiveEscalationActionDescriptor(
    ProcessLiveEscalationActionKind Kind,
    string Text,
    string Icon,
    ProcessOperatorApprovalStatus? ApprovalStatus = null);

internal static class ProcessLiveEscalationActionPolicy
{
    public static ProcessLiveEscalationActionDescriptor ResolvePrimaryAction(ProcessLiveEscalationCard escalation)
    {
        return ResolvePrimaryAction(
            escalation.Kind,
            escalation.StepRunId,
            escalation.SourceExecutionRunId,
            escalation.SourceApprovalId);
    }

    public static ProcessLiveEscalationActionDescriptor ResolvePrimaryAction(
        ProcessEscalationKind kind,
        Guid? stepRunId,
        string sourceExecutionRunId,
        string sourceApprovalId)
    {
        if (CanDecideApproval(kind, sourceExecutionRunId, sourceApprovalId))
        {
            return new ProcessLiveEscalationActionDescriptor(
                ProcessLiveEscalationActionKind.DecideApproval,
                "Approve",
                "check",
                ProcessOperatorApprovalStatus.Approved);
        }

        if (CanRequestRework(kind, stepRunId))
        {
            return new ProcessLiveEscalationActionDescriptor(
                ProcessLiveEscalationActionKind.RequestRework,
                "Request rework",
                "refresh");
        }

        return new ProcessLiveEscalationActionDescriptor(
            ProcessLiveEscalationActionKind.MessageManager,
            "Message manager",
            "open_in_new");
    }

    public static ProcessLiveEscalationActionDescriptor? ResolveSecondaryAction(ProcessLiveEscalationCard escalation)
    {
        return ResolveSecondaryAction(
            escalation.Kind,
            escalation.StepRunId,
            escalation.SourceExecutionRunId,
            escalation.SourceApprovalId);
    }

    public static ProcessLiveEscalationActionDescriptor? ResolveSecondaryAction(
        ProcessEscalationKind kind,
        Guid? stepRunId,
        string sourceExecutionRunId,
        string sourceApprovalId)
    {
        if (CanDecideApproval(kind, sourceExecutionRunId, sourceApprovalId))
        {
            return new ProcessLiveEscalationActionDescriptor(
                ProcessLiveEscalationActionKind.DecideApproval,
                "Reject",
                "close",
                ProcessOperatorApprovalStatus.Rejected);
        }

        if (CanRequestRework(kind, stepRunId))
        {
            return new ProcessLiveEscalationActionDescriptor(
                ProcessLiveEscalationActionKind.Resolve,
                "Resolve",
                "check");
        }

        return null;
    }

    public static bool TryResolveSourceApproval(
        ProcessLiveEscalationCard escalation,
        out Guid executionRunId,
        out string approvalId)
    {
        approvalId = escalation.SourceApprovalId.Trim();
        return TryResolveSourceApproval(
            escalation.Kind,
            escalation.SourceExecutionRunId,
            approvalId,
            out executionRunId);
    }

    private static bool CanDecideApproval(
        ProcessEscalationKind kind,
        string sourceExecutionRunId,
        string sourceApprovalId)
    {
        return TryResolveSourceApproval(kind, sourceExecutionRunId, sourceApprovalId, out _);
    }

    private static bool TryResolveSourceApproval(
        ProcessEscalationKind kind,
        string sourceExecutionRunId,
        string sourceApprovalId,
        out Guid executionRunId)
    {
        executionRunId = Guid.Empty;
        return kind == ProcessEscalationKind.ApprovalRequired &&
            !string.IsNullOrWhiteSpace(sourceApprovalId) &&
            Guid.TryParse(sourceExecutionRunId, out executionRunId) &&
            executionRunId != Guid.Empty;
    }

    private static bool CanRequestRework(ProcessEscalationKind kind, Guid? stepRunId)
    {
        return stepRunId.HasValue &&
            kind is ProcessEscalationKind.BlockedStep or
                ProcessEscalationKind.FailedStep or
                ProcessEscalationKind.ToolPolicyBlocked or
                ProcessEscalationKind.RetryBudgetExhausted or
                ProcessEscalationKind.OperatorRequestedRework;
    }
}
