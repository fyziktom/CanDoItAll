using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Persistence;

public sealed partial class MigrationAuthorityIntegrationTests {
    private static async Task<OwnedHostRetainedHistory> SeedOwnedHostHistoryAsync(IServiceProvider services,
        AppDbContext context, RetainedGraph graph, Guid providerId, string tag, string model, CancellationToken cancellationToken) {
        var definitions = services.GetRequiredService<ILlmChatDefinitionApplicationService>();
        var first = OwnedHostValue(await definitions.CreateAsync(new(
            $"Synthetic retained chat R1 {tag}", "Synthetic pre-upgrade definition", string.Empty,
            $"Retained revision one for H-{tag}. Answer the supplied synthetic text briefly.", providerId, model,
            new LlmModelSettings(0), null, null, "Synthetic historical revision one", []), cancellationToken));
        var active = OwnedHostValue(await definitions.ChangeStatusAsync(new(first.Definition.Id,
            LlmChatDefinitionStatus.Active, first.Definition.ConcurrencyToken), cancellationToken));
        var conversations = services.GetRequiredService<ILlmChatConversationApplicationService>();
        var conversation = OwnedHostValue(await conversations.CreateAsync(new(active.Definition.Id,
            $"Synthetic retained conversation {tag}", LlmChatConversationOrigin.Api), cancellationToken));
        var second = OwnedHostValue(await definitions.UpdateAsync(new(active.Definition.Id,
            $"Synthetic retained chat R2 {tag}", "Current definition with retained historical conversation", string.Empty,
            $"Revision two for H-{tag}. Start new conversations with this instruction.", providerId, model,
            new LlmModelSettings(0), null, null, "Synthetic historical revision two", active.Definition.ConcurrencyToken, []), cancellationToken));
        Assert.Equal(1, conversation.Conversation.DefinitionRevision.Value);
        Assert.Equal(2, second.Definition.CurrentRevision.Value);
        Assert.NotEqual(first.Revision.SystemPrompt, second.Revision.SystemPrompt);
        var provider = Assert.IsType<ProviderProfile>(await services.GetRequiredService<IProviderProfileRegistry>().GetProviderAsync(providerId, cancellationToken));
        var priceHash = ProviderPricingSnapshot.CreateProfileHash(provider);
        var operationId = new LlmChatOperationId(Guid.NewGuid());
        var usage = new LlmUsage(11, 7, 2);
        const decimal priceUsd = 0.000123m;
        var userText = $"Synthetic retained user turn H-{tag}.";
        var assistantText = $"Synthetic retained assistant turn H-{tag}.";
        await using (var lease = await services.GetRequiredService<ILlmChatRuntimeLeaseFactory>().AcquireAsync(cancellationToken)) {
            Assert.True(lease.EnsureCurrent().IsSuccess);
            using var operationScope = services.GetRequiredService<ILlmChatOperationScopeAccessor>().Push(new(operationId, lease.Identity));
            await services.GetRequiredService<ILlmChatUnitOfWork>().ExecuteAsync(async token => {
                var engine = services.GetRequiredService<ILlmChatConversationEngine>();
                var admitted = await engine.AdmitTurnAsync(conversation.Conversation.Id, operationId, second.Definition,
                    first.Revision, userText, conversation.Transcript.TranscriptRevision, token);
                var completed = await engine.CompleteTurnAsync(admitted, new LlmInvocationResult(model, assistantText, usage), token);
                var started = admitted.UserEntry.CreatedAtUtc;
                var finished = completed.State.UpdatedAtUtc;
                var operation = new LlmChatOperation(operationId, conversation.Conversation.Id, LlmChatOperationKind.SendTurn,
                    LlmChatFingerprints.CreateRequest(conversation.Conversation.Id, conversation.Transcript.TranscriptRevision,
                        userText, first.Revision.SettingsFingerprint), conversation.Transcript.TranscriptRevision,
                    LlmChatOperationStatus.Succeeded, started, 0) {
                    TurnAdmittedAtUtc = started, ProviderDispatchStartedAtUtc = started,
                    ProviderDispatchReturnedAtUtc = finished, TranscriptCompletedAtUtc = finished, CompletedAtUtc = finished,
                    ResultingTranscriptRevision = completed.State.TranscriptRevision, AssistantEntryId = completed.AssistantEntryId,
                    DispatchPhase = LlmChatDispatchPhase.ProviderDispatchReturned
                };
                Assert.True((await services.GetRequiredService<ILlmChatOperationRepository>().AdmitAsync(operation, token)).Created);
                await services.GetRequiredService<ILlmChatInvocationRecordRepository>().AppendAsync(new(operationId, providerId,
                    first.Revision.ProviderKind, first.Revision.ProviderName, model, null, null, 1, usage,
                    LlmChatInvocationOutcome.Succeeded, string.Empty, started, finished, $"synthetic-h-history:{operationId.Value:N}",
                    finishReason: "completed", pricingStatus: LlmChatInvocationPricingEvidenceStatus.ProviderReported,
                    providerCostUsd: priceUsd, pricingProfileHash: priceHash, pricingVersion: ProviderPricingSnapshot.Version), token);
                return true;
            }, lease.CancellationToken);
        }
        var partition = await services.GetRequiredService<IProviderHistoryPartition>().GetAsync(cancellationToken);
        var source = new CanonicalEvidenceReference(partition, HistorySourceKind.SimpleChat,
            new(operationId.Value.ToString("N")), new("1"));
        var historyId = HistoryEntryId.ForCanonical(new(source.Kind, source.Owner, source.Evidence));
        HistorySourceMutation queuedProjection;
        await using (var history = await services.GetRequiredService<IDbContextFactory<ProviderHistoryDbContext>>().CreateDbContextAsync(cancellationToken)) {
            var queued = Assert.Single(await history.Set<HistoryOutboxRow>().AsNoTracking().ToArrayAsync(cancellationToken));
            Assert.Equal(source, queued.Mutation.Source);
            queuedProjection = queued.Mutation;
        }
        var persistedProjection = Assert.IsType<HistorySourceMutation>(await services.GetRequiredService<LlmChatHistorySource>().ReadAsync(source, cancellationToken));
        Assert.Equal(queuedProjection.Entry!.Price, persistedProjection.Entry!.Price);
        Assert.Equal(HashOwnedHostValue(JsonSerializer.Serialize(queuedProjection)), HashOwnedHostValue(JsonSerializer.Serialize(persistedProjection)));
        Assert.Equal(1, await services.GetRequiredService<HistoryOutboxProcessor>().ProcessAsync(partition, 1, cancellationToken));
        var collaboration = services.GetRequiredService<CollaborationService>();
        var threadId = OwnedHostValue(await collaboration.CreateThreadAsync(new(
            $"Synthetic retained collaboration {tag}", CollaborationContextKind.ProcessRun, graph.ProcessRunId, graph.ProjectId,
            "Synthetic retained process discussion", null, CollaborationInboxItemKind.Notification,
            $"synthetic-h:{tag}:first", "Synthetic first participant", CollaborationParticipantKind.User,
            "Synthetic retained opening message.", CollaborationMessageKind.Standard), cancellationToken));
        Assert.True((await collaboration.AppendMessageAsync(new(threadId, $"synthetic-h:{tag}:second",
            "Synthetic second participant", CollaborationMessageAuthorKind.User, "Synthetic retained reply.",
            CollaborationMessageKind.Standard), cancellationToken)).IsSuccess);
        var identity = new OwnedHostHistoryIdentity(first.Definition.Id.Value, conversation.Conversation.Id.Value,
            operationId.Value, threadId, partition, historyId.Value);
        var readback = await ReadOwnedHostHistoryAsync(services, identity, cancellationToken);
        Assert.Equal([LlmMessageRole.System, LlmMessageRole.User, LlmMessageRole.Assistant], readback.Transcript.Entries.Select(entry => entry.Role));
        Assert.Equal(first.Revision.SystemPrompt, readback.Transcript.Entries[0].Text);
        Assert.Equal(userText, readback.Transcript.Entries[1].Text);
        Assert.Equal(assistantText, readback.Transcript.Entries[2].Text);
        Assert.Equal(usage, readback.Transcript.Entries[2].Usage);
        Assert.Equal(usage, Assert.Single(readback.Operation.Invocations).Usage);
        Assert.Equal(priceUsd, readback.HistoryEntry.Amount);
        Assert.Equal(priceHash, readback.HistoryEntry.PriceHash);
        Assert.Equal(ProviderPricingSnapshot.Version, readback.HistoryEntry.PriceVersion);
        Assert.Equal(HistoryPriceState.ProviderReported, readback.HistoryEntry.PriceState);
        Assert.Equal((11L, 7L, 2L), (readback.HistoryEntry.InputTokens, readback.HistoryEntry.OutputTokens, readback.HistoryEntry.CachedInputTokens));
        Assert.Equal(graph.ProjectId, readback.Collaboration.ProjectId);
        Assert.Equal(graph.ProcessRunId, readback.Collaboration.ContextId);
        var rows = await ReadOwnedHostHistoryRowsAsync(context, identity, cancellationToken);
        Assert.Equal(10, rows.Chats.Length);
        Assert.Equal(6, rows.Collaboration.Length);
        Assert.Equal(3, rows.History.Length);
        var descriptor = new OwnedHostHistoryDescriptor(identity, readback.Inbox.ItemId,
            readback.Transcript.TranscriptRevision, readback.Transcript.Entries.Select(entry => entry.EntryId).ToArray(),
            usage, priceUsd, priceHash, ProviderPricingSnapshot.Version,
            HashOwnedHostValue(string.Join('\n', rows.Chats)), HashOwnedHostValue(JsonSerializer.Serialize(readback.Transcript, GraphJson)),
            HashOwnedHostValue(string.Join('\n', rows.Collaboration)), HashOwnedHostValue(string.Join('\n', rows.History)));
        var ownerJson = JsonSerializer.Serialize(readback, GraphJson);
        return new(descriptor, async (readServices, database, token) => {
            var observedRows = await ReadOwnedHostHistoryRowsAsync(database, identity, token);
            Assert.Equal(rows.Chats, observedRows.Chats);
            Assert.Equal(rows.Collaboration, observedRows.Collaboration);
            Assert.Equal(rows.History, observedRows.History);
            Assert.Equal(ownerJson, JsonSerializer.Serialize(await ReadOwnedHostHistoryAsync(readServices, identity, token), GraphJson));
            return new(identity, 10, 6, 3, DateTimeOffset.UtcNow);
        });
    }

