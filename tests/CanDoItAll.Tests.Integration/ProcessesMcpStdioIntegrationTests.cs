using System.Text.Json;
using CanDoItAll.Modules.Processes;
using ModelContextProtocol.Client;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessesMcpStdioIntegrationTests
{
    private const string RepositoryRoot = @"C:\repositories\CanDoItAll";
    private static readonly string ServerAssemblyPath = Path.GetFullPath(Path.Combine(RepositoryRoot, @"src\CanDoItAll.Mcp.Processes\bin\Debug\net10.0\CanDoItAll.Mcp.Processes.dll"));

    [Fact]
    public async Task ProcessesMcp_stdio_call_lists_seeded_process_definitions()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var seedService = scope.ServiceProvider.GetRequiredService<ProcessDevelopmentSeedService>();
        var seedResult = await seedService.SeedBaselineAsync();

        Assert.True(seedResult.IsSuccess);
        Assert.NotNull(seedResult.Value);

        var settingsPath = Path.Combine(Path.GetTempPath(), $"candoitall-processes-mcp-{Guid.NewGuid():N}.json");
        try
        {
            var settings = new
            {
                Server = new
                {
                    Name = "CanDoItAll.Mcp.Processes",
                    RepositoryRoot = Path.GetFullPath(RepositoryRoot),
                    EnsureCurrentProfileReadyOnStartup = true
                },
                Database = new
                {
                    Provider = application.ActiveProfile.Provider switch
                    {
                        CanDoItAll.Tests.Support.TestDatabaseProviderKind.Sqlite => "Sqlite",
                        CanDoItAll.Tests.Support.TestDatabaseProviderKind.PostgreSql => "Postgres",
                        _ => "InMemory"
                    },
                    ConnectionString = application.ActiveProfile.ConnectionString
                },
                Storage = new
                {
                    WorkspaceRoot = application.ActiveProfile.WorkspaceRootPath,
                    ManagedFilesFolder = "managed-files",
                    ExportsFolder = "exports",
                    EvidenceFolder = "evidence",
                    ManagerArtifactsFolder = application.ActiveProfile.ManagerArtifactsRootPath
                },
                Workbench = new
                {
                    MaxWarmTabs = 3,
                    SleepAfterMinutes = 15,
                    BrowserStorageKey = "candoitall.workbench.session"
                },
                DevelopmentManager = new
                {
                    TuningModeEnabled = true,
                    ReviewBeforeSend = true,
                    ManagerBaseUrl = "http://127.0.0.1:6407"
                }
            };

            await File.WriteAllTextAsync(settingsPath, JsonSerializer.Serialize(settings));

            var transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "CanDoItAll.Tests.Integration",
                Command = "dotnet",
                Arguments =
                [
                    ServerAssemblyPath,
                    "--settings",
                    settingsPath
                ],
                WorkingDirectory = Path.GetFullPath(RepositoryRoot),
                ShutdownTimeout = TimeSpan.FromSeconds(15)
            });

            await using var client = await McpClient.CreateAsync(transport);
            var result = await client.CallToolAsync("processes_definitions_list", new Dictionary<string, object?>());

            Assert.False(result.IsError ?? false);
            Assert.True(result.StructuredContent is JsonElement { ValueKind: JsonValueKind.Object });
            var envelope = (JsonElement)result.StructuredContent!;
            Assert.True(envelope.GetProperty("ok").GetBoolean());
            Assert.Contains(
                envelope.GetProperty("data").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()),
                id => id == seedResult.Value.PrimaryDefinitionId);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }
}
