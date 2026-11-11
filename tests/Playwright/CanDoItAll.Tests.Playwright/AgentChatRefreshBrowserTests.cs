using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static CanDoItAll.Tests.Playwright.Smoke.AgentUiJourneySupport;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class AgentChatRefreshBrowserTests {
    [Fact]
    public async Task Running_floating_chat_survives_soft_and_hard_reload_and_reopens_its_original_view() {
        const string reply = "The original agent turn finished after both browser refreshes.";
        await using var wire = await AgentResponseFixture.StartAsync(string.Empty);
        wire.Steps = [_ => new([], reply)];
        wire.ReplyHoldTimeout = TimeSpan.FromMinutes(3);
        var circuits = new RefreshCircuitTracker();
        await using var host = await ProcessNativeBrowserHost.StartAsync(wire.BaseUrl, configureServices: services => {
            services.Configure<CircuitOptions>(options => options.DisconnectedCircuitRetentionPeriod = TimeSpan.Zero);
            services.AddSingleton(circuits);
            services.AddScoped<CircuitHandler, RefreshCircuitHandler>();
        });
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() {
            ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1
        });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(45_000);
        var errors = new ConcurrentQueue<string>();
        page.PageError += (_, error) => errors.Enqueue(error);
        var originalRoute = $"/projects/{host.ProjectId:D}/structure?tab=gantt";
        var observations = new List<object>();
        var passed = false;
        try {
            await page.GotoAsync(host.BaseUrl + originalRoute);
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            var circuit = await circuits.NextAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Tab, new() { Name = "Gantt", Exact = true }))
                .ToHaveAttributeAsync("aria-selected", "true");
            var chat = await OpenFloatingChatFromCatalogAsync(page, "project-structure-agents-toggle", host.AgentId,
                Path.Combine(host.Evidence, "agent-chat-refresh-catalog-failure.png"));
            await SendAsync(chat, "Keep working on this project while I refresh the browser.");
            await wire.PrefixFlushed.Task.WaitAsync(TimeSpan.FromSeconds(60));
            var original = await ReadOnlyRunAsync();
            Assert.NotNull(original.ChatSessionId);
            Assert.Equal(AgentChatTrustedSourceKinds.ProjectStructure, original.SourceKind);
            Assert.Equal(host.ProjectId.ToString("D"), original.SourceId);
            Assert.Null(original.CompletedAtUtc);

            foreach (var hardReload in new[] { false, true }) {
                await page.EvaluateAsync("() => window.agentRefreshDocument = true");
                if (hardReload) {
                    var devTools = await context.NewCDPSessionAsync(page);
                    try {
                        await devTools.SendAsync("Page.reload", new Dictionary<string, object> { ["ignoreCache"] = true });
                        await page.WaitForFunctionAsync("() => window.agentRefreshDocument !== true");
                    } finally {
                        await devTools.DetachAsync();
                    }
                } else {
                    await page.ReloadAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded });
                }
                await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
                var replacement = await circuits.NextAsync();
                await circuit.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.NotEqual(circuit.Id, replacement.Id);
                Assert.False(wire.HeldReplyAborted.Task.IsCompleted);
                Assert.Equal(1, wire.Requests);
                var running = await ReadOnlyRunAsync();
                Assert.Equal(original.Id, running.Id);
                Assert.Null(running.CompletedAtUtc);
                await ReopenActiveAsync();
                await Assertions.Expect(chat.GetByTestId("chat-send-button")).ToBeDisabledAsync();
                await page.ScreenshotAsync(new() {
                    Path = Path.Combine(host.Evidence, hardReload ? "agent-chat-refresh-hard.png" : "agent-chat-refresh-soft.png")
                });
                observations.Add(new { HardReload = hardReload, OldCircuitDisposed = circuit.Disposed.Task.IsCompleted,
                    SameRun = running.Id == original.Id, Requests = wire.Requests });
                circuit = replacement;
            }

            await page.GotoAsync(host.BaseUrl + "/agents");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            var away = await circuits.NextAsync();
            await circuit.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.NotEqual(circuit.Id, away.Id);
            await ReopenActiveAsync();
            await Assertions.Expect(page).ToHaveURLAsync(host.BaseUrl + originalRoute);
            await Assertions.Expect(page.GetByRole(AriaRole.Tab, new() { Name = "Gantt", Exact = true }))
                .ToHaveAttributeAsync("aria-selected", "true");
            Assert.False(wire.HeldReplyAborted.Task.IsCompleted);
            wire.ReleaseReply.TrySetResult();
            await Assertions.Expect(chat.GetByTestId("agent-execution-activity-phase"))
                .ToHaveTextAsync("Completed", new() { Timeout = 60_000 });
            await Assertions.Expect(chat).ToContainTextAsync(reply);
            var completed = await ReadOnlyRunAsync();
            Assert.Equal(original.Id, completed.Id);
            Assert.Equal(original.ChatSessionId, completed.ChatSessionId);
            Assert.Equal(ExecutionState.Completed, completed.State);
            Assert.Equal(RunOutcome.Succeeded, completed.Outcome);
            Assert.Equal(1, wire.Requests);
            Assert.Empty(errors);
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "agent-chat-refresh-completed.png") });
            passed = true;

            async Task ReopenActiveAsync() {
                var catalog = page.GetByTestId("floating-agent-catalog-window");
                if (await catalog.CountAsync() == 0) {
                    await page.GetByTestId("shell-agent-chats-action").ClickAsync();
                }
                await catalog.GetByRole(AriaRole.Tab, new() { Name = "Active", Exact = true }).ClickAsync();
                var item = catalog.Locator("[data-testid^='floating-agent-chat-active-list-agent-chat:']")
                    .Filter(new() { HasTextString = "PC1 process manager" });
                await Assertions.Expect(item).ToHaveCountAsync(1);
                await item.GetByRole(AriaRole.Button, new() { Name = "Open", Exact = true }).ClickAsync();
                await Assertions.Expect(chat).ToBeVisibleAsync();
                await Assertions.Expect(page).ToHaveURLAsync(host.BaseUrl + originalRoute);
            }
        } catch {
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "agent-chat-refresh-failure.png"), Timeout = 5_000 });
            throw;
        } finally {
            wire.ReleaseReply.TrySetResult();
            await File.WriteAllTextAsync(Path.Combine(host.Evidence, "agent-chat-refresh-evidence.json"), JsonSerializer.Serialize(new {
                Passed = passed, Viewport = "1920x1080@1", ControlledBoundary = "loopback Responses provider",
                NativeExecution = true, OriginalRoute = originalRoute, Observations = observations,
                Requests = wire.Requests, Errors = errors.ToArray()
            }));
        }

        Task<ExecutionRunRecord> ReadOnlyRunAsync() => host.ReadAsync(async services => Assert.Single(await services
            .GetRequiredService<IAgentFrameworkWorkspaceService>().ListExecutionRunsAsync(new ExecutionRunQuery(AgentId: host.AgentId))));
    }

    private sealed class RefreshCircuitTracker {
        private readonly Channel<ObservedCircuit> opened = Channel.CreateUnbounded<ObservedCircuit>();

        internal ObservedCircuit Open(string id) {
            var circuit = new ObservedCircuit(id);
            Assert.True(opened.Writer.TryWrite(circuit));
            return circuit;
        }

        internal async Task<ObservedCircuit> NextAsync() => await opened.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(45));
    }

    private sealed record ObservedCircuit(string Id) {
        internal TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class RefreshCircuitHandler(RefreshCircuitTracker tracker) : CircuitHandler, IAsyncDisposable {
        private ObservedCircuit? circuit;

        public override Task OnCircuitOpenedAsync(Circuit opened, CancellationToken cancellationToken) {
            circuit = tracker.Open(opened.Id);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() {
            circuit?.Disposed.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }
}
