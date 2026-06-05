using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessAutomationExecutionLogObservation(
    IReadOnlySet<string> SuccessfulToolNames,
    IReadOnlyDictionary<string, IReadOnlyList<string>> BrowserToolOutputFiles)
{
    private static readonly ProcessAutomationExecutionLogObservation EmptyObservation = new(
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));

    internal static ProcessAutomationExecutionLogObservation Empty => EmptyObservation;

    internal static ProcessAutomationExecutionLogObservation Create(
        IReadOnlyList<ProcessAutomationExecutionLogEntry> executionLog,
        bool canTrustCompletedInternalToolLogs)
    {
        if (executionLog.Count == 0)
        {
            return Empty;
        }

        var toolNames = new HashSet<string>(StringComparer.Ordinal);
        var outputFilesByToolName = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var entry in executionLog)
        {
            if (!IsSuccessfulToolLogEntry(entry) ||
                !TryResolveInvokedToolName(entry.Message, out var toolName))
            {
                continue;
            }

            if (IsProviderNativeToolName(toolName) ||
                canTrustCompletedInternalToolLogs && IsInternalMafToolName(toolName))
            {
                toolNames.Add(toolName);
            }

            if (toolName.StartsWith("browser_", StringComparison.Ordinal) &&
                TryResolveFilenameArgument(entry.Message, out var outputFileName))
            {
                AddOutputFile(outputFilesByToolName, toolName, outputFileName);
            }
        }

        return new ProcessAutomationExecutionLogObservation(
            toolNames,
            ToOrderedOutputFileDictionary(outputFilesByToolName));
    }

    internal static bool TryResolveFilenameArgument(string message, out string fileName)
    {
        fileName = string.Empty;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        const string marker = "filename=\"";
        var start = message.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        start += marker.Length;
        var end = message.IndexOf('"', start);
        if (end <= start)
        {
            return false;
        }

        fileName = message[start..end].Trim();
        return !string.IsNullOrWhiteSpace(fileName);
    }

    internal static bool TryResolveInvokedToolName(string message, out string toolName)
    {
        toolName = string.Empty;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        const string prefix = "Invoking tool '";
        var start = message.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        start += prefix.Length;
        var end = message.IndexOf('\'', start);
        if (end <= start)
        {
            return false;
        }

        toolName = ProcessToolReceiptFacts.NormalizeToolToken(message[start..end]);
        return !string.IsNullOrWhiteSpace(toolName);
    }

    internal static bool IsProviderNativeToolName(string toolName)
    {
        return toolName.StartsWith("browser_", StringComparison.Ordinal);
    }

    internal static bool IsInternalMafToolName(string toolName)
    {
        return toolName.StartsWith("project_structure_", StringComparison.Ordinal) ||
               toolName.StartsWith("process_", StringComparison.Ordinal) ||
               toolName.StartsWith("image_generation_", StringComparison.Ordinal);
    }

    private static bool IsSuccessfulToolLogEntry(ProcessAutomationExecutionLogEntry entry)
    {
        return string.Equals(entry.Phase, "Tool", StringComparison.OrdinalIgnoreCase) &&
               entry.State != ProcessAutomationExecutionState.Failed;
    }

    private static void AddOutputFile(
        IDictionary<string, HashSet<string>> outputFilesByToolName,
        string toolName,
        string outputFileName)
    {
        if (!outputFilesByToolName.TryGetValue(toolName, out var outputFiles))
        {
            outputFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            outputFilesByToolName[toolName] = outputFiles;
        }

        outputFiles.Add(WorkspaceScopeDescriptor.NormalizeRelativePath(outputFileName));
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
