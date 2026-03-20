using System.Text.Json;
using System.Text.Json.Serialization;
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
        RepoRoot,
        "src",
        "CanDoItAll.Mcp.DotNetWatch",
        "bin",
        "Debug",
        "net10.0",
        "CanDoItAll.Mcp.DotNetWatch.dll");

    public static string SettingsPath { get; } = Path.Combine(RepoRoot, "CanDoItAll.Mcp.DotNetWatch.settings.json");

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
}
