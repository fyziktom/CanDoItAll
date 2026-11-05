using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Models;

public static class AgentEditorModelSnapshot {
    public static void ApplyTo(AgentEditorModel source, AgentEditorModel target) {
        var value = Copy(source);
        target.Id = value.Id;
        target.ExpectedUpdatedAtUtc = value.ExpectedUpdatedAtUtc;
        target.Name = value.Name;
        target.RoleTitle = value.RoleTitle;
        target.Summary = value.Summary;
        target.Instructions = value.Instructions;
        target.AvatarImageUrl = value.AvatarImageUrl;
        target.Status = value.Status;
        target.ProviderProfileId = value.ProviderProfileId;
        target.Model = value.Model;
        target.ThinkingEffortOverride = value.ThinkingEffortOverride;
        target.IsThinkingEffortOverrideEdited = value.IsThinkingEffortOverrideEdited;
        target.Workload = value.Workload;
        target.ChatHistoryMode = value.ChatHistoryMode;
        target.Temperature = value.Temperature;
        target.RequirePerServiceCallChatHistoryPersistence = value.RequirePerServiceCallChatHistoryPersistence;
        target.EnableBackgroundResponses = value.EnableBackgroundResponses;
        target.ConfigurationJson = value.ConfigurationJson;
        target.IsTemplate = value.IsTemplate;
        target.TemplateKey = value.TemplateKey;
        target.Permissions = value.Permissions;
        target.AllowedSecretReferences = value.AllowedSecretReferences;
        target.SelectedCapabilityIds = value.SelectedCapabilityIds;
        target.Tags = value.Tags;
        target.ProjectStructureAccess = value.ProjectStructureAccess;
        target.ProcessAccess = value.ProcessAccess;
        target.WorkspaceToolAccess = value.WorkspaceToolAccess;
        target.ImageGenerationAccess = value.ImageGenerationAccess;
        target.VoiceAccess = value.VoiceAccess;
        target.MemoryAccess = value.MemoryAccess;
    }

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
