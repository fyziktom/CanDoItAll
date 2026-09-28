using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Playwright.Plugins;

internal sealed class PluginsBrowserHost : IAsyncDisposable {
    private readonly CanDoItAllTestEnvironment? environment;
    private readonly ConcurrentQueue<string> logs = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource> observations = new(StringComparer.Ordinal);
    private readonly Process process;
    private readonly Task[] pumps;
    private bool disposed;
    public TestDatabaseProfile? Profile { get; }
    public string BaseUrl { get; }
    public string? PackageRoot { get; }
    public string Logs => string.Join(Environment.NewLine, logs);

    public PluginsBrowserHost(bool sandbox) {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        BaseUrl = $"http://127.0.0.1:{port}";
        var root = PlaywrightTestHostPaths.RepositoryRoot;
        var project = sandbox ? "src/Sandboxes/CanDoItAll.Plugins.UiSandbox" : "tests/Playwright/PluginsBrowserFixture";
        var assembly = sandbox ? "CanDoItAll.Plugins.UiSandbox.dll" : "PluginsBrowserFixture.dll";
        var start = new ProcessStartInfo("dotnet") {
            WorkingDirectory = Path.Combine(root, project), UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(Path.Combine(root, project, "bin", PlaywrightTestHostPaths.BuildConfiguration, "net10.0", assembly));
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        if (sandbox) {
            start.Environment.Remove("CANDOITALL_TESTS_POSTGRES_CONNECTION");
            start.ArgumentList.Add("--urls");
            start.ArgumentList.Add(BaseUrl);
        } else {
            environment = CanDoItAllTestEnvironment.Create("plugins-browser-probe");
            Profile = environment.CreatePostgreSqlProfile("primary");
            PackageRoot = Path.Combine(environment.RootPath, "packages");
            foreach (var pair in Profile.CreateEnvironmentVariables(new Dictionary<string, string?> {
                ["PluginPackages:RootPath"] = PackageRoot,
                ["DevelopmentManager:TuningModeEnabled"] = "false",
                ["Workbench:RuntimePresentation:EnableWindowsTerminal"] = "false",
                [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind
            })) {
                start.Environment[pair.Key] = pair.Value;
            }
            start.ArgumentList.Add(Path.Combine(root, "src", "App", "CanDoItAll.Web"));
            start.ArgumentList.Add(port.ToString(CultureInfo.InvariantCulture));
        }
        process = Process.Start(start) ?? throw new InvalidOperationException("Owned Plugins browser host failed to start.");
        pumps = [PumpAsync(process.StandardOutput), PumpAsync(process.StandardError)];
    }

    public async Task ReadyAsync(bool sandbox) {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        while (true) {
            deadline.Token.ThrowIfCancellationRequested();
            if (process.HasExited) {
                throw new InvalidOperationException(Logs);
            }
            try {
                var response = await client.GetStringAsync(BaseUrl + (sandbox ? "/" : "/_dev/runtime"), deadline.Token);
                if (sandbox || response.Contains("\"isReady\":true", StringComparison.OrdinalIgnoreCase)) {
                    return;
                }
            } catch (HttpRequestException) {
            } catch (OperationCanceledException) when (!deadline.IsCancellationRequested) {
            }
            await Task.Delay(100, deadline.Token);
        }
    }

    public Task ObserveAsync(string observation) => observations.GetOrAdd(observation,
        _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task.WaitAsync(TimeSpan.FromSeconds(30));

    public async Task CommandAsync(PluginsProbeCommand command) {
        observations.TryRemove(PluginsProbeProtocol.Ack(command), out _);
        await process.StandardInput.WriteLineAsync(command.ToString());
        await process.StandardInput.FlushAsync();
        await ObserveAsync(PluginsProbeProtocol.Ack(command));
    }

    private async Task PumpAsync(StreamReader reader) {
        while (await reader.ReadLineAsync() is { } line) {
            logs.Enqueue(line);
            observations.GetOrAdd(line, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
        }
    }

    public async ValueTask DisposeAsync() {
        if (disposed) {
            return;
        }
        disposed = true;
        if (!process.HasExited) {
            if (Profile is not null) {
                await process.StandardInput.WriteLineAsync(PluginsProbeCommand.Stop.ToString());
                await process.StandardInput.FlushAsync();
                try {
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                } catch (TimeoutException) {
                    process.Kill(entireProcessTree: true);
                }
            } else {
                process.Kill(entireProcessTree: true);
            }
        }
        await process.WaitForExitAsync();
        await Task.WhenAll(pumps);
        process.Dispose();
        if (environment is not null) {
            await environment.DisposeAsync();
        }
    }
}
