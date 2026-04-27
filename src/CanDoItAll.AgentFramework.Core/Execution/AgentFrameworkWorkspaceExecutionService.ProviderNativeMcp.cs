using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceExecutionService
{
    private sealed record SuccessfulSessionToolCall(
        string CallId,
        string ToolName,
        string RequestSummary,
        string? OutputFileName);

    private ExecutionRunDetail EnrichProviderNativeMcpDetail(ExecutionRunDetail detail)
    {
        if (string.IsNullOrWhiteSpace(detail.Run.SerializedSessionStateJson))
        {
            return detail;
        }

        var launchReceipt = ResolvePlaywrightLaunchReceipt(detail.ToolReceipts);
        if (launchReceipt is null)
        {
            return detail;
        }

        var successfulCalls = ResolveSuccessfulSessionToolCalls(detail.Run.SerializedSessionStateJson);
        if (successfulCalls.Count == 0)
        {
            return detail;
        }

        var invocationTimeline = BuildInvocationTimeline(detail.ExecutionLog);
        var syntheticReceipts = new List<ToolExecutionReceiptRecord>();

        foreach (var call in successfulCalls)
        {
            if (!call.ToolName.StartsWith("browser_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (detail.ToolReceipts.Any(receipt =>
                    string.Equals(receipt.ToolName, call.ToolName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(receipt.RequestSummary, call.RequestSummary, StringComparison.Ordinal) &&
                    string.Equals(receipt.WorkingDirectory, launchReceipt.WorkingDirectory, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var startedAtUtc = ResolveStartedAtUtc(
                invocationTimeline,
                call.ToolName,
                detail.Run.StartedAtUtc ?? detail.Run.CreatedAtUtc);
            var completedAtUtc = ResolveCompletedAtUtc(
                launchReceipt.WorkingDirectory,
                call.OutputFileName,
                startedAtUtc,
                detail.Run.CompletedAtUtc ?? detail.Run.UpdatedAtUtc);

            syntheticReceipts.Add(
                new ToolExecutionReceiptRecord(
                    Id: CreateDeterministicGuid($"{detail.Run.Id:N}|provider-native-mcp|{call.CallId}|{call.ToolName}|{call.RequestSummary}"),
                    ExecutionRunId: detail.Run.Id,
                    ToolFamily: "mcp-server",
                    ToolName: call.ToolName,
                    RiskClass: launchReceipt.RiskClass,
                    ApprovalMode: launchReceipt.ApprovalMode,
                    IsolationGuarantee: launchReceipt.IsolationGuarantee,
                    RequestSummary: call.RequestSummary,
                    WorkingDirectory: launchReceipt.WorkingDirectory,
                    ExitSummary: "Succeeded",
                    StartedAtUtc: startedAtUtc,
                    CompletedAtUtc: completedAtUtc));
        }

        if (syntheticReceipts.Count == 0)
        {
            return detail;
        }

        return detail with
        {
            ToolReceipts = detail.ToolReceipts
                .Concat(syntheticReceipts)
                .OrderByDescending(item => item.CompletedAtUtc)
                .ThenByDescending(item => item.StartedAtUtc)
                .ToList()
        };
    }

    private static ToolExecutionReceiptRecord? ResolvePlaywrightLaunchReceipt(
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts)
    {
        return toolReceipts
            .Where(receipt =>
                string.Equals(receipt.ToolName, "local_mcp_launch", StringComparison.OrdinalIgnoreCase) &&
                receipt.RequestSummary.Contains("@playwright/mcp", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
    }

    private static Dictionary<string, Queue<DateTimeOffset>> BuildInvocationTimeline(
        IReadOnlyList<ExecutionLogEntry> executionLog)
    {
        return executionLog
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item => new
            {
                item.CreatedAtUtc,
                ToolName = ExtractToolNameFromLog(item.Message)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.ToolName))
            .GroupBy(item => item.ToolName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new Queue<DateTimeOffset>(group.Select(item => item.CreatedAtUtc)),
                StringComparer.OrdinalIgnoreCase);
    }

    private static DateTimeOffset ResolveStartedAtUtc(
        IReadOnlyDictionary<string, Queue<DateTimeOffset>> invocationTimeline,
        string toolName,
        DateTimeOffset fallbackValue)
    {
        if (invocationTimeline.TryGetValue(toolName, out var timestamps) &&
            timestamps.Count > 0)
        {
            return timestamps.Dequeue();
        }

        return fallbackValue;
    }

    private static DateTimeOffset ResolveCompletedAtUtc(
        string workingDirectory,
        string? outputFileName,
        DateTimeOffset startedAtUtc,
        DateTimeOffset fallbackValue)
    {
        if (string.IsNullOrWhiteSpace(outputFileName))
        {
            return startedAtUtc > fallbackValue ? startedAtUtc : fallbackValue;
        }

        var candidatePath = TryResolveToolOutputPath(workingDirectory, outputFileName);
        if (!string.IsNullOrWhiteSpace(candidatePath) && File.Exists(candidatePath))
        {
            var lastWriteUtc = File.GetLastWriteTimeUtc(candidatePath);
            if (lastWriteUtc > DateTime.MinValue)
            {
                var completedAtUtc = new DateTimeOffset(DateTime.SpecifyKind(lastWriteUtc, DateTimeKind.Utc));
                return completedAtUtc < startedAtUtc
                    ? startedAtUtc
                    : completedAtUtc;
            }
        }

        return startedAtUtc > fallbackValue ? startedAtUtc : fallbackValue;
    }

    private static string? TryResolveToolOutputPath(string workingDirectory, string outputFileName)
    {
        if (string.IsNullOrWhiteSpace(outputFileName))
        {
            return null;
        }

        var normalizedOutputFileName = outputFileName.Trim();
        return Path.IsPathRooted(normalizedOutputFileName)
            ? Path.GetFullPath(normalizedOutputFileName)
            : Path.GetFullPath(Path.Combine(workingDirectory, normalizedOutputFileName));
    }

    private static string? ExtractToolNameFromLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        const string marker = "Invoking tool '";
        var markerIndex = message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var startIndex = markerIndex + marker.Length;
        var endIndex = message.IndexOf('\'', startIndex);
        if (endIndex <= startIndex)
        {
            return null;
        }

        var toolName = message[startIndex..endIndex].Trim();
        return string.IsNullOrWhiteSpace(toolName) ? null : toolName;
    }

    private static IReadOnlyList<SuccessfulSessionToolCall> ResolveSuccessfulSessionToolCalls(
        string serializedSessionStateJson)
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

            var callsById = new Dictionary<string, SuccessfulSessionToolCall>(StringComparer.Ordinal);
            var successfulCalls = new List<SuccessfulSessionToolCall>();

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
                            ? nameElement.GetString()
                            : null;
                        if (string.IsNullOrWhiteSpace(callId) || string.IsNullOrWhiteSpace(toolName))
                        {
                            continue;
                        }

                        var requestSummary = content.TryGetProperty("arguments", out var argumentsElement)
                            ? BuildArgumentSummary(argumentsElement)
                            : string.Empty;
                        var outputFileName = content.TryGetProperty("arguments", out var callArgumentsElement)
                            ? TryResolveOutputFileName(callArgumentsElement)
                            : null;

                        callsById[callId] = new SuccessfulSessionToolCall(
                            CallId: callId,
                            ToolName: toolName.Trim(),
                            RequestSummary: requestSummary,
                            OutputFileName: outputFileName);
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

                    successfulCalls.Add(call);
                }
            }

            return successfulCalls;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string BuildArgumentSummary(JsonElement argumentsElement)
    {
        if (argumentsElement.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        foreach (var property in argumentsElement.EnumerateObject())
        {
            parts.Add($"{property.Name}={FormatArgumentValue(property.Value)}");
        }

        return parts.Count == 0
            ? string.Empty
            : string.Join(", ", parts);
    }

    private static string? TryResolveOutputFileName(JsonElement argumentsElement)
    {
        if (argumentsElement.ValueKind != JsonValueKind.Object ||
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

    private static string FormatArgumentValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => $"\"{value.GetString()}\"",
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.Null => "null",
            _ => value.GetRawText()
        };
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

    private static Guid CreateDeterministicGuid(string seed)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(seed));
        return new Guid(hash);
    }
}
