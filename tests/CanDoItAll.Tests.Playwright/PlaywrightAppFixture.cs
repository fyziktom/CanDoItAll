using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed class PlaywrightAppFixture : IAsyncLifetime
{
    private readonly ConcurrentQueue<string> _logs = new();
    private Process? _process;
    private Task? _stdoutPump;
    private Task? _stderrPump;
    private string? _workspaceRoot;

    public string BaseUrl { get; } = "http://127.0.0.1:5188";

    public IPlaywright Playwright { get; private set; } = default!;

    public IBrowser Browser { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        _workspaceRoot = Path.Combine(Path.GetTempPath(), "candoitall-playwright", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspaceRoot);

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var databasePath = Path.Combine(_workspaceRoot, "playwright.db");
        var processStartInfo = new ProcessStartInfo("dotnet", $"run --no-build --no-launch-profile --project src/CanDoItAll.Web --urls {BaseUrl}")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        processStartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        processStartInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
        processStartInfo.Environment["Database__Provider"] = "Sqlite";
        processStartInfo.Environment["Database__ConnectionString"] = $"Data Source={databasePath}";
        processStartInfo.Environment["Storage__WorkspaceRoot"] = Path.Combine(_workspaceRoot, "workspace");
        processStartInfo.Environment["DevelopmentManager__TuningModeEnabled"] = "false";

        _process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("Failed to start CanDoItAll.Web for Playwright tests.");
        _stdoutPump = PumpAsync(_process.StandardOutput);
        _stderrPump = PumpAsync(_process.StandardError);

        await WaitForRuntimeReadyAsync();

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }

        Playwright?.Dispose();

        if (_process is not null && !_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        if (_stdoutPump is not null)
        {
            await _stdoutPump;
        }

        if (_stderrPump is not null)
        {
            await _stderrPump;
        }

        if (_workspaceRoot is not null && Directory.Exists(_workspaceRoot))
        {
            DeleteDirectoryWithRetry(_workspaceRoot);
        }
    }

    private async Task PumpAsync(StreamReader reader)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            _logs.Enqueue(line);
        }
    }

    private async Task WaitForRuntimeReadyAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(45);

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (_process is { HasExited: true })
            {
                throw new InvalidOperationException($"The web app exited before becoming ready.{Environment.NewLine}{string.Join(Environment.NewLine, _logs)}");
            }

            try
            {
                var payload = await client.GetStringAsync($"{BaseUrl}/_dev/runtime");
                if (payload.Contains("\"isReady\":true", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            catch
            {
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Timed out waiting for runtime readiness.{Environment.NewLine}{string.Join(Environment.NewLine, _logs)}");
    }

    private static void DeleteDirectoryWithRetry(string path)
    {
        const int maxAttempts = 6;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(150 * attempt);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                Thread.Sleep(150 * attempt);
            }
        }
    }
}
