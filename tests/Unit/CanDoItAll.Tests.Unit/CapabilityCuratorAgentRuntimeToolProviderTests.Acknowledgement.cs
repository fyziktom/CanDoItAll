using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.AI;
using ModelCapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class CapabilityCuratorAgentRuntimeToolProviderTests {
    [Theory]
    [InlineData(CapabilityCuratorToolPolicy.CapabilityCuratorSave, CuratorOwnerAcknowledgementFault.BeforeOwner)]
    [InlineData(CapabilityCuratorToolPolicy.CapabilityCuratorSave, CuratorOwnerAcknowledgementFault.BeforeReceipt)]
    [InlineData(CapabilityCuratorToolPolicy.CapabilityCuratorSave, CuratorOwnerAcknowledgementFault.AfterReceipt)]
    [InlineData(CapabilityCuratorToolPolicy.CapabilityCuratorAssignmentUpdate, CuratorOwnerAcknowledgementFault.BeforeOwner)]
    [InlineData(CapabilityCuratorToolPolicy.CapabilityCuratorAssignmentUpdate, CuratorOwnerAcknowledgementFault.BeforeReceipt)]
    [InlineData(CapabilityCuratorToolPolicy.CapabilityCuratorAssignmentUpdate, CuratorOwnerAcknowledgementFault.AfterReceipt)]
    [InlineData(CapabilityCuratorToolPolicy.CapabilityCuratorVerify, CuratorOwnerAcknowledgementFault.BeforeOwner)]
    [InlineData(CapabilityCuratorToolPolicy.CapabilityCuratorVerify, CuratorOwnerAcknowledgementFault.BeforeReceipt)]
    [InlineData(CapabilityCuratorToolPolicy.CapabilityCuratorVerify, CuratorOwnerAcknowledgementFault.AfterReceipt)]
    public async Task Capability_owner_acknowledgements_preserve_the_write_identity_across_followup_failure(
        string toolName, CuratorOwnerAcknowledgementFault fault) {
        var harness = CreateHarness();
        var tools = await CreateToolDictionaryAsync(harness);
        var target = harness.Workspace.Agents.Single(item => item.Id == harness.TargetAgentId);
        var assignment = new CapabilityCuratorAssignmentUpdateInput(harness.TargetAgentId, harness.CustomCapabilityId,
            CapabilityCuratorAssignmentAction.Attach, target.UpdatedAtUtc);
        if (toolName == CapabilityCuratorToolPolicy.CapabilityCuratorVerify) {
            await InvokeAsync<CapabilityCuratorAssignmentUpdateResult>(tools[CapabilityCuratorToolPolicy.CapabilityCuratorAssignmentUpdate], assignment);
        }
        object request = toolName switch {
            CapabilityCuratorToolPolicy.CapabilityCuratorSave => CreateInlineSkillCandidate("acknowledged-skill", "Acknowledged skill", "Exact instructions."),
            CapabilityCuratorToolPolicy.CapabilityCuratorAssignmentUpdate => assignment,
            CapabilityCuratorToolPolicy.CapabilityCuratorVerify => new CapabilityCuratorVerifyInput(harness.TargetAgentId, harness.CustomCapabilityId),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName))
        };
        harness.Workspace.ResetAcknowledgementState();
        harness.Workspace.AcknowledgementFault = fault;
        using var capture = AgentToolInvocationEffectScope.Begin();
        await Assert.ThrowsAsync<IOException>(() => Assert.IsAssignableFrom<AIFunction>(tools[toolName])
            .InvokeAsync(new AIFunctionArguments { ["request"] = request }).AsTask());
        Assert.Equal(fault != CuratorOwnerAcknowledgementFault.BeforeOwner, harness.Workspace.WriteCompleted);
        if (fault != CuratorOwnerAcknowledgementFault.AfterReceipt) {
            Assert.Null(capture.CommittedEffect);
            return;
        }
        var expectedId = toolName switch {
            CapabilityCuratorToolPolicy.CapabilityCuratorSave => harness.Workspace.Capabilities.Single(item => item.Key == "acknowledged-skill").Id,
            CapabilityCuratorToolPolicy.CapabilityCuratorAssignmentUpdate => harness.TargetAgentId,
            _ => harness.CustomCapabilityId
        };
        Assert.Equal(new AgentToolCommittedEffect(toolName == CapabilityCuratorToolPolicy.CapabilityCuratorAssignmentUpdate
            ? "agent-catalog" : "capability-catalog", expectedId.ToString("D")), capture.CommittedEffect);
    }

    [Fact]
    public async Task Failed_tool_setup_does_not_gain_committed_evidence_or_a_setup_attestation() {
        var harness = CreateHarness();
        harness.SetupFlow.ToolSetupSucceeds = false;
        var tools = await CreateToolDictionaryAsync(harness);
        var result = await InvokeAsync<CapabilityCuratorToolSetupTestResult>(tools[CapabilityCuratorToolPolicy.CapabilityCuratorToolSetupTest],
            new CapabilityCuratorCapabilitySetupTestInput(CreateAcknowledgementToolCandidate()));
        Assert.False(result.SetupResult.IsSuccess);
        Assert.Null(result.Attestation);
        Assert.Single(harness.SetupFlow.ToolRequests);
        Assert.Equal(0, harness.Workspace.SaveCapabilityCallCount);
    }

    [Fact]
    public async Task Setup_acknowledgement_for_a_different_owner_identity_is_rejected() {
        var harness = CreateHarness();
        harness.SetupFlow.ReturnDifferentSetupIdentity = true;
        var tools = await CreateToolDictionaryAsync(harness);
        using var capture = AgentToolInvocationEffectScope.Begin();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Assert.IsAssignableFrom<AIFunction>(tools[CapabilityCuratorToolPolicy.CapabilityCuratorToolSetupTest])
                .InvokeAsync(new AIFunctionArguments {
                    ["request"] = new CapabilityCuratorCapabilitySetupTestInput(CreateAcknowledgementToolCandidate())
                }).AsTask());
        Assert.Contains("different capability identity", failure.Message, StringComparison.Ordinal);
        Assert.Null(capture.CommittedEffect);
        Assert.Single(harness.SetupFlow.ToolRequests);
    }

    [Fact]
    public async Task MCP_cleanup_failure_is_not_a_successful_setup_acknowledgement() {
        var harness = CreateHarness();
        harness.SetupFlow.McpCleanupCompleted = false;
        var tools = await CreateToolDictionaryAsync(harness);
        var candidate = new CapabilityCuratorSaveInput(null, null, ModelCapabilityKind.McpServer,
            "acknowledged-mcp", "Acknowledged MCP", "Synthetic setup boundary.",
            McpConfiguration: new CapabilityCuratorMcpConfigurationInput(CapabilityCuratorMcpTransport.Stdio,
                Command: "node", Arguments: ["server.js"], WorkingDirectory: ".", AllowedTools: ["read"],
                ApprovalMode: McpApprovalMode.AlwaysRequire));
        var result = await InvokeAsync<CapabilityCuratorMcpSetupTestResult>(tools[CapabilityCuratorToolPolicy.CapabilityCuratorMcpSetupTest],
            new CapabilityCuratorCapabilitySetupTestInput(candidate));
        Assert.False(result.SetupResult.IsSuccess);
        Assert.False(result.SetupResult.CleanupCompleted);
        Assert.Null(result.Attestation);
        Assert.Single(harness.SetupFlow.McpRequests);
    }

    private static CapabilityCuratorSaveInput CreateAcknowledgementToolCandidate() => new(null, null, ModelCapabilityKind.Tool,
        "acknowledged-tool", "Acknowledged tool", "Synthetic setup boundary.",
        ToolConfiguration: new CapabilityCuratorToolConfigurationInput(CapabilityCuratorToolKind.ExternalProcess,
            "acknowledged_tool", "external.acknowledged-tool", ExternalProcess: new CapabilityCuratorExternalProcessToolInput(
                "dotnet", ["--info"], AllowedExecutableNames: ["dotnet"])));

    private static void AssertOwnerAcknowledgement<T>(string toolName, T result, AgentToolCommittedEffect? actual) {
        AgentToolCommittedEffect? expected = toolName switch {
            CapabilityCuratorToolPolicy.CapabilityCuratorSave =>
                new("capability-catalog", Assert.IsType<CapabilityCuratorEditorResult>(result).CapabilityId.ToString("D")),
            CapabilityCuratorToolPolicy.CapabilityCuratorAssignmentUpdate =>
                new("agent-catalog", Assert.IsType<CapabilityCuratorAssignmentUpdateResult>(result).AgentId.ToString("D")),
            CapabilityCuratorToolPolicy.CapabilityCuratorVerify =>
                new("capability-catalog", Assert.IsType<CapabilityCuratorVerifyResult>(result).CapabilityId.ToString("D")),
            CapabilityCuratorToolPolicy.CapabilityCuratorToolSetupTest when result is CapabilityCuratorToolSetupTestResult { SetupResult.IsSuccess: true } tool =>
                new("capability-tool-setup", tool.SetupResult.Identity.Key.Value),
            CapabilityCuratorToolPolicy.CapabilityCuratorMcpSetupTest when result is CapabilityCuratorMcpSetupTestResult { SetupResult.IsSuccess: true, SetupResult.CleanupCompleted: true } mcp =>
                new("capability-mcp-setup", mcp.SetupResult.Identity.Key.Value),
            _ => null
        };
        Assert.Equal(expected, actual);
    }
}
