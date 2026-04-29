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

        var normalizedResponse = CollapsePromptWhitespace(responseText).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedResponse))
        {
            return string.Empty;
        }

        if (normalizedResponse.Contains("browser proof cannot proceed", StringComparison.Ordinal) ||
            normalizedResponse.Contains("browser proof not possible", StringComparison.Ordinal) ||
            normalizedResponse.Contains("browser proof deferred", StringComparison.Ordinal))
        {
            return "the response says browser proof could not proceed";
        }

        if (normalizedResponse.Contains("manual qa: not possible", StringComparison.Ordinal) ||
            normalizedResponse.Contains("manual qa not possible", StringComparison.Ordinal))
        {
            return "the response says manual QA was not possible";
        }

        if (normalizedResponse.Contains("no screenshots", StringComparison.Ordinal) ||
            normalizedResponse.Contains("screenshots: none possible", StringComparison.Ordinal) ||
            normalizedResponse.Contains("screenshots were not possible", StringComparison.Ordinal))
        {
            return "the response says screenshots were not captured";
        }

        if (normalizedResponse.Contains("application is not running", StringComparison.Ordinal) ||
            normalizedResponse.Contains("app is not running", StringComparison.Ordinal) ||
            normalizedResponse.Contains("no running app", StringComparison.Ordinal) ||
            normalizedResponse.Contains("no runnable output", StringComparison.Ordinal))
        {
            return "the response says the app was not running";
        }

        if (normalizedResponse.Contains("cannot validate ui", StringComparison.Ordinal) ||
            normalizedResponse.Contains("ui validation can not be performed", StringComparison.Ordinal) ||
            normalizedResponse.Contains("ui validation cannot be performed", StringComparison.Ordinal))
        {
            return "the response says UI validation could not be performed";
        }

        return string.Empty;
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
            normalizedResponse.Contains("next required actions", StringComparison.Ordinal) ||
            normalizedResponse.Contains("next implementation steps", StringComparison.Ordinal) ||
            normalizedResponse.Contains("for the next agent or step", StringComparison.Ordinal) ||
            normalizedResponse.Contains("proceeding to implement", StringComparison.Ordinal);

        return defersFeatureImplementation || reportsMissingRequestedBehavior || reportsDeferredExecution
            ? "the response says the step only scaffolded the app and left the requested feature implementation for later work"
            : string.Empty;
    }

    private static string ResolveMissingRequiredArtifactSummary(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string? responseText)
    {
        if (candidate.ExpectedArtifacts.Count == 0)
        {
            return string.Empty;
        }

        var missingRequiredArtifacts = candidate.ExpectedArtifacts
            .Where(item => item.IsRequired)
            .Where(item => !HasRecordedExpectedArtifact(candidate, detail, item))
            .Where(item => !CanAutoSatisfyRequiredArtifact(candidate, detail, item, responseText))
            .Select(item => item.Title.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        return missingRequiredArtifacts.Count == 0
            ? string.Empty
            : string.Join(", ", missingRequiredArtifacts);
    }

    private static bool HasRecordedExpectedArtifact(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        DispatchArtifactExpectation expectedArtifact)
    {
        return detail.Artifacts.Any(artifact => ResolveArtifactExpectationId(candidate, artifact) == expectedArtifact.Id);
    }

    private static bool CanAutoSatisfyRequiredArtifact(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        DispatchArtifactExpectation expectedArtifact,
        string? responseText)
    {
        if (CanProjectProcessMockArtifact(candidate, detail, expectedArtifact))
        {
            return true;
        }

        if (ShouldAutoRecordCompletedDecisionArtifact(expectedArtifact))
        {
            return true;
        }

        if (TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var declaredRelativePath))
        {
            return !string.IsNullOrWhiteSpace(ResolveProviderNativeBrowserToolName(declaredRelativePath)) ||
                   (IsUsableProjectedResponseArtifactContent(expectedArtifact, responseText) &&
                    IsResponseProjectableTextArtifact(declaredRelativePath));
        }

        return IsUsableProjectedResponseArtifactContent(expectedArtifact, responseText) &&
               CanProjectResponseTextArtifactWithoutDeclaredPath(expectedArtifact);
    }

    private static bool CanProjectProcessMockArtifact(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        DispatchArtifactExpectation expectedArtifact)
    {
        return ResolveProcessMockArtifactProjections(detail.Run.SerializedSessionStateJson)
            .Any(projection => ProcessMockArtifactMatchesExpectation(expectedArtifact, projection));
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

    private static bool HasExpectedArtifactContentSignals(
        DispatchArtifactExpectation expectedArtifact,
        string responseText,
        string normalizedResponse)
    {
        if (ContainsArtifactResponseSection(responseText, expectedArtifact.Title))
        {
            return HasExpectedArtifactValidationSignals(expectedArtifact, normalizedResponse);
        }

        var responseTokens = TokenizeArtifactContentSignalText(normalizedResponse)
            .ToHashSet(StringComparer.Ordinal);
        if (responseTokens.Count == 0)
        {
            return false;
        }

        var titleTokens = TokenizeArtifactContentSignalText(expectedArtifact.Title)
            .ToList();
        if (titleTokens.Count >= 2)
        {
            var requiredTitleMatches = Math.Min(2, titleTokens.Count);
            if (titleTokens.Count(responseTokens.Contains) < requiredTitleMatches)
            {
                return false;
            }
        }

        return HasExpectedArtifactValidationSignals(expectedArtifact, responseTokens);
    }

    private static bool HasExpectedArtifactValidationSignals(
        DispatchArtifactExpectation expectedArtifact,
        string normalizedResponse)
    {
        var responseTokens = TokenizeArtifactContentSignalText(normalizedResponse)
            .ToHashSet(StringComparer.Ordinal);
        return HasExpectedArtifactValidationSignals(expectedArtifact, responseTokens);
    }

    private static bool HasExpectedArtifactValidationSignals(
        DispatchArtifactExpectation expectedArtifact,
        IReadOnlySet<string> responseTokens)
    {
        var validationTokens = TokenizeArtifactContentSignalText(expectedArtifact.ValidationRequirementSummary)
            .ToList();
        if (validationTokens.Count < 3)
        {
            return true;
        }

        return validationTokens.Count(responseTokens.Contains) >= Math.Min(2, validationTokens.Count);
    }

    private static IReadOnlyList<string> TokenizeArtifactContentSignalText(string value)
    {
        return TokenizeArtifactComparisonText(value)
            .Where(token => token.Length > 2)
            .Where(token => !ArtifactTitleNoiseTokens.Contains(token))
            .Where(token => !ArtifactContentNoiseTokens.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

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
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = CollapsePromptWhitespace(value);
        return normalized.Contains("browser proof", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("screenshot", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("screenshots", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("manual qa", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("ui validation", StringComparison.OrdinalIgnoreCase);
    }

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

    private static bool IsCriticalToolReceipt(ToolExecutionReceiptRecord receipt)
    {
        if (!string.Equals(receipt.ToolFamily, "workspace-process", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var toolName = NormalizeToolToken(receipt.ToolName);
        return !string.IsNullOrWhiteSpace(toolName) &&
               !NonCriticalWorkspaceProcessToolNames.Contains(toolName);
    }

    private static bool IsFailedToolReceipt(ToolExecutionReceiptRecord receipt)
    {
        if (string.IsNullOrWhiteSpace(receipt.ExitSummary))
        {
            return false;
        }

        return receipt.ExitSummary.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) ||
               receipt.ExitSummary.StartsWith("Denied", StringComparison.OrdinalIgnoreCase) ||
               receipt.ExitSummary.StartsWith("TimedOut", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldIgnoreSupersededCriticalToolFailure(
        ExecutionRunDetail detail,
        ToolExecutionReceiptRecord receipt)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(receipt);

        if (ShouldIgnoreRecoveredImplementationScaffoldFailure(detail, receipt))
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
        ExecutionRunDetail detail,
        ToolExecutionReceiptRecord receipt)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(receipt);

        if ((!receipt.ExitSummary.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) &&
             !receipt.ExitSummary.StartsWith("Denied", StringComparison.OrdinalIgnoreCase)) ||
            !IsImplementationBootstrapToolName(NormalizeToolToken(receipt.ToolName)))
        {
            return false;
        }

        if (detail.Run.State != ExecutionState.Completed ||
            detail.Run.Outcome != RunOutcome.Succeeded)
        {
            return false;
        }

        var responseText = ResolveRecoveredExecutionResponseText(detail);
        if (!TryResolveDeclaredStepOutcome(responseText, out var declaredOutcome) ||
            declaredOutcome.Status != ProcessStepRunStatus.Completed)
        {
            return false;
        }

        return detail.ToolReceipts.Any(item =>
        {
            if (ReferenceEquals(item, receipt) || IsFailedToolReceipt(item))
            {
                return false;
            }

            if (item.CompletedAtUtc < receipt.CompletedAtUtc ||
                item.CompletedAtUtc == receipt.CompletedAtUtc && item.StartedAtUtc < receipt.StartedAtUtc)
            {
                return false;
            }

            return ImplementationProofToolNames.Contains(NormalizeToolToken(item.ToolName));
        });
    }

    private static bool IsPlaceholderCriticalToolRequestSummary(
        string normalizedToolName,
        string? requestSummary)
    {
        if (string.IsNullOrWhiteSpace(normalizedToolName))
        {
            return false;
        }

        var normalizedSummary = NormalizeToolToken(requestSummary ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedSummary))
        {
            return true;
        }

        if (string.Equals(normalizedSummary, normalizedToolName, StringComparison.Ordinal))
        {
            return true;
        }

        return normalizedToolName.StartsWith("workspace_", StringComparison.Ordinal) &&
               string.Equals(
                   normalizedSummary,
                   normalizedToolName["workspace_".Length..],
                   StringComparison.Ordinal);
    }

    private static string NormalizeToolToken(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace('-', '_').Trim().ToLowerInvariant();
    }

    private static string? ResolveProviderNativeBrowserWorkingDirectory(ExecutionRunDetail detail)
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
    {
        return Path.GetExtension(expectedRelativePath).ToLowerInvariant() switch
        {
            ".png" => "browser_take_screenshot",
            ".yml" or ".yaml" => "browser_snapshot",
            ".log" or ".txt" => "browser_console_messages",
            _ => string.Empty
        };
    }

    private static bool MatchesExpectedBrowserOutputFile(string expectedRelativePath, string outputFileName)
    {
        var normalizedExpectedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(expectedRelativePath);
        var normalizedOutputPath = WorkspaceScopeDescriptor.NormalizeRelativePath(outputFileName);
        if (string.Equals(normalizedExpectedPath, normalizedOutputPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var expectedFileName = Path.GetFileName(normalizedExpectedPath);
        var outputFileNameOnly = Path.GetFileName(normalizedOutputPath);
        if (!string.Equals(expectedFileName, outputFileNameOnly, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expectedDirectoryName = Path.GetFileName(Path.GetDirectoryName(normalizedExpectedPath) ?? string.Empty);
        var outputDirectoryName = Path.GetFileName(Path.GetDirectoryName(normalizedOutputPath) ?? string.Empty);
        return string.Equals(expectedDirectoryName, outputDirectoryName, StringComparison.OrdinalIgnoreCase);
    }

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

    private static string BuildArtifactTitle(ExecutionArtifactRecord artifact)
    {
        return string.IsNullOrWhiteSpace(artifact.DisplayName)
            ? Path.GetFileName(artifact.RelativePath)
            : artifact.DisplayName.Trim();
    }

    private static string BuildExternalReferenceKey(ExecutionArtifactRecord artifact)
    {
        return $"agentframework-artifact:{artifact.Id:D}";
    }

    private static string BuildCompletedDecisionArtifactExternalReferenceKey(Guid stepRunId, Guid artifactExpectationId)
    {
        return $"process-step-decision:{stepRunId:D}:{artifactExpectationId:D}";
    }

    private static string BuildProviderNativeBrowserArtifactExternalReferenceKey(Guid executionRunId, string relativePath)
    {
        return $"agentframework-browser-artifact:{executionRunId:D}:{WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath)}";
    }

    private static string BuildProcessMockArtifactExternalReferenceKey(
        Guid stepRunId,
        Guid artifactExpectationId,
        string relativePath)
    {
        return $"process-mock-artifact:{stepRunId:D}:{artifactExpectationId:D}:{NormalizeManagedRelativePathForComparison(relativePath)}";
    }

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
        ExecutionArtifactRecord artifact)
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
        ExecutionArtifactRecord artifact)
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
            var sourceStepRunIds = sourceStepRuns?
                .Select(item => item.Id)
                .ToHashSet()
                ?? [];
            var matchingArtifacts = existingArtifacts
                .Where(item =>
                    item.StepRunId.HasValue &&
                    sourceStepRunIds.Contains(item.StepRunId.Value) &&
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
                matchingArtifacts));
        }

        return resolvedInputs;
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

    private static string BuildExpectedArtifactSummary(IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts)
    {
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

            builder.Append("  Trust: ");
            builder.Append(expectedArtifact.TrustRequirement);
            builder.Append(" | Sensitivity: ");
            builder.AppendLine(expectedArtifact.SensitivityLevel.ToString());
        }

        return builder.ToString().TrimEnd();
    }

    private static Guid? ResolveArtifactExpectationId(
        DispatchCandidate candidate,
        ExecutionArtifactRecord artifact)
    {
        return ResolveArtifactExpectation(candidate, artifact)?.Id;
    }

    private static DispatchArtifactExpectation? ResolveArtifactExpectation(
        DispatchCandidate candidate,
        ExecutionArtifactRecord artifact)
    {
        var matchedExpectationId = MatchExpectedArtifactId(candidate.ExpectedArtifacts, artifact);
        if (!matchedExpectationId.HasValue)
        {
            return null;
        }

        return candidate.ExpectedArtifacts.FirstOrDefault(item => item.Id == matchedExpectationId.Value);
    }

    internal static Guid? MatchExpectedArtifactId(
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
        ExecutionArtifactRecord artifact)
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
        var strongMatches = expectedArtifacts
            .Where(item => MatchesExpectedArtifact(item, relativePath, displayName, displaySlug, fileSlug))
            .ToList();
        if (strongMatches.Count == 1)
        {
            return strongMatches[0].Id;
        }

        if (strongMatches.Count > 1)
        {
            var kindMatches = strongMatches
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
        DispatchArtifactExpectation expectedArtifact,
        string relativePath,
        string displayName,
        string displaySlug,
        string fileSlug)
    {
        if (TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var expectedRelativePath))
        {
            return string.Equals(
                NormalizeManagedRelativePathForComparison(expectedRelativePath),
                NormalizeManagedRelativePathForComparison(relativePath),
                StringComparison.OrdinalIgnoreCase);
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

    private static bool MatchesExpectedArtifactByTitleTokens(
        string expectedTitle,
        string relativePath,
        string displayName)
    {
        var expectedTokens = TokenizeArtifactComparisonText(expectedTitle)
            .Where(token => !ArtifactTitleNoiseTokens.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (expectedTokens.Count < 2)
        {
            return false;
        }

        var observedTokens = TokenizeArtifactComparisonText(relativePath)
            .Concat(TokenizeArtifactComparisonText(displayName))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        if (observedTokens.Count == 0)
        {
            return false;
        }

        var matchedTokenCount = expectedTokens.Count(observedTokens.Contains);
        return matchedTokenCount >= 2;
    }

    private static IReadOnlyList<string> TokenizeArtifactComparisonText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var slug = FileSafeSlugBuilder.Build(value);
        return slug
            .Split(['-', '/', '.', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeArtifactComparisonToken)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToList();
    }

    private static string NormalizeArtifactComparisonToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > 3 &&
            normalized.EndsWith('s') &&
            !normalized.EndsWith("ss", StringComparison.Ordinal))
        {
            normalized = normalized[..^1];
        }

        return normalized;
    }

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
    {
        foreach (var marker in new[]
                 {
                     "Create this artifact at ",
                     "must exist at ",
                     "must be written at "
                 })
        {
            var markerIndex = validationRequirementSummary.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                continue;
            }

            var startIndex = markerIndex + marker.Length;
            var remainder = validationRequirementSummary[startIndex..].TrimStart();
            if (string.IsNullOrWhiteSpace(remainder))
            {
                continue;
            }

            var endIndex = remainder.IndexOfAny([' ', '\r', '\n', '\t']);
            var token = endIndex >= 0
                ? remainder[..endIndex]
                : remainder;
            token = token.Trim().TrimEnd('.', ',', ';', ':').Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            relativePath = token;
            return true;
        }

        relativePath = string.Empty;
        return false;
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
        foreach (var artifactInput in artifactInputs)
        {
            foreach (var artifact in artifactInput.Artifacts)
            {
                var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(artifact.ManagedStoragePath);
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
        }

        return new GovernedInspectionPaths(statPaths.ToList(), readPaths.ToList());
    }

    private static string FormatPromptPathList(IReadOnlyList<string> relativePaths)
    {
        return string.Join(", ", relativePaths.Select(relativePath => $"`{relativePath}`"));
    }

    private static string NormalizeManagedRelativePathForComparison(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var segments = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length >= 5 &&
            IsManagedRootSegment(segments[0]) &&
            string.Equals(segments[1], "scopes", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join('/', [segments[0], .. segments.Skip(4)]);
        }

        return normalized;
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
                "artifacts",
                "process-runs",
                candidate.Run.Id.ToString("D"),
                $"{candidate.StepRun.Sequence + 1:00}-{expectedSlug}.md"));
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

    private static bool ShouldProjectFinalAssistantResponse(ExecutionRunRecord run)
    {
        return run.State == ExecutionState.Completed &&
               run.Outcome == RunOutcome.Succeeded;
    }

    private static bool ShouldProjectResponseTextArtifacts(
        ExecutionRunRecord run,
        ProcessStepRunStatus completionStatus)
    {
        return completionStatus == ProcessStepRunStatus.Completed &&
               ShouldProjectFinalAssistantResponse(run);
    }

    private static string BuildResponseTextArtifactExternalReferenceKey(Guid executionRunId, string relativePath)
    {
        return $"assistant-response|{executionRunId:D}|{NormalizeManagedRelativePathForComparison(relativePath)}";
    }

    private static bool ShouldAutoRecordCompletedDecisionArtifact(DispatchArtifactExpectation expectedArtifact)
    {
        return expectedArtifact.IsRequired &&
               expectedArtifact.ArtifactKind == ProcessArtifactKind.Decision &&
               expectedArtifact.TrustRequirement is ProcessArtifactTrustRequirement.ReviewRequired or ProcessArtifactTrustRequirement.HumanApproved;
    }

    private static ProcessArtifactTrustStatus ResolveCompletedDecisionArtifactTrustStatus(
        ProcessArtifactTrustRequirement trustRequirement)
    {
        return trustRequirement switch
        {
            ProcessArtifactTrustRequirement.HumanApproved => ProcessArtifactTrustStatus.Approved,
            _ => ProcessArtifactTrustStatus.ReviewRequired
        };
    }

    private static string BuildCompletedDecisionArtifactProvenanceSummary(
        DispatchCandidate candidate,
        ExecutionRunDetail detail)
    {
        var executorName = string.IsNullOrWhiteSpace(candidate.StepRun.CurrentExecutorName)
            ? "the assigned approver"
            : candidate.StepRun.CurrentExecutorName.Trim();
        return $"Recorded from the governed step outcome for AgentFramework execution run {detail.Run.Id:D} by {executorName}.";
    }

    private static string BuildCompletedDecisionArtifactReviewSummary(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
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
        ExecutionRunDetail detail,
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
    {
        return string.Equals(segment, "artifacts", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, "output", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, "integration-map", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, "data", StringComparison.OrdinalIgnoreCase);
    }

}