    private static T OwnedHostValue<T>(Result<T> result) {
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private static async Task<OwnedHostHistoryReadback> ReadOwnedHostHistoryAsync(IServiceProvider services,
        OwnedHostHistoryIdentity identity, CancellationToken cancellationToken) {
        var definitions = services.GetRequiredService<ILlmChatDefinitionApplicationService>();
        var head = OwnedHostValue(await definitions.GetAsync(new(identity.DefinitionId), cancellationToken));
        var first = OwnedHostValue(await definitions.GetRevisionAsync(new(identity.DefinitionId), new(1), cancellationToken));
        var second = OwnedHostValue(await definitions.GetRevisionAsync(new(identity.DefinitionId), new(2), cancellationToken));
        var conversation = OwnedHostValue(await services.GetRequiredService<ILlmChatConversationApplicationService>()
            .GetAsync(new(identity.ConversationId), new LlmChatTranscriptQuery(10), cancellationToken));
        var transcript = Assert.IsType<LlmConversationDocument>(await services.GetRequiredService<ILlmChatRuntimePersistenceBoundary>()
            .ConversationStore.TryGetAsync(identity.ConversationId, cancellationToken));
        var operation = Assert.IsType<LlmChatOperationReadModel>(await services.GetRequiredService<ILlmChatOperationReadStore>()
            .TryGetAsync(new(identity.OperationId), cancellationToken));
        Assert.Equal(2, head.Definition.CurrentRevision.Value);
        Assert.Equal(LlmChatDefinitionStatus.Active, head.Definition.Status);
        Assert.Equal(1, conversation.Conversation.DefinitionRevision.Value);
        Assert.Equal(identity.DefinitionId, conversation.Conversation.DefinitionId.Value);
        Assert.Null(transcript.ActiveTurn);
        Assert.False(conversation.Transcript.HasActiveTurn);
        Assert.Equal(LlmChatOperationStatus.Succeeded, operation.Operation.Status);
        Assert.Equal(identity.ConversationId, operation.Operation.ConversationId.Value);
        Assert.Equal(2, conversation.TranscriptMessages.Count);
        var workspace = await services.GetRequiredService<CollaborationService>().GetWorkspaceAsync(identity.CollaborationThreadId, cancellationToken);
        var thread = Assert.IsType<CollaborationThreadDetailModel>(workspace.SelectedThread);
        var inbox = Assert.Single(workspace.InboxItems, item => item.ThreadId == identity.CollaborationThreadId);
        Assert.Equal(2, thread.Participants.Count);
        Assert.Equal(2, thread.Messages.Count);
        Assert.Equal(CollaborationThreadState.Open, thread.State);
        Assert.True(inbox.IsUnread);
        Assert.Equal(2, inbox.UnreadCount);
        await using var history = await services.GetRequiredService<IDbContextFactory<ProviderHistoryDbContext>>().CreateDbContextAsync(cancellationToken);
        var entry = await history.Set<HistoryEntryRow>().AsNoTracking().SingleAsync(item => item.Id == identity.HistoryEntryId, cancellationToken);
        var owner = await history.Set<HistoryOwnerRow>().AsNoTracking().SingleAsync(item => item.EntryId == identity.HistoryEntryId, cancellationToken);
        var source = await history.Set<HistorySourceRow>().AsNoTracking().SingleAsync(item => item.Id == owner.SourceId, cancellationToken);
        Assert.Equal(identity.Partition.StorageLineageId, entry.PartitionId);
        Assert.Equal(HistorySourceKind.SimpleChat, source.Kind);
        Assert.Equal(identity.OperationId.ToString("N"), source.OwnerId);
        Assert.Equal("1", source.EvidenceId);
        Assert.Equal(1, source.Version);
        Assert.Equal(HistoryOwnerRole.ContentOwner, owner.Role);
        Assert.Equal(HistoryOwnerState.Linked, owner.State);
        return new(head, first, second, conversation, transcript, operation, thread with {
            Participants = thread.Participants.OrderBy(item => item.ParticipantId).ToArray(),
            Messages = thread.Messages.OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.MessageId).ToArray()
        }, inbox, entry, source, owner);
    }

