using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private const string EvidenceRefsJsonPropertyName = "evidenceRefs";
    private const string ArtifactRefsJsonPropertyName = "artifactRefs";

    private static string ResolveOutputInspectionText(string? responseText)
    {
        return TryReadProcessStepOutcome(responseText, out var outcome, out _)
            ? outcome.HumanReadableSummaryMarkdown ?? outcome.Reason
            : responseText ?? string.Empty;
    }

    private static bool TryReadProcessStepOutcome(
        string? responseText,
        out ProcessStepOutcomeResult outcome,
        out AgentOutputValidationResult validation)
    {
        var result = AgentOutputJson.DeserializeAndValidate(
            NormalizeProcessStepOutcomeJsonAliases(responseText),
            new ProcessStepOutcomeValidator());
        if (result.Succeeded && result.Output is not null)
        {
            outcome = result.Output;
            validation = result.Validation;
            return true;
        }

        outcome = default!;
        validation = result.Validation;
        return false;
    }

    private static string? NormalizeProcessStepOutcomeJsonAliases(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return responseText;
        }

        try
        {
            var root = JsonNode.Parse(responseText);
            if (root is not JsonObject output)
            {
                return responseText;
            }

            var artifactRefsPropertyName = FindJsonPropertyName(output, ArtifactRefsJsonPropertyName);
            if (artifactRefsPropertyName is null ||
                output[artifactRefsPropertyName] is not JsonArray artifactRefs ||
                artifactRefs.Count == 0)
            {
                return responseText;
            }

            var evidenceRefsPropertyName = FindJsonPropertyName(output, EvidenceRefsJsonPropertyName);
            if (evidenceRefsPropertyName is not null &&
                output[evidenceRefsPropertyName] is JsonArray evidenceRefs &&
                evidenceRefs.Count > 0)
            {
                return responseText;
            }

            if (evidenceRefsPropertyName is not null)
            {
                output.Remove(evidenceRefsPropertyName);
            }

            output[EvidenceRefsJsonPropertyName] = artifactRefs.DeepClone();
            return output.ToJsonString();
        }
        catch (JsonException)
        {
            return responseText;
        }
    }

    private static string? FindJsonPropertyName(
        JsonObject output,
        string propertyName)
    {
        foreach (var property in output)
        {
            if (string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Key;
            }
        }

        return null;
    }

    private static ProcessStepRunStatus MapProcessStepOutcomeStatus(ProcessStepOutcomeStatus status)
    {
        return ProcessDeclaredStepOutcomeRules.MapStatus(status);
    }

    private static AgentOutputValidationResult ValidateProcessStepOutcomeContext(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        ProcessStepOutcomeResult outcome,
        DeclaredStepOutcome declaredOutcome,
        string inspectionText)
    {
        return ValidateProcessStepOutcomeContextWithCarryForward(
            candidate,
            detail,
            outcome,
            declaredOutcome,
            inspectionText,
            CarriedImplementationProof.None);
    }

    private static AgentOutputValidationResult ValidateProcessStepOutcomeContextWithCarryForward(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        ProcessStepOutcomeResult outcome,
        DeclaredStepOutcome declaredOutcome,
        string inspectionText,
        CarriedImplementationProof carriedImplementationProof)
    {
        var errors = new List<AgentOutputValidationError>();
        var branchSelectionFailure = ResolveBranchOutcomeSelectionFailure(candidate, declaredOutcome);
        if (!string.IsNullOrWhiteSpace(branchSelectionFailure))
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = string.IsNullOrWhiteSpace(declaredOutcome.BranchOutcomeKey) &&
                       string.IsNullOrWhiteSpace(declaredOutcome.BranchOutcomeTitle)
                    ? "process.step_outcome.context.branch_required"
                    : "process.step_outcome.context.branch_invalid",
                Message = branchSelectionFailure,
                Path = "$.branchOutcomeKey"
            });
        }

        if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
            RequiresContextEvidenceReferences(candidate, detail) &&
            outcome.EvidenceRefs.Count == 0)
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "process.step_outcome.context.evidence_refs_required",
                Message = "Completed governed process outcomes must include evidence references when the step has proof expectations or produced artifacts.",
                Path = "$.evidenceRefs"
            });
        }

        if (declaredOutcome.Status == ProcessStepRunStatus.Completed)
        {
            AddContextGap(errors, "process.step_outcome.context.missing_upstream_artifact", ResolveMissingUpstreamArtifactInputSummary(candidate));
            AddContextGap(errors, "process.step_outcome.context.missing_upstream_artifact_inspection", ResolveMissingUpstreamArtifactInspectionSummary(candidate, detail));
            AddContextGap(errors, "process.step_outcome.context.missing_concrete_proof", ResolveMissingConcreteProofSummary(candidate, inspectionText));
            AddContextGap(errors, "process.step_outcome.context.incomplete_implementation", ResolveIncompleteImplementationSummary(candidate, inspectionText));
            AddContextGap(
                errors,
                "process.step_outcome.context.missing_implementation_proof",
                ResolveMissingConcreteImplementationProofSummaryWithCarryForward(
                    candidate,
                    detail,
                    carriedImplementationProof));
            AddContextGap(errors, "process.step_outcome.context.invalid_browser_proof", ResolveInvalidBrowserProofSummary(candidate, detail));
            AddContextGap(errors, "process.step_outcome.context.invalid_quality_validation_proof", ResolveInvalidQualityValidationProofSummary(candidate, detail, inspectionText));
            AddContextGap(errors, "process.step_outcome.context.missing_required_artifact", ResolveMissingRequiredArtifactSummary(candidate, detail, inspectionText));
            AddContextGap(errors, "process.step_outcome.context.downgraded_project_structure_requirement", ResolveDowngradedProjectStructureRequirementSummary(candidate, detail, inspectionText));
        }

        AddContextGap(
            errors,
            "process.step_outcome.context.dotnet_validation_repair_required",
            ResolveMissingDotNetValidationRepairBeforeNegativeOutcome(
                candidate,
                detail,
                declaredOutcome.Status,
                declaredOutcome.Reason,
                declaredOutcome.BranchOutcomeKey,
                declaredOutcome.BranchOutcomeTitle,
                inspectionText));

        return errors.Count == 0
            ? AgentOutputValidationResult.Success()
            : AgentOutputValidationResult.Failure([.. errors]);
    }

    private static string? ResolveMissingDotNetValidationRepairBeforeNegativeOutcome(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        ProcessStepRunStatus declaredStatus,
        string declaredReason,
        string branchOutcomeKey,
        string branchOutcomeTitle,
        string inspectionText)
    {
        if (!RequiresDotNetValidationRepairGuidance(candidate) ||
            !IsNegativeDotNetValidationDisposition(declaredStatus, branchOutcomeKey, branchOutcomeTitle))
        {
            return null;
        }

        var diagnosticText = CreateDotNetValidationDiagnosticText(detail, declaredReason, inspectionText);
        if (ContainsUnresolvedMsBuildTimeoutDiagnostic(diagnosticText) &&
            RequiresDotNetBuildServerRetryAfterLatestFailedValidationTool(detail))
        {
            return "This .NET validation step found an MSBuild client/server timeout after the latest successful governed validation script. Run workspace_pwsh_run_script again with a ManagedProcessArtifacts sideEffectManifest, execute dotnet build-server shutdown, then rerun restore/build/test with --disable-build-servers before declaring Blocked, Failed, or an Error branch.";
        }

        if (ContainsTransientDotNetBuildCancellationDiagnostic(diagnosticText))
        {
            return "This .NET validation step found MSBuild build-cancellation diagnostics such as MSB5021. Treat that as transient validation infrastructure, not a product warning. Rerun the governed workspace_pwsh_run_script harness with build-server shutdown, stable exit-code capture, and restore/build/test proof before declaring Blocked, Failed, or an Error branch.";
        }

        if (HasSuccessfulConcreteProductMutation(candidate, detail) ||
            !ContainsRepairableDotNetValidationDiagnostic(diagnosticText))
        {
            return null;
        }

        return "This .NET validation step found repairable project/package diagnostics, but no successful ProductMutation repair receipt was recorded. Inspect and repair the affected project file with workspace_pwsh_run_script using a ProductMutation sideEffectManifest, reread the changed file, and rerun restore/build/test before declaring Blocked, Failed, or an Error branch.";
    }

    private static bool IsNegativeDotNetValidationDisposition(
        ProcessStepRunStatus declaredStatus,
        string branchOutcomeKey,
        string branchOutcomeTitle)
    {
        if (declaredStatus != ProcessStepRunStatus.Completed)
        {
            return true;
        }

        var branchToken = NormalizeBranchOutcomeToken($"{branchOutcomeKey} {branchOutcomeTitle}");
        return ContainsAnyToken(branchToken, "error", "blocked", "failed", "failure", "repair");
    }

    private static string CreateDotNetValidationDiagnosticText(
        ProcessAutomationExecutionRunDetail detail,
        string declaredReason,
        string inspectionText)
    {
        return CollapsePromptWhitespace(string.Join(
            ' ',
            declaredReason,
            inspectionText,
            string.Join(
                ' ',
                detail.ToolReceipts.Select(receipt => string.Join(
                    ' ',
                    receipt.RequestSummary,
                    receipt.WorkingDirectory,
                    receipt.ExitSummary)))));
    }

    private static bool ContainsUnresolvedMsBuildTimeoutDiagnostic(string diagnosticText)
    {
        if (string.IsNullOrWhiteSpace(diagnosticText))
        {
            return false;
        }

        return ContainsAnyToken(
                   diagnosticText,
                   "timeoutexception",
                   "timedout",
                   "timed out",
                   "timeout",
                   "exit -532462766") &&
               ContainsAnyToken(
                   diagnosticText,
                   "msbuild",
                   "build-server",
                   "build server",
                   "named-pipe",
                   "named pipe",
                   "pipe connection",
                   "workspace_dotnet_restore",
                   "workspace_dotnet_build",
                   "workspace_dotnet_test",
                   "dotnet restore",
                   "dotnet build",
                   "dotnet test");
    }

    private static bool ContainsTransientDotNetBuildCancellationDiagnostic(string diagnosticText)
    {
        if (string.IsNullOrWhiteSpace(diagnosticText))
        {
            return false;
        }

        return ContainsAnyToken(
                   diagnosticText,
                   "msb5021",
                   "build was canceled",
                   "build was cancelled",
                   "because the build was canceled",
                   "because the build was cancelled",
                   "terminating the task executable",
                   "terminating task executable") &&
               ContainsAnyToken(
                   diagnosticText,
                   "dotnet build",
                   "msbuild",
                   "csc");
    }

    private static bool RequiresDotNetBuildServerRetryAfterLatestFailedValidationTool(
        ProcessAutomationExecutionRunDetail detail)
    {
        var latestFailedDotNetValidationReceipt = detail.ToolReceipts
            .Where(IsFailedDotNetValidationToolReceipt)
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
        var latestSuccessfulScriptReceipt = detail.ToolReceipts
            .Where(receipt =>
                string.Equals(NormalizeToolToken(receipt.ToolName), ToolContractCatalog.WorkspacePowerShellRunScript, StringComparison.Ordinal) &&
                !IsFailedToolReceipt(receipt))
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();

        if (latestFailedDotNetValidationReceipt is null)
        {
            return latestSuccessfulScriptReceipt is null;
        }

        return latestSuccessfulScriptReceipt is null ||
               IsReceiptLaterThan(latestFailedDotNetValidationReceipt, latestSuccessfulScriptReceipt);
    }

    private static bool IsFailedDotNetValidationToolReceipt(ProcessAutomationToolExecutionReceipt receipt)
    {
        var toolName = NormalizeToolToken(receipt.ToolName);
        return IsFailedToolReceipt(receipt) &&
               (string.Equals(toolName, ToolContractCatalog.WorkspaceDotNetRestore, StringComparison.Ordinal) ||
                string.Equals(toolName, ToolContractCatalog.WorkspaceDotNetBuild, StringComparison.Ordinal) ||
                string.Equals(toolName, ToolContractCatalog.WorkspaceDotNetTest, StringComparison.Ordinal));
    }

    private static bool IsReceiptLaterThan(
        ProcessAutomationToolExecutionReceipt candidate,
        ProcessAutomationToolExecutionReceipt baseline)
    {
        return candidate.CompletedAtUtc > baseline.CompletedAtUtc ||
               candidate.CompletedAtUtc == baseline.CompletedAtUtc &&
               candidate.StartedAtUtc > baseline.StartedAtUtc;
    }

    private static bool ContainsRepairableDotNetValidationDiagnostic(string diagnosticText)
    {
        if (string.IsNullOrWhiteSpace(diagnosticText))
        {
            return false;
        }

        return ContainsAnyToken(
                   diagnosticText,
                   "nu1202",
                   "netsdk",
                   "target-framework/package",
                   "target framework/package",
                   "target-framework",
                   "target framework",
                   "not compatible with net8.0",
                   "not compatible",
                   "package incompatibility") &&
               ContainsAnyToken(
                   diagnosticText,
                   "net8.0",
                   "net10.0",
                   "10.0.8",
                   "microsoft.aspnetcore.components.webassembly",
                   "framework",
                   "package");
    }

    private static bool RequiresContextEvidenceReferences(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
    {
        return detail.Artifacts.Count > 0 ||
               candidate.ExpectedArtifacts.Any(item => item.IsRequired) ||
               RequiresConcreteImplementationProof(candidate) ||
               RequiresConcreteBrowserProof(candidate);
    }

    private static void AddContextGap(
        List<AgentOutputValidationError> errors,
        string code,
        string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        errors.Add(new AgentOutputValidationError
        {
            Code = code,
            Message = message,
            Path = "$"
        });
    }

}
