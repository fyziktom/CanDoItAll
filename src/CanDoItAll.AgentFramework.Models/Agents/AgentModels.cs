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
    DateTimeOffset UpdatedAtUtc);

public sealed record AgentExportResult(string PackagePath, string Summary);

public sealed record AgentChatRunResult(
    Guid ChatSessionId,
    ChatMessageRecord AssistantMessage,
    AgentRunMetric Metric)
{
    public Guid ExecutionRunId { get; init; }
}