    private static async Task<(string[] Chats, string[] Collaboration, string[] History)> ReadOwnedHostHistoryRowsAsync(
        AppDbContext context, OwnedHostHistoryIdentity identity, CancellationToken cancellationToken) {
        var chats = await context.Database.SqlQuery<string>($"""
            SELECT 'definition:' || to_jsonb(row)::text AS "Value" FROM "LlmChats_Definitions" row WHERE "Id" = {identity.DefinitionId}
            UNION ALL SELECT 'revision:' || to_jsonb(row)::text FROM "LlmChats_DefinitionRevisions" row WHERE "DefinitionId" = {identity.DefinitionId}
            UNION ALL SELECT 'conversation:' || to_jsonb(row)::text FROM "LlmChats_Conversations" row WHERE "Id" = {identity.ConversationId}
            UNION ALL SELECT 'transcript:' || to_jsonb(row)::text FROM "LlmChats_Transcripts" row WHERE "ConversationId" = {identity.ConversationId}
            UNION ALL SELECT 'message:' || to_jsonb(row)::text FROM "LlmChats_Messages" row WHERE "ConversationId" = {identity.ConversationId}
            UNION ALL SELECT 'operation:' || to_jsonb(row)::text FROM "LlmChats_Operations" row WHERE "Id" = {identity.OperationId}
            UNION ALL SELECT 'invocation:' || to_jsonb(row)::text FROM "LlmChats_InvocationRecords" row WHERE "OperationId" = {identity.OperationId}
            """).OrderBy(value => value).ToArrayAsync(cancellationToken);
        var collaboration = await context.Database.SqlQuery<string>($"""
            SELECT 'thread:' || to_jsonb(row)::text AS "Value" FROM "Collaboration_Threads" row WHERE "Id" = {identity.CollaborationThreadId}
            UNION ALL SELECT 'participant:' || to_jsonb(row)::text FROM "Collaboration_Participants" row WHERE "ThreadId" = {identity.CollaborationThreadId}
            UNION ALL SELECT 'message:' || to_jsonb(row)::text FROM "Collaboration_Messages" row WHERE "ThreadId" = {identity.CollaborationThreadId}
            UNION ALL SELECT 'inbox:' || to_jsonb(row)::text FROM "Collaboration_InboxItems" row WHERE "ThreadId" = {identity.CollaborationThreadId}
            """).OrderBy(value => value).ToArrayAsync(cancellationToken);
        var history = await context.Database.SqlQuery<string>($"""
            SELECT 'entry:' || to_jsonb(row)::text AS "Value" FROM "ProviderHistory_Entries" row WHERE "Id" = {identity.HistoryEntryId}
            UNION ALL SELECT 'owner:' || to_jsonb(row)::text FROM "ProviderHistory_Owners" row WHERE "EntryId" = {identity.HistoryEntryId}
            UNION ALL SELECT 'source:' || to_jsonb(row)::text FROM "ProviderHistory_Sources" row
                WHERE "Id" IN (SELECT "SourceId" FROM "ProviderHistory_Owners" WHERE "EntryId" = {identity.HistoryEntryId})
            """).OrderBy(value => value).ToArrayAsync(cancellationToken);
        return (chats, collaboration, history);
    }

