using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed class PlaywrightAppFixture : IAsyncLifetime
{
    private const string HeadlessRuntimePresentationEnvironmentVariable =
        "CANDOITALL_PLAYWRIGHT_HEADLESS_RUNTIME_PRESENTATION";

    private readonly ConcurrentQueue<string> _logs = new();
    private readonly object _cleanupLock = new();
    private Task? _stopTask;
    private Task? _disposeTask;
    private Process? _process;
    private Task? _stdoutPump;
    private Task? _stderrPump;
    private CanDoItAllTestEnvironment? _testEnvironment;
    private TestDatabaseProfile? _activeProfile;

    public IReadOnlyDictionary<string, string?> RuntimeConfiguration { get; init; } = new Dictionary<string, string?>();
    internal bool EnableBackgroundWorkers { get; init; }

    public string BaseUrl { get; } = ResolveBaseUrl();

    public IPlaywright Playwright { get; private set; } = default!;

    public IBrowser Browser { get; private set; } = default!;

    internal TestDatabaseProfile OwnedDatabaseProfile => _activeProfile
        ?? throw new InvalidOperationException("The Playwright fixture must own its host profile before creating a seed provider.");

    public string? DatabaseConnectionString => _activeProfile?.ConnectionString;

    public string? StorageWorkspaceRoot => _activeProfile?.WorkspaceRootPath;

    internal static async Task CompleteDatabaseStartupAsync(IPage page) {
        await page.WaitForFunctionAsync("() => typeof window.CanDoItAll?.browserState?.isDatabaseStartupPromptDismissed === 'function'");
        if (!await page.EvaluateAsync<bool>("() => window.CanDoItAll.browserState.isDatabaseStartupPromptDismissed()")) {
            await page.GetByTestId("database-startup-modal").WaitForAsync();
            await page.GetByTestId("database-startup-continue").ClickAsync();
        }
        await page.GetByTestId("database-startup-modal").WaitForAsync(new() { State = WaitForSelectorState.Detached });
        await page.WaitForFunctionAsync("() => window.CanDoItAll.browserState.isDatabaseStartupPromptDismissed()");
    }

    public bool IsRuntimePresentationHeadless =>
        !OperatingSystem.IsWindows() ||
        string.Equals(
            Environment.GetEnvironmentVariable(HeadlessRuntimePresentationEnvironmentVariable),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public string GetLogSnapshot(int maxLines = 200)
    {
        return string.Join(
            Environment.NewLine,
            _logs.Reverse().Take(maxLines).Reverse());
    }

    internal string[] GetLogLines() => _logs.ToArray();

    public async Task InitializeAsync()
    {
        if (await IsRuntimeReadyAsync(TimeSpan.FromSeconds(3)))
        {
            Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });
            return;
        }

        _testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-playwright");
        _activeProfile = _testEnvironment.CreatePostgreSqlProfile("primary");

        var processStartInfo = new ProcessStartInfo(
            "dotnet",
            PlaywrightTestHostPaths.BuildDotnetRunArguments("src/App/CanDoItAll.Web", BaseUrl))
        {
            WorkingDirectory = PlaywrightTestHostPaths.RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        processStartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        processStartInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
        var configuration = new Dictionary<string, string?>(RuntimeConfiguration) {
            ["DevelopmentManager:TuningModeEnabled"] = "false",
            ["Logging:LogLevel:Microsoft.AspNetCore.Components.Server.Circuits.RemoteNavigationManager"] = "Debug",
            ["Logging:LogLevel:Microsoft.AspNetCore.Components.Server.Circuits.CircuitHost"] = "Debug",
            ["Logging:LogLevel:Microsoft.AspNetCore.Components.Server.Circuits.CircuitRegistry"] = "Debug",
            [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = EnableBackgroundWorkers ? "" : LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind
        };
        foreach (var pair in _activeProfile.CreateEnvironmentVariables(configuration))
        {
            processStartInfo.Environment[pair.Key] = pair.Value;
        }

        if (IsRuntimePresentationHeadless)
        {
            processStartInfo.Environment["Workbench__RuntimePresentation__EnableWindowsTerminal"] = "false";
        }

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

    public Task DisposeAsync() {
        lock (_cleanupLock) {
            return _disposeTask ??= DisposeCoreAsync();
        }
    }

    private async Task DisposeCoreAsync() {
        var failures = new List<Exception>();
        Func<Task>[] cleanup = [
            () => Browser?.DisposeAsync().AsTask() ?? Task.CompletedTask,
            () => {
                Playwright?.Dispose();
                return Task.CompletedTask;
            },
            StopOwnedApplicationAsync,
            () => _testEnvironment?.DisposeAsync().AsTask() ?? Task.CompletedTask
        ];
        foreach (var release in cleanup) {
            try {
                await release();
            } catch (Exception failure) {
                failures.Add(failure);
            }
        }
        _process?.Dispose();
        if (failures.Count > 0) {
            throw new AggregateException("Playwright fixture cleanup failed.", failures);
        }
    }

    internal Task StopOwnedApplicationAsync() {
        lock (_cleanupLock) {
            return _stopTask ??= StopOwnedApplicationCoreAsync();
        }
    }

    private async Task StopOwnedApplicationCoreAsync() {
        if (_process is not null && !_process.HasExited) {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        if (_stdoutPump is not null) {
            await _stdoutPump;
        }

        if (_stderrPump is not null) {
            await _stderrPump;
        }
    }

    private async Task PumpAsync(StreamReader reader)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            _logs.Enqueue($"{DateTimeOffset.UtcNow:O} [host:{_process?.Id}] {line}");
        }
    }

    private async Task WaitForRuntimeReadyAsync()
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMinutes(2);

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (_process is { HasExited: true })
            {
                throw new InvalidOperationException($"The web app exited before becoming ready.{Environment.NewLine}{string.Join(Environment.NewLine, _logs)}");
            }

            try
            {
                if (await IsRuntimeReadyAsync(TimeSpan.FromSeconds(2)))
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

    private async Task<bool> IsRuntimeReadyAsync(TimeSpan timeout)
    {
        using var handler = new HttpClientHandler();
        if (Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri) &&
            string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        using var client = new HttpClient(handler) { Timeout = timeout };

        try
        {
            var payload = await client.GetStringAsync($"{BaseUrl}/_dev/runtime");
            return payload.Contains("\"isReady\":true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
    private static string ResolveBaseUrl()
    {
        var configuredBaseUrl = Environment.GetEnvironmentVariable("CANDOITALL_PLAYWRIGHT_BASEURL");
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return configuredBaseUrl;
        }

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return $"http://127.0.0.1:{port}";
    }
}
