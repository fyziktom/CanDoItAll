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
    private static ProcessCompletionIssue? ValidateProductMutationCompletion(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed ||
            !AllowsProductMutation(NormalizeOperations(assignment.AllowedOperations), assignment.OperationTargetScope))
        {
            return null;
        }

        if (output.EvidenceRefs.Count == 0 ||
            output.EvidenceRefs.All(string.IsNullOrWhiteSpace))
        {
            return new ProcessCompletionIssue(
                "process.adapter.product_output_evidence_missing",
                $"Step '{assignment.StepKey}' claimed completion for a product-mutating scope but returned no evidence references.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:evidence-missing",
                [],
                ProcessDiagnosticRetrySafety.SafeToRetry,
                ProcessDiagnosticIdempotencyClassification.Idempotent);
        }

        if (!TryResolveInspectableProductRoot(assignment.LaunchVariables, out var productRoot))
        {
            return null;
        }

        if (ValidateRequiredProductPaths(assignment, productRoot) is { } requiredPathIssue)
        {
            return requiredPathIssue;
        }

        if (ValidateRequiredProductFileContentChecks(assignment, output, productRoot) is { } requiredFileContentIssue)
        {
            return requiredFileContentIssue;
        }

        var inspection = InspectProductRoot(productRoot);
        if (inspection.HasProductFiles)
        {
            return null;
        }

        return new ProcessCompletionIssue(
            "process.adapter.product_output_missing",
            inspection.Summary.Length == 0
                ? $"Step '{assignment.StepKey}' claimed completion but the configured product output root '{productRoot}' contains no product files."
                : $"Step '{assignment.StepKey}' claimed completion but the configured product output root '{productRoot}' is not usable: {inspection.Summary}",
            productRoot,
            [],
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static ProcessCompletionIssue? ValidateRequiredProductPaths(
        ProcessRuntimeStepAssignment assignment,
        string productRoot)
    {
        var requiredPaths = ResolveProductCompletionRequiredPaths(assignment.LaunchVariables, assignment.StepKey);
        if (requiredPaths.Count == 0)
        {
            return null;
        }

        var invalidPaths = new List<string>();
        var missingPaths = new List<string>();
        foreach (var requiredPath in requiredPaths)
        {
            if (!TryResolveRequiredProductPath(productRoot, requiredPath, out var resolvedPath, out var invalidReason))
            {
                invalidPaths.Add($"{requiredPath} ({invalidReason})");
                continue;
            }

            if (!File.Exists(resolvedPath) &&
                !Directory.Exists(resolvedPath))
            {
                missingPaths.Add(resolvedPath);
            }
        }

        if (invalidPaths.Count > 0)
        {
            var invalidSummary = string.Join("; ", invalidPaths);
            return new ProcessCompletionIssue(
                "process.adapter.product_required_output_path_invalid",
                $"Step '{assignment.StepKey}' claimed completion but declared required product path(s) are invalid or outside the configured product root '{productRoot}': {invalidSummary}.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:product-required-path-invalid:{invalidSummary}",
                [],
                ProcessDiagnosticRetrySafety.UnsafeToRetry,
                ProcessDiagnosticIdempotencyClassification.Unknown);
        }

        if (missingPaths.Count == 0)
        {
            return null;
        }

        var missingSummary = string.Join("; ", missingPaths);
        return new ProcessCompletionIssue(
            "process.adapter.product_required_output_missing",
            $"Step '{assignment.StepKey}' claimed completion but required product output path(s) are missing under the configured product root '{productRoot}': {missingSummary}.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:product-required-path-missing:{missingSummary}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static ProcessCompletionIssue? ValidateRequiredProductFileContentChecks(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        string productRoot)
    {
        var resolution = ResolveProductCompletionRequiredFileContentChecks(assignment.LaunchVariables, assignment.StepKey);
        if (!string.IsNullOrWhiteSpace(resolution.InvalidReason))
        {
            return new ProcessCompletionIssue(
                "process.adapter.product_required_file_content_check_invalid",
                $"Step '{assignment.StepKey}' claimed completion but declared required product file content check(s) are invalid: {resolution.InvalidReason}.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:product-required-file-content-check-invalid:{resolution.InvalidReason}",
                [],
                ProcessDiagnosticRetrySafety.UnsafeToRetry,
                ProcessDiagnosticIdempotencyClassification.Unknown);
        }

        if (resolution.Checks.Count == 0)
        {
            return null;
        }

        var failures = new List<string>();
        foreach (var check in resolution.Checks)
        {
            if (check.EnforceBranchOutcomeKeys.Count > 0 &&
                !check.EnforceBranchOutcomeKeys.Contains(output.BranchOutcomeKey, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryResolveRequiredProductFileContentCheckPath(productRoot, check, out var resolvedPath, out var pathFailure, out var skippedMissingOptionalPath))
            {
                if (skippedMissingOptionalPath)
                {
                    continue;
                }

                failures.Add(pathFailure);
                continue;
            }

            string content;
            try
            {
                content = File.ReadAllText(resolvedPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException or NotSupportedException)
            {
                failures.Add($"{resolvedPath} could not be read: {exception.Message}");
                continue;
            }

            foreach (var requiredTextGroup in check.RequiredTextAnyGroups)
            {
                if (requiredTextGroup.Count == 0)
                {
                    failures.Add($"{resolvedPath} has an empty required text group.");
                    continue;
                }

                if (!requiredTextGroup.Any(requiredText => content.Contains(requiredText, StringComparison.OrdinalIgnoreCase)))
                {
                    failures.Add($"{resolvedPath} does not contain any expected text from [{string.Join(" | ", requiredTextGroup)}]");
                }
            }

            foreach (var forbiddenTextGroup in check.ForbiddenTextAnyGroups)
            {
                if (forbiddenTextGroup.Count == 0)
                {
                    failures.Add($"{resolvedPath} has an empty forbidden text group.");
                    continue;
                }

                var foundForbiddenText = forbiddenTextGroup
                    .Where(forbiddenText => content.Contains(forbiddenText, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (foundForbiddenText.Length > 0)
                {
                    failures.Add($"{resolvedPath} contains forbidden text [{string.Join(" | ", foundForbiddenText)}]");
                }
            }
        }

        if (failures.Count == 0)
        {
            return null;
        }

        var failureSummary = string.Join("; ", failures.Distinct(StringComparer.OrdinalIgnoreCase));
        return new ProcessCompletionIssue(
            "process.adapter.product_required_file_content_missing",
            $"Step '{assignment.StepKey}' claimed completion but required product file content/readback check(s) failed: {failureSummary}.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:product-required-file-content-missing:{failureSummary}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static bool TryResolveRequiredProductFileContentCheckPath(
        string productRoot,
        ProductCompletionRequiredFileContentCheck check,
        out string resolvedPath,
        out string failure,
        out bool skippedMissingOptionalPath)
    {
        resolvedPath = string.Empty;
        failure = string.Empty;
        skippedMissingOptionalPath = false;
        var invalidPaths = new List<string>();
        var missingPaths = new List<string>();
        foreach (var pathCandidate in check.PathCandidates)
        {
            if (!TryResolveRequiredProductPath(productRoot, pathCandidate, out var candidatePath, out var invalidReason))
            {
                invalidPaths.Add($"{pathCandidate} ({invalidReason})");
                continue;
            }

            if (!File.Exists(candidatePath))
            {
                missingPaths.Add(candidatePath);
                continue;
            }

            resolvedPath = candidatePath;
            return true;
        }

        if (invalidPaths.Count == check.PathCandidates.Count)
        {
            failure = $"all required content-check path candidates were invalid: {string.Join("; ", invalidPaths)}";
            return false;
        }

        if (!check.MustExist)
        {
            skippedMissingOptionalPath = true;
            return false;
        }

        failure = $"none of the required content-check path candidates existed: {string.Join("; ", missingPaths)}";
        return false;
    }

}
