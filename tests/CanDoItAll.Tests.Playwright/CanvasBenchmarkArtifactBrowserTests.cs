using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AppSmokeTests
{
    [Fact]
    [Trait("Surface", "CanvasBenchmark")]
    [Trait("Artifacts", "Required")]
    public async Task Canvas_benchmark_artifacts_capture_results_and_decision()
    {
        var repoRoot = GetRepoRoot();
        var i25Root = Path.Combine(repoRoot, "artifacts", "screenshots", "i25");
        ResetDirectory(i25Root);
        var sandboxBaseUrl = "http://127.0.0.1:5191";

        await using var sandboxRuntime = await SandboxRuntime.StartAsync(repoRoot, sandboxBaseUrl);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1900,
                Height = 1200
            }
        });

        var page = await context.NewPageAsync();
        var response = await page.GotoAsync($"{sandboxBaseUrl}/groups/canvas/benchmark");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /groups/canvas/benchmark to return 2xx, got {(int)response.Status}.");

        var runButton = page.GetByTestId("canvas-benchmark-run");
        await runButton.WaitForAsync();
        await page.GetByTestId("canvas-benchmark-retained-preview").WaitForAsync();
        await CaptureLocatorAsync(page.Locator("main"), Path.Combine(i25Root, "01-primary-state.png"));

        await runButton.ClickAsync();
        await page.WaitForFunctionAsync(
            @"() => {
                const result = window.__canvasBenchmarkLastRun;
                return !!result &&
                    Array.isArray(result.tiers) &&
                    result.tiers.length >= 3 &&
                    typeof result.recommendation === 'string' &&
                    result.recommendation.length > 0 &&
                    typeof result.summary === 'string' &&
                    result.summary.length > 0;
            }",
            null,
            new()
            {
                Timeout = 60_000
            });

        var results = page.GetByTestId("canvas-benchmark-results");
        var decision = page.GetByTestId("canvas-benchmark-decision");
        await results.WaitForAsync();
        await decision.WaitForAsync();
        var resultsText = await results.TextContentAsync() ?? string.Empty;
        Assert.Contains("Retained Avg", resultsText, StringComparison.Ordinal);
        Assert.Contains("Canvas Avg", resultsText, StringComparison.Ordinal);
        Assert.Contains("Retained DOM", resultsText, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(await decision.TextContentAsync()));

        await CaptureLocatorAsync(results, Path.Combine(i25Root, "02-secondary-state.png"));
        await CaptureLocatorAsync(decision, Path.Combine(i25Root, "03-interaction-result.png"));

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private sealed class SandboxRuntime : IAsyncDisposable
    {
        private readonly ConcurrentQueue<string> logs = new();
        private readonly Process process;
        private readonly Task stdoutPump;
        private readonly Task stderrPump;

        private SandboxRuntime(Process process, Task stdoutPump, Task stderrPump)
        {
            this.process = process;
            this.stdoutPump = stdoutPump;
            this.stderrPump = stderrPump;
        }

        public static async Task<SandboxRuntime> StartAsync(string repoRoot, string baseUrl)
        {
            var processStartInfo = new ProcessStartInfo("dotnet", $"run --no-build --no-launch-profile --project src/CanDoItAll.Components.Sandbox --urls {baseUrl}")
            {
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            processStartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            processStartInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";

            var process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("Failed to start CanDoItAll.Components.Sandbox for benchmark validation.");
            var runtime = new SandboxRuntime(
                process,
                PumpAsync(process.StandardOutput),
                PumpAsync(process.StandardError));

            await runtime.WaitForReadyAsync($"{baseUrl}/groups/canvas/benchmark");
            return runtime;
        }

        public async ValueTask DisposeAsync()
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            await stdoutPump;
            await stderrPump;
        }

        private static async Task PumpAsync(StreamReader reader)
        {
            while (await reader.ReadLineAsync() is not null)
            {
            }
        }

        private async Task WaitForReadyAsync(string benchmarkUrl)
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(3)
            };

            var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(45);
            while (DateTimeOffset.UtcNow < timeoutAt)
            {
                if (process.HasExited)
                {
                    throw new InvalidOperationException($"The sandbox app exited before becoming ready.{Environment.NewLine}{string.Join(Environment.NewLine, logs)}");
                }

                try
                {
                    var response = await client.GetAsync(benchmarkUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var payload = await response.Content.ReadAsStringAsync();
                        if (payload.Contains("Canvas Renderer Benchmark", StringComparison.Ordinal))
                        {
                            return;
                        }
                    }
                }
                catch
                {
                }

                await Task.Delay(250);
            }

            throw new TimeoutException($"Timed out waiting for the sandbox benchmark route.{Environment.NewLine}{string.Join(Environment.NewLine, logs)}");
        }
    }
}
