using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class AgentChatSubmissionBrowserTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_chat_retains_original_submissions_across_typing_navigation_and_unconfirmed_outcomes(bool planning) {
        AgentChatSubmissionBrowserHost? host = null;
        IPlaywright? playwright = null;
        IBrowser? browser = null;
        IBrowserContext? context = null;
        IPage? page = null;
        Exception? primary = null;
        var cleanup = new List<Exception>();
        var errors = new ConcurrentQueue<string>();
        var evidence = new List<object>();
        var passed = false;
        try {
            host = await AgentChatSubmissionBrowserHost.StartAsync();
            playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            context = await browser.NewContextAsync(new() {
                ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1
            });
            page = await context.NewPageAsync();
            page.SetDefaultTimeout(45_000);
            page.PageError += (_, error) => errors.Enqueue(error);
            await page.GotoAsync($"{host.BaseUrl}/agents?tab=chat&agentId={host.AgentId:D}");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await OpenThreadAsync(host.OriginalThread);
            var draft = page.GetByTestId("chat-prompt-input");
            var send = page.GetByTestId(planning ? "agents-chat-plan-tasks" : "chat-send-button");
            var retained = page.GetByTestId("agents-chat-retained-submission");
            var restore = page.GetByTestId("agents-chat-restore-submission");

            const string firstOriginal = "First original prompt Ω — 東京";
            var first = await SendAsync(firstOriginal);
            const string laterDraft = "Later unsent text must survive the delayed refusal.";
            await FillDraftAsync(laterDraft);
            await host.Control.WaitForDraftAsync(host.AgentId, host.OriginalThread.Id, laterDraft);
            first.RefuseBeforeAdmission();
            await first.DrainAsync();
            await Assertions.Expect(retained).ToContainTextAsync("Prompt not sent");
            await Assertions.Expect(draft).ToHaveValueAsync(laterDraft);
            await Assertions.Expect(restore).ToBeDisabledAsync();
            AssertOriginal(first, firstOriginal, AgentChatSubmissionState.NotSent, 1);
            await ShotAsync("not-sent-later-typing");
            await draft.FillAsync("");
            await draft.PressAsync("Tab");
            await Assertions.Expect(restore).ToBeEnabledAsync();
            await restore.ClickAsync();
            await Assertions.Expect(draft).ToHaveValueAsync(firstOriginal);
            await Assertions.Expect(retained).ToHaveCountAsync(0);
            Assert.Equal(1, host.Control.SendCount);

            const string secondOriginal = "Second original prompt retained across panel disposal.";
            var second = await SendAsync(secondOriginal);
            var originalScope = host.Control.Retention;
            await ReopenChatAsync();
            await OpenThreadAsync(host.OtherThread);
            const string otherDraft = "The other thread owns this newer draft.";
            await FillDraftAsync(otherDraft);
            await host.Control.WaitForDraftAsync(host.AgentId, host.OtherThread.Id, otherDraft);
            second.RefuseBeforeAdmission();
            await second.DrainAsync();
            await Assertions.Expect(retained).ToContainTextAsync(secondOriginal);
            await Assertions.Expect(draft).ToHaveValueAsync(otherDraft);
            await Assertions.Expect(restore).ToBeDisabledAsync();
            Assert.Same(originalScope, host.Control.Retention);
            AssertOriginal(second, secondOriginal, AgentChatSubmissionState.NotSent, 2);
            await draft.FillAsync("");
            await draft.PressAsync("Tab");
            await Assertions.Expect(restore).ToBeDisabledAsync();
            await ShotAsync("retired-original-on-other-thread");
            await OpenThreadAsync(host.OriginalThread);
            await Assertions.Expect(restore).ToBeEnabledAsync();
            await restore.ClickAsync();
            await Assertions.Expect(draft).ToHaveValueAsync(secondOriginal);
            await Assertions.Expect(retained).ToHaveCountAsync(0);
            Assert.Equal(2, host.Control.SendCount);

            const string uncertainOriginal = "Third original prompt with an unconfirmed outcome.";
            var uncertain = await SendAsync(uncertainOriginal);
            const string newerUnknownDraft = "Keep editing while the original outcome needs review.";
            await FillDraftAsync(newerUnknownDraft);
            await host.Control.WaitForDraftAsync(host.AgentId, host.OriginalThread.Id, newerUnknownDraft);
            uncertain.LoseConfirmation();
            await uncertain.DrainAsync();
            await Assertions.Expect(retained).ToContainTextAsync("Submission outcome unconfirmed");
            await Assertions.Expect(draft).ToHaveValueAsync(newerUnknownDraft);
            await Assertions.Expect(restore).ToBeDisabledAsync();
            await Assertions.Expect(send).ToBeDisabledAsync();
            AssertOriginal(uncertain, uncertainOriginal, AgentChatSubmissionState.Unconfirmed, 3);
            await ShotAsync("unconfirmed-with-later-typing");
            await ReopenChatAsync();
            await OpenThreadAsync(host.OriginalThread);
            await Assertions.Expect(retained).ToContainTextAsync(uncertainOriginal);
            await Assertions.Expect(restore).ToBeDisabledAsync();
            await Assertions.Expect(send).ToBeDisabledAsync();
            await Assertions.Expect(draft).ToBeEditableAsync();
            await draft.FillAsync("A new local draft after returning to the original thread.");
            await draft.PressAsync("Tab");
            await Assertions.Expect(send).ToBeDisabledAsync();
            AssertOriginal(uncertain, uncertainOriginal, AgentChatSubmissionState.Unconfirmed, 3);
            await ShotAsync("unconfirmed-after-native-navigation");
            await host.AssertNoRunOrMessageAsync();
            Assert.Empty(errors);
            passed = true;

            async Task OpenThreadAsync(ChatSessionRecord thread) {
                var button = page.GetByRole(AriaRole.Button, new() { Name = "Open thread " + thread.Title, Exact = true });
                if (await button.GetAttributeAsync("aria-pressed") != "true") {
                    await button.ClickAsync();
                }
                await Assertions.Expect(button).ToHaveAttributeAsync("aria-pressed", "true");
                await Assertions.Expect(page.GetByTestId("chat-prompt-input")).ToBeEditableAsync();
            }
            async Task ReopenChatAsync() {
                var tabs = page.GetByTestId("agents-shell-tabs");
                await tabs.GetByText("Agents", new() { Exact = true }).ClickAsync();
                await Assertions.Expect(page.GetByTestId("chat-prompt-input")).ToHaveCountAsync(0);
                await tabs.GetByText("Chat", new() { Exact = true }).ClickAsync();
                await Assertions.Expect(page.GetByTestId("chat-prompt-input")).ToBeVisibleAsync();
                await page.GetByTestId("agent-switch-button").ClickAsync();
                var switcher = page.GetByTestId("agent-switch-dialog");
                await Assertions.Expect(switcher).ToBeVisibleAsync();
                const string agentName = "Retained submission browser Agent";
                await switcher.GetByTestId("agent-switch-search").FillAsync(agentName);
                var card = switcher.GetByTestId("agent-switch-card-shell").Filter(new() { HasTextString = agentName });
                await Assertions.Expect(card).ToHaveCountAsync(1);
                await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, $"switch-agent-{host.Control.SendCount}.png") });
                await card.GetByTestId("agent-switch-card").ClickAsync();
                await Assertions.Expect(switcher).ToHaveCountAsync(0);
                await Assertions.Expect(page.GetByTestId("agent-thread-selected-agent")).ToContainTextAsync(agentName);
            }
            async Task<AgentChatSubmissionBrowserHost.Attempt> SendAsync(string text) {
                await FillDraftAsync(text);
                await Assertions.Expect(send).ToBeEnabledAsync();
                var entered = host.Control.NextAsync();
                await send.ClickAsync();
                var attempt = await entered.WaitAsync(TimeSpan.FromSeconds(45));
                await attempt.WaitForConsumerCaptureAsync().WaitAsync(TimeSpan.FromSeconds(45));
                Assert.Equal(host.AgentId, attempt.Request.AgentId);
                Assert.Equal(host.OriginalThread.Id, attempt.Request.ChatSessionId);
                Assert.Equal(text, attempt.Request.Prompt);
                Assert.Equal(planning ? AgentPlanningOwner.ProjectTaskPlan : (AgentPlanningOwner?)null, attempt.Request.PlanningOwner);
                await Assertions.Expect(draft).ToHaveValueAsync("");
                return attempt;
            }
            async Task FillDraftAsync(string text) {
                await draft.FillAsync(text);
                await draft.PressAsync("Tab");
                await Assertions.Expect(draft).ToHaveValueAsync(text);
            }
            void AssertOriginal(AgentChatSubmissionBrowserHost.Attempt attempt, string text,
                AgentChatSubmissionState state, int sends) {
                var item = Assert.Single(host.Control.Retention.Snapshot());
                Assert.Equal(state, item.State);
                Assert.Equal(host.AgentId, item.AgentId);
                Assert.Equal(host.OriginalThread.Id, item.SessionId);
                Assert.Equal(text, item.Draft);
                Assert.Equal(attempt.Request.Prompt, item.RequestPrompt);
                Assert.Equal(attempt.Request.PlanningOwner, item.PlanningOwner);
                Assert.NotNull(item.Stream);
                Assert.Equal(attempt.Stream, item.Stream);
                Assert.Equal(item.ProfileGeneration, item.Stream.DatabaseProfileGeneration);
                Assert.Equal(sends, host.Control.SendCount);
                evidence.Add(new { item.Id, item.AgentId, item.SessionId, item.State, item.Draft, item.RequestPrompt,
                    item.ProfileGeneration, item.Stream, item.PlanningOwner, Sends = sends });
            }
            async Task ShotAsync(string name) {
                Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth"));
                await retained.ScrollIntoViewIfNeededAsync();
                await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, name + ".png"), FullPage = false });
            }
        } catch (Exception failure) {
            primary = failure;
        } finally {
            if (host is not null) {
                await CleanupAsync(host.Control.ReleaseAndDrainAsync);
                if (primary is not null && page is not null) {
                    await CleanupAsync(() => page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "failure.png"), Timeout = 5_000 }));
                }
                await CleanupAsync(() => File.WriteAllTextAsync(Path.Combine(host.Evidence, "evidence.json"), JsonSerializer.Serialize(new {
                    Passed = passed, Planning = planning, Viewport = "1920x1080@1", ControlledBoundary = "IAgentChatExecutionOrchestrator",
                    NativePanelAndRetention = true, RuntimeAdmissionProof = false, FullReloadPersistenceClaimed = false,
                    SendCount = host.Control.SendCount, Observations = evidence, Errors = errors.ToArray()
                })));
            }
            await CleanupAsync(() => context?.DisposeAsync().AsTask() ?? Task.CompletedTask);
            await CleanupAsync(() => browser?.DisposeAsync().AsTask() ?? Task.CompletedTask);
            await CleanupAsync(() => {
                playwright?.Dispose();
                return Task.CompletedTask;
            });
            await CleanupAsync(() => host?.DisposeAsync().AsTask() ?? Task.CompletedTask);
        }
        if (cleanup.Count != 0) {
            throw new AggregateException("Native chat browser proof or cleanup failed.", primary is null ? cleanup : [primary, .. cleanup]);
        }
        if (primary is not null) {
            ExceptionDispatchInfo.Capture(primary).Throw();
        }
        async Task CleanupAsync(Func<Task> release) {
            try {
                await release();
            } catch (Exception failure) {
                cleanup.Add(failure);
            }
        }
    }
}
