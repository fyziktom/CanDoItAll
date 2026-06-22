using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

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

public sealed record AgentAllowedSecretReference(
    Guid SecretId,
    string NameSnapshot,
    string Purpose);

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
    public string? AvatarImageUrl { get; init; }
}

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

public sealed record AgentExportResult(string PackagePath, string Summary);

public sealed record AgentChatRunResult(
    Guid ChatSessionId,
    ChatMessageRecord AssistantMessage,
    AgentRunMetric Metric)
{
    public Guid ExecutionRunId { get; init; }
}
