using CanDoItAll.AgentFramework.Models;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessAutomationSessionObservation(
    IReadOnlySet<string> SuccessfulToolNames,
    IReadOnlyList<ProcessAutomationSessionToolResultText> SuccessfulToolResultTexts,
    IReadOnlyList<ProcessAutomationSessionFileContent> FileWrites,
    IReadOnlyList<ProcessAutomationSessionFileContent> FileReads,
    IReadOnlyList<ProcessAutomationSessionFileContent> PathStats,
    string? LatestAssistantResponseText,
    string? LatestAssistantErrorSummary,
    IReadOnlyDictionary<string, IReadOnlyList<string>> BrowserToolOutputFiles)
{
    private static readonly ProcessAutomationSessionObservation EmptyObservation = new(
        new HashSet<string>(StringComparer.Ordinal),
        [],
        [],
        [],
        [],
        null,
        null,
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));

    internal static ProcessAutomationSessionObservation Empty => EmptyObservation;

    internal static ProcessAutomationSessionObservation Create(string? serializedSessionStateJson)
    {
        if (string.IsNullOrWhiteSpace(serializedSessionStateJson))
        {
            return Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(serializedSessionStateJson);
            return !TryResolveMessages(document.RootElement, out var messages)
                ? Empty
                : CreateFromMessages(messages);
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    internal static string ExtractToolResultText(JsonElement result)
    {
        var builder = new StringBuilder();
        AppendToolResultText(builder, result, 0);
        return builder.ToString();
    }

    internal static bool IsSuccessfulFunctionResult(JsonElement result)
    {
        switch (result.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
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

    internal static string? TryResolveToolOutputFileName(JsonElement functionCallContent)
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

    private static ProcessAutomationSessionObservation CreateFromMessages(JsonElement messages)
    {
        var callsById = new Dictionary<string, ProcessAutomationSessionToolCall>(StringComparer.Ordinal);
        var successfulToolNames = new HashSet<string>(StringComparer.Ordinal);
        var successfulToolResultTexts = new List<ProcessAutomationSessionToolResultText>();
        var fileWrites = new List<ProcessAutomationSessionFileContent>();
        var fileReads = new List<ProcessAutomationSessionFileContent>();
        var pathStats = new List<ProcessAutomationSessionFileContent>();
        var outputFilesByToolName = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        string? latestAssistantResponseText = null;
        string? latestAssistantErrorSummary = null;

        foreach (var message in messages.EnumerateArray())
        {
            if (!message.TryGetProperty("contents", out var contents) ||
                contents.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            if (IsAssistantMessage(message))
            {
                latestAssistantResponseText = ResolveAssistantResponseText(contents) ?? latestAssistantResponseText;
                latestAssistantErrorSummary = ResolveAssistantErrorSummary(contents) ?? latestAssistantErrorSummary;
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
                    TrackFunctionCall(callsById, content);
                    continue;
                }

                if (string.Equals(contentType, "functionResult", StringComparison.Ordinal))
                {
                    TrackFunctionResult(
                        callsById,
                        successfulToolNames,
                        successfulToolResultTexts,
                        fileWrites,
                        fileReads,
                        pathStats,
                        outputFilesByToolName,
                        content);
                }
            }
        }

        return new ProcessAutomationSessionObservation(
            successfulToolNames,
            successfulToolResultTexts,
            fileWrites,
            fileReads,
            pathStats,
            latestAssistantResponseText,
            latestAssistantErrorSummary,
            ToOrderedOutputFileDictionary(outputFilesByToolName));
    }

    private static void TrackFunctionCall(
        IDictionary<string, ProcessAutomationSessionToolCall> callsById,
        JsonElement content)
    {
        var callId = content.TryGetProperty("callId", out var callIdElement)
            ? callIdElement.GetString()
            : null;
        var toolName = content.TryGetProperty("name", out var nameElement)
            ? ProcessToolReceiptFacts.NormalizeToolToken(nameElement.GetString())
            : string.Empty;
        if (string.IsNullOrWhiteSpace(callId) || string.IsNullOrWhiteSpace(toolName))
        {
            return;
        }

        var fileObservationKind = ResolveFileObservationKind(toolName);
        callsById[callId] = new ProcessAutomationSessionToolCall(
            toolName,
            TryResolveToolOutputFileName(content),
            fileObservationKind,
            fileObservationKind == ProcessAutomationSessionFileObservationKind.None
                ? null
                : ResolveCallFileContent(content, fileObservationKind));
    }

    private static void TrackFunctionResult(
        IReadOnlyDictionary<string, ProcessAutomationSessionToolCall> callsById,
        ISet<string> successfulToolNames,
        ICollection<ProcessAutomationSessionToolResultText> successfulToolResultTexts,
        ICollection<ProcessAutomationSessionFileContent> fileWrites,
        ICollection<ProcessAutomationSessionFileContent> fileReads,
        ICollection<ProcessAutomationSessionFileContent> pathStats,
        IDictionary<string, HashSet<string>> outputFilesByToolName,
        JsonElement content)
    {
        var resultCallId = content.TryGetProperty("callId", out var resultCallIdElement)
            ? resultCallIdElement.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(resultCallId) ||
            !callsById.TryGetValue(resultCallId, out var call) ||
            !content.TryGetProperty("result", out var resultElement) ||
            !IsSuccessfulFunctionResult(resultElement))
        {
            return;
        }

        successfulToolNames.Add(call.ToolName);
        TrackToolResultText(successfulToolResultTexts, call.ToolName, resultElement);
        TrackBrowserOutputFile(outputFilesByToolName, call);
        TrackFileObservation(fileWrites, fileReads, pathStats, call, resultElement);
    }

    private static void TrackToolResultText(
        ICollection<ProcessAutomationSessionToolResultText> successfulToolResultTexts,
        string toolName,
        JsonElement resultElement)
    {
        var resultText = ExtractToolResultText(resultElement);
        if (!string.IsNullOrWhiteSpace(resultText))
        {
            successfulToolResultTexts.Add(new ProcessAutomationSessionToolResultText(toolName, resultText));
        }
    }

    private static void TrackBrowserOutputFile(
        IDictionary<string, HashSet<string>> outputFilesByToolName,
        ProcessAutomationSessionToolCall call)
    {
        if (string.IsNullOrWhiteSpace(call.OutputFileName))
        {
            return;
        }

        if (!outputFilesByToolName.TryGetValue(call.ToolName, out var outputFiles))
        {
            outputFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            outputFilesByToolName[call.ToolName] = outputFiles;
        }

        outputFiles.Add(WorkspaceScopeDescriptor.NormalizeRelativePath(call.OutputFileName));
    }

    private static void TrackFileObservation(
        ICollection<ProcessAutomationSessionFileContent> fileWrites,
        ICollection<ProcessAutomationSessionFileContent> fileReads,
        ICollection<ProcessAutomationSessionFileContent> pathStats,
        ProcessAutomationSessionToolCall call,
        JsonElement resultElement)
    {
        if (call.FileObservationKind == ProcessAutomationSessionFileObservationKind.None ||
            call.CallFileContent is null)
        {
            return;
        }

        var resultFileContent = ResolveResultFileContent(resultElement, call.FileObservationKind);
        var fileContent = resultFileContent ?? call.CallFileContent;
        switch (call.FileObservationKind)
        {
            case ProcessAutomationSessionFileObservationKind.Write:
            {
                fileWrites.Add(fileContent);
                break;
            }
            case ProcessAutomationSessionFileObservationKind.Read:
            {
                fileReads.Add(fileContent);
                break;
            }
            case ProcessAutomationSessionFileObservationKind.Stat:
            {
                pathStats.Add(fileContent);
                break;
            }
        }
    }

    private static bool TryResolveMessages(JsonElement root, out JsonElement messages)
    {
        if (root.TryGetProperty("stateBag", out var stateBag) &&
            stateBag.TryGetProperty("InMemoryChatHistoryProvider", out var historyProvider) &&
            historyProvider.TryGetProperty("messages", out messages) &&
            messages.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        messages = default;
        return false;
    }

    private static bool IsAssistantMessage(JsonElement message)
    {
        return message.TryGetProperty("role", out var roleElement) &&
               string.Equals(roleElement.GetString(), "assistant", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveAssistantResponseText(JsonElement contents)
    {
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

        return assistantParts.Count == 0
            ? null
            : string.Join(Environment.NewLine, assistantParts);
    }

    private static string? ResolveAssistantErrorSummary(JsonElement contents)
    {
        string? latestAssistantError = null;
        foreach (var content in contents.EnumerateArray())
        {
            if (TryResolveAssistantErrorSummary(content, out var assistantError))
            {
                latestAssistantError = assistantError;
            }
        }

        return latestAssistantError;
    }

    internal static bool TryResolveAssistantErrorSummary(JsonElement content, out string assistantError)
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

    private static ProcessAutomationSessionFileObservationKind ResolveFileObservationKind(string toolName)
    {
        return toolName switch
        {
            "workspace_write_file" or "workspace_append_file" => ProcessAutomationSessionFileObservationKind.Write,
            "workspace_read_file" => ProcessAutomationSessionFileObservationKind.Read,
            "workspace_stat_path" => ProcessAutomationSessionFileObservationKind.Stat,
            _ => ProcessAutomationSessionFileObservationKind.None
        };
    }

    private static ProcessAutomationSessionFileContent? ResolveCallFileContent(
        JsonElement callContent,
        ProcessAutomationSessionFileObservationKind observationKind)
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

        var content = observationKind == ProcessAutomationSessionFileObservationKind.Write
            ? TryResolveStringProperty(arguments, "content") ?? string.Empty
            : string.Empty;
        return new ProcessAutomationSessionFileContent(path.Trim(), content);
    }

    private static ProcessAutomationSessionFileContent? ResolveResultFileContent(
        JsonElement resultContent,
        ProcessAutomationSessionFileObservationKind observationKind)
    {
        if (observationKind == ProcessAutomationSessionFileObservationKind.Write)
        {
            return null;
        }

        var path = TryResolveStringProperty(resultContent, "path");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var content = observationKind == ProcessAutomationSessionFileObservationKind.Read
            ? TryResolveStringProperty(resultContent, "content") ?? string.Empty
            : string.Empty;
        return new ProcessAutomationSessionFileContent(path.Trim(), content);
    }

    private static string? TryResolveStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var propertyValue) &&
               propertyValue.ValueKind == JsonValueKind.String
            ? propertyValue.GetString()
            : null;
    }

    private static void AppendToolResultText(StringBuilder builder, JsonElement element, int depth)
    {
        if (depth > 4)
        {
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
            {
                AppendToolResultTextPart(builder, element.GetString());
                return;
            }
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
            {
                AppendToolResultTextPart(builder, element.ToString());
                return;
            }
            case JsonValueKind.Array:
            {
                foreach (var item in element.EnumerateArray())
                {
                    AppendToolResultText(builder, item, depth + 1);
                }

                return;
            }
            case JsonValueKind.Object:
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String &&
                        IsDiagnosticToolResultProperty(property.Name))
                    {
                        AppendToolResultTextPart(builder, property.Value.GetString());
                        continue;
                    }

                    if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        AppendToolResultText(builder, property.Value, depth + 1);
                    }
                }

                return;
            }
        }
    }

    private static bool IsDiagnosticToolResultProperty(string propertyName)
    {
        return propertyName.Equals("text", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("content", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("message", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("summary", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("output", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("stdout", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("stderr", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("exitSummary", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendToolResultTextPart(StringBuilder builder, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(value.Trim());
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

    private sealed record ProcessAutomationSessionToolCall(
        string ToolName,
        string? OutputFileName,
        ProcessAutomationSessionFileObservationKind FileObservationKind,
        ProcessAutomationSessionFileContent? CallFileContent);

    private enum ProcessAutomationSessionFileObservationKind
    {
        None,
        Write,
        Read,
        Stat
    }
}

internal sealed record ProcessAutomationSessionToolResultText(string ToolName, string Text);

internal sealed record ProcessAutomationSessionFileContent(string Path, string Content);
