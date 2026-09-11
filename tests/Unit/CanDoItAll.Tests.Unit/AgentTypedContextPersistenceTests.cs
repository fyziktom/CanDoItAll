using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.AgentContext;
using CanDoItAll.Modules.Workbench.ProjectStructure;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class AgentTypedContextPersistenceTests {
    private static AgentChatContextAttachmentPersistence Owners() =>
        new([new ProjectStructureInvocationSnapshotCodec(), new ProjectStructureGanttObservationCodec()]);

    [Fact]
    public void Captured_snapshot_is_detached_from_later_surface_mutation_and_preserves_every_envelope_identity() {
        var original = Context();
        var saved = Owners().Capture(original);
        var json = JsonSerializer.Serialize(saved);
        var restored = Owners().Restore(JsonSerializer.Deserialize<AgentToolAdmittedRuntimeContext>(json)!);
        Assert.Equal(AgentChatContextDigest.Compute(original), AgentChatContextDigest.Compute(restored));
        Assert.Equal(json, JsonSerializer.Serialize(Owners().Capture(restored)));
        Assert.NotSame(original.Attachments[0], restored.Attachments[0]);
        Assert.Throws<InvalidOperationException>(() => saved.ToTransientContext());
    }

    [Fact]
    public void Legacy_attachment_free_context_retains_its_original_encoding_and_restore_behavior() {
        var saved = JsonSerializer.Deserialize<AgentToolAdmittedRuntimeContext>("""{"Content":"Legacy captured text","WorkspaceScope":null}""")!;
        Assert.Equal("Legacy captured text", saved.ToTransientContext().Content);
        Assert.Empty(Owners().Restore(saved).Attachments);
        Assert.DoesNotContain("Attachments", JsonSerializer.Serialize(saved), StringComparison.Ordinal);
        Assert.Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(Owners().Capture(saved.ToTransientContext())));
    }

    [Fact]
    public void Unknown_attachment_remains_request_scoped_and_an_explicit_capture_is_rejected() {
        var known = Context();
        var before = known.Attachments[0];
        var unknown = new AgentChatContextAttachmentDraft(new("tests.unregistered"), before.ContentFingerprint,
            before.CoverageFingerprint, before.DatabaseProfileGeneration, before.FreshnessFingerprint,
            before.CapturedAtUtc, before.FreshUntilUtc, new UnknownAttachment()).CreateEnvelope(before.ScopeId,
                before.Source, before.WorkspaceScope, before.ContributorId, before.PublicationRevision);
        var context = new AgentRuntimeTransientContext(known.Content, known.WorkspaceScope, [unknown]);
        Assert.False(Owners().CanCapture(context));
        Assert.Throws<AgentToolAdmissionException>(() => Owners().Capture(context));
    }

    [Fact]
    public void A_registered_kind_cannot_promote_a_foreign_CLR_payload_to_the_owner_type() {
        var known = Context();
        var before = known.Attachments[0];
        var wrongType = new AgentChatContextAttachmentDraft(before.Kind, before.ContentFingerprint,
            before.CoverageFingerprint, before.DatabaseProfileGeneration, before.FreshnessFingerprint,
            before.CapturedAtUtc, before.FreshUntilUtc, new UnknownAttachment()).CreateEnvelope(before.ScopeId,
                before.Source, before.WorkspaceScope, before.ContributorId, before.PublicationRevision);
        Assert.Throws<InvalidDataException>(() => Owners().Capture(new(known.Content, known.WorkspaceScope, [wrongType])));
    }

    [Fact]
    public void Changed_owner_payload_cannot_reuse_original_envelope_fingerprints_even_with_a_recomputed_JSON_digest() {
        var saved = Owners().Capture(Context());
        var attachment = saved.Attachments[0];
        var changedJson = attachment.Payload.PayloadJson.Replace("Original project", "Later edited project", StringComparison.Ordinal);
        Assert.NotEqual(attachment.Payload.PayloadJson, changedJson);
        var changed = attachment with { Payload = AgentToolProtocolEnvelope.Create(attachment.Payload.Format, attachment.Payload.Version, changedJson) };
        Assert.Throws<InvalidDataException>(() => Owners().Restore(saved with { Attachments = [changed] }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unknown_saved_kind_or_payload_version_is_rejected_without_silently_dropping_it(bool changeKind) {
        var saved = Owners().Capture(Context());
        var attachment = saved.Attachments[0];
        var changed = changeKind ? attachment with { Kind = new("tests.unregistered") } : attachment with {
            Payload = AgentToolProtocolEnvelope.Create(attachment.Payload.Format, 2, attachment.Payload.PayloadJson)
        };
        if (changeKind) {
            Assert.Throws<AgentToolAdmissionException>(() => Owners().Restore(saved with { Attachments = [changed] }));
        } else {
            Assert.Throws<InvalidDataException>(() => Owners().Restore(saved with { Attachments = [changed] }));
        }
    }

    [Fact]
    public void Malformed_owner_JSON_remains_a_visible_error() {
        var saved = Owners().Capture(Context());
        var attachment = saved.Attachments[0];
        var changed = attachment with { Payload = AgentToolProtocolEnvelope.Create(attachment.Payload.Format, 1, "{") };
        Assert.Throws<JsonException>(() => Owners().Restore(saved with { Attachments = [changed] }));
    }

    [Fact]
    public void External_read_bindings_keep_exact_original_protected_values_and_expiry_without_registry_recapture() {
        var known = Context().Attachments[0];
        var draft = AgentChatExternalTargetAccessAttachmentFactory.CreateReadOnlyDraft(["external-target/v1/0123456789abcdef01234567"],
            [new ExternalTargetRootBinding("0123456789abcdef01234567", "test-platform", "opaque-original-protected-token")],
            known.DatabaseProfileGeneration, known.CapturedAtUtc, known.FreshUntilUtc)!;
        var envelope = draft.CreateEnvelope(known.ScopeId, known.Source, known.WorkspaceScope,
            new(AgentChatExternalTargetAccessAttachmentFactory.TrustedContributorIdValue), known.PublicationRevision);
        var context = new AgentRuntimeTransientContext("Captured root access", known.WorkspaceScope, [envelope]);
        var saved = new AgentChatContextAttachmentPersistence().Capture(context);
        var restored = new AgentChatContextAttachmentPersistence().Restore(
            JsonSerializer.Deserialize<AgentToolAdmittedRuntimeContext>(JsonSerializer.Serialize(saved))!);
        Assert.Equal(AgentChatContextDigest.Compute(context), AgentChatContextDigest.Compute(restored));
        var actual = Assert.Single(restored.Attachments);
        Assert.True(AgentChatExternalTargetAccessAttachmentFactory.TryGetValidatedReadOnlyAccess(actual,
            known.DatabaseProfileGeneration, known.CapturedAtUtc, out var aliases, out var bindings));
        Assert.Equal("external-target/v1/0123456789abcdef01234567", Assert.Single(aliases));
        Assert.Equal("opaque-original-protected-token", Assert.Single(bindings).ProtectedRootToken);
        Assert.False(AgentChatExternalTargetAccessAttachmentFactory.TryGetValidatedReadOnlyAccess(actual,
            known.DatabaseProfileGeneration, known.FreshUntilUtc!.Value, out _, out _));
        Assert.False(AgentChatExternalTargetAccessAttachmentFactory.TryGetValidatedReadOnlyAccess(actual,
            new(known.DatabaseProfileGeneration.Value + 1), known.CapturedAtUtc, out _, out _));
    }

    private static AgentRuntimeTransientContext Context() {
        var projectId = Guid.NewGuid();
        var workspace = WorkspaceScopeDescriptor.Project(projectId.ToString("D"));
        var now = DateTimeOffset.UtcNow;
        var captured = ProjectStructureInvocationSnapshotMapper.Capture(new(projectId, "Original project", [], [], null),
            ProjectStructureAgentChatView.Canvas, [], new(7), now);
        var envelope = captured.AttachmentDraft.CreateEnvelope(AgentChatContextScopeId.Create(),
            new(new(AgentChatTrustedSourceKinds.ProjectStructure), new(projectId.ToString("D"))), workspace,
            new("project-structure.selection"), new(9));
        return new("Original captured text", workspace, [envelope]);
    }

    private sealed record UnknownAttachment : IAgentChatContextAttachment;
}
