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
using CanDoItAll.Processes.Contracts;
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

using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactService;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactFormatter;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactOutcomeParser;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessAcceptanceCriteriaGate
{
    internal static ProcessCompletionIssue? ValidateAcceptanceCriteriaCompletion(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed ||
            !assignment.LaunchVariables.TryGetValue(
                ProcessRuntimeLaunchVariables.AcceptanceCriteriaAcceptedBranchOutcomeKeys,
                out var rawBranchKeys))
        {
            return null;
        }

        if (!ProcessLaunchVariableStringList.TryParse(
                rawBranchKeys,
                out var acceptanceBranchKeys))
        {
            return CreateInvalidContractIssue(
                assignment,
                output.BranchOutcomeKey,
                "acceptance branch keys");
        }

        if (!acceptanceBranchKeys.Contains(
                output.BranchOutcomeKey.Trim(),
                StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!assignment.LaunchVariables.TryGetValue(
                ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix,
                out var rawMatrix))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(rawMatrix) ||
            !ProcessAcceptanceCriteriaMatrixJson.TryDeserialize(rawMatrix, out var matrix))
        {
            return CreateInvalidContractIssue(
                assignment,
                output.BranchOutcomeKey,
                "acceptance-criteria matrix");
        }

        if (matrix.RequiredCriteria.Count == 0)
        {
            return null;
        }

        var missingCriteria = matrix.RequiredCriteria
            .Where(criterion => !HasPassedCriterionEvidence(output, criterion.Id))
            .ToArray();
        if (missingCriteria.Length == 0)
        {
            return null;
        }

        var missingSummary = string.Join(
            "; ",
            missingCriteria.Select(criterion => string.IsNullOrWhiteSpace(criterion.Summary)
                ? criterion.Id
                : $"{criterion.Id} ({criterion.Summary})"));
        return new ProcessCompletionIssue(
            "process.adapter.acceptance_criteria_missing",
            $"Step '{assignment.StepKey}' selected acceptance branch '{output.BranchOutcomeKey}', but required acceptance criteria lack passed typed evidence entries with proof refs: {missingSummary}. Retry the same step with criterion-by-criterion evidence, or select a non-acceptance branch when criteria remain failed.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:acceptance-criteria-missing:{output.BranchOutcomeKey}:{missingSummary}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    internal static bool IsAcceptanceCriteriaBranch(
        ProcessRuntimeStepAssignment assignment,
        string branchOutcomeKey)
    {
        if (string.IsNullOrWhiteSpace(branchOutcomeKey) ||
            !assignment.LaunchVariables.TryGetValue(ProcessRuntimeLaunchVariables.AcceptanceCriteriaAcceptedBranchOutcomeKeys, out var rawBranchKeys))
        {
            return false;
        }

        return ProcessLaunchVariableStringList.TryParse(
                   rawBranchKeys,
                   out var branchKeys) &&
               branchKeys.Contains(
                   branchOutcomeKey.Trim(),
                   StringComparer.OrdinalIgnoreCase);
    }

    internal static bool HasPassedCriterionEvidence(
        ProcessStepOutcomeResult output,
        string criterionId)
        => !string.IsNullOrWhiteSpace(criterionId) &&
           (output.AcceptanceCriteriaEvidence ?? []).Any(evidence =>
               evidence is not null &&
               string.Equals(evidence.CriterionId?.Trim(), criterionId.Trim(), StringComparison.OrdinalIgnoreCase) &&
               evidence.Status == ProcessAcceptanceCriterionEvidenceStatus.Passed &&
               !string.IsNullOrWhiteSpace(evidence.Summary) &&
               (evidence.EvidenceRefs ?? []).Any(reference => !string.IsNullOrWhiteSpace(reference)));

    internal static bool TryGetFailedCriterionEvidence(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        out string defectSummary)
    {
        defectSummary = string.Empty;
        if (!assignment.LaunchVariables.TryGetValue(
                ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix,
                out var rawMatrix) ||
            !ProcessAcceptanceCriteriaMatrixJson.TryDeserialize(rawMatrix, out var matrix) ||
            matrix.RequiredCriteria.Count == 0)
        {
            return false;
        }

        var requiredCriterionIds = matrix.RequiredCriteria
            .Select(criterion => criterion.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var failedCriteria = (output.AcceptanceCriteriaEvidence ?? [])
            .Where(evidence => evidence is not null &&
                               evidence.Status == ProcessAcceptanceCriterionEvidenceStatus.Failed &&
                               !string.IsNullOrWhiteSpace(evidence.CriterionId) &&
                               requiredCriterionIds.Contains(evidence.CriterionId.Trim()) &&
                               !string.IsNullOrWhiteSpace(evidence.Summary) &&
                               (evidence.EvidenceRefs ?? []).Any(reference => !string.IsNullOrWhiteSpace(reference)))
            .Select(evidence => evidence.CriterionId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
        if (failedCriteria.Length == 0)
        {
            return false;
        }

        defectSummary = $"Typed failed acceptance-criterion evidence: {string.Join(", ", failedCriteria)}.";
        return true;
    }

    private static ProcessCompletionIssue CreateInvalidContractIssue(
        ProcessRuntimeStepAssignment assignment,
        string branchOutcomeKey,
        string invalidPart)
        => new(
            "process.adapter.acceptance_criteria_contract_invalid",
            $"Step '{assignment.StepKey}' selected acceptance branch '{branchOutcomeKey}', but its {invalidPart} is malformed or violates the typed criterion contract. The launch contract must be repaired before acceptance can continue.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:acceptance-criteria-contract-invalid:{branchOutcomeKey}:{invalidPart}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.UnsafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
}
