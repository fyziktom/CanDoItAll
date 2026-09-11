using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit;

public sealed class MafNativeToolContractTests {
    [Fact]
    public void A_local_function_declaration_does_not_require_a_remote_dispatch_barrier() {
        AIFunctionDeclaration function = AIFunctionFactory.Create(() => "read", "fixture_read");
        Assert.Empty(MafNativeToolContracts.Capture([function]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Hosted_contract_changes_when_its_execution_target_authority_or_options_change(int change) {
        var original = Tool();
        var changed = change switch {
            0 => Tool("https://other.example.test"),
            1 => Tool(allowed: ["read_file"]),
            2 => Tool(mode: HostedMcpServerToolApprovalMode.NeverRequire),
            3 => Tool(secret: "rotated-fixture-header"),
            4 => new HostedMcpServerTool("workspace", "https://mcp.example.test", new Dictionary<string, object?> { ["extension"] = 42 }) {
                AllowedTools = ["write_file"], ApprovalMode = HostedMcpServerToolApprovalMode.AlwaysRequire,
                Headers = new Dictionary<string, string> { ["Authorization"] = "fixture-header-secret" }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        Assert.NotEqual(MafNativeToolContracts.Capture(original), MafNativeToolContracts.Capture(changed));
        Assert.NotEqual(MafToolsetFingerprint.ComputeContractFingerprint([original]), MafToolsetFingerprint.ComputeContractFingerprint([changed]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Every_retained_built_in_native_family_binds_its_provider_options(int family) {
        var properties = new Dictionary<string, object?> { ["fixture-option"] = "changed" };
        (AITool First, AITool Changed) tools = family switch {
            0 => (new HostedCodeInterpreterTool(), new HostedCodeInterpreterTool(properties)),
            1 => (new HostedFileSearchTool(), new HostedFileSearchTool(properties)),
            2 => (new HostedWebSearchTool(), new HostedWebSearchTool(properties)),
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
        Assert.NotEqual(MafNativeToolContracts.Capture(tools.First), MafNativeToolContracts.Capture(tools.Changed));
    }

    [Fact]
    public void Native_approval_keeps_protocol_identity_but_does_not_persist_header_values() {
        var request = new ToolApprovalRequestContent("approval-42", new McpServerToolCallContent("call-42", "write_file", "workspace") {
            Arguments = new Dictionary<string, object?> { ["path"] = "reviewed.txt" }
        });
        var prepared = MafNativeToolContracts.PrepareApproval(request, [Tool()]);
        Assert.True(prepared.RequiresApproval);
        Assert.Equal("call-42", prepared.CallId);
        Assert.Equal("write_file", prepared.Payload.ToolName);
        Assert.DoesNotContain("fixture-header-secret", prepared.ProviderCall!.ProtocolCall.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("fixture-header-secret", prepared.Payload.ArgumentsJson, StringComparison.Ordinal);
        var restored = MafToolProtocolCodec.Decode<ToolApprovalRequestContent>(prepared.ProviderCall.ProtocolCall);
        Assert.Equal(request.RequestId, restored.RequestId);
        Assert.Equal(request.ToolCall.CallId, restored.ToolCall.CallId);
        Assert.Equal(prepared.Payload, MafNativeToolContracts.PrepareApproval(restored, [Tool()]).Payload);
    }

    [Theory]
    [InlineData("other-server", "write_file")]
    [InlineData("workspace", "delete_file")]
    public void Hosted_approval_cannot_escape_the_configured_server_or_allowed_tools(string server, string name) {
        var request = new ToolApprovalRequestContent("approval", new McpServerToolCallContent("call", name, server));
        Assert.Throws<AgentToolAdmissionException>(() => MafNativeToolContracts.PrepareApproval(request, [Tool()]));
    }

    private static HostedMcpServerTool Tool(string address = "https://mcp.example.test", string[]? allowed = null,
        HostedMcpServerToolApprovalMode? mode = null, string secret = "fixture-header-secret")
        => new("workspace", address) {
            AllowedTools = allowed ?? ["write_file"],
            ApprovalMode = mode ?? HostedMcpServerToolApprovalMode.AlwaysRequire,
            Headers = new Dictionary<string, string> { ["Authorization"] = secret }
        };
}
