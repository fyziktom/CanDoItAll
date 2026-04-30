using System.Text.Json;
using CanDoItAll.Mcp.ProjectStructure;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using ModelContextProtocol.Client;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectStructureMcpStdioIntegrationTests
{
    private static readonly string ServerAssemblyPath = IntegrationTestPaths.ResolveProjectOutputAssembly(
        "CanDoItAll.Mcp.ProjectStructure",
        "CanDoItAll.Mcp.ProjectStructure.dll");

    [Trait("Category", "LiveProcess")]
    [Fact]
    public async Task ProjectStructureMcp_stdio_call_lists_projects_when_branch_name_is_resolved_from_git()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var seedTools = ProjectStructureMcpIntegrationTestsAccessor.CreateTools(host, "STDIO seed agent");
        var project = await ProjectStructureMcpIntegrationTestsAccessor.AssertOkAsync(seedTools.ProjectStructureProjectCreateAsync(new ProjectStructureProjectSaveRequest(
            "STDIO branch resolution project",
            "Validate the stdio MCP transport.",
            "Regression coverage for branch resolution inside the MCP server.",
            "Validation")));

        var settingsPath = Path.Combine(Path.GetTempPath(), $"candoitall-project-structure-mcp-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(settingsPath, $$"""
                {
                  "Server": {
                    "Name": "CanDoItAll.Mcp.ProjectStructure",
                    "BaseUrl": "{{host.Client.BaseAddress!.ToString().TrimEnd('/')}}",
                    "AgentToken": "{{host.Client.DefaultRequestHeaders.GetValues(ProjectStructureAgentHttpHeaders.AgentToken).Single()}}",
                    "AgentName": "STDIO regression agent",
                    "RepositoryRoot": "{{IntegrationTestPaths.RepositoryRoot.Replace(@"\", @"\\")}}",
                    "TimeoutSeconds": 30
                  }
                }
                """);

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
                WorkingDirectory = IntegrationTestPaths.RepositoryRoot,
                ShutdownTimeout = TimeSpan.FromSeconds(15)
            });

            await using var client = await McpClient.CreateAsync(transport);
            var result = await client.CallToolAsync("project_structure_projects_list", new Dictionary<string, object?>());

            Assert.False(result.IsError ?? false);
            Assert.True(result.StructuredContent is JsonElement { ValueKind: JsonValueKind.Object });
            var envelope = (JsonElement)result.StructuredContent!;
            Assert.True(envelope.GetProperty("ok").GetBoolean());
            Assert.Contains(
                envelope.GetProperty("data").EnumerateArray().Select(item => item.GetProperty("name").GetString()),
                name => string.Equals(name, project.Name, StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }
}

internal static class ProjectStructureMcpIntegrationTestsAccessor
{
    public static async Task<T> AssertOkAsync<T>(Task<CanDoItAll.Mcp.Core.Contracts.McpToolEnvelope<T>> task)
    {
        var envelope = await task;
        Assert.True(envelope.Ok, envelope.Error?.Message ?? "Tool returned a failed envelope.");
        return envelope.Data!;
    }

    public static ProjectStructureTools CreateTools(ProjectStructureAgentApiTestHost host, string agentName)
    {
        var token = host.Client.DefaultRequestHeaders.GetValues(ProjectStructureAgentHttpHeaders.AgentToken).Single();
        var options = Microsoft.Extensions.Options.Options.Create(new McpServerOptions
        {
            Server = new ServerOptions
            {
                BaseUrl = host.Client.BaseAddress!.ToString().TrimEnd('/'),
                AgentToken = token,
                AgentName = agentName,
                RepositoryRoot = IntegrationTestPaths.RepositoryRoot,
                BranchName = "tests/project-structure",
                TimeoutSeconds = 30
            }
        });

        var runtime = new RuntimeConfiguration(options, new CanDoItAll.Mcp.Core.Identity.ServerInstanceIdentity());
        var httpClient = new ProjectStructureHttpClient(new HttpClient(), runtime, Microsoft.Extensions.Logging.Abstractions.NullLogger<ProjectStructureHttpClient>.Instance);
        var coordinator = new ProjectStructureCoordinator(httpClient, runtime);
        return new ProjectStructureTools(coordinator, Microsoft.Extensions.Logging.Abstractions.NullLogger<ProjectStructureTools>.Instance);
    }
}
