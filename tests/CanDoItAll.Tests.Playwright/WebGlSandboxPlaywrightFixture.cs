using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed class WebGlSandboxPlaywrightFixture : IAsyncLifetime
{
    private readonly ConcurrentQueue<string> logs = new();
    private Process? process;
    private Task? stdoutPump;
    private Task? stderrPump;

    public string BaseUrl { get; } = ResolveBaseUrl();

    public IPlaywright Playwright { get; private set; } = default!;

    public IBrowser Browser { get; private set; } = default!;

    public string GetLogSnapshot(int maxLines = 200)
    {
        return string.Join(
            Environment.NewLine,
            logs.Reverse().Take(maxLines).Reverse());
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

        var processStartInfo = new ProcessStartInfo(
            "dotnet",
            $"run --configuration Release --no-build --no-launch-profile --project src/CanDoItAll.Components.WebGlSandbox --urls {BaseUrl}")
        {
            WorkingDirectory = GetRepoRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        processStartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        processStartInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";

        process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("Failed to start the WebGL sandbox host.");
        stdoutPump = PumpAsync(process.StandardOutput);
        stderrPump = PumpAsync(process.StandardError);

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

        if (process is not null && !process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        if (stdoutPump is not null)
        {
            await stdoutPump;
        }

        if (stderrPump is not null)
        {
            await stderrPump;
        }
    }

    private async Task PumpAsync(StreamReader reader)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            logs.Enqueue(line);
        }
    }

    private async Task WaitForRuntimeReadyAsync()
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(45);

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (process is { HasExited: true })
            {
                throw new InvalidOperationException($"The WebGL sandbox exited before becoming ready.{Environment.NewLine}{GetLogSnapshot()}");
            }

            if (await IsRuntimeReadyAsync(TimeSpan.FromSeconds(2)))
            {
                return;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Timed out waiting for WebGL sandbox readiness.{Environment.NewLine}{GetLogSnapshot()}");
    }

    private async Task<bool> IsRuntimeReadyAsync(TimeSpan timeout)
    {
        using var handler = new HttpClientHandler();
        if (Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri) &&
            string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        using var client = new HttpClient(handler)
        {
            Timeout = timeout
        };

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

    private static string GetRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }

    private static string ResolveBaseUrl()
    {
        var configuredBaseUrl = Environment.GetEnvironmentVariable("CANDOITALL_WEBGL_SANDBOX_BASEURL");
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
