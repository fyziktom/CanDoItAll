using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Contracts;
using System.Text.Json;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessAutomationObservationTests
{
    [Fact]
    public void Session_observation_extracts_successful_tools_files_browser_outputs_and_assistant_state()
    {
        var serializedSessionState = BuildSerializedSessionState(
            BuildAssistantMessage(
                BuildTextContent("Earlier response."),
                BuildErrorContent("rate_limit", "Provider temporarily unavailable.")),
            BuildToolMessage(
                BuildFunctionCall("write-1", "workspace_write_file", new Dictionary<string, object?>
                {
                    ["path"] = "src/App.cs",
                    ["content"] = "public sealed class App {}"
                }),
                BuildFunctionResult("write-1", new Dictionary<string, object?>
                {
                    ["succeeded"] = true,
                    ["text"] = "File written."
                }),
                BuildFunctionCall("read-1", "workspace_read_file", new Dictionary<string, object?>
                {
                    ["path"] = "src/App.cs"
                }),
                BuildFunctionResult("read-1", new Dictionary<string, object?>
                {
                    ["succeeded"] = true,
                    ["path"] = "src/App.cs",
                    ["content"] = "public sealed class App {}"
                }),
                BuildFunctionCall("stat-1", "workspace_stat_path", new Dictionary<string, object?>
                {
                    ["path"] = "src/App.cs"
                }),
                BuildFunctionResult("stat-1", new Dictionary<string, object?>
                {
                    ["succeeded"] = true,
                    ["path"] = "src/App.cs"
                }),
                BuildFunctionCall("browser-1", "browser_snapshot", new Dictionary<string, object?>
                {
                    ["filename"] = "proof/browser/page.yml"
                }),
                BuildFunctionResult("browser-1", new Dictionary<string, object?>
                {
                    ["succeeded"] = true,
                    ["text"] = "Snapshot saved."
                }),
                BuildFunctionCall("failed-1", "workspace_write_file", new Dictionary<string, object?>
                {
                    ["path"] = "src/Failed.cs",
                    ["content"] = "ignored"
                }),
                BuildFunctionResult("failed-1", "Error: denied.")),
            BuildAssistantMessage(BuildTextContent("Latest response.")));

        var observation = ProcessAutomationSessionObservation.Create(serializedSessionState);

        Assert.Contains("workspace_write_file", observation.SuccessfulToolNames);
        Assert.Contains("workspace_read_file", observation.SuccessfulToolNames);
        Assert.Contains("workspace_stat_path", observation.SuccessfulToolNames);
        Assert.Contains("browser_snapshot", observation.SuccessfulToolNames);
        Assert.DoesNotContain("workspace_write_file_failed", observation.SuccessfulToolNames);
        Assert.Contains(observation.SuccessfulToolResultTexts, item =>
            item.ToolName == "browser_snapshot" &&
            item.Text.Contains("Snapshot saved.", StringComparison.Ordinal));
        Assert.Contains(observation.FileWrites, item =>
            item.Path == "src/App.cs" &&
            item.Content == "public sealed class App {}");
        Assert.Contains(observation.FileReads, item =>
            item.Path == "src/App.cs" &&
            item.Content == "public sealed class App {}");
        Assert.Contains(observation.PathStats, item => item.Path == "src/App.cs");
        Assert.Contains(
            "proof/browser/page.yml",
            observation.BrowserToolOutputFiles["browser_snapshot"],
            StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Latest response.", observation.LatestAssistantResponseText);
        Assert.Equal("rate_limit: Provider temporarily unavailable.", observation.LatestAssistantErrorSummary);
    }

    [Fact]
    public void Session_observation_returns_empty_for_malformed_json()
    {
        var observation = ProcessAutomationSessionObservation.Create("{not-json");

        Assert.Empty(observation.SuccessfulToolNames);
        Assert.Empty(observation.SuccessfulToolResultTexts);
        Assert.Empty(observation.FileWrites);
        Assert.Empty(observation.BrowserToolOutputFiles);
        Assert.Null(observation.LatestAssistantResponseText);
        Assert.Null(observation.LatestAssistantErrorSummary);
    }

    [Fact]
    public void Execution_log_observation_trusts_browser_tools_and_only_trusted_internal_maf_tools()
    {
        var now = DateTimeOffset.UtcNow;
        var executionLog = new[]
        {
            CreateExecutionLogToolInvocation(now, "browser_snapshot", "proof/browser/page.yml"),
            CreateExecutionLogToolInvocation(now.AddSeconds(1), "process_update", null),
            new ProcessAutomationExecutionLogEntry(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                now.AddSeconds(2),
                ProcessAutomationExecutionState.Failed,
                "Tool",
                "Invoking tool 'browser_take_screenshot' with filename=\"proof/browser/failed.png\".")
        };

        var untrustedObservation = ProcessAutomationExecutionLogObservation.Create(executionLog, false);
        var trustedObservation = ProcessAutomationExecutionLogObservation.Create(executionLog, true);

        Assert.Contains("browser_snapshot", untrustedObservation.SuccessfulToolNames);
        Assert.DoesNotContain("process_update", untrustedObservation.SuccessfulToolNames);
        Assert.Contains("process_update", trustedObservation.SuccessfulToolNames);
        Assert.Contains(
            "proof/browser/page.yml",
            untrustedObservation.BrowserToolOutputFiles["browser_snapshot"],
            StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("browser_take_screenshot", trustedObservation.SuccessfulToolNames);
    }

    private static ProcessAutomationExecutionLogEntry CreateExecutionLogToolInvocation(
        DateTimeOffset timestamp,
        string toolName,
        string? fileName)
    {
        var message = string.IsNullOrWhiteSpace(fileName)
            ? $"Invoking tool '{toolName}' with test arguments."
            : $"Invoking tool '{toolName}' with filename=\"{fileName}\".";
        return new ProcessAutomationExecutionLogEntry(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            timestamp,
            ProcessAutomationExecutionState.Running,
            "Tool",
            message);
    }

    private static string BuildSerializedSessionState(params object[] messages)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["stateBag"] = new Dictionary<string, object?>
            {
                ["InMemoryChatHistoryProvider"] = new Dictionary<string, object?>
                {
                    ["messages"] = messages
                }
            }
        }, AgentOutputJson.SerializerOptions);
    }

    private static Dictionary<string, object?> BuildAssistantMessage(params object[] contents)
    {
        return new Dictionary<string, object?>
        {
            ["role"] = "assistant",
            ["contents"] = contents
        };
    }

    private static Dictionary<string, object?> BuildToolMessage(params object[] contents)
    {
        return new Dictionary<string, object?>
        {
            ["role"] = "tool",
            ["contents"] = contents
        };
    }

    private static Dictionary<string, object?> BuildTextContent(string text)
    {
        return new Dictionary<string, object?>
        {
            ["$type"] = "text",
            ["text"] = text
        };
    }

    private static Dictionary<string, object?> BuildErrorContent(string errorCode, string message)
    {
        return new Dictionary<string, object?>
        {
            ["$type"] = "error",
            ["errorCode"] = errorCode,
            ["message"] = message
        };
    }

    private static Dictionary<string, object?> BuildFunctionCall(
        string callId,
        string toolName,
        Dictionary<string, object?> arguments)
    {
        return new Dictionary<string, object?>
        {
            ["$type"] = "functionCall",
            ["callId"] = callId,
            ["name"] = toolName,
            ["arguments"] = arguments
        };
    }

    private static Dictionary<string, object?> BuildFunctionResult(string callId, object result)
    {
        return new Dictionary<string, object?>
        {
            ["$type"] = "functionResult",
            ["callId"] = callId,
            ["result"] = result
        };
    }
}
