using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Playwright.StorageRecovery;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Playwright;

internal sealed class StorageRecoveryBrowserHost : IAsyncDisposable {
    private readonly CanDoItAllTestEnvironment environment = CanDoItAllTestEnvironment.Create("recovery-browser-proof");
    private readonly ConcurrentQueue<string> logs = new();
    private readonly TaskCompletionSource<RecoveryBrowserCase[]> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<RecoveryBrowserProof[]> verified = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Process process;
    private readonly Task[] pumps;
    public string BaseUrl { get; }
    public string Logs => string.Join(Environment.NewLine, logs);
    private readonly string credentialPath;

    public StorageRecoveryBrowserHost() {
        var profile = environment.CreatePostgreSqlProfile("recovery");
        credentialPath = Path.Combine(environment.RootPath, "recovery-browser-credential");
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        BaseUrl = $"http://127.0.0.1:{port}";
        var root = PlaywrightTestHostPaths.RepositoryRoot;
        var start = new ProcessStartInfo("dotnet") {
            WorkingDirectory = Path.Combine(root, "tests", "Playwright", "StorageRecoveryBrowserFixture"),
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(Path.Combine(start.WorkingDirectory, "bin", PlaywrightTestHostPaths.BuildConfiguration, "net10.0", "StorageRecoveryBrowserFixture.dll"));
        start.ArgumentList.Add(Path.Combine(root, "src", "App", "CanDoItAll.Web"));
        start.ArgumentList.Add(port.ToString(CultureInfo.InvariantCulture));
        start.ArgumentList.Add(credentialPath);
        foreach (var pair in profile.CreateEnvironmentVariables(new Dictionary<string, string?> {
            ["Api:Authorization:Enabled"] = "true",
            ["Api:Authorization:SigningKey"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            ["DevelopmentManager:TuningModeEnabled"] = "false",
            ["Workbench:RuntimePresentation:EnableWindowsTerminal"] = "false",
            [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind
        })) {
            start.Environment[pair.Key] = pair.Value;
        }
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        process = Process.Start(start) ?? throw new InvalidOperationException("Owned Recovery host did not start.");
        logs.Enqueue($"Owned Recovery PID={process.Id}; URL={BaseUrl}; profile={profile.ProfileKey}");
        pumps = [PumpAsync(process.StandardOutput), PumpAsync(process.StandardError)];
    }

    public Task<RecoveryBrowserCase[]> ReadyAsync() => ready.Task.WaitAsync(TimeSpan.FromMinutes(3));
    public async Task AuthorizeAsync(Microsoft.Playwright.IBrowserContext context, bool readOnly = false) {
        var path = readOnly ? credentialPath + ".read" : credentialPath;
        var token = await File.ReadAllTextAsync(path);
        await context.SetExtraHTTPHeadersAsync(new Dictionary<string, string> { ["Authorization"] = "Bearer " + token });
        File.Delete(path);
    }
    public async Task<RecoveryBrowserProof[]> VerifyAsync() {
        await process.StandardInput.WriteLineAsync(RecoveryProbeCommand.Verify.ToString());
        await process.StandardInput.FlushAsync();
        return await verified.Task.WaitAsync(TimeSpan.FromSeconds(45));
    }

    private async Task PumpAsync(StreamReader reader) {
        while (await reader.ReadLineAsync() is { } line) {
            logs.Enqueue(line);
            if (line.StartsWith(RecoveryProbeProtocol.Ready, StringComparison.Ordinal)) {
                ready.TrySetResult(JsonSerializer.Deserialize<RecoveryBrowserCase[]>(line[RecoveryProbeProtocol.Ready.Length..])!);
            }
            if (line.StartsWith(RecoveryProbeProtocol.Verified, StringComparison.Ordinal)) {
                verified.TrySetResult(JsonSerializer.Deserialize<RecoveryBrowserProof[]>(line[RecoveryProbeProtocol.Verified.Length..])!);
            }
        }
        if (process.HasExited && process.ExitCode != 0) {
            var failure = new InvalidOperationException(Logs);
            ready.TrySetException(failure);
            verified.TrySetException(failure);
        }
    }

    public async ValueTask DisposeAsync() {
        if (!process.HasExited) {
            await process.StandardInput.WriteLineAsync(RecoveryProbeCommand.Stop.ToString());
            await process.StandardInput.FlushAsync();
            try {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            } catch (TimeoutException) {
                process.Kill(entireProcessTree: true);
            }
        }
        await process.WaitForExitAsync();
        await Task.WhenAll(pumps);
        var directory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "artifacts", "workspace-completion", "recovery");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "production-host.log"), Logs);
        process.Dispose();
        await environment.DisposeAsync();
    }
}
