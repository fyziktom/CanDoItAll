using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.AgentFramework.Editor.UI;

public static class AgentEditorDraftSnapshot {
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
            AllowedProjectIds = source.ProjectStructureAccess.AllowedProjectIds.ToList(),
            AllowedProjectLifetimes = source.ProjectStructureAccess.AllowedProjectLifetimes.ToList()
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
        CanScriptsReadEnvironment = source.CanScriptsReadEnvironment,
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
