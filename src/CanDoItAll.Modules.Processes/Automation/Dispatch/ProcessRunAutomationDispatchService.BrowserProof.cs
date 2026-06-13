using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence;
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
    private static string ResolveInvalidBrowserProofSummary(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
    {
        if (!RequiresConcreteBrowserProof(candidate))
        {
            return string.Empty;
        }

        if (ContainsSerializedPowerShellErrorRecord(detail.Run.SerializedSessionStateJson))
        {
            return "the launch helper reported PowerShell errors on stderr despite a successful tool result";
        }

        var outputsByToolName = ResolveSuccessfulBrowserToolOutputFiles(detail);
        var missingEvidenceSummary = ResolveMissingRequiredBrowserEvidenceOutputSummary(candidate, outputsByToolName);
        if (!string.IsNullOrWhiteSpace(missingEvidenceSummary))
        {
            return missingEvidenceSummary;
        }

        var browserWorkingDirectory = ResolveProviderNativeBrowserWorkingDirectory(detail);
        var invalidEvidenceFileSummary = ResolveInvalidRequiredBrowserEvidenceFileSummary(
            candidate,
            browserWorkingDirectory,
            outputsByToolName);
        if (!string.IsNullOrWhiteSpace(invalidEvidenceFileSummary))
        {
            return invalidEvidenceFileSummary;
        }

        var invalidBrowserProofRecordSummary = ResolveInvalidBrowserProofRecordSummary(
            candidate,
            detail,
            outputsByToolName);
        if (!string.IsNullOrWhiteSpace(invalidBrowserProofRecordSummary))
        {
            return invalidBrowserProofRecordSummary;
        }

        var consoleEvidenceSummary = ResolveInvalidBrowserConsoleEvidenceSummary(
            detail,
            browserWorkingDirectory,
            outputsByToolName);
        if (!string.IsNullOrWhiteSpace(consoleEvidenceSummary))
        {
            return consoleEvidenceSummary;
        }

        var shallowInteractionSummary = ResolveShallowRepresentativeBrowserInteractionSummary(candidate, detail);
        if (!string.IsNullOrWhiteSpace(shallowInteractionSummary))
        {
            return shallowInteractionSummary;
        }

        if (string.IsNullOrWhiteSpace(browserWorkingDirectory))
        {
            return string.Empty;
        }

        if (!outputsByToolName.TryGetValue("browser_snapshot", out var snapshotFiles) ||
            snapshotFiles.Count == 0)
        {
            return string.Empty;
        }

        foreach (var snapshotFile in snapshotFiles)
        {
            if (!TryReadBrowserOutputText(browserWorkingDirectory, snapshotFile, out var snapshotText))
            {
                continue;
            }

            if (ContainsStarterTemplateBrowserProof(snapshotText))
            {
                return "browser proof captured starter-template content instead of the requested application";
            }

            if (ContainsRuntimeErrorBrowserProof(snapshotText))
            {
                return "browser proof captured an application runtime error instead of the requested application";
            }
        }

        return string.Empty;
    }

    private static string ResolveInvalidBrowserProofRecordSummary(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        IReadOnlyDictionary<string, IReadOnlyList<string>> outputsByToolName)
    {
        var proofRecords = ResolveSuccessfulSessionFileWrites(detail.Run.SerializedSessionStateJson)
            .Where(item => ProcessBrowserProofValidator.IsPotentialProofRecordPath(item.Path))
            .ToList();
        if (proofRecords.Count == 0)
        {
            return string.Empty;
        }

        var context = new ProcessBrowserProofValidationContext(
            ProcessRunId: candidate.Run.Id,
            ProcessStepRunId: candidate.StepRun.Id,
            ExecutionRunId: detail.Run.Id,
            ProjectId: candidate.Run.ProjectId,
            ExecutionStartedAtUtc: detail.Run.StartedAtUtc,
            RuntimeHostUrl: string.Empty,
            DatabaseProfileId: string.Empty,
            DatabaseProfileFingerprint: string.Empty,
            SuccessfulBrowserOutputPaths: outputsByToolName
                .SelectMany(item => item.Value)
                .Select(WorkspaceScopeDescriptor.NormalizeRelativePath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            SuccessfulBrowserToolNames: ResolveSuccessfulToolNames(detail)
                .Where(toolName => ToolContractCatalog.BrowserToolNames.Contains(toolName, StringComparer.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            RequiresRepresentativeInteraction: RequiresRepresentativeBrowserInteractionProof(candidate),
            RequiresCleanupReceipt: RequiresRuntimeCleanupReceipt(candidate));

        foreach (var proofRecord in proofRecords)
        {
            if (!ProcessBrowserProofValidator.TryParse(proofRecord.Content, out var record, out var parseDiagnostic))
            {
                return parseDiagnostic;
            }

            var result = ProcessBrowserProofValidator.Validate(record, context);
            if (!result.IsValid)
            {
                return result.Diagnostic;
            }
        }

        return string.Empty;
    }

    private static string ResolveMissingRequiredBrowserEvidenceOutputSummary(
        DispatchCandidate candidate,
        IReadOnlyDictionary<string, IReadOnlyList<string>> outputsByToolName)
    {
        if (RequiresBrowserScreenshotEvidenceArtifact(candidate) &&
            !HasBrowserEvidenceOutput(outputsByToolName, "browser_take_screenshot"))
        {
            return "required browser screenshot evidence was not captured as a durable browser artifact";
        }

        if (RequiresBrowserStateEvidenceArtifact(candidate) &&
            !HasBrowserEvidenceOutput(outputsByToolName, "browser_snapshot") &&
            !HasBrowserEvidenceOutput(outputsByToolName, "browser_evaluate"))
        {
            return "required browser snapshot or DOM evidence was not captured as a durable browser artifact";
        }

        if (RequiresBrowserConsoleEvidenceArtifact(candidate) &&
            !HasBrowserEvidenceOutput(outputsByToolName, "browser_console_messages"))
        {
            return "required browser console evidence was not captured as a durable browser artifact";
        }

        return string.Empty;
    }

    private static string ResolveInvalidRequiredBrowserEvidenceFileSummary(
        DispatchCandidate candidate,
        string? browserWorkingDirectory,
        IReadOnlyDictionary<string, IReadOnlyList<string>> outputsByToolName)
    {
        if (string.IsNullOrWhiteSpace(browserWorkingDirectory))
        {
            return string.Empty;
        }

        if (RequiresBrowserScreenshotEvidenceArtifact(candidate) &&
            !HasUsableBrowserEvidenceFile(browserWorkingDirectory, outputsByToolName, "browser_take_screenshot"))
        {
            return "required browser screenshot evidence file is missing, empty, or not an image";
        }

        if (RequiresBrowserStateEvidenceArtifact(candidate) &&
            !HasUsableBrowserEvidenceFile(browserWorkingDirectory, outputsByToolName, "browser_snapshot") &&
            !HasUsableBrowserEvidenceFile(browserWorkingDirectory, outputsByToolName, "browser_evaluate"))
        {
            return "required browser snapshot or DOM evidence file is missing, empty, or not a supported text artifact";
        }

        if (RequiresBrowserConsoleEvidenceArtifact(candidate) &&
            !HasUsableBrowserEvidenceFile(browserWorkingDirectory, outputsByToolName, "browser_console_messages"))
        {
            return "required browser console evidence file is missing, empty, or not a supported text artifact";
        }

        return string.Empty;
    }

    private static string ResolveInvalidBrowserConsoleEvidenceSummary(
        ProcessAutomationExecutionRunDetail detail,
        string? browserWorkingDirectory,
        IReadOnlyDictionary<string, IReadOnlyList<string>> outputsByToolName)
    {
        foreach (var consoleText in ResolveBrowserConsoleEvidenceTexts(detail, browserWorkingDirectory, outputsByToolName))
        {
            if (ContainsActiveBrowserConsoleError(consoleText))
            {
                return "browser console evidence contains active JavaScript or runtime errors";
            }
        }

        return string.Empty;
    }

    private static string ResolveShallowRepresentativeBrowserInteractionSummary(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
    {
        if (!RequiresRepresentativeBrowserInteractionProof(candidate) ||
            HasRepresentativeBrowserInteractionProof(detail))
        {
            return string.Empty;
        }

        return "interactive browser proof did not execute a representative interaction tool required by the step contract";
    }

    private static bool RequiresBrowserScreenshotEvidenceArtifact(DispatchCandidate candidate)
    {
        return CandidateRequiredEvidenceTextContains(candidate, ContainsNaturalScreenshotArtifactSignal) ||
               CandidateHasDeclaredBrowserEvidencePath(candidate, "browser_take_screenshot");
    }

    private static bool RequiresBrowserStateEvidenceArtifact(DispatchCandidate candidate)
    {
        return CandidateRequiredEvidenceTextContains(candidate, ContainsNaturalBrowserStateEvidenceSignal) ||
               CandidateHasDeclaredBrowserEvidencePath(candidate, "browser_snapshot") ||
               CandidateHasDeclaredBrowserEvidencePath(candidate, "browser_evaluate");
    }

    private static bool RequiresBrowserConsoleEvidenceArtifact(DispatchCandidate candidate)
    {
        return CandidateRequiredEvidenceTextContains(candidate, ContainsNaturalBrowserConsoleEvidenceSignal) ||
               CandidateHasDeclaredBrowserEvidencePath(candidate, "browser_console_messages");
    }

    private static bool RequiresRuntimeCleanupReceipt(DispatchCandidate candidate)
    {
        return CandidateRequiredEvidenceTextContains(
            candidate,
            static text => text.Contains("cleanup receipt", StringComparison.OrdinalIgnoreCase));
    }

    private static bool CandidateRequiredEvidenceTextContains(
        DispatchCandidate candidate,
        Func<string, bool> predicate)
    {
        return candidate.ExpectedArtifacts
            .Where(item => item.IsRequired)
            .Where(item => item.ArtifactKind == ProcessArtifactKind.Evidence)
            .Select(item => $"{item.Title} {item.ValidationRequirementSummary}")
            .Any(predicate);
    }

    private static bool CandidateHasDeclaredBrowserEvidencePath(
        DispatchCandidate candidate,
        string toolName)
    {
        return candidate.ExpectedArtifacts
            .Where(item => item.IsRequired)
            .Where(item => item.ArtifactKind == ProcessArtifactKind.Evidence)
            .Select(item => item.ValidationRequirementSummary)
            .Any(summary =>
                TryExtractExpectedArtifactRelativePath(summary, out var declaredRelativePath) &&
                string.Equals(ResolveProviderNativeBrowserToolName(declaredRelativePath), toolName, StringComparison.Ordinal));
    }

    private static bool ContainsNaturalScreenshotArtifactSignal(string text)
    {
        return Regex.IsMatch(
            text,
            @"(?<![A-Za-z0-9_])screen\s+shots?(?![A-Za-z0-9_])|(?<![A-Za-z0-9_])screenshots?(?![A-Za-z0-9_])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool ContainsNaturalBrowserStateEvidenceSignal(string text)
    {
        return Regex.IsMatch(
            text,
            @"(?<![A-Za-z0-9_])browser\s+(?:proof|evidence)(?![A-Za-z0-9_])|(?<![A-Za-z0-9_])snapshot(?:s)?(?![A-Za-z0-9_])|(?<![A-Za-z0-9_])DOM(?![A-Za-z0-9_])|(?<![A-Za-z0-9_])visible\s+state(?![A-Za-z0-9_])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool ContainsNaturalBrowserConsoleEvidenceSignal(string text)
    {
        var scriptToken = Regex.Escape(SoftwareDeliveryContractRules.JavaScriptContractToken);
        return Regex.IsMatch(
            text,
            $@"(?<![A-Za-z0-9_])(?:browser|{scriptToken})\s+console(?![A-Za-z0-9_])|(?<![A-Za-z0-9_])console\s+(?:messages?|logs?|diagnostics?)(?![A-Za-z0-9_])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool HasBrowserEvidenceOutput(
        IReadOnlyDictionary<string, IReadOnlyList<string>> outputsByToolName,
        string toolName)
    {
        return outputsByToolName.TryGetValue(toolName, out var outputFiles) &&
               outputFiles.Any(IsProviderNativeBrowserArtifactPath);
    }

    private static bool HasUsableBrowserEvidenceFile(
        string browserWorkingDirectory,
        IReadOnlyDictionary<string, IReadOnlyList<string>> outputsByToolName,
        string toolName)
    {
        return outputsByToolName.TryGetValue(toolName, out var outputFiles) &&
               outputFiles.Any(outputFile =>
                   BrowserOutputFileMatchesToolType(toolName, outputFile) &&
                   TryResolveSafeBrowserOutputPath(browserWorkingDirectory, outputFile, out var fullPath) &&
                   File.Exists(fullPath) &&
                   new FileInfo(fullPath).Length > 0);
    }

    private static bool BrowserOutputFileMatchesToolType(string toolName, string outputFile)
    {
        var extension = Path.GetExtension(outputFile).ToLowerInvariant();
        return toolName switch
        {
            "browser_take_screenshot" => IsImageExtension(extension),
            "browser_snapshot" => extension is ".yml" or ".yaml" or ".md" or ".txt",
            "browser_console_messages" => extension is ".log" or ".txt" or ".json",
            "browser_evaluate" => extension is ".json" or ".txt" or ".md",
            _ => true
        };
    }

    private static IReadOnlyList<string> ResolveBrowserConsoleEvidenceTexts(
        ProcessAutomationExecutionRunDetail detail,
        string? browserWorkingDirectory,
        IReadOnlyDictionary<string, IReadOnlyList<string>> outputsByToolName)
    {
        var texts = new List<string>();
        texts.AddRange(ResolveSuccessfulSessionToolResultTexts(detail.Run.SerializedSessionStateJson)
            .Where(item => string.Equals(item.ToolName, "browser_console_messages", StringComparison.Ordinal))
            .Select(item => item.Text)
            .Where(item => !string.IsNullOrWhiteSpace(item)));

        if (!string.IsNullOrWhiteSpace(browserWorkingDirectory) &&
            outputsByToolName.TryGetValue("browser_console_messages", out var consoleFiles))
        {
            foreach (var consoleFile in consoleFiles)
            {
                if (TryReadBrowserOutputText(browserWorkingDirectory, consoleFile, out var consoleText) &&
                    !string.IsNullOrWhiteSpace(consoleText))
                {
                    texts.Add(consoleText);
                }
            }
        }

        return texts;
    }

    private static bool ContainsActiveBrowserConsoleError(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = CollapsePromptWhitespace(text);
        if (ContainsPostStopBrowserDisconnectOnly(normalized))
        {
            return false;
        }

        return ContainsBrowserConsoleErrorSignal(normalized);
    }

    private static bool ContainsPostStopBrowserDisconnectOnly(string normalizedText)
    {
        return ContainsBrowserDisconnectSignal(normalizedText) &&
               ContainsPostStopBoundarySignal(normalizedText) &&
               !ContainsNonDisconnectBrowserConsoleErrorSignal(normalizedText);
    }

    private static bool ContainsBrowserConsoleErrorSignal(string normalizedText)
    {
        return ContainsNonDisconnectBrowserConsoleErrorSignal(normalizedText) ||
               ContainsBrowserDisconnectSignal(normalizedText);
    }

    private static bool ContainsNonDisconnectBrowserConsoleErrorSignal(string normalizedText)
    {
        return Regex.IsMatch(
                   normalizedText,
                   @"\b(?:TypeError|ReferenceError|SyntaxError|RangeError|EvalError|URIError)\b|(?:^|\s)(?:Unhandled|Uncaught)(?:\s+\w+)?\b|\bError:\s",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               normalizedText.Contains("[error]", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("blazor-error-ui", StringComparison.OrdinalIgnoreCase) ||
               Regex.IsMatch(
                   normalizedText,
                   @"Failed to load resource:.*\b(?:4\d\d|5\d\d|ERR_[A-Z_]+)\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool ContainsBrowserDisconnectSignal(string normalizedText)
    {
        return normalizedText.Contains("ERR_CONNECTION_REFUSED", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("WebSocket connection", StringComparison.OrdinalIgnoreCase) &&
               normalizedText.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("SignalR connection", StringComparison.OrdinalIgnoreCase) &&
               normalizedText.Contains("disconnected", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsPostStopBoundarySignal(string normalizedText)
    {
        return normalizedText.Contains("post-stop", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("after stop", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("after host stop", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("host stopped", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("server stopped", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("stop command completed", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("browser host stopped", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("process stopped", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresRepresentativeBrowserInteractionProof(DispatchCandidate candidate)
    {
        var contractText = ResolveQualityValidationContractText(candidate);
        if (string.IsNullOrWhiteSpace(contractText))
        {
            return false;
        }

        return Regex.IsMatch(
            contractText,
            @"(?<![A-Za-z0-9_])representative\s+(?:user\s+)?interaction(?![A-Za-z0-9_])|(?<![A-Za-z0-9_])interactive(?![A-Za-z0-9_])|(?<![A-Za-z0-9_])canvas(?![A-Za-z0-9_])|(?<![A-Za-z0-9_])game(?:play)?(?![A-Za-z0-9_])|(?<![A-Za-z0-9_])custom[-\s]+control(?![A-Za-z0-9_])|(?<![A-Za-z0-9_])keyboard[-\s]+first(?![A-Za-z0-9_])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool HasRepresentativeBrowserInteractionProof(ProcessAutomationExecutionRunDetail detail)
    {
        return ResolveSuccessfulToolNames(detail)
            .Any(IsRepresentativeBrowserInteractionToolName);
    }

    private static bool IsRepresentativeBrowserInteractionToolName(string toolName)
    {
        return toolName is "browser_click" or
            "browser_fill_form" or
            "browser_select_option" or
            "browser_press_key" or
            "browser_type" or
            "browser_drag" or
            "browser_evaluate";
    }

    private static bool ContainsSerializedPowerShellErrorRecord(string? serializedSessionStateJson)
    {
        if (string.IsNullOrWhiteSpace(serializedSessionStateJson))
        {
            return false;
        }

        return serializedSessionStateJson.Contains("Cannot overwrite variable PID because it is read-only or constant", StringComparison.OrdinalIgnoreCase) ||
               serializedSessionStateJson.Contains("WriteError:", StringComparison.OrdinalIgnoreCase) ||
               serializedSessionStateJson.Contains("ParserError:", StringComparison.OrdinalIgnoreCase) ||
               serializedSessionStateJson.Contains("RuntimeException:", StringComparison.OrdinalIgnoreCase) ||
               serializedSessionStateJson.Contains("FullyQualifiedErrorId", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadBrowserOutputText(
        string browserWorkingDirectory,
        string relativeOutputPath,
        out string text)
    {
        text = string.Empty;
        if (!TryResolveSafeBrowserOutputPath(browserWorkingDirectory, relativeOutputPath, out var fullPath) ||
            !File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(fullPath);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var buffer = new char[MaxBrowserSnapshotInspectionCharacters];
            var length = reader.ReadBlock(buffer, 0, buffer.Length);
            text = new string(buffer, 0, length);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryResolveSafeBrowserOutputPath(
        string browserWorkingDirectory,
        string relativeOutputPath,
        out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(browserWorkingDirectory) ||
            string.IsNullOrWhiteSpace(relativeOutputPath) ||
            Path.IsPathRooted(relativeOutputPath))
        {
            return false;
        }

        var root = Path.GetFullPath(browserWorkingDirectory);
        var candidate = Path.GetFullPath(Path.Combine(root, relativeOutputPath.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }

    private static bool ContainsStarterTemplateBrowserProof(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("Hello, world!", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Welcome to your new app.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsRuntimeErrorBrowserProof(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("Application error", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("An error has occurred", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Unhandled exception", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("HTTP ERROR 500", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("blazor-error-ui", StringComparison.OrdinalIgnoreCase);
    }

}
