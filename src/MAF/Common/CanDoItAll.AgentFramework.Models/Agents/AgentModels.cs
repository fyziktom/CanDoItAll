using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Catalog capability assigned to an agent, one entry of the agent definition's <c>capabilities</c>. Assignments are
/// changed through the editor form's <c>selectedCapabilityIds</c>; this record is read-only. An assignment declares a
/// capability for the agent; a run attaches the corresponding tools only as the agent's permissions and the runtime
/// policy allow.
/// </summary>
/// <param name="CapabilityId">
/// Identifier of the catalog capability, as listed by <c>GET /api/agents/capabilities</c>.
/// </param>
/// <param name="CapabilityKey">
/// Key of the catalog capability at the time of assignment, kept in sync with the catalog.
/// </param>
/// <param name="Kind">
/// Kind of the capability, as a JSON integer: 0 McpServer, 1 Skill, 2 Tool, 3 Plugin, 4 Rag, 5 AiContext, 6 Memory
/// (retired).
/// </param>
/// <param name="ProofStatus">
/// Result of the latest verification of this assignment, as a JSON integer: 0 NotRun, 1 Verified, 2 Failed,
/// 3 PendingReview. It is informational: the runtime does not use it to decide whether the capability is attached. A
/// new assignment starts with the catalog capability's current proof state.
/// </param>
/// <param name="LastVerifiedAtUtc">
/// Instant, with offset, at which the proof in <c>proofStatus</c> was produced; null when never verified.
/// </param>
/// <param name="ProofNotes">Human-readable notes of the latest verification; empty when none.</param>
public sealed record AgentCapabilityAssignment(
    Guid CapabilityId,
    string CapabilityKey,
    CapabilityKind Kind,
    CapabilityProofStatus ProofStatus,
    DateTimeOffset? LastVerifiedAtUtc,
    string ProofNotes);

public static class AgentSecretPurposes
{
    public const string GeneralAgentRequest = "agent-request";
}

/// <summary>
/// Stored secret an agent may use, one entry of its allowed secret references. It carries the secret's identifier and
/// display name only, never the secret value. For agents, the list limits at run time which secrets MCP capability
/// bindings may resolve; in the permissions of a workflow LLM call component it is only stored.
/// </summary>
/// <param name="SecretId">
/// Identifier of the stored secret. Agent saves drop entries with the all-zero identifier; it is not checked against
/// the secret store.
/// </param>
/// <param name="NameSnapshot">Display name of the secret when it was allowed, trimmed; a label only.</param>
/// <param name="Purpose">Purpose label of the allowance; blank is stored as <c>agent-request</c>.</param>
public sealed record AgentAllowedSecretReference(
    Guid SecretId,
    string NameSnapshot,
    string Purpose);

/// <summary>
/// Runtime permissions of an agent: which kinds of tools and interactions its runs may use. These are agent capability
/// controls, not HTTP permissions. When the whole object is omitted from an agent save, the defaults apply (tools,
/// asking other agents and escalation allowed, approval required for external calls); an object with omitted members
/// sets those members to false. Workflow LLM call components carry the same object; it is stored and returned with the
/// component, but workflow execution does not apply it.
/// </summary>
/// <param name="CanUseTools">
/// Master switch for tools: when false, runs of the agent receive no workspace, runtime provider, MCP or agent-to-agent
/// tools.
/// </param>
/// <param name="CanAskOtherAgents">Allows the agent-to-agent (A2A) remote agent tools.</param>
/// <param name="CanEscalateToHuman">Stored and shown; no runtime behavior depends on it.</param>
/// <param name="CanObserveOtherAgents">
/// Allows the agent to be chosen as a process-run manager or as an HR review manager.
/// </param>
/// <param name="CanScheduleWork">Allows the scheduler tools and workflow scheduling.</param>
/// <param name="RequiresApprovalForExternalCalls">
/// True to require approval of each MCP and agent-to-agent tool call.
/// </param>
/// <param name="AutoApproveExternalCallsByDefault">
/// True to approve pending tool calls of the agent's chat runs automatically, as if the chat session's automatic
/// approval were on. False by default.
/// </param>
/// <param name="AllowedSecrets">
/// Secrets the agent may use. In an agent save this member is ignored and replaced by the editor form's
/// <c>allowedSecretReferences</c>; reads return the stored list.
/// </param>
public sealed record AgentPermissionsPolicy(
    bool CanUseTools,
    bool CanAskOtherAgents,
    bool CanEscalateToHuman,
    bool CanObserveOtherAgents,
    bool CanScheduleWork,
    bool RequiresApprovalForExternalCalls,
    bool AutoApproveExternalCallsByDefault = false,
    IReadOnlyList<AgentAllowedSecretReference>? AllowedSecrets = null)
{
    public static AgentPermissionsPolicy Default { get; } = new(
        CanUseTools: true,
        CanAskOtherAgents: true,
        CanEscalateToHuman: true,
        CanObserveOtherAgents: false,
        CanScheduleWork: false,
        RequiresApprovalForExternalCalls: true,
        AutoApproveExternalCallsByDefault: false,
        AllowedSecrets: []);

    [JsonIgnore]
    public IReadOnlyList<AgentAllowedSecretReference> NormalizedAllowedSecrets
        => AllowedSecrets ?? [];
}

