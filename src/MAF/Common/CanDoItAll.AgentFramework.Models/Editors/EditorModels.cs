namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Editable form of a technical agent definition, returned by <c>GET /api/agents/{agentId}</c> and accepted by
/// <c>POST /api/agents</c> and <c>PUT /api/agents/by-external-key/{externalNamespace}/{key}</c>. A save replaces the
/// whole agent with these values: an omitted member takes its default, so send every member as read and change only
/// what you intend to change. The external-key operation additionally requires <c>id</c> and
/// <c>expectedUpdatedAtUtc</c> to be null, <c>allowedSecretReferences</c> and <c>permissions.allowedSecrets</c> to be
/// empty, <c>selectedCapabilityIds</c> and <c>tags</c> to be present, and <c>configurationJson</c> to contain no raw
/// secret values. Enum members are JSON integers.
/// </summary>
public sealed class AgentEditorModel
{
    /// <summary>
    /// Identifier of the agent. Null for a new agent, which receives a server-generated identifier; the identifier of
    /// an existing agent to replace it. An identifier that does not exist yet creates the agent with that identifier.
    /// Must be null for the external-key operation.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Concurrency revision from the latest read (the agent's <c>updatedAtUtc</c>), sent back unchanged. When present,
    /// the save is rejected with HTTP 400 unless it equals the stored revision exactly, including when the agent does
    /// not exist. Null skips the check: use null for a create; for an update it overwrites without protection. Must be
    /// null for the external-key operation.
    /// </summary>
    public DateTimeOffset? ExpectedUpdatedAtUtc { get; set; }

    /// <summary>
    /// Display name, trimmed. When <c>templateKey</c> is blank the agent's canonical key is derived from it.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Role title shown with the agent, trimmed; may be empty.</summary>
    public string RoleTitle { get; set; } = string.Empty;

    /// <summary>Short description of the agent, trimmed; may be empty.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>System instructions sent to the model on every run, trimmed.</summary>
    public string Instructions { get; set; } = string.Empty;

    /// <summary>
    /// Avatar image reference, trimmed; blank stores no avatar. The product uses a bundled avatar path or a
    /// <c>data:</c> image URL; the API does not validate the text.
    /// </summary>
    public string AvatarImageUrl { get; set; } = string.Empty;

    /// <summary>
    /// Lifecycle status, as a JSON integer: 0 Draft (the default), 1 Active, 2 Suspended, 3 Archived. Many runtime
    /// features require an Active agent that is not a template.
    /// </summary>
    public AgentLifecycleStatus Status { get; set; } = AgentLifecycleStatus.Draft;

    /// <summary>
    /// Identifier of the provider profile the agent uses (see <c>GET /api/agents/providers</c>), or null. A profile
    /// that does not exist is rejected.
    /// </summary>
    public Guid? ProviderProfileId { get; set; }

    /// <summary>
    /// Model identifier within the provider profile, trimmed; blank uses the profile's default model. A shared
    /// (source-managed) profile only accepts the models it publishes, and a local profile only accepts a model that has
    /// a price row on the profile.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Reasoning effort requested for the agent's model, as a JSON integer: 0 None, 1 Low, 2 Medium, 3 High,
    /// 4 ExtraHigh, 5 Max, 6 Minimal; null uses the provider default. A value requires a provider profile and a model
    /// whose thinking-effort capability allows it; otherwise the save is rejected. It is stored in
    /// <c>configurationJson</c> under <c>modelParameters.reasoningEffort</c>.
    /// </summary>
    public AgentReasoningEffortLevel? ThinkingEffortOverride { get; set; }

    /// <summary>
    /// Request-only flag that matters for Ollama provider profiles: true when a null <c>thinkingEffortOverride</c>
    /// should clear the effort stored in <c>configurationJson</c>. When false and the override is null, the stored
    /// effort is kept. Reads always return false.
    /// </summary>
    public bool IsThinkingEffortOverrideEdited { get; set; }

