using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class AgentTypedContextJournalImmutabilityIntegrationTests {
    [Theory]
    [InlineData(AttachmentChange.Scope)]
    [InlineData(AttachmentChange.Source)]
    [InlineData(AttachmentChange.Workspace)]
    [InlineData(AttachmentChange.Contributor)]
    [InlineData(AttachmentChange.Kind)]
    [InlineData(AttachmentChange.Publication)]
    [InlineData(AttachmentChange.ContentFingerprint)]
    [InlineData(AttachmentChange.CoverageFingerprint)]
    [InlineData(AttachmentChange.ProfileGeneration)]
    [InlineData(AttachmentChange.FreshnessFingerprint)]
    [InlineData(AttachmentChange.CaptureInstant)]
    [InlineData(AttachmentChange.CaptureOffset)]
    [InlineData(AttachmentChange.ExpiryInstant)]
    [InlineData(AttachmentChange.ExpiryOffset)]
    [InlineData(AttachmentChange.PayloadText)]
    [InlineData(AttachmentChange.PayloadFormat)]
    [InlineData(AttachmentChange.PayloadVersion)]
    [InlineData(AttachmentChange.Order)]
    [InlineData(AttachmentChange.RemoveOne)]
    public async Task Higher_revision_owner_update_cannot_replace_any_original_attachment_value(AttachmentChange change) {
        var context = CaptureContext();
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(transientContext: context,
            contextAttachmentCodecs: StructureAdmittedContextFixture.Codecs());
        var original = (await fixture.NewStore().GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId))!.Run.ToolAdmission!;
        var changed = Change(RoundTrip(original), change);
        changed.Validate();
        Assert.NotEqual(JsonSerializer.Serialize(original.RuntimeContext), JsonSerializer.Serialize(changed.RuntimeContext));

        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() =>
            ((ISandboxWorkspaceExecutionRunMutationStore)fixture.NewStore()).UpdateExecutionRunDetailAsync(fixture.Session.ExecutionRunId,
                detail => detail with { Run = detail.Run with {
                    ToolAdmission = changed, Revision = detail.Run.Revision + 1
                } }));

        Assert.Equal("tool-admission.conflict", failure.Code);
        var saved = (await fixture.NewStore().GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId))!.Run.ToolAdmission!;
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(saved));
        Assert.Equal(AgentChatContextDigest.Compute(context),
            AgentChatContextDigest.Compute(fixture.NewJournal(fixture.NewStore()).RestoreRuntimeContext(saved.RuntimeContext!)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Detail_and_whole_execution_replacement_cannot_bypass_attachment_immutability(bool wholeExecution) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(transientContext: CaptureContext(),
            contextAttachmentCodecs: StructureAdmittedContextFixture.Codecs());
        var writer = fixture.NewStore();
        var detail = (await writer.GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId))!;
        var original = detail.Run.ToolAdmission!;
        var run = detail.Run with {
            ToolAdmission = Change(RoundTrip(original), AttachmentChange.PayloadText), Revision = detail.Run.Revision + 1
        };
        run.ToolAdmission!.Validate();
        AgentToolAdmissionException failure;
        if (wholeExecution) {
            var state = await writer.LoadExecutionAsync();
            failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => writer.SaveExecutionAsync(state with {
                ExecutionRuns = state.ExecutionRuns.Select(item => item.Id == run.Id ? run : item).ToArray()
            }));
        } else {
            failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => writer.SaveExecutionRunDetailAsync(detail with { Run = run }));
        }
        Assert.Equal("tool-admission.conflict", failure.Code);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(
            (await fixture.NewStore().GetExecutionRunDetailAsync(run.Id))!.Run.ToolAdmission));
    }

    [Fact]
    public async Task Detached_JSON_roundtrip_can_advance_the_real_journal_without_changing_order_or_payload() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(transientContext: CaptureContext(),
            contextAttachmentCodecs: StructureAdmittedContextFixture.Codecs());
        var original = (await fixture.NewStore().GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId))!.Run.ToolAdmission!;
        var detached = RoundTrip(original);
        var originalContext = original.RuntimeContext!;
        var detachedContext = detached.RuntimeContext!;
        Assert.NotSame(originalContext.Attachments[0], detachedContext.Attachments[0]);
        Assert.NotSame(originalContext.Attachments[0].Payload, detachedContext.Attachments[0].Payload);
        await ((ISandboxWorkspaceExecutionRunMutationStore)fixture.NewStore()).UpdateExecutionRunDetailAsync(fixture.Session.ExecutionRunId,
            detail => detail with { Run = detail.Run with {
                ToolAdmission = detached with { Revision = detached.Revision + 1 }, Revision = detail.Run.Revision + 1
            } });

        var journal = fixture.NewJournal(fixture.NewStore());
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
        }
        var saved = (await fixture.NewStore().GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId))!.Run.ToolAdmission!;
        Assert.Single(saved.Segments);
        Assert.True(saved.Revision > original.Revision);
        Assert.Equal(JsonSerializer.Serialize(original.RuntimeContext), JsonSerializer.Serialize(saved.RuntimeContext));
    }

    [Fact]
    public async Task Attachment_free_legacy_context_keeps_default_and_empty_arrays_compatible() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(
            transientContext: new("Original legacy context", WorkspaceScopeDescriptor.Sandbox));
        var original = fixture.Detail.Run.ToolAdmission!;
        Assert.DoesNotContain("Attachments", JsonSerializer.Serialize(original.RuntimeContext), StringComparison.Ordinal);
        foreach (var attachments in new[] { ImmutableArray<AgentToolAdmittedContextAttachment>.Empty, default }) {
            await ((ISandboxWorkspaceExecutionRunMutationStore)fixture.NewStore()).UpdateExecutionRunDetailAsync(fixture.Session.ExecutionRunId,
                detail => detail with { Run = detail.Run with {
                    ToolAdmission = detail.Run.ToolAdmission! with {
                        Revision = detail.Run.ToolAdmission.Revision + 1,
                        RuntimeContext = detail.Run.ToolAdmission.RuntimeContext! with { Attachments = attachments }
                    },
                    Revision = detail.Run.Revision + 1
                } });
        }
        var saved = (await fixture.NewStore().GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId))!.Run.ToolAdmission!;
        Assert.Equal(AgentToolJournalRecord.CurrentSchemaVersion, saved.SchemaVersion);
        Assert.Equal(JsonSerializer.Serialize(original.RuntimeContext), JsonSerializer.Serialize(saved.RuntimeContext));
        Assert.Empty(fixture.NewJournal().RestoreRuntimeContext(saved.RuntimeContext!).Attachments);
    }

    private static AgentRuntimeTransientContext CaptureContext() =>
        StructureAdmittedContextFixture.Capture(Guid.NewGuid(), Guid.NewGuid(), new(1)).Options.TransientContext!;

    private static AgentToolJournalRecord RoundTrip(AgentToolJournalRecord journal) =>
        JsonSerializer.Deserialize<AgentToolJournalRecord>(JsonSerializer.Serialize(journal))!;

    private static AgentToolJournalRecord Change(AgentToolJournalRecord journal, AttachmentChange change) {
        var context = journal.RuntimeContext!;
        var attachments = context.Attachments;
        Assert.Equal(2, attachments.Length);
        var original = attachments[0];
        var changed = change switch {
            AttachmentChange.Scope => original with { ScopeId = AgentChatContextScopeId.Create() },
            AttachmentChange.Source => original with { Source = new(original.Source.Kind, new(Guid.NewGuid().ToString("D"))) },
            AttachmentChange.Workspace => original with { WorkspaceScope = WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")) },
            AttachmentChange.Contributor => original with { ContributorId = new("tests.changed-contributor") },
            AttachmentChange.Kind => original with { Kind = new("tests.changed-kind") },
            AttachmentChange.Publication => original with { PublicationRevision = original.PublicationRevision.Next() },
            AttachmentChange.ContentFingerprint => original with { ContentFingerprint = new("changed-content") },
            AttachmentChange.CoverageFingerprint => original with { CoverageFingerprint = new("changed-coverage") },
            AttachmentChange.ProfileGeneration => original with { DatabaseProfileGeneration = new(original.DatabaseProfileGeneration.Value + 1) },
            AttachmentChange.FreshnessFingerprint => original with { FreshnessFingerprint = new("changed-freshness") },
            AttachmentChange.CaptureInstant => original with { CapturedAtUtc = original.CapturedAtUtc.AddTicks(1) },
            AttachmentChange.CaptureOffset => original with { CapturedAtUtc = original.CapturedAtUtc.ToOffset(TimeSpan.FromHours(1)) },
            AttachmentChange.ExpiryInstant => original with { FreshUntilUtc = original.FreshUntilUtc!.Value.AddTicks(1) },
            AttachmentChange.ExpiryOffset => original with { FreshUntilUtc = original.FreshUntilUtc!.Value.ToOffset(TimeSpan.FromHours(1)) },
            AttachmentChange.PayloadText => original with { Payload = AgentToolProtocolEnvelope.Create(original.Payload.Format,
                original.Payload.Version, original.Payload.PayloadJson + " ") },
            AttachmentChange.PayloadFormat => original with { Payload = AgentToolProtocolEnvelope.Create(original.Payload.Format + "-changed",
                original.Payload.Version, original.Payload.PayloadJson) },
            AttachmentChange.PayloadVersion => original with { Payload = AgentToolProtocolEnvelope.Create(original.Payload.Format,
                original.Payload.Version + 1, original.Payload.PayloadJson) },
            AttachmentChange.Order or AttachmentChange.RemoveOne => original,
            _ => throw new ArgumentOutOfRangeException(nameof(change), change, null)
        };
        return journal with {
            Revision = journal.Revision + 1,
            RuntimeContext = context with { Attachments = change switch {
                AttachmentChange.Order => [attachments[1], attachments[0]],
                AttachmentChange.RemoveOne => [attachments[0]],
                _ => attachments.SetItem(0, changed)
            } }
        };
    }

    public enum AttachmentChange {
        Scope,
        Source,
        Workspace,
        Contributor,
        Kind,
        Publication,
        ContentFingerprint,
        CoverageFingerprint,
        ProfileGeneration,
        FreshnessFingerprint,
        CaptureInstant,
        CaptureOffset,
        ExpiryInstant,
        ExpiryOffset,
        PayloadText,
        PayloadFormat,
        PayloadVersion,
        Order,
        RemoveOne
    }
}
