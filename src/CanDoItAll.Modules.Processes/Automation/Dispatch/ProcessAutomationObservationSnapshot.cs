using CanDoItAll.AgentFramework.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessAutomationObservationSnapshot(
    ProcessAutomationSessionObservation Session,
    ProcessAutomationExecutionLogObservation ExecutionLog,
    IReadOnlySet<string> SuccessfulToolNames,
    IReadOnlyDictionary<string, IReadOnlyList<string>> BrowserToolOutputFiles)
{
    internal static ProcessAutomationObservationSnapshot Create(
        ProcessAutomationExecutionRunDetail detail,
        bool canTrustCompletedInternalToolLogs)
    {
        var session = ProcessAutomationSessionObservation.Create(detail.Run.SerializedSessionStateJson);
        var executionLog = ProcessAutomationExecutionLogObservation.Create(
            detail.ExecutionLog,
            canTrustCompletedInternalToolLogs);
        var successfulToolNames = ProcessAutomationReceiptObservationHelper
            .ResolveSuccessfulToolNames(detail)
            .ToHashSet(StringComparer.Ordinal);
        successfulToolNames.UnionWith(session.SuccessfulToolNames);
        successfulToolNames.UnionWith(executionLog.SuccessfulToolNames);

        var outputFilesByToolName = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var pair in session.BrowserToolOutputFiles)
        {
            AddBrowserOutputFiles(outputFilesByToolName, pair.Key, pair.Value);
        }

        foreach (var pair in executionLog.BrowserToolOutputFiles)
        {
            AddBrowserOutputFiles(outputFilesByToolName, pair.Key, pair.Value);
        }

        foreach (var pair in ResolveBrowserEvidenceReferenceOutputFiles(detail.Run.ResultSummary))
        {
            AddBrowserOutputFiles(outputFilesByToolName, pair.Key, pair.Value);
        }

        return new ProcessAutomationObservationSnapshot(
            session,
            executionLog,
            successfulToolNames,
            ToOrderedOutputFileDictionary(outputFilesByToolName));
    }

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveBrowserEvidenceReferenceOutputFiles(
        string? resultSummary)
    {
        if (string.IsNullOrWhiteSpace(resultSummary))
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        }

        var outputFilesByToolName = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var evidenceRef in ResolveBrowserEvidenceReferences(resultSummary))
        {
            var normalizedRef = WorkspaceScopeDescriptor.NormalizeRelativePath(evidenceRef);
            if (!ProcessArtifactProviderNativeVisualValidationRules.IsProviderNativeBrowserEvidenceReferencePath(normalizedRef))
            {
                continue;
            }

            var toolName = ProcessArtifactProviderNativeVisualValidationRules.ResolveProviderNativeBrowserToolName(normalizedRef);
            if (string.IsNullOrWhiteSpace(toolName))
            {
                continue;
            }

            AddBrowserOutputFiles(outputFilesByToolName, toolName, [normalizedRef]);
        }

        return ToOrderedOutputFileDictionary(outputFilesByToolName);
    }

    internal static IReadOnlyList<string> ResolveBrowserEvidenceReferences(string resultSummary)
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

    private static void AddBrowserOutputFiles(
        IDictionary<string, HashSet<string>> outputFilesByToolName,
        string toolName,
        IEnumerable<string> outputFiles)
    {
        var normalizedToolName = ProcessToolReceiptFacts.NormalizeToolToken(toolName);
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

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ToOrderedOutputFileDictionary(
        IReadOnlyDictionary<string, HashSet<string>> outputFilesByToolName)
    {
        return outputFilesByToolName.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            StringComparer.Ordinal);
    }
}
