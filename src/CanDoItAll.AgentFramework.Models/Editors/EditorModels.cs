namespace CanDoItAll.AgentFramework.Models;

public sealed class AgentEditorModel
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RoleTitle { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string AvatarImageUrl { get; set; } = string.Empty;
    public AgentLifecycleStatus Status { get; set; } = AgentLifecycleStatus.Draft;
    public Guid? ProviderProfileId { get; set; }
    public string Model { get; set; } = string.Empty;
    public AgentWorkloadKind Workload { get; set; } = AgentWorkloadKind.General;
    public AgentChatHistoryMode ChatHistoryMode { get; set; } = AgentChatHistoryMode.ProviderDefault;
    public double Temperature { get; set; } = 0.2d;
    public bool RequirePerServiceCallChatHistoryPersistence { get; set; }
    public bool EnableBackgroundResponses { get; set; }
    public string ConfigurationJson { get; set; } = string.Empty;
    public bool IsTemplate { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public AgentPermissionsPolicy Permissions { get; set; } = AgentPermissionsPolicy.Default;
    public List<AgentAllowedSecretReference> AllowedSecretReferences { get; set; } = [];
    public AgentProjectStructureAccessSettings ProjectStructureAccess { get; set; } = new();
    public AgentProcessAccessSettings ProcessAccess { get; set; } = new();
    public AgentWorkspaceToolAccessSettings WorkspaceToolAccess { get; set; } = new();
    public AgentImageGenerationAccessSettings ImageGenerationAccess { get; set; } = new();
    public AgentVoiceAccessSettings VoiceAccess { get; set; } = new();
    public List<Guid> SelectedCapabilityIds { get; set; } = [];
    public List<string> Tags { get; set; } = [];

    public static AgentEditorModel FromDefinition(AgentDefinition definition)
    {
        return new AgentEditorModel
        {
            Id = definition.Id,
            Name = definition.Name,
            RoleTitle = definition.RoleTitle,
            Summary = definition.Summary,
            Instructions = definition.Instructions,
            AvatarImageUrl = definition.AvatarImageUrl ?? string.Empty,
            Status = definition.Status,
            ProviderProfileId = definition.ProviderProfileId,
            Model = definition.Model,
            Workload = definition.Workload,
            ChatHistoryMode = definition.ChatHistoryMode,
            Temperature = definition.Temperature,
            RequirePerServiceCallChatHistoryPersistence = definition.RequirePerServiceCallChatHistoryPersistence,
            EnableBackgroundResponses = definition.EnableBackgroundResponses,
            ConfigurationJson = definition.ConfigurationJson,
            IsTemplate = definition.IsTemplate,
            TemplateKey = definition.TemplateKey,
            Permissions = definition.Permissions,
            AllowedSecretReferences = definition.Permissions.NormalizedAllowedSecrets.ToList(),
            ProjectStructureAccess = AgentProjectStructureAccessMetadata.Read(definition.ConfigurationJson),
            ProcessAccess = AgentProcessAccessMetadata.Read(definition.ConfigurationJson),
            WorkspaceToolAccess = AgentWorkspaceToolAccessMetadata.Read(definition.ConfigurationJson),
            ImageGenerationAccess = AgentImageGenerationAccessMetadata.Read(definition.ConfigurationJson),
            VoiceAccess = AgentVoiceAccessMetadata.Read(definition.ConfigurationJson),
            SelectedCapabilityIds = definition.Capabilities.Select(item => item.CapabilityId).ToList(),
            Tags = definition.Tags.ToList()
        };
    }
}

public sealed class AgentTeamEditorModel
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<Guid> AgentIds { get; set; } = [];

    public static AgentTeamEditorModel FromDefinition(AgentTeamDefinition definition)
    {
        return new AgentTeamEditorModel
        {
            Id = definition.Id,
            Name = definition.Name,
            Description = definition.Description,
            AgentIds = definition.AgentIds.ToList()
        };
    }
}

public sealed class ProviderProfileEditorModel
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProviderKind Kind { get; set; } = ProviderKind.OpenAi;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKeyEnvironmentVariable { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = string.Empty;
    public ProviderTransportKind Transport { get; set; } = ProviderTransportKind.Responses;
    public ProviderProfilePurpose Purpose { get; set; } = ProviderProfilePurpose.Chat;
    public bool IsEnabled { get; set; } = true;
    public bool SupportsStreaming { get; set; } = true;
    public bool SupportsTools { get; set; } = true;
    public bool PreferFrameworkManagedChatHistory { get; set; }
    public bool SupportsBackgroundResponses { get; set; }
    public string ConfigurationJson { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsPrivateProvider { get; set; }
    public List<string> SuggestedModels { get; set; } = [];
    public List<ProviderModelTokenPriceEditorModel> ModelPrices { get; set; } = [];

    public static ProviderProfileEditorModel FromDefinition(ProviderProfile definition)
    {
        return new ProviderProfileEditorModel
        {
            Id = definition.Id,
            Name = definition.Name,
            Kind = definition.Kind,
            BaseUrl = definition.BaseUrl,
            ApiKeyEnvironmentVariable = definition.ApiKeyEnvironmentVariable,
            DefaultModel = definition.DefaultModel,
            Transport = definition.Transport,
            Purpose = definition.Purpose,
            IsEnabled = definition.IsEnabled,
            SupportsStreaming = definition.SupportsStreaming,
            SupportsTools = definition.SupportsTools,
            PreferFrameworkManagedChatHistory = definition.PreferFrameworkManagedChatHistory,
            SupportsBackgroundResponses = definition.SupportsBackgroundResponses,
            ConfigurationJson = definition.ConfigurationJson,
            Notes = definition.Notes,
            IsPrivateProvider = definition.IsPrivateProvider,
            SuggestedModels = definition.SuggestedModels.ToList(),
            ModelPrices = ProviderPricingDefaults.ToEditorModels(definition.ModelPrices)
        };
    }
}

public sealed class CapabilityEditorModel
{
    public Guid? Id { get; set; }
    public CapabilityKind Kind { get; set; } = CapabilityKind.Skill;
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string EndpointOrPath { get; set; } = string.Empty;
    public string ConfigurationJson { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; }

    public static CapabilityEditorModel FromDefinition(CapabilityCatalogItem definition)
    {
        return new CapabilityEditorModel
        {
            Id = definition.Id,
            Kind = definition.Kind,
            Key = definition.Key,
            Name = definition.Name,
            Description = definition.Description,
            EndpointOrPath = definition.EndpointOrPath,
            ConfigurationJson = definition.ConfigurationJson,
            IsBuiltIn = definition.IsBuiltIn
        };
    }
}

public sealed class MemoryEditorModel
{
    public Guid? Id { get; set; }
    public Guid AgentId { get; set; }
    public MemoryKind Kind { get; set; } = MemoryKind.Context;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Source { get; set; } = "manual";
    public int Importance { get; set; } = 3;
    public string MetadataJson { get; set; } = string.Empty;
}