    /// <summary>
    /// Kind of work the agent does, as a JSON integer: 0 General (the default), 1 Management, 2 Hr, 3 Sales,
    /// 4 Assistant, 5 Qa, 6 Support, 7 Programming, 8 Spreadsheet, 9 Mail, 10 Research.
    /// </summary>
    public AgentWorkloadKind Workload { get; set; } = AgentWorkloadKind.General;

    /// <summary>
    /// Where the chat history is kept between turns, as a JSON integer: 0 ProviderDefault (the default), 1
    /// FrameworkManaged, 2 ProviderManaged.
    /// </summary>
    public AgentChatHistoryMode ChatHistoryMode { get; set; } = AgentChatHistoryMode.ProviderDefault;

    /// <summary>Sampling temperature sent to the model; 0.2 by default. The server does not check its range.</summary>
    public double Temperature { get; set; } = 0.2d;

    /// <summary>True to persist the chat history after every model call of a run.</summary>
    public bool RequirePerServiceCallChatHistoryPersistence { get; set; }

    /// <summary>True to request background (long-running) model responses when the provider supports them.</summary>
    public bool EnableBackgroundResponses { get; set; }

    /// <summary>
    /// Further configuration as a JSON string containing a JSON object; send back the value as read. On save each typed
    /// access setting below replaces its key in this object (<c>projectStructure</c>, <c>processes</c>,
    /// <c>workspaceTools</c>, <c>imageGeneration</c>, <c>voiceAccess</c>, <c>memory</c>) and the thinking effort
    /// replaces <c>modelParameters.reasoningEffort</c>; other keys are kept. Text that is not a JSON object is replaced
    /// by an empty object.
    /// </summary>
    public string ConfigurationJson { get; set; } = string.Empty;

    /// <summary>True to store the agent as a template used to create working agents.</summary>
    public bool IsTemplate { get; set; }

    /// <summary>
    /// Canonical key of the agent; blank derives it from <c>name</c>. It is normalized to lower-case letters and digits
    /// separated by hyphens, must contain a letter or digit and must be unique among all agents and templates, so two
    /// agents whose names normalize alike need different explicit keys.
    /// </summary>
    public string TemplateKey { get; set; } = string.Empty;

    /// <summary>
    /// Runtime permissions of the agent's runs. When omitted, the default permissions apply. Its
    /// <c>allowedSecrets</c> member is ignored on save; send secrets in <c>allowedSecretReferences</c>.
    /// </summary>
    public AgentPermissionsPolicy Permissions { get; set; } = AgentPermissionsPolicy.Default;

    /// <summary>
    /// Secrets the agent may use; the list replaces the stored one. Entries with the all-zero identifier are dropped
    /// and the last entry per secret wins.
    /// </summary>
    public List<AgentAllowedSecretReference> AllowedSecretReferences { get; set; } = [];

    /// <summary>
    /// Project Structure tool access; replaces the stored section, and an omitted object removes the access.
    /// </summary>
    public AgentProjectStructureAccessSettings ProjectStructureAccess { get; set; } = new();

    /// <summary>
    /// Processes workspace access; replaces the stored section, and an omitted object removes the access.
    /// </summary>
    public AgentProcessAccessSettings ProcessAccess { get; set; } = new();

    /// <summary>
    /// Workspace file, command and storage tool access; replaces the stored section, and an omitted object resets it
    /// to the default (file reading only).
    /// </summary>
    public AgentWorkspaceToolAccessSettings WorkspaceToolAccess { get; set; } = new();

    /// <summary>
    /// Image generation tool access; replaces the stored section, and an omitted object removes the access.
    /// </summary>
    public AgentImageGenerationAccessSettings ImageGenerationAccess { get; set; } = new();

    /// <summary>Voice settings; replace the stored section, and an omitted object removes them.</summary>
    public AgentVoiceAccessSettings VoiceAccess { get; set; } = new();

    /// <summary>
    /// Memory provider access; replaces the stored section, and an omitted object removes all memory access. See the
    /// schema for the current limitation on provider bindings.
    /// </summary>
    public AgentMemoryAccessSettings MemoryAccess { get; set; } = new();

