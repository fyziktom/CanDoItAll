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

using static CanDoItAll.Modules.Processes.ProcessCompletionRuleParser;
using static CanDoItAll.Modules.Processes.ProcessProductCompletionRuleParser;
using static CanDoItAll.Modules.Processes.ProcessProductRootResolver;
using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionMetadataBuilder;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactService;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactFormatter;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactOutcomeParser;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProductFileContentCheckEvaluation(
    IReadOnlyList<string> DefectFailures,
    IReadOnlyList<string> InspectionFailures);

internal sealed class ProcessProductCompletionPathGate(ProcessProductFilesystemInspector filesystemInspector)
{
    private const int MaximumPublicFindingCount = 16;

    internal ProcessCompletionIssue? ValidateProductMutationFilesystemState(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed ||
            !AllowsProductMutation(NormalizeOperations(assignment.AllowedOperations), assignment.OperationTargetScope))
        {
            return null;
        }

        var rootResolution = ResolveInspectableProductRoot(assignment.LaunchVariables);
        if (rootResolution.Kind != ProcessProductRootResolutionKind.Resolved)
        {
            return CreateProductRootInspectionIssue(assignment, rootResolution.InvalidReason);
        }

        var productRoot = rootResolution.ProductRoot;
        var inspection = filesystemInspector.InspectProductRoot(productRoot);
        if (inspection.HasProductFiles)
        {
            return null;
        }

        if (string.Equals(
                inspection.Summary,
                "the product root could not be inspected safely",
                StringComparison.Ordinal))
        {
            return CreateProductRootInspectionIssue(assignment, inspection.Summary);
        }

