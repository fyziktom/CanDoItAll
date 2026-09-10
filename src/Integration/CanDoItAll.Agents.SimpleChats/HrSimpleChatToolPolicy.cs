using System.Collections.Frozen;
using CanDoItAll.AgentFramework.Core;
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
    public const string ProviderKey = "hr-agent.simple-chats-definitions";
    public const string CreateProducer = "agents.hr.simple-chats";

    public static IReadOnlyList<HrSimpleChatToolOperation> Operations { get; } = Array.AsReadOnly<HrSimpleChatToolOperation>([
        new(HrSimpleChatOperation.Search, AgentToolInvocationPolicyMetadata.HrSimpleChatsSearch,
            "hr-simple-chats-search", AgentToolProposalEffect.Read, AgentToolProposalRecovery.RevalidateAndRead),
        new(HrSimpleChatOperation.Options, AgentToolInvocationPolicyMetadata.HrSimpleChatCreationOptionsGet,
            "hr-simple-chat-creation-options-get", AgentToolProposalEffect.Read, AgentToolProposalRecovery.RevalidateAndRead),
        new(HrSimpleChatOperation.Settings, AgentToolInvocationPolicyMetadata.HrSimpleChatSettingsGet,
            "hr-simple-chat-settings-get", AgentToolProposalEffect.SensitiveDisclosure, AgentToolProposalRecovery.RevalidateAndRead),
        new(HrSimpleChatOperation.Create, AgentToolInvocationPolicyMetadata.HrSimpleChatCreate,
            "hr-simple-chat-create", AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt),
        new(HrSimpleChatOperation.Update, AgentToolInvocationPolicyMetadata.HrSimpleChatSettingsUpdate,
            "hr-simple-chat-settings-update", AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.ReconcileBeforeRetry),
        new(HrSimpleChatOperation.Status, AgentToolInvocationPolicyMetadata.HrSimpleChatStatusChange,
            "hr-simple-chat-status-change", AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.ReconcileBeforeRetry),
        new(HrSimpleChatOperation.Receipt, AgentToolInvocationPolicyMetadata.HrSimpleChatCreateReceiptGet,
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
