using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentEditorDraftPolicyTests {
    [Fact]
    public void Capture_preserves_version_and_owns_nested_mutable_state() {
        var id = Guid.NewGuid();
        var version = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        var secret = new AgentAllowedSecretReference(Guid.NewGuid(), "Reference", AgentSecretPurposes.GeneralAgentRequest);
        var permissionSecrets = new List<AgentAllowedSecretReference> { secret };
        var providerIds = new List<MemoryProviderInstanceId> { MemoryProviderInstanceId.Parse("provider.snapshot") };
        var draft = new AgentEditorModel {
            Id = id,
            ExpectedUpdatedAtUtc = version,
            Name = "Submitted",
            IsThinkingEffortOverrideEdited = true,
            Permissions = AgentPermissionsPolicy.Default with { AllowedSecrets = permissionSecrets },
            AllowedSecretReferences = [secret],
            SelectedCapabilityIds = [Guid.NewGuid()],
            ProjectStructureAccess = new() { CanRead = true, AllowedProjectIds = [Guid.NewGuid()] },
            ProcessAccess = new() { CanRead = true, AllowedDefinitionIds = [Guid.NewGuid()] },
            WorkspaceToolAccess = new() { CanReadStorage = true, AllowedStorageCatalogIds = [Guid.NewGuid()] },
            ImageGenerationAccess = new() { CanGenerateImages = true, DefaultModel = "image-model" },
            VoiceAccess = new() { CanUseVoiceMode = true, PreferredVoiceId = "alloy" },
            MemoryAccess = new() { InvocationMode = AgentMemoryInvocationMode.Automatic, AllowedProviderInstanceIds = providerIds }
        };
        var submission = AgentEditorDraftPolicy.Capture(draft, ["snapshot"], []);
        draft.Name = "Later";
        permissionSecrets.Clear();
        draft.AllowedSecretReferences.Clear();
        draft.SelectedCapabilityIds.Clear();
        draft.ProjectStructureAccess.AllowedProjectIds.Clear();
        draft.ProcessAccess.AllowedDefinitionIds.Clear();
        draft.WorkspaceToolAccess.AllowedStorageCatalogIds.Clear();
        draft.ImageGenerationAccess.DefaultModel = "later-image";
        draft.VoiceAccess.PreferredVoiceId = "later-voice";
        providerIds.Clear();
        var request = submission.Request;
        Assert.Equal(id, request.Id);
        Assert.Equal(version, request.ExpectedUpdatedAtUtc);
        Assert.Equal("Submitted", request.Name);
        Assert.True(request.IsThinkingEffortOverrideEdited);
        Assert.Single(request.Permissions.NormalizedAllowedSecrets);
        Assert.Single(request.AllowedSecretReferences);
        Assert.Single(request.SelectedCapabilityIds);
        Assert.Single(request.ProjectStructureAccess.AllowedProjectIds);
        Assert.Single(request.ProcessAccess.AllowedDefinitionIds);
        Assert.Single(request.WorkspaceToolAccess.AllowedStorageCatalogIds);
        Assert.Equal("image-model", request.ImageGenerationAccess.DefaultModel);
        Assert.Equal("alloy", request.VoiceAccess.PreferredVoiceId);
        Assert.Single(request.MemoryAccess.AllowedProviderInstanceIds);
        Assert.True(submission.HasLaterEdits(draft, ["snapshot"]));
    }

    [Fact]
    public void Capture_normalizes_tags_and_defaults_without_changing_visible_draft() {
        var projectId = Guid.NewGuid();
        var draft = new AgentEditorModel {
            Model = "  runtime  ",
            Tags = [AgentSpecialTags.Favorite],
            ImageGenerationAccess = new() { CanGenerateImages = true, DefaultModel = "  image  " },
            ProjectStructureAccess = new() { CanWriteTasks = true, AllowedProjectIds = [Guid.Empty, projectId, projectId] }
        };
        var submission = AgentEditorDraftPolicy.Capture(draft, ["  review ", "REVIEW", "", AgentSpecialTags.Favorite], []);
        Assert.Equal("runtime", submission.Request.Model);
        Assert.Equal("image", submission.Request.ImageGenerationAccess.DefaultModel);
        Assert.True(submission.Request.ProjectStructureAccess.CanRead);
        Assert.Equal(projectId, Assert.Single(submission.Request.ProjectStructureAccess.AllowedProjectIds));
        Assert.Equal(2, submission.Request.Tags.Count);
        Assert.Contains(AgentSpecialTags.Favorite, submission.Request.Tags);
        Assert.Contains("review", submission.Request.Tags);
        Assert.Equal("  runtime  ", draft.Model);
        Assert.Equal("  image  ", draft.ImageGenerationAccess.DefaultModel);
        Assert.False(draft.ProjectStructureAccess.CanRead);
        Assert.Equal(3, draft.ProjectStructureAccess.AllowedProjectIds.Count);
        draft.Id = Guid.NewGuid();
        draft.ExpectedUpdatedAtUtc = DateTimeOffset.UtcNow;
        Assert.False(submission.HasLaterEdits(draft, ["review"]));
        Assert.True(submission.HasLaterEdits(draft, ["later tag"]));
    }
}