/// <summary>
/// Stored technical agent definition: the Agents-owned configuration and identity that agent execution runs use.
/// Returned by <c>GET /api/agents</c> and <c>GET /api/agents/teams/{teamId}/agents</c>. The CRM/HR party that
/// represents an agent is a projection of this record, not another definition. To change an agent, read and save its
/// editor form (<c>GET /api/agents/{agentId}</c>, <c>POST /api/agents</c>).
/// </summary>
/// <param name="Id">
/// Agent identifier, used by the <c>/api/agents/{agentId}</c> operations and by execution runs.
/// </param>
/// <param name="Name">Display name of the agent; also the name the model runtime gives the agent.</param>
/// <param name="RoleTitle">Role title shown with the agent, for example <c>Support engineer</c>; may be empty.</param>
/// <param name="Summary">Short description of the agent, also given to the model runtime; may be empty.</param>
/// <param name="Instructions">System instructions sent to the model on every run of the agent.</param>
/// <param name="Status">
/// Lifecycle status, as a JSON integer: 0 Draft, 1 Active, 2 Suspended, 3 Archived. Many runtime features, such as the
/// first-party runtime tools, run recovery and process steps, require an Active agent that is not a template;
/// Suspended and Archived agents cannot take part in handoffs.
/// </param>
/// <param name="ProviderProfileId">
/// Identifier of the provider profile (a configured model provider, see <c>GET /api/agents/providers</c>) the agent
/// uses, or null when none is selected. It is not a model identifier.
/// </param>
/// <param name="Model">
/// Model identifier within the selected provider profile, for example a model or deployment name; empty means the
/// provider profile's default model.
/// </param>
/// <param name="Workload">
/// Kind of work the agent does, as a JSON integer: 0 General, 1 Management, 2 Hr, 3 Sales, 4 Assistant, 5 Qa,
/// 6 Support, 7 Programming, 8 Spreadsheet, 9 Mail, 10 Research. Programming and Research enable conversation
/// compaction under the default policy, and the name is the key of AgentRole memory provider assignments.
/// </param>
/// <param name="ChatHistoryMode">
/// Where the chat history is kept between turns, as a JSON integer: 0 ProviderDefault (kept by the product, except for
/// OpenAI and Azure OpenAI profiles on the Responses transport that do not prefer product-managed history, where the
/// provider keeps it), 1 FrameworkManaged (kept and resent by the product), 2 ProviderManaged (kept by the model
/// provider).
/// </param>
/// <param name="Temperature">
/// Sampling temperature sent to the model. The server does not check its range, and it is omitted for models that do
/// not accept it.
/// </param>
/// <param name="RequirePerServiceCallChatHistoryPersistence">
/// True to persist the chat history after every model call of a run rather than only at the end. The runtime also does
/// this for product-managed history when approval-requiring tools are attached.
/// </param>
/// <param name="EnableBackgroundResponses">
/// True to request background (long-running) model responses; it takes effect only when the provider profile supports
/// them.
/// </param>
/// <param name="ConfigurationJson">
/// Further configuration as a JSON string containing a JSON object: the access settings (keys <c>projectStructure</c>,
/// <c>processes</c>, <c>workspaceTools</c>, <c>imageGeneration</c>, <c>voiceAccess</c>, <c>memory</c>), model
/// parameters such as <c>modelParameters.reasoningEffort</c> and other agent options. The editor form exposes the
/// access settings as typed members. It can contain encrypted external-folder bindings; treat it as sensitive.
/// </param>
/// <param name="IsTemplate">
/// True for a template: an agent definition used to create working agents with
/// <c>POST /api/agents/{agentId}/clone</c>.
/// </param>
/// <param name="TemplateKey">
/// Canonical key of the agent, derived from its template key or name: lower-case letters and digits separated by
/// hyphens, unique among all agents and templates.
/// </param>
/// <param name="Permissions">Runtime permissions of the agent's runs; not HTTP permissions.</param>
/// <param name="Capabilities">Catalog capabilities assigned to the agent, each with its proof state.</param>
/// <param name="Tags">Free-form labels, trimmed and unique ignoring case.</param>
/// <param name="CreatedAtUtc">Instant, with offset, at which the agent was created.</param>
/// <param name="UpdatedAtUtc">
/// Concurrency revision of the agent: an instant, with offset, that advances with accepted changes. The editor form
/// returns it as <c>expectedUpdatedAtUtc</c> for the next save, and a package import that replaces this agent must
/// send it as <c>expectedAgentVersion</c>.
/// </param>
public sealed record AgentDefinition(
Guid Id,
string Name,
string RoleTitle,
    string Summary,
    string Instructions,
    AgentLifecycleStatus Status,
    Guid? ProviderProfileId,
    string Model,
    AgentWorkloadKind Workload,
    AgentChatHistoryMode ChatHistoryMode,
    double Temperature,
    bool RequirePerServiceCallChatHistoryPersistence,
    bool EnableBackgroundResponses,
    string ConfigurationJson,
    bool IsTemplate,
    string TemplateKey,
    AgentPermissionsPolicy Permissions,
IReadOnlyList<AgentCapabilityAssignment> Capabilities,
IReadOnlyList<string> Tags,
DateTimeOffset CreatedAtUtc,
DateTimeOffset UpdatedAtUtc)
{
    /// <summary>
    /// Avatar image reference, or null when none is set. The product uses a bundled avatar path (for example
    /// <c>_content/CanDoItAll.Components.BaseLib/assets/identity/avatars/avatar-01.jpg</c>) or a <c>data:</c> image
    /// URL; the API stores any trimmed text.
    /// </summary>
    public string? AvatarImageUrl { get; init; }
}

