using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static AgentStructuredOutputContract ProcessStepOutcomeStructuredOutputContract { get; } =
        AgentStructuredOutputContracts.ProcessStepOutcomeResult;

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
            responseText,
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

    private static ProcessStepRunStatus MapProcessStepOutcomeStatus(ProcessStepOutcomeStatus status)
    {
        return status switch
        {
            ProcessStepOutcomeStatus.Completed => ProcessStepRunStatus.Completed,
            ProcessStepOutcomeStatus.Blocked => ProcessStepRunStatus.Blocked,
            ProcessStepOutcomeStatus.Failed => ProcessStepRunStatus.Failed,
            ProcessStepOutcomeStatus.WaitingApproval => ProcessStepRunStatus.WaitingApproval,
            ProcessStepOutcomeStatus.Refused => ProcessStepRunStatus.Refused,
            _ => ProcessStepRunStatus.Failed
        };
    }

    private static AgentOutputValidationResult ValidateProcessStepOutcomeContext(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
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
        ExecutionRunDetail detail,
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

        return errors.Count == 0
            ? AgentOutputValidationResult.Success()
            : AgentOutputValidationResult.Failure([.. errors]);
    }

    private static bool RequiresContextEvidenceReferences(
        DispatchCandidate candidate,
        ExecutionRunDetail detail)
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
