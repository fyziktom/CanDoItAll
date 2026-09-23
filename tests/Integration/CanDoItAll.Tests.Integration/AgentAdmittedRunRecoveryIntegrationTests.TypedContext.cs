using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench.ProjectStructure;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class AgentAdmittedRunRecoveryIntegrationTests {
    [Theory]
    [InlineData(ProjectStructureAgentChatView.Canvas)]
    [InlineData(ProjectStructureAgentChatView.Gantt)]
    public async Task Independent_file_restart_restores_exact_shared_context_through_the_real_Core_recovery_entry(ProjectStructureAgentChatView view) {
        var context = StructureAdmittedContextFixture.Capture(Guid.NewGuid(), Guid.NewGuid(), new(1), view).Options.TransientContext!;
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(transientContext: context,
            includeRecoveryInput: true, contextAttachmentCodecs: StructureAdmittedContextFixture.Codecs());
        var original = fixture.Detail.Run.ToolAdmission!;
        Assert.Equal(AgentToolJournalRecord.TypedContextSchemaVersion, original.SchemaVersion);
        await PrepareInterruptedAsync(fixture);
        var store = fixture.NewStore();
        var journal = fixture.NewJournal(store);
        var runtime = new RecoveryRuntime(journal, fixture);
        using var cache = new AgentExecutionPreparationCache(AgentExecutionPreparationCachePolicy.Default);
        using var service = CreateService(fixture, store, journal, new CurrentAuthority(fixture), runtime, cache);
        var completed = await service.RecoverExecutionRunAsync(fixture.Session.ExecutionRunId, AgentExecutionOperationId.New());
        Assert.Equal(ExecutionState.Completed, completed.State);
        Assert.Equal(1, runtime.Calls);
        Assert.Equal(AgentChatContextDigest.Compute(context), AgentChatContextDigest.Compute(runtime.Request!.ExecutionOptions!.TransientContext!));
        var saved = (await fixture.NewStore().GetExecutionRunDetailAsync(completed.ExecutionRunId))!.Run.ToolAdmission!;
        Assert.Equal(JsonSerializer.Serialize(original.RuntimeContext), JsonSerializer.Serialize(saved.RuntimeContext));
        Assert.Equal(AgentToolJournalRecord.TypedContextSchemaVersion, saved.SchemaVersion);
        Assert.Equal(completed.ResponseText,
            (await service.RecoverExecutionRunAsync(completed.ExecutionRunId, AgentExecutionOperationId.New())).ResponseText);
        Assert.Equal(1, runtime.Calls);
    }

    [Fact]
    public async Task Restart_without_the_original_attachment_owner_cannot_drop_the_saved_context() {
        var context = StructureAdmittedContextFixture.Capture(Guid.NewGuid(), Guid.NewGuid(), new(1)).Options.TransientContext!;
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(transientContext: context,
            includeRecoveryInput: true, contextAttachmentCodecs: StructureAdmittedContextFixture.Codecs());
        await PrepareInterruptedAsync(fixture);
        var store = fixture.NewStore();
        var journal = new AgentToolAdmissionJournal(store, fixture.Profile);
        var runtime = new RecoveryRuntime(journal, fixture);
        using var cache = new AgentExecutionPreparationCache(AgentExecutionPreparationCachePolicy.Default);
        using var service = CreateService(fixture, store, journal, new CurrentAuthority(fixture), runtime, cache);
        var before = (await store.GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId))!.Run.ToolAdmission!;
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => service.RecoverExecutionRunAsync(
            fixture.Session.ExecutionRunId, AgentExecutionOperationId.New()));
        Assert.Equal(0, runtime.Calls);
        var after = (await fixture.NewStore().GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId))!.Run.ToolAdmission!;
        Assert.Equal(JsonSerializer.Serialize(before.RuntimeContext), JsonSerializer.Serialize(after.RuntimeContext));
        Assert.Equal(JsonSerializer.Serialize(before.Segments), JsonSerializer.Serialize(after.Segments));
        Assert.Equal(JsonSerializer.Serialize(before.Batches), JsonSerializer.Serialize(after.Batches));
        Assert.NotNull(after.ActiveDispatchLeaseId);
        Assert.NotEqual(before.ActiveDispatchLeaseId, after.ActiveDispatchLeaseId);
        Assert.Equal(before.Revision + 1, after.Revision);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var independent = fixture.NewJournal(fixture.NewStore());
        await using var reacquired = await independent.AcquireRunAsync(fixture.Session, timeout.Token);
        Assert.NotEqual(after.ActiveDispatchLeaseId, reacquired.Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Actual_new_Core_chat_turn_admits_known_shared_context_and_keeps_unknown_input_request_scoped(bool knownOwner) {
        var invocation = StructureAdmittedContextFixture.Capture(Guid.NewGuid(), Guid.NewGuid(), new(1));
        var original = invocation.Options.TransientContext!;
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(transientContext: original,
            includeRecoveryInput: true, contextAttachmentCodecs: StructureAdmittedContextFixture.Codecs());
        await PrepareInterruptedAsync(fixture);
        var store = fixture.NewStore();
        var journal = fixture.NewJournal(store);
        var runtime = new RecoveryRuntime(journal, fixture, requireOriginal: false);
        using var cache = new AgentExecutionPreparationCache(AgentExecutionPreparationCachePolicy.Default);
        using var service = CreateService(fixture, store, journal, new CurrentAuthority(fixture), runtime, cache);
        await service.RecoverExecutionRunAsync(fixture.Session.ExecutionRunId, AgentExecutionOperationId.New());
        var context = original;
        if (!knownOwner) {
            var captured = original.Attachments[0];
            var unknown = new AgentChatContextAttachmentDraft(new("tests.unregistered"), captured.ContentFingerprint,
                captured.CoverageFingerprint, captured.DatabaseProfileGeneration, captured.FreshnessFingerprint,
                captured.CapturedAtUtc, captured.FreshUntilUtc, new UnknownContextAttachment()).CreateEnvelope(
                    captured.ScopeId, captured.Source, captured.WorkspaceScope, captured.ContributorId, captured.PublicationRevision);
            context = new(original.Content, original.WorkspaceScope, [unknown]);
        }
        var options = invocation.Options with {
            InitialActivityOperationId = AgentExecutionOperationId.New(), TransientContext = context,
            Context = invocation.Options.Context! with { MetadataJson = fixture.Detail.Run.MetadataJson }
        };
        var result = await service.SendMessageAsync(fixture.Agent.Id, fixture.Session.ChatSessionId,
            "A new turn with the original captured surface.", options);
        Assert.NotEqual(fixture.Session.ExecutionRunId, result.ExecutionRunId);
        var saved = (await fixture.NewStore().GetExecutionRunDetailAsync(result.ExecutionRunId))!.Run.ToolAdmission!;
        Assert.Equal(knownOwner ? AgentToolAdmissionSupport.Recoverable : AgentToolAdmissionSupport.RequestScopedInput, saved.Support);
        Assert.Equal(knownOwner ? AgentToolJournalRecord.TypedContextSchemaVersion : AgentToolJournalRecord.CurrentSchemaVersion, saved.SchemaVersion);
        if (knownOwner) {
            Assert.Equal(AgentChatContextDigest.Compute(context), AgentChatContextDigest.Compute(journal.RestoreRuntimeContext(saved.RuntimeContext!)));
            Assert.NotNull(runtime.Request!.ExecutionOptions!.AdmittedToolSession);
        } else {
            Assert.Null(saved.RuntimeContext);
            Assert.Empty(saved.Segments);
            Assert.Empty(saved.Batches);
        }
    }

    [Theory]
    [InlineData(AgentToolJournalRecord.CurrentSchemaVersion)]
    [InlineData(AgentToolJournalRecord.BackgroundSchemaVersion)]
    [InlineData(AgentToolJournalRecord.ProviderDispatchSchemaVersion)]
    public async Task Typed_context_cannot_be_encoded_as_an_older_reader_journal(int version) {
        var context = StructureAdmittedContextFixture.Capture(Guid.NewGuid(), Guid.NewGuid(), new(1)).Options.TransientContext!;
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(transientContext: context,
            contextAttachmentCodecs: StructureAdmittedContextFixture.Codecs());
        Assert.Throws<InvalidDataException>(() => (fixture.Detail.Run.ToolAdmission! with { SchemaVersion = version }).Validate());
    }

    [Fact]
    public async Task Adding_real_provider_dispatch_evidence_retains_the_typed_context_version_and_original_payload() {
        var context = StructureAdmittedContextFixture.Capture(Guid.NewGuid(), Guid.NewGuid(), new(1)).Options.TransientContext!;
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(transientContext: context,
            contextAttachmentCodecs: StructureAdmittedContextFixture.Codecs());
        var journal = fixture.NewJournal();
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            var segment = await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
            var digest = AgentToolProtocolEnvelope.ComputeDigest("original provider request");
            var dispatch = await journal.BeginProviderDispatchAsync(lease, segment.Segments[^1].Id, digest, digest, [], default);
            var saved = await journal.CompleteProviderDispatchAsync(lease, dispatch.Id, digest,
                AgentToolAdmissionJournalFixture.Envelope(), [], default);
            Assert.Equal(AgentToolJournalRecord.TypedContextSchemaVersion, saved.SchemaVersion);
            saved.Validate();
        }
        var restarted = (await fixture.NewStore().GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId))!.Run.ToolAdmission!;
        Assert.Equal(AgentToolJournalRecord.TypedContextSchemaVersion, restarted.SchemaVersion);
        Assert.Equal(AgentToolProviderDispatchState.ResponseAdmitted, Assert.Single(restarted.ProviderDispatches).State);
        Assert.Equal(AgentChatContextDigest.Compute(context), AgentChatContextDigest.Compute(fixture.NewJournal(fixture.NewStore()).RestoreRuntimeContext(restarted.RuntimeContext!)));
    }

    private sealed record UnknownContextAttachment : IAgentChatContextAttachment;
}
