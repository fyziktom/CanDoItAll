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
    private static IReadOnlyList<string> ResolveRequiredToolNames(DispatchCandidate candidate)
        => RequiredToolResolver.Resolve(candidate).ToolNames;

    private static IReadOnlyList<string> ResolveRequiredToolNamesCore(
        DispatchCandidate candidate,
        string? additionalGroundingText)
    {
        var requiredToolNames = new SortedSet<string>(StringComparer.Ordinal);
        var workBriefText = candidate.WorkBrief?.WorkBriefText;
        if (!string.IsNullOrWhiteSpace(workBriefText))
        {
            foreach (var toolName in RequiredToolNameRegex.Matches(workBriefText)
                         .Where(match => !IsNegatedRequiredToolReference(workBriefText, match))
                         .Select(match => NormalizeToolToken(match.Value))
                         .Where(IsHardRequiredProcessToolName)
                         .Where(toolName => ShouldKeepHardRequiredToolName(candidate, toolName)))
            {
                requiredToolNames.Add(toolName);
            }
        }

        foreach (var toolName in ResolveImplicitRequiredToolNames(candidate, additionalGroundingText))
        {
            requiredToolNames.Add(toolName);
        }

        return requiredToolNames.ToList();
    }

    private static IReadOnlyList<string> ResolveImplicitRequiredToolNames(
        DispatchCandidate candidate,
        string? additionalGroundingText)
    {
        var requiredToolNames = new List<string>();
        if (HasProjectStructureContext(candidate))
        {
            requiredToolNames.Add(AgentToolInvocationPolicyMetadata.ProjectStructureRead);
        }

        if (RequiresGovernedInspection(candidate.StepRun))
        {
            requiredToolNames.AddRange(GovernedInspectionToolNames);
        }

        if (IsDotNetSolutionSetupScaffoldMutationStep(candidate))
        {
            requiredToolNames.AddRange(ImplementationProofToolNames);
            requiredToolNames.Add(ToolContractCatalog.WorkspaceCreateDirectory);
            requiredToolNames.Add(ToolContractCatalog.WorkspaceDotNetNew);
        }
        else if (RequiresConcreteImplementationProof(candidate))
        {
            requiredToolNames.AddRange(ImplementationProofToolNames);
            requiredToolNames.Add(ToolContractCatalog.WorkspaceWriteFile);

            var requiresDotNetValidation = ImplementationContractMentionsDotNet(candidate);
            if (requiresDotNetValidation &&
                ContainsRunnableApplicationContractSignal(candidate))
            {
                requiredToolNames.Add(ToolContractCatalog.WorkspaceDotNetBuild);
                requiredToolNames.Add(ToolContractCatalog.WorkspaceDotNetRun);
            }

            if (requiresDotNetValidation &&
                ImplementationContractMentionsTests(candidate))
            {
                requiredToolNames.Add(ToolContractCatalog.WorkspaceDotNetTest);
            }
        }

        if (BrowserProofRequirementResolver.Resolve(candidate, additionalGroundingText).IsRequired)
        {
            requiredToolNames.AddRange(ImplicitBrowserProofToolNames);
            if (!ImplementationContractMentionsDotNet(candidate, additionalGroundingText) &&
                ImplementationContractMentionsJavaScript(candidate, additionalGroundingText))
            {
                requiredToolNames.Add(ToolContractCatalog.WorkspacePowerShellRunScript);
            }
        }

        if (RequiresDurableTextArtifactWrite(candidate))
        {
            requiredToolNames.Add(ToolContractCatalog.WorkspaceWriteFile);
        }

        var requiresProjectStructureWriteback = RequiresProjectStructureWriteback(candidate);
        var requiresProjectStructureAssetWriteback = RequiresProjectStructureAssetWriteback(candidate);
        if (requiresProjectStructureWriteback)
        {
            requiredToolNames.Add(AgentToolInvocationPolicyMetadata.ProjectStructureNodeCreate);
        }

        if (requiresProjectStructureAssetWriteback)
        {
            requiredToolNames.Add(AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate);
        }

        if (RequiresRuntimeCleanupCommand(candidate))
        {
            requiredToolNames.Add(ToolContractCatalog.WorkspacePowerShellRunScript);
        }

        return requiredToolNames;
    }

    private static bool RequiresGovernedStepOutcome(ProcessStepRun stepRun)
    {
        return stepRun.StepKind != ProcessStepKind.Start;
    }

    private static bool CanImplicitlyCompleteGovernedStep(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        IReadOnlyCollection<string> missingRequiredTools,
        string? responseText)
    {
        return false;
    }

    private static bool CanImplicitlyCompleteGovernedImplementationStep(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        IReadOnlyCollection<string> missingRequiredTools,
        string? responseText)
    {
        if (!RequiresGovernedStepOutcome(candidate.StepRun) ||
            !RequiresConcreteImplementationProof(candidate) ||
            candidate.BranchOutcomes.Count > 0 ||
            candidate.RequiresExplicitBranchOutcomeSelection ||
            detail.Run.State != ProcessAutomationExecutionState.Completed ||
            detail.Run.PendingApprovals.Count > 0 ||
            detail.Run.Outcome != ProcessAutomationRunOutcome.Succeeded ||
            missingRequiredTools.Count > 0)
        {
            return false;
        }

        if (ResolveUnresolvedCriticalToolFailures(candidate, detail).Count > 0 ||
            TryResolveRecoverableProviderFailure(detail, responseText, out _) ||
            !string.IsNullOrWhiteSpace(ResolveMissingRequiredArtifactSummary(candidate, detail, responseText)) ||
            !string.IsNullOrWhiteSpace(ResolveIncompleteImplementationSummary(candidate, responseText)) ||
            !string.IsNullOrWhiteSpace(ResolveMissingConcreteImplementationProofSummary(candidate, detail)) ||
            TryResolveDeclaredStepOutcome(candidate, responseText, out _))
        {
            return false;
        }

        if (detail.Artifacts.Count == 0)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(detail.Run.ResultSummary) ||
               !string.IsNullOrWhiteSpace(ResolveRecoveredExecutionResponseText(detail));
    }

    private static bool CanImplicitlyCompleteGovernedArtifactResponseStep(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        IReadOnlyCollection<string> missingRequiredTools,
        string? responseText)
    {
        if (!RequiresGovernedStepOutcome(candidate.StepRun) ||
            RequiresConcreteImplementationProof(candidate) ||
            candidate.ExpectedArtifacts.Count == 0 ||
            candidate.BranchOutcomes.Count > 0 ||
            candidate.RequiresExplicitBranchOutcomeSelection ||
            detail.Run.State != ProcessAutomationExecutionState.Completed ||
            detail.Run.PendingApprovals.Count > 0 ||
            detail.Run.Outcome != ProcessAutomationRunOutcome.Succeeded ||
            missingRequiredTools.Count > 0)
        {
            return false;
        }

        if (ResolveUnresolvedCriticalToolFailures(candidate, detail).Count > 0 ||
            TryResolveRecoverableProviderFailure(detail, responseText, out _) ||
            !string.IsNullOrWhiteSpace(ResolveMissingConcreteProofSummary(candidate, responseText)) ||
            !string.IsNullOrWhiteSpace(ResolveIncompleteImplementationSummary(candidate, responseText)) ||
            !string.IsNullOrWhiteSpace(ResolveMissingRequiredArtifactSummary(candidate, detail, responseText)) ||
            TryResolveDeclaredStepOutcome(candidate, responseText, out _))
        {
            return false;
        }

        return HasRequiredArtifactResponseSections(candidate, responseText);
    }

    private static bool RequiresConcreteImplementationProof(DispatchCandidate candidate)
    {
        if (IsDotNetSolutionSetupScaffoldMutationStep(candidate))
        {
            return false;
        }

        return candidate.StepRun.StepKind == ProcessStepKind.Work &&
               (candidate.StepRun.Title.Contains("implement", StringComparison.OrdinalIgnoreCase) ||
                candidate.ExpectedArtifacts.Any(item =>
                    item.ArtifactKind == ProcessArtifactKind.Deliverable &&
                    item.Title.Contains("change set", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool RequiresConcreteImplementationReview(DispatchCandidate candidate)
    {
        return candidate.StepRun.Title.Contains("peer review", StringComparison.OrdinalIgnoreCase) ||
               candidate.StepRun.Title.Contains("integration readiness", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasRequiredArtifactResponseSections(
        DispatchCandidate candidate,
        string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return false;
        }

        var requiredArtifactTitles = candidate.ExpectedArtifacts
            .Where(item => item.IsRequired)
            .Select(item => item.Title?.Trim())
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (requiredArtifactTitles.Count == 0)
        {
            return false;
        }

        return requiredArtifactTitles.All(title => ContainsArtifactResponseSection(responseText, title!));
    }

    private static bool ContainsArtifactResponseSection(string responseText, string artifactTitle)
    {
        if (string.IsNullOrWhiteSpace(responseText) || string.IsNullOrWhiteSpace(artifactTitle))
        {
            return false;
        }

        var escapedTitle = Regex.Escape(artifactTitle.Trim());
        if (Regex.IsMatch(
                responseText,
                $@"(^|\r?\n)\s{{0,3}}(?:#+\s*)?{escapedTitle}\s*(?:\r?\n|:)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return true;
        }

        return false;
    }

    private static bool RequiresGovernedInspection(ProcessStepRun stepRun)
    {
        return stepRun.StepKind is not ProcessStepKind.Start and not ProcessStepKind.Work;
    }

    private static bool RequiresDurableTextArtifactWrite(DispatchCandidate candidate)
    {
        return candidate.ExpectedArtifacts.Any(item =>
        {
            if (!item.IsRequired)
            {
                return false;
            }

            if (!TryExtractExpectedArtifactRelativePath(item.ValidationRequirementSummary, out var relativePath))
            {
                return false;
            }

            return IsResponseProjectableTextArtifact(relativePath);
        });
    }

    private static bool RequiresProjectStructureWriteback(DispatchCandidate candidate)
    {
        if (!AllowsProjectStructureWriteback(candidate.StepDefinition))
        {
            return false;
        }

        var contractText = CollapsePromptWhitespace(string.Join(
            ' ',
            candidate.StepRun.Title,
            candidate.StepDefinition.Title,
            candidate.StepDefinition.InputContractSummary,
            candidate.StepDefinition.OutputContractSummary,
            candidate.StepDefinition.EvidenceContractSummary,
            candidate.StepDefinition.ExceptionPolicySummary,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary,
            string.Join(' ', candidate.ExpectedArtifacts.Select(item => item.Title)),
            string.Join(' ', candidate.ExpectedArtifacts.Select(item => item.ValidationRequirementSummary))));

        if (string.IsNullOrWhiteSpace(contractText))
        {
            return false;
        }

        var normalized = contractText.ToLowerInvariant();
        if (normalized.Contains("project_structure_node_create", StringComparison.Ordinal) ||
            normalized.Contains("project_structure_node_update", StringComparison.Ordinal))
        {
            return true;
        }

        if (candidate.ExpectedArtifacts.Any(item =>
                ContainsProjectStructureResultWritebackSignal(item.Title) ||
                ContainsProjectStructureResultWritebackSignal(item.ValidationRequirementSummary)))
        {
            return true;
        }

        return candidate.StepRun.Title.Contains("record", StringComparison.OrdinalIgnoreCase) &&
               normalized.Contains("project-structure", StringComparison.Ordinal) &&
               (normalized.Contains("writeback", StringComparison.Ordinal) ||
                normalized.Contains("write back", StringComparison.Ordinal));
    }

    private static bool AllowsProjectStructureWriteback(ProcessStepDefinition stepDefinition)
    {
        var allowedOperations = ProcessStepOperationContractState.NormalizeDeclaredAllowedOperations(
            stepDefinition.StepKind,
            stepDefinition.AllowedOperations,
            stepDefinition.OperationTargetScope);

        return allowedOperations.Contains(ProcessStepOperation.ExecuteExternalAction) ||
               stepDefinition.OperationTargetScope == ProcessStepTargetScope.ExternalActionControlled;
    }

    private static bool RequiresProjectStructureAssetWriteback(DispatchCandidate candidate)
    {
        if (!AllowsProjectStructureWriteback(candidate.StepDefinition))
        {
            return false;
        }

        var contractText = CollapsePromptWhitespace(string.Join(
                ' ',
                candidate.StepRun.Title,
                candidate.StepDefinition.Title,
                candidate.StepDefinition.Notes,
                candidate.StepDefinition.EvidenceContractSummary,
                candidate.WorkBrief?.Title,
                candidate.WorkBrief?.WorkBriefText,
                candidate.WorkBrief?.EvidenceExpectationSummary,
                string.Join(" ", candidate.ExpectedArtifacts.Select(item => item.Title)),
                string.Join(" ", candidate.ExpectedArtifacts.Select(item => item.ValidationRequirementSummary))))
            .ToLowerInvariant();
        var requiredToolReferences = RequiredToolNameRegex.Matches(contractText);
        var hasExplicitAssetCreate = requiredToolReferences
            .Any(match =>
                string.Equals(
                    NormalizeToolToken(match.Value),
                    AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate,
                    StringComparison.Ordinal) &&
                !IsNegatedRequiredToolReference(contractText, match));
        if (hasExplicitAssetCreate)
        {
            return true;
        }

        var heuristicText = RequiredToolNameRegex.Replace(
            contractText,
            match => IsNegatedRequiredToolReference(contractText, match) ? " " : match.Value);
        return heuristicText.Contains("project-structure", StringComparison.Ordinal) &&
               heuristicText.Contains("asset", StringComparison.Ordinal) &&
               (heuristicText.Contains("writeback", StringComparison.Ordinal) ||
                heuristicText.Contains("write back", StringComparison.Ordinal));
    }

    private static bool ContainsProjectStructureResultWritebackSignal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.ToLowerInvariant();
        return normalized.Contains("project-structure", StringComparison.Ordinal) &&
               normalized.Contains("result", StringComparison.Ordinal) &&
               (normalized.Contains("writeback", StringComparison.Ordinal) ||
                normalized.Contains("write back", StringComparison.Ordinal));
    }

    private static bool HasProjectStructureContext(DispatchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return ProcessProjectStructureContextFormatter.TryParse(candidate.Run.TriggerReason, out _);
    }

    private static bool RequiresConcreteBrowserProof(
        DispatchCandidate candidate,
        string? additionalGroundingText = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (candidate.StepRun.StepKind == ProcessStepKind.Start)
        {
            return false;
        }

        if (IsDotNetSolutionSetupScaffoldMutationStep(candidate))
        {
            return false;
        }

        if (IsPlanningArchitectureOrBoundaryStep(candidate))
        {
            return false;
        }

        if (TryResolvePersistedOperationContract(candidate.StepDefinition, out var persistedContract) &&
            !persistedContract.AllowedOperations.Contains(ProcessStepOperation.CaptureRuntimeProof))
        {
            return false;
        }

        var triggerText = ProcessProjectStructureContextFormatter.RemoveSerializedContext(candidate.Run.TriggerReason);
        var recoveryDirectiveText = CollapsePromptWhitespace(candidate.ManualRecoveryDirective);
        var stepContractText = CollapsePromptWhitespace(string.Join(
            ' ',
            candidate.StepRun.Title,
            candidate.StepRun.CurrentExecutorName,
            candidate.StepRun.RoleSnapshotSummary,
            candidate.StepDefinition.Title,
            candidate.StepDefinition.InputContractSummary,
            candidate.StepDefinition.OutputContractSummary,
            candidate.StepDefinition.EvidenceContractSummary,
            candidate.WorkBrief?.Title,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary,
            string.Join(' ', candidate.ExpectedArtifacts.Select(item => item.Title)),
            string.Join(' ', candidate.ExpectedArtifacts.Select(item => item.ValidationRequirementSummary))));
        var surfaceContextText = CollapsePromptWhitespace(string.Join(
            ' ',
            triggerText,
            additionalGroundingText,
            stepContractText,
            string.Join(' ', candidate.ArtifactInputs.Select(item => item.SourceStepTitle)),
            string.Join(' ', candidate.ArtifactInputs.Select(item => item.ExpectedArtifactTitle)),
            string.Join(' ', candidate.ArtifactInputs.SelectMany(item => item.Artifacts).Select(item => item.Title)),
            string.Join(' ', candidate.ArtifactInputs.SelectMany(item => item.Artifacts).Select(item => item.ArtifactKind)),
            string.Join(' ', candidate.ArtifactInputs.SelectMany(item => item.Artifacts).Select(item => item.ManagedStoragePath)),
            string.Join(' ', candidate.ArtifactInputs.SelectMany(item => item.Artifacts).Select(item => item.ReviewSummary)),
            string.Join(' ', candidate.ArtifactInputs.SelectMany(item => item.Artifacts).Select(item => item.ProvenanceSummary))));
        if (string.IsNullOrWhiteSpace(surfaceContextText))
        {
            return false;
        }

        if (ContainsNegatedBrowserProofInstruction(stepContractText) ||
            ContainsNegatedBrowserProofInstruction(triggerText) ||
            ContainsNegatedBrowserProofInstruction(recoveryDirectiveText))
        {
            return false;
        }

        if (IsScreenshotReviewOrStorageConsumerStep(candidate, surfaceContextText))
        {
            return false;
        }

        if (IsScreenshotCleanupOrHandoffConsumerStep(candidate, surfaceContextText))
        {
            return false;
        }

        var browserSurfaceText = RemoveApplicabilityOnlyBrowserEvidencePhrases(surfaceContextText);
        var hasBrowserSurfaceSignal = ContainsExplicitBrowserSurfaceSignal(browserSurfaceText) ||
                                      ContainsExplicitBrowserSurfaceSignal(triggerText);
        if (ContainsNonBrowserValidationTargetSignal(stepContractText) &&
            !hasBrowserSurfaceSignal)
        {
            return false;
        }

        var hasConcreteBrowserProofRequest = ContainsConcreteBrowserProofSignal(stepContractText);
        if (RequiresConcreteImplementationProof(candidate))
        {
            return hasConcreteBrowserProofRequest;
        }

        if (!RequiresConcreteBrowserEvidenceStep(stepContractText))
        {
            return false;
        }

        if (hasBrowserSurfaceSignal)
        {
            return true;
        }

        if (!hasConcreteBrowserProofRequest)
        {
            return false;
        }

        return true;
    }

    private static bool ShouldKeepHardRequiredToolName(DispatchCandidate candidate, string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return false;
        }

        if (IsDotNetSolutionSetupScaffoldMutationStep(candidate))
        {
            return toolName is
                "project_structure_read" or
                "workspace_create_directory" or
                "workspace_dotnet_new" or
                "workspace_read_file" or
                "workspace_stat_path" or
                "workspace_write_file";
        }

        if (toolName.StartsWith("workspace_dotnet_", StringComparison.Ordinal) &&
            IsJavaScriptOnlyRunContext(candidate))
        {
            return false;
        }

        if (toolName is "project_structure_node_create" or "project_structure_node_update" or "project_structure_asset_create")
        {
            return true;
        }

        if (!toolName.StartsWith("browser_", StringComparison.Ordinal))
        {
            return true;
        }

        return !IsPlanningArchitectureOrBoundaryStep(candidate) &&
               !IsScreenshotReviewOrStorageConsumerStep(candidate, string.Empty) &&
               !IsScreenshotCleanupOrHandoffConsumerStep(candidate, string.Empty);
    }

    private static bool IsJavaScriptOnlyRunContext(DispatchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var runContextText = CollapsePromptWhitespace(string.Join(
            ' ',
            ProcessProjectStructureContextFormatter.RemoveSerializedContext(candidate.Run.TriggerReason),
            candidate.Run.Name,
            candidate.StepRun.CurrentExecutorName,
            candidate.StepRun.RoleSnapshotSummary,
            candidate.Definition.Name,
            candidate.Definition.Slug,
            candidate.StepDefinition.Key));

        return ContainsJavaScriptRuntimeSignal(runContextText) &&
               !ContainsDotNetRuntimeSignal(runContextText);
    }

    private static bool ContainsJavaScriptRuntimeSignal(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               (value.Contains("javascript", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("typescript", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("vite", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("node", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("npm", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("package.json", StringComparison.OrdinalIgnoreCase) ||
                value.Contains(" js-", StringComparison.OrdinalIgnoreCase) ||
                value.Contains(" js/", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsDotNetRuntimeSignal(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               (value.Contains(".net", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("dotnet", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("c#", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("asp.net", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("blazor", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("razor", StringComparison.OrdinalIgnoreCase) ||
                value.Contains(".csproj", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("csproj", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsScreenshotReviewOrStorageConsumerStep(DispatchCandidate candidate, string currentRunText)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (candidate.StepRun.StepKind != ProcessStepKind.Review)
        {
            return false;
        }

        var stepText = CollapsePromptWhitespace(string.Join(
            ' ',
            currentRunText,
            candidate.StepRun.Title,
            candidate.StepRun.RoleSnapshotSummary,
            candidate.WorkBrief?.Title,
            candidate.WorkBrief?.ExpectedOutcome,
            string.Join(' ', candidate.ExpectedArtifacts.Select(item => item.Title)),
            string.Join(' ', candidate.ExpectedArtifacts.Select(item => item.ValidationRequirementSummary)),
            string.Join(' ', candidate.ArtifactInputs.Select(item => item.ExpectedArtifactTitle))));
        if (string.IsNullOrWhiteSpace(stepText) ||
            !stepText.Contains("screenshot", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hasReviewOrStorageIntent = stepText.Contains("review", StringComparison.OrdinalIgnoreCase) ||
                                       stepText.Contains("store", StringComparison.OrdinalIgnoreCase) ||
                                       stepText.Contains("storage", StringComparison.OrdinalIgnoreCase) ||
                                       stepText.Contains("image asset", StringComparison.OrdinalIgnoreCase) ||
                                       stepText.Contains("asset node", StringComparison.OrdinalIgnoreCase);
        if (!hasReviewOrStorageIntent)
        {
            return false;
        }

        var hasStorageOutput = candidate.ExpectedArtifacts.Any(item =>
            item.Title.Contains("review", StringComparison.OrdinalIgnoreCase) ||
            item.Title.Contains("storage receipt", StringComparison.OrdinalIgnoreCase) ||
            item.Title.Contains("image asset", StringComparison.OrdinalIgnoreCase) ||
            item.ValidationRequirementSummary.Contains("image asset", StringComparison.OrdinalIgnoreCase) ||
            item.ValidationRequirementSummary.Contains("storage locator", StringComparison.OrdinalIgnoreCase));
        if (!hasStorageOutput)
        {
            return false;
        }

        return candidate.ArtifactInputs.Count == 0 ||
               candidate.ArtifactInputs.Any(item =>
                   item.ExpectedArtifactTitle.Contains("screenshot", StringComparison.OrdinalIgnoreCase) ||
                   item.ExpectedArtifactTitle.Contains("browser", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsScreenshotCleanupOrHandoffConsumerStep(DispatchCandidate candidate, string currentRunText)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (candidate.StepRun.StepKind != ProcessStepKind.End)
        {
            return false;
        }

        var stepText = CollapsePromptWhitespace(string.Join(
            ' ',
            currentRunText,
            candidate.StepRun.Title,
            candidate.StepRun.RoleSnapshotSummary,
            candidate.WorkBrief?.Title,
            candidate.WorkBrief?.ExpectedOutcome,
            string.Join(' ', candidate.ExpectedArtifacts.Select(item => item.Title)),
            string.Join(' ', candidate.ExpectedArtifacts.Select(item => item.ValidationRequirementSummary)),
            string.Join(' ', candidate.ArtifactInputs.Select(item => item.ExpectedArtifactTitle))));
        if (string.IsNullOrWhiteSpace(stepText) ||
            !stepText.Contains("screenshot", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hasCleanupOrHandoffIntent = stepText.Contains("cleanup", StringComparison.OrdinalIgnoreCase) ||
                                        stepText.Contains("clean up", StringComparison.OrdinalIgnoreCase) ||
                                        stepText.Contains("handoff", StringComparison.OrdinalIgnoreCase) ||
                                        stepText.Contains("close runtime", StringComparison.OrdinalIgnoreCase) ||
                                        stepText.Contains("stop once", StringComparison.OrdinalIgnoreCase);
        if (!hasCleanupOrHandoffIntent)
        {
            return false;
        }

        return candidate.ArtifactInputs.Count == 0 ||
               candidate.ArtifactInputs.Any(item =>
                   item.ExpectedArtifactTitle.Contains("review", StringComparison.OrdinalIgnoreCase) ||
                   item.ExpectedArtifactTitle.Contains("storage", StringComparison.OrdinalIgnoreCase) ||
                   item.ExpectedArtifactTitle.Contains("asset", StringComparison.OrdinalIgnoreCase) ||
                   item.ExpectedArtifactTitle.Contains("screenshot", StringComparison.OrdinalIgnoreCase));
    }

    private static bool RequiresRuntimeCleanupCommand(DispatchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (candidate.StepRun.StepKind != ProcessStepKind.End)
        {
            return false;
        }

        var stepText = CollapsePromptWhitespace(string.Join(
            ' ',
            candidate.StepRun.Title,
            candidate.StepRun.RoleSnapshotSummary,
            candidate.WorkBrief?.Title,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary,
            candidate.StepDefinition.InputContractSummary,
            candidate.StepDefinition.OutputContractSummary,
            candidate.StepDefinition.EvidenceContractSummary,
            string.Join(' ', candidate.ExpectedArtifacts.Select(item => item.Title)),
            string.Join(' ', candidate.ExpectedArtifacts.Select(item => item.ValidationRequirementSummary))));
        if (string.IsNullOrWhiteSpace(stepText))
        {
            return false;
        }

        var hasCleanupIntent = stepText.Contains("cleanup", StringComparison.OrdinalIgnoreCase) ||
                               stepText.Contains("clean up", StringComparison.OrdinalIgnoreCase) ||
                               stepText.Contains("stop", StringComparison.OrdinalIgnoreCase) ||
                               stepText.Contains("shutdown", StringComparison.OrdinalIgnoreCase) ||
                               stepText.Contains("close runtime", StringComparison.OrdinalIgnoreCase);
        if (!hasCleanupIntent)
        {
            return false;
        }

        return stepText.Contains("app process", StringComparison.OrdinalIgnoreCase) ||
               stepText.Contains("application process", StringComparison.OrdinalIgnoreCase) ||
               stepText.Contains("managed run", StringComparison.OrdinalIgnoreCase) ||
               stepText.Contains("process tree", StringComparison.OrdinalIgnoreCase) ||
               stepText.Contains("runtime session", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlanningArchitectureOrBoundaryStep(DispatchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var stepText = CollapsePromptWhitespace(string.Join(
            ' ',
            candidate.StepRun.Title,
            candidate.StepRun.CurrentExecutorName,
            candidate.StepRun.RoleSnapshotSummary,
            candidate.WorkBrief?.Title,
            candidate.WorkBrief?.ExpectedOutcome,
            string.Join(' ', candidate.ExpectedArtifacts.Select(item => item.Title))));
        if (string.IsNullOrWhiteSpace(stepText))
        {
            return false;
        }

        if (ContainsConcreteQualityProofStepSignal(stepText))
        {
            return false;
        }

        return stepText.Contains("scope", StringComparison.OrdinalIgnoreCase) ||
               stepText.Contains("intake", StringComparison.OrdinalIgnoreCase) ||
               stepText.Contains("boundary", StringComparison.OrdinalIgnoreCase) ||
               stepText.Contains("planning", StringComparison.OrdinalIgnoreCase) ||
               stepText.Contains("architecture", StringComparison.OrdinalIgnoreCase) ||
               stepText.Contains("architect", StringComparison.OrdinalIgnoreCase) ||
               stepText.Contains("source-of-truth", StringComparison.OrdinalIgnoreCase) ||
               stepText.Contains("source of truth", StringComparison.OrdinalIgnoreCase) ||
               stepText.Contains("decision record", StringComparison.OrdinalIgnoreCase) ||
               stepText.Contains("delegation", StringComparison.OrdinalIgnoreCase) ||
               stepText.Contains("allowed tools", StringComparison.OrdinalIgnoreCase) ||
               stepText.Contains("refusal conditions", StringComparison.OrdinalIgnoreCase) ||
               stepText.Contains("naming-contract", StringComparison.OrdinalIgnoreCase) ||
               stepText.Contains("naming contract", StringComparison.OrdinalIgnoreCase) ||
               stepText.Contains("scaffold contract", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDotNetSolutionSetupScaffoldMutationStep(DispatchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!string.Equals(candidate.Definition.Slug, "dotnet-solution-setup", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(candidate.Definition.Name, ".NET solution setup subprocess", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(candidate.StepDefinition.Key, "create-dotnet-project", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(candidate.StepDefinition.Key, "add-test-project", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsConcreteQualityProofStepSignal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains("qa", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("quality validation", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("regression evidence", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("test evidence", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("browser proof", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("runtime proof", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("screenshot", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("smoke proof", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("release readiness", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("evaluate outputs", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("review rendered", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresConcreteBrowserEvidenceStep(string currentRunText)
    {
        if (string.IsNullOrWhiteSpace(currentRunText))
        {
            return false;
        }

        var normalized = RemoveApplicabilityOnlyBrowserEvidencePhrases(currentRunText);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return ContainsConcreteBrowserProofSignal(normalized) ||
               normalized.Contains("browser evidence", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("browser validation", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("browser-visible", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("runtime proof", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("runtime evidence", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("visual validation", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("rendered ui", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("rendered page", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("visible behavior", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("playwright", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsExplicitBrowserSurfaceSignal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains("browser app", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("web app", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("web application", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("web page", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("webpage", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("web site", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("website", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("static web", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("browser-facing", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("browser visible", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("browser-playable", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("web ui", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("frontend", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("front-end", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("index.html", StringComparison.OrdinalIgnoreCase) ||
               value.Contains(".html", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("playwright", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("blazor", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("razor component", StringComparison.OrdinalIgnoreCase) ||
               Regex.IsMatch(
                   value,
                   @"(?:^|[^a-z0-9])(?:ui|ux|react|vue|svelte|vite|css|dom)(?:[^a-z0-9]|$)",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool ContainsNonBrowserValidationTargetSignal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains("console app", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("console application", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("command-line", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("command line", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("terminal app", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("terminal application", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("cli app", StringComparison.OrdinalIgnoreCase) ||
               Regex.IsMatch(
                   value,
                   @"(?:^|[^a-z0-9])cli(?:[^a-z0-9]|$)",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               value.Contains("no web ui", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("no browser", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("non-browser", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("minimal api", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("web api", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("rest api", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("worker service", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("background service", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("class library", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("business plan", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("marketing plan", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("spreadsheet", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("presentation", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("document", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsNegatedBrowserProofInstruction(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Regex.IsMatch(
                   value,
                   @"\b(?:do\s+not|don't|must\s+not|should\s+not|never)\s+(?:use|run|start|capture|take|record)?\s*(?:playwright|browser\s+tools?|browser[-\s]+proof|web\s+app)\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   value,
                   @"\bbrowser[-\s]+proof\s+is\s+not\s+applicable\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   value,
                   @"\b(?:does\s+not|doesn't|do\s+not|don't|is\s+not|isn't|are\s+not|aren't)\s+require\s+(?:mandatory\s+)?(?:browser[-\s]+proof|browser\s+proof\s+tools?|browser\s+tools?|browser\s+evidence|runtime\s+or\s+browser\s+proof)\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   value,
                   @"\b(?:browser[-\s]+proof|browser\s+proof\s+tools?|browser\s+tools?|browser\s+evidence|browser_snapshot|browser_take_screenshot|browser_console_messages)\s+(?:is|are)\s+not\s+(?:required|mandatory|gating|a\s+gate|release[-\s]+blocking)\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   value,
                   @"\b(?:do\s+not|don't|must\s+not|should\s+not|never)\s+(?:capture|take|create|record)\s+(?:a\s+|any\s+)?screenshots?\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

}