public static class AgentAvatarImageCatalog
{
    public const string BundledAvatarBasePath = "_content/CanDoItAll.Components.BaseLib/assets/identity/avatars/";

    public static IReadOnlyList<string> BundledAvatarUrls { get; } =
    [
        CreateBundledAvatarUrl(1),
        CreateBundledAvatarUrl(2),
        CreateBundledAvatarUrl(3),
        CreateBundledAvatarUrl(4),
        CreateBundledAvatarUrl(5),
        CreateBundledAvatarUrl(6),
        CreateBundledAvatarUrl(7),
        CreateBundledAvatarUrl(8)
    ];

    private static readonly HashSet<string> BundledAvatarUrlSet = BundledAvatarUrls
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsBundledAvatarUrl(string? avatarImageUrl)
    {
        return !string.IsNullOrWhiteSpace(avatarImageUrl) &&
               BundledAvatarUrlSet.Contains(avatarImageUrl.Trim());
    }

    public static string RequireBundledAvatarUrl(string? avatarImageUrl, string owner)
    {
        var normalized = avatarImageUrl?.Trim() ?? string.Empty;
        if (IsBundledAvatarUrl(normalized))
        {
            return normalized;
        }

        throw new InvalidOperationException(
            $"Agent template '{owner}' must define a bundled avatar image URL.");
    }

    private static string CreateBundledAvatarUrl(int number)
        => $"{BundledAvatarBasePath}avatar-{number:00}.jpg";
}