    /// <summary>
    /// Identifiers of the catalog capabilities to assign; the list replaces the current assignments. Unknown
    /// identifiers are ignored. A kept assignment keeps its proof state; a new one starts with the catalog
    /// capability's.
    /// </summary>
    public List<Guid> SelectedCapabilityIds { get; set; } = [];

    /// <summary>Free-form labels; trimmed, blanks dropped and duplicates removed ignoring case.</summary>
    public List<string> Tags { get; set; } = [];

    public static AgentEditorModel FromDefinition(
        AgentDefinition definition,
        ProviderKind? providerKind = null)
    {
        return new AgentEditorModel
        {
            Id = definition.Id,
            ExpectedUpdatedAtUtc = definition.UpdatedAtUtc,
            Name = definition.Name,
            RoleTitle = definition.RoleTitle,
            Summary = definition.Summary,
            Instructions = definition.Instructions,
            AvatarImageUrl = definition.AvatarImageUrl ?? string.Empty,
            Status = definition.Status,
            ProviderProfileId = definition.ProviderProfileId,
            Model = definition.Model,
            ThinkingEffortOverride = AgentThinkingEffortPolicy.ReadConfiguredEffort(
                definition.ConfigurationJson,
                "agent",
                includeLegacyOllamaThink: providerKind == ProviderKind.Ollama),
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
            MemoryAccess = AgentMemoryAccessMetadata.Read(definition.ConfigurationJson),
            SelectedCapabilityIds = definition.Capabilities.Select(item => item.CapabilityId).ToList(),
            Tags = definition.Tags.ToList()
        };
    }
}

/// <summary>
/// Editable form of an agent team, returned by <c>GET /api/agents/teams/{teamId}/editor</c> and accepted by
/// <c>POST /api/agents/teams</c> and <c>PUT /api/agents/teams/{teamId}</c>. A save replaces the whole team with these
/// values; there is no concurrency token, so the last save wins.
/// </summary>
public sealed class AgentTeamEditorModel
{
    /// <summary>
    /// Identifier of the team. Null or omitted on <c>POST /api/agents/teams</c> creates a team with a new
    /// identifier; a value replaces that team or creates it with this identifier. <c>PUT</c> ignores this member and
    /// uses the route identifier. Never send the all-zero identifier: the save reports success but the team is
    /// discarded.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Team name. It is trimmed and must be non-blank and unique among teams, ignoring case.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Free-text description, trimmed. Empty when omitted.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Name of the team's icon, for example <c>groups</c>, <c>hub</c>, <c>code</c> or <c>security</c>. A blank or
    /// unsupported name is stored as <c>groups</c>, which is also the default.
    /// </summary>
    public string Icon { get; set; } = AgentTeamIconCatalog.DefaultIcon;

    /// <summary>
    /// Identifiers of the member agents; the list replaces the current members. Every identifier must name an existing
    /// agent; empty identifiers and duplicates are dropped. An empty list or an omitted member stores a team without
    /// members.
    /// </summary>
    public List<Guid> AgentIds { get; set; } = [];

    public static AgentTeamEditorModel FromDefinition(AgentTeamDefinition definition)
    {
        return new AgentTeamEditorModel
        {
            Id = definition.Id,
            Name = definition.Name,
            Description = definition.Description,
            Icon = AgentTeamIconCatalog.Normalize(definition.Icon),
            AgentIds = definition.AgentIds.ToList()
        };
    }
}

public interface IProviderModelPricingEditorModel
{
    bool IsPrivateProvider { get; set; }

    List<ProviderModelTokenPriceEditorModel> ModelPrices { get; set; }
}

/// <summary>
/// Editable form of a local provider profile, read from <c>GET /api/agents/providers/{providerId}/editor</c> and sent
/// whole to <c>POST /api/agents/providers</c>. A save replaces the stored profile with this form; profiles imported
/// from a shared provider source can be read but not saved. Never put a raw API key in any member.
/// </summary>
public sealed class ProviderProfileEditorModel : IProviderModelPricingEditorModel
{
    /// <summary>
    /// Identifier of the profile: null (the server assigns one) or a new identifier to create a profile, or the
    /// identifier of an existing local profile to replace it. The all-zero GUID is rejected.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Concurrency token from the editor read: send it unchanged to replace a profile; a stale token is rejected with
    /// HTTP 409. Send null to create, or the all-zero GUID to create only when no profile with <c>id</c> exists.
    /// </summary>
    public Guid? ExpectedConcurrencyToken { get; set; }