        return new ProcessCompletionIssue(
            "process.adapter.product_output_missing",
            inspection.Summary.Length == 0
                ? $"Step '{assignment.StepKey}' claimed completion but the configured product output root contains no product files."
                : $"Step '{assignment.StepKey}' claimed completion but the configured product output root is not usable: {inspection.Summary}.",
            ComputeHash($"{productRoot}:{inspection.Summary}"),
            [],
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    internal ProcessCompletionIssue? ValidateRequiredProductPaths(
        ProcessRuntimeStepAssignment assignment,
        string productRoot)
    {
        var requiredPaths = ResolveProductCompletionRequiredPaths(assignment.LaunchVariables, assignment.StepKey);
        if (requiredPaths.Count == 0)
        {
            return null;
        }

        var invalidPathDetails = new List<string>();
        var missingPathDetails = new List<string>();
        var unavailablePathDetails = new List<string>();
        for (var pathIndex = 0; pathIndex < requiredPaths.Count; pathIndex++)
        {
            var requiredPath = requiredPaths[pathIndex];
            if (!TryResolveRequiredProductPath(productRoot, requiredPath, out var resolvedPath, out var invalidReason))
            {
                invalidPathDetails.Add($"required-path[{pathIndex}]:{ComputeHash(requiredPath + ":" + invalidReason)}");
                continue;
            }

            var inspection = filesystemInspector.InspectPath(productRoot, resolvedPath);
            if (inspection.State == ProcessProductPathState.Missing)
            {
                missingPathDetails.Add($"required-path[{pathIndex}]:{ComputeHash(resolvedPath)}");
            }
            else if (inspection.State == ProcessProductPathState.Unavailable)
            {
                unavailablePathDetails.Add($"required-path[{pathIndex}]:inspection-unavailable");
            }
        }

        if (invalidPathDetails.Count > 0)
        {
            return new ProcessCompletionIssue(
                "process.adapter.product_required_output_path_invalid",
                $"Step '{assignment.StepKey}' claimed completion but {invalidPathDetails.Count} declared required product path(s) are invalid or outside the configured product root. Repair the typed path contract and retry.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:product-required-path-invalid:{ComputeHash(string.Join(';', invalidPathDetails))}",
                [],
                ProcessDiagnosticRetrySafety.UnsafeToRetry,
                ProcessDiagnosticIdempotencyClassification.Unknown);
        }


        if (unavailablePathDetails.Count > 0)
        {
            return new ProcessCompletionIssue(
                "process.adapter.product_required_output_unavailable",
                $"Step '{assignment.StepKey}' claimed completion but {unavailablePathDetails.Count} required product output path(s) could not be inspected safely. Repair or rebind the product-root authority and retry.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:product-required-path-unavailable:{ComputeHash(string.Join(';', unavailablePathDetails))}",
                assignment.ProducedArtifactSlotIds,
                ProcessDiagnosticRetrySafety.UnsafeToRetry,
                ProcessDiagnosticIdempotencyClassification.Unknown);
        }

        if (missingPathDetails.Count == 0)
        {
            return null;
        }

        return new ProcessCompletionIssue(
            "process.adapter.product_required_output_missing",
            $"Step '{assignment.StepKey}' claimed completion but {missingPathDetails.Count} required product output path(s) are missing under the configured product root. Create the declared outputs and retry.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:product-required-path-missing:{ComputeHash(string.Join(';', missingPathDetails))}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    internal ProcessCompletionIssue? ValidateRequiredProductFileContentChecks(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        string productRoot)
    {
        var resolution = ResolveProductCompletionRequiredFileContentChecks(assignment.LaunchVariables, assignment.StepKey);
        if (!string.IsNullOrWhiteSpace(resolution.InvalidReason))
        {
            return new ProcessCompletionIssue(
                "process.adapter.product_required_file_content_check_invalid",
                $"Step '{assignment.StepKey}' claimed completion but the declared required product file content check contract is invalid. Repair the typed check contract and retry.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:product-required-file-content-check-invalid:{ComputeHash(resolution.InvalidReason)}",
                [],
                ProcessDiagnosticRetrySafety.UnsafeToRetry,
                ProcessDiagnosticIdempotencyClassification.Unknown);
        }

        if (resolution.Checks.Count == 0)
        {
            return null;
        }

        var evaluation = EvaluateProductFileContentCheckFailures(
            productRoot,
            resolution.Checks,
            check => check.EnforceBranchOutcomeKeys.Count == 0 ||
                     check.EnforceBranchOutcomeKeys.Contains(output.BranchOutcomeKey, StringComparer.OrdinalIgnoreCase));

        if (evaluation.InspectionFailures.Count > 0)
        {
            var inspectionSummary = SummarizeFindings(evaluation.InspectionFailures);
            return new ProcessCompletionIssue(
                "process.adapter.product_required_file_content_check_unavailable",
                $"Step '{assignment.StepKey}' claimed completion but {evaluation.InspectionFailures.Count} required product file content/readback check(s) could not be inspected. This is an evidence-access or environment boundary, not verified product defect evidence. Finding identities: {inspectionSummary}.",
                $"{assignment.RunId}:{assignment.StepInstanceId}:product-required-file-content-check-unavailable:{ComputeHash(string.Join(';', evaluation.InspectionFailures))}",
                assignment.ProducedArtifactSlotIds,
                ProcessDiagnosticRetrySafety.UnsafeToRetry,
                ProcessDiagnosticIdempotencyClassification.Unknown);
        }

        if (evaluation.DefectFailures.Count == 0)
        {
            return null;
        }

        var failureSummary = SummarizeFindings(evaluation.DefectFailures);
        return new ProcessCompletionIssue(
            "process.adapter.product_required_file_content_missing",
            $"Step '{assignment.StepKey}' claimed completion but {evaluation.DefectFailures.Count} required product file content/readback check(s) failed. Finding identities: {failureSummary}.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:product-required-file-content-missing:{ComputeHash(string.Join(';', evaluation.DefectFailures))}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    internal ProcessCompletionIssue? ValidateRequiredProductFilesystemState(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed)
        {
            return null;
        }

        var requiredPaths = ResolveProductCompletionRequiredPaths(assignment.LaunchVariables, assignment.StepKey);
        var requiredContentChecks = ResolveProductCompletionRequiredFileContentChecks(
            assignment.LaunchVariables,
            assignment.StepKey);
        if (requiredPaths.Count == 0 &&
            requiredContentChecks.Checks.Count == 0 &&
            string.IsNullOrWhiteSpace(requiredContentChecks.InvalidReason))
        {
            return null;
        }

        var rootResolution = ResolveInspectableProductRoot(assignment.LaunchVariables);
        if (rootResolution.Kind != ProcessProductRootResolutionKind.Resolved)
        {
            return CreateRequiredProductRootInspectionIssue(assignment, rootResolution.InvalidReason);
        }

        var productRoot = rootResolution.ProductRoot;

        if (ValidateRequiredProductPaths(assignment, productRoot) is { } requiredPathIssue)
        {
            return requiredPathIssue;
        }

        return ValidateRequiredProductFileContentChecks(assignment, output, productRoot);
    }

    internal bool HasProductFileContentDefectEvidence(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        out string defectSummary)
    {
        defectSummary = string.Empty;
        if (output.Status != ProcessStepOutcomeStatus.Completed)
        {
            return false;
        }

        var rootResolution = ResolveInspectableProductRoot(assignment.LaunchVariables);
        if (rootResolution.Kind != ProcessProductRootResolutionKind.Resolved)
        {
            return false;
        }

        var productRoot = rootResolution.ProductRoot;

        var resolution = ResolveProductCompletionRequiredFileContentChecks(assignment.LaunchVariables, assignment.StepKey);
        if (!string.IsNullOrWhiteSpace(resolution.InvalidReason) ||
            resolution.Checks.Count == 0)
        {
            return false;
        }

        var evaluation = EvaluateProductFileContentCheckFailures(
            productRoot,
            resolution.Checks,
            check => check.EvidenceBranchOutcomeKeys.Count > 0 &&
                     check.EvidenceBranchOutcomeKeys.Contains(output.BranchOutcomeKey, StringComparer.OrdinalIgnoreCase));
        if (evaluation.DefectFailures.Count == 0)
        {
            return false;
        }

        defectSummary =
            $"{evaluation.DefectFailures.Count} configured product content check(s) failed ({SummarizeFindings(evaluation.DefectFailures)})";
        return true;
    }

    internal ProductFileContentCheckEvaluation EvaluateProductFileContentCheckFailures(
        string productRoot,
        IReadOnlyList<ProductCompletionRequiredFileContentCheck> checks,
        Func<ProductCompletionRequiredFileContentCheck, bool> shouldEvaluate)
    {
        var defectFailures = new List<string>();
        var inspectionFailures = new List<string>();
        for (var checkIndex = 0; checkIndex < checks.Count; checkIndex++)
        {
            var check = checks[checkIndex];
            if (!shouldEvaluate(check))
            {
                continue;
            }

            var invalidPaths = new List<string>();
            var missingPaths = new List<string>();
            var existingPaths = new List<(string Path, string Label)>();
            for (var candidateIndex = 0; candidateIndex < check.PathCandidates.Count; candidateIndex++)
            {
                var pathCandidate = check.PathCandidates[candidateIndex];
                var candidateLabel = $"check[{checkIndex}].path[{candidateIndex}]";
                if (!TryResolveRequiredProductPath(
                        productRoot,
                        pathCandidate,
                        out var candidatePath,
                        out var invalidReason))
                {
                    invalidPaths.Add($"{candidateLabel}:invalid:{ComputeHash(pathCandidate + ":" + invalidReason)}");
                    continue;
                }

                var pathInspection = filesystemInspector.InspectPath(productRoot, candidatePath);
                if (pathInspection.State is ProcessProductPathState.Missing or ProcessProductPathState.Directory)
                {
                    missingPaths.Add($"{candidateLabel}:missing");
                    continue;
                }

                if (pathInspection.State == ProcessProductPathState.Unavailable)
                {
                    invalidPaths.Add($"{candidateLabel}:inspection-unavailable");
                    continue;
                }

                existingPaths.Add((candidatePath, candidateLabel));
            }

            if (invalidPaths.Count > 0)
            {
                inspectionFailures.Add(
                    $"check[{checkIndex}]:invalid-path-contract:{ComputeHash(string.Join(';', invalidPaths))}");
                continue;
            }

            if (existingPaths.Count == 0)
            {
                if (check.MustExist)
                {
                    defectFailures.Add(
                        $"check[{checkIndex}]:required-path-missing:{ComputeHash(string.Join(';', missingPaths))}");
                }

                continue;
            }

            var candidateDefects = new List<string>();
            var candidateInspectionFailures = new List<string>();
            var satisfied = false;
            foreach (var (resolvedPath, candidateLabel) in existingPaths)
            {
                var readResult = filesystemInspector.ReadText(productRoot, resolvedPath);
                if (!readResult.Succeeded)
                {
                    candidateInspectionFailures.Add(
                        $"{candidateLabel}:read-unavailable");
                    continue;
                }

                var content = readResult.Content;

                var resolvedPathDefects = new List<string>();
                for (var groupIndex = 0; groupIndex < check.RequiredTextAnyGroups.Count; groupIndex++)
                {
                    var requiredTextGroup = check.RequiredTextAnyGroups[groupIndex];
                    if (requiredTextGroup.Count == 0)
                    {
                        resolvedPathDefects.Add($"{candidateLabel}:required-group[{groupIndex}]-empty");
                        continue;
                    }

                    if (!requiredTextGroup.Any(requiredText => content.Contains(requiredText, StringComparison.OrdinalIgnoreCase)))
                    {
                        resolvedPathDefects.Add(
                            $"{candidateLabel}:required-group[{groupIndex}]-missing:{ComputeHash(string.Join('\n', requiredTextGroup))}");
                    }
                }

                for (var groupIndex = 0; groupIndex < check.ForbiddenTextAnyGroups.Count; groupIndex++)
                {
                    var forbiddenTextGroup = check.ForbiddenTextAnyGroups[groupIndex];
                    if (forbiddenTextGroup.Count == 0)
                    {
                        resolvedPathDefects.Add($"{candidateLabel}:forbidden-group[{groupIndex}]-empty");
                        continue;
                    }

                    var foundForbiddenText = forbiddenTextGroup
                        .Where(forbiddenText => content.Contains(forbiddenText, StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                    if (foundForbiddenText.Length > 0)
                    {
                        resolvedPathDefects.Add(
                            $"{candidateLabel}:forbidden-group[{groupIndex}]-present:{ComputeHash(string.Join('\n', foundForbiddenText))}");
                    }
                }

                if (resolvedPathDefects.Count == 0)
                {
                    satisfied = true;
                    break;
                }

                candidateDefects.AddRange(resolvedPathDefects);
            }

            if (satisfied)
            {
                continue;
            }

            defectFailures.AddRange(candidateDefects);
            inspectionFailures.AddRange(candidateInspectionFailures);
        }

        return new ProductFileContentCheckEvaluation(defectFailures, inspectionFailures);
    }

    private static ProcessCompletionIssue CreateProductRootInspectionIssue(
        ProcessRuntimeStepAssignment assignment,
        string reason)
    {
        return new ProcessCompletionIssue(
            "process.adapter.product_output_inspection_unavailable",
            $"Step '{assignment.StepKey}' claimed completion but the configured product output root could not be inspected safely. Repair or rebind the product-root authority and retry.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:product-output-inspection-unavailable:{ComputeHash(reason)}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.UnsafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Unknown);
    }

    private static ProcessCompletionIssue CreateRequiredProductRootInspectionIssue(
        ProcessRuntimeStepAssignment assignment,
        string reason)
    {
        return new ProcessCompletionIssue(
            "process.adapter.product_required_output_unavailable",
            $"Step '{assignment.StepKey}' claimed completion but required product output checks could not inspect the configured product root. Repair or rebind the product-root authority and retry.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:product-required-output-unavailable:{ComputeHash(reason)}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.UnsafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Unknown);
    }

    private static string SummarizeFindings(IReadOnlyList<string> findings)
    {
        var distinct = findings
            .Where(finding => !string.IsNullOrWhiteSpace(finding))
            .Distinct(StringComparer.Ordinal)
            .Take(MaximumPublicFindingCount)
            .ToArray();
        return distinct.Length == 0
            ? "not-reported"
            : string.Join(", ", distinct);
    }

}
