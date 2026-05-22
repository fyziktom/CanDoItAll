using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests
{
    [Fact]
    public async Task GetDashboardAsync_counts_recent_active_and_failed_runs_from_split_execution_storage()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var workspaceStore = Assert.IsAssignableFrom<ISandboxWorkspaceExecutionRunStore>(
            scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceStore>());

        var baseline = await workspaceService.GetDashboardAsync();
        var agent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Programming Workspace Analyst", StringComparison.Ordinal));

        var now = DateTimeOffset.UtcNow;
        await workspaceStore.SaveExecutionRunDetailAsync(
            CreateExecutionRunDetail(
                agent.Id,
                Guid.NewGuid(),
                now,
                ExecutionState.Running,
                outcome: null,
                updatedAtUtc: now),
            CancellationToken.None);
        await workspaceStore.SaveExecutionRunDetailAsync(
            CreateExecutionRunDetail(
                agent.Id,
                Guid.NewGuid(),
                now,
                ExecutionState.Completed,
                RunOutcome.Failed,
                updatedAtUtc: now),
            CancellationToken.None);
        await workspaceStore.SaveExecutionRunDetailAsync(
            CreateExecutionRunDetail(
                agent.Id,
                Guid.NewGuid(),
                now.AddHours(-2),
                ExecutionState.Running,
                outcome: null,
                updatedAtUtc: now.AddHours(-2)),
            CancellationToken.None);

        var dashboard = await workspaceService.GetDashboardAsync();

        Assert.Equal(baseline.SessionCount, dashboard.SessionCount);
        Assert.Equal(baseline.ActiveRuns + 1, dashboard.ActiveRuns);
        Assert.Equal(baseline.FailedRuns + 1, dashboard.FailedRuns);
    }

    [Fact]
    public async Task GetExecutionRunDetailAsync_projects_successful_playwright_browser_calls_into_tool_receipts()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var workspaceStore = Assert.IsAssignableFrom<ISandboxWorkspaceExecutionRunStore>(
            scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceStore>());

        var agent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Programming Workspace Analyst", StringComparison.Ordinal));

        var screenshotPath = Path.Combine(application.ActiveProfile.WorkspaceRootPath, "evidence", "playwright", "workflow-proof.png");
        Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath) ?? application.ActiveProfile.WorkspaceRootPath);
        await File.WriteAllTextAsync(screenshotPath, "proof");

        var now = DateTimeOffset.UtcNow;
        var runId = Guid.NewGuid();
        await workspaceStore.SaveExecutionRunDetailAsync(
            new ExecutionRunDetail(
                new ExecutionRunRecord(
                    Id: runId,
                    AgentId: agent.Id,
                    ChatSessionId: null,
                    Title: "Playwright proof",
                    SourceKind: "manual",
                    SourceId: "proof-1",
                    CorrelationId: "corr-1",
                    CausationId: string.Empty,
                    RequestedBy: "codex",
                    RequestedByKind: "system",
                    MetadataJson: "{}",
                    InputSummary: "Validate browser MCP",
                    ResultSummary: "Completed",
                    ProviderName: "OpenAI chat completions",
                    Model: "gpt-4o-mini",
                    State: ExecutionState.Completed,
                    Outcome: RunOutcome.Succeeded,
                    CreatedAtUtc: now,
                    UpdatedAtUtc: now,
                    StartedAtUtc: now,
                    CompletedAtUtc: now.AddSeconds(5),
                    RuntimeSessionKey: string.Empty,
                    SerializedSessionStateJson: BuildSerializedSessionState(
                        ("browser_navigate", new Dictionary<string, object?> { ["url"] = "http://127.0.0.1:5502/agents?tab=capabilities" }, CreateProviderNativeTextResult("Navigation completed.")),
                        ("browser_snapshot", new Dictionary<string, object?>(), CreateProviderNativeTextResult("Snapshot saved.")),
                        ("browser_take_screenshot", new Dictionary<string, object?> { ["type"] = "png", ["filename"] = screenshotPath }, CreateProviderNativeTextResult("Screenshot saved."))),
                    PendingApprovals: []),
                null,
                [
                    CreateLogEntry(runId, agent.Id, now.AddSeconds(1), ExecutionState.WaitingOnTool, "Tool", "Invoking tool 'browser_navigate' with url=\"http://127.0.0.1:5502/agents?tab=capabilities\"."),
                    CreateLogEntry(runId, agent.Id, now.AddSeconds(2), ExecutionState.WaitingOnTool, "Tool", "Invoking tool 'browser_snapshot'."),
                    CreateLogEntry(runId, agent.Id, now.AddSeconds(3), ExecutionState.WaitingOnTool, "Tool", $"Invoking tool 'browser_take_screenshot' with type=\"png\", filename=\"{screenshotPath}\"."),
                    CreateLogEntry(runId, agent.Id, now.AddSeconds(4), ExecutionState.Completed, "Completed", "Execution run response persisted.")
                ],
                [])
            {
                ToolReceipts =
                [
                    new ToolExecutionReceiptRecord(
                        Id: Guid.NewGuid(),
                        ExecutionRunId: runId,
                        ToolFamily: "workspace-process",
                        ToolName: "local_mcp_launch",
                        RiskClass: "LocalExecution:Mcp",
                        ApprovalMode: "NotRequired",
                        IsolationGuarantee: "PolicyOnlyLocal",
                        RequestSummary: "@playwright/mcp@latest, --headless, --caps, vision",
                        WorkingDirectory: application.ActiveProfile.WorkspaceRootPath,
                        ExitSummary: "Prepared",
                        StartedAtUtc: now,
                        CompletedAtUtc: now)
                ]
            },
            CancellationToken.None);

        var detail = await workspaceService.GetExecutionRunDetailAsync(runId);
        var receipts = await workspaceService.ListToolExecutionReceiptsAsync(runId);

        Assert.Contains(detail.ToolReceipts, item => string.Equals(item.ToolName, "browser_navigate", StringComparison.Ordinal));
        Assert.Contains(detail.ToolReceipts, item => string.Equals(item.ToolName, "browser_snapshot", StringComparison.Ordinal));

        var screenshotReceipt = Assert.Single(
            detail.ToolReceipts,
            item => string.Equals(item.ToolName, "browser_take_screenshot", StringComparison.Ordinal));
        Assert.Equal("mcp-server", screenshotReceipt.ToolFamily);
        Assert.Equal(application.ActiveProfile.WorkspaceRootPath, screenshotReceipt.WorkingDirectory);
        Assert.Equal("Succeeded", screenshotReceipt.ExitSummary);
        Assert.Contains("filename=", screenshotReceipt.RequestSummary, StringComparison.Ordinal);

        Assert.Contains(receipts, item => string.Equals(item.ToolName, "browser_navigate", StringComparison.Ordinal));
        Assert.Contains(receipts, item => string.Equals(item.ToolName, "browser_snapshot", StringComparison.Ordinal));
        Assert.Contains(receipts, item => string.Equals(item.ToolName, "browser_take_screenshot", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetExecutionRunDetailAsync_projects_playwright_browser_calls_from_execution_logs_when_chat_history_is_empty()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var workspaceStore = Assert.IsAssignableFrom<ISandboxWorkspaceExecutionRunStore>(
            scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceStore>());

        var agent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => string.Equals(item.Name, "Programming Workspace Analyst", StringComparison.Ordinal));

        var now = DateTimeOffset.UtcNow;
        var runId = Guid.NewGuid();
        const string screenshotPath = "artifacts/process-runs/run-001/browser-proof.png";
        await workspaceStore.SaveExecutionRunDetailAsync(
            new ExecutionRunDetail(
                new ExecutionRunRecord(
                    Id: runId,
                    AgentId: agent.Id,
                    ChatSessionId: null,
                    Title: "Playwright proof from logs",
                    SourceKind: "manual",
                    SourceId: "proof-logs-1",
                    CorrelationId: "corr-logs-1",
                    CausationId: string.Empty,
                    RequestedBy: "codex",
                    RequestedByKind: "system",
                    MetadataJson: "{}",
                    InputSummary: "Validate browser MCP",
                    ResultSummary: "Completed",
                    ProviderName: "OpenAI chat completions",
                    Model: "gpt-4o-mini",
                    State: ExecutionState.Completed,
                    Outcome: RunOutcome.Succeeded,
                    CreatedAtUtc: now,
                    UpdatedAtUtc: now,
                    StartedAtUtc: now,
                    CompletedAtUtc: now.AddSeconds(5),
                    RuntimeSessionKey: string.Empty,
                    SerializedSessionStateJson: BuildEmptySerializedSessionState(),
                    PendingApprovals: []),
                null,
                [
                    CreateLogEntry(runId, agent.Id, now.AddSeconds(1), ExecutionState.WaitingOnTool, "Tool", "Invoking tool 'browser_navigate' with url=\"http://127.0.0.1:5502/\"."),
                    CreateLogEntry(runId, agent.Id, now.AddSeconds(2), ExecutionState.WaitingOnTool, "Tool", $"Invoking tool 'browser_take_screenshot' with type=\"png\", filename=\"{screenshotPath}\", fullPage=\"False\"."),
                    CreateLogEntry(runId, agent.Id, now.AddSeconds(3), ExecutionState.WaitingOnTool, "Tool", "Invoking tool 'browser_console_messages' with level=\"info\", all=\"True\"."),
                    CreateLogEntry(runId, agent.Id, now.AddSeconds(4), ExecutionState.Completed, "Completed", "Execution run response persisted.")
                ],
                [])
            {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Id: Guid.NewGuid(),
                    ExecutionRunId: runId,
                    ToolFamily: "workspace-process",
                    ToolName: "local_mcp_launch",
                    RiskClass: "LocalExecution:Mcp",
                    ApprovalMode: "NotRequired",
                    IsolationGuarantee: "PolicyOnlyLocal",
                    RequestSummary: "@playwright/mcp@latest, --headless, --caps, vision",
                    WorkingDirectory: application.ActiveProfile.WorkspaceRootPath,
                    ExitSummary: "Prepared",
                    StartedAtUtc: now,
                    CompletedAtUtc: now)
            ]
            },
            CancellationToken.None);

        var detail = await workspaceService.GetExecutionRunDetailAsync(runId);

        Assert.Contains(detail.ToolReceipts, item => string.Equals(item.ToolName, "browser_navigate", StringComparison.Ordinal));
        var screenshotReceipt = Assert.Single(
            detail.ToolReceipts,
            item => string.Equals(item.ToolName, "browser_take_screenshot", StringComparison.Ordinal));
        Assert.Equal("mcp-server", screenshotReceipt.ToolFamily);
        Assert.Contains(screenshotPath, screenshotReceipt.RequestSummary, StringComparison.Ordinal);
        Assert.Contains(detail.ToolReceipts, item => string.Equals(item.ToolName, "browser_console_messages", StringComparison.Ordinal));
    }

    private static ExecutionLogEntry CreateLogEntry(
        Guid executionRunId,
        Guid agentId,
        DateTimeOffset createdAtUtc,
        ExecutionState state,
        string phase,
        string message)
    {
        return new ExecutionLogEntry(
            Id: Guid.NewGuid(),
            AgentId: agentId,
            ChatSessionId: null,
            CreatedAtUtc: createdAtUtc,
            State: state,
            Phase: phase,
            Message: message)
        {
            ExecutionRunId = executionRunId
        };
    }

    private static ExecutionRunDetail CreateExecutionRunDetail(
        Guid agentId,
        Guid runId,
        DateTimeOffset createdAtUtc,
        ExecutionState state,
        RunOutcome? outcome,
        DateTimeOffset updatedAtUtc)
    {
        return new ExecutionRunDetail(
            new ExecutionRunRecord(
                Id: runId,
                AgentId: agentId,
                ChatSessionId: null,
                Title: "Dashboard summary test",
                SourceKind: "integration-test",
                SourceId: $"run-{runId:N}",
                CorrelationId: $"corr-{runId:N}",
                CausationId: string.Empty,
                RequestedBy: "test",
                RequestedByKind: "system",
                MetadataJson: "{}",
                InputSummary: "summary",
                ResultSummary: outcome?.ToString() ?? state.ToString(),
                ProviderName: "OpenAI default",
                Model: "gpt-4.1",
                State: state,
                Outcome: outcome,
                CreatedAtUtc: createdAtUtc,
                UpdatedAtUtc: updatedAtUtc,
                StartedAtUtc: createdAtUtc,
                CompletedAtUtc: outcome.HasValue ? updatedAtUtc : null,
                RuntimeSessionKey: string.Empty,
                SerializedSessionStateJson: string.Empty,
                PendingApprovals: []),
            null,
            [],
            []);
    }

    private static string BuildSerializedSessionState(params (string ToolName, IReadOnlyDictionary<string, object?> Arguments, object Result)[] toolCalls)
    {
        var callContents = toolCalls
            .Select((toolCall, index) => new Dictionary<string, object?>
            {
                ["$type"] = "functionCall",
                ["callId"] = $"call-{index + 1}",
                ["name"] = toolCall.ToolName,
                ["arguments"] = toolCall.Arguments
            })
            .ToArray();
        var resultContents = toolCalls
            .Select((toolCall, index) => new Dictionary<string, object?>
            {
                ["$type"] = "functionResult",
                ["callId"] = $"call-{index + 1}",
                ["result"] = toolCall.Result
            })
            .ToArray();

        return JsonSerializer.Serialize(
            new
            {
                stateBag = new
                {
                    InMemoryChatHistoryProvider = new
                    {
                        messages = new object[]
                        {
                            new
                            {
                                role = "assistant",
                                contents = callContents
                            },
                            new
                            {
                                role = "tool",
                                contents = resultContents
                            }
                        }
                    }
                }
            });
    }

    private static string BuildEmptySerializedSessionState()
    {
        return JsonSerializer.Serialize(
            new
            {
                stateBag = new
                {
                    InMemoryChatHistoryProvider = new
                    {
                        messages = Array.Empty<object>()
                    }
                }
            });
    }

    private static object CreateProviderNativeTextResult(string text)
    {
        return new Dictionary<string, string>
        {
            ["$type"] = "text",
            ["text"] = text
        };
    }
}
