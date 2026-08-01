using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public static class HrAgentCapabilityKeys
{
    public const string GovernanceSkill = "hr-agent-governance-inline-skill";
    public const string CapabilityCurationSkill = HrAgentIdentity.CapabilityCurationSkillCapabilityKey;
    public const string AgentsSearch = "hr-agents-search";
    public const string AgentSettingsGet = "hr-agent-settings-get";
    public const string AgentCreationOptionsGet = "hr-agent-creation-options-get";
    public const string AgentCreate = "hr-agent-create";
    public const string AgentSettingsUpdate = "hr-agent-settings-update";
    public const string AgentAvatarGenerate = "hr-agent-avatar-generate";
    public const string AgentUsageGet = "hr-agent-usage-get";
    public const string AgentProcessHistoryGet = "hr-agent-process-history-get";
    public const string AgentProcessManagerReviewRequest = "hr-agent-process-manager-review-request";
    public const string CrmSearch = "hr-crm-search";
    public const string CrmItemSummaryGet = "hr-crm-item-summary-get";
    public const string CrmPartyCreate = "hr-crm-party-create";
    public const string CrmPartyAffiliationsList = "hr-crm-party-affiliations-list";
    public const string CrmAffiliationUpsert = "hr-crm-affiliation-upsert";

    public static IReadOnlyDictionary<string, string> ToolNameToCapabilityKey { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AgentToolInvocationPolicyMetadata.HrAgentsSearch] = AgentsSearch,
            [AgentToolInvocationPolicyMetadata.HrAgentSettingsGet] = AgentSettingsGet,
            [AgentToolInvocationPolicyMetadata.HrAgentCreationOptionsGet] = AgentCreationOptionsGet,
            [AgentToolInvocationPolicyMetadata.HrAgentCreate] = AgentCreate,
            [AgentToolInvocationPolicyMetadata.HrAgentSettingsUpdate] = AgentSettingsUpdate,
            [AgentToolInvocationPolicyMetadata.HrAgentAvatarGenerate] = AgentAvatarGenerate,
            [AgentToolInvocationPolicyMetadata.HrAgentUsageGet] = AgentUsageGet,
            [AgentToolInvocationPolicyMetadata.HrAgentProcessHistoryGet] = AgentProcessHistoryGet,
            [AgentToolInvocationPolicyMetadata.HrAgentProcessManagerReviewRequest] = AgentProcessManagerReviewRequest,
            [AgentToolInvocationPolicyMetadata.HrCrmSearch] = CrmSearch,
            [AgentToolInvocationPolicyMetadata.HrCrmItemSummaryGet] = CrmItemSummaryGet,
            [AgentToolInvocationPolicyMetadata.HrCrmPartyCreate] = CrmPartyCreate,
            [AgentToolInvocationPolicyMetadata.HrCrmPartyAffiliationsList] = CrmPartyAffiliationsList,
            [AgentToolInvocationPolicyMetadata.HrCrmAffiliationUpsert] = CrmAffiliationUpsert
        };

    public static IReadOnlySet<string> PrivilegedKeys { get; } = new HashSet<string>(
        ToolNameToCapabilityKey.Values
            .Append(GovernanceSkill)
            .Append(CapabilityCurationSkill),
        StringComparer.OrdinalIgnoreCase);
}
