using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentRuntimeToolRecoveryPolicyTests {
    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void Recovery_metadata_rejects_unknown_policies(int value) {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgentRuntimeToolMetadata("owner", "read", AgentRuntimeToolOperationKind.Read, false) {
            RecoveryPolicy = (AgentRuntimeToolRecoveryPolicy)value
        });
    }

    [Fact]
    public void Default_metadata_preserves_its_legacy_serialized_shape() {
        var metadata = new AgentRuntimeToolMetadata("owner", "read", AgentRuntimeToolOperationKind.Read, false);
        var json = JsonSerializer.SerializeToElement(metadata);
        Assert.Equal(AgentRuntimeToolRecoveryPolicy.Default, metadata.RecoveryPolicy);
        Assert.False(json.TryGetProperty(nameof(AgentRuntimeToolMetadata.RecoveryPolicy), out _));
    }

    [Fact]
    public void Image_analysis_alone_tightens_generic_recovery_without_changing_the_owner_policy_vector() {
        var owner = new ProjectStructureResultDisclosureService(null!, null!, null!, null!, null!, null!, null!, null!);
        var agent = new AgentDefinition(Guid.NewGuid(), "Metadata actor", string.Empty, string.Empty, string.Empty,
            AgentLifecycleStatus.Active, null, "model", default, default, 0, false, false, "{}", false, string.Empty,
            AgentPermissionsPolicy.Default, [], [], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        var provider = new ProviderProfile(Guid.NewGuid(), "Metadata provider", ProviderKind.Ollama, "http://localhost",
            string.Empty, "model", ProviderTransportKind.ChatCompletions, true, true, true, true, false, "{}", string.Empty, string.Empty, null, []);
        var context = new AgentRuntimeToolProviderContext(agent, provider, [], false,
            AgentRuntimeToolProviderPurpose.InteractiveChat, "metadata", AgentRuntimeContextIntent.Empty, new Dictionary<string, string>());
        var metadata = owner.Metadata(context, "project-structure");
        var metered = Assert.Single(metadata, item => item.RecoveryPolicy == AgentRuntimeToolRecoveryPolicy.ReconcileBeforeRetry);
        Assert.Equal(ProjectStructureToolPolicy.ProjectStructureAssetImageAnalyze, metered.ToolName);
        Assert.Equal(AgentRuntimeToolOperationKind.Read, metered.OperationKind);
        Assert.Null(metered.PrepareAdmission);
        Assert.Null(metered.AuthorizeAdmissionAsync);
        Assert.NotNull(metered.AuthorizeResultDisclosureAsync);
        Assert.All(metadata, item => {
            var policy = Assert.Single(ProjectStructureToolPolicy.Capabilities, policy => policy.Name == item.ToolName);
            Assert.Equal(policy.RequiresApprovalByDefault, item.RequiresApprovalByDefault);
            Assert.Equal(policy.IsStateChanging ? AgentRuntimeToolOperationKind.Mutation : AgentRuntimeToolOperationKind.Read, item.OperationKind);
            if (item != metered) {
                Assert.Equal(AgentRuntimeToolRecoveryPolicy.Default, item.RecoveryPolicy);
            }
        });
    }
}
