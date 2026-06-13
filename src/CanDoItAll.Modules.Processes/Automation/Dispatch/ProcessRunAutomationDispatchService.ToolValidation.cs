using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static IReadOnlyList<ProcessAutomationToolExecutionReceipt> ResolveUnresolvedCriticalToolFailures(ProcessAutomationExecutionRunDetail detail)
    {
        return ProcessCriticalToolFailureRules.ResolveUnresolvedCriticalToolFailures(
            detail,
            NonCriticalWorkspaceProcessToolNames,
            ShouldIgnoreSupersededCriticalToolFailure);
    }

    private static IReadOnlyList<ProcessAutomationToolExecutionReceipt> ResolveUnresolvedCriticalToolFailures(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
    {
        return ResolveUnresolvedCriticalToolFailures(detail)
            .Where(receipt => !ShouldIgnoreStackInapplicableCriticalToolFailure(candidate, receipt))
            .ToList();
    }

    private static bool ShouldIgnoreStackInapplicableCriticalToolFailure(
        DispatchCandidate candidate,
        ProcessAutomationToolExecutionReceipt receipt)
    {
        return ProcessCriticalToolFailureRules.ShouldIgnoreStackInapplicableCriticalToolFailure(
            new ProcessCriticalToolFailureStackContext(
                receipt,
                ResolveRequiredToolNames(candidate),
                ImplementationContractMentionsDotNet(candidate),
                ImplementationContractMentionsJavaScript(candidate),
                ImplementationContractNegatesDotNet(candidate)));
    }

    private static IReadOnlyList<string> ResolveMissingRequiredToolExecutions(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
    {
        return ResolveMissingRequiredToolExecutionsWithCarryForward(candidate, detail, []);
    }

    private static IReadOnlyList<string> ResolveMissingRequiredToolExecutionsWithCarryForward(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        IEnumerable<string> successfulToolNamesFromPriorAttempts)
    {
        return ResolveMissingRequiredToolExecutionsWithCarriedImplementationProof(
            candidate,
            detail,
            successfulToolNamesFromPriorAttempts,
            CarriedImplementationProof.None);
    }

    private static IReadOnlyList<string> ResolveMissingRequiredToolExecutionsWithCarriedImplementationProof(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        IEnumerable<string> successfulToolNamesFromPriorAttempts,
        CarriedImplementationProof carriedImplementationProof)
    {
        var declaredRequiredToolNames = ResolveRequiredToolNames(candidate);
        var metadataRequiredToolNames = ResolveMetadataRequiredToolNames(candidate, detail);
        var requiredToolNames = declaredRequiredToolNames
            .Concat(metadataRequiredToolNames)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var decision = ProcessRequiredToolValidationRules.ResolveMissingRequiredTools(
            new ProcessRequiredToolValidationRequest(
                declaredRequiredToolNames,
                metadataRequiredToolNames,
                ResolveSuccessfulToolNames(detail),
                successfulToolNamesFromPriorAttempts,
                ResolveProcessMockSatisfiedToolNames(candidate, detail, requiredToolNames),
                RequiresConcreteImplementationProof(candidate),
                RequiresConcreteBrowserProof(candidate),
                CanSatisfyMissingDotnetNewWithValidatedExistingScaffold(detail),
                CanSatisfyImplementationProofToolsWithCarriedProof(candidate, detail, carriedImplementationProof),
                CanSatisfyImplementationArtifactWriteWithRecordedArtifacts(candidate, detail),
                CreateRequiredToolValidationPolicy()));
        return decision.MissingToolNames;
    }

    private static IReadOnlyList<string> ResolveMetadataRequiredToolNames(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
    {
        if (!ResolveProcessBrowserToolsAllowed(detail.Run) ||
            !RequiresGovernedStepOutcome(candidate.StepRun) ||
            !RequiresConcreteBrowserProof(candidate) ||
            IsDotNetSolutionSetupScaffoldMutationStep(candidate))
        {
            return [];
        }

        return ImplicitBrowserProofToolNames;
    }

    private static bool CanSatisfyMissingDotnetNewWithValidatedExistingScaffold(ProcessAutomationExecutionRunDetail detail)
    {
        var successfulReceipts = ProcessAutomationReceiptObservationHelper.ResolveSuccessfulReceipts(detail);
        if (successfulReceipts.Count == 0)
        {
            return false;
        }

        var inspectedScaffoldFile = successfulReceipts.Any(IsDotnetScaffoldInspectionReceipt);
        if (!inspectedScaffoldFile)
        {
            return false;
        }

        return successfulReceipts.Any(receipt =>
        {
            var toolName = NormalizeToolToken(receipt.ToolName);
            return toolName is
                ToolContractCatalog.WorkspaceDotNetBuild or
                ToolContractCatalog.WorkspaceDotNetTest or
                ToolContractCatalog.WorkspaceDotNetRun;
        });
    }

    private static bool IsDotnetScaffoldInspectionReceipt(ProcessAutomationToolExecutionReceipt receipt)
    {
        var toolName = NormalizeToolToken(receipt.ToolName);
        if (toolName is not "workspace_stat_path" and not "workspace_read_file")
        {
            return false;
        }

        var inspectedText = string.Join(
            " ",
            receipt.RequestSummary,
            receipt.WorkingDirectory,
            receipt.ExitSummary);
        return ProcessImplementationStackRules.ContainsProjectFileSignal(inspectedText);
    }

    private static bool CanSatisfyImplementationProofToolsWithCarriedProof(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        CarriedImplementationProof carriedImplementationProof)
    {
        return RequiresConcreteImplementationProof(candidate) &&
               carriedImplementationProof.HasConcreteImplementationProof &&
               !HasSuccessfulConcreteProductMutation(candidate, detail);
    }

    private static bool CanSatisfyImplementationArtifactWriteWithRecordedArtifacts(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
    {
        return RequiresConcreteImplementationProof(candidate) &&
               !HasSuccessfulConcreteProductMutation(candidate, detail) &&
               candidate.ExpectedArtifacts
                   .Where(expectedArtifact => expectedArtifact.IsRequired &&
                                              expectedArtifact.ArtifactKind is not ProcessArtifactKind.Decision and not ProcessArtifactKind.DecisionRecord)
                   .All(expectedArtifact => HasRecordedOrExecutionArtifactForExpectedArtifact(
                       candidate,
                       detail,
                       expectedArtifact));
    }

    private static IReadOnlyList<string> ResolveProcessMockSatisfiedToolNames(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        IReadOnlyCollection<string> requiredToolNames)
    {
        var projections = ResolveProcessMockArtifactProjections(detail.Run.SerializedSessionStateJson);
        if (projections.Count == 0 ||
            !projections.Any(projection => ProcessMockProjectionMatchesRequiredArtifact(candidate, projection)))
        {
            return [];
        }

        var satisfiedToolNames = new List<string>();
        if (requiredToolNames.Contains("workspace_write_file", StringComparer.Ordinal))
        {
            satisfiedToolNames.Add("workspace_write_file");
        }

        if (RequiresGovernedInspection(candidate.StepRun))
        {
            satisfiedToolNames.AddRange(requiredToolNames
                .Where(toolName => GovernedInspectionToolNames.Contains(toolName, StringComparer.Ordinal)));
        }

        satisfiedToolNames.AddRange(requiredToolNames
            .Where(IsProjectStructureToolName));
        satisfiedToolNames.AddRange(requiredToolNames
            .Where(IsBrowserToolName));

        var hasProcessMockImplementationProof = projections.Any(projection =>
            CanSatisfyConcreteImplementationProofWithProcessMock(candidate, projection));
        if (hasProcessMockImplementationProof)
        {
            satisfiedToolNames.AddRange(requiredToolNames
                .Where(IsProcessMockImplementationProofToolName));
        }

        return satisfiedToolNames
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsProjectStructureToolName(string toolName)
    {
        return toolName.StartsWith("project_structure_", StringComparison.Ordinal);
    }

    private static bool IsBrowserToolName(string toolName)
    {
        return toolName.StartsWith("browser_", StringComparison.Ordinal);
    }

    private static bool IsProcessMockImplementationProofToolName(string toolName)
    {
        return ImplementationProofToolNames.Contains(toolName, StringComparer.Ordinal) ||
               ConcreteProductMutationToolNames.Contains(toolName, StringComparer.Ordinal) ||
               ProcessImplementationReceiptTimeline.IsImplementationBootstrapToolName(toolName) ||
               IsImplementationValidationToolName(toolName);
    }

    private static bool ShouldCarryForwardSuccessfulToolName(DispatchCandidate candidate, string normalizedToolName)
    {
        return ProcessRequiredToolValidationRules.ShouldCarryForwardSuccessfulToolName(
            CreateRequiredToolValidationPolicy(),
            RequiresConcreteImplementationProof(candidate),
            RequiresConcreteBrowserProof(candidate),
            normalizedToolName);
    }

    private static bool IsCurrentAttemptOnlyImplementationToolName(string normalizedToolName)
    {
        return ProcessRequiredToolValidationRules.IsCurrentAttemptOnlyImplementationToolName(
            CreateRequiredToolValidationPolicy(),
            normalizedToolName);
    }

    private static bool HasUnrecoverableMissingRequiredTool(IReadOnlyList<string> missingRequiredTools)
    {
        return ProcessRequiredToolValidationRules.HasUnrecoverableMissingRequiredTool(
            CreateRequiredToolValidationPolicy(),
            missingRequiredTools);
    }

    private static ProcessRequiredToolValidationPolicy CreateRequiredToolValidationPolicy()
    {
        return new ProcessRequiredToolValidationPolicy(
            ImplementationProofToolNames,
            ConcreteProductMutationToolNames,
            CurrentAttemptOnlyImplementationProofToolNames,
            CurrentAttemptOnlyBrowserProofToolNames,
            IsImplementationValidationToolName);
    }

    private static ProcessStepRunStatus ResolveCompletionStatusWithCarryForward(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        IEnumerable<string> successfulToolNamesFromPriorAttempts)
    {
        return ResolveCompletionStatusWithCarryForward(
            candidate,
            detail,
            successfulToolNamesFromPriorAttempts,
            detail.Run.ResultSummary);
    }

    private static ProcessStepRunStatus ResolveCompletionStatusWithCarryForward(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        IEnumerable<string> successfulToolNamesFromPriorAttempts,
        string? responseText)
    {
        return ResolveCompletionStatusWithCarryForward(
            candidate,
            detail,
            successfulToolNamesFromPriorAttempts,
            responseText,
            CarriedImplementationProof.None);
    }

    private static ProcessStepRunStatus ResolveCompletionStatusWithCarryForward(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        IEnumerable<string> successfulToolNamesFromPriorAttempts,
        string? responseText,
        CarriedImplementationProof carriedImplementationProof)
    {
        var run = detail.Run;
        var missingRequiredTools = ResolveMissingRequiredToolExecutionsWithCarriedImplementationProof(
            candidate,
            detail,
            successfulToolNamesFromPriorAttempts,
                carriedImplementationProof);
        if (ProcessCompletionDecisionRules.TryResolveRunStateDecision(
                new ProcessCompletionDecisionInput(
                    run.State,
                    run.Outcome,
                    run.PendingApprovals.Count,
                    candidate.StepRun.Status),
                out var runStateDecision))
        {
            return runStateDecision.Status;
        }

        var unresolvedCriticalToolFailures = ResolveUnresolvedCriticalToolFailures(candidate, detail);
        var hasDeclaredOutcome = TryResolveDeclaredStepOutcome(candidate, responseText, out var declaredOutcome, out var processOutcome);
        if (hasDeclaredOutcome && declaredOutcome.Status != ProcessStepRunStatus.Completed)
        {
            var contextValidation = ValidateProcessStepOutcomeContextWithCarryForward(
                candidate,
                detail,
                processOutcome,
                declaredOutcome,
                ResolveOutputInspectionText(responseText),
                carriedImplementationProof);
            if (contextValidation.IsValid &&
                TryResolveRepairBranchCompletionFromBlockedOutcome(
                    candidate,
                    detail,
                    declaredOutcome,
                    responseText,
                    missingRequiredTools,
                    carriedImplementationProof,
                    out _))
            {
                return ProcessStepRunStatus.Completed;
            }

            if (contextValidation.IsValid &&
                TryResolveTerminalEscalationCompletionFromBlockedOutcome(
                    candidate,
                    detail,
                    declaredOutcome,
                    responseText,
                    missingRequiredTools,
                    out _))
            {
                return ProcessStepRunStatus.Completed;
            }

            if (contextValidation.IsValid &&
                DeclaredBlockedOutcomeClaimsRequiredToolFailureWithoutReceipt(
                    declaredOutcome,
                    responseText,
                    missingRequiredTools,
                    detail))
            {
                return ProcessStepRunStatus.Failed;
            }

            if (contextValidation.IsValid &&
                (unresolvedCriticalToolFailures.Count > 0 || missingRequiredTools.Count == 0))
            {
                return declaredOutcome.Status;
            }

            if (!contextValidation.IsValid)
            {
                if (CanCompleteExplicitDispositionOutcomeWithContextValidation(
                        candidate,
                        declaredOutcome,
                        contextValidation,
                        responseText,
                        missingRequiredTools))
                {
                    return ProcessStepRunStatus.Completed;
                }

                if (TryRecoverExplicitDispositionBranchSelection(
                        candidate,
                        declaredOutcome,
                        contextValidation,
                        responseText,
                        out _))
                {
                    return ProcessStepRunStatus.Completed;
                }

                if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                    HasUnrecoverableMissingRequiredTool(missingRequiredTools))
                {
                    return ProcessStepRunStatus.Failed;
                }

                return contextValidation.Errors.Any(error =>
                    error.Code is "process.step_outcome.context.branch_required" or "process.step_outcome.context.branch_invalid")
                    ? ProcessStepRunStatus.Failed
                    : declaredOutcome.Status == ProcessStepRunStatus.Completed
                        ? ProcessStepRunStatus.Blocked
                        : ProcessStepRunStatus.Failed;
            }

            if (declaredOutcome.Status != ProcessStepRunStatus.Completed)
            {
                return declaredOutcome.Status;
            }
        }

        if (unresolvedCriticalToolFailures.Count > 0 &&
            (!hasDeclaredOutcome ||
             !CanCompleteExplicitDispositionOutcomeWithCriticalToolFailures(
                 candidate,
                 detail,
                 declaredOutcome,
                 processOutcome,
                 responseText,
                 missingRequiredTools,
                 carriedImplementationProof)))
        {
            return ProcessStepRunStatus.Failed;
        }

        if (TryResolveRecoverableProviderFailure(detail, responseText, out _))
        {
            return ProcessStepRunStatus.Failed;
        }

        var inspectionText = ResolveOutputInspectionText(responseText);
        var missingUpstreamArtifactInputSummary = ResolveMissingUpstreamArtifactInputSummary(candidate);
        var missingConcreteProofSummary = ResolveMissingConcreteProofSummary(candidate, inspectionText);
        var incompleteImplementationSummary = ResolveIncompleteImplementationSummary(candidate, inspectionText);
        var missingConcreteImplementationProofSummary = ResolveMissingConcreteImplementationProofSummaryWithCarryForward(
            candidate,
            detail,
            carriedImplementationProof);
        var missingRunnableApplicationProofSummary = ResolveMissingRunnableApplicationProofSummaryWithCarryForward(
            candidate,
            detail,
            carriedImplementationProof);
        var invalidBrowserProofSummary = ResolveInvalidBrowserProofSummary(candidate, detail);
        var invalidQualityValidationProofSummary = ResolveInvalidQualityValidationProofSummary(candidate, detail, inspectionText);
        var missingRequiredArtifactSummary = ArtifactRequirementMatcher
            .ResolveMissingRequiredArtifact(candidate, detail, inspectionText)
            .Summary;
        var downgradedProjectStructureRequirementSummary = ResolveDowngradedProjectStructureRequirementSummary(candidate, detail, inspectionText);
        var missingUpstreamArtifactInspectionSummary = ResolveMissingUpstreamArtifactInspectionSummary(candidate, detail);
        var outOfScopeExternalTargetReferenceSummary = ResolveOutOfScopeExternalTargetReferenceSummary(detail, inspectionText);
        var shallowSharedManagedArtifactReferenceSummary = ResolveShallowSharedManagedArtifactReferenceSummary(detail, inspectionText);
        var blockerSummary = ProcessCompletionBlockerRules.CreateSummary(
            missingUpstreamArtifactInputSummary,
            missingConcreteProofSummary,
            incompleteImplementationSummary,
            missingConcreteImplementationProofSummary,
            missingRunnableApplicationProofSummary,
            invalidBrowserProofSummary,
            invalidQualityValidationProofSummary,
            missingRequiredArtifactSummary,
            downgradedProjectStructureRequirementSummary,
            missingUpstreamArtifactInspectionSummary,
            outOfScopeExternalTargetReferenceSummary,
            shallowSharedManagedArtifactReferenceSummary);
        if (hasDeclaredOutcome)
        {
            var contextValidation = ValidateProcessStepOutcomeContextWithCarryForward(
                candidate,
                detail,
                processOutcome,
                declaredOutcome,
                inspectionText,
                carriedImplementationProof);
            if (!contextValidation.IsValid)
            {
                if (TryRecoverExplicitDispositionBranchSelection(
                        candidate,
                        declaredOutcome,
                        contextValidation,
                        responseText,
                        out _))
                {
                    return ProcessStepRunStatus.Completed;
                }

                if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                    HasUnrecoverableMissingRequiredTool(missingRequiredTools))
                {
                    return ProcessStepRunStatus.Failed;
                }

                return contextValidation.Errors.Any(error =>
                    error.Code is "process.step_outcome.context.branch_required" or "process.step_outcome.context.branch_invalid")
                    ? ProcessStepRunStatus.Failed
                    : declaredOutcome.Status == ProcessStepRunStatus.Completed
                        ? ProcessStepRunStatus.Blocked
                        : ProcessStepRunStatus.Failed;
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                HasUnrecoverableMissingRequiredTool(missingRequiredTools))
            {
                return ProcessStepRunStatus.Failed;
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                blockerSummary.HasAny)
            {
                return ProcessStepRunStatus.Blocked;
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                missingRequiredTools.Count > 0)
            {
                return ProcessStepRunStatus.Failed;
            }

            return declaredOutcome.Status;
        }

        if (blockerSummary.HasAny)
        {
            return ProcessStepRunStatus.Blocked;
        }

        if (CanImplicitlyCompleteGovernedStep(candidate, detail, missingRequiredTools, inspectionText))
        {
            return ProcessStepRunStatus.Completed;
        }

        if (RequiresGovernedStepOutcome(candidate.StepRun))
        {
            return ProcessStepRunStatus.Failed;
        }

        if (missingRequiredTools.Count > 0)
        {
            return ProcessStepRunStatus.Failed;
        }

        return ProcessStepRunStatus.Completed;
    }

    private static bool CanCompleteExplicitDispositionOutcomeWithCriticalToolFailures(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        DeclaredStepOutcome declaredOutcome,
        ProcessStepOutcomeResult processOutcome,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        CarriedImplementationProof carriedImplementationProof)
    {
        if (declaredOutcome.Status != ProcessStepRunStatus.Completed ||
            missingRequiredTools.Count > 0 ||
            !RequiresGovernedStepOutcome(candidate.StepRun) ||
            !IsDispositionRoutingStep(candidate) ||
            !TryResolveExplicitDispositionBranchOutcome(candidate, responseText, out _))
        {
            return false;
        }

        var contextValidation = ValidateProcessStepOutcomeContextWithCarryForward(
            candidate,
            detail,
            processOutcome,
            declaredOutcome,
            ResolveOutputInspectionText(responseText),
            carriedImplementationProof);
        return CanCompleteExplicitDispositionOutcomeWithContextValidation(
            candidate,
            declaredOutcome,
            contextValidation,
            responseText,
            missingRequiredTools);
    }

    private static bool CanCompleteExplicitDispositionOutcomeWithContextValidation(
        DispatchCandidate candidate,
        DeclaredStepOutcome declaredOutcome,
        AgentOutputValidationResult contextValidation,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools)
    {
        if (declaredOutcome.Status != ProcessStepRunStatus.Completed ||
            missingRequiredTools.Count > 0 ||
            !RequiresGovernedStepOutcome(candidate.StepRun) ||
            !IsDispositionRoutingStep(candidate) ||
            !TryResolveExplicitDispositionBranchOutcome(candidate, responseText, out var branchOutcome))
        {
            return false;
        }

        if (contextValidation.IsValid ||
            TryRecoverExplicitDispositionBranchSelection(
                candidate,
                declaredOutcome,
                contextValidation,
                responseText,
                out _))
        {
            return true;
        }

        if (!IsRepairBranchOutcomeCandidate(branchOutcome, IsPrimaryRepairBranchOutcomeToken) &&
            !IsRepairBranchOutcomeCandidate(branchOutcome, IsSecondaryRepairBranchOutcomeToken))
        {
            return false;
        }

        return contextValidation.Errors.Count > 0 &&
               contextValidation.Errors.All(error =>
                   error.Code is "process.step_outcome.context.branch_required" or
                       "process.step_outcome.context.branch_invalid" or
                       "process.step_outcome.context.invalid_browser_proof" or
                       "process.step_outcome.context.invalid_quality_validation_proof");
    }

    private static string BuildCompletionReasonWithCarryForward(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string stepTitle,
        IEnumerable<string> successfulToolNamesFromPriorAttempts)
    {
        return BuildCompletionReasonWithCarryForward(
            candidate,
            detail,
            stepTitle,
            successfulToolNamesFromPriorAttempts,
            detail.Run.ResultSummary);
    }

    private static string BuildCompletionReasonWithCarryForward(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string stepTitle,
        IEnumerable<string> successfulToolNamesFromPriorAttempts,
        string? responseText)
    {
        return BuildCompletionReasonWithCarryForward(
            candidate,
            detail,
            stepTitle,
            successfulToolNamesFromPriorAttempts,
            responseText,
            CarriedImplementationProof.None);
    }

    private static string BuildCompletionReasonWithCarryForward(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string stepTitle,
        IEnumerable<string> successfulToolNamesFromPriorAttempts,
        string? responseText,
        CarriedImplementationProof carriedImplementationProof)
    {
        return BuildCompletionReasonCoreWithCarryForward(
            candidate,
            detail,
            stepTitle,
            ResolveMissingRequiredToolExecutionsWithCarriedImplementationProof(
                candidate,
                detail,
                successfulToolNamesFromPriorAttempts,
                carriedImplementationProof),
            responseText,
            carriedImplementationProof);
    }

    private static bool TryResolveDeclaredStepOutcome(string? responseText, out DeclaredStepOutcome declaredOutcome)
    {
        return TryResolveDeclaredStepOutcome(responseText, out declaredOutcome, out _);
    }

    private static bool TryResolveDeclaredStepOutcome(
        string? responseText,
        out DeclaredStepOutcome declaredOutcome,
        out ProcessStepOutcomeResult outcome)
    {
        declaredOutcome = default;
        outcome = default!;
        if (!ProcessDeclaredStepOutcomeRules.TryResolve(responseText, out var parsedOutcome, out outcome))
        {
            return false;
        }

        declaredOutcome = new DeclaredStepOutcome(
            parsedOutcome.Status,
            parsedOutcome.Reason,
            null,
            parsedOutcome.BranchOutcomeKey,
            parsedOutcome.BranchOutcomeTitle);
        return true;
    }

    private static bool TryResolveDeclaredStepOutcome(
        DispatchCandidate candidate,
        string? responseText,
        out DeclaredStepOutcome declaredOutcome,
        out ProcessStepOutcomeResult outcome)
    {
        if (!TryResolveDeclaredStepOutcome(responseText, out var parsedOutcome, out outcome))
        {
            declaredOutcome = default;
            return false;
        }

        declaredOutcome = parsedOutcome with
        {
            SelectedBranchOutcomeId = ResolveSelectedBranchOutcomeId(
                candidate,
                parsedOutcome.Status,
                parsedOutcome.BranchOutcomeKey,
                parsedOutcome.BranchOutcomeTitle)
        };
        return true;
    }

    private static bool DeclaredBlockedOutcomeClaimsRequiredToolFailureWithoutReceipt(
        DeclaredStepOutcome declaredOutcome,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        ProcessAutomationExecutionRunDetail detail)
    {
        return ProcessDeclaredStepOutcomeRules.BlockedOutcomeClaimsRequiredToolFailureWithoutReceipt(
            declaredOutcome.Status,
            declaredOutcome.Reason,
            ResolveOutputInspectionText(responseText),
            missingRequiredTools,
            detail.ToolReceipts);
    }

    private static bool HasFailedReceiptForRequiredTool(
        ProcessAutomationExecutionRunDetail detail,
        IReadOnlyList<string> requiredToolNames)
    {
        return ProcessDeclaredStepOutcomeRules.HasFailedReceiptForRequiredTool(detail.ToolReceipts, requiredToolNames);
    }

    private static string BuildDeclaredStepOutcomeReason(string runTitle, string stepTitle, DeclaredStepOutcome declaredOutcome)
    {
        return ProcessDeclaredStepOutcomeRules.BuildReason(
            runTitle,
            stepTitle,
            declaredOutcome.Status,
            declaredOutcome.Reason);
    }

    private static ProcessAutomationObservationSnapshot CreateAutomationObservationSnapshot(ProcessAutomationExecutionRunDetail detail)
    {
        return ProcessAutomationObservationSnapshot.Create(detail, CanTrustCompletedInternalToolLogs(detail));
    }

    private static bool CanTrustCompletedInternalToolLogs(ProcessAutomationExecutionRunDetail detail)
    {
        return detail.Run.State == ProcessAutomationExecutionState.Completed &&
               detail.Run.Outcome == ProcessAutomationRunOutcome.Succeeded &&
               HasCompletedDeclaredStepOutcome(detail);
    }

    private static ISet<string> ResolveSuccessfulToolNames(ProcessAutomationExecutionRunDetail detail)
    {
        return CreateAutomationObservationSnapshot(detail).SuccessfulToolNames.ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> ResolveSuccessfulExecutionLogToolNames(ProcessAutomationExecutionRunDetail detail)
    {
        return ProcessAutomationExecutionLogObservation
            .Create(detail.ExecutionLog, CanTrustCompletedInternalToolLogs(detail))
            .SuccessfulToolNames
            .ToList();
    }

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveSuccessfulBrowserToolOutputFiles(ProcessAutomationExecutionRunDetail detail)
    {
        return CreateAutomationObservationSnapshot(detail).BrowserToolOutputFiles;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveExecutionLogBrowserToolOutputFiles(IReadOnlyList<ProcessAutomationExecutionLogEntry> executionLog)
    {
        return ProcessAutomationExecutionLogObservation.Create(executionLog, false).BrowserToolOutputFiles;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveBrowserEvidenceReferenceOutputFiles(string? resultSummary)
    {
        return ProcessAutomationObservationSnapshot.ResolveBrowserEvidenceReferenceOutputFiles(resultSummary);
    }

    private static IReadOnlyList<string> ResolveBrowserEvidenceReferences(string resultSummary)
    {
        return ProcessAutomationObservationSnapshot.ResolveBrowserEvidenceReferences(resultSummary);
    }

    private static bool TryResolveExecutionLogFilenameArgument(string message, out string fileName)
    {
        return ProcessAutomationExecutionLogObservation.TryResolveFilenameArgument(message, out fileName);
    }

    private static bool TryResolveExecutionLogInvokedToolName(string message, out string toolName)
    {
        return ProcessAutomationExecutionLogObservation.TryResolveInvokedToolName(message, out toolName);
    }

    private static bool IsProviderNativeExecutionLogToolName(string toolName)
    {
        return ProcessAutomationExecutionLogObservation.IsProviderNativeToolName(toolName);
    }

    private static bool IsInternalMafExecutionLogToolName(string toolName)
    {
        return ProcessAutomationExecutionLogObservation.IsInternalMafToolName(toolName);
    }

    private static IReadOnlyList<string> ResolveSuccessfulSessionToolNames(string? serializedSessionStateJson)
    {
        return ProcessAutomationSessionObservation
            .Create(serializedSessionStateJson)
            .SuccessfulToolNames
            .ToList();
    }

    private static IReadOnlyList<SessionToolResultText> ResolveSuccessfulSessionToolResultTexts(string? serializedSessionStateJson)
    {
        return ProcessAutomationSessionObservation
            .Create(serializedSessionStateJson)
            .SuccessfulToolResultTexts
            .Select(item => new SessionToolResultText(item.ToolName, item.Text))
            .ToList();
    }

    internal static IReadOnlyList<SessionFileContent> ResolveSuccessfulSessionFileWrites(string? serializedSessionStateJson)
    {
        return ProcessAutomationSessionObservation
            .Create(serializedSessionStateJson)
            .FileWrites
            .Select(item => new SessionFileContent(item.Path, item.Content))
            .ToList();
    }

    private static IReadOnlyList<SessionFileContent> ResolveSuccessfulSessionFileReads(string? serializedSessionStateJson)
    {
        return ProcessAutomationSessionObservation
            .Create(serializedSessionStateJson)
            .FileReads
            .Select(item => new SessionFileContent(item.Path, item.Content))
            .ToList();
    }

    private static IReadOnlyList<SessionFileContent> ResolveSuccessfulSessionPathStats(string? serializedSessionStateJson)
    {
        return ProcessAutomationSessionObservation
            .Create(serializedSessionStateJson)
            .PathStats
            .Select(item => new SessionFileContent(item.Path, item.Content))
            .ToList();
    }

    private static string? ResolveLatestAssistantResponseText(string? serializedSessionStateJson)
    {
        return ProcessAutomationSessionObservation.Create(serializedSessionStateJson).LatestAssistantResponseText;
    }

    private static string? ResolveLatestAssistantErrorSummary(string? serializedSessionStateJson)
    {
        return ProcessAutomationSessionObservation.Create(serializedSessionStateJson).LatestAssistantErrorSummary;
    }

    private static bool TryResolveAssistantErrorSummary(
        JsonElement content,
        out string assistantError)
    {
        return ProcessAutomationSessionObservation.TryResolveAssistantErrorSummary(content, out assistantError);
    }

    private static bool TryMapRecoverableProviderFailureSummary(
        string? candidateText,
        out string failureSummary)
    {
        return ProcessRecoverableProviderFailureRules.TryMapSummary(candidateText, out failureSummary);
    }

    private static string TruncateForPrompt(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
        {
            return text;
        }

        return text[..maxLength].TrimEnd() + "...";
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveSuccessfulSessionToolOutputFiles(string serializedSessionStateJson)
    {
        return ProcessAutomationSessionObservation.Create(serializedSessionStateJson).BrowserToolOutputFiles;
    }

    private static bool IsSuccessfulSessionFunctionResult(JsonElement result)
    {
        return ProcessAutomationSessionObservation.IsSuccessfulFunctionResult(result);
    }

    private static string? TryResolveSessionToolOutputFileName(JsonElement functionCallContent)
    {
        return ProcessAutomationSessionObservation.TryResolveToolOutputFileName(functionCallContent);
    }

}
