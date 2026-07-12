using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class AgentFrameworkProcessExecutionAdapter
{
    internal static ProcessExecutionAdapterResult ToAdapterResult(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        string rawOutputHash,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts = null)
    {
        output = RemoveNonCitableSourceMetadataFromOutcome(output);

        var outcome = output.Status switch
        {
            ProcessStepOutcomeStatus.Completed => StrategyOutcome.Succeeded,
            ProcessStepOutcomeStatus.Blocked or ProcessStepOutcomeStatus.WaitingApproval => StrategyOutcome.NeedsManager,
            ProcessStepOutcomeStatus.Refused => StrategyOutcome.Canceled,
            ProcessStepOutcomeStatus.Failed => StrategyOutcome.Failed,
            _ => StrategyOutcome.Failed
        };
        if (outcome == StrategyOutcome.NeedsManager &&
            IsRetryableNonTerminalPrimaryArtifactBlocker(assignment, output, toolReceipts))
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                CreateNonTerminalPrimaryArtifactRetryIssue(assignment, output));
        }

        if (outcome == StrategyOutcome.NeedsManager &&
            IsRetryableManagedArtifactSelfEvidenceBlocker(assignment, output))
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                CreateManagedArtifactSelfEvidenceRetryIssue(assignment, output));
        }

        if (outcome == StrategyOutcome.NeedsManager &&
            IsRetryableManagedArtifactMissingPrimaryOutputBlocker(assignment, output))
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                CreateManagedArtifactMissingPrimaryOutputRetryIssue(assignment, output));
        }

        if (outcome == StrategyOutcome.NeedsManager &&
            IsRetryableSubprocessLaunchSkippedBlocker(assignment, output, toolReceipts))
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                CreateSubprocessLaunchSkippedRetryIssue(assignment, output));
        }

        if (outcome == StrategyOutcome.NeedsManager &&
            TryCreateProductRequiredToolReceiptBlockedRetryIssue(assignment, output, toolReceipts, out var requiredToolReceiptBlockedIssue))
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                requiredToolReceiptBlockedIssue);
        }

        if (outcome == StrategyOutcome.NeedsManager &&
            TryCreateProductRequiredStateBlockedRetryIssue(assignment, output, toolReceipts, out var requiredStateBlockedIssue))
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                requiredStateBlockedIssue);
        }

        if (outcome == StrategyOutcome.NeedsManager &&
            IsRetryableProductMutationEvidenceBlocker(assignment, output, toolReceipts))
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                CreateProductMutationEvidenceRetryIssue(assignment, output, toolReceipts!));
        }

        if ((outcome == StrategyOutcome.NeedsManager || outcome == StrategyOutcome.Succeeded) &&
            TryInferEvidenceBackedBranchOutcome(assignment, output, out var inferredBranchOutcomeKey))
        {
            output = CopyWithBranchOutcomeKey(output, inferredBranchOutcomeKey);
        }

        if (outcome == StrategyOutcome.NeedsManager &&
            ShouldRouteBlockedBranchOutcome(assignment, output))
        {
            output = CopyAsCompletedBranchOutcome(output);
            outcome = StrategyOutcome.Succeeded;
        }

        if (outcome == StrategyOutcome.Succeeded &&
            IsRetryableSubprocessLaunchSkippedCompletion(assignment, output, toolReceipts))
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                CreateSubprocessLaunchSkippedRetryIssue(assignment, output));
        }

        if (outcome == StrategyOutcome.Succeeded &&
            ValidateGroundedOutcomeReferences(assignment, output, toolReceipts) is { } ungroundedReferenceIssue)
        {
            return NeedsManagerForCompletionIssue(assignment, rawOutputHash, ungroundedReferenceIssue);
        }

        if (outcome == StrategyOutcome.Succeeded &&
            ValidateProductMutationCompletion(assignment, output) is { } productMutationIssue)
        {
            return NeedsManagerForCompletionIssue(assignment, rawOutputHash, productMutationIssue);
        }

        if (outcome == StrategyOutcome.Succeeded &&
            ValidateProductMutationWriteReceipt(assignment, output, toolReceipts) is { } productMutationWriteIssue)
        {
            return NeedsManagerForCompletionIssue(assignment, rawOutputHash, productMutationWriteIssue);
        }

        if (outcome == StrategyOutcome.Succeeded &&
            ValidateRequiredProductToolReceipts(assignment, toolReceipts) is { } requiredToolReceiptIssue)
        {
            return NeedsManagerForCompletionIssue(assignment, rawOutputHash, requiredToolReceiptIssue);
        }

        if (outcome == StrategyOutcome.Succeeded &&
            ValidateRequiredProductStateCompletion(assignment, output) is { } requiredProductStateIssue)
        {
            return NeedsManagerForCompletionIssue(assignment, rawOutputHash, requiredProductStateIssue);
        }

        if (outcome == StrategyOutcome.Succeeded &&
            ValidateCompletedOutcomeDoesNotDeclareBlockers(assignment, output) is { } declaredBlockerIssue)
        {
            return NeedsManagerForCompletionIssue(assignment, rawOutputHash, declaredBlockerIssue);
        }

        if (outcome == StrategyOutcome.Succeeded &&
            ValidateManagedArtifactCompletion(assignment, output) is { } managedArtifactIssue)
        {
            return NeedsManagerForCompletionIssue(assignment, rawOutputHash, managedArtifactIssue);
        }

        if (outcome == StrategyOutcome.Succeeded &&
            ValidateManagedArtifactWriteReceipt(assignment, toolReceipts) is { } managedArtifactWriteIssue)
        {
            return NeedsManagerForCompletionIssue(assignment, rawOutputHash, managedArtifactWriteIssue);
        }

        IReadOnlyList<ProducedArtifactRef> artifacts = outcome == StrategyOutcome.Succeeded
            ? assignment.ProducedArtifactSlotIds
                .Select(slotId => new ProducedArtifactRef(
                    ArtifactInstanceId.New(),
                    slotId,
                    ComputeHash($"{rawOutputHash}:{assignment.StepInstanceId}:{slotId}")))
                .ToArray()
            : [];
        IReadOnlyList<RequestedArtifactRef> requestedArtifacts = outcome == StrategyOutcome.NeedsManager
            ? assignment.RequiredArtifactSlotIds
                .Select(slotId => new RequestedArtifactRef(
                    slotId,
                    ComputeHash($"{rawOutputHash}:requested:{slotId}")))
                .ToArray()
            : [];
        var diagnostics = new List<ProcessExecutionAdapterDiagnostic>();
        var managerSignals = new List<ManagerSignal>();
        var userSafeSummary = output.Reason;
        if (!string.IsNullOrWhiteSpace(output.BranchOutcomeKey))
        {
            managerSignals.Add(new ManagerSignal(
                ProcessBranchSignalCodes.Outcome(output.BranchOutcomeKey),
                ComputeHash(output.BranchOutcomeKey),
                string.IsNullOrWhiteSpace(output.BranchOutcomeTitle)
                    ? $"Branch outcome selected: {output.BranchOutcomeKey}"
                    : output.BranchOutcomeTitle));
        }

        if (outcome == StrategyOutcome.NeedsManager &&
            TryBuildAgentRightsManagerRequest(assignment, output, out var managerRequest))
        {
            var rightsHash = ComputeHash($"{rawOutputHash}:agent-rights:{managerRequest}");
            diagnostics.Add(new ProcessExecutionAdapterDiagnostic(
                new StrategyDiagnosticCode(AgentRightsManagerRequestCode),
                StrategyDiagnosticSensitivity.Normal,
                rightsHash,
                managerRequest,
                RestrictedEvidenceReference: null,
                ProcessDiagnosticRetrySafety.UnsafeToRetry,
                ProcessDiagnosticIdempotencyClassification.Idempotent));
            managerSignals.Add(new ManagerSignal(
                new ManagerSignalCode(AgentRightsManagerRequestCode),
                rightsHash,
                managerRequest));
            userSafeSummary = string.IsNullOrWhiteSpace(userSafeSummary)
                ? managerRequest
                : $"{userSafeSummary}{Environment.NewLine}{Environment.NewLine}{managerRequest}";
        }

        return new ProcessExecutionAdapterResult(
            outcome,
            artifacts,
            requestedArtifacts,
            diagnostics,
            managerSignals,
            userSafeSummary,
            rawOutputHash);
    }

    private static bool ShouldRouteBlockedBranchOutcome(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
        => output.Status == ProcessStepOutcomeStatus.Blocked &&
           !string.IsNullOrWhiteSpace(output.BranchOutcomeKey) &&
           (assignment.ProducedArtifactSlotIds.Count == 0 || output.EvidenceRefs.Count > 0);

    private static bool TryInferEvidenceBackedBranchOutcome(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        out string branchOutcomeKey)
    {
        branchOutcomeKey = string.Empty;
        if (output.Status is not (ProcessStepOutcomeStatus.Blocked or ProcessStepOutcomeStatus.Completed) ||
            !string.IsNullOrWhiteSpace(output.BranchOutcomeKey) ||
            assignment.ProducedArtifactSlotIds.Count > 0 && output.EvidenceRefs.Count == 0)
        {
            return false;
        }

        var outputTextParts = EnumerateOutcomeText(output)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        var outputText = string.Join(" ", outputTextParts);
        var declaredBranchOutcomes = EnumerateDeclaredBranchOutcomes(assignment.Prompt)
            .GroupBy(outcome => outcome.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        if (TryReadExplicitBranchOutcomeKey(string.Join(Environment.NewLine, outputTextParts), declaredBranchOutcomes, out branchOutcomeKey))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(outputText) ||
            LooksLikeRightsOrToolBoundary(outputText) ||
            !LooksLikeBranchSelectionText(outputText))
        {
            return false;
        }

        var mentionedBranchKeys = declaredBranchOutcomes
            .Where(outcome => ContainsBranchOutcomeKey(outputText, outcome.Key))
            .Select(outcome => outcome.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (mentionedBranchKeys.Length == 1)
        {
            branchOutcomeKey = mentionedBranchKeys[0];
            return true;
        }

        if (mentionedBranchKeys.Length > 1)
        {
            return false;
        }

        var mentionedBranchTitles = declaredBranchOutcomes
            .Where(outcome => ContainsBranchOutcomeTitle(outputText, outcome.Title))
            .Select(outcome => outcome.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (mentionedBranchTitles.Length != 1)
        {
            return false;
        }

        branchOutcomeKey = mentionedBranchTitles[0];
        return true;
    }

    private static bool TryReadExplicitBranchOutcomeKey(
        string text,
        IReadOnlyCollection<BranchOutcomePromptDescriptor> declaredBranchOutcomes,
        out string branchOutcomeKey)
    {
        branchOutcomeKey = string.Empty;
        if (string.IsNullOrWhiteSpace(text) ||
            declaredBranchOutcomes.Count == 0)
        {
            return false;
        }

        var explicitKeys = ReadExplicitBranchOutcomeKeys(text)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (explicitKeys.Length != 1)
        {
            return false;
        }

        var declaredMatches = declaredBranchOutcomes
            .Where(outcome => string.Equals(outcome.Key, explicitKeys[0], StringComparison.OrdinalIgnoreCase))
            .Select(outcome => outcome.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (declaredMatches.Length != 1)
        {
            return false;
        }

        branchOutcomeKey = declaredMatches[0];
        return true;
    }

    private static IEnumerable<string> ReadExplicitBranchOutcomeKeys(string text)
    {
        foreach (Match match in ExplicitBranchOutcomeKeyLineRegex().Matches(text))
        {
            var value = match.Groups["key"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
        }

        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < lines.Length - 1; index++)
        {
            var line = NormalizeOutcomeMarkdownMetadataLine(lines[index]);
            if (!string.Equals(line, "Branch outcome key", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = NormalizeBranchOutcomeKeyCandidate(lines[index + 1]);
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
        }
    }

    private static string NormalizeOutcomeMarkdownMetadataLine(string value)
        => value.Trim().TrimStart('#', '-', '*', ' ').Trim(' ', '*', '`', ':');

    private static string NormalizeBranchOutcomeKeyCandidate(string value)
    {
        var trimmed = NormalizeOutcomeMarkdownMetadataLine(value).Trim('.', ';');
        var commentIndex = trimmed.IndexOf('#', StringComparison.Ordinal);
        if (commentIndex >= 0)
        {
            trimmed = trimmed[..commentIndex].Trim();
        }

        return Regex.IsMatch(
            trimmed,
            @"^[A-Za-z0-9][A-Za-z0-9._-]*$",
            RegexOptions.CultureInvariant)
            ? trimmed
            : string.Empty;
    }

    private static bool LooksLikeBranchSelectionText(string text)
        => ContainsAny(
            text,
            "branch outcome",
            "branch key",
            "selected branch",
            "select branch",
            "selected outcome",
            "select outcome",
            "choose outcome",
            "chose outcome",
            "validation decision",
            "repair decision",
            "acceptance decision",
            "selected decision",
            "route to",
            "routing to",
            "# outcome",
            "outcome -",
            "outcome:");

    private static IEnumerable<string> EnumerateDeclaredBranchOutcomeKeys(string prompt)
        => EnumerateDeclaredBranchOutcomes(prompt).Select(outcome => outcome.Key);

    private static IEnumerable<BranchOutcomePromptDescriptor> EnumerateDeclaredBranchOutcomes(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            yield break;
        }

        foreach (Match match in BranchOutcomePromptLineRegex().Matches(prompt))
        {
            var key = match.Groups["key"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                yield return new BranchOutcomePromptDescriptor(
                    key,
                    ExtractBranchOutcomeTitle(match.Groups["rest"].Value));
            }
        }
    }

    private static string ExtractBranchOutcomeTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var title = value.Trim();
        var separatorIndex = title.IndexOf(" - ", StringComparison.Ordinal);
        if (separatorIndex >= 0)
        {
            title = title[..separatorIndex];
        }

        return title.Trim(' ', '`', '*', '.', ':', ';', '-');
    }

    private static bool ContainsBranchOutcomeKey(string text, string branchOutcomeKey)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            string.IsNullOrWhiteSpace(branchOutcomeKey))
        {
            return false;
        }

        return Regex.IsMatch(
            text,
            $@"(?<![A-Za-z0-9._-]){Regex.Escape(branchOutcomeKey.Trim())}(?![A-Za-z0-9._-])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool ContainsBranchOutcomeTitle(string text, string branchOutcomeTitle)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            !IsInferableBranchOutcomeTitle(branchOutcomeTitle))
        {
            return false;
        }

        return Regex.IsMatch(
            text,
            $@"(?<![A-Za-z0-9]){Regex.Escape(branchOutcomeTitle.Trim())}(?![A-Za-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool IsInferableBranchOutcomeTitle(string branchOutcomeTitle)
    {
        if (string.IsNullOrWhiteSpace(branchOutcomeTitle))
        {
            return false;
        }

        var words = Regex.Matches(branchOutcomeTitle, @"[A-Za-z0-9]+", RegexOptions.CultureInvariant)
            .Select(match => match.Value)
            .ToArray();
        return words.Length >= 2 && words.Sum(word => word.Length) >= 8;
    }

    private sealed record BranchOutcomePromptDescriptor(string Key, string Title);

    private static bool IsRetryableNonTerminalPrimaryArtifactBlocker(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts)
    {
        if (output.Status != ProcessStepOutcomeStatus.Blocked ||
            assignment.ProducedArtifactSlotIds.Count == 0)
        {
            return false;
        }

        var text = string.Join(
            " ",
            EnumerateOutcomeText(output).Where(value => !string.IsNullOrWhiteSpace(value)));
        if (!ContainsNonTerminalStatusDeclaration(text) ||
            LooksLikeRightsOrToolBoundary(text))
        {
            return false;
        }

        var primaryRef = NormalizeManagedArtifactRef(BuildManagedStepArtifactPath(assignment));
        var hasPrimaryEvidenceRef = output.EvidenceRefs.Any(evidenceRef =>
            !string.IsNullOrWhiteSpace(evidenceRef) &&
            ReceiptTargetsManagedRef(evidenceRef, primaryRef));
        var hasPrimaryWriteReceipt = toolReceipts is not null &&
                                     HasManagedArtifactWriteReceipt(toolReceipts, primaryRef);
        return hasPrimaryEvidenceRef || hasPrimaryWriteReceipt;
    }

    private static bool ContainsNonTerminalStatusDeclaration(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (NonTerminalStatusDeclarationRegex().IsMatch(text))
        {
            return true;
        }

        foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim().TrimStart('#', '-', '*', ' ');
            var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim(' ', '*', '`');
            if (!string.Equals(key, "Status", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var statusValue = line[(separatorIndex + 1)..].Trim();
            var commentIndex = statusValue.IndexOf('#', StringComparison.Ordinal);
            if (commentIndex >= 0)
            {
                statusValue = statusValue[..commentIndex].Trim();
            }

            var normalized = new string(
                statusValue
                    .Where(character => char.IsLetterOrDigit(character))
                    .Select(char.ToLowerInvariant)
                    .ToArray());
            if (normalized is "inprogress" or "progress" or "working" or "running" or "started")
            {
                return true;
            }
        }

        return ContainsAny(
            text,
            "non-terminal status",
            "non terminal status",
            "not a terminal status");
    }

    private static ProcessCompletionIssue CreateNonTerminalPrimaryArtifactRetryIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        var primaryRef = BuildManagedStepArtifactPath(assignment);
        var originalReason = string.IsNullOrWhiteSpace(output.Reason)
            ? "The primary managed artifact declared a non-terminal status."
            : output.Reason.Trim();
        var summary = $"Step '{assignment.StepKey}' wrote primary managed artifact '{primaryRef}' with a non-terminal Status line. Retry the same step: preserve or overwrite that artifact only with a final Status line of Completed, Blocked, Failed, WaitingApproval, or Refused after the step's required work and evidence readbacks are complete. Original reason: {originalReason}";

        return new ProcessCompletionIssue(
            "process.adapter.non_terminal_primary_artifact_retry",
            summary,
            $"{assignment.RunId}:{assignment.StepInstanceId}:non-terminal-primary-artifact:{primaryRef}:{ComputeHash(originalReason)}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static ProcessStepOutcomeResult CopyAsCompletedBranchOutcome(ProcessStepOutcomeResult output)
    {
        var originalReason = string.IsNullOrWhiteSpace(output.Reason)
            ? "The agent returned Blocked while selecting a branch outcome."
            : output.Reason.Trim();
        return new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = $"Runtime routed evidence-backed branch outcome '{output.BranchOutcomeKey.Trim()}' after the agent returned Blocked with a selected branch. Original reason: {originalReason}",
            BranchOutcomeKey = output.BranchOutcomeKey,
            BranchOutcomeTitle = output.BranchOutcomeTitle,
            EvidenceRefs = output.EvidenceRefs,
            NextActions = output.NextActions,
            HumanReadableSummaryMarkdown = output.HumanReadableSummaryMarkdown
        };
    }

    private static ProcessStepOutcomeResult CopyWithBranchOutcomeKey(
        ProcessStepOutcomeResult output,
        string branchOutcomeKey)
    {
        return new ProcessStepOutcomeResult
        {
            Status = output.Status,
            Reason = output.Reason,
            BranchOutcomeKey = branchOutcomeKey,
            BranchOutcomeTitle = output.BranchOutcomeTitle,
            EvidenceRefs = output.EvidenceRefs,
            NextActions = output.NextActions,
            HumanReadableSummaryMarkdown = output.HumanReadableSummaryMarkdown
        };
    }

    private static bool IsRetryableManagedArtifactSelfEvidenceBlocker(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        if (output.Status != ProcessStepOutcomeStatus.Blocked ||
            assignment.ProducedArtifactSlotIds.Count == 0)
        {
            return false;
        }

        var operations = NormalizeOperations(assignment.AllowedOperations);
        if (operations.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparer.OrdinalIgnoreCase) ||
            operations.Contains(ProcessOperationContractNames.LaunchRuntime, StringComparer.OrdinalIgnoreCase) ||
            operations.Contains(ProcessOperationContractNames.CaptureRuntimeProof, StringComparer.OrdinalIgnoreCase) ||
            operations.Contains(ProcessOperationContractNames.RunValidation, StringComparer.OrdinalIgnoreCase) ||
            operations.Contains(ProcessOperationContractNames.MutateProductTarget, StringComparer.OrdinalIgnoreCase) ||
            AllowsProductMutation(operations, assignment.OperationTargetScope))
        {
            return false;
        }

        var text = string.Join(
            " ",
            EnumerateOutcomeText(output).Where(value => !string.IsNullOrWhiteSpace(value)));
        if (LooksLikeRightsOrToolBoundary(text))
        {
            return false;
        }

        return ContainsAny(
            text,
            "No prior assistant text",
            "tool output",
            "process artifact evidence",
            "insufficient evidence",
            "current-run evidence",
            "concrete current-run evidence",
            "managed artifact evidence",
            "evidence reference");
    }

    private static bool IsRetryableManagedArtifactMissingPrimaryOutputBlocker(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        if (output.Status != ProcessStepOutcomeStatus.Blocked ||
            assignment.ProducedArtifactSlotIds.Count == 0)
        {
            return false;
        }

        var operations = NormalizeOperations(assignment.AllowedOperations);
        if (!operations.Contains(ProcessOperationContractNames.WriteManagedProcessArtifacts, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var text = string.Join(
            " ",
            EnumerateOutcomeText(output).Where(value => !string.IsNullOrWhiteSpace(value)));
        if (LooksLikeRightsOrToolBoundary(text))
        {
            return false;
        }

        return ContainsAny(
                   text,
                   "missing-primary-output",
                   "primary managed output not written",
                   "primary output ref was not created",
                   "primary managed artifact",
                   "required primary managed output",
                   "create the required primary managed")
               && ContainsAny(
                   text,
                   "workspace_write_file",
                   "workspace_append_file",
                   "primary write ref",
                   "managed artifact");
    }

    private static ProcessCompletionIssue CreateManagedArtifactSelfEvidenceRetryIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        var primaryRef = BuildManagedStepArtifactPath(assignment);
        var requiredSlotSummary = assignment.RequiredArtifactSlotIds.Count == 0
            ? "none"
            : string.Join(", ", assignment.RequiredArtifactSlotIds.Select(slotId => slotId.Value.ToString("D")));
        var originalReason = string.IsNullOrWhiteSpace(output.Reason)
            ? "The agent reported a generic insufficient-evidence blocker."
            : output.Reason.Trim();
        var summary = $"Step '{assignment.StepKey}' returned a generic insufficient-evidence Blocked result before producing its required managed artifact. Retry the same step: read any required upstream refs listed in the step brief with workspace_read_file when a stat says the ref exists, do not invent sibling files from artifact expectation keys, then write primary managed artifact '{primaryRef}' with workspace_write_file or workspace_append_file and return Completed only with evidenceRefs containing that ref. Required upstream slot ids: {requiredSlotSummary}. Original reason: {originalReason}";

        return new ProcessCompletionIssue(
            "process.adapter.managed_artifact_self_evidence_retry",
            summary,
            $"{assignment.RunId}:{assignment.StepInstanceId}:managed-artifact-self-evidence:{primaryRef}:{requiredSlotSummary}:{ComputeHash(originalReason)}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static ProcessCompletionIssue CreateManagedArtifactMissingPrimaryOutputRetryIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        var primaryRef = BuildManagedStepArtifactPath(assignment);
        var originalReason = string.IsNullOrWhiteSpace(output.Reason)
            ? "The agent reported that the primary managed output was not written."
            : output.Reason.Trim();
        var summary = $"Step '{assignment.StepKey}' reported a missing primary managed artifact instead of creating its own output. Retry the same step: use the already-read upstream evidence, create primary managed artifact '{primaryRef}' with workspace_write_file or workspace_append_file, re-read or cite that ref, then return Completed or a concrete repair-required branch only after evidenceRefs contains that ref. Original reason: {originalReason}";

        return new ProcessCompletionIssue(
            "process.adapter.managed_artifact_missing_primary_output_retry",
            summary,
            $"{assignment.RunId}:{assignment.StepInstanceId}:managed-artifact-missing-primary:{primaryRef}:{ComputeHash(originalReason)}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static bool IsRetryableSubprocessLaunchSkippedBlocker(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts)
    {
        if (output.Status != ProcessStepOutcomeStatus.Blocked ||
            !RequiresSubprocessLaunch(assignment) ||
            HasToolReceipt(toolReceipts, SubprocessLaunchToolName))
        {
            return false;
        }

        var text = string.Join(
            " ",
            EnumerateOutcomeText(output).Where(value => !string.IsNullOrWhiteSpace(value)));
        if (LooksLikeParentExpectedDirectChildTools(text))
        {
            return true;
        }

        if (LooksLikeSubprocessLaunchToolBoundary(text))
        {
            return false;
        }

        if (LooksLikeUnverifiedSubprocessLaunchCapabilityBlocker(text))
        {
            return true;
        }

        if (LooksLikeRightsOrToolBoundary(text))
        {
            return false;
        }

        return ContainsAny(
            text,
            "subprocess was not launched",
            "subprocess were not launched",
            "child subprocess was not launched",
            "required subprocess was not launched",
            "required child run was not launched",
            "child run was not launched",
            "no current child run",
            "no child run receipt",
            "missing child run receipt");
    }

    private static bool IsRetryableSubprocessLaunchSkippedCompletion(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts)
    {
        return output.Status == ProcessStepOutcomeStatus.Completed &&
               RequiresSubprocessLaunch(assignment) &&
               !HasToolReceipt(toolReceipts, SubprocessLaunchToolName) &&
               !HasChildProcessEvidenceRef(assignment, output.EvidenceRefs);
    }

    private static bool RequiresSubprocessLaunch(ProcessRuntimeStepAssignment assignment)
    {
        return ProcessRuntimeLaunchVariables.TryReadProcessStepSubprocessDefinitionKey(
                   assignment.LaunchVariables,
                   out _) &&
               assignment.AllowedOperations.Contains(
                   ProcessOperationContractNames.ExecuteExternalAction,
                   StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasChildProcessEvidenceRef(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<string> evidenceRefs)
    {
        var ownRunId = assignment.RunId.Value.ToString("D");
        foreach (var evidenceRef in evidenceRefs)
        {
            var normalizedRef = evidenceRef.Replace('\\', '/');
            var match = Regex.Match(
                normalizedRef,
                @"(?:^|/)process-runs/(?<runId>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})(?:/|$)",
                RegexOptions.CultureInvariant);
            if (match.Success &&
                !string.Equals(match.Groups["runId"].Value, ownRunId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeSubprocessLaunchToolBoundary(string text)
    {
        if (ContainsAny(text, SubprocessLaunchToolName))
        {
            return ContainsAny(
                text,
                $"Tool '{SubprocessLaunchToolName}'",
                $"tool '{SubprocessLaunchToolName}'",
                $"Tool \"{SubprocessLaunchToolName}\"",
                $"tool \"{SubprocessLaunchToolName}\"",
                $"no {SubprocessLaunchToolName}",
                $"{SubprocessLaunchToolName} not available",
                $"{SubprocessLaunchToolName} unavailable",
                $"not authorized to use {SubprocessLaunchToolName}",
                $"denied tool {SubprocessLaunchToolName}");
        }

        return ContainsAny(
            text,
            "subprocess launch tool is not available",
            "subprocess launch tool unavailable");
    }

    private static bool LooksLikeParentExpectedDirectChildTools(string text)
    {
        if (!ContainsAny(text, "subprocess", "child process", "child run"))
        {
            return false;
        }

        if (ContainsAny(
            text,
            "step contract explicitly says to launch",
            "only project-structure subprocess launch tools are available",
            "only subprocess launch tools are available"))
        {
            return true;
        }

        return ContainsAny(
                   text,
                   "direct child-work tools",
                   "direct implementation",
                   "direct scaffold",
                   "direct validation",
                   "parent toolset",
                   "child-work capability") &&
               ContainsAny(
                   text,
                   "not available",
                   "not exposed",
                   "missing tool",
                   "capability",
                   "cannot proceed");
    }

    private static bool LooksLikeUnverifiedSubprocessLaunchCapabilityBlocker(string text)
    {
        if (ContainsAny(text, "composed capability set", "not part of the composed capability set"))
        {
            return false;
        }

        if (!ContainsAny(text, "subprocess", "child process", "child run", SubprocessLaunchToolName))
        {
            return false;
        }

        if (!ContainsAny(text, "launch capability", "child launch", "launch path", "ExecuteExternalAction", SubprocessLaunchToolName))
        {
            return false;
        }

        return ContainsAny(
            text,
            "unavailable",
            "not available",
            "does not expose",
            "not expose",
            "missing",
            "cannot launch",
            "grant",
            "reassign");
    }

    private static ProcessCompletionIssue CreateSubprocessLaunchSkippedRetryIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        ProcessRuntimeLaunchVariables.TryReadProcessStepSubprocessDefinitionKey(
            assignment.LaunchVariables,
            out var subprocessDefinitionKey);
        var childKeySummary = string.IsNullOrWhiteSpace(subprocessDefinitionKey)
            ? "the mapped child process definition"
            : $"DefinitionKey '{subprocessDefinitionKey}'";
        var originalReason = string.IsNullOrWhiteSpace(output.Reason)
            ? $"The agent returned {output.Status} before launching the required subprocess."
            : output.Reason.Trim();
        var requestedSlots = assignment.ProducedArtifactSlotIds.Count > 0
            ? assignment.ProducedArtifactSlotIds
            : assignment.RequiredArtifactSlotIds;

        return new ProcessCompletionIssue(
            "process.adapter.subprocess_launch_skipped_retry",
            $"Step '{assignment.StepKey}' is mapped to a subprocess and has ExecuteExternalAction, but the agent returned {output.Status} before invoking {SubprocessLaunchToolName} or citing child-run evidence. Retry the same step: call {SubprocessLaunchToolName} with {childKeySummary}; if launch returns ParentDeferredOutcomeJson, submit that deferred outcome exactly. Complete from child evidence only after a stopped child run is cited through managed artifact refs. Block only after a current launch-tool denial, missing required launch input, or concrete stopped-child blocker. Original reason: {originalReason}",
            $"{assignment.RunId}:{assignment.StepInstanceId}:subprocess-launch-skipped:{subprocessDefinitionKey}:{ComputeHash(originalReason)}",
            requestedSlots,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static ProcessCompletionIssue CreateSubprocessLaunchCoordinatorMissingOutcomeIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessSubprocessLaunchCoordinatorResult launch)
    {
        var childRunSummary = launch.ChildRunId is { } childRunId
            ? childRunId.Value.ToString("D")
            : "no child run";
        return new ProcessCompletionIssue(
            "process.adapter.subprocess_launch_missing_parent_outcome",
            $"Step '{assignment.StepKey}' launched mapped subprocess DefinitionKey '{launch.DefinitionKey}' with stage '{launch.Stage}' and {childRunSummary}, but the launch coordinator did not return a parent deferred outcome.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:subprocess-launch-missing-parent-outcome:{launch.DefinitionKey}:{childRunSummary}:{launch.Stage}",
            assignment.ProducedArtifactSlotIds.Count > 0 ? assignment.ProducedArtifactSlotIds : assignment.RequiredArtifactSlotIds,
            ProcessDiagnosticRetrySafety.Unknown,
            ProcessDiagnosticIdempotencyClassification.Unknown);
    }

    private static ProcessCompletionIssue CreateSubprocessLaunchDefinitionMissingIssue(
        ProcessRuntimeStepAssignment assignment)
    {
        return new ProcessCompletionIssue(
            "process.adapter.subprocess_launch_definition_missing",
            $"Step '{assignment.StepKey}' is configured as a mapped subprocess launch, but the runtime assignment does not contain a child process definition key.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:subprocess-launch-definition-missing",
            assignment.ProducedArtifactSlotIds.Count > 0 ? assignment.ProducedArtifactSlotIds : assignment.RequiredArtifactSlotIds,
            ProcessDiagnosticRetrySafety.Unknown,
            ProcessDiagnosticIdempotencyClassification.Unknown);
    }

    private static ProcessCompletionIssue CreateSubprocessLaunchCoordinatorUnavailableIssue(
        ProcessRuntimeStepAssignment assignment,
        string subprocessDefinitionKey)
    {
        return new ProcessCompletionIssue(
            "process.adapter.subprocess_launch_coordinator_unavailable",
            $"Step '{assignment.StepKey}' is mapped to subprocess DefinitionKey '{subprocessDefinitionKey}', but no subprocess launch coordinator is registered for this runtime.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:subprocess-launch-coordinator-unavailable:{subprocessDefinitionKey}",
            assignment.ProducedArtifactSlotIds.Count > 0 ? assignment.ProducedArtifactSlotIds : assignment.RequiredArtifactSlotIds,
            ProcessDiagnosticRetrySafety.Unknown,
            ProcessDiagnosticIdempotencyClassification.Unknown);
    }

    private static ProcessCompletionIssue CreateSubprocessLaunchNotHandledIssue(
        ProcessRuntimeStepAssignment assignment,
        string subprocessDefinitionKey)
    {
        return new ProcessCompletionIssue(
            "process.adapter.subprocess_launch_not_handled",
            $"Step '{assignment.StepKey}' is mapped to subprocess DefinitionKey '{subprocessDefinitionKey}', but the registered subprocess launch coordinator did not handle this assignment.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:subprocess-launch-not-handled:{subprocessDefinitionKey}",
            assignment.ProducedArtifactSlotIds.Count > 0 ? assignment.ProducedArtifactSlotIds : assignment.RequiredArtifactSlotIds,
            ProcessDiagnosticRetrySafety.Unknown,
            ProcessDiagnosticIdempotencyClassification.Unknown);
    }

    private static bool HasToolReceipt(
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        string toolName)
    {
        return toolReceipts?.Any(receipt =>
            string.Equals(receipt.ToolName, toolName, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private const string AgentRightsManagerRequestCode = "process.adapter.agent_rights_request";

    private static bool TryBuildAgentRightsManagerRequest(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        out string managerRequest)
    {
        managerRequest = string.Empty;
        var issueText = FirstNonEmpty(
            output.Reason,
            output.HumanReadableSummaryMarkdown ?? string.Empty,
            string.Join(" ", output.NextActions));
        if (!LooksLikeRightsOrToolBoundary(issueText))
        {
            return false;
        }

        var deniedToolOrRight = ResolveDeniedToolOrRight(issueText);
        var operations = NormalizeOperations(assignment.AllowedOperations);
        var operationsSummary = operations.Count == 0
            ? "none declared"
            : string.Join(", ", operations);
        var scope = string.IsNullOrWhiteSpace(assignment.OperationTargetScope)
            ? "unspecified"
            : assignment.OperationTargetScope.Trim();
        var executor = string.IsNullOrWhiteSpace(assignment.ExecutorDisplayName)
            ? assignment.ExecutorId
            : assignment.ExecutorDisplayName.Trim();
        var mutationSummary = AllowsProductMutation(operations, assignment.OperationTargetScope)
            ? "product mutation allowed"
            : "product mutation not allowed";

        managerRequest =
            $"Manager action required: step '{assignment.StepKey}' in run '{assignment.RunId}' is assigned to '{executor}' but reported a tool/right boundary problem for {deniedToolOrRight}. Grant the missing right/tool to this agent or reassign the step to an agent that already has it, then retry the step. Required operation contract: allowed operations [{operationsSummary}], target scope '{scope}', {mutationSummary}.";
        return true;
    }

    private static bool LooksLikeRightsOrToolBoundary(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ContainsAny(
            text,
            "PolicyDenied",
            "blocked by policy",
            "missing tool",
            "tool is not part of the composed capability set",
            "not authorized to use tool",
            "permission",
            "permissions",
            "right",
            "rights",
            "capability",
            "access denied",
            "workspace boundary",
            "outside the current run boundary",
            "approval path",
            "denied tool");
    }

    private static string ResolveDeniedToolOrRight(string text)
    {
        var quotedTool = Regex.Match(text, @"Tool '([^']+)'", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (quotedTool.Success)
        {
            return $"tool '{quotedTool.Groups[1].Value}'";
        }

        return "the denied or unavailable tool/right named in the blocker";
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

}
