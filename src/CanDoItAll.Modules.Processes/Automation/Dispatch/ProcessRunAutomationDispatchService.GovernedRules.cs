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
                         .Where(IsHardRequiredProcessToolName))
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

        if (RequiresConcreteImplementationProof(candidate))
        {
            requiredToolNames.AddRange(ImplementationProofToolNames);
        }

        if (RequiresConcreteBrowserProof(candidate))
        {
            requiredToolNames.AddRange(ImplicitBrowserProofToolNames);
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

        if (ResolveUnresolvedCriticalToolFailures(detail).Count > 0 ||
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

        if (ResolveUnresolvedCriticalToolFailures(detail).Count > 0 ||
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

        return ContainsConcreteBrowserProofSignal(candidate.WorkBrief?.Title) ||
               ContainsConcreteBrowserProofSignal(candidate.WorkBrief?.WorkBriefText) ||
               ContainsConcreteBrowserProofSignal(candidate.WorkBrief?.ExpectedOutcome) ||
               ContainsConcreteBrowserProofSignal(candidate.WorkBrief?.EvidenceExpectationSummary) ||
               candidate.ExpectedArtifacts.Any(item =>
                   ContainsConcreteBrowserProofSignal(item.Title) ||
                   ContainsConcreteBrowserProofSignal(item.ValidationRequirementSummary));
    }

}
