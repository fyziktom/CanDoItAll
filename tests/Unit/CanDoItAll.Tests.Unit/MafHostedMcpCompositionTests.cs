using System.Text.Json;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Security.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class MafHostedMcpCompositionTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_hosted_composition_preserves_allowed_tools_secret_binding_and_approval_mode(bool suppressApproval) {
        var secretId = Guid.NewGuid();
        var resolver = new SecretResolver(secretId);
        var services = MafRuntimeTestServices.CreateProviderRuntimeServiceCollection();
        services.AddSingleton<ISecretRuntimeResolver>(resolver);
        using var provider = services.BuildServiceProvider();
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), provider);
        var configuration = new McpCapabilityConfiguration {
            Hosted = true, ServerName = "workspace", Endpoint = "https://mcp.example.test",
            AllowedTools = ["read_file"], ApprovalMode = "AlwaysRequire",
            HeaderBindings = new Dictionary<string, string> { ["Authorization"] = $"secret:{secretId:D}" }
        };
        var capability = new CapabilityCatalogItem(Guid.NewGuid(), CapabilityKind.McpServer, "hosted-workspace", "Hosted workspace",
            "Hosted fixture", string.Empty, JsonSerializer.Serialize(configuration), CapabilityProofStatus.Verified,
            string.Empty, DateTimeOffset.UtcNow, IsBuiltIn: false);
        var backend = new ProviderProfile(Guid.NewGuid(), "Unit Provider", ProviderKind.OpenAi, "https://api.openai.com/v1",
            "OPENAI_API_KEY", "gpt-4.1", ProviderTransportKind.Responses, true, true, true, false, true,
            "{}", string.Empty, "Not checked", null, []);
        var now = DateTimeOffset.UtcNow;
        var agent = new AgentDefinition(Guid.NewGuid(), "Hosted tester", "Tester", "Hosted composition", "Use supplied tools.",
            AgentLifecycleStatus.Active, backend.Id, backend.DefaultModel, AgentWorkloadKind.Programming,
            AgentChatHistoryMode.FrameworkManaged, 0, false, false, "{}", false, string.Empty,
            AgentPermissionsPolicy.Default with {
                CanUseTools = true, CanAskOtherAgents = false, RequiresApprovalForExternalCalls = false,
                AllowedSecrets = [new AgentAllowedSecretReference(secretId, "MCP fixture", AgentSecretPurposes.GeneralAgentRequest)]
            }, [], [], now, now);
        var state = await runtime.CreateCapabilityStateAsync(agent, backend, [capability], [],
            WorkspaceRuntimeServicesTestFactory.Create(Path.GetTempPath()), (_, _, _) => Task.CompletedTask,
            default, suppressApproval);
        try {
            var hosted = Assert.Single(state.Tools.OfType<HostedMcpServerTool>());
            Assert.Equal("workspace", hosted.ServerName);
            Assert.Equal(["read_file"], hosted.AllowedTools);
            Assert.Equal("resolved-fixture-secret", hosted.Headers!["Authorization"]);
            Assert.Equal(1, resolver.Calls);
            Assert.Equal(suppressApproval ? HostedMcpServerToolApprovalMode.NeverRequire : HostedMcpServerToolApprovalMode.AlwaysRequire,
                hosted.ApprovalMode);
            Assert.DoesNotContain("resolved-fixture-secret", JsonSerializer.Serialize(MafNativeToolContracts.Capture(hosted)), StringComparison.Ordinal);
        } finally {
            Assert.Empty(await state.DisposeAcquiredResourcesAsync());
        }
    }

    private sealed class SecretResolver(Guid secretId) : ISecretRuntimeResolver {
        internal int Calls { get; private set; }
        public Task<string?> ResolveValueAsync(SecretRuntimeRequest request, CancellationToken cancellationToken = default) {
            Assert.Equal(secretId, request.SecretId);
            Calls++;
            return Task.FromResult<string?>("resolved-fixture-secret");
        }
    }
}
