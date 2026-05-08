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

        foreach (var toolName in ResolveImplicitRequiredToolNames(candidate))
        {
            requiredToolNames.Add(toolName);
        }

        return requiredToolNames.ToList();
    }

    private static IReadOnlyList<string> ResolveImplicitRequiredToolNames(DispatchCandidate candidate)
    {
        var requiredToolNames = new List<string>();
        if (HasProjectStructureContext(candidate))
        {
            requiredToolNames.Add("project_structure_read");
        }

        if (RequiresGovernedInspection(candidate.StepRun))
        {
            requiredToolNames.AddRange(GovernedInspectionToolNames);
        }

        if (IsDotNetSolutionSetupScaffoldMutationStep(candidate))
        {
            requiredToolNames.AddRange(ImplementationProofToolNames);
            requiredToolNames.Add("workspace_create_directory");
            requiredToolNames.Add("workspace_dotnet_new");
        }
        else if (RequiresConcreteImplementationProof(candidate))
        {
            requiredToolNames.AddRange(ImplementationProofToolNames);
            requiredToolNames.Add("workspace_write_file");

            var requiresDotNetValidation = ImplementationContractMentionsDotNet(candidate);
            if (requiresDotNetValidation &&
                ContainsRunnableApplicationContractSignal(candidate))
            {
                requiredToolNames.Add("workspace_dotnet_build");
                requiredToolNames.Add("workspace_dotnet_run");
            }

            if (requiresDotNetValidation &&
                ImplementationContractMentionsTests(candidate))
            {
                requiredToolNames.Add("workspace_dotnet_test");
            }
        }

        if (RequiresConcreteBrowserProof(candidate))
        {
            requiredToolNames.AddRange(ImplicitBrowserProofToolNames);
            if (ImplementationContractMentionsJavaScript(candidate))
            {
                requiredToolNames.Add("workspace_pwsh_run_script");
            }
        }

        if (RequiresDurableTextArtifactWrite(candidate))
        {
            requiredToolNames.Add("workspace_write_file");
        }

        return requiredToolNames;
    }

    private static bool RequiresGovernedStepOutcome(ProcessStepRun stepRun)
    {
        return stepRun.StepKind != ProcessStepKind.Start;
    }

    private static bool CanImplicitlyCompleteGovernedStep(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        IReadOnlyCollection<string> missingRequiredTools,
        string? responseText)
    {
        return false;
    }

    private static bool CanImplicitlyCompleteGovernedImplementationStep(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        IReadOnlyCollection<string> missingRequiredTools,
        string? responseText)
    {
        if (!RequiresGovernedStepOutcome(candidate.StepRun) ||
            !RequiresConcreteImplementationProof(candidate) ||
            candidate.BranchOutcomes.Count > 0 ||
            candidate.RequiresExplicitBranchOutcomeSelection ||
            detail.Run.State != ExecutionState.Completed ||
            detail.Run.PendingApprovals.Count > 0 ||
            detail.Run.Outcome != RunOutcome.Succeeded ||
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
        ExecutionRunDetail detail,
        IReadOnlyCollection<string> missingRequiredTools,
        string? responseText)
    {
        if (!RequiresGovernedStepOutcome(candidate.StepRun) ||
            RequiresConcreteImplementationProof(candidate) ||
            candidate.ExpectedArtifacts.Count == 0 ||
            candidate.BranchOutcomes.Count > 0 ||
            candidate.RequiresExplicitBranchOutcomeSelection ||
            detail.Run.State != ExecutionState.Completed ||
            detail.Run.PendingApprovals.Count > 0 ||
            detail.Run.Outcome != RunOutcome.Succeeded ||
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

    private static bool HasProjectStructureContext(DispatchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return ProcessProjectStructureContextFormatter.TryParse(candidate.Run.TriggerReason, out _);
    }

    private static bool RequiresConcreteBrowserProof(DispatchCandidate candidate)
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

        var triggerText = ProcessProjectStructureContextFormatter.RemoveSerializedContext(candidate.Run.TriggerReason);
        var currentRunText = CollapsePromptWhitespace(string.Join(
            ' ',
            triggerText,
            candidate.WorkBrief?.Title,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary,
            string.Join(' ', candidate.ExpectedArtifacts.Select(item => item.Title)),
            string.Join(' ', candidate.ExpectedArtifacts.Select(item => item.ValidationRequirementSummary))));
        if (string.IsNullOrWhiteSpace(currentRunText))
        {
            return false;
        }

        if (ContainsNonBrowserValidationTargetSignal(currentRunText))
        {
            return false;
        }

        var browserSurfaceText = RemoveApplicabilityOnlyBrowserEvidencePhrases(currentRunText);
        var hasConcreteBrowserProofRequest = ContainsConcreteBrowserProofSignal(currentRunText) ||
                                             ContainsConcreteBrowserProofSignal(triggerText);
        if (RequiresConcreteImplementationProof(candidate))
        {
            return hasConcreteBrowserProofRequest;
        }

        if (!RequiresConcreteBrowserEvidenceStep(currentRunText))
        {
            return false;
        }

        if (ContainsExplicitBrowserSurfaceSignal(browserSurfaceText))
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

        if (!toolName.StartsWith("browser_", StringComparison.Ordinal))
        {
            return true;
        }

        return !IsPlanningArchitectureOrBoundaryStep(candidate);
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

        return currentRunText.Contains("qa", StringComparison.OrdinalIgnoreCase) ||
               currentRunText.Contains("validation", StringComparison.OrdinalIgnoreCase) ||
               currentRunText.Contains("browser proof", StringComparison.OrdinalIgnoreCase) ||
               currentRunText.Contains("runtime proof", StringComparison.OrdinalIgnoreCase) ||
               currentRunText.Contains("screenshot", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsExplicitBrowserSurfaceSignal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains("browser app", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("browser-facing", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("browser visible", StringComparison.OrdinalIgnoreCase) ||
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

}
