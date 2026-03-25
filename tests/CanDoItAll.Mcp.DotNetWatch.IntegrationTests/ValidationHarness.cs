using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Mcp.DotNetWatch.Backend;
using ModelContextProtocol.Client;

namespace CanDoItAll.Mcp.DotNetWatch.IntegrationTests;

internal sealed class ValidationHarness : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    static ValidationHarness()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    private readonly McpClient _client;

    private ValidationHarness(McpClient client)
    {
        _client = client;
    }

    public static string RepoRoot { get; } = ResolveRepoRoot();

    public static string ServerAssemblyPath { get; } = Path.Combine(
        AppContext.BaseDirectory,
        "CanDoItAll.Mcp.DotNetWatch.dll");

    public static string SettingsPath { get; } = Path.Combine(RepoRoot, "CanDoItAll.Mcp.DotNetWatch.settings.json");

    public static string BackendRegistrationPath { get; } = Path.Combine(RepoRoot, ".mcp-state", "backend", "registration.json");

    public static string ShadowManifestPath { get; } = Path.Combine(RepoRoot, ".artifacts", "mcp-server-shadow", "current.json");

    public static string WrapperScriptPath { get; } = Path.Combine(
        RepoRoot,
        "tools",
        "CanDoItAll.Mcp.DotNetWatch",
        "Start-CanDoItAllDotNetWatchMcp.ps1");

    public static async Task<ValidationHarness> CreateAsync()
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "CanDoItAll.Mcp.DotNetWatch.ValidationTests",
            Command = "dotnet",
            Arguments =
            [
                ServerAssemblyPath,
                "--settings",
                SettingsPath
            ],
            WorkingDirectory = RepoRoot,
            ShutdownTimeout = TimeSpan.FromSeconds(15)
        });

        var client = await McpClient.CreateAsync(transport);
        return new ValidationHarness(client);
    }

    public static async Task<ValidationHarness> CreateViaWrapperAsync()
    {
        await EnsureWrapperShadowReadyAsync();

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "CanDoItAll.Mcp.DotNetWatch.ValidationTests.Wrapper",
            Command = "powershell",
            Arguments =
            [
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                WrapperScriptPath,
                "-Configuration",
                "Release"
            ],
            WorkingDirectory = RepoRoot,
            ShutdownTimeout = TimeSpan.FromSeconds(30)
        });

        var client = await McpClient.CreateAsync(transport);
        return new ValidationHarness(client);
    }

    internal static async Task<string> GetCurrentShadowServerAssemblyPathAsync()
    {
        await EnsureWrapperShadowReadyAsync();

        await using var stream = File.Open(ShadowManifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var manifest = await JsonSerializer.DeserializeAsync<ShadowManifest>(stream, JsonOptions);
        Assert.NotNull(manifest);
        Assert.False(string.IsNullOrWhiteSpace(manifest!.ShadowDllPath));
        Assert.True(File.Exists(manifest.ShadowDllPath), $"Wrapper manifest points to a missing shadow DLL: {manifest.ShadowDllPath}");
        return manifest.ShadowDllPath!;
    }

    public static async Task<ValidationHarness> CreateCapturedAsync(string stdoutCapturePath, string stderrCapturePath)
    {
        var quotedServerAssemblyPath = ServerAssemblyPath.Replace("'", "''", StringComparison.Ordinal);
        var quotedSettingsPath = SettingsPath.Replace("'", "''", StringComparison.Ordinal);
        var quotedStdoutPath = stdoutCapturePath.Replace("'", "''", StringComparison.Ordinal);
        var quotedStderrPath = stderrCapturePath.Replace("'", "''", StringComparison.Ordinal);
        var command = $"$stdout='{quotedStdoutPath}'; $stderr='{quotedStderrPath}'; & dotnet '{quotedServerAssemblyPath}' --settings '{quotedSettingsPath}' 2>> $stderr | Tee-Object -FilePath $stdout -Append";

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "CanDoItAll.Mcp.DotNetWatch.ValidationTests.Captured",
            Command = "powershell",
            Arguments =
            [
                "-NoProfile",
                "-NonInteractive",
                "-Command",
                command
            ],
            WorkingDirectory = RepoRoot,
            ShutdownTimeout = TimeSpan.FromSeconds(15)
        });

        var client = await McpClient.CreateAsync(transport);
        return new ValidationHarness(client);
    }

    public async Task<T> CallToolAsync<T>(string toolName, IReadOnlyDictionary<string, object?>? arguments = null)
    {
        var result = await _client.CallToolAsync(toolName, arguments ?? new Dictionary<string, object?>());
        Assert.True(result.IsError is not true, Serialize(result));
        var payload = result.StructuredContent is null
            ? Serialize(result.Content)
            : Serialize(result.StructuredContent);
        var value = JsonSerializer.Deserialize<T>(payload, JsonOptions);
        Assert.NotNull(value);
        return value!;
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is IAsyncDisposable asyncClient)
        {
            await asyncClient.DisposeAsync();
        }
    }

    public static async Task StopBackendIfPresentAsync()
    {
        BackendRegistrationRecord? registration = null;
        if (File.Exists(BackendRegistrationPath))
        {
            try
            {
                await using var stream = File.Open(BackendRegistrationPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                registration = await JsonSerializer.DeserializeAsync<BackendRegistrationRecord>(stream, JsonOptions);
            }
            catch
            {
                registration = null;
            }
        }

        if (registration is not null)
        {
            try
            {
                using var process = Process.GetProcessById(registration.ProcessId);
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(10000);
                }
            }
            catch
            {
            }
        }

        try
        {
            if (File.Exists(BackendRegistrationPath))
            {
                File.Delete(BackendRegistrationPath);
            }
        }
        catch
        {
        }
    }

    internal static async Task EnsureWrapperShadowReadyAsync()
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("powershell")
            {
                WorkingDirectory = RepoRoot,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };

        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
        process.StartInfo.ArgumentList.Add("Bypass");
        process.StartInfo.ArgumentList.Add("-File");
        process.StartInfo.ArgumentList.Add(WrapperScriptPath);
        process.StartInfo.ArgumentList.Add("-Configuration");
        process.StartInfo.ArgumentList.Add("Release");
        process.StartInfo.ArgumentList.Add("-PrepareOnly");

        Assert.True(process.Start());
        process.StandardInput.Close();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        Assert.Equal(0, process.ExitCode);

        Assert.True(File.Exists(ShadowManifestPath), $"Wrapper prewarm did not produce '{ShadowManifestPath}'. Stdout={stdout} Stderr={stderr}");
    }

    private static string Serialize(object? value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repo root from the test output directory.");
    }

    private sealed record ShadowManifest(string? ShadowDllPath);
}
