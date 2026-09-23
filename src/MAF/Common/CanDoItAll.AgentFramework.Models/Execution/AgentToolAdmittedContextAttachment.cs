using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public sealed record AgentToolAdmittedContextAttachment(
    AgentChatContextScopeId ScopeId,
    AgentChatContextSource Source,
    WorkspaceScopeDescriptor? WorkspaceScope,
    AgentChatContextContributorId ContributorId,
    AgentChatContextAttachmentKind Kind,
    ModulePublicationRevision PublicationRevision,
    SnapshotContentFingerprint ContentFingerprint,
    SnapshotCoverageFingerprint CoverageFingerprint,
    DatabaseProfileGeneration DatabaseProfileGeneration,
    SnapshotFreshnessFingerprint FreshnessFingerprint,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset? FreshUntilUtc,
    AgentToolProtocolEnvelope Payload) {
    public static AgentToolAdmittedContextAttachment Capture(AgentChatContextAttachmentEnvelope attachment,
        AgentToolProtocolEnvelope payload) => new(attachment.ScopeId, attachment.Source, attachment.WorkspaceScope,
        attachment.ContributorId, attachment.Kind, attachment.PublicationRevision, attachment.ContentFingerprint,
        attachment.CoverageFingerprint, attachment.DatabaseProfileGeneration, attachment.FreshnessFingerprint,
        attachment.CapturedAtUtc, attachment.FreshUntilUtc, payload);

    public AgentChatContextAttachmentEnvelope Restore(IAgentChatContextAttachment payload) =>
        new AgentChatContextAttachmentDraft(Kind, ContentFingerprint, CoverageFingerprint,
            DatabaseProfileGeneration, FreshnessFingerprint, CapturedAtUtc, FreshUntilUtc, payload)
        .CreateEnvelope(ScopeId, Source, WorkspaceScope, ContributorId, PublicationRevision);

    internal void Validate() {
        if (ScopeId.IsEmpty || Source is null || ContributorId.IsEmpty || Kind.IsEmpty || PublicationRevision.Value <= 0 ||
            ContentFingerprint.IsEmpty || CoverageFingerprint.IsEmpty || FreshnessFingerprint.IsEmpty ||
            FreshUntilUtc is { } deadline && deadline <= CapturedAtUtc || Payload is null) {
            throw new InvalidDataException("The saved context attachment identity or capture metadata is invalid.");
        }
        if (AgentToolProtocolEnvelope.ComputeDigest(Payload.PayloadJson) != Payload.Digest) {
            throw new InvalidDataException("The saved context attachment payload does not match its original digest.");
        }
    }
}

public sealed record AgentToolAdmittedRuntimeContext(string Content, WorkspaceScopeDescriptor? WorkspaceScope,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    ImmutableArray<AgentToolAdmittedContextAttachment> Attachments = default) {
    public AgentRuntimeTransientContext ToTransientContext() {
        if (!Attachments.IsDefaultOrEmpty) {
            throw new InvalidOperationException("Saved typed context requires its original owner codecs.");
        }
        return new(Content, WorkspaceScope);
    }

    internal void Validate() {
        _ = new AgentRuntimeTransientContext(Content, WorkspaceScope);
        if (!Attachments.IsDefault) {
            foreach (var attachment in Attachments) {
                ArgumentNullException.ThrowIfNull(attachment);
                attachment.Validate();
            }
        }
    }
}
