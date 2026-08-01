using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Tooling;

public sealed record AgentRuntimeToolProviderContext
{
    public AgentRuntimeToolProviderContext(
        AgentDefinition Agent,
        ProviderProfile Provider,
        IReadOnlyList<CapabilityCatalogItem> Capabilities,
        bool SuppressApprovalRequirements,
        AgentRuntimeToolProviderPurpose Purpose,
        string RuntimeSessionKey,
        AgentRuntimeContextIntent ContextIntent,
        IReadOnlyDictionary<string, string> Tags,
        IReadOnlyList<AgentChatContextAttachmentEnvelope>? Attachments = null)
    {
        this.Agent = Agent ?? throw new ArgumentNullException(nameof(Agent));
        this.Provider = Provider ?? throw new ArgumentNullException(nameof(Provider));
        this.Capabilities = Capabilities
            ?? throw new ArgumentNullException(nameof(Capabilities));
        this.SuppressApprovalRequirements = SuppressApprovalRequirements;
        this.Purpose = Purpose;
        this.RuntimeSessionKey = RuntimeSessionKey ?? string.Empty;
        this.ContextIntent = ContextIntent
            ?? throw new ArgumentNullException(nameof(ContextIntent));
        this.Tags = Tags ?? throw new ArgumentNullException(nameof(Tags));
        this.Attachments = Attachments?.ToImmutableArray() ?? [];
    }

    public AgentDefinition Agent { get; init; }

    public ProviderProfile Provider { get; init; }

    public IReadOnlyList<CapabilityCatalogItem> Capabilities { get; init; }

    public bool SuppressApprovalRequirements { get; init; }

    public AgentRuntimeToolProviderPurpose Purpose { get; init; }

    public string RuntimeSessionKey { get; init; }

    public AgentRuntimeContextIntent ContextIntent { get; init; }

    public IReadOnlyDictionary<string, string> Tags { get; init; }

    public ImmutableArray<AgentChatContextAttachmentEnvelope> Attachments { get; init; }

    public ImmutableArray<AgentChatContextAttachmentEnvelope>
        GetAttachments<TAttachment>()
        where TAttachment : class, IAgentChatContextAttachment
    {
        return Attachments
            .Where(static attachment =>
                attachment.AttachmentType == typeof(TAttachment))
            .ToImmutableArray();
    }
}
