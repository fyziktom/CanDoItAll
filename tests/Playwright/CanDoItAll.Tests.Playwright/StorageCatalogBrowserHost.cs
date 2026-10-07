using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Playwright.StorageCatalog;

internal sealed class StorageCatalogBrowserHost : IAsyncDisposable {
    private readonly CanDoItAllTestEnvironment? environment;
    private readonly ConcurrentQueue<string> logs = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource> observations = new(StringComparer.Ordinal);
    private readonly Process process;
    private readonly Task[] pumps;
    private bool disposed;
    public TestDatabaseProfile? Profile { get; }
    public string BaseUrl { get; }
    public string Logs => string.Join(Environment.NewLine, logs);

    public StorageCatalogBrowserHost(bool sandbox, string? publishedDirectory = null) {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        BaseUrl = $"http://127.0.0.1:{port}";
        var root = PlaywrightTestHostPaths.RepositoryRoot;
        var project = sandbox ? "src/Sandboxes/CanDoItAll.Workspace.StorageCatalog.UiSandbox" : "tests/Playwright/StorageCatalogBrowserFixture";
        var assembly = sandbox ? "CanDoItAll.Workspace.StorageCatalog.UiSandbox.dll" : "StorageCatalogBrowserFixture.dll";
        var start = new ProcessStartInfo("dotnet") {
            WorkingDirectory = publishedDirectory ?? Path.Combine(root, project), UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(publishedDirectory is null ? Path.Combine(root, project, "bin", PlaywrightTestHostPaths.BuildConfiguration, "net10.0", assembly) : Path.Combine(publishedDirectory, assembly));
        start.Environment["ASPNETCORE_ENVIRONMENT"] = publishedDirectory is null ? "Development" : "Production";
        start.Environment["DOTNET_ENVIRONMENT"] = publishedDirectory is null ? "Development" : "Production";
        if (sandbox) {
            start.Environment.Remove("CANDOITALL_TESTS_POSTGRES_CONNECTION");
            start.ArgumentList.Add("--urls");
            start.ArgumentList.Add(BaseUrl);
        } else {
            environment = CanDoItAllTestEnvironment.Create("storage-catalog-browser-probe");
            Profile = environment.CreatePostgreSqlProfile("primary");
            foreach (var pair in Profile.CreateEnvironmentVariables(new Dictionary<string, string?> {
                ["Api:Authorization:Enabled"] = "true",
                ["Api:Authorization:SigningKey"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
                ["DevelopmentManager:TuningModeEnabled"] = "false",
                ["Workbench:RuntimePresentation:EnableWindowsTerminal"] = "false",
                [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind
            })) {
                start.Environment[pair.Key] = pair.Value;
            }
            start.ArgumentList.Add(Path.Combine(root, "src", "App", "CanDoItAll.Web"));
            start.ArgumentList.Add(port.ToString(CultureInfo.InvariantCulture));
        }
        process = Process.Start(start) ?? throw new InvalidOperationException("Owned StorageCatalog browser host failed to start.");
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

    public async Task CommandAsync(StorageCatalogProbeCommand command) {
        observations.TryRemove(StorageCatalogProbeProtocol.Ack(command), out _);
        await process.StandardInput.WriteLineAsync(command.ToString());
        await process.StandardInput.FlushAsync();
        await ObserveAsync(StorageCatalogProbeProtocol.Ack(command));
    }

    private async Task PumpAsync(StreamReader reader) {
        while (await reader.ReadLineAsync() is { } line) {
            logs.Enqueue(line);
            while (logs.Count > 500) {
                logs.TryDequeue(out _);
            }
            if (line.StartsWith(StorageCatalogProbeProtocol.Ready, StringComparison.Ordinal) ||
                Enum.GetValues<StorageCatalogProbeCommand>().Any(command => line == StorageCatalogProbeProtocol.Ack(command))) {
                observations.GetOrAdd(line, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
            }
        }
    }

    public async ValueTask DisposeAsync() {
        if (disposed) {
            return;
        }
        disposed = true;
        if (!process.HasExited) {
            if (Profile is not null) {
                await process.StandardInput.WriteLineAsync(StorageCatalogProbeCommand.Stop.ToString());
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
