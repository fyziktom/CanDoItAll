using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal readonly record struct ProcessRuntimeSubprocessVerifiedEnvelopes(
    string? ChildOutput,
    string? ForwardedContext);

internal static class ProcessRuntimeSubprocessEnvelopeValidator
{
    internal static ProcessRuntimeSubprocessVerifiedEnvelopes Resolve(
        ParentSubprocessBridgedOutcome? verifiedSubprocessOutcome,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts)
    {
        if (verifiedSubprocessOutcome is null ||
            !HasRuntimeSubprocessBridgeReceipt(verifiedSubprocessOutcome, toolReceipts))
        {
            return default;
        }

        var childOutput = ParentSubprocessVerifiedChildOutputEnvelope.Format(
            verifiedSubprocessOutcome.VerifiedChildOutput);

        string? forwardedContext = null;
        if (verifiedSubprocessOutcome.ForwardedContextArtifacts.Count > 0)
        {
            forwardedContext = ParentSubprocessForwardedContextEnvelope.Format(
                verifiedSubprocessOutcome.ForwardedContextArtifacts);
        }

        return new ProcessRuntimeSubprocessVerifiedEnvelopes(
            NormalizeEnvelope(childOutput),
            NormalizeEnvelope(forwardedContext));
    }

    internal static ProcessCompletionIssue? ValidateOutcome(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        ProcessRuntimeSubprocessVerifiedEnvelopes verifiedEnvelopes)
    {
        var summary = output.HumanReadableSummaryMarkdown ?? string.Empty;
        if (!TryRemoveVerified(summary, verifiedEnvelopes, out _) ||
            EnumerateOutcomeTextOutsideSummary(output)
                .Any(text =>
                    ParentSubprocessVerifiedChildOutputEnvelope.ContainsReservedMarker(text) ||
                    ParentSubprocessForwardedContextEnvelope.ContainsReservedMarker(text)))
        {
            return CreateInvalidEnvelopeIssue(assignment, managedArtifact: false);
        }

        return null;
    }

    internal static bool TryRemoveVerified(
        string content,
        ProcessRuntimeSubprocessVerifiedEnvelopes verifiedEnvelopes,
        out string contentWithoutEnvelopes)
    {
        var childOutputMatch = ParentSubprocessVerifiedChildOutputEnvelope.TryRemoveSingleVerified(
            content,
            verifiedEnvelopes.ChildOutput,
            out var contentWithoutChildOutput);
        var forwardedContextMatch = ParentSubprocessForwardedContextEnvelope.TryRemoveSingleVerified(
            contentWithoutChildOutput,
            verifiedEnvelopes.ForwardedContext,
            out contentWithoutEnvelopes);
        return childOutputMatch != ParentSubprocessVerifiedChildOutputEnvelope.MatchResult.Invalid &&
               (verifiedEnvelopes.ChildOutput is null ||
                childOutputMatch == ParentSubprocessVerifiedChildOutputEnvelope.MatchResult.Removed) &&
               forwardedContextMatch != ParentSubprocessForwardedContextEnvelope.MatchResult.Invalid &&
               (verifiedEnvelopes.ForwardedContext is null ||
                forwardedContextMatch == ParentSubprocessForwardedContextEnvelope.MatchResult.Removed) &&
               !ParentSubprocessVerifiedChildOutputEnvelope.ContainsReservedMarker(contentWithoutEnvelopes) &&
               !ParentSubprocessForwardedContextEnvelope.ContainsReservedMarker(contentWithoutEnvelopes);
    }

    internal static string RemoveVerified(
        string content,
        ProcessRuntimeSubprocessVerifiedEnvelopes verifiedEnvelopes)
    {
        var childOutputMatch = ParentSubprocessVerifiedChildOutputEnvelope.TryRemoveSingleVerified(
            content,
            verifiedEnvelopes.ChildOutput,
            out var contentWithoutChildOutput);
        var forwardedContextMatch = ParentSubprocessForwardedContextEnvelope.TryRemoveSingleVerified(
            contentWithoutChildOutput,
            verifiedEnvelopes.ForwardedContext,
            out var contentWithoutEnvelopes);
        return childOutputMatch == ParentSubprocessVerifiedChildOutputEnvelope.MatchResult.Removed ||
               forwardedContextMatch == ParentSubprocessForwardedContextEnvelope.MatchResult.Removed
            ? contentWithoutEnvelopes
            : content;
    }

    internal static ProcessCompletionIssue CreateInvalidEnvelopeIssue(
        ProcessRuntimeStepAssignment assignment,
        bool managedArtifact)
    {
        var location = managedArtifact ? "primary managed artifact" : "structured outcome";
        var diagnosticCode = managedArtifact
            ? "process.adapter.ungrounded_managed_artifact_reference"
            : "process.adapter.ungrounded_outcome_reference";
        return new ProcessCompletionIssue(
            diagnosticCode,
            $"Step '{assignment.StepKey}' produced a {location} containing a reserved runtime child-output or forwarded-context envelope that did not match exactly one verified runtime subprocess bridge. Retry the same step without creating, copying, or editing runtime envelope markers; the process runtime owns subprocess handoff materialization.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:invalid-runtime-forwarded-context-envelope:{location}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static bool HasRuntimeSubprocessBridgeReceipt(
        ParentSubprocessBridgedOutcome verifiedSubprocessOutcome,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts)
        => toolReceipts?.Any(receipt =>
            verifiedSubprocessOutcome.ToolReceipts.Any(trustedReceipt => trustedReceipt.Id == receipt.Id) &&
            receipt.ExecutionRunId == verifiedSubprocessOutcome.SyntheticExecutionRunId &&
            string.Equals(receipt.ToolFamily, "process-runtime", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(receipt.ToolName, ProcessSubprocessState.SubprocessLaunchToolName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(receipt.RiskClass, "ProcessRuntime", StringComparison.OrdinalIgnoreCase) &&
            ProcessOutcomeReferenceGroundingPolicy.IsGroundingToolReceipt(receipt) &&
            receipt.ExitSummary.Contains(
                verifiedSubprocessOutcome.ChildRunId.Value.ToString("D"),
                StringComparison.OrdinalIgnoreCase)) == true;

    private static IEnumerable<string?> EnumerateOutcomeTextOutsideSummary(ProcessStepOutcomeResult output)
    {
        yield return output.Reason;
        yield return output.BranchOutcomeKey;
        yield return output.BranchOutcomeTitle;

        foreach (var evidenceRef in output.EvidenceRefs)
        {
            yield return evidenceRef;
        }

        foreach (var nextAction in output.NextActions)
        {
            yield return nextAction;
        }

        foreach (var criterion in output.AcceptanceCriteriaEvidence)
        {
            yield return criterion.CriterionId;
            yield return criterion.Summary;
            foreach (var evidenceRef in criterion.EvidenceRefs)
            {
                yield return evidenceRef;
            }
        }
    }

    private static string? NormalizeEnvelope(string? envelope)
        => string.IsNullOrWhiteSpace(envelope)
            ? null
            : envelope;
}
