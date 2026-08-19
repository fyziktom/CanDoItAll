using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

public sealed record FakeMcpServerScript(
    IReadOnlyList<DiscoveredMcpTool> Tools,
    IReadOnlyDictionary<McpToolName, string>? ToolResults = null,
    Exception? StartException = null,
    Exception? ListToolsException = null,
    Exception? CallToolException = null,
    Exception? StopException = null);

public sealed class FakeMcpClientFactory(FakeMcpServerScript script) : IMcpClientFactory
{
    public int CreatedClients { get; private set; }

    public FakeMcpRuntimeClient? LastClient { get; private set; }

    public McpServerDescriptor? LastDescriptor { get; private set; }

    public Task<IMcpRuntimeClient> CreateAsync(
        McpServerDescriptor descriptor,
        string correlationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CreatedClients++;
        LastDescriptor = descriptor;
        LastClient = new FakeMcpRuntimeClient(script);
        return Task.FromResult<IMcpRuntimeClient>(LastClient);
    }
}

public sealed class FakeMcpRuntimeClient(FakeMcpServerScript script) : IMcpRuntimeClient
{
    public int StartCount { get; private set; }

    public int ListToolsCount { get; private set; }

    public int CallCount { get; private set; }

    public int StopCount { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StartCount++;
        return script.StartException is null
            ? Task.CompletedTask
            : Task.FromException(script.StartException);
    }

    public Task<IReadOnlyList<DiscoveredMcpTool>> ListToolsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ListToolsCount++;
        return script.ListToolsException is null
            ? Task.FromResult(script.Tools)
            : Task.FromException<IReadOnlyList<DiscoveredMcpTool>>(script.ListToolsException);
    }

    public Task<string> CallToolAsync(
        McpToolName toolName,
        string jsonArguments,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        if (script.CallToolException is not null)
        {
            return Task.FromException<string>(script.CallToolException);
        }

        if (script.ToolResults is not null &&
            script.ToolResults.TryGetValue(toolName, out var result))
        {
            return Task.FromResult(result);
        }

        return Task.FromException<string>(new McpSetupException(
            CapabilityDiagnosticCategory.ImplementationMissing,
            "$.toolName",
            $"Fake MCP tool '{toolName}' was not configured.",
            "Register a deterministic fake tool result for this test."));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StopCount++;
        return script.StopException is null
            ? Task.CompletedTask
            : Task.FromException(script.StopException);
    }
}
