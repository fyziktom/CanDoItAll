using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class CollaborationSandboxBrowserTests {
    [Fact]
    public async Task Backend_free_sandbox_exercises_real_forms_scenarios_assets_and_delayed_actions() {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
        listener.Stop();
        var start = new ProcessStartInfo("dotnet", PlaywrightTestHostPaths.BuildDotnetRunArguments("src/Sandboxes/CanDoItAll.Collaboration.UiSandbox", url)) {
            WorkingDirectory = PlaywrightTestHostPaths.RepositoryRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment.Remove("CANDOITALL_TESTS_POSTGRES_CONNECTION");
        var logs = new ConcurrentQueue<string>();
        using var server = Process.Start(start)!;
        server.OutputDataReceived += (_, args) => {
            if (args.Data is not null) {
                logs.Enqueue(args.Data);
            }
        };
        server.ErrorDataReceived += (_, args) => {
            if (args.Data is not null) {
                logs.Enqueue(args.Data);
            }
        };
        server.BeginOutputReadLine();
        server.BeginErrorReadLine();
        try {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using var ready = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (true) {
                ready.Token.ThrowIfCancellationRequested();
                if (server.HasExited) {
                    throw new InvalidOperationException(string.Join(Environment.NewLine, logs));
                }
                try {
                    using var response = await http.GetAsync(url, ready.Token);
                    if (response.IsSuccessStatusCode) {
                        break;
                    }
                } catch (HttpRequestException) {
                } catch (OperationCanceledException) when (!ready.IsCancellationRequested) {
                }
                await Task.Delay(100, ready.Token);
            }
            using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
            var errors = new List<string>();
            page.PageError += (_, message) => errors.Add(message);
            page.Console += (_, message) => {
                if (message.Type == "error") {
                    errors.Add(message.Text);
                }
            };
            await page.GotoAsync(url);
            await CollaborationBrowserTests.ReadyAsync(page);
            await Assertions.Expect(page.GetByTestId("collaboration-thread-title")).ToBeInViewportAsync();
            Assert.True(await page.EvaluateAsync<bool>("() => document.documentElement.scrollHeight <= innerHeight && document.querySelector('[data-testid=collaboration-workspace]').getBoundingClientRect().top < 160"));
            var artifacts = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "collaboration");
            Directory.CreateDirectory(artifacts);
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, "sandbox-representative.png") });
            await SelectScenarioAsync(page, "InvalidDraft");
            await page.GetByTestId("collaboration-thread-create").ClickAsync();
            await Assertions.Expect(page.Locator("[data-testid=collaboration-create-form] .validation-message").First).ToBeVisibleAsync();
            await SelectScenarioAsync(page, "AdmittedSave");
            await page.GetByTestId("collaboration-thread-create").ClickAsync();
            await Assertions.Expect(page.GetByTestId("collaboration-thread-subject")).ToBeDisabledAsync();
            await Assertions.Expect(page.GetByTestId("collaboration-reply-message")).ToBeEnabledAsync();
            await page.GetByTestId("collaboration-complete-pending").ClickAsync();
            await Assertions.Expect(page.GetByTestId("collaboration-thread-title")).ToHaveTextAsync("Draft collaboration item");
            await SelectScenarioAsync(page, "OwnerRefusal");
            await page.GetByTestId("collaboration-thread-create").ClickAsync();
            await Assertions.Expect(page.GetByTestId("collaboration-create-message")).ToContainTextAsync("owner refused");
            await SelectScenarioAsync(page, "SavedWithRefreshWarning");
            await page.GetByTestId("collaboration-thread-create").ClickAsync();
            await Assertions.Expect(page.GetByTestId("collaboration-read-error")).ToContainTextAsync("Saved successfully");
            await page.GetByTestId("collaboration-refresh").ClickAsync();
            await CollaborationBrowserTests.ReadyAsync(page);
            await SelectScenarioAsync(page, "DelayedTarget");
            await page.GetByTestId("collaboration-inbox-item").Filter(new() { HasText = "Approval needed" }).GetByRole(AriaRole.Button).ClickAsync();
            await Assertions.Expect(page.GetByTestId("collaboration-loading")).ToBeVisibleAsync();
            await page.GetByTestId("collaboration-complete-pending").ClickAsync();
            await CollaborationBrowserTests.ReadyAsync(page);
            await Assertions.Expect(page.GetByTestId("collaboration-thread-title")).ToHaveTextAsync("Approval needed");
            await page.GetByRole(AriaRole.Tab, new() { Name = "Escalations", Exact = false }).ClickAsync();
            await Assertions.Expect(page.GetByTestId("collaboration-escalation-item")).ToHaveCountAsync(1);
            await page.GetByTestId("collaboration-filter-unread").ClickAsync();
            await Assertions.Expect(page.GetByTestId("collaboration-escalation-item")).ToHaveCountAsync(1);
            await page.GetByRole(AriaRole.Tab, new() { Name = "Threads", Exact = false }).ClickAsync();
            await Assertions.Expect(page.GetByTestId("collaboration-thread-item")).ToHaveCountAsync(3);
            await SelectScenarioAsync(page, "LongTranscript");
            await Assertions.Expect(page.GetByTestId("collaboration-thread-message-item")).ToHaveCountAsync(60);
            await page.GetByTestId("collaboration-workspace").EvaluateAsync("element => element.scrollTop = 0");
            await Assertions.Expect(page.GetByTestId("collaboration-thread-title")).ToBeInViewportAsync();
            await page.GetByTestId("collaboration-thread-message-item").Last.ScrollIntoViewIfNeededAsync();
            await Assertions.Expect(page.GetByTestId("collaboration-thread-message-item").Last).ToBeInViewportAsync();
            await Assertions.Expect(page.GetByTestId("collaboration-scenario")).ToBeInViewportAsync();
            Assert.False(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth"));
            await page.GetByTestId("collaboration-workspace").EvaluateAsync("element => element.scrollTop = 0");
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifacts, "sandbox-long-transcript.png") });
            Assert.Empty(errors);
            Assert.DoesNotContain(logs, line => line.Contains("Unhandled exception", StringComparison.OrdinalIgnoreCase) || line.StartsWith("fail:", StringComparison.Ordinal));
        } finally {
            if (!server.HasExited) {
                server.Kill(entireProcessTree: true);
                await server.WaitForExitAsync();
            }
        }
    }

    private static async Task SelectScenarioAsync(IPage page, string scenario) {
        await page.GetByTestId("collaboration-scenario").SelectOptionAsync(scenario);
        await Assertions.Expect(page.GetByTestId("collaboration-sandbox")).ToHaveAttributeAsync("data-scenario", scenario);
    }
}
