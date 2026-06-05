using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

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
            return toolName is "workspace_dotnet_build" or "workspace_dotnet_test" or "workspace_dotnet_run";
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
        return inspectedText.Contains(".csproj", StringComparison.OrdinalIgnoreCase) ||
               inspectedText.Contains(".slnx", StringComparison.OrdinalIgnoreCase) ||
               inspectedText.Contains(".sln", StringComparison.OrdinalIgnoreCase);
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

        var hasProcessMockImplementationProof = projections.Any(projection =>
            CanSatisfyConcreteImplementationProofWithProcessMock(candidate, projection));
        if (hasProcessMockImplementationProof)
        {
            satisfiedToolNames.AddRange(requiredToolNames
                .Where(toolName =>
                    ImplementationProofToolNames.Contains(toolName, StringComparer.Ordinal) ||
                    IsImplementationValidationToolName(toolName)));
        }

        return satisfiedToolNames
            .Distinct(StringComparer.Ordinal)
            .ToList();
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
        if (!TryReadProcessStepOutcome(responseText, out var parsedOutcome, out _))
        {
            return false;
        }

        outcome = parsedOutcome;
        declaredOutcome = new DeclaredStepOutcome(
            MapProcessStepOutcomeStatus(parsedOutcome.Status),
            parsedOutcome.Reason.Trim(),
            null,
            parsedOutcome.BranchOutcomeKey.Trim(),
            parsedOutcome.BranchOutcomeTitle.Trim());
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
        if (declaredOutcome.Status != ProcessStepRunStatus.Blocked ||
            missingRequiredTools.Count == 0 ||
            HasFailedReceiptForRequiredTool(detail, missingRequiredTools))
        {
            return false;
        }

        var normalizedText = CollapsePromptWhitespace($"{declaredOutcome.Reason} {ResolveOutputInspectionText(responseText)}");
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return false;
        }

        return missingRequiredTools.Any(toolName =>
            normalizedText.Contains(toolName, StringComparison.OrdinalIgnoreCase)) ||
               (normalizedText.Contains("tool", StringComparison.OrdinalIgnoreCase) &&
                (normalizedText.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                 normalizedText.Contains("failure", StringComparison.OrdinalIgnoreCase) ||
                 normalizedText.Contains("unavailable", StringComparison.OrdinalIgnoreCase) ||
                 normalizedText.Contains("denied", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool HasFailedReceiptForRequiredTool(
        ProcessAutomationExecutionRunDetail detail,
        IReadOnlyList<string> requiredToolNames)
    {
        if (requiredToolNames.Count == 0)
        {
            return false;
        }

        var required = requiredToolNames
            .Select(NormalizeToolToken)
            .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
            .ToHashSet(StringComparer.Ordinal);

        return detail.ToolReceipts.Any(receipt =>
            required.Contains(NormalizeToolToken(receipt.ToolName)) &&
            IsFailedToolReceipt(receipt));
    }

    private static string BuildDeclaredStepOutcomeReason(string runTitle, string stepTitle, DeclaredStepOutcome declaredOutcome)
    {
        var trimmedReason = declaredOutcome.Reason.Trim();
        return declaredOutcome.Status switch
        {
            ProcessStepRunStatus.Completed => string.IsNullOrWhiteSpace(trimmedReason)
                ? $"AgentFramework run '{runTitle}' completed step '{stepTitle}' with an explicit governed outcome."
                : $"AgentFramework run '{runTitle}' completed step '{stepTitle}': {trimmedReason}",
            ProcessStepRunStatus.Blocked => string.IsNullOrWhiteSpace(trimmedReason)
                ? $"AgentFramework run '{runTitle}' blocked step '{stepTitle}' pending remediation."
                : $"AgentFramework run '{runTitle}' blocked step '{stepTitle}': {trimmedReason}",
            ProcessStepRunStatus.WaitingApproval => string.IsNullOrWhiteSpace(trimmedReason)
                ? $"AgentFramework run '{runTitle}' is waiting on approval before '{stepTitle}' can continue."
                : $"AgentFramework run '{runTitle}' is waiting on approval before '{stepTitle}' can continue: {trimmedReason}",
            ProcessStepRunStatus.Refused => string.IsNullOrWhiteSpace(trimmedReason)
                ? $"AgentFramework run '{runTitle}' refused step '{stepTitle}'."
                : $"AgentFramework run '{runTitle}' refused step '{stepTitle}': {trimmedReason}",
            _ => string.IsNullOrWhiteSpace(trimmedReason)
                ? $"AgentFramework run '{runTitle}' failed step '{stepTitle}'."
                : $"AgentFramework run '{runTitle}' failed step '{stepTitle}': {trimmedReason}"
        };
    }

    private static ISet<string> ResolveSuccessfulToolNames(ProcessAutomationExecutionRunDetail detail)
    {
        var successfulToolNames = ProcessAutomationReceiptObservationHelper.ResolveSuccessfulToolNames(detail);

        foreach (var toolName in ResolveSuccessfulSessionToolNames(detail.Run.SerializedSessionStateJson))
        {
            successfulToolNames.Add(toolName);
        }

        foreach (var toolName in ResolveSuccessfulExecutionLogToolNames(detail))
        {
            successfulToolNames.Add(toolName);
        }

        return successfulToolNames;
    }

    private static IReadOnlyList<string> ResolveSuccessfulExecutionLogToolNames(ProcessAutomationExecutionRunDetail detail)
    {
        var executionLog = detail.ExecutionLog;
        if (executionLog.Count == 0)
        {
            return [];
        }

        var canTrustCompletedInternalToolLogs =
            detail.Run.State == ProcessAutomationExecutionState.Completed &&
            detail.Run.Outcome == ProcessAutomationRunOutcome.Succeeded &&
            HasCompletedDeclaredStepOutcome(detail);
        var toolNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in executionLog)
        {
            if (!string.Equals(entry.Phase, "Tool", StringComparison.OrdinalIgnoreCase) ||
                entry.State == ProcessAutomationExecutionState.Failed ||
                !TryResolveExecutionLogInvokedToolName(entry.Message, out var toolName) ||
                (!IsProviderNativeExecutionLogToolName(toolName) &&
                 !(canTrustCompletedInternalToolLogs && IsInternalMafExecutionLogToolName(toolName))))
            {
                continue;
            }

            toolNames.Add(toolName);
        }

        return toolNames.ToList();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveSuccessfulBrowserToolOutputFiles(ProcessAutomationExecutionRunDetail detail)
    {
        var outputFilesByToolName = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var pair in ResolveSuccessfulSessionToolOutputFiles(detail.Run.SerializedSessionStateJson ?? string.Empty))
        {
            AddBrowserOutputFiles(outputFilesByToolName, pair.Key, pair.Value);
        }

        foreach (var pair in ResolveExecutionLogBrowserToolOutputFiles(detail.ExecutionLog))
        {
            AddBrowserOutputFiles(outputFilesByToolName, pair.Key, pair.Value);
        }

        foreach (var pair in ResolveBrowserEvidenceReferenceOutputFiles(detail.Run.ResultSummary))
        {
            AddBrowserOutputFiles(outputFilesByToolName, pair.Key, pair.Value);
        }

        return outputFilesByToolName.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            StringComparer.Ordinal);
    }

    private static void AddBrowserOutputFiles(
        IDictionary<string, HashSet<string>> outputFilesByToolName,
        string toolName,
        IEnumerable<string> outputFiles)
    {
        var normalizedToolName = NormalizeToolToken(toolName);
        if (string.IsNullOrWhiteSpace(normalizedToolName) ||
            !normalizedToolName.StartsWith("browser_", StringComparison.Ordinal))
        {
            return;
        }

        if (!outputFilesByToolName.TryGetValue(normalizedToolName, out var files))
        {
            files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            outputFilesByToolName[normalizedToolName] = files;
        }

        foreach (var outputFile in outputFiles)
        {
            if (!string.IsNullOrWhiteSpace(outputFile))
            {
                files.Add(WorkspaceScopeDescriptor.NormalizeRelativePath(outputFile));
            }
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveExecutionLogBrowserToolOutputFiles(IReadOnlyList<ProcessAutomationExecutionLogEntry> executionLog)
    {
        if (executionLog.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        }

        var outputFilesByToolName = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var entry in executionLog)
        {
            if (!string.Equals(entry.Phase, "Tool", StringComparison.OrdinalIgnoreCase) ||
                entry.State == ProcessAutomationExecutionState.Failed ||
                !TryResolveExecutionLogInvokedToolName(entry.Message, out var toolName) ||
                !toolName.StartsWith("browser_", StringComparison.Ordinal) ||
                !TryResolveExecutionLogFilenameArgument(entry.Message, out var outputFileName))
            {
                continue;
            }

            if (!outputFilesByToolName.TryGetValue(toolName, out var outputFiles))
            {
                outputFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                outputFilesByToolName[toolName] = outputFiles;
            }

            outputFiles.Add(WorkspaceScopeDescriptor.NormalizeRelativePath(outputFileName));
        }

        return outputFilesByToolName.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveBrowserEvidenceReferenceOutputFiles(string? resultSummary)
    {
        if (string.IsNullOrWhiteSpace(resultSummary))
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        }

        var outputFilesByToolName = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var evidenceRef in ResolveBrowserEvidenceReferences(resultSummary))
        {
            var normalizedRef = WorkspaceScopeDescriptor.NormalizeRelativePath(evidenceRef);
            if (!IsProviderNativeBrowserEvidenceReferencePath(normalizedRef))
            {
                continue;
            }

            var toolName = ResolveProviderNativeBrowserToolName(normalizedRef);
            if (string.IsNullOrWhiteSpace(toolName))
            {
                continue;
            }

            if (!outputFilesByToolName.TryGetValue(toolName, out var outputFiles))
            {
                outputFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                outputFilesByToolName[toolName] = outputFiles;
            }

            outputFiles.Add(normalizedRef);
        }

        return outputFilesByToolName.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> ResolveBrowserEvidenceReferences(string resultSummary)
    {
        var references = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        TryAddStructuredEvidenceReferences(resultSummary, references);

        foreach (Match match in Regex.Matches(
                     resultSummary,
                     @"(?:\.playwright-mcp|artifacts[\\/](?:scopes[\\/][^\s`""',\]\)]+[\\/])?process-runs)[\\/][^\s`""',\]\)]+\.(?:png|jpe?g|yml|yaml|log|txt|json)",
                     RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            references.Add(match.Value.Trim().TrimEnd('.', ',', ';', ':'));
        }

        return references.ToList();
    }

    private static void TryAddStructuredEvidenceReferences(
        string resultSummary,
        ISet<string> references)
    {
        try
        {
            using var document = JsonDocument.Parse(resultSummary);
            if (!document.RootElement.TryGetProperty("evidenceRefs", out var evidenceRefs) ||
                evidenceRefs.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var item in evidenceRefs.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(item.GetString()))
                {
                    references.Add(item.GetString()!.Trim());
                }
            }
        }
        catch (JsonException)
        {
        }
    }

    private static bool TryResolveExecutionLogFilenameArgument(string message, out string fileName)
    {
        fileName = string.Empty;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        const string marker = "filename=\"";
        var start = message.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        start += marker.Length;
        var end = message.IndexOf('"', start);
        if (end <= start)
        {
            return false;
        }

        fileName = message[start..end].Trim();
        return !string.IsNullOrWhiteSpace(fileName);
    }

    private static bool TryResolveExecutionLogInvokedToolName(string message, out string toolName)
    {
        toolName = string.Empty;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        const string prefix = "Invoking tool '";
        var start = message.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        start += prefix.Length;
        var end = message.IndexOf('\'', start);
        if (end <= start)
        {
            return false;
        }

        toolName = NormalizeToolToken(message[start..end]);
        return !string.IsNullOrWhiteSpace(toolName);
    }

    private static bool IsProviderNativeExecutionLogToolName(string toolName)
    {
        return toolName.StartsWith("browser_", StringComparison.Ordinal);
    }

    private static bool IsInternalMafExecutionLogToolName(string toolName)
    {
        return toolName.StartsWith("project_structure_", StringComparison.Ordinal) ||
               toolName.StartsWith("process_", StringComparison.Ordinal) ||
               toolName.StartsWith("image_generation_", StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ResolveSuccessfulSessionToolNames(string? serializedSessionStateJson)
    {
        if (string.IsNullOrWhiteSpace(serializedSessionStateJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(serializedSessionStateJson);
            if (!document.RootElement.TryGetProperty("stateBag", out var stateBag) ||
                !stateBag.TryGetProperty("InMemoryChatHistoryProvider", out var historyProvider) ||
                !historyProvider.TryGetProperty("messages", out var messages) ||
                messages.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var toolNamesByCallId = new Dictionary<string, string>(StringComparer.Ordinal);
            var successfulToolNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var message in messages.EnumerateArray())
            {
                if (!message.TryGetProperty("contents", out var contents) ||
                    contents.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var content in contents.EnumerateArray())
                {
                    if (!content.TryGetProperty("$type", out var typeElement))
                    {
                        continue;
                    }

                    var contentType = typeElement.GetString();
                    if (string.Equals(contentType, "functionCall", StringComparison.Ordinal))
                    {
                        var callId = content.TryGetProperty("callId", out var callIdElement)
                            ? callIdElement.GetString()
                            : null;
                        var toolName = content.TryGetProperty("name", out var nameElement)
                            ? NormalizeToolToken(nameElement.GetString() ?? string.Empty)
                            : string.Empty;
                        if (!string.IsNullOrWhiteSpace(callId) && !string.IsNullOrWhiteSpace(toolName))
                        {
                            toolNamesByCallId[callId] = toolName;
                        }

                        continue;
                    }

                    if (!string.Equals(contentType, "functionResult", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var resultCallId = content.TryGetProperty("callId", out var resultCallIdElement)
                        ? resultCallIdElement.GetString()
                        : null;
                    if (string.IsNullOrWhiteSpace(resultCallId) ||
                        !toolNamesByCallId.TryGetValue(resultCallId, out var recordedToolName) ||
                        !content.TryGetProperty("result", out var resultElement) ||
                        !IsSuccessfulSessionFunctionResult(resultElement))
                    {
                        continue;
                    }

                    successfulToolNames.Add(recordedToolName);
                }
            }

            return successfulToolNames.ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<SessionToolResultText> ResolveSuccessfulSessionToolResultTexts(string? serializedSessionStateJson)
    {
        if (string.IsNullOrWhiteSpace(serializedSessionStateJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(serializedSessionStateJson);
            if (!document.RootElement.TryGetProperty("stateBag", out var stateBag) ||
                !stateBag.TryGetProperty("InMemoryChatHistoryProvider", out var historyProvider) ||
                !historyProvider.TryGetProperty("messages", out var messages) ||
                messages.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var toolNamesByCallId = new Dictionary<string, string>(StringComparer.Ordinal);
            var resultTexts = new List<SessionToolResultText>();

            foreach (var message in messages.EnumerateArray())
            {
                if (!message.TryGetProperty("contents", out var contents) ||
                    contents.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var content in contents.EnumerateArray())
                {
                    if (!content.TryGetProperty("$type", out var typeElement))
                    {
                        continue;
                    }

                    var contentType = typeElement.GetString();
                    if (string.Equals(contentType, "functionCall", StringComparison.Ordinal))
                    {
                        var callId = content.TryGetProperty("callId", out var callIdElement)
                            ? callIdElement.GetString()
                            : null;
                        var toolName = content.TryGetProperty("name", out var nameElement)
                            ? NormalizeToolToken(nameElement.GetString() ?? string.Empty)
                            : string.Empty;
                        if (!string.IsNullOrWhiteSpace(callId) && !string.IsNullOrWhiteSpace(toolName))
                        {
                            toolNamesByCallId[callId] = toolName;
                        }

                        continue;
                    }

                    if (!string.Equals(contentType, "functionResult", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var resultCallId = content.TryGetProperty("callId", out var resultCallIdElement)
                        ? resultCallIdElement.GetString()
                        : null;
                    if (string.IsNullOrWhiteSpace(resultCallId) ||
                        !toolNamesByCallId.TryGetValue(resultCallId, out var recordedToolName) ||
                        !content.TryGetProperty("result", out var resultElement) ||
                        !IsSuccessfulSessionFunctionResult(resultElement))
                    {
                        continue;
                    }

                    var resultText = ExtractSessionToolResultText(resultElement);
                    if (!string.IsNullOrWhiteSpace(resultText))
                    {
                        resultTexts.Add(new SessionToolResultText(recordedToolName, resultText));
                    }
                }
            }

            return resultTexts;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string ExtractSessionToolResultText(JsonElement result)
    {
        var builder = new StringBuilder();
        AppendSessionToolResultText(builder, result, 0);
        return builder.ToString();
    }

    private static void AppendSessionToolResultText(StringBuilder builder, JsonElement element, int depth)
    {
        if (depth > 4)
        {
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
            {
                AppendSessionToolResultTextPart(builder, element.GetString());
                return;
            }
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
            {
                AppendSessionToolResultTextPart(builder, element.ToString());
                return;
            }
            case JsonValueKind.Array:
            {
                foreach (var item in element.EnumerateArray())
                {
                    AppendSessionToolResultText(builder, item, depth + 1);
                }

                return;
            }
            case JsonValueKind.Object:
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String &&
                        IsDiagnosticSessionToolResultProperty(property.Name))
                    {
                        AppendSessionToolResultTextPart(builder, property.Value.GetString());
                        continue;
                    }

                    if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        AppendSessionToolResultText(builder, property.Value, depth + 1);
                    }
                }

                return;
            }
        }
    }

    private static bool IsDiagnosticSessionToolResultProperty(string propertyName)
    {
        return propertyName.Equals("text", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("content", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("message", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("summary", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("output", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("stdout", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("stderr", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("exitSummary", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendSessionToolResultTextPart(StringBuilder builder, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(value.Trim());
    }

    private static IReadOnlyList<SessionFileContent> ResolveSuccessfulSessionFileWrites(string? serializedSessionStateJson)
    {
        return ResolveSuccessfulSessionFileContents(
            serializedSessionStateJson,
            static toolName => string.Equals(toolName, "workspace_write_file", StringComparison.Ordinal) ||
                               string.Equals(toolName, "workspace_append_file", StringComparison.Ordinal),
            static callContent =>
            {
                if (!callContent.TryGetProperty("arguments", out var arguments) ||
                    arguments.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                var path = TryResolveStringProperty(arguments, "path");
                if (string.IsNullOrWhiteSpace(path))
                {
                    return null;
                }

                var content = TryResolveStringProperty(arguments, "content") ?? string.Empty;
                return new SessionFileContent(path.Trim(), content);
            },
            static _ => null);
    }

    private static IReadOnlyList<SessionFileContent> ResolveSuccessfulSessionFileReads(string? serializedSessionStateJson)
    {
        return ResolveSuccessfulSessionFileContents(
            serializedSessionStateJson,
            static toolName => string.Equals(toolName, "workspace_read_file", StringComparison.Ordinal),
            static callContent =>
            {
                if (!callContent.TryGetProperty("arguments", out var arguments) ||
                    arguments.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                var path = TryResolveStringProperty(arguments, "path");
                return string.IsNullOrWhiteSpace(path)
                    ? null
                    : new SessionFileContent(path.Trim(), string.Empty);
            },
            static resultContent =>
            {
                var path = TryResolveStringProperty(resultContent, "path");
                if (string.IsNullOrWhiteSpace(path))
                {
                    return null;
                }

                var content = TryResolveStringProperty(resultContent, "content") ?? string.Empty;
                return new SessionFileContent(path.Trim(), content);
            });
    }

    private static IReadOnlyList<SessionFileContent> ResolveSuccessfulSessionPathStats(string? serializedSessionStateJson)
    {
        return ResolveSuccessfulSessionFileContents(
            serializedSessionStateJson,
            static toolName => string.Equals(toolName, "workspace_stat_path", StringComparison.Ordinal),
            static callContent =>
            {
                if (!callContent.TryGetProperty("arguments", out var arguments) ||
                    arguments.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                var path = TryResolveStringProperty(arguments, "path");
                return string.IsNullOrWhiteSpace(path)
                    ? null
                    : new SessionFileContent(path.Trim(), string.Empty);
            },
            static resultContent =>
            {
                var path = TryResolveStringProperty(resultContent, "path");
                return string.IsNullOrWhiteSpace(path)
                    ? null
                    : new SessionFileContent(path.Trim(), string.Empty);
            });
    }

    private static IReadOnlyList<SessionFileContent> ResolveSuccessfulSessionFileContents(
        string? serializedSessionStateJson,
        Func<string, bool> isTargetTool,
        Func<JsonElement, SessionFileContent?> resolveCallContent,
        Func<JsonElement, SessionFileContent?> resolveResultContent)
    {
        if (string.IsNullOrWhiteSpace(serializedSessionStateJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(serializedSessionStateJson);
            if (!document.RootElement.TryGetProperty("stateBag", out var stateBag) ||
                !stateBag.TryGetProperty("InMemoryChatHistoryProvider", out var historyProvider) ||
                !historyProvider.TryGetProperty("messages", out var messages) ||
                messages.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var callsById = new Dictionary<string, SessionFileContent>(StringComparer.Ordinal);
            var successfulContents = new List<SessionFileContent>();

            foreach (var message in messages.EnumerateArray())
            {
                if (!message.TryGetProperty("contents", out var contents) ||
                    contents.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var content in contents.EnumerateArray())
                {
                    if (!content.TryGetProperty("$type", out var typeElement))
                    {
                        continue;
                    }

                    var contentType = typeElement.GetString();
                    if (string.Equals(contentType, "functionCall", StringComparison.Ordinal))
                    {
                        var callId = content.TryGetProperty("callId", out var callIdElement)
                            ? callIdElement.GetString()
                            : null;
                        var toolName = content.TryGetProperty("name", out var nameElement)
                            ? NormalizeToolToken(nameElement.GetString() ?? string.Empty)
                            : string.Empty;
                        if (string.IsNullOrWhiteSpace(callId) ||
                            string.IsNullOrWhiteSpace(toolName) ||
                            !isTargetTool(toolName))
                        {
                            continue;
                        }

                        var fileContent = resolveCallContent(content);
                        if (fileContent is not null)
                        {
                            callsById[callId] = fileContent;
                        }

                        continue;
                    }

                    if (!string.Equals(contentType, "functionResult", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var resultCallId = content.TryGetProperty("callId", out var resultCallIdElement)
                        ? resultCallIdElement.GetString()
                        : null;
                    if (string.IsNullOrWhiteSpace(resultCallId) ||
                        !callsById.TryGetValue(resultCallId, out var callFileContent) ||
                        !content.TryGetProperty("result", out var resultElement) ||
                        !IsSuccessfulSessionFunctionResult(resultElement))
                    {
                        continue;
                    }

                    var resultFileContent = resolveResultContent(resultElement);
                    successfulContents.Add(resultFileContent ?? callFileContent);
                }
            }

            return successfulContents;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? ResolveLatestAssistantResponseText(string? serializedSessionStateJson)
    {
        if (string.IsNullOrWhiteSpace(serializedSessionStateJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(serializedSessionStateJson);
            if (!document.RootElement.TryGetProperty("stateBag", out var stateBag) ||
                !stateBag.TryGetProperty("InMemoryChatHistoryProvider", out var historyProvider) ||
                !historyProvider.TryGetProperty("messages", out var messages) ||
                messages.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            string? latestAssistantText = null;

            foreach (var message in messages.EnumerateArray())
            {
                if (!message.TryGetProperty("role", out var roleElement) ||
                    !string.Equals(roleElement.GetString(), "assistant", StringComparison.OrdinalIgnoreCase) ||
                    !message.TryGetProperty("contents", out var contents) ||
                    contents.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var assistantParts = new List<string>();
                foreach (var content in contents.EnumerateArray())
                {
                    if (!content.TryGetProperty("$type", out var typeElement) ||
                        !string.Equals(typeElement.GetString(), "text", StringComparison.OrdinalIgnoreCase) ||
                        !content.TryGetProperty("text", out var textElement))
                    {
                        continue;
                    }

                    var text = textElement.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        assistantParts.Add(text.Trim());
                    }
                }

                if (assistantParts.Count > 0)
                {
                    latestAssistantText = string.Join(Environment.NewLine, assistantParts);
                }
            }

            return latestAssistantText;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ResolveLatestAssistantErrorSummary(string? serializedSessionStateJson)
    {
        if (string.IsNullOrWhiteSpace(serializedSessionStateJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(serializedSessionStateJson);
            if (!document.RootElement.TryGetProperty("stateBag", out var stateBag) ||
                !stateBag.TryGetProperty("InMemoryChatHistoryProvider", out var historyProvider) ||
                !historyProvider.TryGetProperty("messages", out var messages) ||
                messages.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            string? latestAssistantError = null;
            foreach (var message in messages.EnumerateArray())
            {
                if (!message.TryGetProperty("role", out var roleElement) ||
                    !string.Equals(roleElement.GetString(), "assistant", StringComparison.OrdinalIgnoreCase) ||
                    !message.TryGetProperty("contents", out var contents) ||
                    contents.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var content in contents.EnumerateArray())
                {
                    if (!TryResolveAssistantErrorSummary(content, out var assistantError))
                    {
                        continue;
                    }

                    latestAssistantError = assistantError;
                }
            }

            return latestAssistantError;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryResolveAssistantErrorSummary(
        JsonElement content,
        out string assistantError)
    {
        assistantError = string.Empty;
        var hasErrorCode = content.TryGetProperty("errorCode", out var errorCodeElement) &&
            errorCodeElement.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(errorCodeElement.GetString());
        var contentType = content.TryGetProperty("$type", out var typeElement)
            ? typeElement.GetString()
            : string.Empty;
        if (!hasErrorCode &&
            !string.Equals(contentType, "error", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var errorCode = hasErrorCode
            ? errorCodeElement.GetString()!.Trim()
            : string.Empty;
        var message = TryResolveStringProperty(content, "message")
            ?? TryResolveStringProperty(content, "errorMessage")
            ?? TryResolveStringProperty(content, "text")
            ?? TryResolveStringProperty(content, "content")
            ?? string.Empty;
        assistantError = string.IsNullOrWhiteSpace(errorCode)
            ? message.Trim()
            : string.IsNullOrWhiteSpace(message)
                ? errorCode
                : $"{errorCode}: {message.Trim()}";
        return !string.IsNullOrWhiteSpace(assistantError);
    }

    private static string? TryResolveStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var propertyValue) &&
               propertyValue.ValueKind == JsonValueKind.String
            ? propertyValue.GetString()
            : null;
    }

    private static bool TryMapRecoverableProviderFailureSummary(
        string? candidateText,
        out string failureSummary)
    {
        failureSummary = string.Empty;
        if (string.IsNullOrWhiteSpace(candidateText))
        {
            return false;
        }

        var normalizedText = Regex.Replace(
                candidateText,
                @"\s+",
                " ",
                RegexOptions.CultureInvariant)
            .Trim();
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return false;
        }

        if (normalizedText.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase) ||
            normalizedText.Contains("exceeded your current quota", StringComparison.OrdinalIgnoreCase))
        {
            failureSummary = "Provider quota was exhausted before the agent returned a usable response.";
            return true;
        }

        if (normalizedText.Contains("rate_limit", StringComparison.OrdinalIgnoreCase) ||
            normalizedText.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            failureSummary = "The assigned provider hit a rate limit before the agent returned a usable response.";
            return true;
        }

        var missingProviderCredential =
            ((normalizedText.Contains("Environment variable '", StringComparison.OrdinalIgnoreCase) &&
              normalizedText.Contains("' is not set.", StringComparison.OrdinalIgnoreCase) &&
              !normalizedText.Contains("memory capability", StringComparison.OrdinalIgnoreCase)) ||
             normalizedText.Contains("No API key environment variable is configured for this provider", StringComparison.OrdinalIgnoreCase) ||
             normalizedText.Contains("No secret record or API key environment variable is configured for this provider", StringComparison.OrdinalIgnoreCase) ||
             (normalizedText.Contains("Secret record '", StringComparison.OrdinalIgnoreCase) &&
              (normalizedText.Contains("was not found.", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("could not be decrypted", StringComparison.OrdinalIgnoreCase))));
        if (missingProviderCredential)
        {
            failureSummary = "The assigned provider did not have usable credentials in the current environment.";
            return true;
        }

        if (normalizedText.Contains("The provider completed without returning text.", StringComparison.OrdinalIgnoreCase) ||
            normalizedText.Contains("provider completed without returning text", StringComparison.OrdinalIgnoreCase) ||
            normalizedText.Contains("provider returned an empty response", StringComparison.OrdinalIgnoreCase))
        {
            failureSummary = "The assigned provider completed without returning text.";
            return true;
        }

        if (normalizedText.Contains("ResponseEnded", StringComparison.OrdinalIgnoreCase) ||
            normalizedText.Contains("response ended prematurely", StringComparison.OrdinalIgnoreCase))
        {
            failureSummary = "The assigned provider response ended before the agent produced a usable response.";
            return true;
        }

        if ((normalizedText.Contains("cannot enforce structured output contract", StringComparison.OrdinalIgnoreCase) ||
             normalizedText.Contains("cannot enforce structured-output contract", StringComparison.OrdinalIgnoreCase)) &&
            (normalizedText.Contains("Choose a structured-output capable", StringComparison.OrdinalIgnoreCase) ||
             normalizedText.Contains("structured-output capable OpenAI", StringComparison.OrdinalIgnoreCase)))
        {
            failureSummary = "The assigned provider cannot enforce the required structured output contract.";
            return true;
        }

        if (Regex.IsMatch(
                normalizedText,
                @"Response status code does not indicate success:\s*5\d\d\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
            normalizedText.Contains("Internal Server Error", StringComparison.OrdinalIgnoreCase) ||
            normalizedText.Contains("Bad Gateway", StringComparison.OrdinalIgnoreCase) ||
            normalizedText.Contains("Service Unavailable", StringComparison.OrdinalIgnoreCase) ||
            normalizedText.Contains("Gateway Timeout", StringComparison.OrdinalIgnoreCase))
        {
            failureSummary = "The assigned provider returned an upstream server error before the agent produced a usable response.";
            return true;
        }

        return false;
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
        if (string.IsNullOrWhiteSpace(serializedSessionStateJson))
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        }

        try
        {
            using var document = JsonDocument.Parse(serializedSessionStateJson);
            if (!document.RootElement.TryGetProperty("stateBag", out var stateBag) ||
                !stateBag.TryGetProperty("InMemoryChatHistoryProvider", out var historyProvider) ||
                !historyProvider.TryGetProperty("messages", out var messages) ||
                messages.ValueKind != JsonValueKind.Array)
            {
                return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            }

            var callsById = new Dictionary<string, SessionToolCall>(StringComparer.Ordinal);
            var outputFilesByToolName = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            foreach (var message in messages.EnumerateArray())
            {
                if (!message.TryGetProperty("contents", out var contents) ||
                    contents.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var content in contents.EnumerateArray())
                {
                    if (!content.TryGetProperty("$type", out var typeElement))
                    {
                        continue;
                    }

                    var contentType = typeElement.GetString();
                    if (string.Equals(contentType, "functionCall", StringComparison.Ordinal))
                    {
                        var callId = content.TryGetProperty("callId", out var callIdElement)
                            ? callIdElement.GetString()
                            : null;
                        var toolName = content.TryGetProperty("name", out var nameElement)
                            ? NormalizeToolToken(nameElement.GetString() ?? string.Empty)
                            : string.Empty;
                        var outputFileName = TryResolveSessionToolOutputFileName(content);
                        if (!string.IsNullOrWhiteSpace(callId) &&
                            !string.IsNullOrWhiteSpace(toolName) &&
                            !string.IsNullOrWhiteSpace(outputFileName))
                        {
                            callsById[callId] = new SessionToolCall(toolName, outputFileName);
                        }

                        continue;
                    }

                    if (!string.Equals(contentType, "functionResult", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var resultCallId = content.TryGetProperty("callId", out var resultCallIdElement)
                        ? resultCallIdElement.GetString()
                        : null;
                    if (string.IsNullOrWhiteSpace(resultCallId) ||
                        !callsById.TryGetValue(resultCallId, out var call) ||
                        !content.TryGetProperty("result", out var resultElement) ||
                        !IsSuccessfulSessionFunctionResult(resultElement))
                    {
                        continue;
                    }

                    if (!outputFilesByToolName.TryGetValue(call.ToolName, out var outputFiles))
                    {
                        outputFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        outputFilesByToolName[call.ToolName] = outputFiles;
                    }

                    outputFiles.Add(WorkspaceScopeDescriptor.NormalizeRelativePath(call.OutputFileName));
                }
            }

            return outputFilesByToolName.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        }
    }

    private static bool IsSuccessfulSessionFunctionResult(JsonElement result)
    {
        switch (result.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            {
                return false;
            }
            case JsonValueKind.False:
            {
                return false;
            }
            case JsonValueKind.True:
            case JsonValueKind.Number:
            {
                return true;
            }
            case JsonValueKind.String:
            {
                var text = result.GetString();
                return !string.IsNullOrWhiteSpace(text) &&
                       !text.TrimStart().StartsWith("Error", StringComparison.OrdinalIgnoreCase);
            }
            case JsonValueKind.Array:
            {
                return result.GetArrayLength() > 0;
            }
            case JsonValueKind.Object:
            {
                if (result.TryGetProperty("succeeded", out var succeededElement))
                {
                    return succeededElement.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.String when bool.TryParse(succeededElement.GetString(), out var succeeded) => succeeded,
                        _ => false
                    };
                }

                if (result.TryGetProperty("receipt", out var receiptElement) &&
                    receiptElement.ValueKind == JsonValueKind.Object &&
                    receiptElement.TryGetProperty("outcome", out var outcomeElement))
                {
                    var outcome = outcomeElement.GetString();
                    return !string.IsNullOrWhiteSpace(outcome) &&
                           !outcome.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) &&
                           !outcome.StartsWith("Denied", StringComparison.OrdinalIgnoreCase) &&
                           !outcome.StartsWith("TimedOut", StringComparison.OrdinalIgnoreCase);
                }

                if (result.TryGetProperty("$type", out _))
                {
                    return true;
                }

                return result.EnumerateObject().Any();
            }
            default:
            {
                return false;
            }
        }
    }

    private static string? TryResolveSessionToolOutputFileName(JsonElement functionCallContent)
    {
        if (!functionCallContent.TryGetProperty("arguments", out var argumentsElement) ||
            argumentsElement.ValueKind != JsonValueKind.Object ||
            !argumentsElement.TryGetProperty("filename", out var fileNameElement) ||
            fileNameElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var fileName = fileNameElement.GetString();
        return string.IsNullOrWhiteSpace(fileName)
            ? null
            : fileName.Trim();
    }

}