    public sealed record OwnedHostHistoryIdentity(Guid DefinitionId, Guid ConversationId, Guid OperationId,
        Guid CollaborationThreadId, HistoryPartition Partition, Guid HistoryEntryId);

    public sealed record OwnedHostHistoryDescriptor(OwnedHostHistoryIdentity Identity, Guid InboxItemId,
        long TranscriptRevision, Guid[] TranscriptEntryIds, LlmUsage SyntheticUsage, decimal SyntheticPriceUsd,
        string HistoricalPriceProfileHash, string HistoricalPriceVersion, string ChatRowsHash, string FullTranscriptHash,
        string CollaborationRowsHash, string CanonicalHistoryRowsHash);

    public sealed record OwnedHostHistoryVerification(OwnedHostHistoryIdentity Identity, int PreservedChatRows,
        int PreservedCollaborationRows, int PreservedCanonicalHistoryRows, DateTimeOffset VerifiedAtUtc);

    private sealed record OwnedHostRetainedHistory(OwnedHostHistoryDescriptor Descriptor,
        Func<IServiceProvider, AppDbContext, CancellationToken, Task<OwnedHostHistoryVerification>> VerifyAsync);

    private sealed record OwnedHostHistoryReadback(LlmChatDefinitionDetails Definition,
        LlmChatDefinitionRevision FirstRevision, LlmChatDefinitionRevision SecondRevision,
        LlmChatConversationDetails Conversation, LlmConversationDocument Transcript, LlmChatOperationReadModel Operation,
        CollaborationThreadDetailModel Collaboration, CollaborationInboxItemSummary Inbox,
        HistoryEntryRow HistoryEntry, HistorySourceRow HistorySource, HistoryOwnerRow HistoryOwner);
}