    /// <summary>Display name of the profile; must not be blank.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Provider kind, as a JSON integer: 0 OpenAi (the default; OpenAI or an OpenAI-compatible endpoint),
    /// 1 AzureOpenAi, 2 Ollama, 3 ComfyUi.
    /// </summary>
    public ProviderKind Kind { get; set; } = ProviderKind.OpenAi;

    /// <summary>
    /// Base URL of the provider's API: an absolute http or https URL without user information, query or fragment.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the stored API key: an empty string for none, or <c>secret:</c> followed by the identifier of an
    /// existing stored secret. Environment variable names and raw keys are rejected. OpenAI and Azure OpenAI profiles
    /// require a reference.
    /// </summary>
    public string ApiKeyEnvironmentVariable { get; set; } = string.Empty;

    /// <summary>Model used when an agent or request names none; blank becomes the connector's default model.</summary>
    public string DefaultModel { get; set; } = string.Empty;

    /// <summary>
    /// API style, as a JSON integer: 0 Responses (the default), 1 ChatCompletions; Ollama requires ChatCompletions.
    /// </summary>
    public ProviderTransportKind Transport { get; set; } = ProviderTransportKind.Responses;

    /// <summary>What the profile is used for, as a JSON integer: 0 Chat (the default), 1 ImageGeneration.</summary>
    public ProviderProfilePurpose Purpose { get; set; } = ProviderProfilePurpose.Chat;

    /// <summary>True (the default) to make the profile usable.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>True (the default) to stream responses from the provider.</summary>
    public bool SupportsStreaming { get; set; } = true;

    /// <summary>True (the default) to allow giving tools to the profile's models.</summary>
    public bool SupportsTools { get; set; } = true;

    /// <summary>Not stored: the server derives it from the kind and transport.</summary>
    public bool PreferFrameworkManagedChatHistory { get; set; }

    /// <summary>Not stored: the server derives it from the kind and transport.</summary>
    public bool SupportsBackgroundResponses { get; set; }

    /// <summary>
    /// Additional settings as the text of a JSON object, or empty. <c>timeoutSeconds</c> sets the request timeout (an
    /// integer of at least 5). When storing it the server adds its own metadata members, such as the connector key.
    /// </summary>
    public string ConfigurationJson { get; set; } = string.Empty;

    /// <summary>Not stored. The editor read returns the provider connector's display name here.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// True for a privately hosted provider; Ollama and ComfyUI profiles are always treated as private.
    /// </summary>
    public bool IsPrivateProvider { get; set; }

    /// <summary>
    /// Models offered when choosing a model for the profile; blank entries and duplicates are dropped.
    /// </summary>
    public List<string> SuggestedModels { get; set; } = [];

    /// <summary>
    /// Token prices of the profile's models, in US dollars per 1,000,000 tokens, used to estimate run costs.
    /// </summary>
    public List<ProviderModelTokenPriceEditorModel> ModelPrices { get; set; } = [];

    /// <summary>Labels of the profile; trimmed, a leading <c>#</c> removed, lower-cased and de-duplicated.</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Thinking-effort capabilities to store for the profile's models; null or empty stores none. Ignored when
    /// <c>configurationJson</c> contains <c>modelThinkingEffortCapabilities</c>, which then takes precedence.
    /// </summary>
    public List<ProviderModelThinkingEffortCapability>? ModelThinkingEffortCapabilities { get; set; }

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
            ModelPrices = ProviderPricingDefaults.ToEditorModels(definition.ModelPrices),
            Tags = definition.Tags.ToList(),
            ModelThinkingEffortCapabilities = definition.ModelThinkingEffortCapabilities
                .Select(capability => capability with
                {
                    AllowedEfforts = capability.AllowedEfforts.ToList()
                })
                .ToList()
        };
    }
}

