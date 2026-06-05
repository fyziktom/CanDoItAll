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
        var texts = new List<string>();
        AddQualityValidationEvidenceText(texts, inspectionText);
        AddQualityValidationEvidenceText(texts, detail.Run.ResultSummary);

        foreach (var receipt in detail.ToolReceipts)
        {
            var toolName = NormalizeToolToken(receipt.ToolName);
            if (!IsQualityValidationEvidenceToolName(toolName))
            {
                continue;
            }

            AddQualityValidationEvidenceText(
                texts,
                string.Join(
                    Environment.NewLine,
                    receipt.RequestSummary,
                    receipt.WorkingDirectory,
                    receipt.ExitSummary));
        }

        foreach (var resultText in ResolveSuccessfulSessionToolResultTexts(detail.Run.SerializedSessionStateJson))
        {
            if (IsQualityValidationEvidenceToolName(resultText.ToolName))
            {
                AddQualityValidationEvidenceText(texts, resultText.Text);
            }
        }

        return texts;
    }

    private static bool IsQualityValidationEvidenceToolName(string normalizedToolName)
        => ProcessArtifactQualityValidationRules.IsQualityValidationEvidenceToolName(
            normalizedToolName,
            IsImplementationValidationToolName);

    private static void AddQualityValidationEvidenceText(List<string> texts, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            texts.Add(value);
        }
    }

    private static string ResolveIncompleteImplementationSummary(
        DispatchCandidate candidate,
        string? responseText)
    {
        if (!RequiresConcreteImplementationProof(candidate) || string.IsNullOrWhiteSpace(responseText))
        {
            return string.Empty;
        }

        var normalizedResponse = CollapsePromptWhitespace(responseText).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedResponse))
        {
            return string.Empty;
        }

        var defersFeatureImplementation =
            normalizedResponse.Contains("ready for feature implementation", StringComparison.Ordinal) ||
            normalizedResponse.Contains("ready for later feature implementation", StringComparison.Ordinal) ||
            normalizedResponse.Contains("ready for further feature implementation", StringComparison.Ordinal) ||
            normalizedResponse.Contains("next steps for feature implementation", StringComparison.Ordinal) ||
            normalizedResponse.Contains("future feature implementation", StringComparison.Ordinal) ||
            normalizedResponse.Contains("later feature implementation", StringComparison.Ordinal) ||
            (normalizedResponse.Contains("ready for", StringComparison.Ordinal) &&
             normalizedResponse.Contains("implementation", StringComparison.Ordinal) &&
             normalizedResponse.Contains("feature, tests, and migration notes", StringComparison.Ordinal)) ||
            (normalizedResponse.Contains("structured for further", StringComparison.Ordinal) &&
             normalizedResponse.Contains("implementation", StringComparison.Ordinal));

        if (!defersFeatureImplementation &&
            normalizedResponse.Contains("later step", StringComparison.Ordinal) &&
            normalizedResponse.Contains("feature implementation", StringComparison.Ordinal))
        {
            defersFeatureImplementation = true;
        }

        var reportsMissingRequestedBehavior =
            normalizedResponse.Contains("not yet implemented", StringComparison.Ordinal) ||
            normalizedResponse.Contains("still untouched template output", StringComparison.Ordinal) ||
            normalizedResponse.Contains("untouched template output", StringComparison.Ordinal) ||
            (normalizedResponse.Contains("hello, world!", StringComparison.Ordinal) &&
             (normalizedResponse.Contains("still", StringComparison.Ordinal) ||
              normalizedResponse.Contains("template", StringComparison.Ordinal))) ||
            (normalizedResponse.Contains("no required", StringComparison.Ordinal) &&
             normalizedResponse.Contains("present yet", StringComparison.Ordinal)) ||
            (normalizedResponse.Contains("required", StringComparison.Ordinal) &&
             normalizedResponse.Contains("is not present yet", StringComparison.Ordinal));

        var reportsDeferredExecution =
            !ContainsNegatedDeferredExecutionPhrase(normalizedResponse) &&
            (normalizedResponse.Contains("next required actions", StringComparison.Ordinal) ||
             normalizedResponse.Contains("next implementation steps", StringComparison.Ordinal) ||
             normalizedResponse.Contains("for the next agent or step", StringComparison.Ordinal) ||
             normalizedResponse.Contains("proceeding to implement", StringComparison.Ordinal));

        return defersFeatureImplementation || reportsMissingRequestedBehavior || reportsDeferredExecution
            ? "the response says the step only scaffolded the app and left the requested feature implementation for later work"
            : string.Empty;
    }

    private static bool ContainsNegatedDeferredExecutionPhrase(string normalizedResponse)
    {
        var phrases = new[]
        {
            "no next required actions",
            "no next implementation steps",
            "no further implementation steps",
            "no remaining implementation steps",
            "no implementation steps remain",
            "no follow-up implementation steps",
            "no deferred implementation steps",
            "no later implementation steps"
        };

        return phrases.Any(phrase => normalizedResponse.Contains(phrase, StringComparison.Ordinal));
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

        var missingRequiredArtifacts = candidate.ExpectedArtifacts
            .Where(item => item.IsRequired)
            .Where(item => !HasSatisfiedRequiredArtifact(candidate, detail, item, responseText))
            .Select(item => item.Title.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        return missingRequiredArtifacts.Count == 0
            ? string.Empty
            : string.Join(", ", missingRequiredArtifacts);
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
        return RequiresConcreteImplementationProof(candidate) &&
               expectedArtifact.IsRequired &&
               expectedArtifact.ArtifactKind is not ProcessArtifactKind.Decision and not ProcessArtifactKind.DecisionRecord;
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
        if (latestConcreteMutation is null)
        {
            var latestConcreteRead = ResolveLatestImplementationProofReadReceipt(candidate, successfulReceipts);
            if (latestConcreteRead is null)
            {
                return false;
            }

            return artifactReceipts.Any(receipt =>
                !IsReceiptAfter(latestConcreteRead, receipt) &&
                (latestValidation is null || !IsReceiptAfter(latestValidation, receipt)));
        }

        return artifactReceipts.Any(receipt =>
            !IsReceiptAfter(latestConcreteMutation, receipt) &&
            (latestValidation is null || !IsReceiptAfter(latestValidation, receipt)));
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
        return candidate.RecordedArtifactExpectationIds.Contains(expectedArtifact.Id) ||
               detail.Artifacts.Any(artifact => ResolveArtifactExpectationId(candidate, detail, artifact) == expectedArtifact.Id);
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
        if (TryResolveProjectStructureExpectedArtifactPath(candidate, expectedArtifact, detail.Run.InputSummary, out _))
        {
            return CanProjectWorkspaceWrittenArtifact(candidate, detail, expectedArtifact);
        }

        if (CanProjectProcessMockArtifact(candidate, detail, expectedArtifact))
        {
            return true;
        }

        if (CanProjectWorkspaceWrittenArtifact(candidate, detail, expectedArtifact))
        {
            return true;
        }

        if (CanProjectProviderNativeVisualArtifact(candidate, detail, expectedArtifact))
        {
            return true;
        }

        if (ShouldAutoRecordCompletedDecisionArtifact(expectedArtifact))
        {
            return true;
        }

        var projectableResponseText = ResolveProjectableResponseArtifactText(responseText);
        if (TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var declaredRelativePath))
        {
            return HasProviderNativeBrowserOutputForDeclaredPath(detail, declaredRelativePath) ||
                   (IsUsableProjectedResponseArtifactContent(expectedArtifact, projectableResponseText) &&
                    IsResponseProjectableTextArtifact(declaredRelativePath));
        }

        return IsUsableProjectedResponseArtifactContent(expectedArtifact, projectableResponseText) &&
               CanProjectResponseTextArtifactWithoutDeclaredPath(expectedArtifact);
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
        => ProcessExternalTargetGroundingService.InspectReferences(text, allowedAliases).Summary;

    private static string ResolveShallowSharedManagedArtifactReferenceSummary(
        ProcessAutomationExecutionRunDetail detail,
        string? responseText)
    {
        var allowedExternalTargetAliases = ResolveAllowedExternalTargetAliases(detail.Run);
        if (allowedExternalTargetAliases.Count == 0)
        {
            return string.Empty;
        }

        var shallowPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in ResolveSuccessfulSessionFileReads(detail.Run.SerializedSessionStateJson)
                     .Concat(ResolveSuccessfulSessionFileWrites(detail.Run.SerializedSessionStateJson)))
        {
            AddShallowSharedManagedArtifactPath(shallowPaths, file.Path, allowedExternalTargetAliases);
            if (shallowPaths.Count >= 3)
            {
                break;
            }
        }

        if (shallowPaths.Count < 3 && !string.IsNullOrWhiteSpace(responseText))
        {
            foreach (Match match in ManagedWorkspacePathRegex.Matches(responseText))
            {
                AddShallowSharedManagedArtifactPath(shallowPaths, match.Groups["path"].Value, allowedExternalTargetAliases);
                if (shallowPaths.Count >= 3)
                {
                    break;
                }
            }
        }

        return shallowPaths.Count == 0
            ? string.Empty
            : $"the run used shallow shared managed artifact paths instead of run-specific artifact paths: {string.Join(", ", shallowPaths)}";
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
        var extension = Path.GetExtension(fileName);
        return string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".razor", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".cshtml", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".fsproj", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".vbproj", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".css", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".js", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".ts", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".tsx", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".jsx", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyProductDeliverableOrSourceFileName(string fileName)
    {
        return IsImplementationDeliverableOrSourceExtension(Path.GetExtension(fileName));
    }

    private static bool CanProjectProcessMockArtifact(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        DispatchArtifactExpectation expectedArtifact)
    {
        return ResolveProcessMockArtifactProjections(detail.Run.SerializedSessionStateJson)
            .Any(projection => ProcessMockArtifactMatchesExpectation(expectedArtifact, projection));
    }

    private static bool CanProjectWorkspaceWrittenArtifact(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        DispatchArtifactExpectation expectedArtifact)
    {
        if (TryResolveProjectStructureExpectedArtifactPath(candidate, expectedArtifact, detail.Run.InputSummary, out var governedPath))
        {
            return ResolveSuccessfulSessionFileWrites(detail.Run.SerializedSessionStateJson)
                .Any(file => ArtifactPathMatchesGovernedProjectStructurePath(file.Path, governedPath)) ||
                detail.ToolReceipts
                    .Where(IsSuccessfulWorkspaceFileMutationReceipt)
                    .SelectMany(ResolveManagedWorkspacePathsFromReceipt)
                    .Any(path => ArtifactPathMatchesGovernedProjectStructurePath(path, governedPath));
        }

        if (ResolveSuccessfulSessionFileWrites(detail.Run.SerializedSessionStateJson)
            .Any(file => WorkspaceWrittenFileMatchesExpectedArtifact(
                candidate.ExpectedArtifacts,
                expectedArtifact,
                file.Path,
                file.Content)))
        {
            return true;
        }

        return detail.ToolReceipts
            .Where(IsSuccessfulWorkspaceFileMutationReceipt)
            .SelectMany(ResolveManagedWorkspacePathsFromReceipt)
            .Any(path => WorkspaceWrittenFileMatchesExpectedArtifact(
                candidate.ExpectedArtifacts,
                expectedArtifact,
                path,
                content: string.Empty));
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
    {
        var expectedToolName = ResolveProviderNativeBrowserToolName(declaredRelativePath);
        if (string.IsNullOrWhiteSpace(expectedToolName))
        {
            return false;
        }

        var browserOutputsByToolName = ResolveSuccessfulBrowserToolOutputFiles(detail);
        if (!browserOutputsByToolName.TryGetValue(expectedToolName, out var outputFiles))
        {
            return false;
        }

        var matchingOutputFiles = outputFiles
            .Where(outputFile => MatchesExpectedBrowserOutputFile(declaredRelativePath, outputFile))
            .ToList();
        if (matchingOutputFiles.Count == 0)
        {
            return false;
        }

        var browserWorkingDirectory = ResolveProviderNativeBrowserWorkingDirectory(detail);
        if (string.IsNullOrWhiteSpace(browserWorkingDirectory))
        {
            return true;
        }

        return matchingOutputFiles.Any(outputFile =>
            TryResolveSafeBrowserOutputPath(browserWorkingDirectory, outputFile, out var fullPath) &&
            File.Exists(fullPath) &&
            new FileInfo(fullPath).Length > 0);
    }

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
        ProcessArtifactValidationExpectation expectedArtifact,
        string normalizedPath)
        => ProcessArtifactPathValidationRules.ExpectedArtifactExplicitlyTargetsPath(expectedArtifact, normalizedPath);

    private static bool ShouldIgnoreProductSourceForNarrativeExpectation(
        DispatchArtifactExpectation expectedArtifact,
        string normalizedPath)
        => ShouldIgnoreProductSourceForNarrativeExpectation(
            ProcessArtifactValidationSnapshotBuilder.FromDispatchExpectation(expectedArtifact),
            normalizedPath);

    private static bool ShouldIgnoreProductSourceForNarrativeExpectation(
        ProcessArtifactValidationExpectation expectedArtifact,
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

    private static bool IsNarrativeEvidenceArtifactExpectation(ProcessArtifactValidationExpectation expectedArtifact)
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

    private static IReadOnlyList<string> ResolveManagedWorkspacePathsFromReceipt(ProcessAutomationToolExecutionReceipt receipt)
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

    private static bool IsUsableProjectedResponseArtifactContent(
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

    private static string ResolveProjectableResponseArtifactText(string? responseText)
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
        ProcessArtifactValidationExpectation expectedArtifact,
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
        ProcessArtifactValidationExpectation expectedArtifact,
        string normalizedResponse)
        => ProcessArtifactTextMatchRules.HasExpectedArtifactValidationSignals(expectedArtifact, normalizedResponse);

    private static bool HasExpectedArtifactValidationSignals(
        DispatchArtifactExpectation expectedArtifact,
        IReadOnlySet<string> responseTokens)
        => HasExpectedArtifactValidationSignals(
            ProcessArtifactValidationSnapshotBuilder.FromDispatchExpectation(expectedArtifact),
            responseTokens);

    private static bool HasExpectedArtifactValidationSignals(
        ProcessArtifactValidationExpectation expectedArtifact,
        IReadOnlySet<string> responseTokens)
        => ProcessArtifactTextMatchRules.HasExpectedArtifactValidationSignals(expectedArtifact, responseTokens);

    private static IReadOnlyList<string> TokenizeArtifactContentSignalText(string value)
        => ProcessArtifactTextMatchRules.TokenizeArtifactContentSignalText(value);

    private static bool IsConversationalNonArtifactResponse(string normalizedResponse)
    {
        if (string.IsNullOrWhiteSpace(normalizedResponse))
        {
            return true;
        }

        var value = normalizedResponse.ToLowerInvariant();
        return value.Contains("ready to help", StringComparison.Ordinal) ||
               value.Contains("please let me know", StringComparison.Ordinal) ||
               value.Contains("let me know what", StringComparison.Ordinal) ||
               value.Contains("what specific", StringComparison.Ordinal) ||
               value.Contains("specific area or step", StringComparison.Ordinal) ||
               value.Contains("how can i help", StringComparison.Ordinal) ||
               value.Contains("i can help with", StringComparison.Ordinal) ||
               value.Contains("provide more details", StringComparison.Ordinal) ||
               value.Contains("please provide", StringComparison.Ordinal) ||
               value.Contains("need more information", StringComparison.Ordinal) ||
               value.Contains("not enough information", StringComparison.Ordinal) ||
               value.Contains("cannot proceed without", StringComparison.Ordinal) ||
               value.Contains("unable to proceed without", StringComparison.Ordinal);
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
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(receipt);

        if (ShouldIgnoreRecoveredImplementationScaffoldFailure(detail, receipt))
        {
            return true;
        }

        if (ShouldIgnoreProviderNativeBrowserOutputFileProbeFailure(detail, receipt))
        {
            return true;
        }

        if (!receipt.ExitSummary.StartsWith("Denied", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var normalizedToolName = NormalizeToolToken(receipt.ToolName);
        if (string.IsNullOrWhiteSpace(normalizedToolName) ||
            !IsPlaceholderCriticalToolRequestSummary(normalizedToolName, receipt.RequestSummary))
        {
            return false;
        }

        return detail.ToolReceipts.Any(item =>
            !ReferenceEquals(item, receipt) &&
            string.Equals(NormalizeToolToken(item.ToolName), normalizedToolName, StringComparison.Ordinal) &&
            !IsFailedToolReceipt(item) &&
            !IsPlaceholderCriticalToolRequestSummary(normalizedToolName, item.RequestSummary));
    }

    private static bool ShouldIgnoreRecoveredImplementationScaffoldFailure(
        ProcessAutomationExecutionRunDetail detail,
        ProcessAutomationToolExecutionReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(receipt);

        if ((!receipt.ExitSummary.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) &&
             !receipt.ExitSummary.StartsWith("Denied", StringComparison.OrdinalIgnoreCase)) ||
            !IsImplementationBootstrapToolName(NormalizeToolToken(receipt.ToolName)))
        {
            return false;
        }

        if (detail.Run.State != ProcessAutomationExecutionState.Completed ||
            detail.Run.Outcome != ProcessAutomationRunOutcome.Succeeded)
        {
            return false;
        }

        if (!HasCompletedDeclaredStepOutcome(detail))
        {
            return false;
        }

        var successfulReceipts = detail.ToolReceipts
            .Where(item =>
            {
                if (ReferenceEquals(item, receipt) || IsFailedToolReceipt(item))
                {
                    return false;
                }

                var normalizedToolName = NormalizeToolToken(item.ToolName);
                return !IsPlaceholderCriticalToolRequestSummary(normalizedToolName, item.RequestSummary);
            })
            .ToList();
        var hasProductCreationOrMutation = successfulReceipts.Any(item =>
            IsConcreteProductMutationToolName(NormalizeToolToken(item.ToolName)));
        var hasValidationOrProof = successfulReceipts.Any(item =>
        {
            var normalizedToolName = NormalizeToolToken(item.ToolName);
            return ImplementationProofToolNames.Contains(normalizedToolName) ||
                   IsImplementationValidationToolName(normalizedToolName);
        });

        return hasProductCreationOrMutation && hasValidationOrProof;
    }

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
    {
        var normalizedToolName = NormalizeToolToken(receipt.ToolName);
        if (normalizedToolName is not ("workspace_read_file" or "workspace_stat_path") ||
            !receipt.ExitSummary.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) ||
            !receipt.ExitSummary.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var browserWorkingDirectory = ResolveProviderNativeBrowserWorkingDirectory(detail);
        if (string.IsNullOrWhiteSpace(browserWorkingDirectory) ||
            !TryResolveRequestedManagedPath(receipt.RequestSummary, out var requestedPath))
        {
            return false;
        }

        var browserOutputsByToolName = ResolveSuccessfulBrowserToolOutputFiles(detail);
        foreach (var outputFileName in browserOutputsByToolName.Values.SelectMany(item => item))
        {
            if (MatchesExpectedBrowserOutputFile(requestedPath, outputFileName) &&
                TryResolveSafeBrowserOutputPath(browserWorkingDirectory, outputFileName, out var fullPath) &&
                File.Exists(fullPath))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveRequestedManagedPath(string requestSummary, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(requestSummary))
        {
            return false;
        }

        var match = ManagedWorkspacePathRegex.Match(requestSummary);
        if (!match.Success)
        {
            return false;
        }

        path = WorkspaceScopeDescriptor.NormalizeRelativePath(match.Groups["path"].Value);
        return !string.IsNullOrWhiteSpace(path);
    }

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

    private static string? ResolveProviderNativeBrowserWorkingDirectory(ProcessAutomationExecutionRunDetail detail)
    {
        return detail.ToolReceipts
            .Where(receipt =>
                string.Equals(NormalizeToolToken(receipt.ToolName), "local_mcp_launch", StringComparison.Ordinal) &&
                receipt.RequestSummary.Contains("@playwright/mcp", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(receipt.WorkingDirectory) &&
                !IsFailedToolReceipt(receipt))
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .Select(receipt => receipt.WorkingDirectory.Trim())
            .FirstOrDefault();
    }

    private static string ResolveProviderNativeBrowserToolName(string expectedRelativePath)
        => ProcessArtifactProviderNativeVisualValidationRules.ResolveProviderNativeBrowserToolName(expectedRelativePath);

    private static bool IsProviderNativeBrowserEvidenceReferencePath(string relativePath)
        => ProcessArtifactProviderNativeVisualValidationRules.IsProviderNativeBrowserEvidenceReferencePath(relativePath);

    private static bool IsManagedBrowserEvidenceReferencePath(string comparablePath)
        => ProcessArtifactProviderNativeVisualValidationRules.IsManagedBrowserEvidenceReferencePath(comparablePath);

    private static bool MatchesExpectedBrowserOutputFile(string expectedRelativePath, string outputFileName)
        => ProcessArtifactProviderNativeVisualValidationRules.MatchesExpectedBrowserOutputFile(expectedRelativePath, outputFileName);

    private static string GuessContentTypeFromPath(string fullPath)
    {
        return Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".svg" => "image/svg+xml",
            ".yml" or ".yaml" => "text/yaml",
            ".log" or ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }

    private static string BuildArtifactTitle(ProcessAutomationExecutionArtifact artifact)
        => ProcessArtifactProjectionPlanner.BuildArtifactTitle(artifact);

    private static string BuildExternalReferenceKey(ProcessAutomationExecutionArtifact artifact)
        => ProcessArtifactProjectionPlanner.BuildExecutionArtifactExternalReferenceKey(artifact.Id);

    private static string BuildCompletedDecisionArtifactExternalReferenceKey(Guid stepRunId, Guid artifactExpectationId)
    {
        return $"process-step-decision:{stepRunId:D}:{artifactExpectationId:D}";
    }

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
    {
        var statusSummary = bindingStatus?.ToString() ?? "MissingDirectorySummary";
        var technicalAgentSummary = technicalAgentId.HasValue
            ? technicalAgentId.Value.ToString("D")
            : "none";
        return $"Process automation dispatch cannot run step '{stepTitle}' ({stepRunId:D}) for process run {processRunId:D} because executor party {currentExecutorPartyId:D} is not bound to an active technical agent. Binding status: {statusSummary}; technical agent ID: {technicalAgentSummary}.";
    }

    private static string BuildStorageRelativePath(
        DispatchCandidate candidate,
        ProcessAutomationExecutionArtifact artifact)
    {
        var normalizedRelativePath = WorkspaceScopeDescriptor.NormalizeRelativePath(artifact.RelativePath);
        if (!string.IsNullOrWhiteSpace(normalizedRelativePath))
        {
            return normalizedRelativePath;
        }

        return $"process-runs/{candidate.Run.Id:D}/{candidate.StepRun.Id:D}/{Path.GetFileName(artifact.RelativePath)}";
    }

    private static ProcessArtifactKind ResolveProcessArtifactKind(
        DispatchCandidate candidate,
        ProcessAutomationExecutionArtifact artifact)
    {
        var matchedExpectation = ResolveArtifactExpectation(candidate, artifact);
        if (matchedExpectation is not null)
        {
            return matchedExpectation.ArtifactKind;
        }

        if (artifact.RelativePath.EndsWith("/response.md", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactKind.Transcript;
        }

        var relativePath = artifact.RelativePath.Replace('\\', '/');
        var fileName = Path.GetFileName(relativePath);
        var extension = Path.GetExtension(fileName);

        if (artifact.ContentType.Contains("image", StringComparison.OrdinalIgnoreCase) ||
            IsImageExtension(extension))
        {
            return ProcessArtifactKind.Evidence;
        }

        if (ContainsArtifactHint(fileName, "checklist"))
        {
            return ProcessArtifactKind.Checklist;
        }

        if (ContainsArtifactHint(fileName, "decision"))
        {
            return ProcessArtifactKind.Decision;
        }

        if (ContainsArtifactHint(fileName, "brief"))
        {
            return ProcessArtifactKind.Brief;
        }

        if (ContainsArtifactHint(fileName, "prompt"))
        {
            return ProcessArtifactKind.Prompt;
        }

        if (ContainsArtifactHint(fileName, "dataset"))
        {
            return ProcessArtifactKind.Dataset;
        }

        if (ContainsArtifactHint(fileName, "log") ||
            ContainsArtifactHint(fileName, "transcript") ||
            ContainsArtifactHint(fileName, "stdout") ||
            ContainsArtifactHint(fileName, "stderr") ||
            extension.Equals(".log", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactKind.Transcript;
        }

        return string.Equals(artifact.ArtifactKind, "generated-output", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               IsCodeOrProjectExtension(extension)
            ? ProcessArtifactKind.Deliverable
            : ProcessArtifactKind.Evidence;
    }

    private static StorageContentKind ResolveStorageContentKind(string contentType, string fullPath)
    {
        if (contentType.Contains("markdown", StringComparison.OrdinalIgnoreCase))
        {
            return StorageContentKind.Markdown;
        }

        if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return StorageContentKind.Json;
        }

        if (contentType.Contains("image", StringComparison.OrdinalIgnoreCase))
        {
            return StorageContentKind.Image;
        }

        if (contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            return StorageContentKind.Pdf;
        }

        return Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".md" => StorageContentKind.Markdown,
            ".json" => StorageContentKind.Json,
            ".svg" => StorageContentKind.Image,
            ".png" => StorageContentKind.Image,
            ".jpg" or ".jpeg" => StorageContentKind.Image,
            ".pdf" => StorageContentKind.Pdf,
            ".txt" or ".log" => StorageContentKind.Log,
            _ => StorageContentKind.Unknown
        };
    }

    private static string NormalizeTrigger(string trigger, Guid? stepRunId)
    {
        if (!string.IsNullOrWhiteSpace(trigger))
        {
            return trigger.Trim();
        }

        return stepRunId.HasValue
            ? $"step:{stepRunId.Value:D}"
            : "process-runtime";
    }

    private static bool IsWithinWorkspace(string workspaceRoot, string fullPath)
    {
        var normalizedWorkspaceRoot = Path.GetFullPath(workspaceRoot);
        var normalizedFullPath = Path.GetFullPath(fullPath);
        return string.Equals(normalizedFullPath, normalizedWorkspaceRoot, StringComparison.OrdinalIgnoreCase) ||
               normalizedFullPath.StartsWith(EnsureTrailingDirectorySeparator(normalizedWorkspaceRoot), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveArtifactFullPath(
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
        if (configuredInputs.Count == 0)
        {
            return [];
        }

        var resolvedInputs = new List<DispatchArtifactInput>(configuredInputs.Count);
        foreach (var configuredInput in configuredInputs)
        {
            if (!artifactExpectationsById.TryGetValue(configuredInput.ArtifactExpectationId, out var artifactExpectation))
            {
                continue;
            }

            sourceStepsById.TryGetValue(artifactExpectation.StepDefinitionId, out var sourceStepDefinition);
            stepRunsByDefinitionId.TryGetValue(artifactExpectation.StepDefinitionId, out var sourceStepRuns);
            var sourceStepRun = sourceStepRuns?
                .OrderByDescending(item => item.Sequence)
                .FirstOrDefault();
            var sourceStepRunIds = sourceStepRuns?
                .Select(item => item.Id)
                .ToHashSet()
                ?? [];
            var sourceProcessRunIds = sourceStepRuns?
                .Select(item => item.ProcessRunId)
                .ToHashSet()
                ?? [];
            var matchingArtifacts = existingArtifacts
                .Where(item =>
                    IsCurrentRunUpstreamArtifactInput(item, sourceStepRunIds, sourceProcessRunIds) &&
                    SatisfiesExpectedArtifactInput(item, artifactExpectation))
                .OrderByDescending(item => item.CreatedAtUtc)
                .Take(3)
                .Select(item => new DispatchArtifactReference(
                    item.Title,
                    item.ArtifactKind.ToString(),
                    item.ManagedStoragePath,
                    item.ReviewSummary,
                    item.ProvenanceSummary))
                .ToList();

            resolvedInputs.Add(new DispatchArtifactInput(
                sourceStepDefinition?.Title ?? "Unknown upstream step",
                artifactExpectation.Title,
                artifactExpectation.Id,
                artifactExpectation.StepDefinitionId,
                sourceStepRun?.Id,
                sourceStepRun?.ConcurrencyToken,
                sourceStepRun?.Status,
                sourceStepRun?.CurrentExecutorPartyId.HasValue == true,
                matchingArtifacts));
        }

        return resolvedInputs;
    }

    internal static bool IsCurrentRunUpstreamArtifactInput(
        ProcessArtifactRecord artifact,
        IReadOnlySet<Guid> sourceStepRunIds,
        IReadOnlySet<Guid> sourceProcessRunIds)
    {
        if (!artifact.StepRunId.HasValue ||
            !sourceStepRunIds.Contains(artifact.StepRunId.Value) ||
            !sourceProcessRunIds.Contains(artifact.ProcessRunId))
        {
            return false;
        }

        return ProcessArtifactLineageValidator
            .ValidateManagedStorageBoundary(artifact, artifact.ProcessRunId)
            .IsCurrentRun;
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

        var preparedInputs = new List<DispatchArtifactInput>(artifactInputs.Count);
        foreach (var artifactInput in artifactInputs)
        {
            var preparedArtifacts = new List<DispatchArtifactReference>(artifactInput.Artifacts.Count);
            foreach (var artifact in artifactInput.Artifacts)
            {
                var preparedPath = PrepareManagedArtifactPathForPrompt(
                    artifact.ManagedStoragePath,
                    workspaceRoot,
                    workspaceScope);
                preparedArtifacts.Add(string.Equals(preparedPath, artifact.ManagedStoragePath, StringComparison.OrdinalIgnoreCase)
                    ? artifact
                    : artifact with
                    {
                        ManagedStoragePath = preparedPath
                    });
            }

            preparedInputs.Add(new DispatchArtifactInput(
                artifactInput.SourceStepTitle,
                artifactInput.ExpectedArtifactTitle,
                artifactInput.ArtifactExpectationId,
                artifactInput.SourceStepDefinitionId,
                artifactInput.SourceStepRunId,
                artifactInput.SourceStepRunConcurrencyToken,
                artifactInput.SourceStepRunStatus,
                artifactInput.SourceStepHasAgentExecutor,
                preparedArtifacts));
        }

        return preparedInputs;
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

    private static bool SatisfiesExpectedArtifactInput(
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

    private static Guid? ResolveArtifactExpectationId(
        DispatchCandidate candidate,
        ProcessAutomationExecutionArtifact artifact)
    {
        return ResolveArtifactExpectation(candidate, artifact)?.Id;
    }

    private static Guid? ResolveArtifactExpectationId(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        ProcessAutomationExecutionArtifact artifact)
    {
        return ResolveArtifactExpectation(candidate, detail.Run.InputSummary, artifact)?.Id;
    }

    private static DispatchArtifactExpectation? ResolveArtifactExpectation(
        DispatchCandidate candidate,
        ProcessAutomationExecutionArtifact artifact)
    {
        return ResolveArtifactExpectation(candidate, artifact, artifactTextContent: null);
    }

    private static DispatchArtifactExpectation? ResolveArtifactExpectation(
        DispatchCandidate candidate,
        ProcessAutomationExecutionArtifact artifact,
        string? artifactTextContent)
    {
        return ResolveArtifactExpectation(candidate, null, artifact, artifactTextContent);
    }

    private static DispatchArtifactExpectation? ResolveArtifactExpectation(
        DispatchCandidate candidate,
        string? projectStructureContractText,
        ProcessAutomationExecutionArtifact artifact)
    {
        return ResolveArtifactExpectation(candidate, projectStructureContractText, artifact, artifactTextContent: null);
    }

    private static DispatchArtifactExpectation? ResolveArtifactExpectation(
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
        IReadOnlyList<ProcessArtifactValidationExpectation> expectedArtifacts,
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
        var projectionExpectations = expectedArtifacts
            .Select(item => item.ToProjectionExpectation())
            .ToList();
        var expectedArtifactsById = expectedArtifacts.ToDictionary(item => item.Id);
        var strongMatchedExpectationId = ProcessArtifactExpectationMatcher.MatchStrongExpectedArtifactId(
            projectionExpectations,
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
        IReadOnlyList<ProcessArtifactValidationExpectation> expectedArtifacts,
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
        ProcessArtifactValidationExpectation expectedArtifact,
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
        ProcessArtifactValidationExpectation expectedArtifact,
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
        IReadOnlyList<ProcessArtifactValidationExpectation> expectedArtifacts,
        ProcessArtifactValidationExpectation expectedArtifact,
        ProcessAutomationExecutionArtifact artifact,
        string relativePath,
        string displayName,
        string? artifactTextContent)
    {
        if (!IsNarrativeEvidenceArtifactExpectation(expectedArtifact) ||
            IsProviderNativeBrowserOutputArtifact(artifact) ||
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

    private static IReadOnlyList<ProcessMockArtifactProjection> ResolveProcessMockArtifactProjections(
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
            _ => (string.Empty, string.Empty)
        };

        return !string.IsNullOrWhiteSpace(fileName);
    }

    private static bool ProcessMockArtifactMatchesExpectation(
        DispatchArtifactExpectation expectedArtifact,
        ProcessMockArtifactProjection projection)
    {
        var observedTokens = TokenizeArtifactContentSignalText($"{projection.RelativePath} {projection.ContentSignalText}")
            .ToHashSet(StringComparer.Ordinal);
        var titleTokens = TokenizeArtifactContentSignalText(expectedArtifact.Title)
            .ToList();
        if (observedTokens.Count == 0 || titleTokens.Count == 0)
        {
            return false;
        }

        return titleTokens.All(observedTokens.Contains);
    }

    private static bool CanSatisfyConcreteImplementationProofWithProcessMock(
        DispatchCandidate candidate,
        ProcessMockArtifactProjection projection)
    {
        return RequiresConcreteImplementationProof(candidate) &&
               IsProcessMockImplementationRole(projection.RoleKey) &&
               ProcessMockProjectionMatchesRequiredArtifact(candidate, projection);
    }

    private static bool IsProcessMockImplementationRole(string roleKey)
    {
        var normalizedRoleKey = roleKey.Trim().ToLowerInvariant();
        return normalizedRoleKey is ProcessMockDeveloperRoleKey or ProcessMockRepairDeveloperRoleKey;
    }

    private static bool ProcessMockProjectionMatchesRequiredArtifact(
        DispatchCandidate candidate,
        ProcessMockArtifactProjection projection)
    {
        return candidate.ExpectedArtifacts
            .Where(item => item.IsRequired)
            .Any(item => ProcessMockArtifactMatchesExpectation(item, projection));
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

    private static bool TryExtractExpectedArtifactRelativePath(string validationRequirementSummary, out string relativePath)
        => ProcessArtifactPathValidationRules.TryExtractExpectedArtifactRelativePath(validationRequirementSummary, out relativePath);

    internal static IReadOnlyList<ProjectStructureRequiredArtifactPath> ResolveProjectStructureRequiredArtifactPaths(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var artifacts = new List<ProjectStructureRequiredArtifactPath>();
        foreach (Match match in Regex.Matches(
                     text,
                     @"Required file\s+`(?<file>[^`]+\.md)`\s+must be written at\s+`(?<path>[^`]+)`",
                     RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            AddProjectStructureRequiredArtifactPath(
                artifacts,
                match.Groups["file"].Value,
                match.Groups["path"].Value);
        }

        foreach (Match match in Regex.Matches(
                     text,
                     @"Governed path:\s*(?<path>external-target/[^\r\n\s`]+)",
                     RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            var path = WorkspaceScopeDescriptor.NormalizeRelativePath(match.Groups["path"].Value);
            AddProjectStructureRequiredArtifactPath(
                artifacts,
                Path.GetFileName(path),
                path);
        }

        return artifacts
            .GroupBy(item => item.AliasPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryResolveProjectStructureExpectedArtifactPath(
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

    private static bool TryResolveProjectStructureExpectedArtifactPath(
        DispatchArtifactExpectation expectedArtifact,
        IReadOnlyList<ProjectStructureRequiredArtifactPath> requiredArtifactPaths,
        out string governedPath)
    {
        governedPath = string.Empty;
        if (requiredArtifactPaths.Count == 0)
        {
            return false;
        }

        var bestMatch = requiredArtifactPaths
            .Select(path => new
            {
                Path = path,
                Score = ScoreProjectStructureArtifactPathMatch(expectedArtifact, path.FileName)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Path.FileName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (bestMatch is null)
        {
            return false;
        }

        governedPath = bestMatch.Path.AliasPath;
        return !string.IsNullOrWhiteSpace(governedPath);
    }

    internal static int ScoreProjectStructureArtifactPathMatch(
        DispatchArtifactExpectation expectedArtifact,
        string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return 0;
        }

        var expectedTokens = TokenizeProjectStructureArtifactName(expectedArtifact.Title);
        var fileTokens = TokenizeProjectStructureArtifactName(Path.GetFileNameWithoutExtension(fileName));
        if (expectedTokens.Count == 0 || fileTokens.Count == 0)
        {
            return 0;
        }

        var matchedTokenCount = expectedTokens.Count(fileTokens.Contains);
        if (matchedTokenCount >= Math.Min(2, expectedTokens.Count))
        {
            return matchedTokenCount * 10 + (expectedTokens.Count == matchedTokenCount ? 5 : 0);
        }

        var expectedSlug = FileSafeSlugBuilder.Build(string.Join('-', expectedTokens));
        var fileSlug = FileSafeSlugBuilder.Build(string.Join('-', fileTokens));
        return !string.IsNullOrWhiteSpace(expectedSlug) &&
               !string.IsNullOrWhiteSpace(fileSlug) &&
               (fileSlug.Contains(expectedSlug, StringComparison.Ordinal) ||
                expectedSlug.Contains(fileSlug, StringComparison.Ordinal))
            ? 1
            : 0;
    }

    private static IReadOnlyList<string> TokenizeProjectStructureArtifactName(string value)
    {
        return TokenizeArtifactComparisonText(value)
            .Where(token => !ProcessArtifactTextMatchRules.IsArtifactTitleNoiseToken(token))
            .Where(token => !ProcessArtifactTextMatchRules.IsArtifactContentNoiseToken(token))
            .Where(token => !token.All(char.IsDigit))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static void AddProjectStructureRequiredArtifactPath(
        ICollection<ProjectStructureRequiredArtifactPath> artifacts,
        string fileName,
        string aliasPath)
    {
        var normalizedFileName = fileName.Trim();
        var normalizedPath = NormalizeProjectStructureArtifactPathForComparison(aliasPath);
        if (string.IsNullOrWhiteSpace(normalizedFileName) ||
            string.IsNullOrWhiteSpace(normalizedPath) ||
            artifacts.Any(item => string.Equals(item.AliasPath, normalizedPath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        artifacts.Add(new ProjectStructureRequiredArtifactPath(normalizedFileName, normalizedPath));
    }

    private static bool ArtifactPathMatchesGovernedProjectStructurePath(
        string observedPath,
        string governedPath)
    {
        return string.Equals(
            NormalizeProjectStructureArtifactPathForComparison(observedPath),
            NormalizeProjectStructureArtifactPathForComparison(governedPath),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeProjectStructureArtifactPathForComparison(string path)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (TryMapAbsoluteExternalPathToAlias(normalized, out var mappedAlias))
        {
            normalized = mappedAlias;
        }

        return NormalizeManagedRelativePathForComparison(normalized);
    }

    private static GovernedInspectionPaths ResolveGovernedInspectionPaths(IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts)
    {
        var statPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var readPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var expectedArtifact in expectedArtifacts)
        {
            if (!TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var relativePath))
            {
                continue;
            }

            var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                continue;
            }

            statPaths.Add(normalizedPath);
            if (IsTextReadableManagedArtifactPath(normalizedPath))
            {
                readPaths.Add(normalizedPath);
            }
        }

        return new GovernedInspectionPaths(statPaths.ToList(), readPaths.ToList());
    }

    private static GovernedInspectionPaths ResolveArtifactInputInspectionPaths(IReadOnlyList<DispatchArtifactInput> artifactInputs)
    {
        var statPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var readPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sourceStepGroup in artifactInputs.GroupBy(input => input.SourceStepTitle, StringComparer.OrdinalIgnoreCase))
        {
            var sourceStepStatPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var sourceStepReadPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var sourceStepVisualAttachmentPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var artifactInput in sourceStepGroup)
            {
                foreach (var artifact in artifactInput.Artifacts)
                {
                    var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(artifact.ManagedStoragePath);
                    if (string.IsNullOrWhiteSpace(normalizedPath))
                    {
                        continue;
                    }

                    if (IsTextReadableManagedArtifactPath(normalizedPath))
                    {
                        sourceStepStatPaths.Add(normalizedPath);
                        sourceStepReadPaths.Add(normalizedPath);
                        continue;
                    }

                    if (IsVisualEvidenceAttachmentPath(normalizedPath))
                    {
                        sourceStepVisualAttachmentPaths.Add(normalizedPath);
                        continue;
                    }

                    sourceStepStatPaths.Add(normalizedPath);
                }
            }

            foreach (var path in sourceStepStatPaths)
            {
                statPaths.Add(path);
            }

            foreach (var path in sourceStepReadPaths)
            {
                readPaths.Add(path);
            }

            if (sourceStepReadPaths.Count == 0)
            {
                foreach (var path in sourceStepVisualAttachmentPaths)
                {
                    statPaths.Add(path);
                }
            }
        }

        return new GovernedInspectionPaths(statPaths.ToList(), readPaths.ToList());
    }

    private static string ResolveMissingUpstreamArtifactInspectionSummary(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
    {
        var missingInspectionPaths = ResolveMissingUpstreamArtifactInspectionPaths(candidate, detail);
        if (missingInspectionPaths.StatPaths.Count == 0 && missingInspectionPaths.ReadPaths.Count == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        if (missingInspectionPaths.StatPaths.Count > 0)
        {
            parts.Add($"workspace_stat_path missing for {FormatPromptPathList(missingInspectionPaths.StatPaths)}");
        }

        if (missingInspectionPaths.ReadPaths.Count > 0)
        {
            parts.Add($"workspace_read_file missing for {FormatPromptPathList(missingInspectionPaths.ReadPaths)}");
        }

        return "the review step did not directly inspect inherited upstream artifacts: " + string.Join("; ", parts);
    }

    private static GovernedInspectionPaths ResolveMissingUpstreamArtifactInspectionPaths(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
    {
        if (!RequiresGovernedInspection(candidate.StepRun) || candidate.ArtifactInputs.Count == 0)
        {
            return new GovernedInspectionPaths([], []);
        }

        var requiredInspectionPaths = ResolveArtifactInputInspectionPaths(candidate.ArtifactInputs);
        if (requiredInspectionPaths.StatPaths.Count == 0 && requiredInspectionPaths.ReadPaths.Count == 0)
        {
            return new GovernedInspectionPaths([], []);
        }

        var successfulStatPaths = ResolveSuccessfulWorkspaceInspectionPaths(
            detail,
            "workspace_stat_path",
            ResolveSuccessfulSessionPathStats(detail.Run.SerializedSessionStateJson));
        var successfulReadPaths = ResolveSuccessfulWorkspaceInspectionPaths(
            detail,
            "workspace_read_file",
            ResolveSuccessfulSessionFileReads(detail.Run.SerializedSessionStateJson));

        var missingStatPaths = requiredInspectionPaths.StatPaths
            .Where(path => !ContainsEquivalentManagedPath(successfulStatPaths, path) &&
                           !ContainsEquivalentManagedPath(successfulReadPaths, path))
            .Take(3)
            .ToList();
        var missingReadPaths = requiredInspectionPaths.ReadPaths
            .Where(path => !ContainsEquivalentManagedPath(successfulReadPaths, path))
            .Take(3)
            .ToList();

        return new GovernedInspectionPaths(missingStatPaths, missingReadPaths);
    }

    private static bool ContainsEquivalentManagedPath(IReadOnlySet<string> paths, string requiredPath)
    {
        if (paths.Contains(requiredPath))
        {
            return true;
        }

        var normalizedRequiredPath = NormalizeManagedRelativePathForComparison(requiredPath);
        return !string.IsNullOrWhiteSpace(normalizedRequiredPath) &&
               paths.Any(path => string.Equals(
                   NormalizeManagedRelativePathForComparison(path),
                   normalizedRequiredPath,
                   StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlySet<string> ResolveSuccessfulWorkspaceInspectionPaths(
        ProcessAutomationExecutionRunDetail detail,
        string normalizedToolName,
        IReadOnlyList<SessionFileContent> sessionPaths)
    {
        var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sessionPath in sessionPaths)
        {
            AddNormalizedWorkspaceInspectionPath(paths, sessionPath.Path);
        }

        foreach (var receipt in detail.ToolReceipts.Where(receipt =>
                     !IsFailedToolReceipt(receipt) &&
                     string.Equals(NormalizeToolToken(receipt.ToolName), normalizedToolName, StringComparison.Ordinal)))
        {
            foreach (var path in ResolveManagedWorkspacePathsFromReceipt(receipt))
            {
                AddNormalizedWorkspaceInspectionPath(paths, path);
            }
        }

        return paths;
    }

    private static void AddNormalizedWorkspaceInspectionPath(ISet<string> paths, string path)
    {
        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(path);
        if (!string.IsNullOrWhiteSpace(normalizedPath))
        {
            paths.Add(normalizedPath);
        }
    }

    private static string FormatPromptPathList(IReadOnlyList<string> relativePaths)
    {
        return string.Join(", ", relativePaths.Select(relativePath => $"`{relativePath}`"));
    }

    private static string NormalizeManagedRelativePathForComparison(string relativePath)
        => ProcessArtifactPathValidationRules.NormalizeManagedRelativePathForComparison(relativePath);

    private static bool IsVisualEvidenceAttachmentPath(string relativePath)
    {
        var extension = Path.GetExtension(relativePath);
        return IsImageExtension(extension);
    }

    private static bool IsResponseProjectableTextArtifact(string relativePath)
    {
        var extension = Path.GetExtension(relativePath);
        return string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveResponseTextArtifactRelativePath(
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
        return expectedArtifact.ArtifactKind is ProcessArtifactKind.Brief
            or ProcessArtifactKind.Checklist
            or ProcessArtifactKind.Prompt
            or ProcessArtifactKind.Transcript ||
               IsPathlessResponseProjectableDeliverable(expectedArtifact) ||
               IsPathlessResponseProjectableEvidence(expectedArtifact);
    }

    private static bool IsPathlessResponseProjectableDeliverable(DispatchArtifactExpectation expectedArtifact)
    {
        if (expectedArtifact.ArtifactKind != ProcessArtifactKind.Deliverable)
        {
            return false;
        }

        var normalizedTitle = CollapsePromptWhitespace(expectedArtifact.Title).ToLowerInvariant();
        var normalizedValidation = CollapsePromptWhitespace(expectedArtifact.ValidationRequirementSummary).ToLowerInvariant();
        return normalizedTitle.Contains("change set", StringComparison.Ordinal) ||
               normalizedValidation.Contains("change set", StringComparison.Ordinal);
    }

    private static bool IsPathlessResponseProjectableEvidence(DispatchArtifactExpectation expectedArtifact)
    {
        if (expectedArtifact.ArtifactKind != ProcessArtifactKind.Evidence)
        {
            return false;
        }

        var normalizedTitle = CollapsePromptWhitespace(expectedArtifact.Title).ToLowerInvariant();
        var normalizedValidation = CollapsePromptWhitespace(expectedArtifact.ValidationRequirementSummary).ToLowerInvariant();
        return normalizedTitle.Contains("note", StringComparison.Ordinal) ||
               normalizedTitle.Contains("review", StringComparison.Ordinal) ||
               normalizedTitle.Contains("evidence index", StringComparison.Ordinal) ||
               normalizedTitle.Contains("result index", StringComparison.Ordinal) ||
               normalizedTitle.Contains("receipt", StringComparison.Ordinal) ||
               normalizedTitle.Contains("handoff", StringComparison.Ordinal) ||
               normalizedTitle.Contains("browser navigation", StringComparison.Ordinal) ||
               normalizedTitle.Contains("console evidence", StringComparison.Ordinal) ||
               normalizedTitle.Contains("evidence pack", StringComparison.Ordinal) ||
               normalizedTitle.Contains("snapshot", StringComparison.Ordinal) ||
               normalizedTitle.Contains("decision record", StringComparison.Ordinal) ||
               normalizedTitle.Contains("handoff packet", StringComparison.Ordinal) ||
               normalizedTitle.Contains("regression", StringComparison.Ordinal) ||
               normalizedValidation.Contains("evidence index", StringComparison.Ordinal) ||
               normalizedValidation.Contains("raw record pointers", StringComparison.Ordinal) ||
               normalizedValidation.Contains("validation evidence", StringComparison.Ordinal) ||
               normalizedValidation.Contains("runtime/api/browser evidence", StringComparison.Ordinal) ||
               normalizedValidation.Contains("accepted issues", StringComparison.Ordinal) ||
               normalizedValidation.Contains("rejected concerns", StringComparison.Ordinal) ||
               normalizedValidation.Contains("residual risk", StringComparison.Ordinal);
    }

    private static string BuildFallbackResponseTextArtifactRelativePath(
        DispatchCandidate candidate,
        DispatchArtifactExpectation expectedArtifact)
    {
        var expectedSlug = FileSafeSlugBuilder.Build(expectedArtifact.Title);
        if (string.IsNullOrWhiteSpace(expectedSlug))
        {
            expectedSlug = "artifact";
        }

        return WorkspaceScopeDescriptor.NormalizeRelativePath(
            Path.Combine(
                BuildCurrentRunManagedArtifactRoot(candidate),
                $"{candidate.StepRun.Sequence + 1:00}-{expectedSlug}.md"));
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
        var extension = Path.GetExtension(relativePath);
        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".log", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
               IsCodeOrProjectExtension(extension);
    }

    private static bool CanMatchArtifactByTextContent(ProcessAutomationExecutionArtifact artifact)
    {
        if (string.IsNullOrWhiteSpace(artifact.RelativePath))
        {
            return false;
        }

        return artifact.ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
               artifact.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
              artifact.ContentType.Contains("xml", StringComparison.OrdinalIgnoreCase) ||
              artifact.ContentType.Contains("yaml", StringComparison.OrdinalIgnoreCase) ||
              IsTextReadableManagedArtifactPath(artifact.RelativePath);
    }

    private static bool IsProviderNativeBrowserOutputArtifact(ProcessAutomationExecutionArtifact artifact)
        => ProcessArtifactProviderNativeVisualValidationRules.IsProviderNativeBrowserOutputArtifact(artifact);

    private static string? TryDecodeTextArtifactContent(
        ProcessAutomationExecutionArtifact artifact,
        string fullPath,
        byte[] content)
    {
        const int maxTextArtifactBytes = 512 * 1024;

        if (!CanMatchArtifactByTextContent(artifact) ||
            content.Length == 0 ||
            content.Length > maxTextArtifactBytes ||
            IsImageExtension(Path.GetExtension(fullPath)))
        {
            return null;
        }

        try
        {
            var text = Encoding.UTF8.GetString(content);
            return text.Contains('\0', StringComparison.Ordinal)
                ? null
                : text;
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static bool ShouldProjectFinalAssistantResponse(ProcessAutomationExecutionRunRecord run)
    {
        return run.State == ProcessAutomationExecutionState.Completed &&
               run.Outcome == ProcessAutomationRunOutcome.Succeeded;
    }

    private static bool ShouldProjectResponseTextArtifacts(
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

    private static string ResolveWorkspaceWrittenArtifactRelativePath(
        WorkspaceScopeDescriptor workspaceScope,
        string path)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (IsExternalTargetAliasPath(normalized))
        {
            return normalized;
        }

        return TryMapAbsoluteExternalPathToAlias(normalized, out var mappedAlias)
            ? mappedAlias
            : ResolveScopedManagedRelativePath(workspaceScope, normalized);
    }

    private static bool TryResolveWorkspaceWrittenArtifactSourceFullPath(
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        string writtenPath,
        string projectedRelativePath,
        out string fullPath,
        out string sourceRelativePath,
        out string failureReason)
    {
        fullPath = string.Empty;
        sourceRelativePath = string.Empty;
        failureReason = string.Empty;

        var sourceCandidates = ResolveWorkspaceWrittenArtifactSourceRelativePaths(
            workspaceScope,
            writtenPath,
            projectedRelativePath);
        foreach (var candidatePath in sourceCandidates)
        {
            if (!TryResolveArtifactFullPath(workspaceRoot, candidatePath, out var candidateFullPath, out var candidateFailure))
            {
                failureReason = candidateFailure;
                continue;
            }

            if (!File.Exists(candidateFullPath))
            {
                failureReason = $"File '{candidatePath}' does not exist.";
                continue;
            }

            fullPath = candidateFullPath;
            sourceRelativePath = candidatePath;
            failureReason = string.Empty;
            return true;
        }

        return false;
    }

    internal static IReadOnlyList<string> ResolveWorkspaceWrittenArtifactSourceRelativePaths(
        WorkspaceScopeDescriptor workspaceScope,
        string writtenPath,
        string projectedRelativePath)
    {
        var paths = new List<string>();
        AddWorkspaceWrittenArtifactSourceRelativePath(paths, writtenPath);
        AddWorkspaceWrittenArtifactSourceRelativePath(paths, projectedRelativePath);

        var normalizedWrittenPath = WorkspaceScopeDescriptor.NormalizeRelativePath(writtenPath);
        if (!string.IsNullOrWhiteSpace(normalizedWrittenPath) &&
            !IsExternalTargetAliasPath(normalizedWrittenPath) &&
            !TryMapAbsoluteExternalPathToAlias(normalizedWrittenPath, out _) &&
            IsManagedWorkspaceArtifactPath(normalizedWrittenPath))
        {
            AddWorkspaceWrittenArtifactSourceRelativePath(
                paths,
                ResolveScopedManagedRelativePath(workspaceScope, normalizedWrittenPath));
        }

        return paths;
    }

    private static void AddWorkspaceWrittenArtifactSourceRelativePath(
        ICollection<string> paths,
        string path)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(path);
        if (string.IsNullOrWhiteSpace(normalized) ||
            paths.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        if (TryMapAbsoluteExternalPathToAlias(normalized, out var mappedAlias))
        {
            if (!paths.Contains(mappedAlias, StringComparer.OrdinalIgnoreCase))
            {
                paths.Add(mappedAlias);
            }

            return;
        }

        paths.Add(normalized);
    }

    private static bool IsManagedWorkspaceArtifactPath(string path)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var rootSegment = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        return rootSegment is not null && IsManagedRootSegment(rootSegment);
    }

    private static bool ShouldAutoRecordCompletedDecisionArtifact(DispatchArtifactExpectation expectedArtifact)
    {
        return expectedArtifact.IsRequired &&
               expectedArtifact.ArtifactKind is ProcessArtifactKind.Decision or ProcessArtifactKind.DecisionRecord &&
               expectedArtifact.TrustRequirement is ProcessArtifactTrustRequirement.ReviewRequired or ProcessArtifactTrustRequirement.HumanApproved or ProcessArtifactTrustRequirement.ApprovalRequired;
    }

    private static ProcessArtifactTrustStatus ResolveCompletedDecisionArtifactTrustStatus(
        ProcessArtifactTrustRequirement trustRequirement)
    {
        return trustRequirement switch
        {
            ProcessArtifactTrustRequirement.HumanApproved or ProcessArtifactTrustRequirement.ApprovalRequired => ProcessArtifactTrustStatus.Approved,
            _ => ProcessArtifactTrustStatus.ReviewRequired
        };
    }

    internal static ProcessArtifactTrustStatus ResolveProjectedArtifactTrustStatus(
        DispatchArtifactExpectation expectedArtifact,
        ProcessStepRunStatus completionStatus)
        => ProcessArtifactProjectionPlanner.ResolveProjectedArtifactTrustStatus(
            ProcessArtifactValidationSnapshotBuilder.ToProjectionExpectation(expectedArtifact),
            completionStatus);

    private static string BuildCompletedDecisionArtifactProvenanceSummary(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
    {
        var executorName = string.IsNullOrWhiteSpace(candidate.StepRun.CurrentExecutorName)
            ? "the assigned approver"
            : candidate.StepRun.CurrentExecutorName.Trim();
        return $"Recorded from the governed step outcome for AgentFramework execution run {detail.Run.Id:D} by {executorName}.";
    }

    private static string BuildCompletedDecisionArtifactReviewSummary(
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

    private static string ResolveScopedManagedRelativePath(WorkspaceScopeDescriptor workspaceScope, string relativePath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath);
        if (workspaceScope.IsDefaultSandbox || string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return TryResolveScopedManagedRelativePath(normalized, "artifacts", workspaceScope.ArtifactRootRelativePath)
            ?? TryResolveScopedManagedRelativePath(normalized, "output", workspaceScope.OutputRootRelativePath)
            ?? TryResolveScopedManagedRelativePath(normalized, "integration-map", workspaceScope.IntegrationMapRootRelativePath)
            ?? TryResolveScopedManagedRelativePath(normalized, "data", workspaceScope.DataRootRelativePath)
            ?? normalized;
    }

    private static string? TryResolveScopedManagedRelativePath(string relativePath, string rootName, string scopedRootRelativePath)
    {
        if (!IsManagedRootMatch(relativePath, rootName))
        {
            return null;
        }

        if (IsManagedRootMatch(relativePath, scopedRootRelativePath))
        {
            return relativePath;
        }

        var foreignScopedPrefix = $"{rootName}/scopes/";
        if (relativePath.StartsWith(foreignScopedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return relativePath;
        }

        var suffix = RemoveManagedRoot(relativePath, rootName);
        return string.IsNullOrWhiteSpace(suffix)
            ? scopedRootRelativePath
            : WorkspaceScopeDescriptor.NormalizeRelativePath(Path.Combine(scopedRootRelativePath, suffix));
    }

    private static bool IsManagedRootMatch(string relativePath, string rootRelativePath)
    {
        return string.Equals(relativePath, rootRelativePath, StringComparison.OrdinalIgnoreCase) ||
               relativePath.StartsWith(rootRelativePath + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string RemoveManagedRoot(string relativePath, string rootRelativePath)
    {
        if (string.Equals(relativePath, rootRelativePath, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return relativePath[(rootRelativePath.Length + 1)..];
    }

    private static bool IsManagedRootSegment(string segment)
        => ProcessArtifactPathValidationRules.IsManagedRootSegment(segment);

}
