using System.Collections.Frozen;
using CanDoItAll.AgentFramework.Core;
using static CanDoItAll.AgentFramework.Core.ToolCapabilityMetadataFactory;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;

namespace CanDoItAll.Agents.SimpleChats;

public enum HrSimpleChatOperation {
    Search,
    Options,
    Settings,
    Create,
    Update,
    Status,
    Receipt
}

public sealed record HrSimpleChatToolOperation(
    HrSimpleChatOperation Operation,
    string ToolName,
    string CapabilityKey,
    AgentToolProposalEffect Effect,
    AgentToolProposalRecovery Recovery) {
    public bool RequiresApproval => Effect != AgentToolProposalEffect.Read;

    public AgentRuntimeToolOperationKind OperationKind => Effect == AgentToolProposalEffect.Mutation
        ? AgentRuntimeToolOperationKind.Mutation
        : AgentRuntimeToolOperationKind.Read;
}

public static class HrSimpleChatToolPolicy {
    public const string HrSimpleChatsSearch = "hr_simple_chats_search";
    public const string HrSimpleChatCreationOptionsGet = "hr_simple_chat_creation_options_get";
    public const string HrSimpleChatSettingsGet = "hr_simple_chat_settings_get";
    public const string HrSimpleChatCreate = "hr_simple_chat_create";
    public const string HrSimpleChatSettingsUpdate = "hr_simple_chat_settings_update";
    public const string HrSimpleChatStatusChange = "hr_simple_chat_status_change";
    public const string HrSimpleChatCreateReceiptGet = "hr_simple_chat_create_receipt_get";

    public static IReadOnlyList<ToolCapabilityMetadata> Capabilities { get; } = Array.AsReadOnly<ToolCapabilityMetadata>([
        Read(HrSimpleChatsSearch, ToolCapabilitySideEffectKind.InternalDataRead) with { BusinessArgumentRetentionScheme = "hr-approval-redacted-v1", ProtectRuntimeStateOnExport = true },
        Read(HrSimpleChatCreationOptionsGet, ToolCapabilitySideEffectKind.InternalDataRead) with { BusinessArgumentRetentionScheme = "hr-approval-redacted-v1", ProtectRuntimeStateOnExport = true },
        Read(HrSimpleChatSettingsGet, ToolCapabilitySideEffectKind.InternalDataRead) with { RequiresApprovalByDefault = true, BusinessArgumentRetentionScheme = "hr-approval-redacted-v1", ProtectRuntimeStateOnExport = true },
        Mutation(HrSimpleChatCreate, ToolCapabilitySideEffectKind.InternalStateMutation) with { BusinessArgumentRetentionScheme = "hr-approval-redacted-v1", ProtectRuntimeStateOnExport = true },
        Mutation(HrSimpleChatSettingsUpdate, ToolCapabilitySideEffectKind.InternalStateMutation) with { BusinessArgumentRetentionScheme = "hr-approval-redacted-v1", ProtectRuntimeStateOnExport = true },
        Mutation(HrSimpleChatStatusChange, ToolCapabilitySideEffectKind.InternalStateMutation) with { BusinessArgumentRetentionScheme = "hr-approval-redacted-v1", ProtectRuntimeStateOnExport = true },
        Read(HrSimpleChatCreateReceiptGet, ToolCapabilitySideEffectKind.InternalDataRead) with { BusinessArgumentRetentionScheme = "hr-approval-redacted-v1", ProtectRuntimeStateOnExport = true }
    ]);

    public const string ProviderKey = "hr-agent.simple-chats-definitions";
    public const string CreateProducer = "agents.hr.simple-chats";

    public static IReadOnlyList<HrSimpleChatToolOperation> Operations { get; } = Array.AsReadOnly<HrSimpleChatToolOperation>([
        new(HrSimpleChatOperation.Search, HrSimpleChatsSearch,
            "hr-simple-chats-search", AgentToolProposalEffect.Read, AgentToolProposalRecovery.RevalidateAndRead),
        new(HrSimpleChatOperation.Options, HrSimpleChatCreationOptionsGet,
            "hr-simple-chat-creation-options-get", AgentToolProposalEffect.Read, AgentToolProposalRecovery.RevalidateAndRead),
        new(HrSimpleChatOperation.Settings, HrSimpleChatSettingsGet,
            "hr-simple-chat-settings-get", AgentToolProposalEffect.SensitiveDisclosure, AgentToolProposalRecovery.RevalidateAndRead),
        new(HrSimpleChatOperation.Create, HrSimpleChatCreate,
            "hr-simple-chat-create", AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt),
        new(HrSimpleChatOperation.Update, HrSimpleChatSettingsUpdate,
            "hr-simple-chat-settings-update", AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.ReconcileBeforeRetry),
        new(HrSimpleChatOperation.Status, HrSimpleChatStatusChange,
            "hr-simple-chat-status-change", AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.ReconcileBeforeRetry),
        new(HrSimpleChatOperation.Receipt, HrSimpleChatCreateReceiptGet,
            "hr-simple-chat-create-receipt-get", AgentToolProposalEffect.Read, AgentToolProposalRecovery.RevalidateAndRead)
    ]);

    public static IReadOnlySet<string> PrivilegedKeys { get; } = Operations
        .Select(operation => operation.CapabilityKey)
        .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static HrSimpleChatToolOperation Get(HrSimpleChatOperation operation)
        => Operations.Single(item => item.Operation == operation);

    public static HrSimpleChatToolOperation Get(string toolName)
        => Operations.SingleOrDefault(item => string.Equals(item.ToolName, toolName, StringComparison.Ordinal))
            ?? throw new ArgumentException("The Simple Chat administration tool is unknown.", nameof(toolName));
}
