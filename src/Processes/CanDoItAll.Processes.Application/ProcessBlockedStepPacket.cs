using System.Globalization;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

internal enum ProcessBlockedStepPacketKind
{
    ExpiredClaim,
    ConcreteDiagnostic,
    RuntimeReceiptOnly,
    MissingDiagnostics
}

internal sealed record ProcessBlockedStepPacket(
    ProcessBlockedStepPacketKind Kind,
    string ProblemSummary,
    string RequiredOperatorDecision,
    string RecommendedInstruction);

internal static class ProcessBlockedStepPacketBuilder
{
    public static ProcessBlockedStepPacket Create(
        string stepKey,
        ProcessRuntimeStepState step,
        ProcessRuntimeStepAssignment? assignment,
        StrategyResultReceipt? receipt,
        DispatchClaimState? expiredClaim,
        StepExecutionDiagnostic? diagnostic)
    {
        var roleDisplayName = FirstNonEmpty(assignment?.RoleDisplayName, "unassigned role");
        var executorDisplayName = FirstNonEmpty(assignment?.ExecutorDisplayName, "unassigned executor");

        if (expiredClaim is not null)
        {
            return CreateExpiredClaimPacket(stepKey, step, roleDisplayName, executorDisplayName, expiredClaim);
        }

        if (diagnostic is not null)
        {
            return CreateDiagnosticPacket(stepKey, step, roleDisplayName, executorDisplayName, receipt, diagnostic);
        }

        return receipt is null
            ? CreateMissingDiagnosticsPacket(stepKey, step, roleDisplayName, executorDisplayName)
            : CreateRuntimeReceiptPacket(stepKey, step, roleDisplayName, executorDisplayName, receipt);
    }

    private static ProcessBlockedStepPacket CreateExpiredClaimPacket(
        string stepKey,
        ProcessRuntimeStepState step,
        string roleDisplayName,
        string executorDisplayName,
        DispatchClaimState expiredClaim)
    {
        var expiredAt = expiredClaim.ExpiresAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        return new ProcessBlockedStepPacket(
            ProcessBlockedStepPacketKind.ExpiredClaim,
            $"{stepKey} is still {step.Status}, but its dispatch lease expired at {expiredAt}. The runtime cannot accept a late result for the expired claim. Current executor: {executorDisplayName}.",
            $"Retry {stepKey} by expiring the stale dispatch claim and letting the process manager dispatch {roleDisplayName} again. Add an operator note when the next attempt needs extra context.",
            $"Manager-approved retry for expired dispatch claim on step '{stepKey}'. Preserve any managed artifacts already written by {executorDisplayName}, verify they satisfy the output contract, produce the required evidence for role '{roleDisplayName}', and continue the process. Step status before retry: {step.Status}.");
    }

    private static ProcessBlockedStepPacket CreateDiagnosticPacket(
        string stepKey,
        ProcessRuntimeStepState step,
        string roleDisplayName,
        string executorDisplayName,
        StrategyResultReceipt? receipt,
        StepExecutionDiagnostic diagnostic)
    {
        var outcomeText = receipt is null
            ? "The runtime has no stored strategy outcome for the current blocker."
            : $"The last strategy outcome was {receipt.Outcome} and the runtime applied {receipt.AppliedStepStatus}.";
        var attemptText = step.AttemptNumber <= 0
            ? "before a dispatch attempt was recorded"
            : $"on attempt {step.AttemptNumber.ToString(CultureInfo.InvariantCulture)}";
        var diagnosticSummary = ProcessRuntimeOperatorActionDiagnostics.BuildExecutionSummary(diagnostic);
        var branchInstruction = IsRepairBranch(diagnostic.BranchOutcomeKey) &&
                                string.Equals(diagnostic.Status, nameof(ProcessRuntimeStepStatus.Blocked), StringComparison.OrdinalIgnoreCase)
            ? $" If the previous finding is repairable and evidence is complete, return a completed process-step outcome with branchOutcomeKey '{diagnostic.BranchOutcomeKey}' instead of Blocked."
            : string.Empty;
        var priorActions = ProcessRuntimeOperatorActionDiagnostics.BuildPriorNextActions(diagnostic);
        var failedToolInstruction = ProcessRuntimeOperatorActionDiagnostics.BuildFailedToolInstruction(diagnostic);

        return new ProcessBlockedStepPacket(
            ProcessBlockedStepPacketKind.ConcreteDiagnostic,
            $"{stepKey} is {step.Status} {attemptText}. {outcomeText} {diagnosticSummary} This is the actionable upstream step for role {roleDisplayName}, currently assigned to {executorDisplayName}.",
            $"Approve rework for {stepKey} only with instructions that address the persisted AgentFramework diagnostic.{branchInstruction} Current executor: {executorDisplayName}.",
            $"Manager-approved rework for step '{stepKey}'. Resolve the persisted diagnostic, preserve accepted upstream artifacts, produce the required evidence for role '{roleDisplayName}', and continue the process. Previous executor: {executorDisplayName}. Step status before rework: {step.Status}.{branchInstruction}{priorActions}{failedToolInstruction}");
    }

    private static ProcessBlockedStepPacket CreateRuntimeReceiptPacket(
        string stepKey,
        ProcessRuntimeStepState step,
        string roleDisplayName,
        string executorDisplayName,
        StrategyResultReceipt receipt)
    {
        return new ProcessBlockedStepPacket(
            ProcessBlockedStepPacketKind.RuntimeReceiptOnly,
            $"{stepKey} is {step.Status}. The runtime stored strategy outcome {receipt.Outcome} and applied {receipt.AppliedStepStatus}, but no exact AgentFramework result summary was found for this step. This is a diagnostics gap, not evidence that a blind retry is safe. Current executor: {executorDisplayName}.",
            $"Do not approve a blind retry for {stepKey}. Inspect execution runs by process run id and exact step id, review runtime receipt diagnostics, then approve rework only with an operator note naming the concrete missing evidence, tool access, or child-process state to fix.",
            $"Manager-guided rework for step '{stepKey}' requires a diagnostic note before redispatch. Use the runtime receipt outcome {receipt.Outcome}/{receipt.AppliedStepStatus}, preserve accepted upstream artifacts, and require {roleDisplayName} to cite fresh execution or managed-artifact evidence before completing.");
    }

    private static ProcessBlockedStepPacket CreateMissingDiagnosticsPacket(
        string stepKey,
        ProcessRuntimeStepState step,
        string roleDisplayName,
        string executorDisplayName)
    {
        return new ProcessBlockedStepPacket(
            ProcessBlockedStepPacketKind.MissingDiagnostics,
            $"{stepKey} is {step.Status}, but neither a runtime strategy receipt nor an exact AgentFramework result summary was found. Current executor: {executorDisplayName}.",
            $"Do not approve a blind retry for {stepKey}. First confirm the process run id, exact step id, dispatch claim state, and assignment metadata. Approve rework only after the operator note states what evidence or runtime condition changed.",
            $"Manager-guided rework for step '{stepKey}' must start by collecting the missing diagnostic context. Preserve upstream artifacts, verify the assignment for role '{roleDisplayName}', and require fresh step-scoped evidence before returning Completed.");
    }

    private static bool IsRepairBranch(string branchOutcomeKey)
        => branchOutcomeKey.Contains("repair", StringComparison.OrdinalIgnoreCase);

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
