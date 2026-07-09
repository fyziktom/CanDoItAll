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

namespace CanDoItAll.Modules.Processes;

internal sealed partial class AgentFrameworkProcessExecutionAdapter
{
    private static ProcessCompletionIssue? ValidateAcceptanceCriteriaCompletion(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed ||
            !IsAcceptanceCriteriaBranch(assignment, output.BranchOutcomeKey))
        {
            return null;
        }

        if (!assignment.LaunchVariables.TryGetValue(ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix, out var rawMatrix) ||
            !ProcessAcceptanceCriteriaMatrixJson.TryDeserialize(rawMatrix, out var matrix) ||
            matrix.RequiredCriteria.Count == 0)
        {
            return null;
        }

        var outcomeText = string.Join(
            " ",
            EnumerateOutcomeText(output).Where(value => !string.IsNullOrWhiteSpace(value)));
        var missingCriteria = matrix.RequiredCriteria
            .Where(criterion => !ContainsCriterionId(outcomeText, criterion.Id))
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
            $"Step '{assignment.StepKey}' selected acceptance branch '{output.BranchOutcomeKey}', but required acceptance criteria id(s) were not cited with proof: {missingSummary}. Retry the same step with criterion-by-criterion evidence, or select a non-acceptance branch when criteria remain failed.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:acceptance-criteria-missing:{output.BranchOutcomeKey}:{missingSummary}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static bool IsAcceptanceCriteriaBranch(
        ProcessRuntimeStepAssignment assignment,
        string branchOutcomeKey)
    {
        if (string.IsNullOrWhiteSpace(branchOutcomeKey) ||
            !assignment.LaunchVariables.TryGetValue(ProcessRuntimeLaunchVariables.AcceptanceCriteriaAcceptedBranchOutcomeKeys, out var rawBranchKeys))
        {
            return false;
        }

        return SplitLaunchVariableList(rawBranchKeys)
            .Contains(branchOutcomeKey.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static bool ContainsCriterionId(string text, string criterionId)
        => !string.IsNullOrWhiteSpace(text) &&
           !string.IsNullOrWhiteSpace(criterionId) &&
           text.Contains(criterionId.Trim(), StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> SplitLaunchVariableList(string value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split([';', ',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
}