/// <summary>
/// Stored agent team: a named group of agent definitions used to organize agents. Returned by the team reads and by
/// the member replacement operations. A team does not grant capabilities or tools and does not change how its member
/// agents run; an agent can belong to several teams. Teams shipped with the product keep fixed identifiers and are
/// reset to their shipped content whenever the agent catalog is written.
/// </summary>
/// <param name="Id">Team identifier, used by the <c>/api/agents/teams/{teamId}</c> operations.</param>
/// <param name="Name">Team name, trimmed and unique among the workspace's teams, ignoring case.</param>
/// <param name="Description">Free-text description; an empty string when none was given.</param>
/// <param name="AgentIds">
/// Identifiers of the member agents, ordered by agent name when the members were last saved. Deleting an agent
/// removes it from every team.
/// </param>
/// <param name="CreatedAtUtc">Instant, with offset, at which the team was first saved.</param>
/// <param name="UpdatedAtUtc">Instant, with offset, of the team's last save or member replacement.</param>
/// <param name="Icon">
/// Name of the team's icon from the supported icon set, for example <c>groups</c> (the default), <c>hub</c> or
/// <c>code</c>. Saving an unsupported name stores <c>groups</c>.
/// </param>
public sealed record AgentTeamDefinition(
    Guid Id,
    string Name,
    string Description,
    IReadOnlyList<Guid> AgentIds,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Icon = AgentTeamIconCatalog.DefaultIcon);

public sealed record AgentTeamIconOption(string Icon, string Label);

public static class AgentTeamIconCatalog
{
    public const string DefaultIcon = "groups";

    public static IReadOnlyList<AgentTeamIconOption> Options { get; } =
    [
        new("groups", "Team"),
        new("account_tree", "Structure"),
        new("hub", "Hub"),
        new("diversity_3", "Collaboration"),
        new("support_agent", "Support"),
        new("smart_toy", "Agent"),
        new("psychology", "Reasoning"),
        new("engineering", "Engineering"),
        new("code", "Code"),
        new("terminal", "Terminal"),
        new("integration_instructions", "Integration"),
        new("schema", "Schema"),
        new("api", "API"),
        new("memory", "Memory"),
        new("dns", "Database"),
        new("storage", "Storage"),
        new("cloud", "Cloud"),
        new("security", "Security"),
        new("verified_user", "Verified"),
        new("policy", "Policy"),
        new("fact_check", "Review"),
        new("bug_report", "Debug"),
        new("query_stats", "Analytics"),
        new("analytics", "Metrics"),
        new("bar_chart", "Chart"),
        new("monitor_heart", "Monitoring"),
        new("rocket_launch", "Launch"),
        new("bolt", "Fast"),
        new("auto_awesome", "Creative"),
        new("brush", "Design"),
        new("palette", "Visual"),
        new("image", "Image"),
        new("visibility", "Vision"),
        new("language", "Language"),
        new("translate", "Translate"),
        new("article", "Docs"),
        new("description", "Document"),
        new("task_alt", "Tasks"),
        new("checklist", "Checklist"),
        new("rule", "Rules"),
        new("science", "Research"),
        new("biotech", "Experiment"),
        new("school", "Learning"),
        new("workspaces", "Workspace"),
        new("inventory_2", "Inventory"),
        new("build", "Build"),
        new("construction", "Construction"),
        new("settings", "Settings"),
        new("tune", "Tune"),
        new("forum", "Conversation"),
        new("campaign", "Campaign"),
        new("public", "Global")
    ];

    private static readonly HashSet<string> AllowedIcons = Options
        .Select(item => item.Icon)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsAllowed(string? icon)
        => !string.IsNullOrWhiteSpace(icon) && AllowedIcons.Contains(icon.Trim());

    public static string Normalize(string? icon)
    {
        var normalizedIcon = (icon ?? string.Empty).Trim();
        return IsAllowed(normalizedIcon) ? normalizedIcon : DefaultIcon;
    }
}

/// <summary>
/// Result of <c>GET /api/agents/{agentId}/export</c>: where the server wrote the new export package.
/// </summary>
/// <param name="PackagePath">
/// Absolute path of the ZIP package in the server's file system, inside the workspace's export folder. It can be sent
/// to <c>POST /api/agents/import</c> on the same host; it is not a download URL.
/// </param>
/// <param name="Summary">
/// Human-readable summary of what was exported, for example the number of chat sessions and memory notes. Do not parse
/// it.
/// </param>
public sealed record AgentExportResult(string PackagePath, string Summary);

public sealed record AgentChatRunResult(
    Guid ChatSessionId,
    ChatMessageRecord AssistantMessage,
    AgentRunMetric Metric)
{
    public Guid ExecutionRunId { get; init; }

    public ExecutionState State { get; init; }

    [JsonIgnore]
    public AgentChatExecutionCompleted? ContextCompletionNotification { get; init; }
}
