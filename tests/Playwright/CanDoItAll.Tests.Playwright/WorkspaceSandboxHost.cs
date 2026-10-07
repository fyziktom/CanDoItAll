using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace CanDoItAll.Tests.Playwright;

internal sealed class WorkspaceSandboxHost : IAsyncDisposable {
    private readonly Process process;
    private readonly ConcurrentQueue<string> logs = new();
    private readonly Task[] pumps;
    public string BaseUrl { get; }
    public string Logs => string.Join(Environment.NewLine, logs);

    public WorkspaceSandboxHost() {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        BaseUrl = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
        var root = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "src", "Sandboxes", "CanDoItAll.Workspace.UiSandbox");
        var start = new ProcessStartInfo("dotnet") {
            WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(Path.Combine(root, "bin", PlaywrightTestHostPaths.BuildConfiguration, "net10.0", "CanDoItAll.Workspace.UiSandbox.dll"));
        start.ArgumentList.Add("--urls");
        start.ArgumentList.Add(BaseUrl);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        start.Environment.Remove("CANDOITALL_TESTS_POSTGRES_CONNECTION");
        process = Process.Start(start) ?? throw new InvalidOperationException("Owned Workspace sandbox did not start.");
        logs.Enqueue($"Owned sandbox PID={process.Id}, URL={BaseUrl}");
        pumps = [PumpAsync(process.StandardOutput), PumpAsync(process.StandardError)];
    }
    public async Task ReadyAsync() {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        while (true) {
            deadline.Token.ThrowIfCancellationRequested();
            if (process.HasExited) {
                throw new InvalidOperationException(Logs);
            }
            try {
                if ((await client.GetAsync(BaseUrl, deadline.Token)).IsSuccessStatusCode) {
                    return;
                }
            } catch (HttpRequestException) {
            } catch (OperationCanceledException) when (!deadline.IsCancellationRequested) {
            }
            await Task.Delay(100, deadline.Token);
        }
    }
    private async Task PumpAsync(StreamReader reader) {
        while (await reader.ReadLineAsync() is { } line) {
            logs.Enqueue(line);
        }
    }
    public async ValueTask DisposeAsync() {
        if (!process.HasExited) {
            process.Kill(entireProcessTree: true);
        }
        await process.WaitForExitAsync();
        await Task.WhenAll(pumps);
        process.Dispose();
    }
}
