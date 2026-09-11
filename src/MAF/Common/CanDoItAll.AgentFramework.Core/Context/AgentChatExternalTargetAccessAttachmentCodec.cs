using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class AgentChatExternalTargetAccessAttachmentCodec : IAgentChatContextAttachmentCodec {
    private const int PayloadVersion = 1;
    public AgentChatContextAttachmentKind Kind => new(AgentChatExternalTargetAccessAttachmentFactory.AttachmentKindValue);

    public AgentToolProtocolEnvelope Capture(AgentChatContextAttachmentEnvelope attachment) {
        if (attachment.Kind != Kind || !attachment.TryGetAttachment<AgentChatExternalTargetAccessAttachment>(out var value)) {
            throw new InvalidDataException("The external-root context attachment has an unsupported owner payload.");
        }
        var expected = AgentChatExternalTargetAccessAttachmentFactory.CreateReadOnlyDraft(value.ReadOnlyAliases,
            value.ExternalTargetRootBindings, attachment.DatabaseProfileGeneration, attachment.CapturedAtUtc, attachment.FreshUntilUtc);
        if (expected is null || expected.ContentFingerprint != attachment.ContentFingerprint ||
            expected.CoverageFingerprint != attachment.CoverageFingerprint || expected.FreshnessFingerprint != attachment.FreshnessFingerprint) {
            throw new InvalidDataException("The external-root context attachment does not match its captured fingerprints.");
        }
        return AgentToolProtocolEnvelope.Create(Kind.Value, PayloadVersion,
            JsonSerializer.Serialize(new Payload(value.ReadOnlyAliases.ToArray(), value.ExternalTargetRootBindings.ToArray())));
    }

    public IAgentChatContextAttachment Restore(AgentToolProtocolEnvelope payload) {
        if (payload.Format != Kind.Value || payload.Version != PayloadVersion) {
            throw new InvalidDataException("The saved external-root context attachment version is unsupported.");
        }
        var value = JsonSerializer.Deserialize<Payload>(payload.PayloadJson)
            ?? throw new InvalidDataException("The saved external-root context attachment is empty.");
        return new AgentChatExternalTargetAccessAttachment(value.ReadOnlyAliases, value.Bindings);
    }

    private sealed record Payload(string[] ReadOnlyAliases, ExternalTargetRootBinding[] Bindings);
}
