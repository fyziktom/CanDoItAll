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
    private readonly ConcurrentQueue<string> _logs = new();
    private Process? _process;
    private Task? _stdoutPump;
    private Task? _stderrPump;
    private CanDoItAllTestEnvironment? _testEnvironment;
    private TestDatabaseProfile? _activeProfile;

    public string BaseUrl { get; } = ResolveBaseUrl();

    public IPlaywright Playwright { get; private set; } = default!;

    public IBrowser Browser { get; private set; } = default!;

    public string? DatabaseConnectionString => _activeProfile?.ConnectionString;

    public string? StorageWorkspaceRoot => _activeProfile?.WorkspaceRootPath;

    public string GetLogSnapshot(int maxLines = 200)
    {
        return string.Join(
            Environment.NewLine,
            _logs.Reverse().Take(maxLines).Reverse());
    }

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
        foreach (var pair in _activeProfile.CreateEnvironmentVariables(new Dictionary<string, string?>
        {
            ["DevelopmentManager:TuningModeEnabled"] = "false",
            [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind
        }))
        {
            processStartInfo.Environment[pair.Key] = pair.Value;
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

        if (_testEnvironment is not null)
        {
            await _testEnvironment.DisposeAsync();
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
