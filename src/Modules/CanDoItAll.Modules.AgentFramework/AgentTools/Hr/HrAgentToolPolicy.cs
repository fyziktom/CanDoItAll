using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Core;
using static CanDoItAll.AgentFramework.Core.ToolCapabilityMetadataFactory;

namespace CanDoItAll.Modules.AgentFramework;

public static class HrAgentToolPolicy {
    private static readonly ToolCapabilityOperationEffects ExternalActionEffects = new(
        [ProcessOperationContractNames.ExternalActionControlled], canExecuteExternalAction: true);

    public const string HrAgentsSearch = "hr_agents_search";
    public const string HrAgentSettingsGet = "hr_agent_settings_get";
    public const string HrAgentCreationOptionsGet = "hr_agent_creation_options_get";
    public const string HrAgentCreate = "hr_agent_create";
    public const string HrAgentSettingsUpdate = "hr_agent_settings_update";
    public const string HrAgentAvatarGenerate = "hr_agent_avatar_generate";
    public const string HrAgentUsageGet = "hr_agent_usage_get";
    public const string HrAgentProcessHistoryGet = "hr_agent_process_history_get";
    public const string HrAgentProcessManagerReviewRequest = "hr_agent_process_manager_review_request";
    public const string HrCrmSearch = "hr_crm_search";
    public const string HrCrmItemSummaryGet = "hr_crm_item_summary_get";
    public const string HrCrmPartyCreate = "hr_crm_party_create";
    public const string HrCrmPartyAffiliationsList = "hr_crm_party_affiliations_list";
    public const string HrCrmAffiliationUpsert = "hr_crm_affiliation_upsert";

    public static IReadOnlyList<ToolCapabilityMetadata> Capabilities { get; } = Array.AsReadOnly<ToolCapabilityMetadata>([
        Read(HrAgentsSearch, ToolCapabilitySideEffectKind.InternalDataRead) with { BusinessArgumentRetentionScheme = "hr-approval-redacted-v1", ProtectRuntimeStateOnExport = true },
        Read(HrAgentSettingsGet, ToolCapabilitySideEffectKind.InternalDataRead),
        Read(HrAgentCreationOptionsGet, ToolCapabilitySideEffectKind.InternalDataRead),
        Mutation(HrAgentCreate, ToolCapabilitySideEffectKind.InternalStateMutation) with { BusinessArgumentRetentionScheme = "hr-approval-redacted-v1", ProtectRuntimeStateOnExport = true },
        Mutation(HrAgentSettingsUpdate, ToolCapabilitySideEffectKind.InternalStateMutation) with { BusinessArgumentRetentionScheme = "hr-approval-redacted-v1", ProtectRuntimeStateOnExport = true },
        Mutation(HrAgentAvatarGenerate, ToolCapabilitySideEffectKind.MediaGeneration, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution), BusinessArgumentRetentionScheme = "hr-approval-redacted-v1", ProtectRuntimeStateOnExport = true },
        Read(HrAgentUsageGet, ToolCapabilitySideEffectKind.InternalDataRead),
        Read(HrAgentProcessHistoryGet, ToolCapabilitySideEffectKind.InternalDataRead),
        Mutation(HrAgentProcessManagerReviewRequest, ToolCapabilitySideEffectKind.ExternalAction, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution), BusinessArgumentRetentionScheme = "hr-approval-redacted-v1", ProtectRuntimeStateOnExport = true },
        Read(HrCrmSearch, ToolCapabilitySideEffectKind.InternalDataRead) with { BusinessArgumentRetentionScheme = "hr-approval-redacted-v1", ProtectRuntimeStateOnExport = true },
        Read(HrCrmItemSummaryGet, ToolCapabilitySideEffectKind.InternalDataRead),
        Mutation(HrCrmPartyCreate, ToolCapabilitySideEffectKind.InternalStateMutation) with { BusinessArgumentRetentionScheme = "hr-approval-redacted-v1", ProtectRuntimeStateOnExport = true },
        Read(HrCrmPartyAffiliationsList, ToolCapabilitySideEffectKind.InternalDataRead) with { BusinessArgumentRetentionScheme = "hr-approval-redacted-v1", ProtectRuntimeStateOnExport = true },
        Mutation(HrCrmAffiliationUpsert, ToolCapabilitySideEffectKind.InternalStateMutation) with { BusinessArgumentRetentionScheme = "hr-approval-redacted-v1", ProtectRuntimeStateOnExport = true }
    ]);
}
