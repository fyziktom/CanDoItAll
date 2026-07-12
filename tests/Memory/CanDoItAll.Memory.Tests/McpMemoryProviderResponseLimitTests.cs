using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Mcp;

namespace CanDoItAll.Memory.Tests;

public sealed class McpMemoryProviderResponseLimitTests
{
    [Fact]
    public async Task Mcp_driver_rejects_oversized_result_before_response_mapping()
    {
        const int limitBytes = 128;
        var providerPayload = $"{{\"providerSecret\":\"{new string('x', limitBytes)}\"}}";
        var factory = new OversizedMcpClientFactory(providerPayload);
        var driver = new McpMemoryProviderDriver(
            factory,
            new McpMemoryProviderOptions
            {
                ResponseSizeLimit = new MemoryProviderResponseSizeLimit(limitBytes)
            });

        var result = await driver.ExecuteContextQueryAsync(
            CreateProfile(),
            CreateOperation(),
            new MemoryContextQueryRequest(
                "query",
                [MemoryCapabilityIds.ContextQuerySync],
                MemorySourceProvenance.None));

        Assert.Equal(MemoryProviderDriverResultKind.ProviderError, result.Kind);
        Assert.Contains("128 bytes", result.Diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("providerSecret", result.Diagnostic, StringComparison.Ordinal);
        Assert.True(factory.Client.Stopped);
    }

    private static MemoryProviderProfile CreateProfile()
    {
        var extensions = MemoryExtensionData.From(
            (McpMemoryProviderConfigurationKeys.ServerKey, JsonString("memory-limit")),
            (McpMemoryProviderConfigurationKeys.RemoteEndpoint, JsonString("https://memory.example.test/mcp")),
            (McpMemoryProviderConfigurationKeys.ContextQueryTool, JsonString("memory_context_query")));
        return new MemoryProviderProfile(
            MemoryProviderInstanceId.Parse("provider.mcp-limit"),
            "MCP response limit provider",
            MemoryProviderDriverKind.Mcp,
            IsEnabled: true,
            MemoryProviderHealthState.Healthy,
            MemoryProviderWorkspaceScope.AllWorkspaces,
            SelectionTags: [],
            MemoryProviderProfilePolicy.Default,
            new MemoryProviderManifest(
                MemoryProviderKind.Parse("memory.mcp"),
                MemoryProtocolVersion.Current,
                [new MemoryCapabilityDescriptor(MemoryCapabilityIds.ContextQuerySync, "1", Supported: true)],
                MemoryProviderInteractionSupport.SyncQueryOnly,
                UiSurfaces: [],
                MemoryProviderLimits.Default,
                extensions));
    }

    private static MemoryOperationRecord CreateOperation()
    {
        var now = DateTimeOffset.Parse("2026-07-12T12:00:00Z");
        return MemoryOperationRecord.Create(
            MemoryOperationRecordId.New(),
            MemoryOperationId.New(),
            MemoryProviderInstanceId.Parse("provider.mcp-limit"),
            MemoryCapabilityIds.ContextQuerySync,
            MemoryOperationKind.ContextQuery,
            new MemoryLedgerRequester("test", null, null, null, null, null, null, null),
            MemoryCorrelationId.New(),
            MemoryCausationId.New(),
            [],
            MemoryLedgerRetentionPolicy.Expiring(now.AddHours(1), now.AddHours(2)),
            now);
    }

    private static JsonElement JsonString(string value) =>
        JsonSerializer.SerializeToElement(value);

    private sealed class OversizedMcpClientFactory(string responseJson) : IMcpClientFactory
    {
        public OversizedMcpClient Client { get; } = new(responseJson);

        public Task<IMcpRuntimeClient> CreateAsync(
            McpServerDescriptor descriptor,
            string correlationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IMcpRuntimeClient>(Client);
    }

    private sealed class OversizedMcpClient(string responseJson) : IMcpRuntimeClient
    {
        public bool Stopped { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<DiscoveredMcpTool>> ListToolsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DiscoveredMcpTool>>([]);

        public Task<string> CallToolAsync(
            McpToolName toolName,
            string jsonArguments,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseJson);

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stopped = true;
            return Task.CompletedTask;
        }
    }
}
