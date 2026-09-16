using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class ImageGenerationAgentRuntimeToolProviderTests {
    [Theory]
    [InlineData(AgentRuntimeToolProviderPurpose.InteractiveChat)]
    [InlineData(AgentRuntimeToolProviderPurpose.GovernedProcessAutomation)]
    [InlineData(AgentRuntimeToolProviderPurpose.AutoApprovedNonInteractive)]
    public async Task Explicit_image_metadata_preserves_existing_attachment_and_approval_for_every_supported_purpose(AgentRuntimeToolProviderPurpose purpose) {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var workspace = new ImageGenerationTempWorkspace();
        var image = CreateProvider(ProviderProfilePurpose.ImageGeneration);
        var provider = new ImageGenerationAgentRuntimeToolProvider(new InMemoryProviderProfileRegistry([image]),
            TestWorkspaceServices.CreatePathResolutionService(workspace.Path), new FakeAgentImageGenerationService(), services);
        var agent = CreateAgent(image.Id, AgentImageGenerationAccessMetadata.Write("{}", new() { CanGenerateImages = true }));
        var context = CreateContext(agent, image) with { Purpose = purpose };
        var tool = Assert.Single(await provider.CreateToolsAsync(context, default));
        var metadata = Assert.Single(provider.GetToolMetadata(context));
        Assert.Equal(ImageGenerationToolPolicy.ImageGenerationCreate, tool.Name);
        Assert.Equal(tool.Name, metadata.ToolName);
        Assert.Equal(provider.Descriptor.ProviderKey, metadata.ProviderKey);
        Assert.Equal(provider.Descriptor.DomainTags, metadata.OwnershipTags);
        Assert.Equal(AgentRuntimeToolOperationKind.Mutation, metadata.OperationKind);
        Assert.True(metadata.RequiresApprovalByDefault);
        Assert.NotNull(metadata.AuthorizeResultDisclosureAsync);
        Assert.Null(metadata.PrepareAdmission);
        Assert.Null(metadata.AuthorizeAdmissionAsync);
        var prior = new AgentRuntimeToolMetadata(provider.Descriptor.ProviderKey, tool.Name,
            AgentRuntimeToolOperationKind.Mutation, true, provider.Descriptor.DomainTags);
        Assert.Equal(JsonSerializer.Serialize(prior), JsonSerializer.Serialize(metadata));
    }

    [Fact]
    public async Task An_admitted_image_tool_fails_explicitly_when_its_owner_capture_is_not_registered() {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var workspace = new ImageGenerationTempWorkspace();
        var image = CreateProvider(ProviderProfilePurpose.ImageGeneration);
        var generation = new FakeAgentImageGenerationService();
        var provider = new ImageGenerationAgentRuntimeToolProvider(new InMemoryProviderProfileRegistry([image]),
            TestWorkspaceServices.CreatePathResolutionService(workspace.Path), generation, services);
        var agent = CreateAgent(image.Id, AgentImageGenerationAccessMetadata.Write("{}", new() { CanGenerateImages = true }));
        var context = CreateContext(agent, image) with {
            AdmittedToolSession = new(Guid.NewGuid(), Guid.NewGuid(), AgentExecutionAuthorityId.Create()),
            ToolAdmissionSupport = AgentToolAdmissionSupport.Recoverable
        };
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => provider.CreateToolsAsync(context, default).AsTask());
        Assert.Equal("image-generation.result-authority-unavailable", denied.Code);
        Assert.Empty(generation.Requests);
        var disabled = context with { Agent = agent with { ConfigurationJson = "{}" } };
        Assert.Empty(await provider.CreateToolsAsync(disabled, default));
        Assert.Empty(provider.GetToolMetadata(disabled));
    }

    [Fact]
    public void Image_authority_roundtrip_preserves_original_profile_session_lifetimes_and_paths_without_image_bytes() {
        var evidence = Evidence();
        var envelope = ImageGenerationDisclosureEvidenceCodec.Write(evidence);
        var restored = ImageGenerationDisclosureEvidenceCodec.Read(envelope);
        Assert.Equal(evidence.Session, restored.Session);
        Assert.Equal(evidence.DatabaseProfileId, restored.DatabaseProfileId);
        Assert.Equal(evidence.AgentId, restored.AgentId);
        Assert.Equal(evidence.ProviderProfileId, restored.ProviderProfileId);
        Assert.Equal(evidence.State, restored.State);
        Assert.Equal(evidence.Output, restored.Output);
        Assert.Equal(evidence.SourceProjects.ToArray(), restored.SourceProjects.ToArray());
        Assert.Equal(evidence.SourcePaths.ToArray(), restored.SourcePaths.ToArray());
        Assert.DoesNotContain("Prompt", envelope.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Bytes", envelope.PayloadJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("different-owner", 1)]
    [InlineData("image-generation-result-authority", 2)]
    public void Unsupported_image_evidence_formats_are_explicit(string format, int version) {
        var original = ImageGenerationDisclosureEvidenceCodec.Write(Evidence());
        var changed = AgentToolProtocolEnvelope.Create(format, version, original.PayloadJson);
        Assert.Throws<InvalidDataException>(() => ImageGenerationDisclosureEvidenceCodec.Read(changed));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Malformed_image_evidence_cannot_supply_target_authority(int scenario) {
        var original = Evidence();
        var invalid = scenario switch {
            0 => original with { ProviderProfileId = Guid.Empty },
            1 => original with { SourceProjects = [new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())] },
            2 => original with { Output = original.Output with { RelativePath = original.Output.FullPath } },
            _ => original with { SourceProjects = original.SourceProjects.Add(original.SourceProjects[0]) }
        };
        Assert.Throws<InvalidDataException>(() => ImageGenerationDisclosureEvidenceCodec.Write(invalid));
    }

    private static ImageGenerationDisclosureEvidence Evidence() {
        var profile = Guid.NewGuid();
        return new(new(Guid.NewGuid(), Guid.NewGuid(), AgentExecutionAuthorityId.Create()), profile, Guid.NewGuid(),
            ImageGenerationDisclosureState.Complete, Guid.NewGuid(), new("output/image.png", Path.Combine(Path.GetTempPath(), "output", "image.png")),
            [new(profile, Guid.NewGuid(), Guid.NewGuid())],
            [new("data/source.png", Path.Combine(Path.GetTempPath(), "data", "source.png"))]);
    }
}
