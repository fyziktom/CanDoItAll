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
using System.Xml;
using System.Xml.Linq;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static string ResolveMissingConcreteImplementationProofSummary(
        DispatchCandidate candidate,
        ExecutionRunDetail detail)
    {
        if (!RequiresConcreteImplementationProof(candidate))
        {
            return string.Empty;
        }

        if (ResolveProcessMockArtifactProjections(detail.Run.SerializedSessionStateJson)
            .Any(projection => CanSatisfyConcreteImplementationProofWithProcessMock(candidate, projection)))
        {
            return string.Empty;
        }

        var successfulReceipts = detail.ToolReceipts
            .Where(receipt => !IsFailedToolReceipt(receipt))
            .ToList();
        var concreteReadReceipt = ResolveLatestImplementationProofReadReceipt(candidate, successfulReceipts);
        if (concreteReadReceipt is null)
        {
            return RequiresSourceOrProjectImplementationProof(candidate)
                ? "the current attempt did not read any concrete product source or project file"
                : "the current attempt did not read any concrete product deliverable, source, or project file";
        }

        var concreteMutationReceipts = successfulReceipts
            .Where(receipt => IsConcreteProductMutationToolName(NormalizeToolToken(receipt.ToolName)))
            .Where(receipt => IsConcreteProductMutationReceipt(candidate, receipt))
            .ToList();
        if (RequiresCurrentAttemptProductMutation(candidate) &&
            concreteMutationReceipts.Count == 0)
        {
            return "the current repair attempt did not mutate any concrete product file";
        }

        var latestMutationReceipt = concreteMutationReceipts
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
        if (latestMutationReceipt is not null)
        {
            var latestValidationReceipt = ResolveLatestRequiredImplementationValidationReceipt(
                candidate,
                successfulReceipts);
            var hasValidationAfterLatestMutation = latestValidationReceipt is not null &&
                                                   !IsReceiptAfter(latestMutationReceipt, latestValidationReceipt);
            if (IsReceiptAfter(latestMutationReceipt, concreteReadReceipt) &&
                !hasValidationAfterLatestMutation)
            {
                return "workspace_read_file ran before the latest concrete product mutation";
            }

            var latestBootstrapReceipt = concreteMutationReceipts
                .Where(receipt => IsImplementationBootstrapToolName(NormalizeToolToken(receipt.ToolName)))
                .OrderByDescending(receipt => receipt.CompletedAtUtc)
                .ThenByDescending(receipt => receipt.StartedAtUtc)
                .FirstOrDefault();
            if (latestBootstrapReceipt is not null &&
                !successfulReceipts.Any(receipt =>
                    ConcreteProductSourceWriteToolNames.Contains(NormalizeToolToken(receipt.ToolName)) &&
                    IsReceiptAfter(receipt, latestBootstrapReceipt) &&
                    HasConcreteProductImplementationPath(candidate, receipt)))
            {
                return "the latest scaffold or bootstrap tool was not followed by a concrete product deliverable, source, or project file write";
            }

            if (latestValidationReceipt is not null &&
                IsReceiptAfter(latestMutationReceipt, latestValidationReceipt))
            {
                return $"{latestValidationReceipt.ToolName} ran before the latest concrete product mutation";
            }
        }

        return string.Empty;
    }

    private static string ResolveMissingRunnableApplicationProofSummary(
        DispatchCandidate candidate,
        ExecutionRunDetail detail)
    {
        if (!RequiresConcreteImplementationProof(candidate))
        {
            return string.Empty;
        }

        if (IsDotNetSolutionSetupScaffoldMutationStep(candidate))
        {
            return string.Empty;
        }

        var implementationMentionsDotNet = ImplementationContractMentionsDotNet(candidate);
        if (!implementationMentionsDotNet &&
            (ImplementationContractMentionsJavaScript(candidate) || ImplementationContractNegatesDotNet(candidate)))
        {
            return string.Empty;
        }

        var successfulReceipts = detail.ToolReceipts
            .Where(receipt => !IsFailedToolReceipt(receipt))
            .ToList();
        if (!HasBuildValidationReceipt(successfulReceipts) &&
            !ContainsRunnableApplicationContractSignal(candidate))
        {
            return string.Empty;
        }

        var runnableDotNetProjectPaths = ResolveRunnableDotNetHostProjectPaths(detail, successfulReceipts);
        if (runnableDotNetProjectPaths.Count == 0)
        {
            return string.Empty;
        }

        var invalidHostSummary = ResolveInvalidRunnableDotNetHostSummary(runnableDotNetProjectPaths);
        if (!string.IsNullOrWhiteSpace(invalidHostSummary))
        {
            return invalidHostSummary;
        }

        var latestRunReceipt = ResolveLatestReceipt(
            successfulReceipts,
            IsRunValidationToolName,
            requireConcreteProductPath: true,
            requireConcreteDeliverableOrSourcePath: false);
        if (latestRunReceipt is null)
        {
            return $"the current attempt did not start the runnable .NET host with a run tool after implementation; detected host project: {runnableDotNetProjectPaths[0]}";
        }

        var latestMutationReceipt = successfulReceipts
            .Where(receipt => IsConcreteProductMutationToolName(NormalizeToolToken(receipt.ToolName)))
            .Where(receipt => IsConcreteProductMutationReceipt(candidate, receipt))
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
        if (latestMutationReceipt is not null &&
            IsReceiptAfter(latestMutationReceipt, latestRunReceipt))
        {
            return "the run tool ran before the latest concrete product mutation";
        }

        return string.Empty;
    }

    private static CarriedImplementationProof ResolveCarriedImplementationProof(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        CarriedImplementationProof previous)
    {
        if (!RequiresConcreteImplementationProof(candidate))
        {
            return previous;
        }

        var hasConcreteMutation = HasSuccessfulConcreteProductMutation(candidate, detail);
        var hasConcreteProductMutation = previous.HasConcreteProductMutation || hasConcreteMutation;
        var hasConcreteImplementationProof = hasConcreteMutation
            ? false
            : previous.HasConcreteImplementationProof;
        var hasRunnableApplicationProof = hasConcreteMutation
            ? false
            : previous.HasRunnableApplicationProof;

        if (!hasConcreteMutation &&
            previous.HasConcreteProductMutation &&
            HasConcreteImplementationProofEvidence(candidate, detail))
        {
            hasConcreteImplementationProof = true;
        }

        if (string.IsNullOrWhiteSpace(ResolveMissingConcreteImplementationProofSummary(candidate, detail)) &&
            HasConcreteImplementationProofEvidence(candidate, detail))
        {
            hasConcreteImplementationProof = true;
        }

        if (string.IsNullOrWhiteSpace(ResolveMissingRunnableApplicationProofSummary(candidate, detail)) &&
            HasRunnableApplicationProofEvidence(detail))
        {
            hasRunnableApplicationProof = true;
        }

        return new CarriedImplementationProof(hasConcreteImplementationProof, hasRunnableApplicationProof, hasConcreteProductMutation);
    }

    private static CarriedImplementationProof ResolveHistoricalCarriedImplementationProof(
        DispatchCandidate candidate,
        IEnumerable<ExecutionRunDetail> historicalDetails)
    {
        ArgumentNullException.ThrowIfNull(historicalDetails);

        if (!RequiresCurrentAttemptProductMutation(candidate))
        {
            return CarriedImplementationProof.None;
        }

        return historicalDetails.Any(detail =>
            IsHistoricalCarryForwardExecutionRun(detail.Run) &&
            HasSuccessfulConcreteProductMutation(candidate, detail))
            ? new CarriedImplementationProof(false, false, true)
            : CarriedImplementationProof.None;
    }

    private static bool IsHistoricalCarryForwardExecutionRun(ExecutionRunRecord executionRun)
    {
        return string.Equals(executionRun.RequestedBy, AutomationActor, StringComparison.OrdinalIgnoreCase) &&
               executionRun.State is ExecutionState.Completed or ExecutionState.Failed;
    }

    private static string ResolveMissingConcreteImplementationProofSummaryWithCarryForward(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        CarriedImplementationProof carriedProof)
    {
        var summary = ResolveMissingConcreteImplementationProofSummary(candidate, detail);
        if (RequiresCurrentAttemptProductMutation(candidate) &&
            carriedProof.HasConcreteProductMutation &&
            HasConcreteImplementationProofEvidence(candidate, detail) &&
            string.Equals(
                summary,
                "the current repair attempt did not mutate any concrete product file",
                StringComparison.Ordinal))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(summary) ||
            RequiresCurrentAttemptProductMutation(candidate) ||
            !carriedProof.HasConcreteImplementationProof ||
            HasSuccessfulConcreteProductMutation(candidate, detail))
        {
            return summary;
        }

        return string.Empty;
    }

    private static string ResolveMissingRunnableApplicationProofSummaryWithCarryForward(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        CarriedImplementationProof carriedProof)
    {
        var summary = ResolveMissingRunnableApplicationProofSummary(candidate, detail);
        if (string.IsNullOrWhiteSpace(summary) ||
            !carriedProof.HasRunnableApplicationProof ||
            HasSuccessfulConcreteProductMutation(candidate, detail))
        {
            return summary;
        }

        return string.Empty;
    }

    private static bool RequiresCurrentAttemptProductMutation(DispatchCandidate candidate)
    {
        if (!RequiresConcreteImplementationProof(candidate))
        {
            return false;
        }

        var text = CollapsePromptWhitespace(string.Join(
            ' ',
            candidate.StepRun.Title,
            candidate.WorkBrief?.Title,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary,
            string.Join(' ', candidate.ExpectedArtifacts.Select(item => item.Title)),
            string.Join(' ', candidate.ExpectedArtifacts.Select(item => item.ValidationRequirementSummary))));
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ContainsContractWord(text, "repair") ||
               ContainsContractWord(text, "repaired") ||
               ContainsContractWord(text, "rework") ||
               ContainsContractWord(text, "remediation") ||
               ContainsContractWord(text, "remediate") ||
               ContainsContractWord(text, "fix") ||
               ContainsContractWord(text, "fixes");
    }

    private static bool HasConcreteImplementationProofEvidence(
        DispatchCandidate candidate,
        ExecutionRunDetail detail)
    {
        if (ResolveProcessMockArtifactProjections(detail.Run.SerializedSessionStateJson)
            .Any(projection => CanSatisfyConcreteImplementationProofWithProcessMock(candidate, projection)))
        {
            return true;
        }

        var successfulReceipts = detail.ToolReceipts
            .Where(receipt => !IsFailedToolReceipt(receipt))
            .ToList();
        return ResolveLatestImplementationProofReadReceipt(candidate, successfulReceipts) is not null;
    }

    private static bool HasRunnableApplicationProofEvidence(ExecutionRunDetail detail)
    {
        var successfulReceipts = detail.ToolReceipts
            .Where(receipt => !IsFailedToolReceipt(receipt))
            .ToList();
        return ResolveLatestReceipt(
            successfulReceipts,
            IsRunValidationToolName,
            requireConcreteProductPath: true,
            requireConcreteDeliverableOrSourcePath: false) is not null;
    }

    private static bool HasSuccessfulConcreteProductMutation(
        DispatchCandidate candidate,
        ExecutionRunDetail detail)
    {
        return detail.ToolReceipts
            .Where(receipt => !IsFailedToolReceipt(receipt))
            .Any(receipt =>
                IsConcreteProductMutationToolName(NormalizeToolToken(receipt.ToolName)) &&
                IsConcreteProductMutationReceipt(candidate, receipt));
    }

    private static ToolExecutionReceiptRecord? ResolveLatestImplementationProofReadReceipt(
        DispatchCandidate candidate,
        IEnumerable<ToolExecutionReceiptRecord> successfulReceipts)
    {
        return successfulReceipts
            .Where(receipt => string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_read_file", StringComparison.Ordinal))
            .Where(receipt => HasConcreteProductPath(receipt))
            .Where(receipt => HasConcreteProductImplementationPath(candidate, receipt))
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
    }

    private static bool HasBuildValidationReceipt(IReadOnlyList<ToolExecutionReceiptRecord> successfulReceipts)
    {
        return successfulReceipts.Any(receipt =>
        {
            var toolName = NormalizeToolToken(receipt.ToolName);
            return IsBuildValidationToolName(toolName);
        });
    }

    private static bool IsBuildValidationToolName(string normalizedToolName)
    {
        return normalizedToolName.EndsWith("_build", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_test", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_publish", StringComparison.Ordinal);
    }

    private static bool IsRunValidationToolName(string normalizedToolName)
    {
        return normalizedToolName.EndsWith("_run", StringComparison.Ordinal);
    }

    private static bool ContainsRunnableApplicationContractSignal(DispatchCandidate candidate)
    {
        var textParts = new[]
            {
                candidate.StepRun.Title,
                candidate.WorkBrief?.Title,
                candidate.WorkBrief?.WorkBriefText,
                candidate.WorkBrief?.ExpectedOutcome,
                candidate.WorkBrief?.EvidenceExpectationSummary
            }
            .Concat(candidate.ExpectedArtifacts.Select(item => item.Title))
            .Concat(candidate.ExpectedArtifacts.Select(item => item.ValidationRequirementSummary));
        var text = CollapsePromptWhitespace(string.Join(' ', textParts));
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ContainsContractWord(text, "application") ||
               ContainsContractWord(text, "app") ||
               ContainsContractWord(text, "api") ||
               ContainsContractWord(text, "service") ||
               ContainsContractWord(text, "host") ||
               ContainsContractWord(text, "startup") ||
               ContainsContractWord(text, "runnable") ||
               ContainsContractWord(text, "browser") ||
               ContainsContractWord(text, "ui") ||
               text.Contains("asp.net", StringComparison.OrdinalIgnoreCase) ||
               text.Contains(".csproj", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ImplementationContractMentionsTests(DispatchCandidate candidate)
    {
        if (!RequiresConcreteImplementationProof(candidate) ||
            !ContainsRunnableApplicationContractSignal(candidate))
        {
            return false;
        }

        var triggerText = ProcessProjectStructureContextFormatter.RemoveSerializedContext(candidate.Run.TriggerReason);
        var textParts = new[]
        {
            triggerText,
            candidate.StepRun.Title,
            candidate.WorkBrief?.Title,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary
        };
        var text = CollapsePromptWhitespace(string.Join(' ', textParts));
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ContainsExplicitImplementationTestRequest(text);
    }

    private static bool ContainsExplicitImplementationTestRequest(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("dotnet test", StringComparison.OrdinalIgnoreCase) ||
               text.Contains(".Tests.csproj", StringComparison.OrdinalIgnoreCase) ||
               text.Contains(".Test.csproj", StringComparison.OrdinalIgnoreCase) ||
               ContainsContractWord(text, "MSTest") ||
               ContainsContractWord(text, "xUnit") ||
               ContainsContractWord(text, "NUnit") ||
               Regex.IsMatch(
                   text,
                   @"\b(?:add|create|write|include|implement|update|run|rerun|execute)\s+(?:the\s+)?(?:relevant\s+)?(?:automated\s+)?(?:unit\s+|integration\s+|regression\s+)?tests?\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   text,
                   @"\bwith\s+(?:automated\s+)?(?:unit\s+|integration\s+|regression\s+)?tests?\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   text,
                   @"\b(?:prove|verify|validate)\b.{0,80}\btests?\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   text,
                   @"\b(?:implement|deliver|build)\b.{0,80}\btests?\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   text,
                   @"\btests?\s+(?:pass|passes|passing|succeed|succeeds)\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   text,
                   @"\b(?:unit|integration|regression)\s+tests?\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   text,
                   @"\btest\s+project\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool ImplementationContractMentionsDotNet(DispatchCandidate candidate, string? additionalContext = null)
    {
        var text = BuildImplementationContractText(candidate, additionalContext);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ContainsAffirmativeImplementationStackToken(text, ".net") ||
               ContainsAffirmativeImplementationStackToken(text, "asp.net") ||
               ContainsAffirmativeImplementationStackToken(text, "dotnet") ||
               ContainsAffirmativeImplementationStackToken(text, "c#") ||
               ContainsAffirmativeImplementationStackToken(text, "csharp") ||
               ContainsAffirmativeImplementationStackToken(text, "blazor") ||
               ContainsAffirmativeImplementationStackToken(text, "razor") ||
               ContainsAffirmativeImplementationStackToken(text, ".csproj") ||
               ContainsAffirmativeImplementationStackToken(text, ".sln") ||
               ContainsAffirmativeImplementationStackToken(text, ".slnx") ||
               ContainsAffirmativeImplementationStackToken(text, "nuget");
    }

    private static bool ImplementationContractMentionsJavaScript(DispatchCandidate candidate, string? additionalContext = null)
    {
        var text = BuildImplementationContractText(candidate, additionalContext);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ContainsAffirmativeImplementationStackToken(text, "javascript") ||
               ContainsAffirmativeImplementationStackToken(text, "typescript") ||
               ContainsAffirmativeImplementationStackToken(text, "package.json") ||
               ContainsAffirmativeImplementationStackToken(text, ".mjs") ||
               ContainsAffirmativeImplementationStackToken(text, ".cjs") ||
               ContainsAffirmativeImplementationStackPattern(
                   text,
                   @"(?:^|[^a-z0-9])(?:js|node\.?js|npm|pnpm|yarn|vite|react|vue|svelte)(?:[^a-z0-9]|$)",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool ImplementationContractNegatesDotNet(DispatchCandidate candidate, string? additionalContext = null)
    {
        var text = BuildImplementationContractText(candidate, additionalContext);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ContainsNegatedImplementationStackToken(text, ".net") ||
               ContainsNegatedImplementationStackToken(text, "asp.net") ||
               ContainsNegatedImplementationStackToken(text, "dotnet") ||
               ContainsNegatedImplementationStackToken(text, "c#") ||
               ContainsNegatedImplementationStackToken(text, "csharp") ||
               ContainsNegatedImplementationStackToken(text, "blazor") ||
               ContainsNegatedImplementationStackToken(text, "razor") ||
               ContainsNegatedImplementationStackToken(text, ".csproj") ||
               ContainsNegatedImplementationStackToken(text, ".sln") ||
               ContainsNegatedImplementationStackToken(text, ".slnx") ||
               ContainsNegatedImplementationStackToken(text, "nuget");
    }

    private static string BuildImplementationContractText(DispatchCandidate candidate, string? additionalContext = null)
    {
        var textParts = new[]
            {
                candidate.StepRun.Title,
                candidate.WorkBrief?.Title,
                candidate.WorkBrief?.WorkBriefText,
                candidate.WorkBrief?.ExpectedOutcome,
                candidate.WorkBrief?.EvidenceExpectationSummary,
                additionalContext
            }
            .Concat(candidate.ExpectedArtifacts.Select(item => item.Title))
            .Concat(candidate.ExpectedArtifacts.Select(item => item.ValidationRequirementSummary));
        return CollapsePromptWhitespace(string.Join(' ', textParts));
    }

    private static bool ContainsNegatedImplementationStackToken(string text, string token)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var pattern = $@"(?<![A-Za-z0-9_]){Regex.Escape(token)}(?![A-Za-z0-9_])";
        return Regex
            .Matches(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Any(match => IsNegatedImplementationStackMention(text, match.Index));
    }

    private static bool ContainsAffirmativeImplementationStackToken(string text, string token)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var pattern = $@"(?<![A-Za-z0-9_]){Regex.Escape(token)}(?![A-Za-z0-9_])";
        return ContainsAffirmativeImplementationStackPattern(
            text,
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool ContainsAffirmativeImplementationStackPattern(
        string text,
        string pattern,
        RegexOptions options)
    {
        foreach (Match match in Regex.Matches(text, pattern, options))
        {
            if (!IsNegatedImplementationStackMention(text, match.Index))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNegatedImplementationStackMention(string text, int matchIndex)
    {
        var prefixStart = Math.Max(0, matchIndex - 64);
        var prefix = text[prefixStart..matchIndex];
        return Regex.IsMatch(
            prefix,
            @"(?:\bnot\s+(?:a\s+)?$|\bno\s+$|\bnon[-\s]+$|\bnegated\s+$|\bwithout\s+$|\bnever\s+$|\bdo\s+not\s+(?:use\s+|call\s+|default\s+to\s+)?[^.;:\r\n]{0,48}$|\bdon't\s+(?:use\s+|call\s+|default\s+to\s+)?[^.;:\r\n]{0,48}$|\bmust\s+not\s+(?:use\s+|call\s+|default\s+to\s+)?[^.;:\r\n]{0,48}$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool ContainsContractWord(string text, string word)
    {
        return Regex.IsMatch(
            text,
            $@"(?<![A-Za-z0-9_]){Regex.Escape(word)}(?![A-Za-z0-9_])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string ResolveInvalidRunnableDotNetHostSummary(IReadOnlyList<string> runnableDotNetProjectPaths)
    {
        foreach (var projectPath in runnableDotNetProjectPaths)
        {
            if (!TryResolvePromptPathToFullPath(projectPath, out var fullPath))
            {
                continue;
            }

            if (TryResolveInvalidWebHostShapeSummary(fullPath, out var summary))
            {
                return summary;
            }
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> ResolveRunnableDotNetHostProjectPaths(
        ExecutionRunDetail detail,
        IReadOnlyList<ToolExecutionReceiptRecord> successfulReceipts)
    {
        var candidatePaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in ExecutionInvocationMetadata.ResolveAllowedExternalTargetAliases(detail.Run))
        {
            AddResolvedPromptPathCandidates(candidatePaths, alias);
        }

        foreach (var receipt in successfulReceipts)
        {
            foreach (var path in ResolveWorkspacePathsFromToolRequest(receipt.RequestSummary))
            {
                AddResolvedPromptPathCandidates(candidatePaths, path);
            }

            if (TryMapWorkspacePathForPrompt(receipt.WorkingDirectory, out var mappedWorkingDirectory))
            {
                AddResolvedPromptPathCandidates(candidatePaths, mappedWorkingDirectory);
            }
        }

        return candidatePaths
            .SelectMany(EnumerateCandidateDotNetProjectFiles)
            .Where(IsRunnableDotNetHostProjectFile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(TryMapAbsolutePathToExternalTargetAlias)
            .ToList();
    }

    private static void AddResolvedPromptPathCandidates(
        SortedSet<string> candidatePaths,
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (!IsConcreteProductPath(normalized))
        {
            return;
        }

        candidatePaths.Add(normalized);
    }

    private static IEnumerable<string> EnumerateCandidateDotNetProjectFiles(string promptPath)
    {
        if (!TryResolvePromptPathToFullPath(promptPath, out var fullPath))
        {
            yield break;
        }

        if (File.Exists(fullPath))
        {
            if (string.Equals(Path.GetExtension(fullPath), ".csproj", StringComparison.OrdinalIgnoreCase))
            {
                yield return fullPath;
            }

            yield break;
        }

        var searchRoot = string.Equals(Path.GetExtension(fullPath), ".sln", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(Path.GetExtension(fullPath), ".slnx", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(fullPath)
            : fullPath;
        if (string.IsNullOrWhiteSpace(searchRoot) || !Directory.Exists(searchRoot))
        {
            yield break;
        }

        IEnumerable<string> projectFiles;
        try
        {
            projectFiles = Directory.EnumerateFiles(searchRoot, "*.csproj", SearchOption.AllDirectories)
                .Where(path => !HasIgnoredProjectPathSegment(path))
                .Take(32)
                .ToList();
        }
        catch (IOException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var projectFile in projectFiles)
        {
            yield return projectFile;
        }
    }

    private static bool TryResolvePromptPathToFullPath(string promptPath, out string fullPath)
    {
        fullPath = string.Empty;
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (IsExternalTargetAliasPath(normalized))
        {
            return TryResolveExternalTargetArtifactFullPath(normalized, out fullPath, out _);
        }

        if (Path.IsPathRooted(promptPath))
        {
            fullPath = Path.GetFullPath(promptPath);
            return true;
        }

        return false;
    }

    private static bool HasIgnoredProjectPathSegment(string path)
    {
        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Any(segment =>
            string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsRunnableDotNetHostProjectFile(string fullPath)
    {
        try
        {
            var document = XDocument.Load(fullPath, LoadOptions.None);
            var sdk = document.Root?.Attribute("Sdk")?.Value ?? string.Empty;
            if (sdk.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) ||
                sdk.Contains("Microsoft.NET.Sdk.Worker", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return document
                .Descendants()
                .Where(element => string.Equals(element.Name.LocalName, "OutputType", StringComparison.OrdinalIgnoreCase))
                .Select(element => element.Value.Trim())
                .Any(value =>
                    string.Equals(value, "Exe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "WinExe", StringComparison.OrdinalIgnoreCase));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private static string TryMapAbsolutePathToExternalTargetAlias(string fullPath)
    {
        var normalized = Path.GetFullPath(fullPath).Replace(Path.DirectorySeparatorChar, '/');
        if (normalized.Length < 3 || normalized[1] != ':' || normalized[2] != '/')
        {
            return fullPath;
        }

        var driveLetter = char.ToUpperInvariant(normalized[0]);
        var suffix = normalized[3..].Trim('/');
        return string.IsNullOrWhiteSpace(suffix)
            ? $"{ExternalTargetAliasRoot}/{driveLetter}"
            : $"{ExternalTargetAliasRoot}/{driveLetter}/{suffix}";
    }

    private static ToolExecutionReceiptRecord? ResolveLatestRequiredImplementationValidationReceipt(
        DispatchCandidate candidate,
        IReadOnlyList<ToolExecutionReceiptRecord> successfulReceipts)
    {
        var requiredToolNames = ResolveRequiredToolNames(candidate)
            .ToHashSet(StringComparer.Ordinal);
        if (requiredToolNames.Count == 0)
        {
            return null;
        }

        return successfulReceipts
            .Where(receipt =>
            {
                var normalizedToolName = NormalizeToolToken(receipt.ToolName);
                return requiredToolNames.Contains(normalizedToolName) &&
                       IsImplementationValidationToolName(normalizedToolName);
            })
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
    }

    private static ToolExecutionReceiptRecord? ResolveLatestReceipt(
        IEnumerable<ToolExecutionReceiptRecord> receipts,
        string normalizedToolName,
        bool requireConcreteProductPath,
        bool requireConcreteDeliverableOrSourcePath)
    {
        return ResolveLatestReceipt(
            receipts,
            toolName => string.Equals(toolName, normalizedToolName, StringComparison.Ordinal),
            requireConcreteProductPath,
            requireConcreteDeliverableOrSourcePath);
    }

    private static ToolExecutionReceiptRecord? ResolveLatestReceipt(
        IEnumerable<ToolExecutionReceiptRecord> receipts,
        Func<string, bool> matchesToolName,
        bool requireConcreteProductPath,
        bool requireConcreteDeliverableOrSourcePath)
    {
        return receipts
            .Where(receipt => matchesToolName(NormalizeToolToken(receipt.ToolName)))
            .Where(receipt => !requireConcreteProductPath || HasConcreteProductPath(receipt))
            .Where(receipt => !requireConcreteDeliverableOrSourcePath || HasConcreteProductDeliverableOrSourcePath(receipt))
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
    }

    private static bool IsConcreteProductMutationReceipt(
        DispatchCandidate candidate,
        ToolExecutionReceiptRecord receipt)
    {
        var toolName = NormalizeToolToken(receipt.ToolName);
        if (string.Equals(toolName, "workspace_write_file", StringComparison.Ordinal) ||
            string.Equals(toolName, "workspace_append_file", StringComparison.Ordinal))
        {
            return RequiresCurrentAttemptProductMutation(candidate)
                ? HasConcreteProductDeliverableOrSourcePath(receipt)
                : HasConcreteProductImplementationPath(candidate, receipt);
        }

        return HasConcreteProductPath(receipt);
    }

    private static bool IsConcreteProductMutationToolName(string normalizedToolName)
    {
        return ConcreteProductMutationToolNames.Contains(normalizedToolName) ||
               IsImplementationBootstrapToolName(normalizedToolName);
    }

    private static bool IsImplementationBootstrapToolName(string normalizedToolName)
    {
        return normalizedToolName.StartsWith("workspace_", StringComparison.Ordinal) &&
               normalizedToolName.EndsWith("_new", StringComparison.Ordinal);
    }

    private static bool IsImplementationValidationToolName(string normalizedToolName)
    {
        return normalizedToolName.EndsWith("_build", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_test", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_run", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_publish", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_validate", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_lint", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_check", StringComparison.Ordinal) ||
               normalizedToolName.StartsWith("browser_", StringComparison.Ordinal);
    }

    private static bool HasConcreteProductPath(ToolExecutionReceiptRecord receipt)
    {
        return ResolveWorkspacePathsFromReceipt(receipt)
            .Any(IsConcreteProductPath);
    }

    private static bool HasConcreteProductDeliverableOrSourcePath(ToolExecutionReceiptRecord receipt)
    {
        return ResolveWorkspacePathsFromReceipt(receipt)
            .Any(IsConcreteProductDeliverableOrSourcePath);
    }

    private static bool HasConcreteProductImplementationPath(
        DispatchCandidate candidate,
        ToolExecutionReceiptRecord receipt)
    {
        return RequiresSourceOrProjectImplementationProof(candidate)
            ? HasConcreteProductSourceOrProjectPath(receipt)
            : HasConcreteProductDeliverableOrSourcePath(receipt);
    }

    private static bool HasConcreteProductSourceOrProjectPath(ToolExecutionReceiptRecord receipt)
    {
        return ResolveWorkspacePathsFromReceipt(receipt)
            .Any(IsConcreteProductSourceOrProjectPath);
    }

    private static IReadOnlyList<string> ResolveWorkspacePathsFromReceipt(ToolExecutionReceiptRecord receipt)
    {
        var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in ResolveWorkspacePathsFromToolRequest(receipt.RequestSummary))
        {
            paths.Add(path);
        }

        if (TryMapWorkspacePathForPrompt(receipt.WorkingDirectory, out var workingDirectory))
        {
            paths.Add(workingDirectory);
        }

        return paths.ToList();
    }

    private static IReadOnlyList<string> ResolveWorkspacePathsFromToolRequest(string requestSummary)
    {
        if (string.IsNullOrWhiteSpace(requestSummary))
        {
            return [];
        }

        var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in WorkspacePathInToolRequestRegex.Matches(requestSummary))
        {
            var candidatePath = match.Groups["path"].Value;
            if (TryMapWorkspacePathForPrompt(candidatePath, out var promptPath))
            {
                paths.Add(promptPath);
            }
        }

        foreach (Match match in ManagedWorkspacePathRegex.Matches(requestSummary))
        {
            var candidatePath = WorkspaceScopeDescriptor.NormalizeRelativePath(match.Groups["path"].Value);
            if (IsManagedProcessRunProductOutputPath(candidatePath))
            {
                paths.Add(candidatePath);
            }
        }

        return paths.ToList();
    }

    private static bool TryMapWorkspacePathForPrompt(string path, out string promptPath)
    {
        promptPath = string.Empty;
        var normalized = path.Trim().TrimEnd(',', ';', '.', ')', ']', '}').Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (normalized.StartsWith($"{ExternalTargetAliasRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            promptPath = normalized;
            return true;
        }

        if (normalized.Length < 3 || !char.IsLetter(normalized[0]) || normalized[1] != ':' || normalized[2] != '/')
        {
            return false;
        }

        var driveLetter = char.ToUpperInvariant(normalized[0]);
        var remainder = normalized.Length == 3
            ? string.Empty
            : normalized[3..].Trim('/');
        promptPath = string.IsNullOrWhiteSpace(remainder)
            ? $"{ExternalTargetAliasRoot}/{driveLetter}"
            : $"{ExternalTargetAliasRoot}/{driveLetter}/{remainder}";
        return true;
    }

    private static bool IsConcreteProductDeliverableOrSourcePath(string promptPath)
    {
        if (!IsConcreteProductPath(promptPath))
        {
            return false;
        }

        var extension = Path.GetExtension(promptPath);
        return IsImplementationDeliverableOrSourceExtension(extension);
    }

    private static bool IsConcreteProductSourceOrProjectPath(string promptPath)
    {
        return IsConcreteProductPath(promptPath) &&
               IsCodeOrProjectExtension(Path.GetExtension(promptPath));
    }

    private static bool IsImplementationDeliverableOrSourceExtension(string extension)
    {
        return IsCodeOrProjectExtension(extension) ||
               extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".docx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".xls", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tsv", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".pptx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".ppt", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConcreteProductPath(string promptPath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (IsExternalTargetAliasPath(normalized))
        {
            return segments.Length >= 2 &&
                   !IsExternalTargetAliasWithinManagedWorkspace(segments) &&
                   !segments.Any(IsExternalTargetNonProductPathSegment);
        }

        if (segments.Length == 0)
        {
            return false;
        }

        if (IsManagedRootSegment(segments[0]))
        {
            return IsManagedProcessRunProductOutputPath(segments);
        }

        return !segments.Any(IsNonProductPathSegment);
    }

    private static bool IsManagedProcessRunProductOutputPath(string path)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return IsManagedProcessRunProductOutputPath(segments);
    }

    private static bool IsManagedProcessRunProductOutputPath(IReadOnlyList<string> segments)
    {
        if (segments.Count == 0 ||
            !string.Equals(segments[0], "output", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var processRunsIndex = -1;
        for (var index = 1; index < segments.Count; index++)
        {
            if (string.Equals(segments[index], "process-runs", StringComparison.OrdinalIgnoreCase))
            {
                processRunsIndex = index;
                break;
            }
        }

        if (processRunsIndex < 0 || segments.Count <= processRunsIndex + 2)
        {
            return false;
        }

        return segments
            .Skip(processRunsIndex + 2)
            .All(segment => !IsManagedProcessRunNonProductPathSegment(segment));
    }

    private static bool IsManagedProcessRunNonProductPathSegment(string segment)
    {
        return string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, ".playwright-mcp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresSourceOrProjectImplementationProof(DispatchCandidate candidate)
    {
        return ContainsRunnableApplicationContractSignal(candidate);
    }

    private static bool IsExternalTargetAliasWithinManagedWorkspace(IReadOnlyList<string> segments)
    {
        var hasCanDoItAllControlPlanePrefix = false;
        for (var index = 0; index < segments.Count; index++)
        {
            if (string.Equals(segments[index], "CanDoItAll", StringComparison.OrdinalIgnoreCase))
            {
                hasCanDoItAllControlPlanePrefix = segments
                    .Skip(index + 1)
                    .Take(3)
                    .Any(segment => string.Equals(segment, "control-plane", StringComparison.OrdinalIgnoreCase));
            }

            if (!hasCanDoItAllControlPlanePrefix ||
                !string.Equals(segments[index], "workspace", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsExternalTargetNonProductPathSegment(string segment)
    {
        return string.Equals(segment, ".playwright-mcp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNonProductPathSegment(string segment)
    {
        return IsManagedRootSegment(segment) ||
               string.Equals(segment, ".playwright-mcp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReceiptAfter(ToolExecutionReceiptRecord candidate, ToolExecutionReceiptRecord baseline)
    {
        return candidate.CompletedAtUtc > baseline.CompletedAtUtc ||
               candidate.CompletedAtUtc == baseline.CompletedAtUtc &&
               candidate.StartedAtUtc > baseline.StartedAtUtc;
    }
}