/// <summary>
/// Editable form of a catalog capability, read from <c>GET /api/agents/capabilities/{capabilityId}/editor</c> and sent
/// whole to <c>POST /api/agents/capabilities</c>. A save replaces the capability's definition and keeps its proof
/// fields. A catalog capability is a declaration: agents use it only when selected in their
/// <c>selectedCapabilityIds</c>, and each run still grants tools only as the runtime's policies allow.
/// </summary>
public sealed class CapabilityEditorModel
{
    /// <summary>
    /// Identifier of the capability: null to create one (the server assigns the identifier), or the identifier of an
    /// existing capability to replace it; an unknown identifier is rejected.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Concurrency fingerprint from the editor read. On an update, a value that no longer matches the stored
    /// capability rejects the save and null or blank skips the check; on a create it must be null or blank.
    /// </summary>
    public string? ExpectedFingerprint { get; set; }

    /// <summary>
    /// Kind of the capability, as a JSON integer: 0 McpServer, 1 Skill (the default), 2 Tool, 3 Plugin, 4 Rag,
    /// 5 AiContext; 6 Memory is retired and rejected.
    /// </summary>
    public CapabilityKind Kind { get; set; } = CapabilityKind.Skill;

    /// <summary>
    /// Stable key of the capability, normalized to lower-case letters and digits with other characters collapsed to
    /// hyphens; it must contain a letter or digit and be unique per kind.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Display name; trimmed.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Description; trimmed.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Endpoint URL, command or path the capability uses, depending on its kind; trimmed.
    /// </summary>
    public string EndpointOrPath { get; set; } = string.Empty;

    /// <summary>
    /// Kind-specific settings, normally the text of a JSON object: a Skill's configuration must be a JSON object and
    /// other kinds are stored as sent. It is returned without redaction, so never put secret values in it.
    /// </summary>
    public string ConfigurationJson { get; set; } = string.Empty;

    /// <summary>
    /// True for a capability shipped with the product. It is stored as sent, but shipped capabilities are restored
    /// to their shipped definition whenever the catalog is written.
    /// </summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>Labels of the capability; trimmed, a leading <c>#</c> removed, lower-cased and de-duplicated.</summary>
    public List<string> Tags { get; set; } = [];

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
            IsBuiltIn = definition.IsBuiltIn,
            Tags = definition.Tags.ToList()
        };
    }
}

/// <summary>
/// Workspace memory note to create or replace through <c>POST /api/agents/memory</c>. A save replaces the whole note.
/// These notes are kept in the agent catalog and listed per agent; they are not injected into agent runs and are
/// separate from the memory providers configured in the agent's <c>memoryAccess</c> settings.
/// </summary>
public sealed class MemoryEditorModel
{
    /// <summary>
    /// Identifier of the note. Null or omitted creates a note with a new identifier; a value replaces that note or,
    /// if none exists, creates it with this identifier.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Identifier of the agent the note belongs to. The agent must exist; otherwise the save fails.
    /// </summary>
    public Guid AgentId { get; set; }

    /// <summary>
    /// Category of the note, as a JSON integer: 0 Fact, 1 Preference, 2 Context (the default), 3 FollowUp,
    /// 4 Architecture. It only labels the note.
    /// </summary>
    public MemoryKind Kind { get; set; } = MemoryKind.Context;

    /// <summary>Short title of the note; trimmed. Must not be null.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Text of the note; trimmed. Must not be null.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Free-text origin of the note, trimmed; <c>manual</c> when omitted. Must not be null.
    /// </summary>
    public string Source { get; set; } = "manual";

    /// <summary>
    /// Caller-assigned importance, stored as sent; 3 when omitted. The server applies no range check.
    /// </summary>
    public int Importance { get; set; } = 3;

    /// <summary>
    /// Optional metadata as a JSON string containing JSON, trimmed and stored as sent; its content is not validated.
    /// Must not be null; send an empty string when there is none.
    /// </summary>
    public string MetadataJson { get; set; } = string.Empty;
}
