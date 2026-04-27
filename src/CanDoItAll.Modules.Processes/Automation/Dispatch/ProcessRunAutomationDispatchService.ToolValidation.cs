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
    private static IReadOnlyList<ToolExecutionReceiptRecord> ResolveUnresolvedCriticalToolFailures(ExecutionRunDetail detail)
    {
        var latestCriticalReceipts = detail.ToolReceipts
            .Where(IsCriticalToolReceipt)
            .GroupBy(
                item => string.Join(
                    "|",
                    NormalizeToolToken(item.ToolName),
                    item.RequestSummary.Trim(),
                    item.WorkingDirectory.Trim()),
                StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(item => item.CompletedAtUtc)
                .ThenByDescending(item => item.StartedAtUtc)
                .First())
            .ToList();

        return latestCriticalReceipts
            .Where(IsFailedToolReceipt)
            .Where(item => !ShouldIgnoreSupersededCriticalToolFailure(detail, item))
            .ToList();
    }

    private static IReadOnlyList<string> ResolveMissingRequiredToolExecutions(
        DispatchCandidate candidate,
        ExecutionRunDetail detail)
    {
        return ResolveMissingRequiredToolExecutionsWithCarryForward(candidate, detail, []);
    }

    private static IReadOnlyList<string> ResolveMissingRequiredToolExecutionsWithCarryForward(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        IEnumerable<string> successfulToolNamesFromPriorAttempts)
    {
        var requiredToolNames = ResolveRequiredToolNames(candidate);
        if (requiredToolNames.Count == 0)
        {
            return [];
        }

        var successfulToolNames = ResolveSuccessfulToolNames(detail);
        foreach (var toolName in successfulToolNamesFromPriorAttempts)
        {
            var normalizedToolName = NormalizeToolToken(toolName);
            if (!string.IsNullOrWhiteSpace(normalizedToolName) &&
                ShouldCarryForwardSuccessfulToolName(candidate, normalizedToolName))
            {
                successfulToolNames.Add(normalizedToolName);
            }
        }

        foreach (var toolName in ResolveProcessMockSatisfiedToolNames(candidate, detail, requiredToolNames))
        {
            successfulToolNames.Add(toolName);
        }

        var missing = new List<string>();

        foreach (var requiredToolName in requiredToolNames)
        {
            if (!successfulToolNames.Contains(requiredToolName))
            {
                missing.Add(requiredToolName);
            }
        }

        return missing;
    }

    private static IReadOnlyList<string> ResolveProcessMockSatisfiedToolNames(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        IReadOnlyCollection<string> requiredToolNames)
    {
        var projections = ResolveProcessMockArtifactProjections(detail.Run.SerializedSessionStateJson);
        if (projections.Count == 0 ||
            !projections.Any(projection => ProcessMockProjectionMatchesRequiredArtifact(candidate, projection)))
        {
            return [];
        }

        var satisfiedToolNames = new List<string>();
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
                .Where(toolName => ImplementationProofToolNames.Contains(toolName, StringComparer.Ordinal)));
            if (RequiresConcreteTestProof(candidate) &&
                requiredToolNames.Contains("workspace_dotnet_test", StringComparer.Ordinal))
            {
                satisfiedToolNames.Add("workspace_dotnet_test");
            }
        }

        return satisfiedToolNames
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static bool ShouldCarryForwardSuccessfulToolName(DispatchCandidate candidate, string normalizedToolName)
    {
        if (string.IsNullOrWhiteSpace(normalizedToolName))
        {
            return false;
        }

        if (RequiresConcreteImplementationProof(candidate) &&
            CurrentAttemptOnlyImplementationProofToolNames.Contains(normalizedToolName))
        {
            return false;
        }

        if (RequiresConcreteBrowserProof(candidate) &&
            CurrentAttemptOnlyBrowserProofToolNames.Contains(normalizedToolName))
        {
            return false;
        }

        return true;
    }

    private static ProcessStepRunStatus ResolveCompletionStatusWithCarryForward(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
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
        ExecutionRunDetail detail,
        IEnumerable<string> successfulToolNamesFromPriorAttempts,
        string? responseText)
    {
        var run = detail.Run;
        var missingRequiredTools = ResolveMissingRequiredToolExecutionsWithCarryForward(
            candidate,
            detail,
            successfulToolNamesFromPriorAttempts);
        if (run.State != ExecutionState.Completed)
        {
            return run.PendingApprovals.Count > 0
                ? ProcessStepRunStatus.WaitingApproval
                : run.State == ExecutionState.Failed
                    ? ProcessStepRunStatus.Failed
                    : candidate.StepRun.Status == ProcessStepRunStatus.WaitingApproval
                        ? ProcessStepRunStatus.WaitingApproval
                        : ProcessStepRunStatus.InProgress;
        }

        if (run.PendingApprovals.Count > 0)
        {
            return ProcessStepRunStatus.WaitingApproval;
        }

        if (run.Outcome != RunOutcome.Succeeded)
        {
            return ProcessStepRunStatus.Failed;
        }

        if (missingRequiredTools.Count > 0)
        {
            return ProcessStepRunStatus.Failed;
        }

        if (ResolveUnresolvedCriticalToolFailures(detail).Count > 0)
        {
            return ProcessStepRunStatus.Failed;
        }

        if (TryResolveRecoverableProviderFailure(detail, responseText, out _))
        {
            return ProcessStepRunStatus.Failed;
        }

        var inspectionText = ResolveOutputInspectionText(responseText);
        var missingConcreteProofSummary = ResolveMissingConcreteProofSummary(candidate, inspectionText);
        var incompleteImplementationSummary = ResolveIncompleteImplementationSummary(candidate, inspectionText);
        var missingConcreteImplementationProofSummary = ResolveMissingConcreteImplementationProofSummary(candidate, detail);
        var invalidBrowserProofSummary = ResolveInvalidBrowserProofSummary(candidate, detail);
        var missingRequiredArtifactSummary = ResolveMissingRequiredArtifactSummary(candidate, detail, inspectionText);
        if (TryResolveDeclaredStepOutcome(candidate, responseText, out var declaredOutcome))
        {
            if (!string.IsNullOrWhiteSpace(ResolveBranchOutcomeSelectionFailure(candidate, declaredOutcome)))
            {
                return ProcessStepRunStatus.Failed;
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(missingConcreteProofSummary))
            {
                return ProcessStepRunStatus.Blocked;
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(incompleteImplementationSummary))
            {
                return ProcessStepRunStatus.Blocked;
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(missingConcreteImplementationProofSummary))
            {
                return ProcessStepRunStatus.Blocked;
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(invalidBrowserProofSummary))
            {
                return ProcessStepRunStatus.Blocked;
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(missingRequiredArtifactSummary))
            {
                return ProcessStepRunStatus.Blocked;
            }

            return declaredOutcome.Status;
        }

        if (!string.IsNullOrWhiteSpace(missingConcreteProofSummary) ||
            !string.IsNullOrWhiteSpace(incompleteImplementationSummary) ||
            !string.IsNullOrWhiteSpace(missingConcreteImplementationProofSummary) ||
            !string.IsNullOrWhiteSpace(invalidBrowserProofSummary) ||
            !string.IsNullOrWhiteSpace(missingRequiredArtifactSummary))
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

        return ProcessStepRunStatus.Completed;
    }

    private static string BuildCompletionReasonWithCarryForward(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
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
        ExecutionRunDetail detail,
        string stepTitle,
        IEnumerable<string> successfulToolNamesFromPriorAttempts,
        string? responseText)
    {
        return BuildCompletionReasonCore(
            candidate,
            detail,
            stepTitle,
            ResolveMissingRequiredToolExecutionsWithCarryForward(candidate, detail, successfulToolNamesFromPriorAttempts),
            responseText);
    }

    private static bool TryResolveDeclaredStepOutcome(string? responseText, out DeclaredStepOutcome declaredOutcome)
    {
        declaredOutcome = default;
        if (!TryReadProcessStepOutcome(responseText, out var outcome, out _))
        {
            return false;
        }

        declaredOutcome = new DeclaredStepOutcome(
            MapProcessStepOutcomeStatus(outcome.Status),
            outcome.Reason.Trim(),
            null,
            outcome.BranchOutcomeKey.Trim(),
            outcome.BranchOutcomeTitle.Trim());
        return true;
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

    private static ISet<string> ResolveSuccessfulToolNames(ExecutionRunDetail detail)
    {
        var successfulToolNames = detail.ToolReceipts
            .Where(receipt => !IsFailedToolReceipt(receipt))
            .Select(receipt => NormalizeToolToken(receipt.ToolName))
            .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var toolName in ResolveSuccessfulSessionToolNames(detail.Run.SerializedSessionStateJson))
        {
            successfulToolNames.Add(toolName);
        }

        return successfulToolNames;
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
