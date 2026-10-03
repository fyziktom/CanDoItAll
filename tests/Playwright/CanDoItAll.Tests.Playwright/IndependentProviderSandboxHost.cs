using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Playwright;

internal enum ProviderSandboxKind { Profiles, History }

internal sealed class IndependentProviderSandboxHost(ProviderSandboxKind kind) : IAsyncDisposable {
    private readonly CanDoItAllTestEnvironment environment = CanDoItAllTestEnvironment.Create("provider-ui-sandbox");
    private readonly ConcurrentQueue<string> logs = new();
    private readonly string evidence = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "artifacts",
        kind == ProviderSandboxKind.History ? "provider-history-pp3" : "provider-profiles-pp1", "sandbox-hosts", Guid.NewGuid().ToString("N"));
    private Process? process;
    private bool publishedHost;
    private Task[] pumps = [];
    public string BaseUrl { get; private set; } = string.Empty;
    public string Logs => string.Join(Environment.NewLine, logs);

    public async Task StartAsync(bool published) {
        publishedHost = published;
        var projectName = kind switch {
            ProviderSandboxKind.Profiles => "CanDoItAll.AgentFramework.Providers.UiSandbox",
            ProviderSandboxKind.History => "CanDoItAll.AgentFramework.UiSandbox",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var root = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "src", "Sandboxes", projectName);
        var sourceBinaryRoot = kind == ProviderSandboxKind.History
            ? Path.Combine(root, "bin", "Parity", PlaywrightTestHostPaths.BuildConfiguration, "net10.0")
            : Path.Combine(root, "bin", PlaywrightTestHostPaths.BuildConfiguration, "net10.0");
        var directory = published ? Path.Combine(environment.RootPath, "publish") : root;
        if (published) {
            var publish = StartInfo(root);
            foreach (var argument in new[] { "publish", "--no-build", "--no-restore", "/m:1", "/nr:false", "--configuration", PlaywrightTestHostPaths.BuildConfiguration, "--output", directory }) {
                publish.ArgumentList.Add(argument);
            }
            using var publishing = Process.Start(publish) ?? throw new InvalidOperationException("Sandbox publish did not start.");
            var output = publishing.StandardOutput.ReadToEndAsync();
            var error = publishing.StandardError.ReadToEndAsync();
            await publishing.WaitForExitAsync();
            var text = await output + await error;
            Directory.CreateDirectory(evidence);
            await File.WriteAllTextAsync(Path.Combine(evidence, "sandbox-publish.log"), text);
            Assert.Equal(0, publishing.ExitCode);
        }
        var binaryRoot = published ? directory : sourceBinaryRoot;
        var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(binaryRoot, "*", SearchOption.AllDirectories)
                     .Where(file => Path.GetExtension(file) is ".dll" or ".pdb" or ".css" or ".woff2")) {
            using var stream = File.OpenRead(file);
            hashes.Add(Path.GetRelativePath(binaryRoot, file), Convert.ToHexString(await SHA256.HashDataAsync(stream)));
        }
        Directory.CreateDirectory(evidence);
        await File.WriteAllTextAsync(Path.Combine(evidence, "compiled-artifacts.json"), JsonSerializer.Serialize(new {
            published, configuration = PlaywrightTestHostPaths.BuildConfiguration, hashes
        }));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        BaseUrl = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
        listener.Stop();
        var start = StartInfo(directory);
        start.ArgumentList.Add(Path.Combine(binaryRoot, projectName + ".dll"));
        start.ArgumentList.Add("--urls");
        start.ArgumentList.Add(BaseUrl);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = published ? "Production" : "Development";
        start.Environment["DOTNET_ENVIRONMENT"] = published ? "Production" : "Development";
        start.Environment.Remove("CANDOITALL_TESTS_POSTGRES_CONNECTION");
        process = Process.Start(start) ?? throw new InvalidOperationException("Owned provider sandbox did not start.");
        logs.Enqueue($"Owned sandbox PID={process.Id}; published={published}; URL={BaseUrl}");
        pumps = [PumpAsync(process.StandardOutput), PumpAsync(process.StandardError)];
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        while (true) {
            deadline.Token.ThrowIfCancellationRequested();
            if (process.HasExited) {
                throw new InvalidOperationException(Logs);
            }
            try {
                using var response = await client.GetAsync(BaseUrl, deadline.Token);
                if (response.IsSuccessStatusCode) {
                    return;
                }
            } catch (HttpRequestException) {
            } catch (OperationCanceledException) when (!deadline.IsCancellationRequested) {
            }
            await Task.Delay(100, deadline.Token);
        }
    }
    private static ProcessStartInfo StartInfo(string directory) => new("dotnet") {
        WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardOutput = true, RedirectStandardError = true
    };
    private async Task PumpAsync(StreamReader reader) {
        while (await reader.ReadLineAsync() is { } line) {
            logs.Enqueue(line);
        }
    }
    public async ValueTask DisposeAsync() {
        if (process is not null) {
            if (!process.HasExited) {
                process.Kill(entireProcessTree: true);
            }
            await process.WaitForExitAsync();
            await Task.WhenAll(pumps);
            Directory.CreateDirectory(evidence);
            await File.WriteAllTextAsync(Path.Combine(evidence, publishedHost ? "published-sandbox-host.log" : "source-sandbox-host.log"), Logs);
            process.Dispose();
        }
        await environment.DisposeAsync();
    }
}
