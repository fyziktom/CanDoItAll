using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentEditorSubmission {
    private readonly byte[] originalState;
    private readonly Guid? originalId;
    private readonly DateTimeOffset? originalVersion;

    internal AgentEditorSubmission(AgentEditorModel original, AgentEditorModel request) {
        originalId = original.Id;
        originalVersion = original.ExpectedUpdatedAtUtc;
        originalState = JsonSerializer.SerializeToUtf8Bytes(original);
        Request = request;
    }

    public AgentEditorModel Request { get; }

    public bool HasLaterEdits(AgentEditorModel draft, IReadOnlyList<string> visibleTags) {
        var current = AgentEditorDraftPolicy.Copy(draft);
        current.Id = originalId;
        current.ExpectedUpdatedAtUtc = originalVersion;
        current.Tags = AgentEditorDraftPolicy.BuildTags(draft.Tags, visibleTags);
        return !originalState.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(current));
    }
}

public static class AgentEditorDraftPolicy {
    public static AgentEditorSubmission Capture(AgentEditorModel draft, IReadOnlyList<string> visibleTags,
        IReadOnlyList<ProviderProfile> providers) {
        var original = Copy(draft);
        original.Tags = BuildTags(draft.Tags, visibleTags);
        var request = Copy(original);
        var runtime = providers.FirstOrDefault(provider => provider.Id == request.ProviderProfileId);
        request.Model = ProviderModelValuePolicy.Normalize(request.Model);
        var image = request.ImageGenerationAccess.PreferredProviderProfileId is { } imageProviderId
            ? providers.FirstOrDefault(provider => provider.Id == imageProviderId)
            : ImageGenerationProviderSelectionPolicy.ResolveDefault(providers, runtime);
        var access = AgentImageGenerationAccessMetadata.Normalize(request.ImageGenerationAccess);
        if (access.CanGenerateImages && !access.PreferredProviderProfileId.HasValue && image is not null) {
            access.PreferredProviderProfileId = image.Id;
        }
        access.DefaultModel = ProviderModelValuePolicy.Normalize(access.DefaultModel);
        request.ImageGenerationAccess = AgentImageGenerationAccessMetadata.Normalize(access);
        request.ProjectStructureAccess = AgentProjectStructureAccessMetadata.Normalize(request.ProjectStructureAccess);
        return new(original, request);
    }

