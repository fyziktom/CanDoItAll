using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactEvidence;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactService;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactFormatter;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactOutcomeParser;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessOutcomeGroundingValidator(IWorkspaceFileService workspaceFiles)
{
    internal ProcessCompletionIssue? ValidateGroundedOutcomeReferences(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        ProcessStepExecutionContract stepContract,
        ParentSubprocessBridgedOutcome? verifiedSubprocessOutcome = null)
        => ValidateGroundedOutcomeReferencesCore(
            assignment,
            output,
            toolReceipts,
            verifiedSubprocessOutcome,
            ReadVerifiedRequiredArtifactGroundingTexts(stepContract));

    internal static ProcessCompletionIssue? ValidateGroundedOutcomeReferences(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        ParentSubprocessBridgedOutcome? verifiedSubprocessOutcome = null)
        => ValidateGroundedOutcomeReferencesCore(
            assignment,
            output,
            toolReceipts,
            verifiedSubprocessOutcome,
            additionalGroundingTexts: null);

    internal ProcessStepOutcomeResult RemoveUngroundedNonAuthoritativeCriterionEvidenceRefs(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        ProcessStepExecutionContract stepContract,
        out int removedCount)
    {
        if (!ProcessOutcomeGroundingSanitizer.CanRemoveUngroundedNonAuthoritativeCriterionEvidenceRefs(
                assignment,
                output,
                stepContract))
        {
            removedCount = 0;
            return output;
        }

        return ProcessOutcomeGroundingSanitizer.RemoveUngroundedNonAuthoritativeCriterionEvidenceRefs(
            assignment,
            output,
            ProcessOutcomeReferenceGroundingPolicy.BuildOutcomeReferenceGroundingTexts(
                assignment,
                toolReceipts,
                ReadVerifiedRequiredArtifactGroundingTexts(stepContract)),
            out removedCount);
    }

    internal ProcessStepOutcomeResult RemoveUngroundedSupplementalEvidenceRefsFromGroundedDefectOutcome(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        ProcessStepExecutionContract stepContract,
        out int removedCount)
    {
        if (!ProcessOutcomeGroundingSanitizer.CanRemoveUngroundedSupplementalEvidenceRefsFromGroundedDefectOutcome(
                assignment,
                output,
                stepContract))
        {
            removedCount = 0;
            return output;
        }

        return ProcessOutcomeGroundingSanitizer.RemoveUngroundedSupplementalEvidenceRefsFromGroundedDefectOutcome(
            assignment,
            output,
            toolReceipts,
            ProcessOutcomeReferenceGroundingPolicy.BuildOutcomeReferenceGroundingTexts(
                assignment,
                toolReceipts,
                ReadVerifiedRequiredArtifactGroundingTexts(stepContract)),
            out removedCount);
    }

    private static ProcessCompletionIssue? ValidateGroundedOutcomeReferencesCore(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        ParentSubprocessBridgedOutcome? verifiedSubprocessOutcome,
        IEnumerable<string>? additionalGroundingTexts)
    {
        var verifiedEnvelopes = ProcessRuntimeSubprocessEnvelopeValidator.Resolve(
            verifiedSubprocessOutcome,
            toolReceipts);
        if (ProcessRuntimeSubprocessEnvelopeValidator.ValidateOutcome(
                assignment,
                output,
                verifiedEnvelopes) is { } forwardedContextIssue)
        {
            return forwardedContextIssue;
        }

        var ungroundedRefs = ProcessOutcomeReferenceGroundingPolicy.FindUngroundedPathReferences(
            assignment,
            EnumerateOutcomePathReferences(
                output,
                verifiedEnvelopes.ForwardedContext,
                verifiedEnvelopes.ChildOutput)
                .Concat(ProcessOutcomeReferenceGroundingPolicy
                    .EnumerateAcceptanceCriteriaPathReferences(output)),
            toolReceipts,
            additionalGroundingTexts);
        if (ungroundedRefs.Length == 0)
        {
            return null;
        }

        var refSummary = DescribeUngroundedReferenceSet(ungroundedRefs);
        return new ProcessCompletionIssue(
            "process.adapter.ungrounded_outcome_reference",
            $"Step '{assignment.StepKey}' claimed completion but cited {refSummary}. Those refs are not grounded in the current step brief, launch variables, required upstream refs, or current-run tool receipts. Retry the same step, remove the rejected path-like refs from the reason, summary, next actions, and evidence refs, and overwrite the managed artifact if needed. Do not quote or restate rejected literal path strings from diagnostics or earlier attempts. Keep a path-like ref only if this same retry first reads or writes current-run evidence that grounds the exact ref.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:ungrounded-outcome-reference:{ComputeHash(string.Join("|", ungroundedRefs))}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    internal ProcessCompletionIssue? ValidateManagedArtifactBodyReferences(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        ParentSubprocessBridgedOutcome? verifiedSubprocessOutcome = null,
        ProcessStepExecutionContract? stepContract = null)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed ||
            assignment.ProducedArtifactSlotIds.Count == 0)
        {
            return null;
        }

        var primaryRef = BuildManagedStepArtifactPath(assignment);
        var readResult = workspaceFiles.ReadTextFile(
            primaryRef,
            WorkspaceFileLimits.MaxTextReadCharacters);
        if (!readResult.Succeeded ||
            readResult.IsTruncated ||
            string.IsNullOrWhiteSpace(readResult.Content))
        {
            var readbackMessage = !readResult.Succeeded
                ? readResult.Message
                : readResult.IsTruncated
                    ? $"content exceeds the complete-read limit of {WorkspaceFileLimits.MaxTextReadCharacters} characters"
                    : "content is empty";
            return ProcessManagedArtifactService.CreateManagedArtifactReadbackIssue(
                assignment,
                primaryRef,
                readbackMessage);
        }

        var content = readResult.Content;
        if (ProcessOutcomeCitationSanitizer.TryRemoveNonCitableSourceMetadataLines(content, out var sanitizedContent))
        {
            var writeResult = workspaceFiles.WriteTextFile(primaryRef, sanitizedContent, overwrite: true);
            if (writeResult.Succeeded)
            {
                content = sanitizedContent;
            }
        }

        var verifiedEnvelopes = ProcessRuntimeSubprocessEnvelopeValidator.Resolve(
            verifiedSubprocessOutcome,
            toolReceipts);
        if (!ProcessRuntimeSubprocessEnvelopeValidator.TryRemoveVerified(
                content,
                verifiedEnvelopes,
                out var contentForGrounding))
        {
            return ProcessRuntimeSubprocessEnvelopeValidator.CreateInvalidEnvelopeIssue(
                assignment,
                managedArtifact: true);
        }

        var ungroundedRefs = ProcessOutcomeReferenceGroundingPolicy.FindUngroundedPathReferences(
            assignment,
            ProcessOutcomeReferenceGroundingPolicy.EnumerateTextPathReferences(contentForGrounding),
            toolReceipts,
            ReadVerifiedRequiredArtifactGroundingTexts(
                stepContract ?? ProcessStepExecutionContract.Empty));
        if (ungroundedRefs.Length == 0)
        {
            return null;
        }

        var refSummary = DescribeUngroundedReferenceSet(ungroundedRefs);
        return new ProcessCompletionIssue(
            "process.adapter.ungrounded_managed_artifact_reference",
            $"Step '{assignment.StepKey}' wrote primary managed artifact '{primaryRef}' with {refSummary}. Those refs are not grounded in the current step brief, launch variables, required upstream refs, or current-run successful tool receipts. Retry the same step, overwrite the artifact, and remove rejected path-like refs from the artifact body, reason, summary, next actions, and evidence refs. Do not quote or restate rejected literal path strings from diagnostics or earlier attempts. Keep a path-like ref only if this same retry first reads or writes current-run evidence that grounds the exact ref.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:ungrounded-managed-artifact-reference:{ComputeHash(string.Join("|", ungroundedRefs))}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static string RemoveVerifiedRuntimeSubprocessEnvelopes(
        string content,
        string? verifiedForwardedContextEnvelope,
        string? verifiedChildOutputEnvelope)
        => ProcessRuntimeSubprocessEnvelopeValidator.RemoveVerified(
            content,
            new ProcessRuntimeSubprocessVerifiedEnvelopes(
                verifiedChildOutputEnvelope,
                verifiedForwardedContextEnvelope));

    internal static string DescribeUngroundedReferenceSet(IReadOnlyList<string> ungroundedRefs)
        => ungroundedRefs.Count == 1
            ? "1 ungrounded path-like ref"
            : $"{ungroundedRefs.Count} ungrounded path-like refs";

    private IReadOnlyList<string> ReadVerifiedRequiredArtifactGroundingTexts(
        ProcessStepExecutionContract stepContract)
    {
        var groundingTexts = new List<string>();
        var availableInputs = stepContract.RequiredArtifacts
            .Where(input =>
                input.Availability == ProcessArtifactInputAvailability.Available &&
                input.ArtifactId is not null &&
                !string.IsNullOrWhiteSpace(input.ContentHash))
            .GroupBy(input => input.SlotId)
            .Where(group => group.Count() == 1)
            .Select(group => group.Single())
            .OrderBy(input => input.SlotId.Value);
        foreach (var input in availableInputs)
        {
            var matchingDescriptors = stepContract.ArtifactDescriptors
                .Where(descriptor => descriptor.SlotId == input.SlotId)
                .ToArray();
            if (matchingDescriptors.Length != 1)
            {
                continue;
            }

            var primaryRef = NormalizeManagedArtifactRef(
                matchingDescriptors[0].PrimaryManagedRef);
            if (!ProcessOutcomeReferenceGroundingPolicy.IsManagedMarkdownArtifactRef(primaryRef))
            {
                continue;
            }

            var readResult = workspaceFiles.ReadTextFile(
                primaryRef,
                WorkspaceFileLimits.MaxTextReadCharacters);
            if (!readResult.Succeeded ||
                readResult.IsTruncated ||
                string.IsNullOrWhiteSpace(readResult.Content) ||
                !string.Equals(
                    ComputeHash(readResult.Content),
                    input.ContentHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            groundingTexts.Add(primaryRef);
            groundingTexts.Add(readResult.Content);
        }

        return groundingTexts;
    }

    internal static IEnumerable<string> EnumerateOutcomePathReferences(
        ProcessStepOutcomeResult output,
        string? verifiedForwardedContextEnvelope = null,
        string? verifiedChildOutputEnvelope = null)
    {
        foreach (var text in ProcessOutcomeReferenceGroundingPolicy
                     .EnumerateOutcomeNarrativeText(output))
        {
            var textForGrounding = RemoveVerifiedRuntimeSubprocessEnvelopes(
                text ?? string.Empty,
                verifiedForwardedContextEnvelope,
                verifiedChildOutputEnvelope);
            foreach (var candidate in ProcessOutcomeReferenceGroundingPolicy
                         .EnumerateTextPathReferences(textForGrounding))
            {
                yield return candidate;
            }
        }

        foreach (var evidenceRef in output.EvidenceRefs)
        {
            foreach (var candidate in ProcessOutcomeReferenceGroundingPolicy
                         .EnumerateTextPathReferences(evidenceRef))
            {
                yield return candidate;
            }
        }
    }

}
