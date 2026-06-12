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
    private static string ResolveMissingConcreteProofSummary(
        DispatchCandidate candidate,
        string? responseText)
    {
        if (!RequiresConcreteBrowserProof(candidate))
        {
            return string.Empty;
        }

        return ProcessArtifactQualityValidationRules.ResolveMissingConcreteProofSummary(responseText);
    }

    private static string ResolveInvalidQualityValidationProofSummary(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string? inspectionText)
    {
        if (!RequiresQualityValidationEvidence(candidate))
        {
            return string.Empty;
        }

        var evidenceTexts = ResolveQualityValidationEvidenceTexts(detail, inspectionText);
        return ProcessArtifactQualityValidationRules.ResolveInvalidQualityValidationProofSummary(evidenceTexts);
    }

    private static string ResolveDowngradedProjectStructureRequirementSummary(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string? inspectionText)
        => ProcessArtifactProjectStructureRequirementValidationRules.ResolveDowngradedProjectStructureRequirementSummary(
            ResolveProjectStructureRequirementPreservationContractText(candidate),
            detail.Run.InputSummary,
            inspectionText);

    private static string ResolveProjectStructureRequirementPreservationContractText(DispatchCandidate candidate)
    {
        return CollapsePromptWhitespace(string.Join(
            ' ',
            candidate.StepDefinition.InputContractSummary,
            candidate.StepDefinition.OutputContractSummary,
            candidate.StepDefinition.EvidenceContractSummary,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary,
            string.Join(' ', candidate.ExpectedArtifacts.Select(item => item.ValidationRequirementSummary))));
    }

    private static bool RequiresQualityValidationEvidence(DispatchCandidate candidate)
    {
        if (candidate.StepRun.StepKind is ProcessStepKind.Review or ProcessStepKind.Approval or ProcessStepKind.Delivery)
        {
            return true;
        }

        var text = ResolveQualityValidationContractText(candidate);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ProcessArtifactQualityValidationRules.ContainsQualityValidationContractSignal(text);
    }

    private static string ResolveQualityValidationContractText(DispatchCandidate candidate)
    {
        var triggerText = ProcessProjectStructureContextFormatter.RemoveSerializedContext(candidate.Run.TriggerReason);
        var textParts = new[]
            {
                triggerText,
                candidate.StepRun.Title,
                candidate.WorkBrief?.Title,
                candidate.WorkBrief?.WorkBriefText,
                candidate.WorkBrief?.ExpectedOutcome,
                candidate.WorkBrief?.EvidenceExpectationSummary
            }
            .Concat(candidate.ExpectedArtifacts.Select(item => item.Title))
            .Concat(candidate.ExpectedArtifacts.Select(item => item.ValidationRequirementSummary));
        return CollapsePromptWhitespace(string.Join(' ', textParts));
    }

    private static IReadOnlyList<string> ResolveQualityValidationEvidenceTexts(
        ProcessAutomationExecutionRunDetail detail,
        string? inspectionText)
    {
        var sessionToolResultTexts = ResolveSuccessfulSessionToolResultTexts(detail.Run.SerializedSessionStateJson)
            .Select(item => (item.ToolName, item.Text))
            .ToList();
        return ProcessQualityValidationEvidenceAggregator.ResolveEvidenceTexts(
            inspectionText,
            detail.Run.ResultSummary,
            detail.ToolReceipts,
            sessionToolResultTexts,
            IsQualityValidationEvidenceToolName,
            NormalizeToolToken);
    }

    private static bool IsQualityValidationEvidenceToolName(string normalizedToolName)
        => ProcessArtifactQualityValidationRules.IsQualityValidationEvidenceToolName(
            normalizedToolName,
            IsImplementationValidationToolName);

    private static string ResolveIncompleteImplementationSummary(
        DispatchCandidate candidate,
        string? responseText)
    {
        return ProcessIncompleteImplementationSignalRules.ResolveIncompleteImplementationSummary(
            RequiresConcreteImplementationProof(candidate),
            responseText);
    }

    private static string ResolveMissingRequiredArtifactSummary(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string? responseText)
    {
        if (candidate.ExpectedArtifacts.Count == 0)
        {
            return string.Empty;
        }

        return ProcessArtifactSatisfactionBlockerSummaryBuilder.BuildMissingRequiredArtifactSummary(
            candidate.ExpectedArtifacts,
            expectedArtifact => HasSatisfiedRequiredArtifact(candidate, detail, expectedArtifact, responseText));
    }

    private static bool HasSatisfiedRequiredArtifact(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        DispatchArtifactExpectation expectedArtifact,
        string? responseText)
    {
        if (HasRecordedExpectedArtifact(candidate, detail, expectedArtifact) &&
            !HasSuccessfulConcreteProductMutation(candidate, detail))
        {
            return true;
        }

        if (CanProjectProcessMockArtifact(candidate, detail, expectedArtifact))
        {
            return true;
        }

        if (RequiresFreshCurrentAttemptImplementationArtifact(candidate, expectedArtifact))
        {
            return HasFreshCurrentAttemptImplementationArtifact(candidate, detail, expectedArtifact);
        }

        return HasRecordedExpectedArtifact(candidate, detail, expectedArtifact) ||
               CanAutoSatisfyRequiredArtifact(candidate, detail, expectedArtifact, responseText);
    }

    private static bool RequiresFreshCurrentAttemptImplementationArtifact(
        DispatchCandidate candidate,
        DispatchArtifactExpectation expectedArtifact)
    {
        return ProcessFreshImplementationArtifactSatisfactionRules.RequiresFreshCurrentAttemptImplementationArtifact(
            RequiresConcreteImplementationProof(candidate),
            expectedArtifact);
    }

    private static bool HasFreshCurrentAttemptImplementationArtifact(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        DispatchArtifactExpectation expectedArtifact)
    {
        var successfulReceipts = detail.ToolReceipts
            .Where(receipt => !IsFailedToolReceipt(receipt))
            .ToList();
        var latestConcreteMutation = successfulReceipts
            .Where(receipt => IsConcreteProductMutationToolName(NormalizeToolToken(receipt.ToolName)))
            .Where(receipt => IsConcreteProductMutationReceipt(candidate, detail, receipt))
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
        var latestValidation = ResolveLatestRequiredImplementationValidationReceipt(candidate, successfulReceipts);
        var artifactReceipts = successfulReceipts
            .Where(IsSuccessfulWorkspaceFileMutationReceipt)
            .Where(receipt => WorkspaceMutationReceiptMatchesExpectedArtifact(candidate, detail, expectedArtifact, receipt))
            .ToList();
        var latestConcreteRead = latestConcreteMutation is null
            ? ResolveLatestImplementationProofReadReceipt(candidate, successfulReceipts)
            : null;
        return ProcessFreshImplementationArtifactSatisfactionRules.HasFreshCurrentAttemptImplementationArtifact(
            latestConcreteMutation,
            latestConcreteRead,
            latestValidation,
            artifactReceipts,
            IsReceiptAfter);
    }

    private static bool WorkspaceMutationReceiptMatchesExpectedArtifact(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        DispatchArtifactExpectation expectedArtifact,
        ProcessAutomationToolExecutionReceipt receipt)
    {
        if (TryResolveProjectStructureExpectedArtifactPath(candidate, expectedArtifact, detail.Run.InputSummary, out var governedPath))
        {
            return ResolveManagedWorkspacePathsFromReceipt(receipt)
                .Any(path => ArtifactPathMatchesGovernedProjectStructurePath(path, governedPath));
        }

        return ResolveManagedWorkspacePathsFromReceipt(receipt)
            .Any(path => WorkspaceWrittenFileMatchesExpectedArtifact(
                candidate.ExpectedArtifacts,
                expectedArtifact,
                path,
                content: string.Empty));
    }

    private static bool HasRecordedExpectedArtifact(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        DispatchArtifactExpectation expectedArtifact)
    {
        var snapshot = ProcessArtifactSatisfactionSnapshotBuilder.From(candidate, detail);
        return ProcessArtifactRecordedSatisfactionRules.HasRecordedExpectedArtifact(
            snapshot,
            expectedArtifact,
            artifact => ResolveArtifactExpectationId(candidate, detail, artifact));
    }

    private static bool HasRecordedOrExecutionArtifactForExpectedArtifact(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        DispatchArtifactExpectation expectedArtifact)
    {
        return HasRecordedExpectedArtifact(candidate, detail, expectedArtifact) ||
               CanProjectProcessMockArtifact(candidate, detail, expectedArtifact) ||
               CanProjectWorkspaceWrittenArtifact(candidate, detail, expectedArtifact);
    }

    private static bool CanAutoSatisfyRequiredArtifact(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        DispatchArtifactExpectation expectedArtifact,
        string? responseText)
    {
        return ProcessRequiredArtifactAutoSatisfactionRules.CanAutoSatisfyRequiredArtifact(
            () => TryResolveProjectStructureExpectedArtifactPath(candidate, expectedArtifact, detail.Run.InputSummary, out _),
            () => CanProjectWorkspaceWrittenArtifact(candidate, detail, expectedArtifact),
            () => CanProjectProcessMockArtifact(candidate, detail, expectedArtifact),
            () => CanProjectProviderNativeVisualArtifact(candidate, detail, expectedArtifact),
            () => ShouldAutoRecordCompletedDecisionArtifact(expectedArtifact),
            () => ResolveProjectableResponseArtifactText(responseText),
            () =>
            {
                var hasDeclaredPath = TryExtractExpectedArtifactRelativePath(
                    expectedArtifact.ValidationRequirementSummary,
                    out var declaredRelativePath);
                return (hasDeclaredPath, declaredRelativePath);
            },
            declaredRelativePath => HasProviderNativeBrowserOutputForDeclaredPath(detail, declaredRelativePath),
            IsResponseProjectableTextArtifact,
            projectableResponseText => IsUsableProjectedResponseArtifactContent(expectedArtifact, projectableResponseText),
            () => CanProjectResponseTextArtifactWithoutDeclaredPath(expectedArtifact));
    }

    private static string ResolveOutOfScopeExternalTargetReferenceSummary(
        ProcessAutomationExecutionRunDetail detail,
        string? responseText)
    {
        var allowedAliases = ResolveAllowedExternalTargetAliases(detail.Run);
        if (allowedAliases.Count == 0)
        {
            return string.Empty;
        }

        var content = new List<string>();
        if (!string.IsNullOrWhiteSpace(responseText))
        {
            content.Add(responseText);
        }

        content.AddRange(ResolveSuccessfulSessionFileWrites(detail.Run.SerializedSessionStateJson)
            .Where(file => IsTextReadableManagedArtifactPath(file.Path))
            .Select(file => file.Content)
            .Where(fileContent => !string.IsNullOrWhiteSpace(fileContent)));

        return ResolveOutOfScopeExternalTargetReferenceSummary(
            string.Join(Environment.NewLine, content),
            allowedAliases);
    }

    internal static string ResolveOutOfScopeExternalTargetReferenceSummary(
        string? text,
        IReadOnlyList<string> allowedAliases)
        => ProcessExternalTargetReferenceGuard.ResolveOutOfScopeReferenceSummary(text, allowedAliases);

    private static string ResolveShallowSharedManagedArtifactReferenceSummary(
        ProcessAutomationExecutionRunDetail detail,
        string? responseText)
    {
        var allowedExternalTargetAliases = ResolveAllowedExternalTargetAliases(detail.Run);
        if (allowedExternalTargetAliases.Count == 0)
        {
            return string.Empty;
        }

        var observedPaths = ResolveSuccessfulSessionFileReads(detail.Run.SerializedSessionStateJson)
            .Concat(ResolveSuccessfulSessionFileWrites(detail.Run.SerializedSessionStateJson))
            .Select(file => file.Path)
            .ToList();
        return ProcessShallowManagedArtifactReferenceGuard.ResolveSummary(
            observedPaths,
            responseText,
            allowedExternalTargetAliases,
            ManagedWorkspacePathRegex,
            AddShallowSharedManagedArtifactPath);
    }

    private static void AddShallowSharedManagedArtifactPath(
        ISet<string> shallowPaths,
        string path,
        IReadOnlyList<string> allowedExternalTargetAliases)
    {
        var normalizedPath = NormalizeManagedPathReference(path);
        if (!IsShallowSharedManagedArtifactPath(normalizedPath))
        {
            return;
        }

        if (IsLikelyProductFileRelativeToAllowedExternalTargetLeaf(normalizedPath, allowedExternalTargetAliases))
        {
            return;
        }

        shallowPaths.Add(normalizedPath);
    }

    private static string NormalizeManagedPathReference(string path)
        => ProcessArtifactPathValidationRules.NormalizeManagedPathReference(path);

    internal static bool IsShallowSharedManagedArtifactPath(string path)
        => ProcessArtifactPathValidationRules.IsShallowSharedManagedArtifactPath(path);

    private static bool IsManagedEvidenceRootSegment(string segment)
        => ProcessArtifactPathValidationRules.IsManagedRootSegment(segment);

    private static bool IsLikelyProductFileRelativeToAllowedExternalTargetLeaf(
        string path,
        IReadOnlyList<string> allowedExternalTargetAliases)
    {
        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(path);
        var segments = normalizedPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 2 ||
            !IsManagedEvidenceRootSegment(segments[0]) ||
            !IsLikelyProductDeliverableOrSourceFileName(segments[1]))
        {
            return false;
        }

        return allowedExternalTargetAliases
            .Select(alias => WorkspaceScopeDescriptor.NormalizeRelativePath(alias))
            .Any(alias =>
            {
                var aliasSegments = alias.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return aliasSegments.Length > 0 &&
                       string.Equals(aliasSegments[^1], segments[0], StringComparison.OrdinalIgnoreCase);
            });
    }

    private static bool IsLikelyProductSourceOrProjectFileName(string fileName)
    {
        return ProcessManagedArtifactPathClassificationRules.IsLikelyProductSourceOrProjectFileName(fileName);
    }

    private static bool IsLikelyProductDeliverableOrSourceFileName(string fileName)
    {
        return ProcessManagedArtifactPathClassificationRules.IsLikelyProductDeliverableOrSourceFileName(fileName);
    }

    private static bool CanProjectProcessMockArtifact(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        DispatchArtifactExpectation expectedArtifact)
    {
        return ResolveProcessMockArtifactProjections(detail.Run.SerializedSessionStateJson)
            .Any(projection => ProcessMockImplementationProofBridge.MatchesExpectedArtifact(expectedArtifact, projection));
    }

    private static bool CanProjectWorkspaceWrittenArtifact(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        DispatchArtifactExpectation expectedArtifact)
    {
        return ProcessImplementationArtifactWriteSatisfactionBridge.CanProjectWorkspaceWrittenArtifact(
            candidate,
            detail,
            expectedArtifact);
    }

    private static bool CanProjectProviderNativeVisualArtifact(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        DispatchArtifactExpectation expectedArtifact)
    {
        if (expectedArtifact.ArtifactKind != ProcessArtifactKind.Evidence)
        {
            return false;
        }

        var browserOutputsByToolName = ResolveSuccessfulBrowserToolOutputFiles(detail);
        foreach (var pair in browserOutputsByToolName)
        {
            foreach (var outputFileName in pair.Value)
            {
                var normalizedOutputPath = WorkspaceScopeDescriptor.NormalizeRelativePath(outputFileName);
                if (!IsProviderNativeBrowserArtifactPath(normalizedOutputPath))
                {
                    continue;
                }

                var syntheticArtifact = new ProcessAutomationExecutionArtifact(
                    Guid.Empty,
                    detail.Run.Id,
                    "generated-output",
                    ResolvePromptFileName(normalizedOutputPath),
                    normalizedOutputPath,
                    GuessContentTypeFromPath(normalizedOutputPath),
                    pair.Key,
                    "Provider-native browser output captured by a browser MCP tool.",
                    DateTimeOffset.MinValue);
                if (MatchExpectedArtifactId(candidate.ExpectedArtifacts, syntheticArtifact) == expectedArtifact.Id)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasProviderNativeBrowserOutputForDeclaredPath(
        ProcessAutomationExecutionRunDetail detail,
        string declaredRelativePath)
        => ProcessProviderNativeBrowserOutputFacts.HasProviderNativeBrowserOutputForDeclaredPath(
            detail,
            declaredRelativePath,
            ResolveSuccessfulBrowserToolOutputFiles,
            TryResolveSafeBrowserOutputPath);

    internal static bool WorkspaceWrittenFileMatchesExpectedArtifact(
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
        DispatchArtifactExpectation expectedArtifact,
        string path,
        string content)
    {
        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(path);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return false;
        }

        if (ShouldIgnoreProductSourceForNarrativeExpectation(expectedArtifact, normalizedPath))
        {
            return false;
        }

        var syntheticArtifact = new ProcessAutomationExecutionArtifact(
            Guid.Empty,
            Guid.Empty,
            "generated-output",
            Path.GetFileNameWithoutExtension(normalizedPath),
            normalizedPath,
            GuessContentTypeFromPath(normalizedPath),
            "workspace_write_file",
            "Workspace file written by the agent.",
            DateTimeOffset.MinValue);
        var matchedExpectationId = MatchExpectedArtifactId(expectedArtifacts, syntheticArtifact, content);
        return matchedExpectationId == expectedArtifact.Id;
    }

    private static bool ExpectedArtifactExplicitlyTargetsPath(
        DispatchArtifactExpectation expectedArtifact,
        string normalizedPath)
        => ExpectedArtifactExplicitlyTargetsPath(
            ProcessArtifactValidationSnapshotBuilder.FromDispatchExpectation(expectedArtifact),
            normalizedPath);

    private static bool ExpectedArtifactExplicitlyTargetsPath(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        string normalizedPath)
        => ProcessArtifactPathValidationRules.ExpectedArtifactExplicitlyTargetsPath(expectedArtifact, normalizedPath);

    private static bool ShouldIgnoreProductSourceForNarrativeExpectation(
        DispatchArtifactExpectation expectedArtifact,
        string normalizedPath)
        => ShouldIgnoreProductSourceForNarrativeExpectation(
            ProcessArtifactValidationSnapshotBuilder.FromDispatchExpectation(expectedArtifact),
            normalizedPath);

    private static bool ShouldIgnoreProductSourceForNarrativeExpectation(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        string normalizedPath)
    {
        return IsLikelyProductSourceOrProjectFileName(ResolvePromptFileName(normalizedPath)) &&
               IsNarrativeEvidenceArtifactExpectation(expectedArtifact) &&
               !ExpectedArtifactExplicitlyTargetsPath(expectedArtifact, normalizedPath);
    }

    private static string ResolvePromptFileName(string path)
    {
        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(path);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return string.Empty;
        }

        var segments = normalizedPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length == 0 ? string.Empty : segments[^1];
    }

    private static bool IsNarrativeEvidenceArtifactExpectation(DispatchArtifactExpectation expectedArtifact)
        => IsNarrativeEvidenceArtifactExpectation(
            ProcessArtifactValidationSnapshotBuilder.FromDispatchExpectation(expectedArtifact));

    private static bool IsNarrativeEvidenceArtifactExpectation(ProcessArtifactExpectationSnapshot expectedArtifact)
    {
        var text = CollapsePromptWhitespace($"{expectedArtifact.Title} {expectedArtifact.ValidationRequirementSummary}");
        return text.Contains("change set", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("checklist", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("summary", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("brief", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("report", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("notes", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("evidence", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("rollout", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("migration", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSuccessfulWorkspaceFileMutationReceipt(ProcessAutomationToolExecutionReceipt receipt)
    {
        var toolName = NormalizeToolToken(receipt.ToolName);
        return (string.Equals(toolName, "workspace_write_file", StringComparison.Ordinal) ||
                string.Equals(toolName, "workspace_append_file", StringComparison.Ordinal)) &&
               !IsFailedToolReceipt(receipt);
    }

    internal static IReadOnlyList<string> ResolveManagedWorkspacePathsFromReceipt(ProcessAutomationToolExecutionReceipt receipt)
    {
        var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var text = string.Join(
            Environment.NewLine,
            [
                receipt.RequestSummary,
                receipt.ExitSummary
            ]);
        foreach (Match match in ManagedWorkspacePathRegex.Matches(text))
        {
            var path = WorkspaceScopeDescriptor.NormalizeRelativePath(match.Groups["path"].Value);
            if (!string.IsNullOrWhiteSpace(path))
            {
                paths.Add(path);
            }
        }

        foreach (var path in ResolveWorkspacePathsFromToolRequest(text))
        {
            paths.Add(path);
        }

        return paths.ToList();
    }

    internal static bool IsUsableProjectedResponseArtifactContent(
        DispatchArtifactExpectation expectedArtifact,
        string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return false;
        }

        var normalizedResponse = CollapsePromptWhitespace(responseText);
        if (normalizedResponse.Length < 160)
        {
            return false;
        }

        if (IsConversationalNonArtifactResponse(normalizedResponse))
        {
            return false;
        }

        return HasExpectedArtifactContentSignals(expectedArtifact, responseText, normalizedResponse);
    }

    internal static string ResolveProjectableResponseArtifactText(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return string.Empty;
        }

        if (!TryReadProcessStepOutcome(responseText, out var outcome, out _))
        {
            return responseText.Trim();
        }

        if (!string.IsNullOrWhiteSpace(outcome.HumanReadableSummaryMarkdown))
        {
            return outcome.HumanReadableSummaryMarkdown.Trim();
        }

        return outcome.Reason?.Trim() ?? string.Empty;
    }

    private static bool HasExpectedArtifactContentSignals(
        DispatchArtifactExpectation expectedArtifact,
        string responseText,
        string normalizedResponse)
        => HasExpectedArtifactContentSignals(
            ProcessArtifactValidationSnapshotBuilder.FromDispatchExpectation(expectedArtifact),
            responseText,
            normalizedResponse);

    private static bool HasExpectedArtifactContentSignals(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        string responseText,
        string normalizedResponse)
        => ProcessArtifactTextMatchRules.HasExpectedArtifactContentSignals(
            expectedArtifact,
            responseText,
            normalizedResponse,
            ContainsArtifactResponseSection(responseText, expectedArtifact.Title));

    private static bool HasExpectedArtifactValidationSignals(
        DispatchArtifactExpectation expectedArtifact,
        string normalizedResponse)
        => HasExpectedArtifactValidationSignals(
            ProcessArtifactValidationSnapshotBuilder.FromDispatchExpectation(expectedArtifact),
            normalizedResponse);

    private static bool HasExpectedArtifactValidationSignals(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        string normalizedResponse)
        => ProcessArtifactTextMatchRules.HasExpectedArtifactValidationSignals(expectedArtifact, normalizedResponse);

    private static bool HasExpectedArtifactValidationSignals(
        DispatchArtifactExpectation expectedArtifact,
        IReadOnlySet<string> responseTokens)
        => HasExpectedArtifactValidationSignals(
            ProcessArtifactValidationSnapshotBuilder.FromDispatchExpectation(expectedArtifact),
            responseTokens);

    private static bool HasExpectedArtifactValidationSignals(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        IReadOnlySet<string> responseTokens)
        => ProcessArtifactTextMatchRules.HasExpectedArtifactValidationSignals(expectedArtifact, responseTokens);

    private static IReadOnlyList<string> TokenizeArtifactContentSignalText(string value)
        => ProcessArtifactTextMatchRules.TokenizeArtifactContentSignalText(value);

    private static bool IsConversationalNonArtifactResponse(string normalizedResponse)
    {
        return ProcessResponseTextArtifactSatisfactionRules.IsConversationalNonArtifactResponse(normalizedResponse);
    }

    private static bool ContainsConcreteBrowserProofSignal(string? value)
        => ProcessArtifactQualityValidationRules.ContainsConcreteBrowserProofSignal(value);

    private static string RemoveApplicabilityOnlyBrowserEvidencePhrases(string? value)
        => ProcessArtifactQualityValidationRules.RemoveApplicabilityOnlyBrowserEvidencePhrases(value);

    private static bool IsRecoverableImplementationPunt(
        DispatchCandidate candidate,
        string? responseText)
    {
        if (!RequiresConcreteImplementationProof(candidate) ||
            !TryResolveDeclaredStepOutcome(candidate, responseText, out var declaredOutcome) ||
            declaredOutcome.Status != ProcessStepRunStatus.Blocked ||
            string.IsNullOrWhiteSpace(declaredOutcome.Reason))
        {
            return false;
        }

        var normalizedReason = Regex.Replace(
                declaredOutcome.Reason,
                @"\s+",
                " ",
                RegexOptions.CultureInvariant)
            .Trim()
            .ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedReason))
        {
            return false;
        }

        return normalizedReason.Contains("no implementation", StringComparison.Ordinal) ||
               normalizedReason.Contains("no code artifact", StringComparison.Ordinal) ||
               normalizedReason.Contains("bootstrap and implement", StringComparison.Ordinal) ||
               normalizedReason.Contains("proceed to bootstrap", StringComparison.Ordinal) ||
               normalizedReason.Contains("scaffold", StringComparison.Ordinal) ||
               normalizedReason.Contains("before marking as completed", StringComparison.Ordinal);
    }

    private static bool IsHardRequiredProcessToolName(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return false;
        }

        return !toolName.StartsWith("browser_", StringComparison.Ordinal) ||
               RequiredBrowserEvidenceToolNames.Contains(toolName);
    }

    private static bool IsNegatedRequiredToolReference(string workBriefText, Match match)
    {
        if (!match.Success)
        {
            return false;
        }

        var segmentStart = FindInstructionSegmentStart(workBriefText, match.Index);
        var contextLength = match.Index - segmentStart;
        if (contextLength <= 0)
        {
            return false;
        }

        var context = workBriefText.Substring(segmentStart, contextLength);
        if (string.IsNullOrWhiteSpace(context))
        {
            return false;
        }

        var normalizedContext = Regex.Replace(context, @"\s+", " ").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedContext))
        {
            return false;
        }

        foreach (var phrase in NegatedRequiredToolPhrases)
        {
            if (normalizedContext.Contains(phrase, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int FindInstructionSegmentStart(string workBriefText, int matchIndex)
    {
        if (matchIndex <= 0)
        {
            return 0;
        }

        var segmentStart = 0;
        for (var index = matchIndex - 1; index >= 0; index--)
        {
            var current = workBriefText[index];
            if (current is '\r' or '\n' or '.' or '!' or '?' or ';')
            {
                segmentStart = index + 1;
                break;
            }
        }

        return segmentStart;
    }

    private static bool IsCriticalToolReceipt(ProcessAutomationToolExecutionReceipt receipt)
    {
        return ProcessToolReceiptFacts.IsCriticalWorkspaceProcessReceipt(
            receipt,
            NonCriticalWorkspaceProcessToolNames);
    }

    private static bool IsFailedToolReceipt(ProcessAutomationToolExecutionReceipt receipt)
    {
        return ProcessAutomationReceiptObservationHelper.IsFailedReceipt(receipt);
    }

    private static bool ShouldIgnoreSupersededCriticalToolFailure(
        ProcessAutomationExecutionRunDetail detail,
        ProcessAutomationToolExecutionReceipt receipt)
        => ProcessCriticalToolFailureSuppressionRules.ShouldIgnoreSupersededCriticalToolFailure(
            detail,
            receipt,
            new ProcessCriticalToolFailureSuppressionContext(
                IsImplementationBootstrapToolName,
                IsConcreteProductMutationToolName,
                IsImplementationValidationToolName,
                ImplementationProofToolNames,
                HasCompletedDeclaredStepOutcome,
                ShouldIgnoreProviderNativeBrowserOutputFileProbeFailure));

    private static bool HasCompletedDeclaredStepOutcome(ProcessAutomationExecutionRunDetail detail)
    {
        return IsCompletedDeclaredStepOutcome(detail.Run.ResultSummary) ||
               IsCompletedDeclaredStepOutcome(ResolveRecoveredExecutionResponseText(detail));
    }

    private static bool IsCompletedDeclaredStepOutcome(string? responseText)
    {
        return TryResolveDeclaredStepOutcome(responseText, out var declaredOutcome) &&
               declaredOutcome.Status == ProcessStepRunStatus.Completed;
    }

    private static bool ShouldIgnoreProviderNativeBrowserOutputFileProbeFailure(
        ProcessAutomationExecutionRunDetail detail,
        ProcessAutomationToolExecutionReceipt receipt)
        => ProcessProviderNativeBrowserProbeFailureRules.ShouldIgnoreProviderNativeBrowserOutputFileProbeFailure(
            detail,
            receipt,
            ManagedWorkspacePathRegex,
            ResolveSuccessfulBrowserToolOutputFiles,
            TryResolveSafeBrowserOutputPath);

    private static bool TryResolveRequestedManagedPath(string requestSummary, out string path)
        => ProcessProviderNativeBrowserOutputFacts.TryResolveRequestedManagedPath(
            requestSummary,
            ManagedWorkspacePathRegex,
            out path);

    private static bool IsPlaceholderCriticalToolRequestSummary(
        string normalizedToolName,
        string? requestSummary)
        => ProcessArtifactQualityValidationRules.IsPlaceholderCriticalToolRequestSummary(
            normalizedToolName,
            requestSummary);

    private static string NormalizeToolToken(string value)
    {
        return ProcessToolReceiptFacts.NormalizeToolToken(value);
    }

    internal static string? ResolveProviderNativeBrowserWorkingDirectory(ProcessAutomationExecutionRunDetail detail)
        => ProcessProviderNativeBrowserOutputFacts.ResolveProviderNativeBrowserWorkingDirectory(detail);

    internal static string ResolveProviderNativeBrowserToolName(string expectedRelativePath)
        => ProcessArtifactProviderNativeVisualValidationRules.ResolveProviderNativeBrowserToolName(expectedRelativePath);

    private static bool IsProviderNativeBrowserEvidenceReferencePath(string relativePath)
        => ProcessArtifactProviderNativeVisualValidationRules.IsProviderNativeBrowserEvidenceReferencePath(relativePath);

    private static bool IsManagedBrowserEvidenceReferencePath(string comparablePath)
        => ProcessArtifactProviderNativeVisualValidationRules.IsManagedBrowserEvidenceReferencePath(comparablePath);

    internal static bool MatchesExpectedBrowserOutputFile(string expectedRelativePath, string outputFileName)
        => ProcessArtifactProviderNativeVisualValidationRules.MatchesExpectedBrowserOutputFile(expectedRelativePath, outputFileName);

    internal static string GuessContentTypeFromPath(string fullPath)
        => ProcessArtifactKindClassificationRules.GuessContentTypeFromPath(fullPath);

    private static string BuildArtifactTitle(ProcessAutomationExecutionArtifact artifact)
        => ProcessArtifactProjectionPlanner.BuildArtifactTitle(artifact);

    private static string BuildExternalReferenceKey(ProcessAutomationExecutionArtifact artifact)
        => ProcessArtifactProjectionPlanner.BuildExecutionArtifactExternalReferenceKey(artifact.Id);

    internal static string BuildCompletedDecisionArtifactExternalReferenceKey(Guid stepRunId, Guid artifactExpectationId)
        => ProcessExecutionArtifactMetadataRules.BuildCompletedDecisionArtifactExternalReferenceKey(
            stepRunId,
            artifactExpectationId);

    private static string BuildProviderNativeBrowserArtifactExternalReferenceKey(Guid executionRunId, string relativePath)
        => ProcessArtifactProjectionPlanner.BuildProviderNativeBrowserArtifactExternalReferenceKey(executionRunId, relativePath);

    private static string BuildProcessMockArtifactExternalReferenceKey(
        Guid stepRunId,
        Guid artifactExpectationId,
        string relativePath)
        => ProcessArtifactProjectionPlanner.BuildProcessMockArtifactExternalReferenceKey(
            stepRunId,
            artifactExpectationId,
            relativePath);

    private static string BuildMissingTechnicalAgentBindingDiagnostic(
        Guid processRunId,
        Guid stepRunId,
        string stepTitle,
        Guid currentExecutorPartyId,
        AiResourceBindingStatus? bindingStatus,
        Guid? technicalAgentId)
        => ProcessExecutionArtifactMetadataRules.BuildMissingTechnicalAgentBindingDiagnostic(
            processRunId,
            stepRunId,
            stepTitle,
            currentExecutorPartyId,
            bindingStatus,
            technicalAgentId);

    internal static string BuildStorageRelativePath(
        DispatchCandidate candidate,
        ProcessAutomationExecutionArtifact artifact)
        => ProcessExecutionArtifactMetadataRules.BuildStorageRelativePath(
            candidate.Run.Id,
            candidate.StepRun.Id,
            artifact.RelativePath);

    internal static ProcessArtifactKind ResolveProcessArtifactKind(
        DispatchCandidate candidate,
        ProcessAutomationExecutionArtifact artifact)
        => ProcessArtifactKindClassificationRules.ResolveProcessArtifactKind(
            artifact,
            ResolveArtifactExpectation(candidate, artifact)?.ArtifactKind);

    internal static StorageContentKind ResolveStorageContentKind(string contentType, string fullPath)
        => ProcessStorageContentKindRules.ResolveStorageContentKind(contentType, fullPath);

    internal static string NormalizeTrigger(string trigger, Guid? stepRunId)
    {
        if (!string.IsNullOrWhiteSpace(trigger))
        {
            return trigger.Trim();
        }

        return stepRunId.HasValue
            ? $"step:{stepRunId.Value:D}"
            : "process-runtime";
    }

    internal static bool IsWithinWorkspace(string workspaceRoot, string fullPath)
    {
        var normalizedWorkspaceRoot = Path.GetFullPath(workspaceRoot);
        var normalizedFullPath = Path.GetFullPath(fullPath);
        return string.Equals(normalizedFullPath, normalizedWorkspaceRoot, StringComparison.OrdinalIgnoreCase) ||
               normalizedFullPath.StartsWith(EnsureTrailingDirectorySeparator(normalizedWorkspaceRoot), StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryResolveArtifactFullPath(
        string workspaceRoot,
        string relativePath,
        out string fullPath,
        out string failureReason)
    {
        fullPath = string.Empty;
        failureReason = string.Empty;

        var normalizedRelativePath = WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(normalizedRelativePath))
        {
            failureReason = "Artifact relative path is empty.";
            return false;
        }

        if (IsExternalTargetAliasPath(normalizedRelativePath))
        {
            return TryResolveExternalTargetArtifactFullPath(normalizedRelativePath, out fullPath, out failureReason);
        }

        fullPath = Path.GetFullPath(Path.Combine(
            workspaceRoot,
            normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (IsWithinWorkspace(workspaceRoot, fullPath))
        {
            return true;
        }

        failureReason = $"Artifact path '{normalizedRelativePath}' resolves outside the workspace root.";
        fullPath = string.Empty;
        return false;
    }

    private static bool TryResolveExternalTargetArtifactFullPath(
        string normalizedRelativePath,
        out string fullPath,
        out string failureReason)
    {
        fullPath = string.Empty;
        failureReason = string.Empty;

        var suffix = normalizedRelativePath.Length == ExternalTargetAliasRoot.Length
            ? string.Empty
            : normalizedRelativePath[(ExternalTargetAliasRoot.Length + 1)..];
        var segments = suffix.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0 ||
            segments[0].Length != 1 ||
            !char.IsLetter(segments[0][0]))
        {
            failureReason = $"Artifact path '{normalizedRelativePath}' uses invalid external-target syntax.";
            return false;
        }

        var driveRoot = $"{char.ToUpperInvariant(segments[0][0])}:{Path.DirectorySeparatorChar}";
        var remainingSegments = segments.Skip(1).ToArray();
        fullPath = Path.GetFullPath(
            remainingSegments.Length == 0
                ? driveRoot
                : Path.Combine(driveRoot, Path.Combine(remainingSegments)));
        return true;
    }

    private static bool IsExternalTargetAliasPath(string normalizedRelativePath)
    {
        return string.Equals(normalizedRelativePath, ExternalTargetAliasRoot, StringComparison.OrdinalIgnoreCase) ||
               normalizedRelativePath.StartsWith(ExternalTargetAliasRoot + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static IReadOnlyList<DispatchArtifactInput> BuildResolvedArtifactInputs(
        IReadOnlyList<ProcessStepArtifactInputDefinition> configuredInputs,
        IReadOnlyDictionary<Guid, ProcessArtifactExpectation> artifactExpectationsById,
        IReadOnlyDictionary<Guid, ProcessStepDefinition> sourceStepsById,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProcessStepRun>> stepRunsByDefinitionId,
        IReadOnlyList<ProcessArtifactRecord> existingArtifacts)
    {
        return ProcessDispatchArtifactInputAssembler.BuildResolvedArtifactInputs(
            configuredInputs,
            artifactExpectationsById,
            sourceStepsById,
            stepRunsByDefinitionId,
            existingArtifacts);
    }

    internal static bool IsCurrentRunUpstreamArtifactInput(
        ProcessArtifactRecord artifact,
        IReadOnlySet<Guid> sourceStepRunIds,
        IReadOnlySet<Guid> sourceProcessRunIds)
    {
        return ProcessDispatchArtifactInputAssembler.IsCurrentRunUpstreamArtifactInput(
            artifact,
            sourceStepRunIds,
            sourceProcessRunIds);
    }

    private static string BuildArtifactInputSummary(IReadOnlyList<DispatchArtifactInput> artifactInputs)
    {
        if (artifactInputs.Count == 0)
        {
            return "No configured upstream artifact inputs for this step.";
        }

        var builder = new StringBuilder();
        foreach (var artifactInput in artifactInputs)
        {
            builder.Append("- Source step: ");
            builder.Append(artifactInput.SourceStepTitle);
            builder.Append(" | Expected artifact: ");
            builder.AppendLine(artifactInput.ExpectedArtifactTitle);

            if (artifactInput.Artifacts.Count == 0)
            {
                builder.AppendLine("  No recorded upstream artifacts are attached yet. If the contract cannot be fulfilled without them, stop and say so explicitly.");
                continue;
            }

            foreach (var artifact in artifactInput.Artifacts)
            {
                builder.Append("  - ");
                builder.Append(artifact.Title);
                builder.Append(" [");
                builder.Append(artifact.ArtifactKind);
                builder.Append(']');
                if (!string.IsNullOrWhiteSpace(artifact.ManagedStoragePath))
                {
                    builder.Append(" @ ");
                    builder.Append(artifact.ManagedStoragePath);
                }

                builder.AppendLine();
                if (!string.IsNullOrWhiteSpace(artifact.ReviewSummary))
                {
                    builder.Append("    Review: ");
                    builder.AppendLine(TrimForPrompt(artifact.ReviewSummary, 240));
                }

                if (!string.IsNullOrWhiteSpace(artifact.ProvenanceSummary))
                {
                    builder.Append("    Provenance: ");
                    builder.AppendLine(TrimForPrompt(artifact.ProvenanceSummary, 240));
                }
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string ResolveMissingUpstreamArtifactInputSummary(DispatchCandidate candidate)
    {
        var missingInputs = candidate.ArtifactInputs
            .Where(item => item.Artifacts.Count == 0)
            .Select(item =>
                $"Upstream step '{item.SourceStepTitle}' must provide required artifact '{item.ExpectedArtifactTitle}'.")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
        return missingInputs.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, missingInputs);
    }

    private IReadOnlyList<DispatchArtifactInput> PrepareArtifactInputsForPrompt(
        IReadOnlyList<DispatchArtifactInput> artifactInputs,
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope)
    {
        if (artifactInputs.Count == 0 || workspaceScope.IsDefaultSandbox)
        {
            return artifactInputs;
        }

        return ProcessDispatchArtifactInputAssembler.PrepareArtifactInputsForPrompt(
            artifactInputs,
            managedStoragePath => PrepareManagedArtifactPathForPrompt(
                managedStoragePath,
                workspaceRoot,
                workspaceScope));
    }

    private string PrepareManagedArtifactPathForPrompt(
        string managedStoragePath,
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope)
    {
        if (string.IsNullOrWhiteSpace(managedStoragePath))
        {
            return string.Empty;
        }

        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(managedStoragePath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return string.Empty;
        }

        var scopedPath = ResolveScopedManagedRelativePath(workspaceScope, normalizedPath);
        if (string.Equals(scopedPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedPath;
        }

        var sourceFullPath = Path.GetFullPath(Path.Combine(
            workspaceRoot,
            normalizedPath.Replace('/', Path.DirectorySeparatorChar)));
        var scopedFullPath = Path.GetFullPath(Path.Combine(
            workspaceRoot,
            scopedPath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinWorkspace(workspaceRoot, sourceFullPath) ||
            !IsWithinWorkspace(workspaceRoot, scopedFullPath) ||
            !File.Exists(sourceFullPath))
        {
            return normalizedPath;
        }

        var scopedDirectory = Path.GetDirectoryName(scopedFullPath);
        if (!string.IsNullOrWhiteSpace(scopedDirectory))
        {
            Directory.CreateDirectory(scopedDirectory);
        }

        if (!string.Equals(sourceFullPath, scopedFullPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourceFullPath, scopedFullPath, overwrite: true);
        }

        return File.Exists(scopedFullPath)
            ? scopedPath
            : normalizedPath;
    }

    private static string TrimForPrompt(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.ReplaceLineEndings(" ").Trim();
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return normalized[..maxLength].TrimEnd() + "...";
    }

    internal static bool SatisfiesExpectedArtifactInput(
        ProcessArtifactRecord artifact,
        ProcessArtifactExpectation expectation)
    {
        if (artifact.ArtifactKind != expectation.ArtifactKind)
        {
            return false;
        }

        if (artifact.ArtifactExpectationId.HasValue)
        {
            return artifact.ArtifactExpectationId.Value == expectation.Id;
        }

        return string.Equals(artifact.Title, expectation.Title, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildExpectedArtifactSummary(
        DispatchCandidate candidate,
        string? projectStructureGroundingSummary = null)
    {
        var expectedArtifacts = candidate.ExpectedArtifacts;
        if (expectedArtifacts.Count == 0)
        {
            return "No explicit artifact outputs are configured for this step.";
        }

        var builder = new StringBuilder();
        foreach (var expectedArtifact in expectedArtifacts)
        {
            builder.Append("- ");
            builder.Append(expectedArtifact.Title);
            builder.Append(" [");
            builder.Append(expectedArtifact.ArtifactKind);
            builder.Append(']');
            if (expectedArtifact.IsRequired)
            {
                builder.Append(" required");
            }

            builder.AppendLine();
            if (!string.IsNullOrWhiteSpace(expectedArtifact.ValidationRequirementSummary))
            {
                builder.Append("  Validation: ");
                builder.AppendLine(TrimForPrompt(expectedArtifact.ValidationRequirementSummary, 240));
            }

            if (TryResolveProjectStructureExpectedArtifactPath(
                    candidate,
                    expectedArtifact,
                    projectStructureGroundingSummary,
                    out var governedPath))
            {
                builder.Append("  Governed path: ");
                builder.AppendLine(governedPath);
                builder.AppendLine("  Contract: this required output must be created at the governed path; an internal-only artifact or wrong-root file does not satisfy it.");
            }

            var suggestedManagedPath = ResolveSuggestedManagedArtifactPath(candidate, expectedArtifact);
            if (!string.IsNullOrWhiteSpace(suggestedManagedPath))
            {
                builder.Append("  Managed path: ");
                builder.AppendLine(suggestedManagedPath);
            }

            builder.Append("  Trust: ");
            builder.Append(expectedArtifact.TrustRequirement);
            builder.Append(" | Sensitivity: ");
            builder.AppendLine(expectedArtifact.SensitivityLevel.ToString());
        }

        return builder.ToString().TrimEnd();
    }

    private static string ResolveSuggestedManagedArtifactPath(
        DispatchCandidate candidate,
        DispatchArtifactExpectation expectedArtifact)
    {
        if (TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var declaredRelativePath))
        {
            return WorkspaceScopeDescriptor.NormalizeRelativePath(declaredRelativePath);
        }

        return CanProjectResponseTextArtifactWithoutDeclaredPath(expectedArtifact)
            ? BuildFallbackResponseTextArtifactRelativePath(candidate, expectedArtifact)
            : string.Empty;
    }

    internal static Guid? ResolveArtifactExpectationId(
        DispatchCandidate candidate,
        ProcessAutomationExecutionArtifact artifact)
    {
        return ResolveArtifactExpectation(candidate, artifact)?.Id;
    }

    internal static Guid? ResolveArtifactExpectationId(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        ProcessAutomationExecutionArtifact artifact)
    {
        return ResolveArtifactExpectation(candidate, detail.Run.InputSummary, artifact)?.Id;
    }

    internal static DispatchArtifactExpectation? ResolveArtifactExpectation(
        DispatchCandidate candidate,
        ProcessAutomationExecutionArtifact artifact)
    {
        return ResolveArtifactExpectation(candidate, artifact, artifactTextContent: null);
    }

    internal static DispatchArtifactExpectation? ResolveArtifactExpectation(
        DispatchCandidate candidate,
        ProcessAutomationExecutionArtifact artifact,
        string? artifactTextContent)
    {
        return ResolveArtifactExpectation(candidate, null, artifact, artifactTextContent);
    }

    internal static DispatchArtifactExpectation? ResolveArtifactExpectation(
        DispatchCandidate candidate,
        string? projectStructureContractText,
        ProcessAutomationExecutionArtifact artifact)
    {
        return ResolveArtifactExpectation(candidate, projectStructureContractText, artifact, artifactTextContent: null);
    }

    internal static DispatchArtifactExpectation? ResolveArtifactExpectation(
        DispatchCandidate candidate,
        string? projectStructureContractText,
        ProcessAutomationExecutionArtifact artifact,
        string? artifactTextContent)
    {
        var governedArtifacts = ResolveProjectStructureRequiredArtifactPaths(projectStructureContractText);
        if (governedArtifacts.Count > 0)
        {
            foreach (var expectedArtifact in candidate.ExpectedArtifacts)
            {
                if (TryResolveProjectStructureExpectedArtifactPath(
                        expectedArtifact,
                        governedArtifacts,
                        out var governedPath) &&
                    ArtifactPathMatchesGovernedProjectStructurePath(artifact.RelativePath, governedPath))
                {
                    return expectedArtifact;
                }
            }

            var ungovernedExpectedArtifacts = candidate.ExpectedArtifacts
                .Where(item => !TryResolveProjectStructureExpectedArtifactPath(item, governedArtifacts, out _))
                .ToList();
            if (ungovernedExpectedArtifacts.Count == 0)
            {
                return null;
            }

            var ungovernedMatchedExpectationId = MatchExpectedArtifactId(ungovernedExpectedArtifacts, artifact, artifactTextContent);
            return ungovernedMatchedExpectationId.HasValue
                ? ungovernedExpectedArtifacts.FirstOrDefault(item => item.Id == ungovernedMatchedExpectationId.Value)
                : null;
        }

        var matchedExpectationId = MatchExpectedArtifactId(candidate.ExpectedArtifacts, artifact, artifactTextContent);
        if (!matchedExpectationId.HasValue)
        {
            return null;
        }

        return candidate.ExpectedArtifacts.FirstOrDefault(item => item.Id == matchedExpectationId.Value);
    }

    internal static Guid? MatchExpectedArtifactId(
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
        ProcessAutomationExecutionArtifact artifact)
    {
        return MatchExpectedArtifactId(expectedArtifacts, artifact, artifactTextContent: null);
    }

    internal static Guid? MatchExpectedArtifactId(
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
        ProcessAutomationExecutionArtifact artifact,
        string? artifactTextContent)
    {
        var snapshot = ProcessArtifactValidationSnapshotBuilder.FromDispatchExpectations(
            expectedArtifacts,
            projectStructureContractText: null);

        return MatchExpectedArtifactId(snapshot.ExpectedArtifacts, artifact, artifactTextContent);
    }

    private static Guid? MatchExpectedArtifactId(
        IReadOnlyList<ProcessArtifactExpectationSnapshot> expectedArtifacts,
        ProcessAutomationExecutionArtifact artifact,
        string? artifactTextContent)
    {
        if (expectedArtifacts.Count == 0)
        {
            return null;
        }

        if (IsTransientExecutionArtifact(artifact))
        {
            return null;
        }

        var relativePath = artifact.RelativePath.Replace('\\', '/');
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(relativePath);
        var displayName = BuildArtifactTitle(artifact);
        var displaySlug = FileSafeSlugBuilder.Build(displayName);
        var fileSlug = FileSafeSlugBuilder.Build(fileNameWithoutExtension);
        var expectedKind = ResolveExpectedArtifactKind(artifact);
        var expectedArtifactsById = expectedArtifacts.ToDictionary(item => item.Id);
        var strongMatchedExpectationId = ProcessArtifactExpectationMatcher.MatchStrongExpectedArtifactId(
            expectedArtifacts,
            expectedKind,
            item => MatchesExpectedArtifact(
                expectedArtifactsById[item.Id],
                artifact,
                relativePath,
                displayName,
                displaySlug,
                fileSlug));
        if (strongMatchedExpectationId.HasValue)
        {
            return strongMatchedExpectationId.Value;
        }

        var providerNativeVisualMatches = expectedArtifacts
            .Select(item => new
            {
                Expectation = item,
                Score = ScoreProviderNativeVisualArtifactExpectation(item, artifact, relativePath, displayName)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Expectation.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (providerNativeVisualMatches.Count == 1 ||
            providerNativeVisualMatches.Count > 1 &&
            providerNativeVisualMatches[0].Score > providerNativeVisualMatches[1].Score)
        {
            return providerNativeVisualMatches[0].Expectation.Id;
        }

        var managedNarrativeMatches = expectedArtifacts
            .Where(item => IsManagedNarrativeArtifactFallbackMatch(
                expectedArtifacts,
                item,
                artifact,
                relativePath,
                displayName,
                artifactTextContent))
            .ToList();
        if (managedNarrativeMatches.Count == 1)
        {
            return managedNarrativeMatches[0].Id;
        }

        return MatchExpectedArtifactIdByTextContent(
            expectedArtifacts,
            artifact,
            expectedKind,
            artifactTextContent);
    }

    private static Guid? MatchExpectedArtifactIdByTextContent(
        IReadOnlyList<ProcessArtifactExpectationSnapshot> expectedArtifacts,
        ProcessAutomationExecutionArtifact artifact,
        ProcessArtifactKind expectedKind,
        string? artifactTextContent)
    {
        if (string.IsNullOrWhiteSpace(artifactTextContent) ||
            !CanMatchArtifactByTextContent(artifact))
        {
            return null;
        }

        if (IsProviderNativeBrowserOutputArtifact(artifact))
        {
            return null;
        }

        var normalizedContent = CollapsePromptWhitespace(artifactTextContent);
        var contentMatches = expectedArtifacts
            .Where(item => !TryExtractExpectedArtifactRelativePath(item.ValidationRequirementSummary, out _))
            .Where(item => !ShouldIgnoreProductSourceForNarrativeExpectation(item, artifact.RelativePath))
            .Where(item => HasExpectedArtifactContentSignals(item, artifactTextContent, normalizedContent))
            .ToList();
        if (contentMatches.Count == 1)
        {
            return contentMatches[0].Id;
        }

        if (contentMatches.Count > 1)
        {
            var kindMatches = contentMatches
                .Where(item => item.ArtifactKind == expectedKind)
                .ToList();
            if (kindMatches.Count == 1)
            {
                return kindMatches[0].Id;
            }
        }

        return null;
    }

    private static bool MatchesExpectedArtifact(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        ProcessAutomationExecutionArtifact artifact,
        string relativePath,
        string displayName,
        string displaySlug,
        string fileSlug)
    {
        if (ShouldIgnoreProductSourceForNarrativeExpectation(expectedArtifact, relativePath))
        {
            return false;
        }

        if (ProcessArtifactProviderNativeVisualValidationRules.RequiresVisualArtifactFile(expectedArtifact) &&
            !ProcessArtifactProviderNativeVisualValidationRules.IsImageArtifact(artifact))
        {
            return false;
        }

        if (TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var expectedRelativePath))
        {
            return string.Equals(
                NormalizeManagedRelativePathForComparison(expectedRelativePath),
                NormalizeManagedRelativePathForComparison(relativePath),
                StringComparison.OrdinalIgnoreCase);
        }

        if (IsProviderNativeBrowserOutputArtifact(artifact))
        {
            return false;
        }

        if (string.Equals(expectedArtifact.Title, displayName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var expectedSlug = FileSafeSlugBuilder.Build(expectedArtifact.Title);
        return string.Equals(expectedSlug, displaySlug, StringComparison.Ordinal) ||
               string.Equals(expectedSlug, fileSlug, StringComparison.Ordinal) ||
               relativePath.Contains(expectedSlug, StringComparison.OrdinalIgnoreCase) ||
               MatchesExpectedArtifactByTitleTokens(expectedArtifact.Title, relativePath, displayName);
    }

    private static int ScoreProviderNativeVisualArtifactExpectation(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        ProcessAutomationExecutionArtifact artifact,
        string relativePath,
        string displayName)
    {
        if (ShouldIgnoreProductSourceForNarrativeExpectation(expectedArtifact, relativePath) ||
            TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out _))
        {
            return 0;
        }

        return ProcessArtifactProviderNativeVisualValidationRules.ScoreProviderNativeVisualArtifactExpectation(
            expectedArtifact,
            artifact,
            relativePath,
            displayName);
    }

    private static bool MatchesExpectedArtifactByTitleTokens(
        string expectedTitle,
        string relativePath,
        string displayName)
        => ProcessArtifactTextMatchRules.MatchesExpectedArtifactByTitleTokens(expectedTitle, relativePath, displayName);

    private static bool IsManagedNarrativeArtifactFallbackMatch(
        IReadOnlyList<ProcessArtifactExpectationSnapshot> expectedArtifacts,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        ProcessAutomationExecutionArtifact artifact,
        string relativePath,
        string displayName,
        string? artifactTextContent)
    {
        if (!IsNarrativeEvidenceArtifactExpectation(expectedArtifact) ||
            IsProviderNativeBrowserOutputArtifact(artifact) ||
            ProcessArtifactProviderNativeVisualValidationRules.RequiresVisualArtifactFile(expectedArtifact) ||
            !IsManagedRunTextArtifactPath(relativePath) ||
            expectedArtifacts.Count(IsNarrativeEvidenceArtifactExpectation) != 1)
        {
            return false;
        }

        var observedText = CollapsePromptWhitespace($"{relativePath} {displayName} {artifactTextContent}").ToLowerInvariant();
        if (!ContainsNarrativeArtifactSignal(observedText))
        {
            return false;
        }

        var expectedText = CollapsePromptWhitespace(
            $"{expectedArtifact.Title} {expectedArtifact.ValidationRequirementSummary}").ToLowerInvariant();
        return SharesNarrativeArtifactPurpose(expectedText, observedText);
    }

    private static bool IsManagedRunTextArtifactPath(string relativePath)
    {
        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(normalizedPath) ||
            !IsTextReadableManagedArtifactPath(normalizedPath))
        {
            return false;
        }

        var segments = normalizedPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Any(segment => string.Equals(segment, "process-runs", StringComparison.OrdinalIgnoreCase)) &&
               segments.Any(IsManagedEvidenceRootSegment);
    }

    private static bool ContainsNarrativeArtifactSignal(string text)
        => ProcessArtifactTextMatchRules.ContainsNarrativeArtifactSignal(text);

    private static bool SharesNarrativeArtifactPurpose(string expectedText, string observedText)
        => ProcessArtifactTextMatchRules.SharesNarrativeArtifactPurpose(expectedText, observedText);

    private static IReadOnlyList<string> TokenizeArtifactComparisonText(string value)
        => ProcessArtifactTextMatchRules.TokenizeArtifactComparisonText(value);

    private static bool TryResolveProcessMockArtifactProjection(
        string? serializedSessionStateJson,
        out ProcessMockArtifactProjection projection)
    {
        var projections = ResolveProcessMockArtifactProjections(serializedSessionStateJson);
        projection = projections.FirstOrDefault();
        return projections.Count > 0;
    }

    internal static IReadOnlyList<ProcessMockArtifactProjection> ResolveProcessMockArtifactProjections(
        string? serializedSessionStateJson)
    {
        if (string.IsNullOrWhiteSpace(serializedSessionStateJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(serializedSessionStateJson);
            var root = document.RootElement;
            if (!root.TryGetProperty(ProcessMockSessionFlagPropertyName, out var processMockFlag) ||
                processMockFlag.ValueKind != JsonValueKind.True ||
                !TryGetStringProperty(root, ProcessMockRoleKeyPropertyName, out var roleKey) ||
                !TryGetStringProperty(root, ProcessMockArtifactRootPropertyName, out var artifactRoot))
            {
                return [];
            }

            var normalizedRoot = WorkspaceScopeDescriptor.NormalizeRelativePath(artifactRoot);
            if (string.IsNullOrWhiteSpace(normalizedRoot))
            {
                return [];
            }

            var branchOutcomeKey = TryGetStringProperty(root, ProcessMockBranchOutcomeKeyPropertyName, out var resolvedBranchOutcomeKey)
                ? resolvedBranchOutcomeKey
                : null;
            var projections = new List<ProcessMockArtifactProjection>();
            if (root.TryGetProperty("artifacts", out var artifactsElement) &&
                artifactsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var artifactElement in artifactsElement.EnumerateArray())
                {
                    if ((!TryGetStringProperty(artifactElement, "relativePath", out var relativePath) &&
                         !TryGetStringProperty(artifactElement, "RelativePath", out relativePath)) ||
                        (!TryGetStringProperty(artifactElement, "contentSignalText", out var contentSignalText) &&
                         !TryGetStringProperty(artifactElement, "ContentSignalText", out contentSignalText)))
                    {
                        continue;
                    }

                    projections.Add(new ProcessMockArtifactProjection(
                        roleKey.Trim(),
                        branchOutcomeKey,
                        WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath),
                        contentSignalText));
                }
            }

            if (projections.Count > 0)
            {
                return projections;
            }

            if (!TryResolveProcessMockArtifactFile(roleKey, branchOutcomeKey, out var fileName, out var fallbackContentSignalText))
            {
                return [];
            }

            return
            [
                new ProcessMockArtifactProjection(
                    roleKey.Trim(),
                    branchOutcomeKey,
                    WorkspaceScopeDescriptor.NormalizeRelativePath($"{normalizedRoot.TrimEnd('/')}/{fileName}"),
                    fallbackContentSignalText)
            ];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool TryResolveProcessMockArtifactFile(
        string roleKey,
        string? branchOutcomeKey,
        out string fileName,
        out string contentSignalText)
    {
        var normalizedRoleKey = roleKey.Trim().ToLowerInvariant();
        var normalizedBranchOutcomeKey = branchOutcomeKey?.Trim().ToLowerInvariant() ?? string.Empty;
        (fileName, contentSignalText) = (normalizedRoleKey, normalizedBranchOutcomeKey) switch
        {
            (ProcessMockProductOwnerRoleKey, _) => ("01-scope.md", "scope acceptance criteria requirements"),
            (ProcessMockArchitectRoleKey, _) => ("02-architecture.md", "architecture boundaries implementation qa expectations"),
            (ProcessMockDeveloperRoleKey, _) => ("03-implementation.md", "implementation change set deliverable validation evidence"),
            (ProcessMockQaRoleKey, ProcessMockBranchRepairsRequired) => ("04-qa-finding.md", "qa rejection finding repair branch reason"),
            (ProcessMockRepairDeveloperRoleKey, _) => ("05-repair.md", "repair implementation validation evidence"),
            (ProcessMockQaRoleKey, ProcessMockBranchApproved) => ("06-qa-approval.md", "qa approval implementation release evidence"),
            (ProcessMockReleaseManagerRoleKey, _) => ("07-release-notes.md", "release notes qa approval rollout evidence"),
            (ProcessMockBusinessStrategistRoleKey, _) => ("08-business-strategy.md", "business strategy intake product evidence assessment business plan integrated review approved handoff"),
            (ProcessMockFinancialStrategistRoleKey, _) => ("09-financial-model.md", "financial model sensitivity note drivers assumptions ranges data gaps"),
            (ProcessMockMarketingSpecialistRoleKey, _) => ("10-marketing-plan.md", "marketing market experiment plan audience promise channels metrics validation"),
            _ => (string.Empty, string.Empty)
        };

        return !string.IsNullOrWhiteSpace(fileName);
    }

    internal static bool ProcessMockArtifactMatchesExpectation(
        DispatchArtifactExpectation expectedArtifact,
        ProcessMockArtifactProjection projection)
    {
        return ProcessMockImplementationProofBridge.MatchesExpectedArtifact(expectedArtifact, projection);
    }

    private static bool CanSatisfyConcreteImplementationProofWithProcessMock(
        DispatchCandidate candidate,
        ProcessMockArtifactProjection projection)
    {
        return ProcessMockImplementationProofBridge.CanSatisfyConcreteImplementationProof(
            RequiresConcreteImplementationProof(candidate),
            candidate.ExpectedArtifacts,
            projection);
    }

    private static bool IsProcessMockImplementationRole(string roleKey)
    {
        return ProcessMockImplementationProofBridge.IsImplementationRole(roleKey);
    }

    private static bool ProcessMockProjectionMatchesRequiredArtifact(
        DispatchCandidate candidate,
        ProcessMockArtifactProjection projection)
    {
        return ProcessMockImplementationProofBridge.MatchesRequiredArtifact(candidate.ExpectedArtifacts, projection);
    }

    private static bool TryGetStringProperty(
        JsonElement root,
        string propertyName,
        out string value)
    {
        if (root.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString()?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        value = string.Empty;
        return false;
    }

    internal static bool TryExtractExpectedArtifactRelativePath(string validationRequirementSummary, out string relativePath)
        => ProcessArtifactPathValidationRules.TryExtractExpectedArtifactRelativePath(validationRequirementSummary, out relativePath);

    internal static IReadOnlyList<ProjectStructureRequiredArtifactPath> ResolveProjectStructureRequiredArtifactPaths(string? text)
        => ProcessProjectStructureArtifactPathRules.ResolveProjectStructureRequiredArtifactPaths(
            text,
            TryMapAbsoluteExternalPathToAlias);

    internal static bool TryResolveProjectStructureExpectedArtifactPath(
        DispatchCandidate candidate,
        DispatchArtifactExpectation expectedArtifact,
        string? projectStructureContractText,
        out string governedPath)
    {
        return TryResolveProjectStructureExpectedArtifactPath(
            expectedArtifact,
            ResolveProjectStructureRequiredArtifactPaths(projectStructureContractText),
            out governedPath);
    }

    internal static bool TryResolveProjectStructureExpectedArtifactPath(
        DispatchArtifactExpectation expectedArtifact,
        IReadOnlyList<ProjectStructureRequiredArtifactPath> requiredArtifactPaths,
        out string governedPath)
        => ProcessProjectStructureArtifactPathRules.TryResolveProjectStructureExpectedArtifactPath(
            expectedArtifact,
            requiredArtifactPaths,
            out governedPath);

    internal static int ScoreProjectStructureArtifactPathMatch(
        DispatchArtifactExpectation expectedArtifact,
        string fileName)
        => ProcessProjectStructureArtifactPathRules.ScoreProjectStructureArtifactPathMatch(
            expectedArtifact,
            fileName);

    internal static bool ArtifactPathMatchesGovernedProjectStructurePath(
        string observedPath,
        string governedPath)
        => ProcessProjectStructureArtifactPathRules.ArtifactPathMatchesGovernedProjectStructurePath(
            observedPath,
            governedPath,
            TryMapAbsoluteExternalPathToAlias);

    private static GovernedInspectionPaths ResolveGovernedInspectionPaths(IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts)
        => ToGovernedInspectionPaths(
            ProcessGovernedArtifactInspectionRules.ResolveGovernedInspectionPaths(expectedArtifacts));

    private static GovernedInspectionPaths ResolveArtifactInputInspectionPaths(IReadOnlyList<DispatchArtifactInput> artifactInputs)
        => ToGovernedInspectionPaths(
            ProcessGovernedArtifactInspectionRules.ResolveArtifactInputInspectionPaths(artifactInputs));

    private static string ResolveMissingUpstreamArtifactInspectionSummary(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
        => ProcessGovernedArtifactInspectionRules.ResolveMissingUpstreamArtifactInspectionSummary(
            ToGovernedInspectionPathSet(ResolveMissingUpstreamArtifactInspectionPaths(candidate, detail)));

    private static GovernedInspectionPaths ResolveMissingUpstreamArtifactInspectionPaths(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
        => ToGovernedInspectionPaths(
            ProcessGovernedArtifactInspectionRules.ResolveMissingUpstreamArtifactInspectionPaths(
                RequiresGovernedInspection(candidate.StepRun),
                candidate.ArtifactInputs,
                ResolveSuccessfulSessionPathStats(detail.Run.SerializedSessionStateJson)
                    .Select(path => path.Path)
                    .ToList(),
                ResolveSuccessfulSessionFileReads(detail.Run.SerializedSessionStateJson)
                    .Select(file => file.Path)
                    .ToList(),
                detail.ToolReceipts,
                ResolveManagedWorkspacePathsFromReceipt));

    private static string FormatPromptPathList(IReadOnlyList<string> relativePaths)
        => ProcessGovernedArtifactInspectionRules.FormatPromptPathList(relativePaths);

    private static GovernedInspectionPaths ToGovernedInspectionPaths(ProcessGovernedInspectionPathSet paths)
        => new(paths.StatPaths, paths.ReadPaths);

    private static ProcessGovernedInspectionPathSet ToGovernedInspectionPathSet(GovernedInspectionPaths paths)
        => new(paths.StatPaths, paths.ReadPaths);

    private static string NormalizeManagedRelativePathForComparison(string relativePath)
        => ProcessArtifactPathValidationRules.NormalizeManagedRelativePathForComparison(relativePath);

    private static bool IsVisualEvidenceAttachmentPath(string relativePath)
    {
        return ProcessManagedArtifactPathClassificationRules.IsVisualEvidenceAttachmentPath(relativePath);
    }

    private static bool IsResponseProjectableTextArtifact(string relativePath)
    {
        return ProcessManagedArtifactPathClassificationRules.IsResponseProjectableTextArtifact(relativePath);
    }

    internal static bool TryResolveResponseTextArtifactRelativePath(
        DispatchCandidate candidate,
        WorkspaceScopeDescriptor workspaceScope,
        DispatchArtifactExpectation expectedArtifact,
        out string relativePath)
    {
        if (TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var declaredRelativePath))
        {
            if (!IsResponseProjectableTextArtifact(declaredRelativePath))
            {
                relativePath = string.Empty;
                return false;
            }

            relativePath = ResolveScopedManagedRelativePath(workspaceScope, declaredRelativePath);
            return !string.IsNullOrWhiteSpace(relativePath);
        }

        if (!CanProjectResponseTextArtifactWithoutDeclaredPath(expectedArtifact))
        {
            relativePath = string.Empty;
            return false;
        }

        relativePath = ResolveScopedManagedRelativePath(
            workspaceScope,
            BuildFallbackResponseTextArtifactRelativePath(candidate, expectedArtifact));
        return !string.IsNullOrWhiteSpace(relativePath);
    }

    private static bool CanProjectResponseTextArtifactWithoutDeclaredPath(DispatchArtifactExpectation expectedArtifact)
    {
        return ProcessResponseTextArtifactSatisfactionRules.CanProjectResponseTextArtifactWithoutDeclaredPath(expectedArtifact);
    }

    private static string BuildFallbackResponseTextArtifactRelativePath(
        DispatchCandidate candidate,
        DispatchArtifactExpectation expectedArtifact)
    {
        return ProcessResponseTextArtifactSatisfactionRules.BuildFallbackResponseTextArtifactRelativePath(
            BuildCurrentRunManagedArtifactRoot(candidate),
            candidate.StepRun.Sequence,
            expectedArtifact);
    }

    private static string BuildCurrentRunManagedArtifactRoot(DispatchCandidate candidate)
    {
        return WorkspaceScopeDescriptor.NormalizeRelativePath(
            Path.Combine(
                "artifacts",
                "process-runs",
                candidate.Run.Id.ToString("D")));
    }

    private static string BuildCurrentRunManagedOutputRoot(DispatchCandidate candidate)
    {
        return WorkspaceScopeDescriptor.NormalizeRelativePath(
            Path.Combine(
                "output",
                "process-runs",
                candidate.Run.Id.ToString("D")));
    }

    private static bool IsTextReadableManagedArtifactPath(string relativePath)
    {
        return ProcessManagedArtifactPathClassificationRules.IsTextReadableManagedArtifactPath(relativePath);
    }

    private static bool CanMatchArtifactByTextContent(ProcessAutomationExecutionArtifact artifact)
        => ProcessExecutionArtifactTextContentRules.CanMatchArtifactByTextContent(artifact);

    private static bool IsProviderNativeBrowserOutputArtifact(ProcessAutomationExecutionArtifact artifact)
        => ProcessArtifactProviderNativeVisualValidationRules.IsProviderNativeBrowserOutputArtifact(artifact);

    internal static string? TryDecodeTextArtifactContent(
        ProcessAutomationExecutionArtifact artifact,
        string fullPath,
        byte[] content)
        => ProcessExecutionArtifactTextContentRules.TryDecodeTextArtifactContent(
            artifact,
            fullPath,
            content);

    private static bool ShouldProjectFinalAssistantResponse(ProcessAutomationExecutionRunRecord run)
    {
        return run.State == ProcessAutomationExecutionState.Completed &&
               run.Outcome == ProcessAutomationRunOutcome.Succeeded;
    }

    internal static bool ShouldProjectResponseTextArtifacts(
        ProcessAutomationExecutionRunRecord run,
        ProcessStepRunStatus completionStatus)
    {
        return completionStatus == ProcessStepRunStatus.Completed &&
               ShouldProjectFinalAssistantResponse(run);
    }

    private static string BuildResponseTextArtifactExternalReferenceKey(Guid executionRunId, string relativePath)
        => ProcessArtifactProjectionPlanner.BuildResponseTextArtifactExternalReferenceKey(executionRunId, relativePath);

    private static string BuildWorkspaceWrittenArtifactExternalReferenceKey(
        Guid executionRunId,
        Guid artifactExpectationId,
        string relativePath)
        => ProcessArtifactProjectionPlanner.BuildWorkspaceWrittenArtifactExternalReferenceKey(
            executionRunId,
            artifactExpectationId,
            relativePath);

    private static string BuildExistingManagedArtifactExternalReferenceKey(
        Guid executionRunId,
        Guid artifactExpectationId,
        string relativePath)
        => ProcessArtifactProjectionPlanner.BuildExistingManagedArtifactExternalReferenceKey(
            executionRunId,
            artifactExpectationId,
            relativePath);

    internal static string ResolveWorkspaceWrittenArtifactRelativePath(
        WorkspaceScopeDescriptor workspaceScope,
        string path)
        => ProcessExecutionArtifactMetadataRules.ResolveWorkspaceWrittenArtifactRelativePath(
            workspaceScope,
            path,
            IsExternalTargetAliasPath,
            TryMapAbsoluteExternalPathToAlias,
            ResolveScopedManagedRelativePath);

    internal static bool TryResolveWorkspaceWrittenArtifactSourceFullPath(
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        string writtenPath,
        string projectedRelativePath,
        out string fullPath,
        out string sourceRelativePath,
        out string failureReason)
        => ProcessExecutionArtifactMetadataRules.TryResolveWorkspaceWrittenArtifactSourceFullPath(
            workspaceRoot,
            workspaceScope,
            writtenPath,
            projectedRelativePath,
            IsExternalTargetAliasPath,
            TryMapAbsoluteExternalPathToAlias,
            ResolveScopedManagedRelativePath,
            TryResolveArtifactFullPath,
            out fullPath,
            out sourceRelativePath,
            out failureReason);

    internal static IReadOnlyList<string> ResolveWorkspaceWrittenArtifactSourceRelativePaths(
        WorkspaceScopeDescriptor workspaceScope,
        string writtenPath,
        string projectedRelativePath)
        => ProcessExecutionArtifactMetadataRules.ResolveWorkspaceWrittenArtifactSourceRelativePaths(
            workspaceScope,
            writtenPath,
            projectedRelativePath,
            IsExternalTargetAliasPath,
            TryMapAbsoluteExternalPathToAlias,
            ResolveScopedManagedRelativePath);

    internal static bool ShouldAutoRecordCompletedDecisionArtifact(DispatchArtifactExpectation expectedArtifact)
        => ProcessExecutionArtifactMetadataRules.ShouldAutoRecordCompletedDecisionArtifact(expectedArtifact);

    internal static ProcessArtifactTrustStatus ResolveCompletedDecisionArtifactTrustStatus(
        ProcessArtifactTrustRequirement trustRequirement)
        => ProcessExecutionArtifactMetadataRules.ResolveCompletedDecisionArtifactTrustStatus(trustRequirement);

    internal static ProcessArtifactTrustStatus ResolveProjectedArtifactTrustStatus(
        DispatchArtifactExpectation expectedArtifact,
        ProcessStepRunStatus completionStatus)
        => ProcessArtifactProjectionPlanner.ResolveProjectedArtifactTrustStatus(
            ProcessArtifactValidationSnapshotBuilder.FromDispatchExpectation(expectedArtifact),
            completionStatus);

    internal static string BuildCompletedDecisionArtifactProvenanceSummary(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
    {
        var executorName = string.IsNullOrWhiteSpace(candidate.StepRun.CurrentExecutorName)
            ? "the assigned approver"
            : candidate.StepRun.CurrentExecutorName.Trim();
        return $"Recorded from the governed step outcome for AgentFramework execution run {detail.Run.Id:D} by {executorName}.";
    }

    internal static string BuildCompletedDecisionArtifactReviewSummary(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string responseText,
        DispatchArtifactExpectation expectedArtifact)
    {
        var executorName = string.IsNullOrWhiteSpace(candidate.StepRun.CurrentExecutorName)
            ? "The assigned approver"
            : candidate.StepRun.CurrentExecutorName.Trim();
        var summary = ResolveCompletedDecisionArtifactOutcomeSummary(candidate, detail, responseText);
        var builder = new StringBuilder();
        builder.Append(executorName);
        builder.Append(" completed step '");
        builder.Append(candidate.StepRun.Title);
        builder.Append("' and recorded decision artifact '");
        builder.Append(expectedArtifact.Title);
        builder.Append("'.");

        if (!string.IsNullOrWhiteSpace(summary))
        {
            builder.Append(' ');
            builder.Append(EnsureTerminalPunctuation(summary));
        }

        if (!string.IsNullOrWhiteSpace(expectedArtifact.ValidationRequirementSummary))
        {
            builder.Append(" Validation expectation: ");
            builder.Append(EnsureTerminalPunctuation(expectedArtifact.ValidationRequirementSummary.Trim()));
        }

        return builder.ToString();
    }

    private static string ResolveCompletedDecisionArtifactOutcomeSummary(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string responseText)
    {
        if (TryResolveDeclaredStepOutcome(responseText, out var declaredOutcome) &&
            !string.IsNullOrWhiteSpace(declaredOutcome.Reason))
        {
            return declaredOutcome.Reason.Trim();
        }

        if (!string.IsNullOrWhiteSpace(candidate.StepRun.DecisionSummary))
        {
            return candidate.StepRun.DecisionSummary.Trim();
        }

        if (!string.IsNullOrWhiteSpace(detail.Run.ResultSummary))
        {
            return detail.Run.ResultSummary.Trim();
        }

        var normalizedResponse = CollapsePromptWhitespace(responseText);
        if (!string.IsNullOrWhiteSpace(normalizedResponse) &&
            !string.Equals(
                normalizedResponse,
                "The provider completed without returning text.",
                StringComparison.OrdinalIgnoreCase))
        {
            return TrimForPrompt(normalizedResponse, 420);
        }

        return string.Empty;
    }

    private static string EnsureTerminalPunctuation(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (trimmed.EndsWith('.') || trimmed.EndsWith('!') || trimmed.EndsWith('?'))
        {
            return trimmed;
        }

        return $"{trimmed}.";
    }

    internal static string ResolveScopedManagedRelativePath(WorkspaceScopeDescriptor workspaceScope, string relativePath)
        => ProcessScopedManagedArtifactPathRules.ResolveScopedManagedRelativePath(
            workspaceScope,
            relativePath);

    private static bool IsManagedRootSegment(string segment)
        => ProcessArtifactPathValidationRules.IsManagedRootSegment(segment);

}
