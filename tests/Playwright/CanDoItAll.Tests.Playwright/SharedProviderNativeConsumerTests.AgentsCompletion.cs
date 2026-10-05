using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using Microsoft.Playwright;
using static CanDoItAll.Tests.Playwright.Smoke.AgentUiJourneySupport;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class SharedProviderNativeConsumerTests {
    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Final_image_AC1_enabled_completed_schedule_survives_two_restarts_and_future_fire() {
        await using var fixture = await SharedProviderConsumerFixture.StartAsync();
        var completed = await ScheduleAsync();
        var run = await AwaitWorkflowStateAsync(fixture, completed.Definition, WorkflowRunState.Completed);
        var beforeCaptures = await fixture.ReadCapturesAsync();
        var initialDetail = await fixture.GetAsync($"api/workflows/runs/{run.GetProperty("runId").GetGuid():D}/detail");
        for (var restart = 1; restart <= 2; restart++) {
            await fixture.RestartOwnedAppsAsync($"ac1-scheduler-restart-{restart}");
            await AwaitOwnedClientAsync(fixture);
            await AssertUnchangedAsync();
            await fixture.EvidenceAsync($"ac1-scheduler-after-{restart}", new {
                completed.PlanId, WorkflowId = completed.Definition.Id.Value, VersionId = completed.Definition.VersionId.Value,
                Run = run, Detail = initialDetail, Enabled = true, NativeRunCount = 1
            });
        }
        var future = await ScheduleAsync();
        var futureRun = await AwaitWorkflowStateAsync(fixture, future.Definition, WorkflowRunState.Completed);
        await AssertUnchangedAsync();
        Assert.Equal(beforeCaptures.GetRawText(), (await fixture.ReadCapturesAsync()).GetRawText());
        await fixture.EvidenceAsync("ac1-scheduler-final", new {
            CompletedPlan = completed.PlanId, CompletedWorkflow = completed.Definition.Id.Value, CompletedRun = run,
            FuturePlan = future.PlanId, FutureWorkflow = future.Definition.Id.Value, FutureRun = futureRun,
            CompletedEnabled = true, Restarts = 2, DuplicateRuns = 0, ProviderDispatches = 0
        });
        await fixture.ScreenshotAsync("ac1-scheduler-final-1920");

        async Task<(WorkflowDefinition Definition, Guid PlanId)> ScheduleAsync() {
            var draft = await AuthorNativeDefinitionAsync(fixture, human: false);
            var published = await fixture.PostAsync<WorkflowDefinition>(
                $"api/workflows/definitions/{draft.Id.Value:D}/publish?expectedVersionId={draft.VersionId.Value:D}", new { });
            var page = fixture.Page;
            await fixture.NavigateAsync("/scheduler");
            await page.GetByTestId("scheduler-tab-new").ClickAsync();
            await page.GetByTestId("scheduler-name").FillAsync(published.Name);
            await page.GetByTestId("scheduler-target-open").ClickAsync();
            await page.GetByTestId("scheduler-target-search").FillAsync(published.Name);
            await page.GetByTestId("scheduler-target-search").PressAsync("Tab");
            await page.GetByTestId("scheduler-target-card").Filter(new() { HasTextString = published.Name }).ClickAsync();
            await Assertions.Expect(page.GetByTestId("scheduler-target-selected")).ToContainTextAsync(published.Name);
            await Assertions.Expect(page.GetByTestId("scheduler-typed-inputs").GetByRole(AriaRole.Heading,
                new() { Name = published.Name, Exact = true })).ToBeVisibleAsync();
            var (_, cron) = await SetFiniteScheduleInputAsync(page, "AC1 retained enabled schedule");
            await page.GetByTestId("scheduler-save").ClickAsync();
            await Assertions.Expect(page.GetByTestId("scheduler-draft-receipt")).ToContainTextAsync("Committed");
            var receipt = await page.GetByTestId("scheduler-draft-receipt").InnerTextAsync();
            var match = Regex.Match(receipt, "Plan: ([0-9a-fA-F-]{36})");
            Assert.True(match.Success, receipt);
            await page.GetByTestId("scheduler-tab-schedules").ClickAsync();
            await page.GetByTestId("scheduler-plan-card").Filter(new() { HasTextString = published.Name })
                .GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByTestId("scheduler-edit-cron")).ToHaveValueAsync(cron);
            await Assertions.Expect(page.GetByTestId("scheduler-edit-timezone")).ToHaveValueAsync("UTC");
            await Assertions.Expect(page.GetByTestId("scheduler-input-message")).ToHaveValueAsync("AC1 retained enabled schedule");
            await page.GetByTestId("scheduler-edit-cancel").ClickAsync();
            await fixture.EvidenceAsync("ac1-scheduler-plan-" + match.Groups[1].Value, new {
                PlanId = match.Groups[1].Value, WorkflowId = published.Id.Value, VersionId = published.VersionId.Value, Cron = cron,
                TimeZone = "UTC", Enabled = true, Input = "AC1 retained enabled schedule", Receipt = receipt
            });
            return (published, Guid.Parse(match.Groups[1].Value));
        }

        async Task AssertUnchangedAsync() {
            var current = await AwaitWorkflowStateAsync(fixture, completed.Definition, WorkflowRunState.Completed);
            Assert.Equal(run.GetRawText(), current.GetRawText());
            var detail = await fixture.GetAsync($"api/workflows/runs/{run.GetProperty("runId").GetGuid():D}/detail");
            Assert.Equal(initialDetail.GetRawText(), detail.GetRawText());
            await fixture.NavigateAsync("/scheduler");
            await fixture.Page.GetByTestId("scheduler-tab-schedules").ClickAsync();
            var card = fixture.Page.GetByTestId("scheduler-plan-card").Filter(new() { HasTextString = completed.Definition.Name });
            await Assertions.Expect(card.GetByRole(AriaRole.Button, new() { Name = "Pause", Exact = true })).ToBeVisibleAsync();
            await fixture.Page.GetByTestId("scheduler-tab-history").ClickAsync();
            await fixture.Page.GetByTestId("scheduler-history-search").FillAsync(completed.Definition.Name);
            await fixture.Page.GetByTestId("scheduler-history-search").PressAsync("Tab");
            await fixture.Page.GetByTestId("scheduler-history").GetByRole(AriaRole.Button, new() { Name = "Search", Exact = true }).ClickAsync();
            await Assertions.Expect(fixture.Page.GetByTestId("scheduler-history").GetByText("1 run(s)", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(fixture.Page.GetByTestId("scheduler-history")).ToContainTextAsync(completed.Definition.Name);
        }
    }

    private static async Task<(DateTimeOffset FireAt, string Cron)> SetFiniteScheduleInputAsync(IPage page, string message) {
        await page.GetByTestId("scheduler-input-message").FillAsync(message);
        await Assertions.Expect(page.GetByTestId("scheduler-input-json")).ToHaveValueAsync(new Regex(Regex.Escape(message)));
        await page.GetByTestId("scheduler-timezone").FillAsync("UTC");
        await Assertions.Expect(page.GetByTestId("scheduler-cron-preview")).ToContainTextAsync("Schedule time zone: UTC.");
        var fireAt = DateTimeOffset.UtcNow.AddSeconds(45);
        var cron = $"{fireAt.Second} {fireAt.Minute} {fireAt.Hour} {fireAt.Day} {fireAt.Month} ? {fireAt.Year}";
        await page.GetByTestId("scheduler-cron").FillAsync(cron);
        var seconds = fireAt.Second == 0 ? string.Empty : $" at second {fireAt.Second}";
        await Assertions.Expect(page.GetByTestId("scheduler-cron-preview")).ToContainTextAsync(
            $"At {fireAt:HH:mm}{seconds} on day {fireAt.Day} in month field '{fireAt.Month}' (UTC).");
        return (fireAt, cron);
    }

    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Final_image_AC1_all_shell_tabs_and_native_settings_reload_preserve_ownership() {
        await using var fixture = await SharedProviderConsumerFixture.StartAsync();
        var page = fixture.Page;
        var tabs = new[] {
            ("Overview", "overview"), ("Agents", "agents"), ("Simple Chats", "simple-chats"), ("Providers", "providers"),
            ("Request history", "request-history"), ("Voice", "voice"), ("Floating chat", "floating-chat"),
            ("Chat", "chat"), ("Capabilities", "capabilities"), ("Governance", "governance"), ("Diagnostics", "diagnostics")
        };
        await fixture.NavigateAsync("/agents?tab=overview");
        foreach (var (label, token) in tabs) {
            await page.GetByTestId("agents-shell-tabs").GetByText(label, new() { Exact = true }).ClickAsync();
            await Assertions.Expect(page).ToHaveURLAsync(new Regex("[?&]tab=" + token + "(?:&|$)"));
            await Assertions.Expect(page.GetByTestId("agents-shell-tabs")).ToBeVisibleAsync();
        }
        await fixture.NavigateAsync("/agents?tab=voice");
        await Assertions.Expect(page.GetByTestId("agents-voice-save")).ToBeEnabledAsync();
        var prompt = "AC1 non-sensitive settings " + Guid.NewGuid().ToString("N");
        var priorPrompt = await page.GetByTestId("agents-voice-stt-prompt").InputValueAsync();
        await fixture.EvidenceAsync("ac1-voice-before-save", new { PriorPrompt = priorPrompt, SubmittedPrompt = prompt });
        await page.GetByTestId("agents-voice-stt-prompt").FillAsync(prompt);
        await page.GetByTestId("agents-voice-stt-prompt").PressAsync("Tab");
        await page.GetByTestId("agents-voice-save").ClickAsync();
        await page.GetByText("Voice settings saved", new() { Exact = true }).WaitForAsync();
        await fixture.NavigateAsync("/agents?tab=floating-chat");
        var retention = page.GetByTestId("floating-agent-chat-retention");
        await Assertions.Expect(retention).ToBeEnabledAsync();
        var priorRetention = await retention.InputValueAsync();
        var changedRetention = priorRetention == "37" ? "38" : "37";
        await fixture.EvidenceAsync("ac1-floating-before-save", new { PriorRetention = priorRetention, SubmittedRetention = changedRetention });
        await retention.FillAsync(changedRetention);
        await retention.PressAsync("Tab");
        await page.GetByTestId("floating-agent-chat-settings-save").ClickAsync();
        await page.GetByText("Floating chat settings saved", new() { Exact = true }).WaitForAsync();
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page, page.Url, _ => page.ReloadAsync());
        await Assertions.Expect(retention).ToHaveValueAsync(changedRetention);
        await retention.FillAsync(priorRetention);
        await retention.PressAsync("Tab");
        await page.GetByTestId("floating-agent-chat-settings-save").ClickAsync();
        await page.GetByText("Floating chat settings saved", new() { Exact = true }).WaitForAsync();
        await fixture.NavigateAsync("/agents?tab=voice");
        await Assertions.Expect(page.GetByTestId("agents-voice-stt-prompt")).ToHaveValueAsync(prompt);
        await page.GetByTestId("agents-voice-stt-prompt").FillAsync(priorPrompt);
        await page.GetByTestId("agents-voice-stt-prompt").PressAsync("Tab");
        await page.GetByTestId("agents-voice-save").ClickAsync();
        await page.GetByText("Voice settings saved", new() { Exact = true }).WaitForAsync();
        await fixture.NavigateAsync("/agents?tab=voice");
        await Assertions.Expect(page.GetByTestId("agents-voice-stt-prompt")).ToHaveValueAsync(priorPrompt);
        await fixture.NavigateAsync("/agents?tab=floating-chat");
        await Assertions.Expect(retention).ToHaveValueAsync(priorRetention);
        await fixture.ScreenshotAsync("ac1-native-settings-1920");
        await fixture.EvidenceAsync("ac1-native-shell-settings", new { Tabs = tabs.Select(tab => tab.Item2), VoiceReloaded = true,
            FloatingReloaded = true, OriginalValuesRestored = true, SampleRequests = 0 });
    }

    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Final_image_AC1_standard_chat_runtime_cancel_reopen_and_usage_keep_exact_run() {
        await using var fixture = await SharedProviderConsumerFixture.StartAsync();
        var profile = await ImportedResponsesAsync(fixture);
        var marker = "AC1_STANDARD_" + Guid.NewGuid().ToString("N");
        var projectName = "AC1 chat " + marker;
        var projectId = await fixture.PostAsync<Guid>("api/projects", new ProjectEditorModel { Name = projectName });
        var alternate = profile.ModelCatalog.First(model => model.Id != profile.DefaultModel && profile.SuggestedModels.Contains(model.Id));
        var agentId = await CreateFileAgentAsync(fixture, profile, alternate.Id, marker, projectId, projectName);
        await fixture.NavigateAsync($"/agents?tab=chat&agentId={agentId:D}");
        var page = fixture.Page;
        var chat = page.GetByTestId("agents-chat-panel");
        await chat.GetByRole(AriaRole.Button, new() { Name = "New thread", Exact = true }).ClickAsync();
        await Assertions.Expect(chat.GetByText("This thread is ready for the first prompt", new() { Exact = true })).ToBeVisibleAsync();
        var prompt = "Save this exact original prompt " + marker;
        var answer = "Stored original answer " + marker;
        await fixture.ScriptAsync(alternate.DisplayName, marker, TextStep(answer));
        await SendAsync(chat, prompt);
        await Assertions.Expect(chat).ToContainTextAsync(answer, new() { Timeout = 60_000 });
        await Assertions.Expect(chat.GetByTestId("agent-execution-activity-phase")).ToHaveTextAsync("Completed");
        var workspace = await fixture.GetAsync($"api/agents/{agentId:D}/chat-workspace");
        var originalSession = workspace.GetProperty("selectedSession");
        var run = workspace.GetProperty("selectedRun");
        Assert.Equal(alternate.Id, run.GetProperty("model").GetString());
        Assert.Contains(prompt, originalSession.GetRawText(), StringComparison.Ordinal);
        Assert.Contains(answer, originalSession.GetRawText(), StringComparison.Ordinal);
        await fixture.AssertScriptCompleteAsync(1);
        await chat.GetByTestId("agents-chat-open-runtime-details").ClickAsync();
        var runtime = page.GetByTestId("agent-runtime-details-dialog-body");
        await Assertions.Expect(runtime).ToContainTextAsync("Completed");
        await fixture.ScreenshotAsync("ac1-native-runtime-1920");
        await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await chat.GetByTestId("chat-execution-summary").ClickAsync();
        var log = page.GetByTestId("agent-execution-log-dialog-body");
        await Assertions.Expect(log.GetByTestId("agent-execution-log-dialog-entry").First).ToHaveAttributeAsync("data-run-id", run.GetProperty("id").GetGuid().ToString());
        await fixture.ScreenshotAsync("ac1-native-log-1920");
        await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        var targetPath = "ac1/" + marker + "/never-written.txt";
        await fixture.ScriptAsync(alternate.DisplayName, marker,
            ToolsStep(Call(ToolContractCatalog.WorkspaceWriteFile, new { path = targetPath, content = "Must remain uncommitted", overwrite = false })));
        await SendAsync(chat, "Propose a new file and wait for approval.");
        await Assertions.Expect(chat.Locator("[data-testid^='chat-approval-approve-']")).ToHaveCountAsync(1, new() { Timeout = 60_000 });
        var pending = await fixture.ReadAgentAsync(agentId);
        Assert.All(pending.GetProperty("proposals").EnumerateArray(), item => Assert.Equal((int)ExecutionApprovalStatus.Pending, item.GetProperty("approvalStatus").GetInt32()));
        await chat.GetByTestId("agents-chat-cancel-pending-run").ClickAsync();
        await Assertions.Expect(chat.GetByTestId("agent-execution-activity-phase")).ToHaveTextAsync("Cancelled", new() { Timeout = 60_000 });
        await fixture.AssertScriptCompleteAsync(1);
        var cancelled = await fixture.GetAsync($"api/agents/{agentId:D}/execution-runs/{pending.GetProperty("runId").GetGuid():D}");
        await fixture.NavigateAsync($"/agents?tab=chat&agentId={agentId:D}");
        await page.GetByRole(AriaRole.Button, new() { Name = "Open thread " + originalSession.GetProperty("title").GetString(), Exact = true }).ClickAsync();
        await Assertions.Expect(chat).ToContainTextAsync(answer);
        await fixture.NavigateAsync("/agents?tab=overview&usageScope=both");
        await Assertions.Expect(page.GetByTestId("agents-overview-window")).ToContainTextAsync("≤ usage <");
        var window = await page.GetByTestId("agents-overview-window").InnerTextAsync();
        foreach (var detail in new[] { "agent", "provider", "model" }) {
            await page.GetByTestId("agents-overview-open-" + detail + "-usage").ClickAsync();
            var dialog = page.GetByRole(AriaRole.Dialog);
            await Assertions.Expect(dialog.Locator("[data-testid$='usage-dialog-grid']")).ToBeVisibleAsync();
            await fixture.ScreenshotAsync("ac1-native-usage-" + detail + "-1920");
            await dialog.GetByTestId("usage-detail-close").ClickAsync();
            await Assertions.Expect(page.GetByTestId("agents-overview-window")).ToHaveTextAsync(window);
        }
        await fixture.EvidenceAsync("ac1-standard-runtime-cancel-usage", new {
            AgentId = agentId, ProviderId = profile.Id, Model = alternate.Id, SessionId = originalSession.GetProperty("id"),
            OriginalRun = run, CancelledRun = cancelled, PromptSha256 = SharedProviderConsumerFixture.Hash(prompt),
            AnswerSha256 = SharedProviderConsumerFixture.Hash(answer), UsageWindow = window, PendingRunId = pending.GetProperty("runId")
        });
    }

    private static async Task AwaitOwnedClientAsync(SharedProviderConsumerFixture fixture) {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        while (true) {
            try {
                using var response = await fixture.Api.GetAsync("api/agents", deadline.Token);
                if (response.StatusCode == HttpStatusCode.OK) {
                    return;
                }
            } catch (HttpRequestException) {
            }
            await Task.Delay(250, deadline.Token);
        }
    }
}
