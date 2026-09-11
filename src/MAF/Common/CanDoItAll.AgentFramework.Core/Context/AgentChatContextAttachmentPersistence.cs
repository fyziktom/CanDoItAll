using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentChatContextAttachmentCodec {
    AgentChatContextAttachmentKind Kind { get; }
    AgentToolProtocolEnvelope Capture(AgentChatContextAttachmentEnvelope attachment);
    IAgentChatContextAttachment Restore(AgentToolProtocolEnvelope payload);
}

public sealed class AgentChatContextAttachmentPersistence {
    private readonly IReadOnlyDictionary<AgentChatContextAttachmentKind, IAgentChatContextAttachmentCodec> codecs;

    public AgentChatContextAttachmentPersistence(IEnumerable<IAgentChatContextAttachmentCodec>? codecs = null) {
        this.codecs = (codecs ?? []).Prepend(new AgentChatExternalTargetAccessAttachmentCodec())
            .ToDictionary(codec => codec.Kind);
    }

    public bool CanCapture(AgentRuntimeTransientContext? context) =>
        context is null || context.Attachments.All(attachment => codecs.ContainsKey(attachment.Kind));

    public AgentToolAdmittedRuntimeContext Capture(AgentRuntimeTransientContext context) {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Attachments.IsEmpty) {
            return new(context.Content, context.WorkspaceScope);
        }
        return new(context.Content, context.WorkspaceScope, context.Attachments.Select(attachment =>
            AgentToolAdmittedContextAttachment.Capture(attachment, RequireCodec(attachment.Kind).Capture(attachment))).ToImmutableArray());
    }

    public AgentRuntimeTransientContext Restore(AgentToolAdmittedRuntimeContext context) {
        ArgumentNullException.ThrowIfNull(context);
        context.Validate();
        if (context.Attachments.IsDefaultOrEmpty) {
            return context.ToTransientContext();
        }
        var attachments = context.Attachments.Select(saved => {
            var codec = RequireCodec(saved.Kind);
            var attachment = saved.Restore(codec.Restore(saved.Payload));
            _ = codec.Capture(attachment);
            return attachment;
        }).ToArray();
        return new(context.Content, context.WorkspaceScope, attachments);
    }

    private IAgentChatContextAttachmentCodec RequireCodec(AgentChatContextAttachmentKind kind) =>
        codecs.TryGetValue(kind, out var codec) ? codec : throw new AgentToolAdmissionException(
            "tool-admission.context-codec-unavailable", "The original context attachment owner cannot restore this saved kind.");
}
