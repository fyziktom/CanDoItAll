using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Agents.Storage;

namespace CanDoItAll.Tests.Unit.Storage;

public sealed class StorageToolPolicyTests {
    [Theory]
    [InlineData(StorageToolPolicy.StorageCatalogList, false)]
    [InlineData(StorageToolPolicy.StorageBrowse, false)]
    [InlineData(StorageToolPolicy.StorageReadTextFile, false)]
    [InlineData(StorageToolPolicy.StorageWriteTextFile, true)]
    [InlineData(StorageToolPolicy.StorageDeleteObject, true)]
    public void Owner_preserves_legacy_storage_policy(string name, bool mutation) {
        Assert.False(ToolCapabilityRegistry.TryResolve(name, out _));
        var policy = Assert.Single(StorageToolPolicy.Capabilities, policy => policy.Name == name);
        Assert.Equal(mutation ? ToolInvocationClassification.Mutation : ToolInvocationClassification.Read, policy.Classification);
        Assert.Equal(mutation, policy.RequiresApprovalByDefault);
        Assert.Equal(mutation, policy.IsStateChanging);
        Assert.Equal(mutation ? ToolCapabilitySideEffectKind.InternalStateMutation : ToolCapabilitySideEffectKind.InternalDataRead, policy.SideEffectKind);
        Assert.Equal(ToolCapabilityOperationRequirementKind.None, policy.OperationRequirementKind);
        Assert.Empty(policy.OperationRequirements);
        Assert.Empty(policy.TargetScopeRequirements);
        Assert.False(policy.CanMutateProduct);
        Assert.False(policy.CanExecuteExternalAction);
        Assert.Equal(!mutation, policy.CanReadExternalTarget);
        Assert.False(policy.CanWriteManagedArtifact);
        Assert.Equal(ToolCapabilityBrowserProofRole.None, policy.BrowserProofRole);
        Assert.Equal(mutation ? ToolCapabilityIdempotencyDescriptor.StateChanging : ToolCapabilityIdempotencyDescriptor.Idempotent, policy.IdempotencyDescriptor);
        Assert.Null(policy.BusinessArgumentRetentionScheme);
        Assert.False(policy.ProtectRuntimeStateOnExport);
    }

    [Fact]
    public void Existing_provider_descriptors_default_to_runtime_phase_and_reject_unknown_phase() {
        var descriptor = new AgentRuntimeToolProviderDescriptor("provider", "Provider", string.Empty);
        Assert.Equal(AgentRuntimeToolAttachmentPhase.RuntimeProviders, descriptor.AttachmentPhase);
        Assert.DoesNotContain("attachmentPhase", System.Text.Json.JsonSerializer.Serialize(descriptor,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)), StringComparison.Ordinal);
        Assert.NotNull(typeof(AgentRuntimeToolProviderDescriptor).GetConstructor([
            typeof(string), typeof(string), typeof(string), typeof(IReadOnlyCollection<string>),
            typeof(IReadOnlyCollection<AgentRuntimeToolProviderPurpose>)
        ]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgentRuntimeToolProviderDescriptor("provider", "Provider", string.Empty) {
            AttachmentPhase = (AgentRuntimeToolAttachmentPhase)91
        });
    }
}