    public static List<string> BuildTags(IEnumerable<string> storedTags, IEnumerable<string> visibleTags)
        => visibleTags.Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag) && !AgentSpecialTags.IsFavorite(tag))
            .Concat(storedTags.Any(AgentSpecialTags.IsFavorite) ? [AgentSpecialTags.Favorite] : [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static AgentEditorModel Copy(AgentEditorModel source) => new() {
        Id = source.Id,
        ExpectedUpdatedAtUtc = source.ExpectedUpdatedAtUtc,
        Name = source.Name,
        RoleTitle = source.RoleTitle,
        Summary = source.Summary,
        Instructions = source.Instructions,
        AvatarImageUrl = source.AvatarImageUrl,
        Status = source.Status,
        ProviderProfileId = source.ProviderProfileId,
        Model = source.Model,
        ThinkingEffortOverride = source.ThinkingEffortOverride,
        IsThinkingEffortOverrideEdited = source.IsThinkingEffortOverrideEdited,
        Workload = source.Workload,
        ChatHistoryMode = source.ChatHistoryMode,
        Temperature = source.Temperature,
        RequirePerServiceCallChatHistoryPersistence = source.RequirePerServiceCallChatHistoryPersistence,
        EnableBackgroundResponses = source.EnableBackgroundResponses,
        ConfigurationJson = source.ConfigurationJson,
        IsTemplate = source.IsTemplate,
        TemplateKey = source.TemplateKey,
        Permissions = source.Permissions with { AllowedSecrets = source.Permissions.AllowedSecrets?.ToArray() },
        AllowedSecretReferences = source.AllowedSecretReferences.ToList(),
        SelectedCapabilityIds = source.SelectedCapabilityIds.ToList(),
        Tags = source.Tags.ToList(),
        ProjectStructureAccess = new() {
            CanRead = source.ProjectStructureAccess.CanRead,
            CanWrite = source.ProjectStructureAccess.CanWrite,
            CanWriteNonTaskStructure = source.ProjectStructureAccess.CanWriteNonTaskStructure,
            CanWriteTasks = source.ProjectStructureAccess.CanWriteTasks,
            CanCreateProjects = source.ProjectStructureAccess.CanCreateProjects,
            CanCreateSubprojects = source.ProjectStructureAccess.CanCreateSubprojects,
            AllowAllProjects = source.ProjectStructureAccess.AllowAllProjects,
            AllowedProjectIds = source.ProjectStructureAccess.AllowedProjectIds.ToList()
        },
        ProcessAccess = new() {
            CanRead = source.ProcessAccess.CanRead,
            CanWrite = source.ProcessAccess.CanWrite,
            AllowAllDefinitions = source.ProcessAccess.AllowAllDefinitions,
            AllowedDefinitionIds = source.ProcessAccess.AllowedDefinitionIds.ToList()
        },
        WorkspaceToolAccess = CopyWorkspaceAccess(source.WorkspaceToolAccess),
        ImageGenerationAccess = new() {
            CanGenerateImages = source.ImageGenerationAccess.CanGenerateImages,
            CanStoreImagesAsProjectAssets = source.ImageGenerationAccess.CanStoreImagesAsProjectAssets,
            PreferredProviderProfileId = source.ImageGenerationAccess.PreferredProviderProfileId,
            DefaultModel = source.ImageGenerationAccess.DefaultModel
        },
        VoiceAccess = new() {
            CanUseVoiceMode = source.VoiceAccess.CanUseVoiceMode,
            PreferredVoiceId = source.VoiceAccess.PreferredVoiceId
        },
        MemoryAccess = new() {
            InvocationMode = source.MemoryAccess.InvocationMode,
            CanUseMemoryTools = source.MemoryAccess.CanUseMemoryTools,
            RequireContextContributions = source.MemoryAccess.RequireContextContributions,
            AllowAsyncContextContributions = source.MemoryAccess.AllowAsyncContextContributions,
            CanIngestSources = source.MemoryAccess.CanIngestSources,
            PreferredProviderInstanceId = source.MemoryAccess.PreferredProviderInstanceId,
            DefaultProviderInstanceId = source.MemoryAccess.DefaultProviderInstanceId,
            AllowedProviderInstanceIds = source.MemoryAccess.AllowedProviderInstanceIds.ToArray(),
            ProviderBindings = source.MemoryAccess.ProviderBindings.ToArray(),
            AllowedCapabilityIds = source.MemoryAccess.AllowedCapabilityIds.ToArray(),
            DeniedCapabilityIds = source.MemoryAccess.DeniedCapabilityIds.ToArray(),
            AllowedSourceScopes = source.MemoryAccess.AllowedSourceScopes.ToArray(),
            ProviderAssignments = source.MemoryAccess.ProviderAssignments.ToArray()
        }
    };

    public static AgentWorkspaceToolAccessSettings CopyWorkspaceAccess(AgentWorkspaceToolAccessSettings source) => new() {
        Profile = source.Profile,
        CanReadFiles = source.CanReadFiles,
        CanWriteFiles = source.CanWriteFiles,
        CanRunValidationCommands = source.CanRunValidationCommands,
        CanRunLocalScripts = source.CanRunLocalScripts,
        CanScaffoldProjects = source.CanScaffoldProjects,
        CanManageWorkspacePaths = source.CanManageWorkspacePaths,
        CanTransformArtifacts = source.CanTransformArtifacts,
        AllowedExternalTargetAliases = source.AllowedExternalTargetAliases.ToList(),
        ExternalTargetRootBindings = source.ExternalTargetRootBindings.ToList(),
        CanReadStorage = source.CanReadStorage,
        CanWriteStorage = source.CanWriteStorage,
        AllowAllStorageCatalogs = source.AllowAllStorageCatalogs,
        AllowedStorageCatalogIds = source.AllowedStorageCatalogIds.ToList()
    };
}
