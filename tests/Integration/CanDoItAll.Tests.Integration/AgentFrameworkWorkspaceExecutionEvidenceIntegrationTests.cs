using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests
{
    [Fact]
    public async Task GetDashboardAsync_counts_state_based_active_and_failed_runs_from_split_execution_storage()
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
        Assert.Equal(baseline.ActiveRuns + 2, dashboard.ActiveRuns);
        Assert.Equal(baseline.FailedRuns + 1, dashboard.FailedRuns);
    }

    [Fact]
    public async Task GetAgentOverviewAsync_projects_usage_statistics_from_split_execution_projection()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var workspaceStore = Assert.IsAssignableFrom<ISandboxWorkspaceExecutionRunStore>(
            scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceStore>());
        var agents = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        Assert.True(agents.Length >= 2);

        var now = DateTimeOffset.UtcNow;
        var topAgent = agents[0];
        var secondAgent = agents[1];
        var firstRunId = Guid.NewGuid();
        var secondRunId = Guid.NewGuid();
        var thirdRunId = Guid.NewGuid();

        await workspaceStore.SaveExecutionRunDetailAsync(
            CreateExecutionRunDetail(
                topAgent.Id,
                firstRunId,
                now,
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                updatedAtUtc: now,
                providerName: "OpenAI default",
                model: "gpt-4.1") with
            {
                UsageObservations =
                [
                    CreateUsageObservation(
                        firstRunId,
                        topAgent.Id,
                        now,
                        "OpenAI default",
                        ProviderKind.OpenAi,
                        "gpt-4.1",
                        ProviderUsageObservationStatus.Observed,
                        inputTokens: 100,
                        outputTokens: 40,
                        costUsd: 0.015m)
                ]
            },
            CancellationToken.None);

        await workspaceStore.SaveExecutionRunDetailAsync(
            CreateExecutionRunDetail(
                topAgent.Id,
                secondRunId,
                now.AddMinutes(1),
                ExecutionState.Completed,
                RunOutcome.Failed,
                updatedAtUtc: now.AddMinutes(1),
                providerName: "OpenAI default",
                model: "gpt-4.1") with
            {
                UsageObservations =
                [
                    CreateUsageObservation(
                        secondRunId,
                        topAgent.Id,
                        now.AddMinutes(1),
                        "OpenAI default",
                        ProviderKind.OpenAi,
                        "gpt-4.1",
                        ProviderUsageObservationStatus.MissingAfterProviderActivity,
                        inputTokens: 0,
                        outputTokens: 0,
                        costUsd: null)
                ]
            },
            CancellationToken.None);

        await workspaceStore.SaveExecutionRunDetailAsync(
            CreateExecutionRunDetail(
                secondAgent.Id,
                thirdRunId,
                now.AddMinutes(2),
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                updatedAtUtc: now.AddMinutes(2),
                providerName: "Azure OpenAI default",
                model: "gpt-4o") with
            {
                UsageObservations =
                [
                    CreateUsageObservation(
                        thirdRunId,
                        secondAgent.Id,
                        now.AddMinutes(2),
                        "Azure OpenAI default",
                        ProviderKind.AzureOpenAi,
                        "gpt-4o",
                        ProviderUsageObservationStatus.Observed,
                        inputTokens: 80,
                        outputTokens: 30,
                        costUsd: 0.01m)
                ]
            },
            CancellationToken.None);

        var overview = await workspaceService.GetAgentOverviewAsync();
        var agentDetails = await workspaceService.GetAgentUsageDetailsAsync();
        var providerDetails = await workspaceService.GetProviderUsageDetailsAsync();
        var modelDetails = await workspaceService.GetModelUsageDetailsAsync();

        Assert.True(AgentAvatarImageCatalog.IsBundledAvatarUrl(topAgent.AvatarImageUrl));
        Assert.True(overview.Totals.UsageObservationCount >= 3);
        Assert.True(overview.Totals.KnownUsageObservationCount >= 2);
        Assert.True(overview.Totals.UnknownUsageObservationCount >= 1);
        Assert.True(overview.Totals.TotalTokens >= 250);
        Assert.Contains(overview.TopAgents, item =>
            item.AgentId == topAgent.Id &&
            item.AgentName == topAgent.Name &&
            item.AvatarImageUrl == topAgent.AvatarImageUrl &&
            item.RunCount == 2 &&
            item.FailedRunCount == 1 &&
            item.UsageObservationCount == 2 &&
            item.UnknownUsageObservationCount == 1);
        Assert.Contains(overview.TopFailingAgents, item =>
            item.AgentId == topAgent.Id &&
            item.FailedRunCount == 1);
        Assert.All(overview.TopFailingAgents, item => Assert.True(item.FailedRunCount > 0));
        Assert.True(overview.TopFailingAgents.Count <= 5);
        Assert.NotEmpty(overview.TeamShortcuts);
        Assert.All(overview.TeamShortcuts, item => Assert.True(AgentTeamIconCatalog.IsAllowed(item.Icon)));

        var topAgentDetail = Assert.Single(agentDetails.Agents, item => item.AgentId == topAgent.Id);
        Assert.Equal(topAgent.Name, topAgentDetail.AgentName);
        Assert.Equal(2, topAgentDetail.RunCount);

        var openAiProvider = Assert.Single(providerDetails.Providers, item =>
            item.ProviderName == "OpenAI default" &&
            item.ProviderKind == ProviderKind.OpenAi);
        Assert.Equal(2, openAiProvider.UsageObservationCount);
        Assert.Equal(1, openAiProvider.KnownUsageObservationCount);
        Assert.Equal(1, openAiProvider.UnknownUsageObservationCount);
        Assert.Equal(1, openAiProvider.FailedRunCount);

        var model = Assert.Single(modelDetails.Models, item =>
            item.ProviderName == "OpenAI default" &&
            item.Model == "gpt-4.1");
        Assert.Equal(2, model.UsageObservationCount);
        Assert.Equal(140, model.TotalTokens);
    }

    [Fact]
    public async Task LoadUsageProjectionAsync_returns_empty_projection_without_execution_usage()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"cda-empty-agent-usage-{Guid.NewGuid():N}");
        try
        {
            var store = new FileSandboxWorkspaceStore(
                workspaceRoot,
                WorkspaceScopeDescriptor.Organization("empty-agent-usage-test"));

            var projection = await store.LoadUsageProjectionAsync();

            Assert.Equal(0, projection.Agents.Sum(item => item.RunCount));
            Assert.Equal(0, projection.UsageObservationCount);
            Assert.Equal(0, projection.KnownUsageObservationCount);
            Assert.Equal(0, projection.UnknownUsageObservationCount);
            Assert.Equal(0, projection.TotalTokens);
            Assert.Empty(projection.Agents);
            Assert.Empty(projection.Providers);
            Assert.Empty(projection.Models);
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
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
                        RequestSummary: "--yes, @playwright/mcp@0.0.78, --headless, --caps, vision",
                            WorkingDirectory: application.ActiveProfile.WorkspaceRootPath,
                            ExitSummary: "Prepared",
                            StartedAtUtc: now,
                            CompletedAtUtc: now)
                        {
                            RuntimeToolProviderKey = "provider-native.mcp",
                            RuntimeToolProviderName = "Provider-native MCP"
                        }
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
        Assert.Equal("provider-native.mcp", screenshotReceipt.RuntimeToolProviderKey);
        Assert.Equal("Provider-native MCP", screenshotReceipt.RuntimeToolProviderName);

        var projectedBrowserReceipts = detail.ToolReceipts
            .Where(item => item.ToolName.StartsWith("browser_", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(3, projectedBrowserReceipts.Count);
        Assert.All(
            projectedBrowserReceipts,
            receipt =>
            {
                Assert.Equal("provider-native.mcp", receipt.RuntimeToolProviderKey);
                Assert.Equal("Provider-native MCP", receipt.RuntimeToolProviderName);
            });

        Assert.Contains(receipts, item => string.Equals(item.ToolName, "browser_navigate", StringComparison.Ordinal));
        Assert.Contains(receipts, item => string.Equals(item.ToolName, "browser_snapshot", StringComparison.Ordinal));
        Assert.Contains(receipts, item => string.Equals(item.ToolName, "browser_take_screenshot", StringComparison.Ordinal));
        var persistedBrowserReceipts = receipts
            .Where(item => item.ToolName.StartsWith("browser_", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(3, persistedBrowserReceipts.Count);
        Assert.All(
            persistedBrowserReceipts,
            receipt =>
            {
                Assert.Equal("provider-native.mcp", receipt.RuntimeToolProviderKey);
                Assert.Equal("Provider-native MCP", receipt.RuntimeToolProviderName);
            });
    }

    [Fact]
    public async Task GetExecutionRunDetailAsync_projects_playwright_browser_calls_from_execution_logs_when_session_state_is_absent()
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
                    SerializedSessionStateJson: null,
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
                    RequestSummary: "--yes, @playwright/mcp@0.0.78, --headless, --caps, vision",
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
        DateTimeOffset updatedAtUtc,
        string providerName = "OpenAI default",
        string model = "gpt-4.1")
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
                ProviderName: providerName,
                Model: model,
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

    private static ProviderUsageObservation CreateUsageObservation(
        Guid executionRunId,
        Guid agentId,
        DateTimeOffset createdAtUtc,
        string providerName,
        ProviderKind providerKind,
        string model,
        ProviderUsageObservationStatus status,
        int inputTokens,
        int outputTokens,
        decimal? costUsd)
    {
        return new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: createdAtUtc,
            ProviderName: providerName,
            ProviderKind: providerKind,
            Model: model,
            TransportKind: ProviderTransportKind.Responses,
            SourcePhase: ProviderUsageSourcePhases.AgentRuntime,
            UsageStatus: status,
            InputTokens: inputTokens,
            CachedInputTokens: 0,
            OutputTokens: outputTokens,
            ReasoningTokens: 0,
            TotalTokens: inputTokens + outputTokens,
            ToolCallCount: 0)
        {
            ExecutionRunId = executionRunId,
            AgentId = agentId,
            CalculatedCostUsd = costUsd
        };
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

    private static object CreateProviderNativeTextResult(string text)
    {
        return new Dictionary<string, string>
        {
            ["$type"] = "text",
            ["text"] = text
        };
    }
}
